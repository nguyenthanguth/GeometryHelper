using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace GeometryHelper.Viewer.Scripting
{
    /// <summary>
    /// What the globals of a script are: the scene it builds, the tolerance it works at, and the two calls that show a shape
    /// and say a line. A script writes <c>Show(loop)</c> or <c>Scene.Add(loop, layer: "source")</c>, <c>Note("done")</c> and
    /// reads or sets <c>Tol</c>.
    /// </summary>
    public sealed class ScriptGlobals
    {
        private readonly Action<string> _note;
        private IDisposable _scope;
        private Tolerance _tolerance;

        internal ScriptGlobals(SceneWriter scene, Action<string> note, CancellationToken cancel)
        {
            Scene = scene;
            _note = note;
            Cancel = cancel;
            scene.Cancellation = cancel;
            Apply(Tolerance.Global);
        }

        /// <summary>
        /// Gets the token that Esc cancels. <c>Show</c>, <c>Note</c>, <c>Scene.Add</c> and setting <c>Tol</c> throw when it is
        /// cancelled, which stops a loop that calls them; a long loop that calls none should test <c>Cancel.IsCancellationRequested</c>
        /// or call <c>Cancel.ThrowIfCancellationRequested()</c> itself.
        /// </summary>
        public CancellationToken Cancel { get; }

        /// <summary>
        /// Gets the scene being built; <c>Show</c> adds to it.
        /// </summary>
        public SceneWriter Scene { get; }

        /// <summary>
        /// Gets or sets the tolerance the script works at, which the library reads for the rest of the script on its thread
        /// and the scene is opened at, all four thresholds of it. It starts as the tolerance in force when the script starts.
        /// </summary>
        public Tolerance Tol
        {
            get => _tolerance;
            set
            {
                Cancel.ThrowIfCancellationRequested();
                Apply(value);
            }
        }

        private void Apply(Tolerance value)
        {
            // A scope has to be closed on the thread that opened it, and a script runs on one thread, so changing the
            // tolerance closes the old scope and opens a new one right here.
            _scope?.Dispose();
            _tolerance = value;
            _scope = Tolerance.Use(value);
            Scene.Scene.SetTolerance(value);
        }

        /// <summary>
        /// Adds a shape, or a sequence of shapes, to the scene.
        /// </summary>
        /// <param name="geo">A library shape, or an array or list of them.</param>
        /// <param name="layer">The layer; "default" when none is given.</param>
        /// <param name="name">The name in the scene tree.</param>
        /// <param name="color">The colour as #RRGGBB.</param>
        /// <param name="props">Properties to show with the item.</param>
        public void Show(object geo, string layer = null, string name = null, string color = null, object props = null)
        {
            Cancel.ThrowIfCancellationRequested();
            Scene.Add(geo, layer, name, color, props);
        }

        /// <summary>
        /// Says a line in the output pane.
        /// </summary>
        /// <param name="text">The line.</param>
        public void Note(string text)
        {
            Cancel.ThrowIfCancellationRequested();
            _note?.Invoke(text ?? string.Empty);
        }

        internal void Close()
        {
            try
            {
                _scope?.Dispose();
            }
            catch (InvalidOperationException)
            {
                // A scope closed on another thread than the one that opened it cannot be; the thread ends and takes it with it.
            }

            _scope = null;
        }
    }

    /// <summary>
    /// How a script run ended.
    /// </summary>
    public sealed class ScriptRunResult
    {
        internal ScriptRunResult(bool succeeded, Scene scene, IReadOnlyList<string> notes, IReadOnlyList<string> errors, TimeSpan elapsed)
        {
            Succeeded = succeeded;
            Scene = scene;
            Notes = notes;
            Errors = errors;
            Elapsed = elapsed;
        }

        /// <summary>
        /// Gets whether the script compiled and ran to its end.
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// Gets the scene the script built; whatever it had shown before it stopped, where it did not succeed.
        /// </summary>
        public Scene Scene { get; }

        /// <summary>
        /// Gets the lines the script said with <c>Note</c>.
        /// </summary>
        public IReadOnlyList<string> Notes { get; }

        /// <summary>
        /// Gets the errors: each compile error on a line of its own with the file and line number, or the exception the script
        /// ended with and the line it was thrown from.
        /// </summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>
        /// Gets how long compiling and running took.
        /// </summary>
        public TimeSpan Elapsed { get; }
    }

    /// <summary>
    /// Compiles and runs a C# script with Roslyn on a worker thread.
    /// </summary>
    public static class ScriptHost
    {
        private static readonly Regex _scriptLine = new Regex(@" in (.+\.csx):line (\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Runs a script on a worker thread of its own, at the tolerance it asks for, and gives back its scene.
        /// </summary>
        /// <param name="code">The text of the script.</param>
        /// <param name="path">The path of the script, which the line numbers of errors name and <c>#load</c> and <c>#r</c> start from; null for none.</param>
        /// <param name="note">Told each line the script says, as it says it; may be null.</param>
        /// <param name="cancel">Stops the script. The script is cancelled while it is compiled and as it starts; a statement already running ends first.</param>
        /// <returns>The result: never an exception from the script, which comes back in <see cref="ScriptRunResult.Errors"/>.</returns>
        public static Task<ScriptRunResult> RunAsync(string code, string path, Action<string> note, CancellationToken cancel)
        {
            var completion = new TaskCompletionSource<ScriptRunResult>();

            // A deep recursion in a geometry routine needs more than the default megabyte, so the thread gets 64.
            var thread = new Thread(() =>
            {
                try
                {
                    completion.SetResult(Run(code, path, note, cancel));
                }
                catch (Exception e)
                {
                    completion.SetException(e);
                }
            }, 64 * 1024 * 1024)
            {
                IsBackground = true,
                Name = "GeometryHelper.Viewer script",
            };

            thread.Start();
            return completion.Task;
        }

        private static ScriptRunResult Run(string code, string path, Action<string> note, CancellationToken cancel)
        {
            DateTime started = DateTime.UtcNow;
            var notes = new List<string>();
            var errors = new List<string>();
            string title = path == null ? "script" : Path.GetFileName(path);
            var writer = new SceneWriter(title);

            var globals = new ScriptGlobals(
                writer,
                line =>
                {
                    lock (notes)
                    {
                        notes.Add(line);
                    }

                    note?.Invoke(line);
                },
                cancel);

            bool succeeded = false;

            try
            {
                ScriptOptions options = Options(path);
                // Debug information needs the source text to have an encoding, which a stream of UTF-8 gives it and a string does not.
                Script<object> script = CSharpScript.Create(new MemoryStream(new UTF8Encoding(false).GetBytes(code ?? string.Empty)), options, typeof(ScriptGlobals));
                var diagnostics = script.Compile(cancel);
                bool compiled = true;

                foreach (Diagnostic diagnostic in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                {
                    errors.Add(Describe(diagnostic, path));
                    compiled = false;
                }

                if (compiled)
                {
                    RunOnPump(script, globals, cancel);
                    succeeded = true;
                }
            }
            catch (OperationCanceledException)
            {
                errors.Add("The script was stopped.");
            }
            catch (CompilationErrorException e)
            {
                foreach (Diagnostic diagnostic in e.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                {
                    errors.Add(Describe(diagnostic, path));
                }
            }
            catch (Exception e)
            {
                errors.Add(DescribeException(e, path));
            }
            finally
            {
                globals.Close();
            }

            return new ScriptRunResult(succeeded, writer.Scene, notes, errors, DateTime.UtcNow - started);
        }

        /// <summary>
        /// Runs the script on this thread under a context that brings what follows an <c>await</c> back to this thread, which
        /// holds the tolerance scope: a continuation on a pool thread would run at the tolerance of the process and close the
        /// scope on the wrong thread.
        /// </summary>
        private static void RunOnPump(Script<object> script, ScriptGlobals globals, CancellationToken cancel)
        {
            SynchronizationContext previous = SynchronizationContext.Current;
            var pump = new Pump(Thread.CurrentThread);
            SynchronizationContext.SetSynchronizationContext(pump);

            try
            {
                Task<ScriptState<object>> task = script.RunAsync(globals, cancel);
                task.ContinueWith(_ => pump.Complete(), TaskScheduler.Default);
                pump.Run();
                task.GetAwaiter().GetResult();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        private sealed class Pump : SynchronizationContext
        {
            private readonly BlockingCollection<KeyValuePair<SendOrPostCallback, object>> _queue = new BlockingCollection<KeyValuePair<SendOrPostCallback, object>>();
            private readonly Thread _thread;

            internal Pump(Thread thread)
            {
                _thread = thread;
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                try
                {
                    _queue.Add(new KeyValuePair<SendOrPostCallback, object>(d, state));
                }
                catch (InvalidOperationException)
                {
                    // The script has ended; nothing is waiting for this.
                }
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                if (Thread.CurrentThread == _thread)
                {
                    d(state);
                    return;
                }

                using (var done = new ManualResetEventSlim())
                {
                    Post(s =>
                    {
                        d(s);
                        done.Set();
                    }, state);
                    done.Wait();
                }
            }

            public override SynchronizationContext CreateCopy() => this;

            internal void Complete() => _queue.CompleteAdding();

            internal void Run()
            {
                foreach (KeyValuePair<SendOrPostCallback, object> work in _queue.GetConsumingEnumerable())
                {
                    work.Key(work.Value);
                }
            }
        }

        private static string Describe(Diagnostic diagnostic, string path)
        {
            FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
            string file = !string.IsNullOrEmpty(span.Path) ? span.Path : path ?? "script";

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}({1},{2}): error {3}: {4}",
                file,
                span.StartLinePosition.Line + 1,
                span.StartLinePosition.Character + 1,
                diagnostic.Id,
                diagnostic.GetMessage(CultureInfo.InvariantCulture));
        }

        private static string DescribeException(Exception e, string path)
        {
            Exception inner = e;

            while (inner is AggregateException aggregate && aggregate.InnerException != null)
            {
                inner = aggregate.InnerException;
            }

            // The file and line of the script an exception came from are in its stack trace, since a script is compiled with its
            // path and debug information; a file loaded with #load names itself there.
            string file = null;
            string line = null;

            if (inner.StackTrace != null)
            {
                foreach (string frame in inner.StackTrace.Split('\n'))
                {
                    Match match = _scriptLine.Match(frame);

                    if (match.Success)
                    {
                        file = match.Groups[1].Value;
                        line = match.Groups[2].Value;
                        break;
                    }
                }
            }

            var text = new StringBuilder();
            text.Append(file ?? path ?? "script");

            if (line != null)
            {
                text.Append('(').Append(line).Append(')');
            }

            text.Append(": ").Append(inner.GetType().Name).Append(": ").Append(inner.Message);            return text.ToString();
        }

        private static ScriptOptions Options(string path)
        {
            string directory = path == null ? Environment.CurrentDirectory : Path.GetDirectoryName(Path.GetFullPath(path));

            ScriptOptions options = ScriptOptions.Default
                .WithReferences(References())
                .WithImports(Imports())
                .WithEmitDebugInformation(true)
                .WithSourceResolver(new SourceFileResolver(new[] { directory }, directory))
                .WithMetadataResolver(ScriptMetadataResolver.Default.WithBaseDirectory(directory));

            return path == null ? options : options.WithFilePath(Path.GetFullPath(path));
        }

        private static IEnumerable<MetadataReference> References()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var paths = new List<string>();

            // The framework the viewer runs on, which the library (written against netstandard) needs to resolve its types.
            string trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;

            foreach (string file in (trusted ?? string.Empty).Split(Path.PathSeparator).Where(f => f.Length > 0))
            {
                string name = Path.GetFileNameWithoutExtension(file);

                if (name.StartsWith("System.", StringComparison.Ordinal) || name == "System" || name == "netstandard" || name == "mscorlib" || name == "Microsoft.CSharp")
                {
                    paths.Add(file);
                }
            }

            paths.Add(typeof(Tolerance).Assembly.Location);
            paths.Add(typeof(SceneWriter).Assembly.Location);
            paths.Add(typeof(ScriptGlobals).Assembly.Location);

            foreach (string file in paths.Where(f => !string.IsNullOrEmpty(f) && File.Exists(f) && seen.Add(Path.GetFileName(f))))
            {
                yield return MetadataReference.CreateFromFile(file);
            }
        }

        private static IEnumerable<string> Imports()
        {
            var imports = new List<string> { "System", "System.Linq", "System.Collections.Generic", "GeometryHelper.Viewer" };

            imports.AddRange(typeof(Tolerance).Assembly.GetExportedTypes()
                .Select(type => type.Namespace)
                .Where(ns => !string.IsNullOrEmpty(ns))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(ns => ns, StringComparer.Ordinal));

            return imports.Distinct(StringComparer.Ordinal);
        }
    }
}

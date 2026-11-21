using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace GeometryHelper.Cad.Remote.TestServer
{
    /// <summary>
    /// Hosts a <see cref="CadRemoteServer"/> over a host with no window, so a script in another process can drive it as it
    /// would drive the real app. It writes <c>READY &lt;pipe name&gt; &lt;process id&gt;</c> when it listens, then answers
    /// each line of its standard input with one line: <c>type &lt;text&gt;</c> types into the active drawing's command line,
    /// <c>escape</c> presses Escape there (how a test answers or cancels a pick), <c>quit</c> stops it. It exits by itself
    /// after <c>--minutes</c>, so a test run that died leaves nothing running.
    /// </summary>
    internal static class Program
    {
        private const string Usage = "usage: GeometryHelper.Cad.Remote.TestServer.exe --folder <dir> [--minutes <n>]";

        private static int Main(string[] args)
        {
            if (!TryReadArguments(args, out string folder, out double minutes))
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }

            Type[] wanted = { typeof(ICadRemoteHost), typeof(ICadRemoteDispatcher), typeof(string), typeof(string) };
            ConstructorInfo constructor = typeof(CadRemoteServer)
                .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(c => c.GetParameters().Select(p => p.ParameterType).SequenceEqual(wanted));
            if (constructor == null)
            {
                Console.Error.WriteLine(
                    "CadRemoteServer has no internal constructor (host, dispatcher, folder, instance): the test server is out of date."
                );
                return 3;
            }

            Directory.CreateDirectory(folder);
            var host = new TestHost();
            var gate = new object();
            var server = (CadRemoteServer)
                constructor.Invoke(new object[] { host, new InlineDispatcher(gate), folder, "pytest" });
            server.Start();

            // The timer ends the process from its own thread: the main one is waiting on the standard input.
            using (
                new Timer(
                    _ =>
                    {
                        server.Dispose();
                        Environment.Exit(4);
                    },
                    null,
                    TimeSpan.FromMinutes(minutes),
                    Timeout.InfiniteTimeSpan
                )
            )
            {
                Console.Out.WriteLine("READY " + server.PipeName + " " + Environment.ProcessId);
                Console.Out.Flush();

                string line;
                while ((line = Console.In.ReadLine()) != null)
                {
                    int space = line.IndexOf(' ');
                    string word = space < 0 ? line : line.Substring(0, space);
                    if (word == "quit")
                    {
                        break;
                    }

                    Console.Out.WriteLine(Answer(host, gate, word, space < 0 ? "" : line.Substring(space + 1)));
                    Console.Out.Flush();
                }
            }

            server.Dispose();
            return 0;
        }

        /// <summary>Runs one console line under the gate, on the active drawing, and gives the line to answer with.</summary>
        private static string Answer(TestHost host, object gate, string word, string rest)
        {
            if (word != "type" && word != "escape")
            {
                return "ERROR unknown command " + word;
            }

            try
            {
                lock (gate)
                {
                    CadDocument active = host.ActiveDocument;
                    if (active == null)
                    {
                        return "ERROR no drawing is open";
                    }

                    if (word == "type")
                    {
                        host.Type(active, rest);
                    }
                    else
                    {
                        host.SessionOf(active).Escape();
                    }
                }

                return "DONE";
            }
            catch (Exception ex)
            {
                // One answer is one line, whatever the message holds.
                return "ERROR " + ex.GetType().Name + ": " + ex.Message.Replace("\r", " ").Replace("\n", " ");
            }
        }

        private static bool TryReadArguments(string[] args, out string folder, out double minutes)
        {
            folder = null;
            minutes = 15;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--folder" && i + 1 < args.Length)
                {
                    folder = args[++i];
                }
                else if (args[i] == "--minutes" && i + 1 < args.Length)
                {
                    if (
                        !double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out minutes)
                        || minutes <= 0
                    )
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            return !string.IsNullOrEmpty(folder);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// A drawing opened in the background: its file read and its display list and pick index made on a worker thread, so
    /// that the window answers while a drawing of a million entities is drawn, and shown once drawn. The worker has the
    /// document to itself until then: the window does not hold it, and the extensions are told of it and its fonts set on the
    /// window's thread between the reading and the drawing.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private Opening _opening;
        private DelegateCommand _cancelOpeningCommand;

        /// <summary>
        /// Gets or sets whether <see cref="Open(string)"/> opens a file in the background, as <see cref="OpenAsync"/> does: false, the
        /// default, opens it before returning, as a test or a screenshot needs; the app's window sets it.
        /// </summary>
        public bool OpensInBackground { get; set; }

        /// <summary>Gets whether a drawing is opening in the background.</summary>
        public bool IsOpening => _opening != null;

        /// <summary>
        /// Gets how far the drawing opening in the background has got, as "Opening plan.dxf: drawing 45 %"; empty with none.
        /// The status bar shows it on its progress meter (<see cref="Progress"/>).
        /// </summary>
        public string OpeningText
        {
            get
            {
                Opening opening = _opening;

                if (opening == null)
                {
                    return string.Empty;
                }

                int percent = opening.Percent;
                return "Opening "
                    + System.IO.Path.GetFileName(opening.Path)
                    + ": "
                    + (percent < 0 ? "reading the file" : "drawing " + percent + " %");
            }
        }

        /// <summary>Gets the command that stops the drawing opening in the background, as Esc does.</summary>
        public ICommand CancelOpeningCommand =>
            _cancelOpeningCommand
            ?? (_cancelOpeningCommand = new DelegateCommand(_ => CancelOpening(), _ => IsOpening));

        /// <summary>
        /// Opens a DXF file in the background, or shows it again where it is open already, as <see cref="Open(string)"/> does, without
        /// holding the window's thread: the file is read and its display drawn on a worker thread, the status bar saying how
        /// far it has got (<see cref="OpeningText"/>), and the drawing is shown once drawn. The window answers meanwhile: the
        /// view shown pans and zooms, another drawing open is shown, and Esc stops the opening. A command typed or started
        /// meanwhile is not run, the command line saying why, so that none runs on a drawing half drawn. One drawing opens at
        /// a time. Call it on the window's thread, whose dispatcher shows the drawing.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>True once the file is shown; false where it could not be read, was stopped, or another was opening.</returns>
        public Task<bool> OpenAsync(string path)
        {
            bool shown = Open(path, true, out Opening opening);
            return opening != null ? opening.Done.Task : Task.FromResult(shown);
        }

        /// <summary>
        /// Stops the drawing opening in the background, as Esc does: the command line says so, and the drawing shown stays.
        /// </summary>
        /// <returns>Whether a drawing was opening.</returns>
        public bool CancelOpening()
        {
            Opening opening = _opening;

            if (opening == null)
            {
                return false;
            }

            opening.Cancel.Cancel();
            return true;
        }

        /// <summary>
        /// A command typed or started while a drawing opens in the background: not run, the command line saying why. Nothing
        /// is refused with none opening.
        /// </summary>
        private bool RefusedWhileOpening(string input)
        {
            Opening opening = _opening;

            if (opening == null)
            {
                return false;
            }

            string typed = (input ?? string.Empty).Trim();
            Say(
                (typed.Length > 0 ? typed.ToUpperInvariant() + " not run: " : "Not run: ")
                    + System.IO.Path.GetFileName(opening.Path)
                    + " is still opening. Run it again once it is shown, or press Esc to stop opening it."
            );
            return true;
        }

        /// <summary>
        /// Starts opening a file in the background: read on a worker thread, its extensions told and its fonts set back on this
        /// thread, its display drawn on a worker thread again, then shown here.
        /// </summary>
        /// <returns>True where it has started, or the file was opening already; false, the command line saying why, where another is.</returns>
        private bool StartOpening(string full, out Opening opening)
        {
            Opening running = _opening;

            if (running != null)
            {
                if (SamePath(running.Path, full))
                {
                    opening = running;
                    return true;
                }

                opening = null;
                Say(
                    "Cannot open "
                        + System.IO.Path.GetFileName(full)
                        + " yet: "
                        + System.IO.Path.GetFileName(running.Path)
                        + " is still opening."
                );
                return false;
            }

            var started = new Opening(full, Dispatcher.CurrentDispatcher, RaiseOpening)
            {
                Meter = Progress.Start("Opening " + System.IO.Path.GetFileName(full)),
            };
            opening = started;
            _opening = started;
            RaiseOpening();
            CancellationToken stop = started.Cancel.Token;

            Task.Run(() => CadDocument.Open(full, started.Reading, stop), stop)
                .ContinueWith(
                    read => started.Post(() => Read(started, read)),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default
                );
            return true;
        }

        /// <summary>The file read: stopped, not read (recovered, as Open does), or told to the extensions and drawn.</summary>
        private void Read(Opening opening, Task<CadDocument> read)
        {
            if (opening.Cancel.IsCancellationRequested || read.IsCanceled)
            {
                Stopped(opening);
                return;
            }

            if (read.IsFaulted)
            {
                Exception why = read.Exception.GetBaseException();
                End(opening);

                if (why is OutOfMemoryException)
                {
                    Say("Cannot open " + System.IO.Path.GetFileName(opening.Path) + ": " + why.Message);
                    opening.Done.SetResult(false);
                    return;
                }

                opening.Done.SetResult(OpenRecovered(opening.Path, why));
                return;
            }

            CadDocument document = read.Result;

            foreach (CadWorkspaceExtension extension in _extensions)
            {
                extension.DocumentOpened(document);
            }

            // The fonts and folders the display is drawn with, set as Attach sets them, and what the fonts say kept to be
            // written once the drawing is shown.
            document.Fonts = _settings.ToFonts(SkiaCanvasRenderer.HasFamily);
            document.SupportFolders = _settings.SupportSearchFolders;
            var said = new List<string>();
            EventHandler<CadFontMissingEventArgs> keep = (s, e) =>
            {
                lock (said)
                {
                    said.Add(e.Message);
                }
            };
            document.FontMissing += keep;
            CadSpace space = document.CurrentSpace;
            CancellationToken stop = opening.Cancel.Token;
            opening.Percent = 0;
            RaiseOpening();

            Task.Run(() => document.PrepareDisplay(space, opening, stop), stop)
                .ContinueWith(
                    drawn =>
                        opening.Post(() =>
                        {
                            document.FontMissing -= keep;
                            Drawn(opening, document, drawn, said);
                        }),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default
                );
        }

        /// <summary>The display drawn: stopped, or the drawing shown.</summary>
        private void Drawn(Opening opening, CadDocument document, Task drawn, List<string> said)
        {
            if (opening.Cancel.IsCancellationRequested || drawn.IsCanceled)
            {
                Stopped(opening);
                return;
            }

            if (drawn.IsFaulted)
            {
                // What could not be drawn here is drawn as the canvas first asks for it, as before the drawing was opened so.
                GeometryHelperLog.Warn(
                    "MainViewModel: the display of " + opening.Path + " could not be drawn in the background.",
                    drawn.Exception.GetBaseException()
                );
            }

            opening.Meter.Report(1.0);
            End(opening);

            if (_files.TryGetValue(opening.Path, out KnownFile known) && known.Document != null)
            {
                // Opened meanwhile at once, as a test or an extension may open it.
                Show(known);
                opening.Done.SetResult(true);
                return;
            }

            opening.Done.SetResult(ShowOpened(opening.Path, known, document, said));
        }

        private void Stopped(Opening opening)
        {
            End(opening);
            Say("Opening " + System.IO.Path.GetFileName(opening.Path) + " stopped.");
            opening.Done.SetResult(false);
        }

        private void End(Opening opening)
        {
            opening.Meter.Stop();

            if (_opening == opening)
            {
                _opening = null;
                RaiseOpening();
            }
        }

        private void RaiseOpening()
        {
            Raise(nameof(IsOpening));
            Raise(nameof(OpeningText));
            _cancelOpeningCommand?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// A drawing opening in the background: its file, what stops it, the share of its display drawn, told by the worker and
        /// shown on the window's thread once a whole percent more is drawn, and its end.
        /// </summary>
        private sealed class Opening : IProgress<double>
        {
            private readonly Dispatcher _dispatcher;
            private readonly Action _changed;
            private int _percent = -1;

            public Opening(string path, Dispatcher dispatcher, Action changed)
            {
                Path = path;
                _dispatcher = dispatcher;
                _changed = changed;

                // The share of the status bar's meter the reading takes, the drawing the rest, as a drawing of 100 000 lines
                // takes them: a DWG is read in about the time it is drawn, a DXF in a fifth of it.
                _reading = path.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) ? 0.5 : 0.2;
                Reading = new Share(value => Meter.Report(value * _reading));
            }

            private readonly double _reading;

            public string Path { get; }

            /// <summary>Gets or sets the meter of the status bar the opening moves.</summary>
            public CadProgressMeter Meter { get; set; }

            /// <summary>Gets what the reading of the file tells how far it has got, its share of the meter.</summary>
            public IProgress<double> Reading { get; }

            public CancellationTokenSource Cancel { get; } = new CancellationTokenSource();

            public TaskCompletionSource<bool> Done { get; } = new TaskCompletionSource<bool>();

            /// <summary>Gets or sets the whole percent of the display drawn; -1 while the file is read.</summary>
            public int Percent
            {
                get => Volatile.Read(ref _percent);
                set => Volatile.Write(ref _percent, value);
            }

            public void Report(double value)
            {
                Meter.Report(_reading + (1.0 - _reading) * value);
                int percent = (int)Math.Floor(Math.Max(0.0, Math.Min(1.0, value)) * 100.0);

                if (Interlocked.Exchange(ref _percent, percent) != percent)
                {
                    Post(_changed);
                }
            }

            /// <summary>Runs something on the window's thread.</summary>
            public void Post(Action action) => _dispatcher.BeginInvoke(action);
        }

        /// <summary>A progress that hands each share on.</summary>
        private sealed class Share : IProgress<double>
        {
            private readonly Action<double> _report;

            public Share(Action<double> report) => _report = report;

            public void Report(double value) => _report(value);
        }
    }
}

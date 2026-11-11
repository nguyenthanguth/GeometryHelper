using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using GeometryHelper.Dxf.Viewer.Wpf;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>Which dialog the model drawing asks the window to show.</summary>
    public enum DrawingDialogKind
    {
        /// <summary>New drawing: source, objects, settings.</summary>
        NewDrawing,

        /// <summary>Update from model.</summary>
        Update,

        /// <summary>Drawing settings.</summary>
        Settings,

        /// <summary>Export.</summary>
        Export,

        /// <summary>A message: an error, a warning, a notice of work done.</summary>
        Message,

        /// <summary>Run plugin: a drawing plugin, its view, objects and settings.</summary>
        RunPlugin,
    }

    /// <summary>A dialog the model drawing asks the window to show: its kind and its view model.</summary>
    public sealed class DrawingDialogRequestEventArgs : EventArgs
    {
        internal DrawingDialogRequestEventArgs(DrawingDialogKind kind, DialogViewModel model)
        {
            Kind = kind;
            Model = model;
        }

        /// <summary>Gets which dialog.</summary>
        public DrawingDialogKind Kind { get; }

        /// <summary>Gets its view model: a <see cref="NewDrawingViewModel"/>, <see cref="UpdateFromModelViewModel"/>,
        /// <see cref="DrawingSettingsViewModel"/>, <see cref="ExportViewModel"/>, <see cref="MessageViewModel"/> or
        /// <see cref="PluginRunViewModel"/>.</summary>
        public DialogViewModel Model { get; }
    }

    /// <summary>
    /// The base of the view models of the model drawing's dialogs: whether heavy work runs, with a line saying what, the
    /// command that stops it, and the request to close the dialog.
    /// </summary>
    public abstract class DialogViewModel : ObservableObject
    {
        private bool _isBusy;
        private string _busyText = string.Empty;
        private CancellationTokenSource _cancel;

        /// <summary>Makes the view model.</summary>
        protected DialogViewModel()
        {
            StopCommand = new DelegateCommand(_ => Stop(), _ => _isBusy);
        }

        /// <summary>Raised when the dialog is to close: true when what it was for was done, false when given up.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets whether heavy work runs, off the UI thread; the dialog shows <see cref="BusyText"/> and a Stop button.</summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (Set(ref _isBusy, value))
                {
                    ((DelegateCommand)StopCommand).RaiseCanExecuteChanged();
                    OnBusyChanged();
                }
            }
        }

        /// <summary>Gets what the heavy work does, as "Making the views, dimensions and sheet of B-101…".</summary>
        public string BusyText
        {
            get => _busyText;
            private set => Set(ref _busyText, value ?? string.Empty);
        }

        /// <summary>Gets the command that stops the heavy work: its result is dropped and the dialog stays open.</summary>
        public ICommand StopCommand { get; }

        /// <summary>Asks the window to close the dialog.</summary>
        /// <param name="done">Whether what the dialog was for was done.</param>
        public void Close(bool done)
        {
            Stop();
            CloseRequested?.Invoke(this, done);
        }

        /// <summary>Stops the heavy work running, if any.</summary>
        public void Stop() => _cancel?.Cancel();

        /// <summary>Called when <see cref="IsBusy"/> changes, so that commands can say whether they run.</summary>
        protected virtual void OnBusyChanged() { }

        /// <summary>
        /// Runs heavy work off the UI thread, showing what it does, and comes back on the caller's thread. Stopped, it throws
        /// <see cref="OperationCanceledException"/> at once and the work's result, when it comes, is dropped.
        /// </summary>
        /// <typeparam name="T">What the work gives.</typeparam>
        /// <param name="text">What the work does, shown while it runs.</param>
        /// <param name="work">The work.</param>
        /// <returns>What it gave.</returns>
        protected async Task<T> RunAsync<T>(string text, Func<T> work)
        {
            var cancel = new CancellationTokenSource();
            _cancel = cancel;
            BusyText = text;
            IsBusy = true;

            try
            {
                Task<T> task = Task.Run(work);
                var stopped = new TaskCompletionSource<bool>();

                using (cancel.Token.Register(() => stopped.TrySetResult(true)))
                {
                    if (await Task.WhenAny(task, stopped.Task) != task)
                    {
                        // The work goes on to its end on its own; its failure, if any, is looked at so that it is not thrown later.
                        _ = task.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                        throw new OperationCanceledException(cancel.Token);
                    }
                }

                return await task;
            }
            finally
            {
                if (_cancel == cancel)
                {
                    _cancel = null;
                    IsBusy = false;
                    BusyText = string.Empty;
                }

                cancel.Dispose();
            }
        }

        /// <summary>Writes a length as the design does, thousands set apart by a space and no more places than it has: 3 000, 2.5.</summary>
        /// <param name="value">The length.</param>
        /// <returns>The text.</returns>
        internal static string Length(double value) =>
            value.ToString("#,0.##", new NumberFormatInfo { NumberGroupSeparator = " ", NumberDecimalSeparator = "." });
    }

    /// <summary>How a message looks: its icon and colour.</summary>
    public enum MessageKind
    {
        /// <summary>Something failed: red.</summary>
        Error,

        /// <summary>Something needs a look: amber.</summary>
        Warning,

        /// <summary>Something to know: blue.</summary>
        Info,

        /// <summary>Work done: green.</summary>
        Done,
    }

    /// <summary>
    /// A message of the model drawing, as the Messages board of the design draws them: an icon, a title, a line or two, a
    /// detail in monospace (the error's type and where), an action button and OK.
    /// </summary>
    /// <remarks>
    /// A drawing that could not be made is <c>new MessageViewModel(MessageKind.Error, "Drawing not made", "B1 could not be drawn:
    /// the settings allow no scale.", "ArgumentException in DrawingBuilder.Build")</c>; an export done carries the action
    /// "Open folder".
    /// </remarks>
    public sealed class MessageViewModel : DialogViewModel
    {
        private readonly Action _action;

        /// <summary>Makes a message.</summary>
        /// <param name="kind">How it looks.</param>
        /// <param name="title">The title, as "Drawing not made".</param>
        /// <param name="text">What happened, in words.</param>
        /// <param name="detail">A detail in monospace, as the error's type; null for none.</param>
        /// <param name="actionText">The label of an action button, as "Open folder"; null for none.</param>
        /// <param name="action">What the action does; null for none.</param>
        public MessageViewModel(
            MessageKind kind,
            string title,
            string text,
            string detail = null,
            string actionText = null,
            Action action = null
        )
        {
            Kind = kind;
            Title = title ?? string.Empty;
            Text = text ?? string.Empty;
            Detail = detail;
            ActionText = action == null ? null : actionText;
            _action = action;
            OkCommand = new DelegateCommand(_ => Close(true));
            ActionCommand = new DelegateCommand(_ =>
            {
                _action?.Invoke();
                Close(true);
            });
        }

        /// <summary>Gets how it looks.</summary>
        public MessageKind Kind { get; }

        /// <summary>Gets the title.</summary>
        public string Title { get; }

        /// <summary>Gets what happened.</summary>
        public string Text { get; }

        /// <summary>Gets the detail in monospace; null for none.</summary>
        public string Detail { get; }

        /// <summary>Gets whether there is a detail.</summary>
        public bool HasDetail => !string.IsNullOrEmpty(Detail);

        /// <summary>Gets the label of the action button; null for none.</summary>
        public string ActionText { get; }

        /// <summary>Gets whether there is an action button.</summary>
        public bool HasAction => !string.IsNullOrEmpty(ActionText);

        /// <summary>Gets the command of OK, which closes the message.</summary>
        public ICommand OkCommand { get; }

        /// <summary>Gets the command of the action button, which runs it and closes the message.</summary>
        public ICommand ActionCommand { get; }

        /// <summary>Makes the message of a failure: the error's type and the method it came from as the detail.</summary>
        /// <param name="title">The title.</param>
        /// <param name="text">What failed, in words.</param>
        /// <param name="error">The error.</param>
        /// <returns>The message.</returns>
        public static MessageViewModel Failure(string title, string text, Exception error)
        {
            Exception inner =
                error is AggregateException aggregate && aggregate.InnerExceptions.Count == 1
                    ? aggregate.InnerExceptions[0]
                    : error;
            string where =
                inner?.TargetSite == null
                    ? string.Empty
                    : " in " + inner.TargetSite.DeclaringType?.Name + "." + inner.TargetSite.Name;
            return new MessageViewModel(
                MessageKind.Error,
                title,
                text + (inner == null ? string.Empty : " " + inner.Message),
                inner == null ? null : inner.GetType().Name + where
            );
        }

        /// <inheritdoc/>
        public override string ToString() => Kind + ": " + Title + ": " + Text;
    }
}

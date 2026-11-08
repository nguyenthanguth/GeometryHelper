using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// The base of the model drawing's dialogs: a window with no chrome of its own, its header dragged to move it, closed when
    /// its view model asks, and its content at hand for a screenshot drawn with no window shown.
    /// </summary>
    public abstract class DrawingDialogWindow : Window
    {
        private DialogViewModel _model;

        /// <summary>Makes the window: no chrome, not resized, centred over its owner, not in the task bar.</summary>
        protected DrawingDialogWindow()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SizeToContent = SizeToContent.Manual;
            Closed += (s, e) =>
            {
                if (_model != null)
                {
                    _model.CloseRequested -= OnCloseRequested;
                    _model.Stop();
                }
            };
        }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public abstract FrameworkElement Body { get; }

        /// <summary>Gets whether the dialog closed with its work done.</summary>
        public bool Done { get; private set; }

        /// <summary>Puts the view model in: the window's data context, and closed when it asks.</summary>
        /// <param name="model">The view model.</param>
        protected void Attach(DialogViewModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            DataContext = model;
            model.CloseRequested += OnCloseRequested;
        }

        /// <summary>Drags the window by its header.</summary>
        /// <param name="sender">The header.</param>
        /// <param name="e">The press.</param>
        protected void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e != null && e.ButtonState == MouseButtonState.Pressed && IsLoaded && IsVisible)
            {
                DragMove();
            }
        }

        /// <summary>The close cross: the dialog given up.</summary>
        /// <param name="sender">The button.</param>
        /// <param name="e">The click.</param>
        protected void OnCloseClick(object sender, RoutedEventArgs e) => _model?.Close(false);

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Escape && _model != null)
            {
                if (_model.IsBusy)
                {
                    _model.Stop();
                }
                else
                {
                    _model.Close(false);
                }

                e.Handled = true;
            }
        }

        private void OnCloseRequested(object sender, bool done)
        {
            Done = done;

            if (IsVisible)
            {
                Close();
            }
        }
    }
}

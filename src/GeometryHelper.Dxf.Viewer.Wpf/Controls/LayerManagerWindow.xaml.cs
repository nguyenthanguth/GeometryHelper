using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Layer manager, as the design draws it: the layer table of the drawing with its current mark, on, frozen and locked
    /// flags, colour, linetype, weight and owner; New layer, Delete and Set current; a search. Every change is one undo step, made
    /// through <see cref="LayerManagerViewModel"/>; the dialog follows the drawing while it is open and lets it go when closed.
    /// </summary>
    public partial class LayerManagerWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model; disposed when the dialog closes.</param>
        public LayerManagerWindow(LayerManagerViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;

            // Wider in a layout, for the VP columns.
            Width = model.WindowWidth;
            Height = model.WindowHeight;
            model.PickColor = dialog => SelectColorWindow.Pick(this, dialog);
            model.PickLinetype = dialog => SelectLinetypeWindow.Pick(this, dialog);
            model.ShowMessage = message => CadQuestionWindow.ShowMessage(this, message);
            Closed += (s, e) => Model.Dispose();
        }

        // Windows' double-click time by default: a second click on a name later than this opens its cell.
        private const int DoubleClickMilliseconds = 500;

        // When the name of the chosen row was last clicked, for the slow second click that opens its cell.
        private LayerRowViewModel _clicked;
        private int _clickedAt;
        private bool _committing;

        /// <summary>F2 on the table: the chosen layer's name cell opened, as in AutoCAD's Layer manager.</summary>
        private void OnTableKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F2 && !(e.OriginalSource is System.Windows.Controls.TextBox))
            {
                Model.BeginRename();
                e.Handled = true;
            }
        }

        /// <summary>
        /// A click on a name: a double click opens its cell, and so does a second click on the chosen row's name that comes
        /// after the double-click time, as Windows lists rename.
        /// </summary>
        private void OnNameDown(object sender, MouseButtonEventArgs e)
        {
            if (!(((FrameworkElement)sender).DataContext is LayerRowViewModel row))
            {
                return;
            }

            if (e.ClickCount == 2)
            {
                Model.BeginRename(row);
                e.Handled = true;
                return;
            }

            int now = Environment.TickCount;
            bool slowSecond =
                row == _clicked && row == Model.SelectedRow && unchecked(now - _clickedAt) > DoubleClickMilliseconds;
            _clicked = row;
            _clickedAt = now;

            if (slowSecond)
            {
                Model.BeginRename(row);
                e.Handled = true;
            }
        }

        /// <summary>The name cell opened: the box takes the keys, the whole name chosen.</summary>
        private void OnNameBoxShown(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox box && box.IsVisible)
            {
                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        box.Focus();
                        box.SelectAll();
                    }),
                    System.Windows.Threading.DispatcherPriority.Input
                );
            }
        }

        /// <summary>Enter keeps the name typed, Esc the old one.</summary>
        private void OnNameBoxKey(object sender, KeyEventArgs e)
        {
            if (!(((FrameworkElement)sender).DataContext is LayerRowViewModel row))
            {
                return;
            }

            if (e.Key == Key.Enter)
            {
                Commit(row, (System.Windows.Controls.TextBox)sender);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Model.CancelRename(row);
                e.Handled = true;
            }
        }

        /// <summary>A click elsewhere keeps the name typed, as Enter does.</summary>
        private void OnNameBoxLeft(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is LayerRowViewModel row && row.IsRenaming && !_committing)
            {
                Commit(row, (System.Windows.Controls.TextBox)sender);
            }
        }

        /// <summary>The rename made; refused, the message shown and the cell open again with the name typed chosen.</summary>
        private void Commit(LayerRowViewModel row, System.Windows.Controls.TextBox box)
        {
            _committing = true;

            try
            {
                if (!Model.CommitRename(row) && row.IsRenaming)
                {
                    box.Focus();
                    box.SelectAll();
                }
            }
            finally
            {
                _committing = false;
            }
        }

        /// <summary>Gets the view model.</summary>
        public LayerManagerViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}

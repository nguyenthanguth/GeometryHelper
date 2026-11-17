using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
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
            Width = Math.Min(model.WindowWidth, Math.Max(800, SystemParameters.WorkArea.Width - 16));
            Height = Math.Min(model.WindowHeight, Math.Max(480, SystemParameters.WorkArea.Height - 16));
            model.PickColor = dialog => SelectColorWindow.Pick(this, dialog);
            model.PickLinetype = dialog => SelectLinetypeWindow.Pick(this, dialog);
            model.EditFilter = dialog => LayerFilterPropertiesWindow.Ask(this, dialog);
            model.ShowMessage = message =>
            {
                if (message == CadQuestionTexts.RenameRefused)
                {
                    CadQuestionWindow.ShowMessage(
                        this,
                        CadQuestionTexts.RenameRefusedTitle,
                        message,
                        CadQuestionTexts.RenameRefusedDetail
                    );
                }
                else
                {
                    CadQuestionWindow.ShowMessage(this, message);
                }
            };
            Closed += (s, e) => Model.Dispose();
        }

        // Windows' double-click time by default: a second click on a name later than this opens its cell.
        private const int DoubleClickMilliseconds = 500;

        // When the name of the chosen row was last clicked, for the slow second click that opens its cell.
        private LayerRowViewModel _clicked;
        private int _clickedAt;
        private bool _committing;

        // Where the left button went down on a layer's name, for a drag of the layers onto a group filter.
        private Point _dragFrom;

        /// <summary>The layers dragged from the list onto a group filter: the names, as a string array.</summary>
        internal const string LayerNamesFormat = "GeometryHelper.Cad.LayerNames";

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

            _dragFrom = e.GetPosition(null);
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

        /// <summary>The grip of the Name head dragged: the column is as much wider or narrower as the mouse moved.</summary>
        private void OnNameResize(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) =>
            Model.NameWidth += e.HorizontalChange;

        /// <summary>A layer's name dragged: the layers chosen, or the layer alone where it is not among them, go onto a group filter of the tree.</summary>
        private void OnNameMove(object sender, MouseEventArgs e)
        {
            if (
                e.LeftButton != MouseButtonState.Pressed
                || !(((FrameworkElement)sender).DataContext is LayerRowViewModel row)
                || (
                    Math.Abs(e.GetPosition(null).X - _dragFrom.X) < SystemParameters.MinimumHorizontalDragDistance
                    && Math.Abs(e.GetPosition(null).Y - _dragFrom.Y) < SystemParameters.MinimumVerticalDragDistance
                )
            )
            {
                return;
            }

            List<string> names = row.IsChosen ? Model.ChosenNames() : new List<string> { row.Name };
            DragDrop.DoDragDrop(
                (DependencyObject)sender,
                new DataObject(LayerNamesFormat, names.ToArray()),
                DragDropEffects.Copy
            );
        }

        /// <summary>A layer dragged over a node of the filter tree: allowed over a group filter.</summary>
        private void OnFilterDragOver(object sender, DragEventArgs e)
        {
            bool group = ((FrameworkElement)sender).DataContext is LayerFilterNodeViewModel node && node.IsGroup;
            e.Effects = group && e.Data.GetDataPresent(LayerNamesFormat) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        /// <summary>Layers dropped on a node of the filter tree: added to it where it is a group filter.</summary>
        private void OnFilterDrop(object sender, DragEventArgs e)
        {
            e.Handled = true;

            if (
                ((FrameworkElement)sender).DataContext is LayerFilterNodeViewModel node
                && e.Data.GetData(LayerNamesFormat) is string[] names
            )
            {
                Model.AddLayersToGroup(node, names);
            }
        }

        /// <summary>A node chosen in the tree: its layers are listed.</summary>
        private void OnFilterSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is LayerFilterNodeViewModel node)
            {
                Model.SelectedFilter = node;
            }
        }

        /// <summary>F2 and Delete on the tree.</summary>
        private void OnFilterKey(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource is TextBox)
            {
                return;
            }

            if (e.Key == Key.F2)
            {
                Model.BeginRenameFilter(Model.SelectedFilter);
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                Model.DeleteFilter(Model.SelectedFilter);
                e.Handled = true;
            }
        }

        /// <summary>A double click on the name of a filter of the drawing opens it for a new name.</summary>
        private void OnFilterNameDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && ((FrameworkElement)sender).DataContext is LayerFilterNodeViewModel node)
            {
                if (node.Kind == LayerFilterNodeKind.Property)
                {
                    Model.ChangeFilter(node);
                }
                else
                {
                    Model.BeginRenameFilter(node);
                }

                e.Handled = true;
            }
        }

        /// <summary>Enter keeps the name typed in the tree, Esc the old one.</summary>
        private void OnFilterNameBoxKey(object sender, KeyEventArgs e)
        {
            if (!(((FrameworkElement)sender).DataContext is LayerFilterNodeViewModel node))
            {
                return;
            }

            if (e.Key == Key.Enter)
            {
                CommitFilterName(node, (TextBox)sender);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Model.CancelRenameFilter(node);
                e.Handled = true;
            }
        }

        /// <summary>A click elsewhere keeps the name typed in the tree, as Enter does.</summary>
        private void OnFilterNameBoxLeft(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (
                ((FrameworkElement)sender).DataContext is LayerFilterNodeViewModel node
                && node.IsRenaming
                && !_committing
            )
            {
                CommitFilterName(node, (TextBox)sender);
            }
        }

        private void CommitFilterName(LayerFilterNodeViewModel node, TextBox box)
        {
            _committing = true;

            try
            {
                if (!Model.CommitRenameFilter(node) && node.IsRenaming)
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

        /// <summary>A layer chosen in the right-click item Add to Group Filter: the layers chosen go into that group.</summary>
        private void OnAddToGroupFilter(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is LayerFilterNodeViewModel node)
            {
                Model.AddLayersToGroup(node, Model.ChosenNames());
            }
        }

        /// <summary>A value chosen from the list of a cell that can be typed in: set at once, not when the cell is left.</summary>
        private void OnCellDropDownClosed(object sender, EventArgs e) =>
            ((ComboBox)sender).GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();

        /// <summary>Enter in a cell that is typed in: its value set, as leaving the cell sets it.</summary>
        private void OnCellKey(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            DependencyProperty property = sender is ComboBox ? ComboBox.TextProperty : TextBox.TextProperty;
            ((FrameworkElement)sender).GetBindingExpression(property)?.UpdateSource();
            e.Handled = true;
        }
    }
}

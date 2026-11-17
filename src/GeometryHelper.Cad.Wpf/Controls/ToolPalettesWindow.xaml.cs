using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>Turns the key of a tool's icon into its geometry, the plain command icon where the icon set has none of that name.</summary>
    public sealed class ToolIconConverter : IValueConverter
    {
        private readonly IconKeyConverter _icons = new IconKeyConverter();

        /// <summary>Finds the icon of a key.</summary>
        /// <param name="value">The key, as <c>GhIconLine</c>.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">Not used.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>The geometry.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            _icons.Convert(value, targetType, parameter, culture)
            ?? _icons.Convert("GhIconPluginCommand", targetType, parameter, culture);

        /// <summary>Not supported: the key is read, never written back.</summary>
        /// <param name="value">Not used.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">Not used.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>Nothing; it throws.</returns>
        /// <exception cref="NotSupportedException">Always.</exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>Turns the width of the list into the width of a line of text or a separator in it: a little less, for the border and the scroll bar.</summary>
    public sealed class ToolRowWidthConverter : IValueConverter
    {
        /// <summary>Takes 30 pixels off the width, but leaves 40.</summary>
        /// <param name="value">The width of the list.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">Not used.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>The width.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is double width && !double.IsNaN(width) ? Math.Max(40.0, width - 30.0) : 40.0;

        /// <summary>Not supported: the width is read, never written back.</summary>
        /// <param name="value">Not used.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">Not used.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>Nothing; it throws.</returns>
        /// <exception cref="NotSupportedException">Always.</exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>Chooses how an item of the palette is drawn: a line of text, a separator, a tool as an icon, or a tool as a row.</summary>
    public sealed class ToolItemTemplateSelector : DataTemplateSelector
    {
        /// <summary>Gets or sets the template of a tool in the icon views.</summary>
        public DataTemplate Icons { get; set; }

        /// <summary>Gets or sets the template of a tool in the list view.</summary>
        public DataTemplate Row { get; set; }

        /// <summary>Gets or sets the template of a line of text.</summary>
        public DataTemplate Text { get; set; }

        /// <summary>Gets or sets the template of a separator.</summary>
        public DataTemplate Separator { get; set; }

        /// <summary>Chooses the template of an item.</summary>
        /// <param name="item">The item.</param>
        /// <param name="container">Not used.</param>
        /// <returns>The template.</returns>
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var tool = item as ToolItemViewModel;
            return tool == null ? null
                : tool.IsText ? Text
                : tool.IsSeparator ? Separator
                : tool.UsesRow ? Row
                : Icons;
        }
    }

    /// <summary>
    /// AutoCAD's tool palettes, which TOOLPALETTES shows beside the window and leaves open until TOOLPALETTESCLOSE: the tools of
    /// a palette as icons, icons with names or rows, and the tabs of the palettes along the right edge. A click runs a tool; a
    /// block or hatch tool dragged onto the canvas is placed or hatched where it is dropped; objects dragged from the drawing,
    /// and blocks dragged from DesignCenter, become tools. The right-click menus cut, copy, paste, delete, rename and give
    /// properties to a tool, and make, rename and delete palettes. Its logic is <see cref="ToolPalettesViewModel"/>.
    /// </summary>
    public partial class ToolPalettesWindow : Window
    {
        private static ToolPalettesWindow _open;
        private Point _dragStart;
        private ToolItemViewModel _pressed;
        private bool _runOnRelease;

        /// <summary>Makes the palettes over their view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public ToolPalettesWindow(ToolPalettesViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public ToolPalettesViewModel Model { get; }

        /// <summary>Gets the palettes' content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>
        /// Shows the palettes over a window, at its right edge and left open: the ones open already are brought to the front.
        /// The model is told they are open, so that the key given TOOLPALETTES closes them again.
        /// </summary>
        /// <param name="owner">The window they are shown over; null for none.</param>
        /// <param name="mainModel">The window's view model, whose drawing the tools run on.</param>
        /// <param name="model">The view model.</param>
        public static void ShowPalette(Window owner, MainViewModel mainModel, ToolPalettesViewModel model)
        {
            if (_open != null)
            {
                _open.Activate();
                return;
            }

            var window = new ToolPalettesWindow(model) { Owner = owner };
            _open = window;

            if (owner != null && owner.IsVisible)
            {
                window.Left = Math.Max(0.0, owner.Left + owner.ActualWidth - window.Width - 24.0);
                window.Top = owner.Top + 96.0;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            mainModel.PropertyChanged += window.OnMainChanged;
            window.Closed += (s, e) =>
            {
                mainModel.PropertyChanged -= window.OnMainChanged;
                mainModel.NoteToolPalettesShown(false);
                model.Detach();
                _open = null;
            };
            mainModel.NoteToolPalettesShown(true);
            window.Show();
        }

        /// <summary>Closes the palettes that are open, as TOOLPALETTESCLOSE does; nothing where none are.</summary>
        public static void ClosePalette() => _open?.Close();

        private void OnMainChanged(object sender, PropertyChangedEventArgs e)
        {
            // The pictures of the blocks of the drawing are those of the drawing shown.
            if (e.PropertyName == nameof(MainViewModel.Document))
            {
                Model.Reload();
            }
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();

        /// <summary>A click on a tab shows its palette; the right-click menu then belongs to that palette.</summary>
        private void OnTabDown(object sender, MouseButtonEventArgs e)
        {
            if (TabOf(e.OriginalSource as DependencyObject) is ToolPaletteTabViewModel tab)
            {
                Model.Choose(tab);
            }
        }

        private void OnMenuButton(object sender, RoutedEventArgs e)
        {
            ContextMenu menu = PaletteMenu(null);
            menu.PlacementTarget = MenuButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void OnPaletteMenuOpening(object sender, ContextMenuEventArgs e) =>
            TabList.ContextMenu = PaletteMenu(null);

        /// <summary>The right-click menu of the tools: what the tools under the pointer can be given, or the palette's menu over no tool.</summary>
        private void OnItemMenuOpening(object sender, ContextMenuEventArgs e)
        {
            ToolItemViewModel under = ItemOf(e.OriginalSource as DependencyObject);

            if (under == null)
            {
                ItemList.UnselectAll();
                ItemList.ContextMenu = PaletteMenu(null);
                return;
            }

            if (!ItemList.SelectedItems.Contains(under))
            {
                ItemList.SelectedItems.Clear();
                ItemList.SelectedItems.Add(under);
            }

            List<ToolItemViewModel> chosen = ItemList.SelectedItems.Cast<ToolItemViewModel>().ToList();
            ContextMenu menu = NewMenu();
            menu.Items.Add(Entry("Cut", () => Model.Cut(chosen)));
            menu.Items.Add(Entry("Copy", () => Model.Copy(chosen)));
            menu.Items.Add(Entry("Paste", () => Model.Paste(under), Model.CanPaste));
            menu.Items.Add(Entry("Delete", () => Model.Delete(chosen)));
            menu.Items.Add(new Separator());
            menu.Items.Add(
                Entry("Rename…", () => RenameTool(under), chosen.Count == 1 && under.Kind != CadToolKind.Separator)
            );
            menu.Items.Add(
                Entry(
                    "Properties…",
                    () => ShowProperties(under),
                    chosen.Count == 1 && under.Kind != CadToolKind.Separator
                )
            );
            ItemList.ContextMenu = menu;
        }

        /// <summary>The menu of the palette: New, Rename and Delete Palette, Add Text and Separator, Paste, View Options, Import and Export.</summary>
        private ContextMenu PaletteMenu(ToolItemViewModel after)
        {
            ContextMenu menu = NewMenu();
            bool has = Model.HasPalette;
            menu.Items.Add(Entry("New Palette…", NewPalette));
            menu.Items.Add(Entry("Rename Palette…", RenamePalette, has));
            menu.Items.Add(Entry("Delete Palette", DeletePalette, has));
            menu.Items.Add(new Separator());
            menu.Items.Add(Entry("Add Text…", () => AddText(after), has));
            menu.Items.Add(Entry("Add Separator", () => Model.AddSeparator(after), has));
            menu.Items.Add(Entry("Paste", () => Model.Paste(after), Model.CanPaste));
            menu.Items.Add(new Separator());
            menu.Items.Add(Entry("View Options…", ViewOptions, has));
            menu.Items.Add(new Separator());
            menu.Items.Add(Entry("Import Palette…", ImportPalettes));
            menu.Items.Add(Entry("Export Palette…", () => ExportPalettes(false), has));
            menu.Items.Add(Entry("Export All Palettes…", () => ExportPalettes(true), has));
            return menu;
        }

        private ContextMenu NewMenu() => new ContextMenu { Style = (Style)FindResource("GhContextMenu") };

        private MenuItem Entry(string text, Action run, bool enabled = true)
        {
            var item = new MenuItem
            {
                Header = text,
                Style = (Style)FindResource("GhMenuItem"),
                IsEnabled = enabled,
            };
            item.Click += (s, a) => run();
            return item;
        }

        private void NewPalette()
        {
            var question = new ToolPaletteNameViewModel("New Palette", "Palette name:", CadToolPalettes.NewPaletteName);

            if (ToolPaletteNameWindow.Show(this, question))
            {
                Model.NewPalette(question.Name);
            }
        }

        private void RenamePalette()
        {
            ToolPaletteTabViewModel tab = Model.Tabs.FirstOrDefault(t => t.IsActive);

            if (tab == null)
            {
                return;
            }

            var question = new ToolPaletteNameViewModel("Rename Palette", "Palette name:", tab.Name);

            if (ToolPaletteNameWindow.Show(this, question))
            {
                Model.RenamePalette(tab, question.Name);
            }
        }

        private void DeletePalette()
        {
            ToolPaletteTabViewModel tab = Model.Tabs.FirstOrDefault(t => t.IsActive);

            if (tab == null)
            {
                return;
            }

            var ask = new CadQuestionWindow(
                "Delete the palette?",
                "The palette and its "
                    + tab.Palette.Tools.Count.ToString(CultureInfo.InvariantCulture)
                    + " tools are deleted.",
                tab.Name,
                "Delete",
                "Keep"
            )
            {
                Owner = this,
            };
            ask.ShowDialog();

            if (ask.Answer == CadSaveChoice.Save)
            {
                Model.DeletePalette(tab);
            }
        }

        private void AddText(ToolItemViewModel after)
        {
            var question = new ToolPaletteNameViewModel("Add Text", "Text:", "Text");

            if (ToolPaletteNameWindow.Show(this, question))
            {
                Model.AddText(question.Name, after);
            }
        }

        private void RenameTool(ToolItemViewModel item)
        {
            var question = new ToolPaletteNameViewModel(
                item.IsText ? "Edit Text" : "Rename Tool",
                item.IsText ? "Text:" : "Tool name:",
                item.Name
            );

            if (ToolPaletteNameWindow.Show(this, question))
            {
                Model.Rename(item, question.Name);
            }
        }

        private void ShowProperties(ToolItemViewModel item)
        {
            CadDocument document = Model.Model.Document;
            var properties = new ToolPropertiesViewModel(
                item.Tool,
                document?.Database.LayerTable.Select(l => l.Name).ToList(),
                document?.Database.LinetypeTable.Select(l => l.Name).ToList()
            );

            if (ToolPropertiesWindow.Show(this, properties))
            {
                Model.PropertiesChanged(item);
            }
        }

        private void ViewOptions()
        {
            var options = new ToolPaletteViewOptionsViewModel(Model.ViewStyle, Model.IconSize);

            if (ToolPaletteViewOptionsWindow.Show(this, options))
            {
                Model.SetView(options.Style, options.IconSize, options.ApplyToAll);
            }
        }

        private void ImportPalettes()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Tool palettes (*.ghtp;*.json)|*.ghtp;*.json|All files (*.*)|*.*",
                Title = "Import Tool Palettes",
            };

            if (dialog.ShowDialog(this) == true)
            {
                Model.Import(dialog.FileName);
            }
        }

        private void ExportPalettes(bool all)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Tool palettes (*.ghtp)|*.ghtp",
                Title = all ? "Export All Tool Palettes" : "Export Tool Palette",
                FileName = all ? "ToolPalettes" : Model.Active?.Name,
                DefaultExt = ".ghtp",
            };

            if (dialog.ShowDialog(this) == true)
            {
                Model.Export(dialog.FileName, all);
            }
        }

        /// <summary>A press on a tool: where a click or a drag may start. A plain click runs the tool when it is let go; Ctrl or Shift selects.</summary>
        private void OnItemDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(ItemList);
            _pressed = ItemOf(e.OriginalSource as DependencyObject);
            _runOnRelease =
                _pressed != null
                && e.ClickCount == 1
                && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == 0;
        }

        private void OnItemUp(object sender, MouseButtonEventArgs e)
        {
            ToolItemViewModel item = _pressed;
            bool run =
                _runOnRelease && item != null && ReferenceEquals(item, ItemOf(e.OriginalSource as DependencyObject));
            _pressed = null;
            _runOnRelease = false;

            if (run && Model.Run(item))
            {
                // The tool goes on at the drawing: the window's command line and the canvas take the next click.
                Owner?.Activate();
            }
        }

        /// <summary>A tool pressed and moved is dragged: a block or hatch tool can be dropped on the canvas, any tool at another place of its palette.</summary>
        private void OnItemMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _pressed == null)
            {
                return;
            }

            Vector moved = e.GetPosition(ItemList) - _dragStart;

            if (
                Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance
            )
            {
                return;
            }

            ToolItemViewModel item = _pressed;
            _pressed = null;
            _runOnRelease = false;
            int palette = Model.ToolSet.ActiveIndex;
            int index = Model.Items.IndexOf(item);

            if (index < 0)
            {
                return;
            }

            var data = new DataObject();
            data.SetData(ToolPaletteDrag.Format, ToolPaletteDrag.Write(palette, index));
            DragDrop.DoDragDrop(ItemList, data, DragDropEffects.Copy | DragDropEffects.Move);
        }

        /// <summary>Delete deletes the tools chosen, Ctrl+X, Ctrl+C and Ctrl+V cut, copy and paste them, F2 renames one, Enter runs it.</summary>
        private void OnItemKey(object sender, KeyEventArgs e)
        {
            List<ToolItemViewModel> chosen = ItemList.SelectedItems.Cast<ToolItemViewModel>().ToList();
            bool control = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            if (e.Key == Key.Delete)
            {
                Model.Delete(chosen);
            }
            else if (control && e.Key == Key.X)
            {
                Model.Cut(chosen);
            }
            else if (control && e.Key == Key.C)
            {
                Model.Copy(chosen);
            }
            else if (control && e.Key == Key.V)
            {
                Model.Paste(chosen.LastOrDefault());
            }
            else if (e.Key == Key.F2 && chosen.Count == 1 && chosen[0].Kind != CadToolKind.Separator)
            {
                RenameTool(chosen[0]);
            }
            else if (e.Key == Key.Enter && chosen.Count == 1)
            {
                if (Model.Run(chosen[0]))
                {
                    Owner?.Activate();
                }
            }
            else
            {
                return;
            }

            e.Handled = true;
        }

        private void OnToolsDragOver(object sender, DragEventArgs e)
        {
            bool taken =
                Model.HasPalette
                && (
                    e.Data.GetDataPresent(ObjectDrag.Format)
                    || e.Data.GetDataPresent(DesignCenterDrag.Format)
                    || e.Data.GetDataPresent(ToolPaletteDrag.Format)
                );
            e.Effects = taken
                ? (e.Data.GetDataPresent(ToolPaletteDrag.Format) ? DragDropEffects.Move : DragDropEffects.Copy)
                : DragDropEffects.None;
            e.Handled = true;
        }

        /// <summary>
        /// Objects of the drawing and blocks of DesignCenter dropped on the palette become tools before the one they are dropped
        /// on, last where dropped on nothing; a tool of the palette dropped on it is moved there.
        /// </summary>
        private void OnToolsDrop(object sender, DragEventArgs e)
        {
            ToolItemViewModel before = ItemOf(e.OriginalSource as DependencyObject);

            if (e.Data.GetData(ToolPaletteDrag.Format) is string moving)
            {
                if (
                    ToolPaletteDrag.TryRead(moving, out int palette, out int index)
                    && palette == Model.ToolSet.ActiveIndex
                    && index >= 0
                    && index < Model.Items.Count
                )
                {
                    Model.Move(Model.Items[index], before);
                }
            }
            else if (e.Data.GetData(ObjectDrag.Format) is string objects)
            {
                Model.Drop(ObjectDrag.Format, objects, before);
            }
            else if (e.Data.GetData(DesignCenterDrag.Format) is string blocks)
            {
                Model.Drop(DesignCenterDrag.Format, blocks, before);
            }

            e.Handled = true;
        }

        private static ToolItemViewModel ItemOf(DependencyObject source)
        {
            while (source != null)
            {
                if (source is ListBoxItem box)
                {
                    return box.DataContext as ToolItemViewModel;
                }

                source =
                    source is Visual || source is System.Windows.Media.Media3D.Visual3D
                        ? VisualTreeHelper.GetParent(source)
                        : LogicalTreeHelper.GetParent(source);
            }

            return null;
        }

        private static ToolPaletteTabViewModel TabOf(DependencyObject source)
        {
            while (source != null)
            {
                if (source is ListBoxItem box)
                {
                    return box.DataContext as ToolPaletteTabViewModel;
                }

                source =
                    source is Visual || source is System.Windows.Media.Media3D.Visual3D
                        ? VisualTreeHelper.GetParent(source)
                        : LogicalTreeHelper.GetParent(source);
            }

            return null;
        }
    }
}

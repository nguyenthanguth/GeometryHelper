using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// AutoCAD's Layer Translator, which LAYTRANS shows: Translate From and Translate To side by side with Map and Map same,
    /// Load… and New…, the mappings with Edit…, Remove and Save…, Settings… and Translate. Load… reads a .dwg, .dws, .dwt or
    /// .dxf; Save… writes a .dws; New… and Edit… show <see cref="TranslateLayerWindow"/>, Settings… <see cref="TranslateSettingsWindow"/>.
    /// Its logic is <see cref="LayerTranslatorViewModel"/>.
    /// </summary>
    public partial class LayerTranslatorWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public LayerTranslatorWindow(LayerTranslatorViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.AskLoadFile = () =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter =
                        "Standards and drawings (*.dws;*.dwg;*.dwt;*.dxf)|*.dws;*.dwg;*.dwt;*.dxf|Standards (*.dws)|*.dws|Drawing (*.dwg)|*.dwg|Template (*.dwt)|*.dwt|DXF (*.dxf)|*.dxf|All files (*.*)|*.*",
                    Title = "Select Drawing File",
                };
                return dialog.ShowDialog(this) == true ? dialog.FileName : null;
            };
            model.AskSaveFile = () =>
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Standards (*.dws)|*.dws|Drawing (*.dwg)|*.dwg|DXF (*.dxf)|*.dxf",
                    Title = "Save Layer Mappings",
                    FileName = "Layers.dws",
                };
                return dialog.ShowDialog(this) == true ? dialog.FileName : null;
            };
            model.ShowLayerDialog = dialog => TranslateLayerWindow.Show(this, dialog);
            model.ShowSettings = dialog => TranslateSettingsWindow.Show(this, dialog);
        }

        /// <summary>Gets the view model.</summary>
        public LayerTranslatorViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window; the action it closed with is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered.</returns>
        public static bool Show(Window owner, LayerTranslatorViewModel model)
        {
            new LayerTranslatorWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        private string[] FromNames() =>
            FromList.SelectedItems.Cast<TranslateLayerRowViewModel>().Select(r => r.Name).ToArray();

        private string[] MappingNames() =>
            MappingList.SelectedItems.Cast<TranslateMappingRowViewModel>().Select(r => r.OldName).ToArray();

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnFromSelectionChanged(object sender, SelectionChangedEventArgs e) =>
            Model.FromSelected(FromNames());

        private void OnSelectByFilter(object sender, RoutedEventArgs e)
        {
            var names = Model.SelectByFilter();
            FromList.SelectedItems.Clear();

            foreach (TranslateLayerRowViewModel row in Model.FromRows.Where(r => names.Contains(r.Name)))
            {
                FromList.SelectedItems.Add(row);
            }
        }

        private void OnMap(object sender, RoutedEventArgs e) =>
            Model.Map(FromNames(), (ToList.SelectedItem as TranslateLayerRowViewModel)?.Name);

        private void OnMapSame(object sender, RoutedEventArgs e) => Model.MapSame();

        private void OnLoad(object sender, RoutedEventArgs e) => Model.Load();

        private void OnNew(object sender, RoutedEventArgs e) => Model.NewLayer();

        private void OnEdit(object sender, RoutedEventArgs e) => Model.Edit(MappingNames());

        private void OnRemove(object sender, RoutedEventArgs e) => Model.Remove(MappingNames());

        private void OnSave(object sender, RoutedEventArgs e) => Model.Save();

        private void OnSettings(object sender, RoutedEventArgs e) => Model.OpenSettings();

        private void OnTranslate(object sender, RoutedEventArgs e)
        {
            if (Model.Translate())
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Model.Cancel();
            DialogResult = false;
        }
    }
}

using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The classic Attribute Extraction dialog, which ATTEXT shows; Select Objects closes it, ATTEXT then asking the objects
    /// and showing it again. Its logic is <see cref="AttributeExtractionViewModel"/>.
    /// </summary>
    public partial class AttributeExtractionWindow : Window
    {
        /// <summary>Makes the dialog over its view model, giving it the file dialogs of its buttons.</summary>
        /// <param name="model">The view model.</param>
        public AttributeExtractionWindow(AttributeExtractionViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.BrowseTemplate = file => Ask(new OpenFileDialog(), file, "Template files (*.txt)|*.txt");
            model.BrowseOutput = file =>
                Ask(
                    new SaveFileDialog(),
                    file,
                    model.IsDxf ? "DXF extract files (*.dxx)|*.dxx" : "Extract files (*.txt)|*.txt"
                );
        }

        /// <summary>Gets the view model.</summary>
        public AttributeExtractionViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window until OK, Cancel or Select Objects.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        public static void Show(Window owner, AttributeExtractionViewModel model) =>
            new AttributeExtractionWindow(model) { Owner = owner }.ShowDialog();

        private string Ask(FileDialog dialog, string file, string filter)
        {
            dialog.Filter = filter + "|All files (*.*)|*.*";
            dialog.FileName = Path.GetFileName(file ?? string.Empty);
            return dialog.ShowDialog(this) == true ? dialog.FileName : null;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnSelect(object sender, RoutedEventArgs e)
        {
            Model.SelectObjects();
            DialogResult = false;
        }

        private void OnTemplate(object sender, RoutedEventArgs e) => Model.ChooseTemplate();

        private void OnOutput(object sender, RoutedEventArgs e) => Model.ChooseOutput();

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Model.TryAccept())
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}

using System;
using System.Windows;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>The name asked by New and Rename of the style dialogs; for a dimension style also the style it starts with (Create New Dimension Style).</summary>
    public partial class StyleNameWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        public StyleNameWindow(StyleNameViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.CloseRequested += (s, ok) =>
            {
                if (IsLoaded)
                {
                    DialogResult = ok;
                }
            };
            Loaded += (s, e) =>
            {
                NameBox.Focus();
                NameBox.SelectAll();
            };
        }

        /// <summary>Gets the view model.</summary>
        public StyleNameViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Asks a name over a window.</summary>
        /// <param name="owner">The window it is shown over.</param>
        /// <param name="model">What is asked.</param>
        /// <returns>The name given, or null for Cancel.</returns>
        public static string Ask(Window owner, StyleNameViewModel model)
        {
            var dialog = new StyleNameWindow(model) { Owner = owner };
            return dialog.ShowDialog() == true ? model.Name : null;
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            NameBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
            Model.Ok();
        }
    }
}

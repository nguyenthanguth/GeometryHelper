using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>AutoCAD's New and Modify Multiline Style dialog: caps, fill, joints and elements; its logic is <see cref="MlineStyleEditViewModel"/>.</summary>
    public partial class MlineStyleEditWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        public MlineStyleEditWindow(MlineStyleEditViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.PickColor = dialog => SelectColorWindow.Pick(this, dialog);
            model.PickLinetype = dialog => SelectLinetypeWindow.Pick(this, dialog);
        }

        /// <summary>Gets the view model.</summary>
        public MlineStyleEditViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether OK was pressed with the settings taken.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when OK was pressed.</returns>
        public static bool Show(Window owner, MlineStyleEditViewModel model) =>
            new MlineStyleEditWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnAdd(object sender, RoutedEventArgs e) => Model.Add();

        private void OnLinetype(object sender, RoutedEventArgs e) => Model.ChooseLinetype();

        private void OnDelete(object sender, RoutedEventArgs e) => Model.Delete();

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

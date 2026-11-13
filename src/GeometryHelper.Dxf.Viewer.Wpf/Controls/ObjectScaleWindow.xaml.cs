using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>AutoCAD's Annotation Object Scale dialog, which OBJECTSCALE shows; its logic is <see cref="ObjectScaleViewModel"/>.</summary>
    public partial class ObjectScaleWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        public ObjectScaleWindow(ObjectScaleViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public ObjectScaleViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether OK was pressed.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when OK was pressed.</returns>
        public static bool Show(Window owner, ObjectScaleViewModel model) =>
            new ObjectScaleWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        // Add Scales to Object over the scales the list does not show; its OK adds those picked.
        private void OnAdd(object sender, RoutedEventArgs e)
        {
            var picker = new ObjectScaleAddViewModel(Model.AddChoices);

            if (ObjectScaleAddWindow.Show(this, picker))
            {
                Model.Add(picker.PickedNames);
            }
        }

        private void OnDelete(object sender, RoutedEventArgs e) => Model.Delete();

        private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}

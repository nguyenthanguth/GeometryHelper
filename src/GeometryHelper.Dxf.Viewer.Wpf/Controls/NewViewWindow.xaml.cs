using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's New View / Shot Properties of the View Manager: the name, the boundary and the layer snapshot; Define window
    /// closes it with <see cref="CadDialogAction.SelectObjects"/>. A used name asks whether to redefine the view. Its logic is
    /// <see cref="NewViewViewModel"/>.
    /// </summary>
    public partial class NewViewWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public NewViewWindow(NewViewViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.AskRedefine = name => CadQuestionWindow.AskRedefineView(this, name);
        }

        /// <summary>Gets the view model.</summary>
        public NewViewViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnPick(object sender, RoutedEventArgs e)
        {
            Model.Pick();
            DialogResult = true;
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Model.Ok() || Model.Request.Action == CadDialogAction.SelectObjects)
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

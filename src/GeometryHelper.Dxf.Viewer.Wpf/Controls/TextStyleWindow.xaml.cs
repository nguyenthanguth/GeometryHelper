using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Text Style dialog (STYLE), dark as the window's other dialogs. Its logic is <see cref="TextStyleViewModel"/>;
    /// New and Rename ask the name in <see cref="StyleNameWindow"/>, a change not applied is asked about in a message box.
    /// </summary>
    public partial class TextStyleWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        public TextStyleWindow(TextStyleViewModel model)
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
            model.AskName = (title, offered) => StyleNameWindow.Ask(this, new StyleNameViewModel(title, offered, null));
            model.AskSave = question =>
            {
                MessageBoxResult answer = MessageBox.Show(this, question, "Text Style", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                return answer == MessageBoxResult.Yes ? true : answer == MessageBoxResult.No ? (bool?)false : null;
            };
        }

        /// <summary>Gets the view model.</summary>
        public TextStyleViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnApply(object sender, RoutedEventArgs e)
        {
            (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            Model.Apply();
        }
    }
}

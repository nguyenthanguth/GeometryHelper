using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Global settings, as the design draws it: 840 by 600, the nine categories at the left, the page of the one chosen at the
    /// right, Reset to defaults, Cancel, Apply and OK at the foot. Its logic is <see cref="GlobalSettingsViewModel"/>.
    /// </summary>
    public partial class GlobalSettingsWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when it is null.</exception>
        public GlobalSettingsWindow(GlobalSettingsViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.CloseRequested += (s, done) =>
            {
                if (IsVisible)
                {
                    Close();
                }
            };
        }

        /// <summary>Gets the view model.</summary>
        public GlobalSettingsViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }
    }
}

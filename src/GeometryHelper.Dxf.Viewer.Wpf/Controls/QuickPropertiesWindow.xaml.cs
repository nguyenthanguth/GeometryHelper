using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Quick Properties panel: a small window near the cursor over the objects selected, shown while
    /// <see cref="MainViewModel.IsQuickPropertiesShown"/> says so, without taking the keyboard from the canvas.
    /// </summary>
    public partial class QuickPropertiesWindow : Window
    {
        /// <summary>Makes the panel over the main view model.</summary>
        /// <param name="model">The main view model, whose <see cref="MainViewModel.QuickPropertyRows"/> it shows.</param>
        public QuickPropertiesWindow(MainViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the main view model.</summary>
        public MainViewModel Model { get; }

        /// <summary>Gets the panel's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => Model.CloseQuickProperties();
    }
}

using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Check Spelling dialog, which SPELL shows. Close ends it, SPELL then writing the words changed; Select objects
    /// closes it for the objects to be picked, SPELL showing it again. Its logic is <see cref="CheckSpellingViewModel"/>.
    /// </summary>
    public partial class CheckSpellingWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        public CheckSpellingWindow(CheckSpellingViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public CheckSpellingViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window until Close or Select objects.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        public static void Show(Window owner, CheckSpellingViewModel model) =>
            new CheckSpellingWindow(model) { Owner = owner }.ShowDialog();

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnSelectObjects(object sender, RoutedEventArgs e)
        {
            Model.SelectObjects();
            DialogResult = false;
        }

        private void OnStart(object sender, RoutedEventArgs e) => Model.Start();

        private void OnAdd(object sender, RoutedEventArgs e) => Model.AddToDictionary();

        private void OnIgnore(object sender, RoutedEventArgs e) => Model.Ignore();

        private void OnIgnoreAll(object sender, RoutedEventArgs e) => Model.IgnoreAll();

        private void OnChange(object sender, RoutedEventArgs e) => Model.Change();

        private void OnChangeAll(object sender, RoutedEventArgs e) => Model.ChangeAll();

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Model.LoadDictionary();
            DialogResult = true;
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Select Custom Arrow Block, which User Arrow... of Leader Settings opens, as board SelectArrowBlock draws it:
    /// the drawing's blocks in an editable list, the block in use chosen; OK makes the block named the arrow and closes, or
    /// says the drawing has none of the name and stays open; Cancel, Esc and the close button leave the arrow as it was. Its
    /// logic is the user arrow of <see cref="LeaderSettingsViewModel"/>.
    /// </summary>
    public partial class SelectArrowBlockWindow : Window
    {
        /// <summary>Makes the dialog over the Leader Settings it is opened from.</summary>
        /// <param name="model">The view model of Leader Settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public SelectArrowBlockWindow(LeaderSettingsViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            Loaded += (sender, e) =>
            {
                Typed.Focus();
                Typed.SelectAll();
            };
        }

        /// <summary>Gets the view model of Leader Settings.</summary>
        public LeaderSettingsViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over Leader Settings, centred on it, until a block is kept or it is cancelled.</summary>
        /// <param name="owner">The Leader Settings window.</param>
        /// <param name="model">Its view model.</param>
        /// <returns>Whether a block was kept as the arrow.</returns>
        public static bool Show(Window owner, LeaderSettingsViewModel model) =>
            new SelectArrowBlockWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        /// <summary>A block of the list picked: its name in the box, the list closed.</summary>
        private void OnBlockPicked(object sender, MouseButtonEventArgs e)
        {
            if ((sender as ListBox)?.SelectedItem is string name)
            {
                Model.UserArrowBlock = name;
                Open.IsChecked = false;
                Typed.Focus();
            }
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Model.KeepUserArrow())
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Model.DropUserArrow();
            DialogResult = false;
        }

        /// <inheritdoc/>
        protected override void OnClosed(EventArgs e)
        {
            // Closed any other way, as by Alt+F4, the arrow stays as it was.
            if (Model.IsChoosingUserArrow)
            {
                Model.DropUserArrow();
            }

            base.OnClosed(e);
        }
    }
}

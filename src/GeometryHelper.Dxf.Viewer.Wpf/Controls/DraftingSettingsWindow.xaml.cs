using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Drafting Settings (DSETTINGS): Snap and Grid, Polar Tracking and Object Snap, dark as the window's other
    /// dialogs. Its logic is <see cref="DraftingSettingsViewModel"/>.
    /// </summary>
    public partial class DraftingSettingsWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        public DraftingSettingsWindow(DraftingSettingsViewModel model)
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
        }

        /// <summary>Gets the view model.</summary>
        public DraftingSettingsViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        /// <summary>A box of the dialog: Enter takes what is typed, as leaving the box does, and does not press OK.</summary>
        private void OnBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ((TextBox)sender).GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                e.Handled = true;
            }
        }

        /// <summary>The edit row of a new additional angle, shown by New: the caret put in it.</summary>
        private void OnNewAngleShown(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                Dispatcher.BeginInvoke(new Action(() => NewAngleBox.Focus()));
            }
        }

        /// <summary>Enter in the edit row keeps the angle where it is one; Esc drops the row, and neither presses OK or Cancel.</summary>
        private void OnNewAngleKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Model.KeepNewAngle();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Model.DropNewAngle();
                e.Handled = true;
            }
        }

        /// <summary>A click elsewhere keeps the angle typed; one that is not an angle keeps the caret in the row.</summary>
        private void OnNewAngleLeft(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (Model.IsTypingAngle && !Model.KeepNewAngle() && IsActive)
            {
                Dispatcher.BeginInvoke(new Action(() => NewAngleBox.Focus()));
            }
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            // A box being typed in gives its text before the settings are taken.
            (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            Model.Ok();
        }

        private void OnApply(object sender, RoutedEventArgs e)
        {
            (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            Model.Apply();
        }
    }
}

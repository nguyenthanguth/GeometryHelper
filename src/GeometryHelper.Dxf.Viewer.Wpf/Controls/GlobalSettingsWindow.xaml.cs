using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Global settings, as the design draws it: 840 by 600, the eight categories at the left, the page of the one chosen at the
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
            model.BrowsePgpFile = PickPgpFile;
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

        /// <summary>Import acad.pgp…: a .pgp file; null where none was chosen.</summary>
        private string PickPgpFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Program parameters files (*.pgp)|*.pgp|All files (*.*)|*.*",
                Title = "Import Command Aliases",
            };
            return dialog.ShowDialog(this) == true ? dialog.FileName : null;
        }

        private void OnKeysFocus(object sender, KeyboardFocusChangedEventArgs e) =>
            ((sender as FrameworkElement)?.DataContext as ShortcutRowViewModel)?.BeginKeyCapture();

        private void OnKeysLeft(object sender, KeyboardFocusChangedEventArgs e) =>
            ((sender as FrameworkElement)?.DataContext as ShortcutRowViewModel)?.EndKeyCapture();

        /// <summary>A key pressed in a Keys cell is taken as the command's keys, Alt's held keys read as the key itself.</summary>
        private void OnKeysPressed(object sender, KeyEventArgs e)
        {
            if (!((sender as FrameworkElement)?.DataContext is ShortcutRowViewModel row))
            {
                return;
            }

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            e.Handled = row.PressKey(key, Keyboard.Modifiers);

            if (e.Handled && !row.IsCapturingKeys)
            {
                // Esc gave the old keys back: the cell is left, so that a second Esc reaches Cancel.
                Keyboard.ClearFocus();
            }
        }

        /// <summary>The list of fonts scrolls by families, so that only those shown are drawn, each in its own face.</summary>
        private void OnFontDropDownOpened(object sender, EventArgs e)
        {
            if (
                sender is ComboBox box
                && box.Template.FindName("PART_Popup", box) is Popup popup
                && FindScrollViewer(popup.Child) is ScrollViewer scroll
            )
            {
                scroll.CanContentScroll = true;
            }
        }

        private static ScrollViewer FindScrollViewer(DependencyObject root)
        {
            if (root is ScrollViewer found)
            {
                return found;
            }

            for (int i = 0; root != null && i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                ScrollViewer below = FindScrollViewer(VisualTreeHelper.GetChild(root, i));

                if (below != null)
                {
                    return below;
                }
            }

            return null;
        }
    }
}

using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Multiline Style dialog, which MLSTYLE shows, with its Create New Multiline Style, New and Modify Multiline
    /// Style and Load Multiline Styles dialogs; its logic is <see cref="MlineStyleViewModel"/>.
    /// </summary>
    public partial class MlineStyleWindow : Window
    {
        /// <summary>Makes the dialog over its view model, giving it the dialogs its buttons open.</summary>
        /// <param name="model">The view model.</param>
        public MlineStyleWindow(MlineStyleViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.AskNewStyle = naming => MlineStyleNewWindow.Show(this, naming);
            model.EditStyle = editing => MlineStyleEditWindow.Show(this, editing);
            model.AskLoad = loading =>
            {
                loading.BrowseFile = () => AskFile(new OpenFileDialog(), "acad.mln");
                return MlineStyleLoadWindow.Show(this, loading);
            };
            model.AskSaveFile = name => AskFile(new SaveFileDialog { OverwritePrompt = false }, name);
        }

        /// <summary>Gets the view model.</summary>
        public MlineStyleViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether OK was pressed.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when OK was pressed.</returns>
        public static bool Show(Window owner, MlineStyleViewModel model) =>
            new MlineStyleWindow(model) { Owner = owner }.ShowDialog() == true;

        private static string AskFile(FileDialog dialog, string name)
        {
            dialog.Filter = "Multiline style files (*.mln)|*.mln|All files (*.*)|*.*";
            dialog.FileName = name;
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnSetCurrent(object sender, RoutedEventArgs e) => Model.SetCurrent();

        private void OnNew(object sender, RoutedEventArgs e) => Model.New();

        private void OnModify(object sender, RoutedEventArgs e) => Model.Modify();

        private void OnRename(object sender, RoutedEventArgs e)
        {
            Model.BeginRename();

            if (Model.IsRenaming)
            {
                RenameBox.Focus();
                RenameBox.SelectAll();
            }
        }

        private void OnRenameKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Model.CommitRename();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Model.CancelRename();
                e.Handled = true;
            }
        }

        private void OnDelete(object sender, RoutedEventArgs e) => Model.Delete();

        private void OnLoad(object sender, RoutedEventArgs e) => Model.Load();

        private void OnSave(object sender, RoutedEventArgs e) => Model.Save();

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Model.IsRenaming && !Model.CommitRename())
            {
                return;
            }

            Model.Accept();
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}

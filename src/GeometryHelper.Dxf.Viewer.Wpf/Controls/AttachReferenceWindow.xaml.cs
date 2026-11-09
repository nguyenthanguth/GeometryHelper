using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Attach External Reference and Attach Image dialogs, which XATTACH (XA) and IMAGEATTACH (IAT) show: as
    /// AutoCAD's, the file is chosen first, in the Windows file dialog, and the dialog then shows its name, preview, path and
    /// placing; Cancel there cancels the command. Its logic is <see cref="AttachReferenceViewModel"/>.
    /// </summary>
    public partial class AttachReferenceWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public AttachReferenceWindow(AttachReferenceViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public AttachReferenceViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the file dialog, then the dialog over a window with the file chosen; the action it closed with is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered, Cancel among the answers.</returns>
        public static bool Show(Window owner, AttachReferenceViewModel model)
        {
            if (model.FileName == null)
            {
                string chosen = Choose(owner, model, null);

                if (chosen == null)
                {
                    model.Close(CadDialogAction.Cancel);
                    return true;
                }

                model.FileName = chosen;
            }

            new AttachReferenceWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        /// <summary>The Windows file dialog of the kind of file the dialog attaches; null where it was cancelled.</summary>
        private static string Choose(Window owner, AttachReferenceViewModel model, string from)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = model.IsImage ? "Select Image File" : "Select Reference File",
                Filter = model.BrowseFilter,
            };

            if (!string.IsNullOrEmpty(from))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(from);
                dialog.FileName = Path.GetFileName(from);
            }
            else if (model.Request.Document.HasFile)
            {
                dialog.InitialDirectory = Path.GetDirectoryName(model.Request.Document.FileName);
            }

            return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnBrowse(object sender, RoutedEventArgs e)
        {
            string chosen = Choose(this, Model, Model.FileName);

            if (chosen != null)
            {
                Model.FileName = chosen;
            }
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            Model.Close(CadDialogAction.Ok);
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Model.Close(CadDialogAction.Cancel);
            DialogResult = false;
        }
    }
}

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's External References palette, which XREF (XR), EXTERNALREFERENCES (ER) and IMAGE (IM) show beside the
    /// window and leave open: the drawing and its references with their status, read again as the drawing changes, as the
    /// drawing shown changes and as a file referenced changes on disk. Its logic is <see cref="ExternalReferencesViewModel"/>.
    /// </summary>
    public partial class ExternalReferencesWindow : Window
    {
        private static ExternalReferencesWindow _open;
        private MainViewModel _model;
        private CadDocument _document;

        /// <summary>Makes the palette over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public ExternalReferencesWindow(ExternalReferencesViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public ExternalReferencesViewModel Model { get; private set; }

        /// <summary>Gets the palette's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>
        /// Shows the palette over a window, left open beside it: the one open already is brought to the front and listed
        /// again. The drawing's files are watched while it is open, so that one changed on disk shows Needs reloading.
        /// </summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="mainModel">The window's view model, whose drawing shown it lists.</param>
        /// <param name="model">The view model.</param>
        public static void ShowPalette(Window owner, MainViewModel mainModel, ExternalReferencesViewModel model)
        {
            if (_open != null)
            {
                _open.Model = model;
                _open.DataContext = model;
                _open.Activate();
                return;
            }

            var window = new ExternalReferencesWindow(model) { Owner = owner };
            window.Attach(mainModel);
            _open = window;
            window.Closed += (s, e) =>
            {
                window.Detach();
                _open = null;
            };
            window.Show();
        }

        private void Attach(MainViewModel model)
        {
            _model = model;
            _model.PropertyChanged += OnModelChanged;
            Watch(_model.Document);
        }

        private void Detach()
        {
            if (_model != null)
            {
                _model.PropertyChanged -= OnModelChanged;
            }

            Watch(null);
        }

        /// <summary>Follows the drawing shown: its changes and its files.</summary>
        private void Watch(CadDocument document)
        {
            if (_document != null)
            {
                _document.Changed -= OnDocumentChanged;
                _document.Xrefs.FilesChanged -= OnFilesChanged;
                _document.Xrefs.WatchFiles = false;
            }

            _document = document;

            if (_document != null)
            {
                _document.Changed += OnDocumentChanged;
                _document.Xrefs.FilesChanged += OnFilesChanged;
                _document.Xrefs.WatchFiles = true;
            }
        }

        private void OnModelChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.Document) && !ReferenceEquals(_document, _model.Document))
            {
                Watch(_model.Document);
                Model.Refresh();
            }
        }

        private void OnDocumentChanged(object sender, CadDocumentChangedEventArgs e) => Model.Refresh();

        private void OnFilesChanged(object sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(Model.Refresh));

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnMenuButton(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var menu = (ContextMenu)FindResource((string)button.Tag);
            menu.DataContext = Model;
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void OnSelectNewPath(object sender, RoutedEventArgs e)
        {
            ReferenceRowViewModel row = Model.Selected;

            if (row?.Reference == null)
            {
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select New Path",
                Filter = row.IsImage ? "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp" : "Drawing Exchange Format (*.dxf)|*.dxf",
                FileName = row.Reference.FoundAt ?? string.Empty,
            };

            if (dialog.ShowDialog(this) == true)
            {
                Model.SetPath(dialog.FileName);
            }
        }

        private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Model.OpenCommand.CanExecute(null))
            {
                Model.OpenCommand.Execute(null);
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}

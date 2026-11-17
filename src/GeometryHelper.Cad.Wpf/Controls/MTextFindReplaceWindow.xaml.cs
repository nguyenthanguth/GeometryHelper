using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The MTEXT editor's Find and Replace, as AutoCAD 2021's: Find what, Replace with, Match case, Whole words only, Use
    /// wildcards, Match diacritics, Match half/full width forms, and Find Next, Replace and Replace All. It stays open beside the
    /// words, which show what it found selected, and closes with the editor. Its logic is <see cref="TextEditorViewModel"/>.
    /// </summary>
    public partial class MTextFindReplaceWindow : Window
    {
        private static MTextFindReplaceWindow _open;

        /// <summary>Makes the dialog over the editor.</summary>
        /// <param name="model">The editor's view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public MTextFindReplaceWindow(TextEditorViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.Closed += OnEditorClosed;
            Closed += (s, e) =>
            {
                model.Closed -= OnEditorClosed;

                if (ReferenceEquals(_open, this))
                {
                    _open = null;
                }
            };
            Loaded += (s, e) =>
            {
                FindBox.Focus();
                FindBox.SelectAll();
            };
        }

        /// <summary>Gets the editor's view model.</summary>
        public TextEditorViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog beside the editor without waiting for it; one already open for the editor is brought forward instead.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The editor's view model.</param>
        public static void Open(Window owner, TextEditorViewModel model)
        {
            if (_open != null && ReferenceEquals(_open.Model, model))
            {
                _open.Activate();
                return;
            }

            _open?.Close();
            _open = new MTextFindReplaceWindow(model) { Owner = owner };
            _open.Show();
        }

        private void OnEditorClosed(object sender, EventArgs e) => Close();

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}

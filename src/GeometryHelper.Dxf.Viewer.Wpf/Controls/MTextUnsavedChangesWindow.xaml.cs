using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The question Esc in the MTEXT editor asks where the words were changed, as AutoCAD 2021's Multiline Text - Unsaved
    /// Changes: "Do you want to save your text changes?", Yes to save and close, No to drop the changes and close, Cancel to
    /// go on editing, and Always perform my current choice.
    /// </summary>
    public partial class MTextUnsavedChangesWindow : Window
    {
        /// <summary>Makes the question.</summary>
        /// <param name="model">The question's state: whether Always perform my current choice is ticked.</param>
        public MTextUnsavedChangesWindow(MTextUnsavedChangesViewModel model)
        {
            InitializeComponent();
            Model = model ?? throw new ArgumentNullException(nameof(model));
            DataContext = model;
        }

        /// <summary>Gets the question's state.</summary>
        public MTextUnsavedChangesViewModel Model { get; }

        /// <summary>Gets the question's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Gets the answer: true for Yes, false for No, null for Cancel and the window closed.</summary>
        public bool? Answer { get; private set; }

        /// <summary>Asks the question over a window.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The question's state, its <see cref="MTextUnsavedChangesViewModel.AlwaysPerform"/> set as ticked.</param>
        /// <returns>True for Yes, false for No, null for Cancel.</returns>
        public static bool? Ask(Window owner, MTextUnsavedChangesViewModel model)
        {
            var question = new MTextUnsavedChangesWindow(model);

            if (owner != null && owner.IsVisible)
            {
                question.Owner = owner;
            }
            else
            {
                question.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            question.ShowDialog();
            return question.Answer;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnAlwaysText(object sender, MouseButtonEventArgs e) => AlwaysBox.IsChecked = AlwaysBox.IsChecked != true;

        private void OnYes(object sender, RoutedEventArgs e)
        {
            Answer = true;
            DialogResult = true;
        }

        private void OnNo(object sender, RoutedEventArgs e)
        {
            Answer = false;
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Answer = null;
            DialogResult = false;
        }
    }
}

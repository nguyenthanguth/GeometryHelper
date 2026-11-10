using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The question BCLOSE asks of a block with changes not saved, as AutoCAD 2021's task dialog: "Save changes to
    /// DOOR_900?", Save the changes (every reference updates), Discard them and close, or Cancel to go on editing.
    /// </summary>
    public partial class BlockCloseWindow : Window
    {
        /// <summary>Makes the question about a block.</summary>
        /// <param name="name">The name of the block, or &lt;Current Drawing&gt;.</param>
        public BlockCloseWindow(string name)
        {
            InitializeComponent();
            Question.Text = "Save changes to " + name + "?";
            SaveTitle.Text = "Save the changes to " + name;
            SaveDetail.Text = "Every reference to " + name + " in the drawing updates. Enter.";
        }

        /// <summary>Gets the question's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Gets the answer: Save, Discard, or null for Cancel and the window closed.</summary>
        public string Answer { get; private set; }

        /// <summary>Asks the question over a window.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="name">The name of the block.</param>
        /// <returns>Save, Discard, or null for Cancel.</returns>
        public static string Ask(Window owner, string name)
        {
            var question = new BlockCloseWindow(name);

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

        private void OnSave(object sender, RoutedEventArgs e)
        {
            Answer = "Save";
            DialogResult = true;
        }

        private void OnDiscard(object sender, RoutedEventArgs e)
        {
            Answer = "Discard";
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Answer = null;
            DialogResult = false;
        }
    }
}

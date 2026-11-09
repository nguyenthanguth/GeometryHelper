using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// A question with two answers, in the look of the Messages board: an amber triangle, a title, a line, a detail in monospace
    /// and two buttons. Asked before a plugin outside the trusted folders is loaded, the detail being the file's full path.
    /// </summary>
    public partial class CadQuestionWindow : Window
    {
        /// <summary>Makes the question.</summary>
        /// <param name="title">The title, as "Load a plugin from an untrusted folder?".</param>
        /// <param name="text">The line under it.</param>
        /// <param name="detail">The detail in monospace, as the full path.</param>
        /// <param name="yes">The label of the button that answers yes, as "Load".</param>
        /// <param name="no">The label of the button that answers no, as "Don't load".</param>
        public CadQuestionWindow(string title, string text, string detail, string yes, string no)
        {
            InitializeComponent();
            HeadingText.Text = title ?? string.Empty;
            BodyText.Text = text ?? string.Empty;
            DetailText.Text = detail ?? string.Empty;
            YesButton.Content = yes ?? "Yes";
            NoButton.Content = no ?? "No";
        }

        /// <summary>Gets the question's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Gets the answer given: Save for the yes button, Don't save for no, Cancel for the third or for the window closed.</summary>
        public CadSaveChoice Answer { get; private set; } = CadSaveChoice.Cancel;

        /// <summary>
        /// Asks whether to save the changes of documents before they close, as the Messages board of the design draws it: the
        /// save glyph in blue, "Save changes to plan.dxf?", then Don't save, Cancel and Save.
        /// </summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="documents">The documents with changes not saved.</param>
        /// <returns>The answer.</returns>
        public static CadSaveChoice AskSave(Window owner, IReadOnlyList<CadDocument> documents)
        {
            var question = ForSave(documents);

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

        /// <summary>Makes the question <see cref="AskSave"/> shows, not shown.</summary>
        /// <param name="documents">The documents with changes not saved.</param>
        /// <returns>The question.</returns>
        public static CadQuestionWindow ForSave(IReadOnlyList<CadDocument> documents)
        {
            int count = documents?.Count ?? 0;
            string title = count == 1 ? "Save changes to " + documents[0].Title + "?" : "Save changes to " + count.ToString(CultureInfo.InvariantCulture) + " drawings?";
            string text = count == 1 ? "The drawing has changes not saved. Closing it without saving drops them." : "These drawings have changes not saved; closing without saving drops them all.";
            string detail = count == 1 ? documents[0].FileName ?? documents[0].Title : string.Join("\n", documents.Select(d => d.Title));
            var question = new CadQuestionWindow(title, text, detail, "Save", "Don't save");
            question.ThirdButton.Content = "Cancel";
            question.ThirdButton.Visibility = Visibility.Visible;
            question.NoButton.IsCancel = false;
            question.ThirdButton.IsCancel = true;
            question.Glyph.Data = System.Windows.Media.Geometry.Parse("M5 3h11l3 3v15H5zM8 3v5h7V3M8 21v-7h8v7");
            question.Glyph.Stroke = (Brush)question.FindResource("GhAccent");
            return question;
        }

        /// <summary>
        /// Asks, on Esc in the MTEXT editor, whether to keep the changes, as AutoCAD asks "Save changes to MText?": Save keeps
        /// them, Don't save drops them, Cancel goes on editing.
        /// </summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <returns>True to keep, false to drop, null to go on editing.</returns>
        public static bool? AskKeepMTextChanges(Window owner)
        {
            var question = new CadQuestionWindow("Save changes to MText?", "The words were changed in the editor. Don't save closes it as the MTEXT was.", string.Empty, "Save", "Don't save");
            question.ThirdButton.Content = "Cancel";
            question.ThirdButton.Visibility = Visibility.Visible;
            question.NoButton.IsCancel = false;
            question.ThirdButton.IsCancel = true;

            if (owner != null && owner.IsVisible)
            {
                question.Owner = owner;
            }
            else
            {
                question.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            question.ShowDialog();
            return question.Answer == CadSaveChoice.Save ? true : question.Answer == CadSaveChoice.DontSave ? (bool?)false : null;
        }

        private void OnThird(object sender, RoutedEventArgs e)
        {
            Answer = CadSaveChoice.Cancel;
            DialogResult = false;
        }

        /// <summary>
        /// Asks whether to load a plugin outside the trusted folders, naming its full path; what the manager's
        /// <c>ConfirmUntrusted</c> calls.
        /// </summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="path">The file's full path.</param>
        /// <returns>Whether to load it.</returns>
        public static bool ConfirmUntrustedPlugin(Window owner, string path)
        {
            var question = new CadQuestionWindow(
                "Load a plugin from a folder that is not trusted?",
                "Loading a plugin runs its code with your rights. Load it only if you know where it comes from; to stop being asked, add its folder to the trusted folders in the Plugin manager.",
                path,
                "Load",
                "Don't load");

            if (owner != null && owner.IsVisible)
            {
                question.Owner = owner;
            }
            else
            {
                question.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            return question.ShowDialog() == true;
        }

        private void OnYes(object sender, RoutedEventArgs e)
        {
            Answer = CadSaveChoice.Save;
            DialogResult = true;
        }

        private void OnNo(object sender, RoutedEventArgs e)
        {
            Answer = CadSaveChoice.DontSave;
            DialogResult = false;
        }
    }
}

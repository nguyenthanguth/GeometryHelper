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
            string title =
                count == 1
                    ? "Save changes to " + documents[0].Title + "?"
                    : "Save changes to " + count.ToString(CultureInfo.InvariantCulture) + " drawings?";
            string text =
                count == 1
                    ? "The drawing has changes not saved. Closing it without saving drops them."
                    : "These drawings have changes not saved; closing without saving drops them all.";
            string detail =
                count == 1
                    ? documents[0].FileName ?? documents[0].Title
                    : string.Join("\n", documents.Select(d => d.Title));
            var question = new CadQuestionWindow(title, text, detail, "Save", "Don't save");
            question.ThirdButton.Content = "Cancel";
            question.ThirdButton.Visibility = Visibility.Visible;
            question.NoButton.IsCancel = false;
            question.ThirdButton.IsCancel = true;
            question.Glyph.Data = System.Windows.Media.Geometry.Parse("M5 3h11l3 3v15H5zM8 3v5h7V3M8 21v-7h8v7");
            question.Glyph.Stroke = (Brush)question.FindResource("GhAccent");
            return question;
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
                "Don't load"
            );

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

        /// <summary>
        /// Asks whether a block INSERT brings from another drawing redefines the drawing's own of its name, as AutoCAD asks:
        /// "Block GB already exists. Redefine?".
        /// </summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="name">The name of the block.</param>
        /// <returns>Whether to redefine it.</returns>
        public static bool AskRedefineBlock(Window owner, string name)
        {
            var question = new CadQuestionWindow(
                "Block " + name + " already exists. Redefine?",
                "Redefine replaces the drawing's block with the one of the other drawing, and every INSERT of it shows the new one. Don't redefine places the drawing's own.",
                string.Empty,
                "Redefine",
                "Don't redefine"
            );

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

        /// <summary>Asks whether New View redefines the view of a name used, as the View Manager asks: "View Stair core already exists. Redefine?".</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="name">The name of the view.</param>
        /// <returns>Whether to redefine it.</returns>
        public static bool AskRedefineView(Window owner, string name)
        {
            var question = new CadQuestionWindow(
                "View " + name + " already exists. Redefine?",
                "Redefine saves the view on screen, or the window picked, under the name in the place of the view it had.",
                string.Empty,
                "Redefine",
                "Don't redefine"
            );

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

        /// <summary>
        /// Asks whether to purge one object, as AutoCAD's Confirm Purge asks with Confirm each item to be purged: "Purge layer
        /// "TMP"?", then Purge this item, Skip this item and Cancel.
        /// </summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="question">The question, "Purge layer "TMP"?".</param>
        /// <returns>True to purge it, false to keep it, null to stop the purge.</returns>
        public static bool? ConfirmPurgeItem(Window owner, string question)
        {
            var dialog = new CadQuestionWindow(
                question,
                "Purge removes the item from the drawing; U takes the whole purge back.",
                string.Empty,
                "Purge this item",
                "Skip this item"
            );
            dialog.ThirdButton.Content = "Cancel";
            dialog.ThirdButton.Visibility = Visibility.Visible;
            dialog.NoButton.IsCancel = false;
            dialog.ThirdButton.IsCancel = true;

            if (owner != null && owner.IsVisible)
            {
                dialog.Owner = owner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            dialog.ShowDialog();
            return dialog.Answer == CadSaveChoice.Save ? true
                : dialog.Answer == CadSaveChoice.DontSave ? (bool?)false
                : null;
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

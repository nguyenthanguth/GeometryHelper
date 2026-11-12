using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        /// Asks whether a block INSERT brings from another drawing, or the Block dialog is given, redefines the drawing's own of
        /// its name, in the words of AutoCAD 2021's task dialog "Block - Redefine Block": "GB" is already defined as a block in
        /// this drawing, would you like to redefine this block reference?, then Redefine block and Don't redefine "GB".
        /// </summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="name">The name of the block.</param>
        /// <returns>Whether to redefine it.</returns>
        public static bool AskRedefineBlock(Window owner, string name) =>
            Shown(
                owner,
                new CadQuestionWindow(
                    CadQuestionTexts.RedefineBlockTitle,
                    CadQuestionTexts.RedefineBlockQuestion(name),
                    CadQuestionTexts.RedefineBlock
                        + ": "
                        + CadQuestionTexts.RedefineBlockDetail
                        + "\n"
                        + CadQuestionTexts.KeepBlock(name)
                        + ": "
                        + CadQuestionTexts.KeepBlockDetail,
                    CadQuestionTexts.RedefineBlock,
                    CadQuestionTexts.KeepBlock(name)
                )
            ) == true;

        /// <summary>
        /// Asks whether New View replaces the view of a name used, in the words of the task dialog AutoCAD 2021 gives a manager
        /// over a name used: "View - Already Exists", "Stair core already exists.  Do you want to replace it?", Replace and Cancel.
        /// </summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="name">The name of the view.</param>
        /// <returns>Whether to replace it.</returns>
        public static bool AskRedefineView(Window owner, string name) =>
            Shown(
                owner,
                new CadQuestionWindow(
                    CadQuestionTexts.ViewExistsTitle,
                    CadQuestionTexts.ViewExists(name),
                    string.Empty,
                    CadQuestionTexts.ReplaceView,
                    CadQuestionTexts.Cancel
                )
            ) == true;

        /// <summary>
        /// Asks whether to purge one object, as AutoCAD 2021's "Purge - Confirm Purge" asks with Confirm each item to be
        /// purged: "You are about to purge layer TMP. What do you want to do?", then Purge this item, Skip this item and Cancel.
        /// </summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="question">The question, "You are about to purge layer TMP. What do you want to do?".</param>
        /// <returns>True to purge it, false to keep it, null to stop the purge.</returns>
        public static bool? ConfirmPurgeItem(Window owner, string question)
        {
            var dialog = new CadQuestionWindow(
                CadQuestionTexts.PurgeTitle,
                question,
                string.Empty,
                CadQuestionTexts.PurgeThisItem,
                CadQuestionTexts.SkipThisItem
            );
            dialog.ThirdButton.Content = CadQuestionTexts.Cancel;
            dialog.ThirdButton.Visibility = Visibility.Visible;
            dialog.NoButton.IsCancel = false;
            dialog.ThirdButton.IsCancel = true;
            Shown(owner, dialog);
            return dialog.Answer == CadSaveChoice.Save ? true
                : dialog.Answer == CadSaveChoice.DontSave ? (bool?)false
                : null;
        }

        /// <summary>
        /// Asks Reset's question of Edit Drawing Scales in the words of AutoCAD 2021's task dialog: "Scale List - Reset",
        /// "Resetting will restore a default list of scales for the drawing. Which default scale list do you want to
        /// restore?", the detail on unreferenced scales, then Metric scales, Imperial scales, Metric and Imperial scales and
        /// Cancel.
        /// </summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <returns>The list chosen; <see cref="ScaleListResetChoice.Cancel"/> for none.</returns>
        public static ScaleListResetChoice AskResetScaleList(Window owner)
        {
            CadQuestionWindow dialog = ForChoice(
                ScaleListEditViewModel.ResetTitle,
                ScaleListEditViewModel.ResetQuestion,
                ScaleListEditViewModel.ResetDetail,
                ScaleListEditViewModel.ResetMetric,
                ScaleListEditViewModel.ResetImperial,
                ScaleListEditViewModel.ResetMetricAndImperial,
                CadQuestionTexts.Cancel
            );
            Shown(owner, dialog);
            return dialog.Chosen >= 0 && dialog.Chosen <= 2
                ? (ScaleListResetChoice)(dialog.Chosen + 1)
                : ScaleListResetChoice.Cancel;
        }

        /// <summary>
        /// Asks Delete's question of Edit Drawing Scales over a selection that holds scales in use, as AutoCAD 2021's message
        /// box does: "Scales in use cannot be deleted." and "Delete selected scales not in use?", Yes and No.
        /// </summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <returns>Whether to delete the scales not in use.</returns>
        public static bool AskDeleteScalesInUse(Window owner)
        {
            string[] lines = ScaleListEditViewModel.DeleteInUseQuestion.Split(
                new[] { "\r\n" },
                2,
                StringSplitOptions.None
            );
            return Shown(owner, new CadQuestionWindow(lines[0].Trim(), lines[1], string.Empty, "Yes", "No")) == true;
        }

        /// <summary>Asks Add Scale's question of Edit Drawing Scales over a name used: "1:25 already exists.  Would you like to redefine this scale?".</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="name">The name of the scale.</param>
        /// <returns>Whether to redefine it.</returns>
        public static bool AskRedefineScale(Window owner, string name) =>
            Shown(
                owner,
                new CadQuestionWindow(
                    ScaleEditViewModel.RedefineQuestion(name),
                    string.Empty,
                    string.Empty,
                    "Yes",
                    "No"
                )
            ) == true;

        /// <summary>
        /// Asks whether New Layer State to Save overwrites a state of the name given, in the words of AutoCAD 2021's task
        /// dialog "Layer - Overwrite Layer State": "Do you want to overwrite Plan with the drawing's current layer state?", Yes
        /// and No.
        /// </summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="name">The name of the state.</param>
        /// <returns>Whether to replace it.</returns>
        public static bool AskReplaceLayerState(Window owner, string name) =>
            Shown(
                owner,
                new CadQuestionWindow(
                    CadQuestionTexts.OverwriteLayerStateTitle,
                    CadQuestionTexts.OverwriteLayerState(name),
                    string.Empty,
                    "Yes",
                    "No"
                )
            ) == true;

        /// <summary>
        /// Asks whether the Block Attribute Manager saves the changes made to a block before it shows another, in the words of
        /// AutoCAD's "Block Attribute Manager Alert": "The attributes in the current block have been changed." and "Do you want
        /// to save these changes?", Yes and No.
        /// </summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="block">The name of the block changed, which the alert does not name.</param>
        /// <returns>Whether to save them.</returns>
        public static bool AskSaveAttributeChanges(Window owner, string block) =>
            Shown(
                owner,
                new CadQuestionWindow(
                    CadQuestionTexts.AttributeChangesLine,
                    CadQuestionTexts.AttributeChangesQuestion,
                    string.Empty,
                    "Yes",
                    "No"
                )
            ) == true;

        /// <summary>Shows a message with OK alone, as the Layer manager shows why a rename is refused.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="message">The message.</param>
        public static void ShowMessage(Window owner, string message) =>
            ShowMessage(owner, string.Empty, message, string.Empty);

        /// <summary>Shows a message with OK alone, under a title and with a detail, as AutoCAD's task dialogs with Close alone.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="title">The title, as "Layer - Rename"; empty for none.</param>
        /// <param name="message">The message, as "The selected layer cannot be renamed.".</param>
        /// <param name="detail">The detail under it; empty for none.</param>
        public static void ShowMessage(Window owner, string title, string message, string detail)
        {
            bool titled = !string.IsNullOrEmpty(title);
            var dialog = new CadQuestionWindow(
                titled ? title : message,
                titled ? message : string.Empty,
                detail,
                "OK",
                null
            );
            dialog.NoButton.Visibility = Visibility.Collapsed;
            dialog.YesButton.IsCancel = true;
            Shown(owner, dialog);
        }

        /// <summary>Gets the index of the button a question made by <see cref="ForChoice"/> was answered with; -1 for none.</summary>
        public int Chosen { get; private set; } = -1;

        /// <summary>
        /// Makes a question with a button for each answer, left to right, the last of them the one that gives it up (Escape),
        /// not shown.
        /// </summary>
        /// <param name="title">The title.</param>
        /// <param name="text">The line under it.</param>
        /// <param name="detail">The detail in monospace.</param>
        /// <param name="labels">The labels of the buttons, the last one's the way out.</param>
        /// <returns>The question.</returns>
        public static CadQuestionWindow ForChoice(string title, string text, string detail, params string[] labels)
        {
            var question = new CadQuestionWindow(title, text, detail, null, null) { Width = 640 };
            question.Buttons.Children.Clear();

            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                var button = new Button
                {
                    Content = labels[i],
                    Style = (Style)question.FindResource("GhDialogButton"),
                    Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0),
                    IsCancel = i == labels.Length - 1,
                };
                button.Click += (sender, e) =>
                {
                    question.Chosen = index;
                    question.DialogResult = index < labels.Length - 1;
                };
                question.Buttons.Children.Add(button);
            }

            return question;
        }

        private static bool? Shown(Window owner, CadQuestionWindow dialog)
        {
            if (owner != null && owner.IsVisible)
            {
                dialog.Owner = owner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            return dialog.ShowDialog();
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

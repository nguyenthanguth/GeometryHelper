using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// One way Paste Special offers to paste: its name in the list, the paste it runs (PASTECLIP, PASTEBLOCK, PASTEORIG or
    /// TEXT) and what it gives, the Result line.
    /// </summary>
    public sealed class PasteFormatViewModel
    {
        internal PasteFormatViewModel(string name, string answer, string result, bool isEnabled)
        {
            Name = name;
            Answer = answer;
            Result = result;
            IsEnabled = isEnabled;
        }

        /// <summary>Gets the name in the As list: "Drawing entities as a block (PASTEBLOCK)".</summary>
        public string Name { get; }

        /// <summary>Gets the paste it runs: PASTECLIP, PASTEBLOCK, PASTEORIG or TEXT; null for one not offered.</summary>
        public string Answer { get; }

        /// <summary>Gets what it gives, as the Result line says.</summary>
        public string Result { get; }

        /// <summary>Gets whether it can be chosen: the Clipboard holds what it pastes, and it is built (an image is not, as OLE is parked).</summary>
        public bool IsEnabled { get; }
    }

    /// <summary>
    /// The Paste Special dialog (PASTESPEC) as the design draws it: where the Clipboard's contents came from, Paste chosen and
    /// Paste Link not offered, the ways to paste them in the As list, each with what it gives, and Display As Icon greyed, as
    /// OLE is not built. OK answers the PASTESPEC command with the way chosen (<see cref="Answer"/>).
    /// </summary>
    public sealed class PasteSpecialViewModel : ObservableObject
    {
        private PasteFormatViewModel _selected;

        /// <summary>Makes the dialog for what the Clipboard holds.</summary>
        /// <param name="data">What the Clipboard holds; null for nothing.</param>
        public PasteSpecialViewModel(CadClipboardData data)
        {
            bool objects = data != null && data.HasObjects;
            bool text = data?.Text != null;

            if (objects)
            {
                Source = "GeometryHelper Drawing entities";
                SourceDetail =
                    Count(data.Count)
                    + (data.Count == 1 ? " object" : " objects")
                    + " copied from "
                    + (data.SourceName ?? "a drawing")
                    + (data.SpaceName != null ? " · " + data.SpaceName : string.Empty);
            }
            else if (text)
            {
                Source = "Unicode text";
                SourceDetail = "copied from another application";
            }
            else
            {
                Source = "The Clipboard is empty";
                SourceDetail = string.Empty;
            }

            Formats = new List<PasteFormatViewModel>
            {
                new PasteFormatViewModel(
                    "Drawing entities (PASTECLIP)",
                    "PASTECLIP",
                    "Inserts the clipboard contents as the objects they were, their layers and styles brought along, at the point you pick: the same as PASTECLIP.",
                    objects
                ),
                new PasteFormatViewModel(
                    "Drawing entities as a block (PASTEBLOCK)",
                    "PASTEBLOCK",
                    "Inserts the clipboard contents as one block reference, named A$C followed by a number, at the point you pick: the same as PASTEBLOCK.",
                    objects
                ),
                new PasteFormatViewModel(
                    "Drawing entities at original coordinates (PASTEORIG)",
                    "PASTEORIG",
                    "Inserts the clipboard contents where they were in the drawing they were copied from, which is another drawing: the same as PASTEORIG.",
                    objects
                ),
                new PasteFormatViewModel(
                    "Multiline text (MTEXT)",
                    "TEXT",
                    "Inserts the clipboard's text as multiline text in the current text style, each line a paragraph, at the point you pick.",
                    text
                ),
                new PasteFormatViewModel(
                    "Unicode text",
                    "TEXT",
                    "Inserts the clipboard's text as it reads, as multiline text with no formatting, at the point you pick.",
                    text
                ),
                new PasteFormatViewModel(
                    "Image (PNG)",
                    null,
                    "Pasting a picture as an OLE object is not built yet.",
                    false
                ),
            };

            _selected = Formats.FirstOrDefault(f => f.IsEnabled);
        }

        /// <summary>Gets what the Clipboard holds, as the Source line names it.</summary>
        public string Source { get; }

        /// <summary>Gets how many objects came from where, the second line of Source.</summary>
        public string SourceDetail { get; }

        /// <summary>Gets the ways to paste, in the order of the As list.</summary>
        public IReadOnlyList<PasteFormatViewModel> Formats { get; }

        /// <summary>Gets or sets the way chosen; one that cannot be chosen is not taken.</summary>
        public PasteFormatViewModel Selected
        {
            get => _selected;
            set
            {
                if (value != null && !value.IsEnabled)
                {
                    return;
                }

                if (Set(ref _selected, value))
                {
                    Raise(nameof(Result));
                    Raise(nameof(CanPaste));
                }
            }
        }

        /// <summary>Gets what the way chosen gives, the Result line.</summary>
        public string Result => _selected?.Result ?? "The Clipboard holds nothing to paste.";

        /// <summary>Gets whether OK pastes anything.</summary>
        public bool CanPaste => _selected != null;

        /// <summary>Gets the paste OK runs: PASTECLIP, PASTEBLOCK, PASTEORIG or TEXT; null for none.</summary>
        public string Answer => _selected?.Answer;

        private static string Count(int value) => value.ToString("#,0", CultureInfo.InvariantCulture);
    }
}

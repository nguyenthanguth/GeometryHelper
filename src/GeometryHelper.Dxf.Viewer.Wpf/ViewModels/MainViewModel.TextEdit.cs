using System;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The clipboard of the window's drawings, text edited in place (TEXTEDIT's edit box, the MTEXT editor and its Text Editor
    /// tab) and Paste Special.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private ICadClipboard _clipboard = new CadMemoryClipboard();
        private CadTextEditorTab _textEditorTab;
        private CadInPlaceEdit _inPlace;
        private TextEditorViewModel _textEditor;

        /// <summary>Raised when a text starts or stops being edited in place, so that the window shows or hides its editor.</summary>
        public event EventHandler InPlaceEditChanged;

        /// <summary>
        /// Gets or sets the clipboard of every drawing open (<see cref="CadCommandSession.Clipboard"/>): one in memory shared by
        /// them until set; the window sets the Windows clipboard (<see cref="CadWindowsClipboard"/>), so that objects go
        /// between other copies of the app too, and a test leaves the one in memory.
        /// </summary>
        public ICadClipboard Clipboard
        {
            get => _clipboard;
            set
            {
                _clipboard = value ?? new CadMemoryClipboard();

                foreach (KnownFile file in _files.Values.Where(f => f.Session != null))
                {
                    file.Session.Clipboard = _clipboard;
                }
            }
        }

        /// <summary>Gets or sets what shows Paste Special and gives back the paste chosen (PASTECLIP, PASTEBLOCK, PASTEORIG, TEXT), or null where it was cancelled.</summary>
        public Func<PasteSpecialViewModel, string> ShowPasteSpecial { get; set; }

        /// <summary>Gets or sets what asks, on Esc in the MTEXT editor, whether to keep the changes: true to keep, false to drop, null to go on editing.</summary>
        public Func<bool?> AskKeepMTextChanges { get; set; }

        /// <summary>Gets the contextual Text Editor tab, shown while an MTEXT is being written or edited.</summary>
        public CadWorkspaceExtension TextEditorTab
        {
            get
            {
                if (_textEditorTab == null)
                {
                    _textEditorTab = new CadTextEditorTab();
                    _builtInTabs.Add(_textEditorTab);
                }

                return _textEditorTab;
            }
        }

        /// <summary>Gets the text edited in place now, as the prompt of the command running asks it; null for none.</summary>
        public CadInPlaceEdit InPlaceEdit => _inPlace;

        /// <summary>Gets whether a single-line text is edited in its edit box.</summary>
        public bool IsTextEditShown => _inPlace?.Kind == CadInPlaceKind.Text;

        /// <summary>Gets whether an MTEXT is edited in the MTEXT editor.</summary>
        public bool IsMTextEditorShown => _inPlace?.Kind == CadInPlaceKind.MText;

        /// <summary>Gets the MTEXT editor and its Text Editor tab while an MTEXT is edited; null otherwise.</summary>
        public TextEditorViewModel TextEditor => _textEditor;

        /// <summary>Gets the items of the Paste button's arrow, as AutoCAD's: Paste, Paste as Block, Paste to Original Coordinates, Paste Special.</summary>
        /// <returns>The items.</returns>
        public System.Collections.Generic.IReadOnlyList<CadMenuItemViewModel> PasteMenu() => new[]
        {
            new CadMenuItemViewModel("Paste", () => StartCommand("PASTECLIP"), HasDocument, "PASTECLIP  Ctrl+V"),
            new CadMenuItemViewModel("Paste as Block", () => StartCommand("PASTEBLOCK"), HasDocument, "PASTEBLOCK  Ctrl+Shift+V"),
            new CadMenuItemViewModel("Paste to Original Coordinates", () => StartCommand("PASTEORIG"), HasDocument, "PASTEORIG"),
            new CadMenuItemViewModel("Paste Special…", () => StartCommand("PASTESPEC"), HasDocument, "PASTESPEC"),
        };

        /// <summary>Ends the edit of a single-line text with its words, as Enter or a click outside its box ends it: the same words change nothing.</summary>
        /// <param name="words">The words.</param>
        public void CommitInPlace(string words)
        {
            CadCommandSession session = Session;

            if (session?.Prompt?.InPlace == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(words))
            {
                session.Enter();
            }
            else
            {
                session.Feed(words);
            }
        }

        /// <summary>Cancels the edit of the text, as Esc in its edit box: the text is as it was, and TEXTEDIT asks for the next.</summary>
        public void CancelInPlace()
        {
            if (Session?.Prompt?.InPlace != null)
            {
                Session.Enter();
            }
        }

        /// <summary>Closes the MTEXT editor, keeping its words and frame (Close Text Editor, a click outside) or not.</summary>
        /// <param name="keep">Whether to keep the changes.</param>
        public void CloseTextEditor(bool keep)
        {
            TextEditorViewModel editor = _textEditor;

            if (editor == null)
            {
                return;
            }

            if (!keep)
            {
                CancelInPlace();
                return;
            }

            editor.WriteBox();
            CommitInPlace(editor.IsModified || editor.Edit.Handle == null ? editor.Editor.Contents : editor.Edit.Value);
        }

        /// <summary>Esc in the MTEXT editor: asks whether to keep the changes where there are any, as AutoCAD; with nothing changed it just closes.</summary>
        public void EscapeTextEditor()
        {
            if (_textEditor == null)
            {
                return;
            }

            // With no one to ask, the changes are kept, as Close Text Editor keeps them.
            bool? keep = !_textEditor.IsModified ? false : AskKeepMTextChanges != null ? AskKeepMTextChanges() : true;

            if (keep.HasValue)
            {
                CloseTextEditor(keep.Value);
            }
        }

        /// <summary>A double click on an object: a text, an MTEXT or a dimension is edited in place, as TEXTEDIT; nothing while a command runs.</summary>
        /// <param name="handle">The handle of the object.</param>
        /// <returns>Whether an edit started.</returns>
        public bool EditTextAt(string handle)
        {
            CadCommandSession session = Session;
            DxfEntity entity = Document?.FindEntity(handle);

            if (session == null || session.IsRunning || !(entity is DxfDBText || entity is DxfMText || entity is DxfDimension) || entity is DxfAttributeDefinition)
            {
                return false;
            }

            StartCommand("TEXTEDIT");
            session.FeedSelection(new[] { handle });
            return session.Prompt?.InPlace != null;
        }

        /// <summary>Follows the prompt of the command running: the editor shown when it asks for a text in place, hidden when it no longer does.</summary>
        private void SyncInPlace()
        {
            CadInPlaceEdit now = Session?.Prompt?.InPlace;

            if (ReferenceEquals(now, _inPlace))
            {
                return;
            }

            _inPlace = now;

            if (now != null && now.Kind == CadInPlaceKind.MText)
            {
                StyleFontOf(now.StyleName, out string font, out bool shx);
                double spacing = now.Handle != null && Document?.FindEntity(now.Handle) is DxfMText mtext ? mtext.LineSpacingFactor : 1.0;
                _textEditor = new TextEditorViewModel(this, now, font, shx, spacing);
                ((CadTextEditorTab)TextEditorTab).Show(_textEditor);
                SelectedTab = TextEditorTab;
            }
            else
            {
                _textEditor = null;

                if (_textEditorTab != null && _textEditorTab.IsTabVisible)
                {
                    _textEditorTab.Hide();

                    if (SelectedTab == _textEditorTab || SelectedTab == null)
                    {
                        SelectedTab = null;
                    }
                }
            }

            Raise(nameof(InPlaceEdit));
            Raise(nameof(IsTextEditShown));
            Raise(nameof(IsMTextEditorShown));
            Raise(nameof(TextEditor));
            InPlaceEditChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>The TrueType family the edit box of a text of a style writes in: the style's, or null for an SHX style, which the box shows in the canvas's own font.</summary>
        internal string TrueTypeFamilyOf(string styleName)
        {
            StyleFontOf(styleName, out string font, out bool shx);
            return shx ? null : font;
        }

        /// <summary>The colour the canvas draws an object in, 0xRRGGBB; white where it draws none.</summary>
        internal int DrawnRgbOf(string handle)
        {
            CadDocument document = Document;

            if (document == null || handle == null)
            {
                return 0xFFFFFF;
            }

            DisplayItem2 item = document.GetDisplay(document.CurrentSpace).Items.FirstOrDefault(i => string.Equals(i.Handle, handle, StringComparison.OrdinalIgnoreCase));
            return item?.Style.Rgb ?? 0xFFFFFF;
        }

        /// <summary>The colour of the layer of an object, or of the current layer for an object being made, 0xRRGGBB.</summary>
        internal int LayerRgbOf(string handle)
        {
            DxfDatabase database = Document?.Database;
            string layer = Document?.FindEntity(handle)?.Layer ?? CurrentLayer?.Name;

            if (database == null || string.IsNullOrEmpty(layer) || !database.LayerTable.Has(layer))
            {
                return 0xFFFFFF;
            }

            return database.LayerTable[layer].Color.ColorValue & 0xFFFFFF;
        }

        /// <summary>The font of a text style: its TrueType family (the style's typeface, or the family of its file), else its SHX file.</summary>
        private void StyleFontOf(string styleName, out string font, out bool shx)
        {
            font = null;
            shx = false;
            DxfDatabase database = Document?.Database;
            string name = string.IsNullOrEmpty(styleName) ? "Standard" : styleName;

            if (database == null || !database.TextStyleTable.Has(name))
            {
                return;
            }

            DxfTextStyleTableRecord record = database.TextStyleTable[name];
            string file = record.FileName ?? string.Empty;
            string typeface = record.Font.TypeFace;

            if (!string.IsNullOrEmpty(typeface))
            {
                font = typeface;
            }
            else if (file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
            {
                font = CadTrueTypeFonts.FamilyOf(file) ?? System.IO.Path.GetFileNameWithoutExtension(file);
            }
            else if (file.Length > 0)
            {
                font = file.EndsWith(".shx", StringComparison.OrdinalIgnoreCase) ? file.Substring(0, file.Length - 4) : file;
                shx = true;
            }
        }

        /// <summary>PASTESPEC: the Paste Special dialog over the window, its choice the answer of the command.</summary>
        private void OnPasteSpecialRequested(CadUiRequestEventArgs e)
        {
            if (ShowPasteSpecial == null)
            {
                return;
            }

            CadClipboardData data = null;

            try
            {
                data = Session?.Clipboard.GetData();
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                GeometryHelperLog.Warn("MainViewModel: the Clipboard could not be read for Paste Special.", exception);
            }

            e.Answer = ShowPasteSpecial(new PasteSpecialViewModel(data));
            e.Handled = true;
        }
    }

    /// <summary>The contextual Text Editor tab, in green as a contextual tab, shown while the MTEXT editor is open.</summary>
    internal sealed class CadTextEditorTab : CadWorkspaceExtension
    {
        public override string TabHeader => "Text Editor";

        public void Show(TextEditorViewModel editor)
        {
            TabContent = editor;
            IsTabVisible = true;
        }

        public void Hide()
        {
            IsTabVisible = false;
            TabContent = null;
        }
    }
}

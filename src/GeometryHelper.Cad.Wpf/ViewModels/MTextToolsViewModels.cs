using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>A character of the character map: the letter and its code point.</summary>
    public sealed class CharacterMapCell
    {
        internal CharacterMapCell(int code)
        {
            Code = code;
            Text = char.ConvertFromUtf32(code);
        }

        /// <summary>Gets the code point.</summary>
        public int Code { get; }

        /// <summary>Gets the character as text.</summary>
        public string Text { get; }

        /// <summary>Gets the code point as Unicode writes it: <c>U+0394</c>.</summary>
        public string Hex => "U+" + Code.ToString("X4", CultureInfo.InvariantCulture);

        /// <inheritdoc/>
        public override string ToString() => Text;
    }

    /// <summary>A group of the character map's character set box: a name and the code points it covers.</summary>
    public sealed class CharacterMapBlock
    {
        internal CharacterMapBlock(string name, int first, int last)
        {
            Name = name;
            First = first;
            Last = last;
        }

        /// <summary>Gets the name: Basic Latin, Greek and Coptic.</summary>
        public string Name { get; }

        /// <summary>Gets the first code point.</summary>
        public int First { get; }

        /// <summary>Gets the last code point.</summary>
        public int Last { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// The character map the Symbol menu's Other… opens, as the one AutoCAD 2021 hands over to Windows: a font, its characters by
    /// character set, Select to put the one picked in the Characters to copy box, and Copy to put that on the clipboard, to be pasted
    /// into the words. The font starts as the style's, or the letters'.
    /// </summary>
    public sealed class CharacterMapViewModel : ObservableObject
    {
        private static readonly CharacterMapBlock[] Blocks =
        {
            new CharacterMapBlock("Basic Latin", 0x20, 0x7E),
            new CharacterMapBlock("Latin-1 Supplement", 0xA0, 0xFF),
            new CharacterMapBlock("Latin Extended-A", 0x100, 0x17F),
            new CharacterMapBlock("Latin Extended-B", 0x180, 0x24F),
            new CharacterMapBlock("IPA Extensions", 0x250, 0x2AF),
            new CharacterMapBlock("Spacing Modifier Letters", 0x2B0, 0x2FF),
            new CharacterMapBlock("Greek and Coptic", 0x370, 0x3FF),
            new CharacterMapBlock("Cyrillic", 0x400, 0x4FF),
            new CharacterMapBlock("General Punctuation", 0x2000, 0x206F),
            new CharacterMapBlock("Superscripts and Subscripts", 0x2070, 0x209F),
            new CharacterMapBlock("Currency Symbols", 0x20A0, 0x20CF),
            new CharacterMapBlock("Letterlike Symbols", 0x2100, 0x214F),
            new CharacterMapBlock("Number Forms", 0x2150, 0x218F),
            new CharacterMapBlock("Arrows", 0x2190, 0x21FF),
            new CharacterMapBlock("Mathematical Operators", 0x2200, 0x22FF),
            new CharacterMapBlock("Miscellaneous Technical", 0x2300, 0x23FF),
            new CharacterMapBlock("Box Drawing", 0x2500, 0x257F),
            new CharacterMapBlock("Geometric Shapes", 0x25A0, 0x25FF),
            new CharacterMapBlock("Miscellaneous Symbols", 0x2600, 0x26FF),
            new CharacterMapBlock("Private Use Area", 0xE000, 0xE2FF),
        };

        private string _font;
        private IReadOnlyList<CharacterMapBlock> _blocks = Array.Empty<CharacterMapBlock>();
        private CharacterMapBlock _block;
        private IReadOnlyList<CharacterMapCell> _cells = Array.Empty<CharacterMapCell>();
        private CharacterMapCell _selected;
        private string _toCopy = string.Empty;
        private string _message = string.Empty;
        private HashSet<int> _glyphs = new HashSet<int>();

        /// <summary>Makes the map over a font.</summary>
        /// <param name="font">The family the map opens with; Arial for none.</param>
        public CharacterMapViewModel(string font)
        {
            string first = string.IsNullOrWhiteSpace(font) ? "Arial" : font;
            Fonts = new[] { first }
                .Concat(CadTrueTypeFonts.Families)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            SelectCommand = new DelegateCommand(_ => Select());
            CopyCommand = new DelegateCommand(_ => Copy());
            SetFont(first);
        }

        /// <summary>Gets or sets how the text reaches the clipboard: the Windows clipboard until set; a test sets its own.</summary>
        public Action<string> SetClipboardText { get; set; } = WindowsClipboardText;

        /// <summary>Gets the fonts the box offers: the one the map opened with first, then the families of the machine.</summary>
        public IReadOnlyList<string> Fonts { get; }

        /// <summary>Gets or sets the font; its characters are listed anew, in the character set kept where it has some.</summary>
        public string Font
        {
            get => _font;
            set
            {
                if (
                    !string.IsNullOrWhiteSpace(value)
                    && !string.Equals(value, _font, StringComparison.OrdinalIgnoreCase)
                )
                {
                    SetFont(value);
                }
            }
        }

        /// <summary>Gets the character sets the font has characters in.</summary>
        public IReadOnlyList<CharacterMapBlock> CharacterSets => _blocks;

        /// <summary>Gets or sets the character set shown.</summary>
        public CharacterMapBlock CharacterSet
        {
            get => _block;
            set
            {
                if (value != null && !ReferenceEquals(value, _block))
                {
                    _block = value;
                    _cells = Enumerable
                        .Range(value.First, value.Last - value.First + 1)
                        .Where(_glyphs.Contains)
                        .Select(c => new CharacterMapCell(c))
                        .ToList();
                    _selected = null;
                    Raise(nameof(CharacterSet));
                    Raise(nameof(Cells));
                    Raise(nameof(Selected));
                    Raise(nameof(Info));
                }
            }
        }

        /// <summary>Gets the characters of the font in the character set, each with a glyph.</summary>
        public IReadOnlyList<CharacterMapCell> Cells => _cells;

        /// <summary>Gets or sets the character picked.</summary>
        public CharacterMapCell Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    Raise(nameof(Info));
                }
            }
        }

        /// <summary>Gets what is said of the character picked: <c>U+0394</c>; or how many characters the set has.</summary>
        public string Info =>
            _selected != null
                ? _selected.Hex
                : _cells.Count.ToString(CultureInfo.InvariantCulture) + " characters in " + (_block?.Name ?? "no set");

        /// <summary>Gets or sets the Characters to copy box.</summary>
        public string CharactersToCopy
        {
            get => _toCopy;
            set => Set(ref _toCopy, value ?? string.Empty);
        }

        /// <summary>Gets the line under the box: what Copy did.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value);
        }

        /// <summary>Gets Select: the character picked added to the end of the Characters to copy box.</summary>
        public ICommand SelectCommand { get; }

        /// <summary>Gets Copy: the Characters to copy put on the clipboard.</summary>
        public ICommand CopyCommand { get; }

        /// <summary>Select: adds the character picked to the end of the Characters to copy box.</summary>
        public void Select()
        {
            if (_selected != null)
            {
                CharactersToCopy = _toCopy + _selected.Text;
                Message = string.Empty;
            }
        }

        /// <summary>Copy: puts the Characters to copy on the clipboard.</summary>
        public void Copy()
        {
            if (_toCopy.Length == 0)
            {
                return;
            }

            try
            {
                SetClipboardText(_toCopy);
                Message = "Copied. Paste it into the words with Ctrl+V.";
            }
            catch (System.Runtime.InteropServices.COMException exception)
            {
                GeometryHelperLog.Warn(
                    "CharacterMapViewModel: the clipboard was busy; the characters were not copied.",
                    exception
                );
                Message = "The clipboard is busy; try again.";
            }
        }

        private void SetFont(string font)
        {
            _font = font;
            _glyphs = GlyphsOf(font);
            _blocks = Blocks.Where(b => Enumerable.Range(b.First, b.Last - b.First + 1).Any(_glyphs.Contains)).ToList();
            _block = null;
            Raise(nameof(Font));
            Raise(nameof(CharacterSets));
            CharacterSet = _blocks.FirstOrDefault();

            if (_block == null)
            {
                _cells = Array.Empty<CharacterMapCell>();
                Raise(nameof(Cells));
                Raise(nameof(Info));
            }
        }

        /// <summary>The code points a family has a glyph for; none where the machine has no such family.</summary>
        private static HashSet<int> GlyphsOf(string family)
        {
            var typeface = new System.Windows.Media.Typeface(family);

            if (!typeface.TryGetGlyphTypeface(out System.Windows.Media.GlyphTypeface glyphs))
            {
                return new HashSet<int>();
            }

            return new HashSet<int>(glyphs.CharacterToGlyphMap.Keys);
        }

        private static void WindowsClipboardText(string text) => CadWindowsClipboard.TrySetText(text);
    }

    /// <summary>
    /// AutoCAD 2021's Dictionaries dialog of the Text Editor tab's Edit Dictionaries, as far as the presenter has dictionaries: the
    /// main dictionary named (the built-in English one), the custom dictionary file and its words, to which a word typed is added and
    /// from which the word picked is deleted, each written to the file at once.
    /// </summary>
    public sealed class SpellDictionariesViewModel : ObservableObject
    {
        private readonly CadSpellDictionary _dictionary;
        private string _newWord = string.Empty;
        private string _selected;
        private string _message = string.Empty;

        /// <summary>Makes the dialog over a dictionary.</summary>
        /// <param name="dictionary">The dictionary.</param>
        /// <exception cref="ArgumentNullException">Thrown when the dictionary is null.</exception>
        public SpellDictionariesViewModel(CadSpellDictionary dictionary)
        {
            _dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
            AddCommand = new DelegateCommand(_ => Add());
            DeleteCommand = new DelegateCommand(_ => Delete());
        }

        /// <summary>Gets the main dictionary as the dialog names it: English with the number of its words.</summary>
        public string MainDictionary =>
            "English (built in), "
            + CadSpellDictionary.BuiltInCount.ToString("N0", CultureInfo.InvariantCulture)
            + " words";

        /// <summary>Gets the custom dictionary file; where there is none, that the words are kept for the session only.</summary>
        public string CustomFile => _dictionary.CustomFile ?? "(none: the words are kept for the session)";

        /// <summary>Gets the custom words, in order.</summary>
        public IReadOnlyList<string> Words => _dictionary.CustomWords;

        /// <summary>Gets or sets the word typed in the Add box.</summary>
        public string NewWord
        {
            get => _newWord;
            set => Set(ref _newWord, value ?? string.Empty);
        }

        /// <summary>Gets or sets the word picked in the list.</summary>
        public string Selected
        {
            get => _selected;
            set => Set(ref _selected, value);
        }

        /// <summary>Gets the line under the list: a word refused, or the file that could not be written.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value);
        }

        /// <summary>Gets Add: the word typed added to the custom dictionary.</summary>
        public ICommand AddCommand { get; }

        /// <summary>Gets Delete: the word picked taken out of the custom dictionary.</summary>
        public ICommand DeleteCommand { get; }

        /// <summary>Add: puts the word typed in the custom dictionary and its file; a word of it already, or one that starts a comment, is said so.</summary>
        public void Add()
        {
            string word = _newWord.Trim();

            if (word.Length == 0)
            {
                return;
            }

            if (word[0] == '#' || word.Any(char.IsWhiteSpace))
            {
                Message = "A word has no spaces and does not start with #.";
                return;
            }

            if (Words.Contains(word, StringComparer.OrdinalIgnoreCase))
            {
                Message = "\"" + word + "\" is in the dictionary already.";
                return;
            }

            Message = _dictionary.Add(word) ?? string.Empty;
            NewWord = string.Empty;
            Raise(nameof(Words));
        }

        /// <summary>Delete: takes the word picked out of the custom dictionary and its file.</summary>
        public void Delete()
        {
            if (_selected == null)
            {
                return;
            }

            Message = _dictionary.Remove(_selected) ?? string.Empty;
            Selected = null;
            Raise(nameof(Words));
        }
    }
}

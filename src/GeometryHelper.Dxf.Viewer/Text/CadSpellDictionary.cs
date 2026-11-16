using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>A word of the MTEXT editor that is not in the dictionary: where it stands, how long it is, and the letters.</summary>
    public sealed class CadMisspelledWord
    {
        internal CadMisspelledWord(int start, int length, string word)
        {
            Start = start;
            Length = length;
            Word = word;
        }

        /// <summary>Gets the first place of the word, counted as <see cref="CadMTextEditor.Caret"/> counts.</summary>
        public int Start { get; }

        /// <summary>Gets how many places the word takes.</summary>
        public int Length { get; }

        /// <summary>Gets the letters of the word.</summary>
        public string Word { get; }
    }

    /// <summary>
    /// The dictionary the MTEXT editor's spell check looks words up in, as AutoCAD 2021's Spell Check panel does: the built-in
    /// English dictionary (<see cref="CadSpelling"/>) and a custom dictionary, a text file of words one to a line (AutoCAD's
    /// <c>sample.cus</c> form), with the words ignored for the session. Add Word appends to the file; Edit Dictionaries adds
    /// and deletes words in it.
    /// </summary>
    /// <remarks>
    /// A word of one letter, with a digit in it or in capitals is passed over until <see cref="Settings"/> says otherwise, as
    /// in Check Spelling. The file need not exist: it is made when the first word is added.
    /// </remarks>
    public sealed class CadSpellDictionary
    {
        private readonly HashSet<string> _custom = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Makes the dictionary over a custom dictionary file, read where it exists.</summary>
        /// <param name="customFile">The file of custom words; null for none, the words then kept for the session only.</param>
        public CadSpellDictionary(string customFile = null)
        {
            CustomFile = string.IsNullOrWhiteSpace(customFile) ? null : customFile;

            if (CustomFile != null && File.Exists(CustomFile))
            {
                try
                {
                    _custom.UnionWith(CadSpelling.ReadDictionary(CustomFile));
                }
                catch (Exception e) when (IsFileError(e))
                {
                    GeometryHelperLog.Warn(
                        "CadSpellDictionary: the custom dictionary " + CustomFile + " was not read.",
                        e
                    );
                }
            }
        }

        /// <summary>Gets the custom dictionary file; null where the words are kept for the session only.</summary>
        public string CustomFile { get; }

        /// <summary>Gets or sets which words are passed over, as Check Spelling Settings gives them; the defaults until set.</summary>
        public CadSpellSettings Settings { get; set; } = new CadSpellSettings();

        /// <summary>Gets how many words the built-in English dictionary holds.</summary>
        public static int BuiltInCount => CadSpelling.MainCount;

        /// <summary>Gets the custom words in ordinal order, letters of either case the same.</summary>
        public IReadOnlyList<string> CustomWords =>
            _custom.OrderBy(w => w, StringComparer.OrdinalIgnoreCase).ThenBy(w => w, StringComparer.Ordinal).ToList();

        /// <summary>Gets whether a word is wrong: not passed over by the settings, not ignored, not in the built-in dictionary (with its plain endings) or the custom one.</summary>
        /// <param name="word">The word.</param>
        /// <returns>Whether it is misspelled; false for an empty word.</returns>
        public bool IsMisspelled(string word) =>
            !string.IsNullOrEmpty(word)
            && !Settings.IsPassedOver(word)
            && !_ignored.Contains(word)
            && !CadSpelling.IsKnown(word, _custom);

        /// <summary>Gets the words nearest a misspelled one, at most five, the best first, in the case the word begins with.</summary>
        /// <param name="word">The word.</param>
        /// <returns>The words; none for an empty word or where none is near.</returns>
        public IReadOnlyList<string> Suggest(string word) =>
            string.IsNullOrEmpty(word) ? Array.Empty<string>() : CadSpelling.Suggest(word, _custom);

        /// <summary>
        /// Finds the words of the editor that are misspelled, in order: runs of letters, digits and inner apostrophes, a
        /// symbol or a stack ending a word. The words of the editor as it is now, not as it was when asked before.
        /// </summary>
        /// <param name="editor">The editor.</param>
        /// <returns>The words, each with its place.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the editor is null.</exception>
        public IReadOnlyList<CadMisspelledWord> Check(CadMTextEditor editor)
        {
            if (editor == null)
            {
                throw new ArgumentNullException(nameof(editor));
            }

            var found = new List<CadMisspelledWord>();
            var word = new StringBuilder();
            int start = 0;

            void End(int end)
            {
                if (word.Length == 0)
                {
                    return;
                }

                // Apostrophes at either end belong to the quotes around the word.
                int first = 0;
                int last = word.Length;

                while (first < last && word[first] == '\'')
                {
                    first++;
                }

                while (last > first && word[last - 1] == '\'')
                {
                    last--;
                }

                string text = word.ToString(first, last - first);

                if (text.Length > 0 && IsMisspelled(text))
                {
                    found.Add(new CadMisspelledWord(start + first, last - first, text));
                }

                word.Clear();
            }

            for (int i = 0; i < editor.Length; i++)
            {
                string shown = editor.TextAt(i);
                char c = shown.Length == 1 ? shown[0] : '\0';

                if (!editor.HasCodeAt(i) && c != '\0' && (char.IsLetterOrDigit(c) || (c == '\'' && word.Length > 0)))
                {
                    if (word.Length == 0)
                    {
                        start = i;
                    }

                    word.Append(c);
                }
                else
                {
                    End(i);
                }
            }

            End(editor.Length);
            return found;
        }

        /// <summary>Leaves a word as it is wherever it comes, for as long as the dictionary lives: Ignore All.</summary>
        /// <param name="word">The word.</param>
        public void IgnoreAll(string word)
        {
            if (!string.IsNullOrEmpty(word))
            {
                _ignored.Add(word);
            }
        }

        /// <summary>Adds a word to the custom dictionary, so that it is spelt right from now on, and to the end of its file: Add Word.</summary>
        /// <param name="word">The word.</param>
        /// <returns>Null where the word was added, or was in the dictionary already; else the line saying why the file was not written.</returns>
        public string Add(string word)
        {
            string text = (word ?? string.Empty).Trim();

            if (text.Length == 0 || text[0] == '#' || text.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                return null;
            }

            if (!_custom.Add(text))
            {
                return null;
            }

            return Update(() =>
            {
                string old = File.Exists(CustomFile) ? File.ReadAllText(CustomFile, Encoding.UTF8) : string.Empty;
                File.AppendAllText(
                    CustomFile,
                    (old.Length > 0 && !old.EndsWith("\n", StringComparison.Ordinal) ? "\n" : string.Empty)
                        + text
                        + "\n",
                    new UTF8Encoding(false)
                );
            });
        }

        /// <summary>Takes a word out of the custom dictionary and its file, the other lines of the file left as they are: Edit Dictionaries' Delete.</summary>
        /// <param name="word">The word.</param>
        /// <returns>Null where the word was taken out, or was not there; else the line saying why the file was not written.</returns>
        public string Remove(string word)
        {
            string text = (word ?? string.Empty).Trim();

            if (text.Length == 0 || !_custom.Remove(text))
            {
                return null;
            }

            return Update(() =>
            {
                if (!File.Exists(CustomFile))
                {
                    return;
                }

                IEnumerable<string> kept = File.ReadAllLines(CustomFile, Encoding.UTF8)
                    .Where(line => !string.Equals(line.Trim(), text, StringComparison.OrdinalIgnoreCase));
                File.WriteAllText(CustomFile, string.Concat(kept.Select(l => l + "\n")), new UTF8Encoding(false));
            });
        }

        /// <summary>Does a change of the file where there is one, its folder made first.</summary>
        private string Update(Action change)
        {
            if (CustomFile == null)
            {
                return null;
            }

            try
            {
                string folder = Path.GetDirectoryName(CustomFile);

                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                change();
                return null;
            }
            catch (Exception e) when (IsFileError(e))
            {
                GeometryHelperLog.Warn(
                    "CadSpellDictionary: the custom dictionary " + CustomFile + " was not written.",
                    e
                );
                return "Cannot update custom dictionary.";
            }
        }

        private static bool IsFileError(Exception e) =>
            e is IOException
            || e is UnauthorizedAccessException
            || e is ArgumentException
            || e is NotSupportedException;
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>Where FIND looks, as Find where of AutoCAD's dialog says.</summary>
    public enum CadFindScope
    {
        /// <summary>Every space of the drawing: model space and the layouts.</summary>
        EntireDrawing,

        /// <summary>The space shown.</summary>
        CurrentSpace,

        /// <summary>The objects selected.</summary>
        SelectedObjects,
    }

    /// <summary>The options of FIND, as the Search Options and Text Types of AutoCAD's dialog; the defaults are AutoCAD's.</summary>
    public sealed class CadFindOptions
    {
        /// <summary>Gets or sets whether letters match only in the same case.</summary>
        public bool MatchCase { get; set; }

        /// <summary>Gets or sets whether the text found is a whole word, not part of one: M20 finds "M20 x 60" but not "M200".</summary>
        public bool WholeWords { get; set; }

        /// <summary>Gets or sets whether the text is a wild-card pattern: * any run, ? one character, # a digit, @ a letter.</summary>
        public bool UseWildcards { get; set; }

        /// <summary>Gets or sets whether the texts inside the blocks an INSERT places are searched too; true by default.</summary>
        public bool SearchBlocks { get; set; } = true;

        /// <summary>Gets or sets whether texts on layers off or frozen, and attributes hidden, are passed over; true by default.</summary>
        public bool IgnoreHidden { get; set; } = true;

        /// <summary>Gets or sets whether the values of block attributes are searched; true by default.</summary>
        public bool BlockAttributes { get; set; } = true;

        /// <summary>Gets or sets whether the text of dimensions and multileaders is searched; true by default.</summary>
        public bool DimensionText { get; set; } = true;

        /// <summary>Gets or sets whether TEXT and MTEXT are searched; true by default.</summary>
        public bool Text { get; set; } = true;

        /// <summary>Gets or sets whether the cells of tables are searched; true by default.</summary>
        public bool TableText { get; set; } = true;
    }

    /// <summary>One text FIND found: the object it is in, where that is, and the text as the result list shows it.</summary>
    public sealed class CadFindResult
    {
        internal CadFindResult(string handle, string location, string objectType, string text, string target, int row, int column, int count)
        {
            Handle = handle;
            Location = location;
            ObjectType = objectType;
            Text = text;
            Target = target;
            Row = row;
            Column = column;
            Count = count;
        }

        /// <summary>Gets the handle of the object of a space it is in, which Zoom shows and Select selects: the INSERT of an attribute or of a block's text.</summary>
        public string Handle { get; }

        /// <summary>Gets where the object is: Model, the name of a layout, or the block a text of a block is in.</summary>
        public string Location { get; }

        /// <summary>Gets the type of object, as AutoCAD's list writes it: Text, MText, Attribute, Dimension, Multileader, Table cell.</summary>
        public string ObjectType { get; }

        /// <summary>Gets the text, its formatting taken out.</summary>
        public string Text { get; }

        /// <summary>Gets how many times the text searched for is in it.</summary>
        public int Count { get; }

        /// <summary>The handle of the object whose text it is: the attribute, the text of the block, the entity itself.</summary>
        internal string Target { get; }

        /// <summary>The row of a table cell; -1 for any other.</summary>
        internal int Row { get; }

        /// <summary>The column of a table cell; -1 for any other.</summary>
        internal int Column { get; }

        /// <inheritdoc/>
        public override string ToString() => Location + " " + ObjectType + " " + Text;
    }

    /// <summary>
    /// FIND, as AutoCAD's Find and Replace: the TEXT, MTEXT, attribute values, dimension text, multileader text and table
    /// cells of a drawing that hold a text, and the text replaced in one of them or in all, each replacement one undo step.
    /// MTEXT is searched by its words, its formatting taken out, and replaced in its words alone, every code kept in its
    /// place: <c>{\fArial|b1;Bolts M20}</c> searched for M20 and replaced by M24 is <c>{\fArial|b1;Bolts M24}</c>.
    /// </summary>
    public static class CadFind
    {
        /// <summary>Finds a text in the drawing, in the order of the spaces' tabs and then of each space.</summary>
        /// <param name="document">The document.</param>
        /// <param name="what">The text searched for.</param>
        /// <param name="options">The options; null for the defaults.</param>
        /// <param name="scope">Where to look.</param>
        /// <param name="selected">The objects of <see cref="CadFindScope.SelectedObjects"/>; null for none.</param>
        /// <returns>The texts found, each object once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static IReadOnlyList<CadFindResult> Find(CadDocument document, string what, CadFindOptions options, CadFindScope scope, IEnumerable<string> selected = null)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var results = new List<CadFindResult>();

            if (string.IsNullOrEmpty(what))
            {
                return results;
            }

            options = options ?? new CadFindOptions();
            Regex pattern = Pattern(what, options);
            var hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (options.IgnoreHidden)
            {
                foreach (DxfLayerTableRecord layer in document.Database.LayerTable)
                {
                    if (layer.IsOff || layer.IsFrozen)
                    {
                        hidden.Add(layer.Name);
                    }
                }
            }

            HashSet<string> wanted = null;

            if (scope == CadFindScope.SelectedObjects)
            {
                wanted = new HashSet<string>(selected ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            }

            var search = new Search(document, pattern, options, hidden, results);

            foreach (CadSpace space in document.Spaces)
            {
                if (scope == CadFindScope.CurrentSpace && space != document.CurrentSpace)
                {
                    continue;
                }

                string location = space.IsModel ? "Model" : space.Name;

                foreach (DxfEntity entity in space.Block)
                {
                    if (entity.Handle != null && (wanted == null || wanted.Contains(entity.Handle)))
                    {
                        search.Entity(entity, entity.Handle, location, 0);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Replaces the text searched for in one result, every time it is in it, as Replace does: one undo step "FIND". An
        /// object whose owner keeps it (<see cref="CadEditRule"/>) is refused as any edit of it is.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="result">The result.</param>
        /// <param name="what">The text searched for, as found.</param>
        /// <param name="with">The text put in its place.</param>
        /// <param name="options">The options it was found with; null for the defaults.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the result is null.</exception>
        public static CadEditResult Replace(CadDocument document, CadFindResult result, string what, string with, CadFindOptions options)
            => ReplaceAll(document, new[] { result ?? throw new ArgumentNullException(nameof(result)) }, what, with, options);

        /// <summary>Replaces the text searched for in every result, as Replace All does: one undo step "FIND".</summary>
        /// <param name="document">The document.</param>
        /// <param name="results">The results.</param>
        /// <param name="what">The text searched for, as found.</param>
        /// <param name="with">The text put in its place.</param>
        /// <param name="options">The options it was found with; null for the defaults.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the results are null.</exception>
        public static CadEditResult ReplaceAll(CadDocument document, IReadOnlyList<CadFindResult> results, string what, string with, CadFindOptions options)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            options = options ?? new CadFindOptions();
            Regex pattern = Pattern(what ?? string.Empty, options);
            string replacement = with ?? string.Empty;
            var records = new List<CadEditRecord>();
            var unique = new List<CadFindResult>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // A text of a block found through each of its INSERTs is replaced once.
            foreach (CadFindResult result in results)
            {
                if (result != null && seen.Add(result.Target + "|" + result.Row.ToString(CultureInfo.InvariantCulture) + "|" + result.Column.ToString(CultureInfo.InvariantCulture)))
                {
                    unique.Add(result);
                }
            }

            results = unique;

            foreach (CadFindResult result in results)
            {
                bool own = document.FindEntity(result.Target) != null;

                // A text of a block is the block's, not an entity of a space: it goes as a change of the tables, as BEDIT's would.
                records.Add(own
                    ? new CadEditRecord(CadEditRecordKind.PropertyChanged, result.Target, "Contents", result.Text, null)
                    : new CadEditRecord(CadEditRecordKind.TableChanged, null, "Block text", result.Text, result.Target));
            }

            return document.ApplyEdit("FIND", records, s =>
            {
                foreach (CadFindResult result in results)
                {
                    if (s.IsHandled(result.Target) || !s.Database.TryGetObject(result.Target, out DxfDBObject value))
                    {
                        continue;
                    }

                    ReplaceIn(value, result, pattern, replacement);
                }
            });
        }

        /// <summary>The regular expression of the text searched for.</summary>
        internal static Regex Pattern(string what, CadFindOptions options)
        {
            string body = options.UseWildcards ? CadWildcard.ToRegex(what, !options.MatchCase, false).ToString() : Regex.Escape(what);

            if (options.WholeWords)
            {
                body = "(?<![\\p{L}\\p{Nd}_])(?:" + body + ")(?![\\p{L}\\p{Nd}_])";
            }

            RegexOptions flags = RegexOptions.CultureInvariant | RegexOptions.Singleline | (options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
            return new Regex(body, flags);
        }

        private static void ReplaceIn(DxfDBObject value, CadFindResult result, Regex pattern, string with)
        {
            switch (value)
            {
                case DxfTable table when result.Row >= 0:
                    string cell = table.TextString(result.Row, result.Column);
                    string replaced = pattern.Replace(cell, with.Replace("$", "$$"));

                    if (replaced != cell)
                    {
                        // The table drawn anew with the new words, as AutoCAD draws it after a replace.
                        table.SetTextString(result.Row, result.Column, replaced);
                        table.RecomputeTableBlock();
                    }

                    break;
                case DxfMLeader leader:
                    DxfMText words = leader.MText;

                    if (words != null)
                    {
                        string contents = ReplaceFormatted(words.Contents, pattern, with);

                        if (contents != words.Contents)
                        {
                            words.Contents = contents;
                            leader.MText = words;
                        }
                    }

                    break;
                case DxfMText mtext:
                    string now = ReplaceFormatted(mtext.Contents, pattern, with);

                    if (now != mtext.Contents)
                    {
                        mtext.Contents = now;
                    }

                    break;
                case DxfDimension dimension:
                    string text = dimension.DimensionText;
                    string changed = ReplacePlain(text, pattern, with);

                    if (changed != text)
                    {
                        dimension.DimensionText = changed;
                    }

                    break;
                case DxfDBText dbtext:
                    string plain = dbtext.TextString;
                    string after = ReplacePlain(plain, pattern, with);

                    if (after != plain)
                    {
                        dbtext.TextString = after;
                    }

                    break;
            }
        }

        private static string ReplacePlain(string text, Regex pattern, string with) => pattern.Replace(text ?? string.Empty, with.Replace("$", "$$"));

        /// <summary>
        /// Replaces each match in the words of an MTEXT's contents, its codes kept: the words of a match taken out one by one,
        /// the text given put where the first of them was, the codes between them left in their places.
        /// </summary>
        internal static string ReplaceFormatted(string contents, Regex pattern, string with)
        {
            string plain = Plain(contents ?? string.Empty, out List<int> starts, out List<int> ends);
            MatchCollection matches = pattern.Matches(plain);

            if (matches.Count == 0)
            {
                return contents;
            }

            string escaped = Escape(with);
            var builder = new StringBuilder(contents);

            // From the last match back, so that the places before are not moved.
            for (int m = matches.Count - 1; m >= 0; m--)
            {
                Match match = matches[m];

                if (match.Length == 0)
                {
                    continue;
                }

                for (int i = match.Index + match.Length - 1; i > match.Index; i--)
                {
                    builder.Remove(starts[i], ends[i] - starts[i]);
                }

                builder.Remove(starts[match.Index], ends[match.Index] - starts[match.Index]);
                builder.Insert(starts[match.Index], escaped);
            }

            return builder.ToString();
        }

        /// <summary>
        /// The words of MTEXT contents, and for each letter of them where it starts and ends in the contents: a code (\f...;,
        /// \H...;, \L, a brace) gives no letter, \P a line feed, \~ a space, \\ \{ \} the letter after the back slash, and a
        /// stack \S...; no letter.
        /// </summary>
        internal static string Plain(string contents, out List<int> starts, out List<int> ends)
        {
            var plain = new StringBuilder(contents.Length);
            starts = new List<int>(contents.Length);
            ends = new List<int>(contents.Length);

            for (int i = 0; i < contents.Length; i++)
            {
                char ch = contents[i];

                if (ch == '{' || ch == '}')
                {
                    continue;
                }

                if (ch != '\\' || i + 1 >= contents.Length)
                {
                    plain.Append(ch);
                    starts.Add(i);
                    ends.Add(i + 1);
                    continue;
                }

                char code = contents[i + 1];

                switch (code)
                {
                    case 'P':
                        plain.Append('\n');
                        starts.Add(i);
                        ends.Add(i + 2);
                        i++;
                        break;
                    case '~':
                        plain.Append(' ');
                        starts.Add(i);
                        ends.Add(i + 2);
                        i++;
                        break;
                    case '\\':
                    case '{':
                    case '}':
                        plain.Append(code);
                        starts.Add(i);
                        ends.Add(i + 2);
                        i++;
                        break;
                    case 'L':
                    case 'l':
                    case 'O':
                    case 'o':
                    case 'K':
                    case 'k':
                    case 'N':
                        i++;
                        break;
                    case 'U':
                        if (i + 6 < contents.Length && contents[i + 2] == '+' && int.TryParse(contents.Substring(i + 3, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int letter))
                        {
                            plain.Append((char)letter);
                            starts.Add(i);
                            ends.Add(i + 7);
                            i += 6;
                        }
                        else
                        {
                            plain.Append(ch);
                            starts.Add(i);
                            ends.Add(i + 1);
                        }

                        break;
                    default:
                        int end = contents.IndexOf(';', i + 2);

                        if (char.IsLetter(code) && end > 0)
                        {
                            i = end;
                        }
                        else
                        {
                            plain.Append(ch);
                            starts.Add(i);
                            ends.Add(i + 1);
                        }

                        break;
                }
            }

            return plain.ToString();
        }

        /// <summary>A text written into MTEXT contents: its back slashes and braces escaped, a line feed a new paragraph.</summary>
        private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("{", "\\{").Replace("}", "\\}").Replace("\r\n", "\\P").Replace("\n", "\\P");

        /// <summary>One run of a search: the pattern, the options, the layers hidden, and the results it adds to.</summary>
        private sealed class Search
        {
            private const int MaxDepth = 12;
            private readonly CadDocument _document;
            private readonly Regex _pattern;
            private readonly CadFindOptions _options;
            private readonly HashSet<string> _hidden;
            private readonly List<CadFindResult> _results;

            internal Search(CadDocument document, Regex pattern, CadFindOptions options, HashSet<string> hidden, List<CadFindResult> results)
            {
                _document = document;
                _pattern = pattern;
                _options = options;
                _hidden = hidden;
                _results = results;
            }

            internal void Entity(DxfEntity entity, string top, string location, int depth)
            {
                if (_hidden.Contains(entity.Layer ?? "0"))
                {
                    return;
                }

                switch (entity)
                {
                    case DxfAttributeDefinition _:
                        break;
                    case DxfAttributeReference attribute:
                        if (_options.BlockAttributes && !(_options.IgnoreHidden && attribute.Invisible))
                        {
                            Add(top, entity.Handle, location, "Attribute", attribute.TextString, -1, -1);
                        }

                        break;
                    case DxfDBText text:
                        if (_options.Text)
                        {
                            Add(top, entity.Handle, location, "Text", text.TextString, -1, -1);
                        }

                        break;
                    case DxfMText mtext:
                        if (_options.Text)
                        {
                            Add(top, entity.Handle, location, "MText", Plain(mtext.Contents ?? string.Empty, out _, out _), -1, -1);
                        }

                        break;
                    case DxfDimension dimension:
                        if (_options.DimensionText)
                        {
                            Add(top, entity.Handle, location, "Dimension", dimension.DimensionText, -1, -1);
                        }

                        break;
                    case DxfMLeader leader:
                        if (_options.DimensionText)
                        {
                            DxfMText words = leader.MText;

                            if (words != null)
                            {
                                Add(top, entity.Handle, location, "Multileader", Plain(words.Contents ?? string.Empty, out _, out _), -1, -1);
                            }
                        }

                        break;
                    case DxfTable table:
                        if (_options.TableText)
                        {
                            for (int row = 0; row < table.NumRows; row++)
                            {
                                for (int column = 0; column < table.NumColumns; column++)
                                {
                                    Add(top, entity.Handle, location, "Table cell", table.TextString(row, column), row, column);
                                }
                            }
                        }

                        break;
                    case DxfBlockReference reference:
                        if (_options.BlockAttributes)
                        {
                            foreach (DxfAttributeReference attribute in reference.AttributeCollection)
                            {
                                if (!attribute.IsErased)
                                {
                                    Entity(attribute, top, location, depth);
                                }
                            }
                        }

                        if (_options.SearchBlocks && depth < MaxDepth)
                        {
                            DxfBlockTableRecord block = reference.BlockTableRecord;

                            if (block != null && !block.IsLayout)
                            {
                                foreach (DxfEntity inner in block)
                                {
                                    Entity(inner, top, block.Name, depth + 1);
                                }
                            }
                        }

                        break;
                }
            }

            private void Add(string top, string target, string location, string type, string text, int row, int column)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                int count = _pattern.Matches(text).Count;

                if (count > 0)
                {
                    _results.Add(new CadFindResult(top, location, type, text, target, row, column, count));
                }
            }
        }
    }
}

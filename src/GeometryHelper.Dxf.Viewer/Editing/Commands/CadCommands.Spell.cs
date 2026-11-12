using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// SPELL (SP): the command line form of AutoCAD's Check Spelling. "Select objects or [All/Dictionary]:" takes the objects
    /// to check (Enter, or All, every TEXT, MTEXT and attribute of the space), Dictionary a custom dictionary file of words one
    /// to a line; each word not in the built-in English list (<see cref="CadSpelling"/>) or the custom one is written with the
    /// words nearest it, and "Enter replacement or [Ignore/ignore aLl/Add/Change/Stop] &lt;Ignore&gt;:" takes a replacement
    /// typed, Change (the first suggestion), Add (to the custom dictionary, and its file), Ignore, ignore all, or Stop. The
    /// changes of a text are written as one edit of the command's one undo step.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> Spell(CadCommandContext c)
        {
            var custom = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string customFile = null;
            IReadOnlyList<string> chosen = null;

            while (true)
            {
                yield return CadPrompt.ForSelection("Select objects", new[] { "All", "Dictionary" });

                if (c.Input.Is("Dictionary"))
                {
                    yield return CadPrompt.ForString("Enter custom dictionary file", customFile ?? "none");

                    if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0)
                    {
                        continue;
                    }

                    string path = c.Input.Text.Trim();

                    try
                    {
                        HashSet<string> words = CadSpelling.ReadDictionary(path);
                        custom.UnionWith(words);
                        customFile = path;
                    }
                    catch (Exception e)
                        when (e is IOException
                            || e is UnauthorizedAccessException
                            || e is ArgumentException
                            || e is NotSupportedException
                        )
                    {
                        GeometryHelperLog.Warn("SPELL: the custom dictionary " + path + " was not read.", e);
                        c.Write("Cannot read custom dictionary.");
                    }

                    continue;
                }

                if (c.Input.Is("All") || (c.Input.Kind == CadInputKind.Selection && c.Input.Handles.Count == 0))
                {
                    break;
                }

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                chosen = c.Input.Handles;
                break;
            }

            var wanted = chosen == null ? null : new HashSet<string>(chosen, StringComparer.OrdinalIgnoreCase);
            var items = new List<SpellItem>();
            int locked = 0;

            foreach (DxfEntity entity in c.Space.Block.ToList())
            {
                if (entity.IsErased || (wanted != null && !wanted.Contains(entity.Handle ?? string.Empty)))
                {
                    continue;
                }

                bool checkable =
                    (entity is DxfBlockReference block && block.AttributeCollection.Count > 0)
                    || entity is DxfMText
                    || (entity is DxfDBText && !(entity is DxfAttributeDefinition));

                if (!checkable)
                {
                    continue;
                }

                if (CadLayerEditor.IsLocked(c.Database, entity.Layer))
                {
                    locked++;
                    continue;
                }

                if (entity is DxfBlockReference insert)
                {
                    foreach (DxfAttributeReference attribute in insert.AttributeCollection)
                    {
                        if (!attribute.IsErased)
                        {
                            items.Add(
                                new SpellItem(
                                    insert.Handle,
                                    attribute.Handle,
                                    attribute.TextString ?? string.Empty,
                                    false
                                )
                            );
                        }
                    }
                }
                else if (entity is DxfMText mtext)
                {
                    items.Add(new SpellItem(mtext.Handle, null, mtext.Contents ?? string.Empty, true));
                }
                else
                {
                    var text = (DxfDBText)entity;
                    items.Add(new SpellItem(text.Handle, null, text.TextString ?? string.Empty, false));
                }
            }

            if (locked > 0)
            {
                c.Write(
                    locked.ToString(CultureInfo.InvariantCulture)
                        + (locked == 1 ? " was" : " were")
                        + " on a locked layer."
                );
            }

            foreach (SpellItem item in items)
            {
                var changes = new List<KeyValuePair<CadSpelling.Word, string>>();
                bool stop = false;

                foreach (CadSpelling.Word word in CadSpelling.WordsOf(item.Raw, item.IsMText))
                {
                    if (
                        CadSpelling.IsPassedOver(word.Text)
                        || ignored.Contains(word.Text)
                        || CadSpelling.IsKnown(word.Text, custom)
                    )
                    {
                        continue;
                    }

                    List<string> suggestions = CadSpelling.Suggest(word.Text, custom);

                    while (true)
                    {
                        c.Write(
                            "Not in dictionary: \""
                                + word.Text
                                + "\". "
                                + (
                                    suggestions.Count == 0
                                        ? "No suggestions."
                                        : "Suggestions: " + string.Join(", ", suggestions)
                                )
                        );
                        yield return CadPrompt.ForKeywordOrText(
                            "Enter replacement or",
                            new[] { "Ignore", "ignore aLl", "Add", "Change", "Stop" },
                            "Ignore"
                        );

                        if (c.Input.Is("Change"))
                        {
                            if (suggestions.Count == 0)
                            {
                                c.Write("No suggestions.");
                                continue;
                            }

                            changes.Add(new KeyValuePair<CadSpelling.Word, string>(word, suggestions[0]));
                        }
                        else if (c.Input.Is("ignore aLl"))
                        {
                            ignored.Add(word.Text);
                        }
                        else if (c.Input.Is("Add"))
                        {
                            custom.Add(word.Text);

                            if (customFile != null)
                            {
                                AppendWord(c, customFile, word.Text);
                            }
                        }
                        else if (c.Input.Is("Stop"))
                        {
                            stop = true;
                        }
                        else if (c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0)
                        {
                            changes.Add(new KeyValuePair<CadSpelling.Word, string>(word, c.Input.Text.Trim()));
                        }

                        break;
                    }

                    if (stop)
                    {
                        break;
                    }
                }

                WriteSpelling(c, item, changes);

                if (stop)
                {
                    yield break;
                }
            }

            c.Write("Spelling check complete.");
        }

        /// <summary>A text to check: the object (an INSERT for one of its attributes), the attribute, the words as written, and whether they are an MTEXT's.</summary>
        private sealed class SpellItem
        {
            internal SpellItem(string handle, string attribute, string raw, bool isMText)
            {
                Handle = handle;
                Attribute = attribute;
                Raw = raw;
                IsMText = isMText;
            }

            internal string Handle { get; }

            internal string Attribute { get; }

            internal string Raw { get; }

            internal bool IsMText { get; }
        }

        /// <summary>Writes the words changed into a text, those the replacements put in place of the words they were, one edit of the command's step.</summary>
        private static void WriteSpelling(
            CadCommandContext c,
            SpellItem item,
            List<KeyValuePair<CadSpelling.Word, string>> changes
        )
        {
            if (changes.Count == 0)
            {
                return;
            }

            var made = new StringBuilder(item.Raw);

            // From the end, so that each start is still where the words were found.
            foreach (KeyValuePair<CadSpelling.Word, string> change in changes.OrderByDescending(p => p.Key.Start))
            {
                made.Remove(change.Key.Start, change.Key.Text.Length).Insert(change.Key.Start, change.Value);
            }

            string value = made.ToString();
            c.OpenUndoStep();

            if (item.Attribute == null)
            {
                CadEditResult result = CadPropertyEditor.Set(c.Document, new[] { item.Handle }, "Contents", value);

                if (!result.Succeeded)
                {
                    c.Write(result.Message);
                }

                return;
            }

            CadAttributeRow row = Rows(c.Document, item.Handle)
                .Rows.First(r => string.Equals(r.Handle, item.Attribute, StringComparison.OrdinalIgnoreCase));
            CadAttributeRow old = row.Clone();
            row.Value = value;
            WriteAttributes(
                c,
                new CadAttributeEditRequest(c.Document, item.Handle, null, new[] { row }),
                new List<CadAttributeRow> { old }
            );
        }

        /// <summary>Adds a word to the end of a custom dictionary file, on a line of its own.</summary>
        private static void AppendWord(CadCommandContext c, string path, string word)
        {
            try
            {
                string text = File.ReadAllText(path, Encoding.UTF8);
                File.AppendAllText(
                    path,
                    (text.Length > 0 && !text.EndsWith("\n", StringComparison.Ordinal) ? "\n" : string.Empty)
                        + word
                        + "\n",
                    new UTF8Encoding(false)
                );
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                GeometryHelperLog.Warn("SPELL: the word " + word + " was not added to " + path + ".", e);
                c.Write("Cannot update custom dictionary.");
            }
        }
    }
}

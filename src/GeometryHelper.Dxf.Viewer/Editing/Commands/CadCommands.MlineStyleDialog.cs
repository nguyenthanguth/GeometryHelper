using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// MLSTYLE with its Multiline Style dialog, and MLEDIT with its Multiline Edit Tools dialog; with no window, both ask on the
    /// command line as before (CadCommands.MlineStyle.cs, CadCommands.MlineEdit.cs).
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// MLSTYLE: the window's Multiline Style dialog over a <see cref="CadMlineStyleRequest"/>, whose buttons act at once, each
        /// an edit of the command's one undo step; the style Set Current chose is made current when it closes with OK. With
        /// no window, the questions of the typed MLSTYLE.
        /// </summary>
        private static IEnumerable<CadPrompt> MlineStyleDialog(CadCommandContext c)
        {
            EnsureStandardMlineStyle(c);
            var request = new CadMlineStyleRequest(c);

            if (!c.Session.Request("MLSTYLE", c.Document, request))
            {
                foreach (CadPrompt prompt in MlineStyleTyped(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            DxfMlineStyle chosen = request.IsAccepted
                ? FindMlineStyle(c.Database, request.Current ?? string.Empty)
                : null;

            if (chosen != null)
            {
                SetMlineStyle(c, chosen);
            }
        }

        internal static string MlineStyleCurrentName(DxfDatabase database) => CurrentMlineStyleName(database);

        internal static IReadOnlyList<string> MlineStyleNames(DxfDatabase database) =>
            MlineStyles(database).Select(s => s.Name).ToList();

        internal static DxfMlineStyle MlineStyleFound(DxfDatabase database, string name) =>
            string.IsNullOrWhiteSpace(name) ? null : FindMlineStyle(database, name.Trim());

        internal static bool MlineStyleLocked(DxfDatabase database, string name)
        {
            DxfMlineStyle style = MlineStyleFound(database, name);
            return style != null
                && (
                    string.Equals(style.Name, "STANDARD", StringComparison.OrdinalIgnoreCase)
                    || MlineStyleInUse(database, style)
                );
        }

        internal static string MlineStyleDeleteRefusal(DxfDatabase database, string name)
        {
            DxfMlineStyle style = MlineStyleFound(database, name);

            if (style == null)
            {
                return "Cannot find multiline style \"" + name + "\".";
            }

            return
                string.Equals(style.Name, "STANDARD", StringComparison.OrdinalIgnoreCase)
                || string.Equals(style.Name, CurrentMlineStyleName(database), StringComparison.OrdinalIgnoreCase)
                || MlineStyleInUse(database, style)
                ? "The multiline style "
                    + style.Name
                    + " cannot be deleted: STANDARD, the current style and one a multiline is drawn by are kept."
                : null;
        }

        internal static string MlineStyleNameIsRefused(DxfDatabase database, string name) =>
            string.IsNullOrWhiteSpace(name) ? "Enter a name." : MlineStyleNameRefusal(database, name.Trim());

        internal static string MlineStyleAdd(CadCommandContext c, string name, DxfMlineStyleSettings settings)
        {
            string refusal = MlineStyleNameIsRefused(c.Database, name);

            if (refusal != null)
            {
                return refusal;
            }

            string trimmed = name.Trim();
            return Refusal(
                c.Apply(
                    new[]
                    {
                        new CadEditRecord(CadEditRecordKind.TableChanged, null, "Multiline styles", null, trimmed),
                    },
                    s => s.Database.AddMlineStyle(trimmed, settings)
                )
            );
        }

        internal static string MlineStyleModify(CadCommandContext c, string name, DxfMlineStyleSettings settings)
        {
            DxfMlineStyle style = MlineStyleFound(c.Database, name);

            if (style == null)
            {
                return "Cannot find multiline style \"" + name + "\".";
            }

            DxfMlineStyleSettings taken = settings;

            if (MlineStyleLocked(c.Database, name))
            {
                taken = style.GetSettings();
                taken.Description = settings.Description ?? string.Empty;
            }

            return Refusal(
                c.Apply(
                    new[]
                    {
                        new CadEditRecord(CadEditRecordKind.TableChanged, null, "Multiline styles", null, style.Name),
                    },
                    s => style.Apply(taken)
                )
            );
        }

        internal static string MlineStyleRename(CadCommandContext c, string oldName, string newName)
        {
            DxfMlineStyle style = MlineStyleFound(c.Database, oldName);

            if (style == null)
            {
                return "Cannot find multiline style \"" + oldName + "\".";
            }

            if (string.Equals(style.Name, "STANDARD", StringComparison.OrdinalIgnoreCase))
            {
                return "The STANDARD multiline style cannot be renamed.";
            }

            string name = (newName ?? string.Empty).Trim();
            string refusal = string.Equals(name, style.Name, StringComparison.OrdinalIgnoreCase)
                ? null
                : MlineStyleNameIsRefused(c.Database, name);

            if (refusal != null)
            {
                return refusal;
            }

            string old = c.Database.MLStyleDictionary.NameAt(style);
            return Refusal(
                c.Apply(
                    new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Multiline styles", old, name) },
                    s => s.Database.RenameMlineStyle(old, name)
                )
            );
        }

        internal static string MlineStyleDelete(CadCommandContext c, string name)
        {
            string refusal = MlineStyleDeleteRefusal(c.Database, name);

            if (refusal != null)
            {
                return refusal;
            }

            string key = c.Database.MLStyleDictionary.NameAt(MlineStyleFound(c.Database, name));
            return Refusal(
                c.Apply(
                    new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Multiline styles", key, null) },
                    s => s.Database.MLStyleDictionary.Remove(key)
                )
            );
        }

        internal static IReadOnlyList<string> MlineStyleNamesInFile(
            CadCommandContext c,
            string path,
            out string refusal
        )
        {
            string found = MlineStyleFilePath(c, path);

            if (found == null)
            {
                refusal = "Cannot find multiline style file \"" + path + "\".";
                return Array.Empty<string>();
            }

            try
            {
                refusal = null;
                return MlineStyleNamesIn(found);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is FormatException)
            {
                refusal = "Cannot read multiline style file \"" + found + "\": " + e.Message;
                return Array.Empty<string>();
            }
        }

        internal static string MlineStyleLoad(CadCommandContext c, string path, string name)
        {
            string found = MlineStyleFilePath(c, path);

            if (found == null)
            {
                return "Cannot find multiline style file \"" + path + "\".";
            }

            if (MlineStyleFound(c.Database, name) != null)
            {
                return "Multiline style " + name.Trim().ToUpperInvariant() + " already exists.";
            }

            return Refusal(
                c.Apply(
                    new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Multiline styles", null, name) },
                    s => s.Database.LoadMlineStyleFile(name, found)
                )
            );
        }

        internal static string MlineStyleSave(CadCommandContext c, string name, string path)
        {
            DxfMlineStyle style = MlineStyleFound(c.Database, name);

            if (style == null)
            {
                return "Cannot find multiline style \"" + name + "\".";
            }

            string file = (path ?? string.Empty).Trim().Trim('"');

            if (file.Length == 0)
            {
                return "Enter a file name.";
            }

            file = Path.HasExtension(file) ? file : file + ".mln";

            try
            {
                c.Database.SaveMlineStyleFile(style.Name, file);
                c.Write("Multiline style " + style.Name + " saved to " + Path.GetFileName(file) + ".");
                return null;
            }
            catch (Exception e)
                when (e is IOException
                    || e is UnauthorizedAccessException
                    || e is FormatException
                    || e is ArgumentException
                    || e is NotSupportedException
                )
            {
                return "Cannot save to multiline style file \"" + file + "\": " + e.Message.Split('\r', '\n')[0];
            }
        }

        /// <summary>A file of multiline styles named or chosen: as given where it is there, else found in the support folders.</summary>
        private static string MlineStyleFilePath(CadCommandContext c, string path)
        {
            string file = (path ?? string.Empty).Trim().Trim('"');

            if (file.Length == 0)
            {
                return null;
            }

            file = Path.HasExtension(file) ? file : file + ".mln";
            return File.Exists(file) ? file : CadSupportFiles.Find(c.Document, file);
        }

        /// <summary>The first line of why an edit was refused; null where it was made.</summary>
        private static string Refusal(CadEditResult result) =>
            result.Succeeded ? null : (result.Message ?? "Refused.").Split('\r', '\n')[0];

        /// <summary>
        /// MLEDIT: the window's Multiline Edit Tools dialog, the tool picked then asked as the typed option of its letters;
        /// closed with none, nothing is asked. With no window, "Enter mline editing option".
        /// </summary>
        private static IEnumerable<CadPrompt> MlineEditDialog(CadCommandContext c)
        {
            var request = new CadMlineEditRequest();
            IEnumerable<CadPrompt> asked;

            if (c.Session.Request("MLEDIT", c.Document, request))
            {
                if (request.Tool == null || Array.IndexOf(MlineEditOptions, request.Tool) < 0)
                {
                    yield break;
                }

                asked = MlineEditWith(c, request.Tool);
            }
            else
            {
                asked = MlineEdit(c);
            }

            foreach (CadPrompt prompt in asked)
            {
                yield return prompt;
            }
        }
    }
}

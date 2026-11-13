using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The Rename, Delete, Load and Save of MLSTYLE typed: the buttons of AutoCAD 2021's Multiline Style dialog as questions,
    /// which the dialog's rules hold to: STANDARD is neither renamed nor deleted, nor is the current style or one a multiline
    /// is drawn by deleted; Load and Save read and write a file of styles as acad.mln is written.
    /// </summary>
    internal static partial class CadCommands
    {
        private const string MlineStyleFile = "acad.mln";

        /// <summary>Rename: a style of the drawing, then its new name.</summary>
        private static IEnumerable<CadPrompt> RenameMlineStyle(CadCommandContext c)
        {
            yield return CadPrompt.ForString("Enter name of the style to rename", CurrentMlineStyleName(c.Database));
            DxfMlineStyle style = TypedMlineStyle(c, CurrentMlineStyleName(c.Database));

            if (style == null)
            {
                yield break;
            }

            if (string.Equals(style.Name, "STANDARD", StringComparison.OrdinalIgnoreCase))
            {
                c.Write("The STANDARD multiline style cannot be renamed.");
                yield break;
            }

            yield return CadPrompt.ForString("Enter new name for " + style.Name);
            string name = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

            if (name.Length == 0)
            {
                yield break;
            }

            string refusal = string.Equals(name, style.Name, StringComparison.OrdinalIgnoreCase)
                ? null
                : MlineStyleNameRefusal(c.Database, name);

            if (refusal != null)
            {
                c.Write(refusal);
                yield break;
            }

            string old = c.Database.MLStyleDictionary.NameAt(style);
            c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Multiline styles", old, name) },
                s => s.Database.RenameMlineStyle(old, name)
            );
        }

        /// <summary>Delete: a style of the drawing that is not STANDARD, current or drawn by a multiline.</summary>
        private static IEnumerable<CadPrompt> DeleteMlineStyle(CadCommandContext c)
        {
            yield return CadPrompt.ForString("Enter name of the style to delete");
            DxfMlineStyle style = TypedMlineStyle(c, null);

            if (style == null)
            {
                yield break;
            }

            if (
                string.Equals(style.Name, "STANDARD", StringComparison.OrdinalIgnoreCase)
                || string.Equals(style.Name, CurrentMlineStyleName(c.Database), StringComparison.OrdinalIgnoreCase)
                || MlineStyleInUse(c.Database, style)
            )
            {
                c.Write(
                    "The multiline style "
                        + style.Name
                        + " cannot be deleted: STANDARD, the current style and one a multiline is drawn by are kept."
                );
                yield break;
            }

            string name = c.Database.MLStyleDictionary.NameAt(style);
            c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Multiline styles", name, null) },
                s => s.Database.MLStyleDictionary.Remove(name)
            );
        }

        /// <summary>Load: a file of styles, then a style of it (? lists them), added to the drawing.</summary>
        private static IEnumerable<CadPrompt> LoadMlineStyle(CadCommandContext c)
        {
            yield return CadPrompt.ForString("Enter name of multiline style file", MlineStyleFile);
            string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;
            string file = typed.Length == 0 ? MlineStyleFile : typed;
            string path = CadSupportFiles.Find(c.Document, Path.HasExtension(file) ? file : file + ".mln");

            if (path == null)
            {
                c.Write("Cannot find multiline style file \"" + file + "\".");
                yield break;
            }

            List<string> names;

            try
            {
                names = MlineStyleNamesIn(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is FormatException)
            {
                c.Write("Cannot read multiline style file \"" + path + "\": " + e.Message);
                yield break;
            }

            while (true)
            {
                yield return CadPrompt.ForString("Enter name of style to load or [?]");
                string name = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

                if (name.Length == 0)
                {
                    yield break;
                }

                if (name == "?")
                {
                    c.Write("Multiline styles in " + Path.GetFileName(path) + ":");

                    foreach (string each in names)
                    {
                        c.Write("  " + each);
                    }

                    continue;
                }

                if (!names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                {
                    c.Write("Cannot find multiline style \"" + name + "\" in " + Path.GetFileName(path) + ".");
                    yield break;
                }

                if (FindMlineStyle(c.Database, name) != null)
                {
                    c.Write("Multiline style " + name.ToUpperInvariant() + " already exists.");
                    yield break;
                }

                // A style drawn with a linetype the drawing has not is refused, and the edit taken back.
                CadEditResult loaded = c.Apply(
                    new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Multiline styles", null, name) },
                    s => s.Database.LoadMlineStyleFile(name, path)
                );

                if (!loaded.Succeeded && loaded.Message != null)
                {
                    c.Write(loaded.Message.Split('\r', '\n')[0]);
                }

                yield break;
            }
        }

        /// <summary>Save: a style of the drawing written to a file of styles, the file's other styles kept.</summary>
        private static IEnumerable<CadPrompt> SaveMlineStyle(CadCommandContext c)
        {
            yield return CadPrompt.ForString("Enter name of the style to save", CurrentMlineStyleName(c.Database));
            DxfMlineStyle style = TypedMlineStyle(c, CurrentMlineStyleName(c.Database));

            if (style == null)
            {
                yield break;
            }

            yield return CadPrompt.ForString("Enter name of multiline style file", MlineStyleFile);
            string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"') : string.Empty;
            string file = typed.Length == 0 ? MlineStyleFile : typed;
            file = Path.HasExtension(file) ? file : file + ".mln";
            string path = CadSupportFiles.Find(c.Document, file) ?? file;

            try
            {
                c.Database.SaveMlineStyleFile(style.Name, path);
                c.Write("Multiline style " + style.Name + " saved to " + Path.GetFileName(path) + ".");
            }
            catch (Exception e)
                when (e is IOException
                    || e is UnauthorizedAccessException
                    || e is FormatException
                    || e is ArgumentException
                    || e is NotSupportedException
                )
            {
                c.Write("Cannot save to multiline style file \"" + path + "\": " + e.Message.Split('\r', '\n')[0]);
            }
        }

        /// <summary>The style the answer to a name prompt names, the default where Enter took it; null, said so, where the drawing has none of the name.</summary>
        private static DxfMlineStyle TypedMlineStyle(CadCommandContext c, string fallback)
        {
            string typed =
                c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim()
                : c.Input.IsNone ? fallback ?? string.Empty
                : string.Empty;

            if (typed.Length == 0)
            {
                return null;
            }

            DxfMlineStyle found = FindMlineStyle(c.Database, typed);

            if (found == null)
            {
                c.Write("Cannot find multiline style \"" + typed + "\".");
            }

            return found;
        }

        /// <summary>The names of the styles a file of them holds, in its order.</summary>
        private static List<string> MlineStyleNamesIn(string path) => ReadMlnNames(path).ToList();

        /// <summary>The value after each group 2 of a file of styles: their names.</summary>
        private static IEnumerable<string> ReadMlnNames(string path)
        {
            string[] lines = File.ReadAllText(path).Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() == "MLSTYLE")
                {
                    i -= 1;
                    continue;
                }

                if (lines[i].Trim() == "2")
                {
                    yield return lines[i + 1].Trim();
                }
            }
        }
    }
}

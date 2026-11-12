using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// SAVEAS and DXFOUT on the command line (FILEDIA 0, or no window), asking in the words of AutoCAD 2021: SAVEAS the file
    /// format, for a DXF its settings, then the file; DXFOUT the file, then the settings.
    /// </summary>
    internal static partial class CadCommands
    {
        // The versions a DXF is written in, newest first as AutoCAD lists them in its Save As: the version, how its prompt
        // lists it, and the words it is typed by (the year, the Light name, or the listed form), any case.
        private static readonly Format[] DxfChoices =
        {
            new Format("AC1009", "R12(LT2)", "R12", "LT2", "R12(LT2)"),
            new Format("AC1015", "2000(LT2000)", "2000", "LT2000", "2000(LT2000)"),
            new Format("AC1018", "2004(LT2004)", "2004", "LT2004", "2004(LT2004)"),
            new Format("AC1021", "2007(LT2007)", "2007", "LT2007", "2007(LT2007)"),
            new Format("AC1024", "2010(LT2010)", "2010", "LT2010", "2010(LT2010)"),
            new Format("AC1027", "2013(LT2013)", "2013", "LT2013", "2013(LT2013)"),
            new Format("AC1032", "2018(LT2018)", "2018", "LT2018", "2018(LT2018)"),
        };

        // The formats of the DWG the prompt lists, as the version it stands for, how it is listed and how it is typed. R14 and
        // the five before 2018 are listed as AutoCAD lists them and refused, since the DWG is written as AutoCAD 2018 only.
        private static readonly Format[] DwgChoices =
        {
            new Format("AutoCAD R14(LT98/LT97) Drawing", "R14(LT98&LT97)", "R14", "LT98", "LT97", "R14(LT98&LT97)"),
            new Format("AutoCAD 2000(LT2000) Drawing", "2000(LT2000)", "2000", "LT2000", "2000(LT2000)"),
            new Format("AutoCAD 2004(LT2004) Drawing", "2004(LT2004)", "2004", "LT2004", "2004(LT2004)"),
            new Format("AutoCAD 2007(LT2007) Drawing", "2007(LT2007)", "2007", "LT2007", "2007(LT2007)"),
            new Format("AutoCAD 2010(LT2010) Drawing", "2010(LT2010)", "2010", "LT2010", "2010(LT2010)"),
            new Format("AutoCAD 2013(LT2013) Drawing", "2013(LT2013)", "2013", "LT2013", "2013(LT2013)"),
            new Format("AutoCAD 2018 Drawing", "2018(LT2018)", "2018", "LT2018", "2018(LT2018)"),
        };

        /// <summary>A format the prompts list: what it is known by, how the prompt lists it, and the words it is typed by, of any case.</summary>
        private sealed class Format
        {
            internal Format(string key, string label, params string[] words)
            {
                Key = key;
                Label = label;
                Words = words;
            }

            internal string Key { get; }

            internal string Label { get; }

            internal string[] Words { get; }
        }

        /// <summary>What a typed SAVEAS or DXFOUT decided: the file, and the version of a DXF (null for a DWG).</summary>
        private sealed class SaveChoice
        {
            internal string File { get; set; }

            internal string Version { get; set; }
        }

        internal static void AddSaveAsTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("SAVEAS", SaveAs));
            registry.Add(new CadCommand("DXFOUT", DxfOut));
        }

        /// <summary>
        /// SAVEAS: the window's Save Drawing As dialog; with FILEDIA 0 or no window, "Current file format" and "Enter file format
        /// [R14(LT98&amp;LT97)/2000(LT2000)/.../2018(LT2018)/Dxf/Template/Standards] &lt;2018&gt;:", the DWG of AutoCAD 2018 and
        /// Dxf the two it writes; Dxf asks its settings, the version among them, then the file, which is handed to the window to
        /// save, or saved here with no window.
        /// </summary>
        private static IEnumerable<CadPrompt> SaveAs(CadCommandContext c)
        {
            if (c.Session.FileDialogs && c.Session.Request("SAVEAS"))
            {
                yield break;
            }

            foreach (CadPrompt prompt in SaveAsTyped(c, c.Document.FileName ?? c.Document.Title))
            {
                yield return prompt;
            }
        }

        /// <summary>The typed Save As of SAVEAS and of QSAVE of a drawing with no file: the format, the settings of a DXF, the file, then the saving.</summary>
        private static IEnumerable<CadPrompt> SaveAsTyped(CadCommandContext c, string offered)
        {
            bool wasDwg = CadDrawingFile.IsDwgName(offered);
            string own = c.Document.SaveVersion ?? c.Document.Database.Version;
            string current = wasDwg ? "AutoCAD 2018 Drawing" : "AutoCAD " + DxfShown(own) + " DXF Format";
            c.Write("Current file format: " + current);
            string list = string.Join("/", DwgChoices.Select(d => d.Label)) + "/Dxf/Template/Standards";
            bool dxf = false;
            bool typedFormat = false;

            while (true)
            {
                yield return CadPrompt.ForString("Enter file format [" + list + "]", wasDwg ? "2018" : "Dxf");
                string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

                if (typed.Length == 0)
                {
                    dxf = !wasDwg;
                    break;
                }

                typedFormat = true;

                if (Named(typed, "Dxf"))
                {
                    dxf = true;
                    break;
                }

                if (Named(typed, "Template"))
                {
                    c.Write("A drawing template (*.dwt) is not written here.");
                    yield break;
                }

                if (Named(typed, "Standards"))
                {
                    c.Write("A drawing standards file (*.dws) is not written here.");
                    yield break;
                }

                var dwg = DwgChoices.Where(d => d.Words.Contains(typed, StringComparer.OrdinalIgnoreCase)).ToList();

                if (dwg.Count == 0)
                {
                    c.Write("Invalid option keyword.");
                    continue;
                }

                if (dwg[0].Label != "2018(LT2018)")
                {
                    c.Write(dwg[0].Key + " (*.dwg) is not written: a DWG is saved as AutoCAD 2018 (2018).");
                    yield break;
                }

                break;
            }

            var choice = new SaveChoice();

            if (dxf)
            {
                choice.Version = wasDwg ? "AC1032" : DxfDefault(own);

                foreach (CadPrompt prompt in DxfSettings(c, choice))
                {
                    yield return prompt;
                }

                if (choice.Version == null)
                {
                    yield break;
                }

                offered = wasDwg ? Path.ChangeExtension(offered, ".dxf") : offered;
            }
            else
            {
                offered = wasDwg ? offered : Path.ChangeExtension(offered, ".dwg");
            }

            yield return CadPrompt.ForString("Save drawing as", offered);
            string file = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"') : string.Empty;
            file = file.Length == 0 ? offered : file;
            string extension = dxf ? ".dxf" : ".dwg";
            string other = dxf ? ".dwg" : ".dxf";

            if (file.EndsWith(other, StringComparison.OrdinalIgnoreCase))
            {
                if (typedFormat)
                {
                    c.Write(
                        (dxf ? "A DXF" : "A DWG")
                            + " is saved to a name ending in "
                            + extension
                            + ", not "
                            + other
                            + "."
                    );
                    yield break;
                }

                // The format was no more than the one offered: the name says what it is, as it did before the format was asked.
                dxf = !dxf;
                extension = other;
                choice.Version = dxf ? (wasDwg ? "AC1032" : DxfDefault(own)) : null;
            }

            choice.File = file.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? file : file + extension;

            var job = new CadExportJob(
                "Save Drawing As",
                new[] { CadExportJob.Dwg }.Concat(CadExportJob.DxfSaveTypes).ToList(),
                dxf ? CadExportJob.DxfTypeOfVersion(choice.Version) : CadExportJob.Dwg,
                choice.File
            );

            if (c.Session.Request("SAVEAS", c.Document, job))
            {
                yield break;
            }

            try
            {
                c.Document.SaveAs(choice.File, choice.Version, null);
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                )
            {
                c.Write("Cannot save " + choice.File + ": " + exception.Message);
                yield break;
            }

            c.Write("Saved " + c.Document.FileName + ".");
            SayLeftOut(c);
        }

        /// <summary>
        /// DXFOUT: a DXF copy of the drawing, which keeps its own file. The window's Create DXF File dialog, or with FILEDIA 0 or
        /// no window "Enter DXF file name &lt;file&gt;:" then the settings, as SAVEAS asks them.
        /// </summary>
        private static IEnumerable<CadPrompt> DxfOut(CadCommandContext c)
        {
            string own = c.Document.SaveVersion ?? c.Document.Database.Version;
            string name = Path.GetFileNameWithoutExtension(c.Document.FileName ?? c.Document.Title);
            string folder = c.Document.HasFile ? Path.GetDirectoryName(c.Document.FileName) : null;
            string offered = string.IsNullOrEmpty(folder) ? name : Path.Combine(folder, name);
            var choice = new SaveChoice
            {
                Version = CadDrawingFile.IsDwgName(c.Document.FileName ?? c.Document.Title)
                    ? "AC1032"
                    : DxfDefault(own),
            };

            if (c.Session.FileDialogs)
            {
                var job = new CadExportJob(
                    "Create DXF File",
                    CadExportJob.DxfSaveTypes,
                    CadExportJob.DxfTypeOfVersion(choice.Version),
                    offered + ".dxf"
                );

                if (c.Session.Request("DXFOUT", c.Document, job))
                {
                    if (job.FileName != null)
                    {
                        choice.File = job.FileName;
                        choice.Version = CadExportJob.DxfVersionOfType(job.FileType) ?? choice.Version;
                        WriteCopy(c, choice);
                    }

                    yield break;
                }
            }

            yield return CadPrompt.ForString("Enter DXF file name", offered);
            string file = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"') : string.Empty;
            file = file.Length == 0 ? offered : file;
            choice.File = file.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase) ? file : file + ".dxf";

            foreach (CadPrompt prompt in DxfSettings(c, choice))
            {
                yield return prompt;
            }

            if (choice.Version != null)
            {
                WriteCopy(c, choice);
            }
        }

        /// <summary>Writes the copy DXFOUT made, or says why not.</summary>
        private static void WriteCopy(CadCommandContext c, SaveChoice choice)
        {
            try
            {
                foreach (string sentence in c.Document.SaveCopy(choice.File, choice.Version))
                {
                    c.Write(sentence);
                }
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                )
            {
                c.Write("Cannot save " + choice.File + ": " + exception.Message);
                return;
            }

            c.Write("Wrote " + Path.GetFullPath(choice.File) + ".");
        }

        /// <summary>
        /// The settings of a DXF as AutoCAD 2021 asks them: "Current DXF settings: Precision= 16 Format= ASCII Preview= No
        /// Version= 2018" and "Enter decimal places of accuracy (0 to 16) or [Version] &lt;16&gt;:", Version asking the version
        /// of the file. Binary, Preview and select Objects are not offered: the file is written in ASCII, at full accuracy, with
        /// the drawing whole. Leaves <see cref="SaveChoice.Version"/> null where the command was refused.
        /// </summary>
        private static IEnumerable<CadPrompt> DxfSettings(CadCommandContext c, SaveChoice choice)
        {
            while (true)
            {
                c.Write(
                    "Current DXF settings: Precision= 16 Format= ASCII Preview= No Version= " + DxfShown(choice.Version)
                );
                yield return CadPrompt.ForInteger(
                    "Enter decimal places of accuracy (0 to 16)",
                    new[] { "Version" },
                    "16"
                );

                if (c.Input.Is("Version"))
                {
                    foreach (CadPrompt prompt in DxfVersion(c, choice))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (c.Input.Kind == CadInputKind.Number)
                {
                    if (c.Input.Number < 0 || c.Input.Number > 16)
                    {
                        c.Write("Value must be between 0 and 16.");
                        continue;
                    }

                    if (c.Input.Number < 16)
                    {
                        c.Write("The coordinates are written in full: the places of accuracy are not set.");
                    }
                }

                yield break;
            }
        }

        /// <summary>"Enter version [R12(LT2)/2000(LT2000)/.../2018(LT2018)] &lt;2018&gt;:", the version typed taken; R14 and the others refused and asked again.</summary>
        private static IEnumerable<CadPrompt> DxfVersion(CadCommandContext c, SaveChoice choice)
        {
            string list = string.Join("/", DxfChoices.Select(d => d.Label));

            while (true)
            {
                yield return CadPrompt.ForString("Enter version [" + list + "]", DxfShown(choice.Version));
                string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

                if (typed.Length == 0)
                {
                    yield break;
                }

                var found = DxfChoices.Where(d => d.Words.Contains(typed, StringComparer.OrdinalIgnoreCase)).ToList();

                if (found.Count > 0)
                {
                    choice.Version = found[0].Key;
                    yield break;
                }

                if (DwgChoices[0].Words.Contains(typed, StringComparer.OrdinalIgnoreCase))
                {
                    c.Write("R14 is not written: a DXF is saved as R12 or R2000 and later.");
                }
                else
                {
                    c.Write("Invalid option keyword.");
                }
            }
        }

        /// <summary>Whether an answer names a word of the prompt: whole, or by its first letters, of either case.</summary>
        private static bool Named(string typed, string word) =>
            typed.Length > 0 && word.StartsWith(typed, StringComparison.OrdinalIgnoreCase);

        /// <summary>The version a DXF is offered in: the own of a drawing that is a DXF where it is written, AutoCAD 2018 for any other, and for a DWG, which is of AutoCAD 2018.</summary>
        private static string DxfDefault(string own) =>
            DxfChoices.Any(d => string.Equals(d.Key, own, StringComparison.OrdinalIgnoreCase))
                ? own.ToUpperInvariant()
                : "AC1032";

        /// <summary>A version as the prompts show it: "2018" for AC1032, "R12" for AC1009.</summary>
        private static string DxfShown(string version)
        {
            foreach (Format format in DxfChoices)
            {
                if (string.Equals(format.Key, version, StringComparison.OrdinalIgnoreCase))
                {
                    return format.Words[0];
                }
            }

            return string.IsNullOrEmpty(version) ? "2018" : version;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Template and SAveas of LAYOUT and -LAYOUT, as AutoCAD 2021's console asks and answers them (probed with layouts made
    /// in a side database and saved as DXF): Template reads the layouts of a template file in, SAveas writes one to a template.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>The types of LAYOUT Template's Select Template From File dialog, as AutoCAD 2021 lists them.</summary>
        private static readonly string[] TemplateOpenTypes =
        {
            "Drawing Template (*.dwt)",
            "Drawing (*.dwg)",
            "DXF (*.dxf)",
        };

        /// <summary>The types of LAYOUT SAveas's Create Drawing File dialog: a template first, then the DWG and the DXF versions written.</summary>
        private static readonly string[] TemplateSaveTypes = new[]
        {
            "AutoCAD Drawing Template (*.dwt)",
            CadExportJob.Dwg,
        }
            .Concat(CadExportJob.DxfSaveTypes)
            .ToArray();

        /// <summary>
        /// The layout LAYOUT offers for Set, Rename, Copy, Delete and SAveas: the one shown, the first tab where Model is
        /// shown, as AutoCAD offers <c>&lt;Layout1&gt;</c> in a drawing that shows Model.
        /// </summary>
        private static string OfferedLayout(CadCommandContext c) =>
            c.Document.CurrentSpace.IsModel ? LayoutSpaces(c).FirstOrDefault()?.Name : c.Document.CurrentSpace.Name;

        /// <summary>
        /// LAYOUT Template: "Enter file name:", then "Enter layout name(s) or [?]:". A name with no extension takes .dwt, a file
        /// not there is "&lt;name&gt;": Can't find file.; ? lists "Layout(s) in drawing:" and its layouts and asks again; the
        /// names are parted by commas, may hold wild cards, and a name no layout of the file has is passed over, "The specified
        /// layout(s) were not found in &lt;file&gt;." where none is found and the question asked again; Enter ends the command.
        /// Each layout found comes with its sheet and its entities, in the order of the file's tabs, as the last tabs of the
        /// drawing, named as it is where the drawing has no layout of the name, else Layout3-Plan: the first number N the
        /// drawing has no layout starting with LayoutN. Its layers, linetypes, styles and blocks come with it where the drawing
        /// has none of the name. The layout shown does not change. Nothing is said of the making; the whole is one undo step.
        /// With FILEDIA 1 and a window, the file is chosen in its Select Template From File dialog and the layouts in its
        /// Insert Layout(s) dialog (<see cref="CadInsertLayoutRequest"/>), several at once, the first picked to start with;
        /// Cancel in either brings nothing in.
        /// </summary>
        private static IEnumerable<CadPrompt> LayoutTemplate(CadCommandContext c)
        {
            var job = new CadExportJob(
                "Select Template From File",
                TemplateOpenTypes,
                TemplateOpenTypes[0],
                c.Document.HasFile ? c.Document.DrawingFolder + Path.DirectorySeparatorChar : null
            )
            {
                Opens = true,
            };
            bool dialogs = c.Session.FileDialogs && c.Session.Request("LAYOUT", c.Document, job);
            string file;

            if (dialogs)
            {
                if (job.FileName == null)
                {
                    yield break;
                }

                file = job.FileName;
            }
            else
            {
                yield return CadPrompt.ForString("Enter file name");
                string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"') : string.Empty;

                if (typed.Length == 0)
                {
                    yield break;
                }

                file = ResolveTemplate(c, typed);

                if (file == null)
                {
                    string leaf = Path.GetFileName(Path.HasExtension(typed) ? typed : typed + ".dwt");
                    c.Write("\"" + leaf + "\": Can't find file.");
                    yield break;
                }
            }

            DxfDatabase source;

            try
            {
                source = CadDrawingFile.Open(file);
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is FormatException
                    || exception is NotSupportedException
                )
            {
                c.Write("Cannot read " + file + ": " + exception.Message);
                yield break;
            }

            List<DxfLayout> sheets = source
                .Layouts.Where(l =>
                    !string.Equals(l.LayoutName, "Model", StringComparison.OrdinalIgnoreCase)
                    && l.BlockTableRecord != null
                )
                .ToList();
            List<DxfLayout> chosen = null;
            var request = new CadInsertLayoutRequest(file, sheets.Select(l => l.LayoutName).ToList());

            if (dialogs && c.Session.Request("LAYOUT", c.Document, request))
            {
                chosen = request.IsAccepted
                    ? sheets.Where(l => request.Picked.Contains(l.LayoutName, StringComparer.Ordinal)).ToList()
                    : new List<DxfLayout>();

                if (chosen.Count == 0)
                {
                    yield break;
                }
            }

            while (chosen == null)
            {
                yield return CadPrompt.ForString("Enter layout name(s) or [?]");
                string names = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

                if (names.Length == 0)
                {
                    yield break;
                }

                if (names == "?")
                {
                    c.Write("Layout(s) in drawing:");

                    foreach (DxfLayout sheet in sheets)
                    {
                        c.Write(sheet.LayoutName);
                    }

                    continue;
                }

                List<DxfLayout> found = sheets.Where(l => CadWildcard.IsMatch(l.LayoutName, names, true)).ToList();

                if (found.Count > 0)
                {
                    chosen = found;
                    break;
                }

                c.Write("The specified layout(s) were not found in " + file + ".");
            }

            c.Apply(
                new[]
                {
                    new CadEditRecord(
                        CadEditRecordKind.TableChanged,
                        null,
                        "Layout template",
                        null,
                        string.Join(",", chosen.Select(l => l.LayoutName))
                    ),
                },
                s =>
                {
                    var taken = new List<string>(LayoutNames(s.Database));

                    foreach (DxfLayout sheet in chosen)
                    {
                        string name = TemplateLayoutName(taken, sheet.LayoutName);
                        taken.Add(name);
                        ImportLayout(s.Database, source, sheet, name);
                    }
                }
            );
        }

        /// <summary>The file a template is typed as: as typed, else from the drawing's folder, a name with no extension given .dwt; null where there is none.</summary>
        private static string ResolveTemplate(CadCommandContext c, string typed)
        {
            string name = Path.HasExtension(typed) ? typed : typed + ".dwt";

            try
            {
                if (File.Exists(name))
                {
                    return Path.GetFullPath(name);
                }

                string folder = c.Document.DrawingFolder;

                if (!Path.IsPathRooted(name) && !string.IsNullOrEmpty(folder))
                {
                    string inFolder = Path.Combine(folder, name);
                    return File.Exists(inFolder) ? Path.GetFullPath(inFolder) : null;
                }
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is PathTooLongException
                )
            {
                return null;
            }

            return null;
        }

        /// <summary>The names of the drawing's layouts.</summary>
        private static IEnumerable<string> LayoutNames(DxfDatabase database) =>
            database.Layouts.Select(l => l.LayoutName);

        /// <summary>
        /// The name a layout of a template takes: its own where no layout of the drawing has it, else <c>Layout3-Plan</c>, N the
        /// first number from 1 that no layout's name starts with LayoutN (the next a separator or the end) as AutoCAD 2021 numbers
        /// them: with Layout1, Layout2 and Plan there, a second Plan is Layout3-Plan, and the next Layout1 Layout4-Layout1.
        /// </summary>
        private static string TemplateLayoutName(IReadOnlyList<string> taken, string wanted)
        {
            if (!taken.Any(n => string.Equals(n, wanted, StringComparison.OrdinalIgnoreCase)))
            {
                return wanted;
            }

            for (int number = 1; ; number++)
            {
                string stem = "Layout" + number.ToString(CultureInfo.InvariantCulture);

                if (
                    !taken.Any(n =>
                        n.StartsWith(stem, StringComparison.OrdinalIgnoreCase)
                        && (n.Length == stem.Length || !char.IsDigit(n[stem.Length]))
                    )
                )
                {
                    return stem + "-" + wanted;
                }
            }
        }

        /// <summary>A layout of the name made of one of another drawing: its page settings, and a copy of every entity of its block, its viewports among them, with the layers, styles and blocks they name.</summary>
        private static void ImportLayout(DxfDatabase database, DxfDatabase source, DxfLayout from, string name)
        {
            var layout = new DxfLayout { LayoutName = name };
            layout.AddToLayoutDictionary(database, null);
            CopySheet(source, from, layout);
        }

        /// <summary>
        /// Copies the page settings and the entities of a layout into a new one: the sheet's own viewport (number 1) is made again,
        /// as a layout of the drawing makes it, not cloned, since a viewport appended to a layout is numbered from 2 and the copy
        /// would show model space across its sheet; the other viewports and entities are cloned.
        /// </summary>
        private static void CopySheet(DxfDatabase source, DxfLayout from, DxfLayout layout)
        {
            layout.CopyFrom(from);
            bool sheet = from.BlockTableRecord.OfType<DxfViewport>().Any(v => v.Number == 1);

            if (sheet)
            {
                layout.Initialize();
            }

            var entities = from.BlockTableRecord.Where(e => !(sheet && e is DxfViewport v && v.Number == 1)).ToList();

            if (entities.Count > 0)
            {
                source.WblockCloneObjects(entities, layout.BlockTableRecord);
            }
        }

        /// <summary>
        /// LAYOUT SAveas: "Enter layout to save to template &lt;Layout1&gt;:", then "Enter name of output file:", and the file
        /// written, with nothing said. Model is refused, "May not save Model tab.", a name that is no layout's, Layout "x" does not
        /// exist. The template is the drawing with the one layout kept and no entity in model space, as AutoCAD 2021 writes it,
        /// its layers, linetypes, styles and blocks as they are. A name with no extension takes .dwt; .dwt and .dwg are written as
        /// a DWG of AutoCAD 2018, .dxf as a DXF after the questions DXFOUT asks of its settings; a file of the name is replaced.
        /// With FILEDIA 1 and a window, the file is chosen in its Create Drawing File dialog (a DXF type giving the version, no
        /// questions asked), and a .dwt shows Template Options (<see cref="CadTemplateOptionsRequest"/>): the description is
        /// written into the DWG's template section and MEASUREMENT set as chosen; Cancel writes nothing.
        /// </summary>
        private static IEnumerable<CadPrompt> LayoutSaveAs(CadCommandContext c)
        {
            string offered = OfferedLayout(c);
            yield return CadPrompt.ForString("Enter layout to save to template", offered);
            string typed = Typed(c, offered);
            CadSpace space = FindLayout(c, typed);

            if (space == null || space.IsModel)
            {
                c.Write(space == null ? "Layout \"" + typed + "\" does not exist." : "May not save Model tab.");
                yield break;
            }

            var choice = new SaveChoice { Version = DxfDefault(c.Document.SaveVersion ?? c.Document.Database.Version) };
            var job = new CadExportJob(
                "Create Drawing File",
                TemplateSaveTypes,
                TemplateSaveTypes[0],
                Path.Combine(c.Document.HasFile ? c.Document.DrawingFolder : string.Empty, space.Name + ".dwt")
            );
            bool dialogs = c.Session.FileDialogs && c.Session.Request("LAYOUT", c.Document, job);
            string file;

            if (dialogs)
            {
                if (job.FileName == null)
                {
                    yield break;
                }

                file = job.FileName;
                choice.Version = CadExportJob.DxfVersionOfType(job.FileType) ?? choice.Version;
            }
            else
            {
                yield return CadPrompt.ForString("Enter name of output file");
                file = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"') : string.Empty;

                if (file.Length == 0)
                {
                    yield break;
                }
            }

            string extension = Path.GetExtension(file);

            if (extension.Length == 0)
            {
                file += ".dwt";
                extension = ".dwt";
            }

            bool dxf = string.Equals(extension, ".dxf", StringComparison.OrdinalIgnoreCase);

            if (
                !dxf
                && !string.Equals(extension, ".dwt", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".dwg", StringComparison.OrdinalIgnoreCase)
            )
            {
                c.Write("A template is saved as .dwt, .dwg or .dxf, not " + extension + ".");
                yield break;
            }

            if (dxf && !dialogs)
            {
                foreach (CadPrompt prompt in DxfSettings(c, choice))
                {
                    yield return prompt;
                }

                if (choice.Version == null)
                {
                    yield break;
                }
            }

            if (!Path.IsPathRooted(file) && c.Document.HasFile)
            {
                file = Path.Combine(c.Document.DrawingFolder, file);
            }

            // A .dwt chosen in the dialog: Template Options, its description and measurement; Cancel writes nothing.
            CadTemplateOptionsRequest options = null;

            if (dialogs && string.Equals(extension, ".dwt", StringComparison.OrdinalIgnoreCase))
            {
                options = new CadTemplateOptionsRequest(
                    file,
                    c.Document.Database.Measurement == DxfMeasurementValue.Metric
                );

                if (c.Session.Request("LAYOUT", c.Document, options) && !options.IsAccepted)
                {
                    yield break;
                }
            }

            try
            {
                DxfDatabase template = TemplateOf(c.Document.Database, space.Layout.LayoutName);

                if (options != null && options.IsAccepted)
                {
                    template.Measurement = options.IsMetric ? DxfMeasurementValue.Metric : DxfMeasurementValue.English;
                }

                if (dxf)
                {
                    foreach (string sentence in template.Save(Path.GetFullPath(file), choice.Version))
                    {
                        c.Write(sentence);
                    }
                }
                else
                {
                    byte[] bytes = DwgWriter.ToDwg(
                        template.ToText(),
                        DwgWriter.DefaultVersion,
                        out IDictionary<string, int> left,
                        null,
                        options != null && options.IsAccepted ? options.Description : null
                    );
                    File.WriteAllBytes(Path.GetFullPath(file), bytes);

                    if (left.Count > 0)
                    {
                        c.Write(
                            "Left out of the DWG, not written yet: "
                                + string.Join(
                                    ", ",
                                    left.Select(p => p.Key + " " + p.Value.ToString(CultureInfo.InvariantCulture))
                                )
                                + "."
                        );
                    }
                }
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                )
            {
                c.Write("Cannot save " + file + ": " + exception.Message);
            }
        }

        /// <summary>A copy of a drawing with one layout kept and nothing in model space, as the template of that layout: the drawing as it is, read again, the other layouts and their blocks taken out and every entity of model space erased.</summary>
        private static DxfDatabase TemplateOf(DxfDatabase database, string layoutName)
        {
            DxfDatabase copy = DxfDatabase.Parse(database.ToText());

            // The one that holds *Paper_Space last: deleted, it takes the place of the layout that follows it, which is the one kept.
            foreach (
                DxfLayout other in copy
                    .Layouts.Where(l =>
                        !string.Equals(l.LayoutName, "Model", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(l.LayoutName, layoutName, StringComparison.Ordinal)
                    )
                    .OrderBy(l =>
                        string.Equals(
                            l.BlockTableRecord.Name,
                            DxfBlockTableRecord.PaperSpace,
                            StringComparison.OrdinalIgnoreCase
                        )
                            ? 1
                            : 0
                    )
                    .ToList()
            )
            {
                RemoveLayout(copy, other);
            }

            foreach (DxfEntity entity in copy.ModelSpace)
            {
                entity.Erase();
            }

            return copy;
        }
    }
}

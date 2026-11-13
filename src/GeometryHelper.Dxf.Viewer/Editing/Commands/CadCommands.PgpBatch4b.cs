using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Batch 4 of acad.pgp's 2D commands, the command-line forms and SAVE: -PAN, -MTEXT (in CadCommands.DrawText.cs), -LAYOUT,
    /// -XBIND with XBIND, and SAVE; each asks in the words of AutoCAD 2021, read from its resources and its console.
    /// </summary>
    internal static partial class CadCommands
    {
        // The options -LAYOUT offers, as AutoCAD 2021 writes them; Set is what Enter takes, written in small letters there.
        private static readonly string[] LayoutChoices =
        {
            "Copy",
            "Delete",
            "New",
            "Template",
            "Rename",
            "SAveas",
            "Set",
            "?",
        };

        // What a layout's name may not hold, as AutoCAD 2021 says it.
        private const string LayoutBadCharacters = "<>/\\\":;?*|,=`";

        // The kinds of symbol XBIND binds: how it is typed, the table of the drawing, and the word the prompt and the count use.
        private static readonly string[][] XbindKinds =
        {
            new[] { "Block", "BLOCK", "Block" },
            new[] { "Dimstyle", "DIMSTYLE", "Dimstyle" },
            new[] { "LAyer", "LAYER", "Layer" },
            new[] { "LType", "LTYPE", "Linetype" },
            new[] { "Style", "STYLE", "Style" },
        };

        private static void AddBatchFourBTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("-PAN", c => PanBody(c, true)));
            registry.Add(new CadCommand("-MTEXT", c => MTextBody(c, true)));
            registry.Add(new CadCommand("-LAYOUT", LayoutTyped));
            registry.Add(new CadCommand("-XBIND", XBind));
            registry.Add(new CadCommand("XBIND", XBindDialog));
            registry.Add(new CadCommand("SAVE", Save));
        }

        /// <summary>
        /// The first question of PAN: "Specify base point or displacement:". Typed as -PAN, Enter is refused, "Requires two
        /// points, or a displacement followed by ENTER.", and the question asked again, as AutoCAD 2021 does.
        /// </summary>
        private static IEnumerable<CadPrompt> PanBase(CadCommandContext c, bool typed)
        {
            while (true)
            {
                yield return CadPrompt.ForPoint("Specify base point or displacement");

                if (!typed || !c.Input.IsNone)
                {
                    yield break;
                }

                c.Write("Requires two points, or a displacement followed by ENTER.");
            }
        }

        #region -LAYOUT

        /// <summary>
        /// -LAYOUT: "Enter layout option [Copy/Delete/New/Template/Rename/SAveas/Set/?] &lt;set&gt;:" in the words of AutoCAD
        /// 2021, each option one undo step: Copy makes a layout of another, named as the layout and (2) where the answer is
        /// Enter, with its sheet and its entities; Delete takes one out, the last leaving a new Layout1; New adds one of the
        /// first name LayoutN not taken; Rename, Set and ? as AutoCAD asks them. Template and SAveas, which read and write a
        /// template file, say they are not built.
        /// </summary>
        private static IEnumerable<CadPrompt> LayoutTyped(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword("Enter layout option", LayoutChoices, "set");

            if (c.Input.Is("?"))
            {
                c.Write("Active Layouts:");
                c.Write(string.Empty);

                foreach (
                    CadSpace space in LayoutSpaces(c)
                        .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(s => s.Name, StringComparer.Ordinal)
                )
                {
                    c.Write("Layout: " + space.Name.PadRight(12) + "  Block name: " + space.Block.Name + ".");
                }

                yield break;
            }

            if (c.Input.Is("Template"))
            {
                c.Write("A layout is not brought in from a template file here.");
                yield break;
            }

            if (c.Input.Is("SAveas"))
            {
                c.Write("A layout is not saved to a template file (*.dwt) here.");
                yield break;
            }

            string option = c.Input.Kind == CadInputKind.Keyword ? c.Input.Text : null;
            string offered = c.Space.Name;

            if (option == "Set")
            {
                yield return CadPrompt.ForString("Enter layout to make current", offered);
                string typed = Typed(c, offered);
                CadSpace space = FindLayout(c, typed);

                if (space == null)
                {
                    c.Write("Cannot find layout \"" + typed + "\".");
                    yield break;
                }

                c.Document.ShowSpace(space);
            }
            else if (option == "New")
            {
                int next = 1;

                while (FindLayout(c, "Layout" + next.ToString(CultureInfo.InvariantCulture)) != null)
                {
                    next++;
                }

                string proposed = "Layout" + next.ToString(CultureInfo.InvariantCulture);
                yield return CadPrompt.ForString("Enter new Layout name", proposed);
                string name = Typed(c, proposed);

                if (!LayoutNameFits(c, name))
                {
                    yield break;
                }

                if (FindLayout(c, name) != null)
                {
                    c.Write("Layout \"" + name + "\" already exists, not created.");
                    yield break;
                }

                c.Apply(
                    new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "New layout", null, name) },
                    s => AddLayout(s.Database, name)
                );
            }
            else if (option == "Rename")
            {
                yield return CadPrompt.ForString("Enter layout to rename", offered);
                string old = Typed(c, offered);
                CadSpace space = FindLayout(c, old);

                if (space == null || space.IsModel)
                {
                    c.Write(space == null ? "Layout \"" + old + "\" does not exist." : "May not rename Model Tab.");
                    yield break;
                }

                yield return CadPrompt.ForString("Enter new layout name");
                string name = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

                if (!LayoutNameFits(c, name))
                {
                    yield break;
                }

                CadSpace taken = FindLayout(c, name);

                if (taken != null && taken != space)
                {
                    c.Write("Layout \"" + name + "\" already exists, not renamed.");
                    yield break;
                }

                DxfLayout layout = space.Layout;
                CadEditResult renamed = c.Apply(
                    new[]
                    {
                        new CadEditRecord(CadEditRecordKind.TableChanged, null, "Rename layout", space.Name, name),
                    },
                    s => layout.LayoutName = name
                );

                if (renamed.Succeeded)
                {
                    c.Write("Layout \"" + old + "\" renamed to \"" + name + "\".");
                }
            }
            else if (option == "Copy")
            {
                yield return CadPrompt.ForString("Enter name of layout to copy", offered);
                string from = Typed(c, offered);
                CadSpace space = FindLayout(c, from);

                if (space == null || space.IsModel)
                {
                    c.Write(space == null ? "Layout \"" + from + "\" does not exist." : "May not copy Model tab.");
                    yield break;
                }

                int copies = 2;

                while (FindLayout(c, from + " (" + copies.ToString(CultureInfo.InvariantCulture) + ")") != null)
                {
                    copies++;
                }

                string proposed = from + " (" + copies.ToString(CultureInfo.InvariantCulture) + ")";
                yield return CadPrompt.ForString("Enter layout name for copy", proposed);
                string name = Typed(c, proposed);

                if (!LayoutNameFits(c, name))
                {
                    yield break;
                }

                if (FindLayout(c, name) != null)
                {
                    c.Write("Layout \"" + name + "\" already exists, not copied.");
                    yield break;
                }

                DxfLayout source = space.Layout;
                CadEditResult copied = c.Apply(
                    new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Copy layout", space.Name, name) },
                    s => CopyLayout(s.Database, source, name)
                );

                if (copied.Succeeded)
                {
                    c.Write("Layout \"" + from + "\" copied to \"" + name + "\".");
                }
            }
            else if (option == "Delete")
            {
                yield return CadPrompt.ForString("Enter name of layout to delete", offered);
                string typed = Typed(c, offered);
                CadSpace space = FindLayout(c, typed);

                if (space == null || space.IsModel)
                {
                    c.Write(
                        space == null
                            ? "Cannot find layout \"" + typed + "\", nothing deleted."
                            : "May not delete Model tab."
                    );
                    yield break;
                }

                DxfLayout layout = space.Layout;
                CadEditResult deleted = c.Apply(
                    new[]
                    {
                        new CadEditRecord(CadEditRecordKind.TableChanged, null, "Delete layout", space.Name, null),
                    },
                    s => RemoveLayout(s.Database, layout)
                );

                if (deleted.Succeeded)
                {
                    c.Write("Layout \"" + typed + "\" deleted.");
                }
            }
        }

        /// <summary>What a prompt for text was answered: the words typed, trimmed, or the default where Enter was pressed.</summary>
        private static string Typed(CadCommandContext c, string offered) =>
            c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : offered;

        /// <summary>The layouts other than Model, whose block the tab shows.</summary>
        private static IEnumerable<CadSpace> LayoutSpaces(CadCommandContext c) =>
            c.Document.Spaces.Where(s => !s.IsModel && s.Layout != null && s.Block != null);

        /// <summary>Says why a name is no layout's, as AutoCAD 2021 says it; true where it is one.</summary>
        private static bool LayoutNameFits(CadCommandContext c, string name)
        {
            if (name.Length == 0 || name.Length > 255)
            {
                c.Write("Invalid layout name.");
                return false;
            }

            if (name.IndexOfAny(LayoutBadCharacters.ToCharArray()) >= 0)
            {
                c.Write("Invalid characters in Layout name: \"" + name + "\".");
                c.Write("The following characters are not valid in layout names:  " + LayoutBadCharacters);
                return false;
            }

            return true;
        }

        /// <summary>A layout of the name, with the viewport that shows its sheet.</summary>
        private static DxfLayout AddLayout(DxfDatabase database, string name)
        {
            var layout = new DxfLayout { LayoutName = name };
            layout.AddToLayoutDictionary(database, null);
            layout.Initialize();
            return layout;
        }

        /// <summary>A layout of the name made of another: its page settings, and a copy of every entity of its block, its viewports among them.</summary>
        private static void CopyLayout(DxfDatabase database, DxfLayout source, string name)
        {
            var layout = new DxfLayout { LayoutName = name };
            layout.AddToLayoutDictionary(database, null);
            layout.CopyFrom(source);
            database.WblockCloneObjects(new List<DxfEntity>(source.BlockTableRecord), layout.BlockTableRecord);
        }

        /// <summary>
        /// A layout taken out of the drawing with its block and what is drawn in it; where it was the last of the sheets a new
        /// Layout1 takes its place, as AutoCAD leaves a drawing one.
        /// </summary>
        private static void RemoveLayout(DxfDatabase database, DxfLayout layout)
        {
            DxfBlockTableRecord block = layout.BlockTableRecord;
            database.LayoutDictionary.Remove(layout.LayoutName);
            block.Remove();

            if (!database.Layouts.Any(l => !string.Equals(l.LayoutName, "Model", StringComparison.OrdinalIgnoreCase)))
            {
                AddLayout(database, "Layout1");
            }
        }

        #endregion

        #region -XBIND

        /// <summary>
        /// -XBIND and XBIND (the window shows no dialog of it): "Enter symbol type to bind
        /// [Block/Dimstyle/LAyer/LType/Style]:", then "Enter dependent Layer name(s):", names with wildcards parted by commas,
        /// as AutoCAD 2021 asks them; a layer, linetype, text style or dimension style an external reference brought
        /// (<c>grid|WALL</c>) is renamed <c>grid$0$WALL</c> and is the drawing's own, a block of the reference's drawing becomes
        /// the block <c>grid$0$BLK</c>. "1 Layer(s) bound." or "No matching Layer names found.", and the first question again,
        /// until Enter, the whole command one undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> XBind(CadCommandContext c)
        {
            while (true)
            {
                yield return CadPrompt.ForKeyword("Enter symbol type to bind", XbindKinds.Select(k => k[0]).ToArray());

                string[] kind = c.Input.Kind == CadInputKind.Keyword ? XbindKinds.First(k => c.Input.Is(k[0])) : null;

                if (kind == null)
                {
                    yield break;
                }

                yield return CadPrompt.ForString("Enter dependent " + kind[2] + " name(s)");

                if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0)
                {
                    continue;
                }

                string[] patterns = c.Input.Text.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
                int bound = kind[1] == "BLOCK" ? BindBlocks(c, patterns) : BindSymbols(c, kind[1], patterns);

                c.Write(
                    bound == 0
                        ? "No matching " + kind[2] + " names found."
                        : bound.ToString(CultureInfo.InvariantCulture) + " " + kind[2] + "(s) bound."
                );
            }
        }

        /// <summary>The names of the records of a table that an external reference brought, in ordinal order.</summary>
        private static List<string> DependentNames(CadCommandContext c, string table)
        {
            IEnumerable<DxfSymbolTableRecord> records =
                table == "LAYER" ? c.Database.LayerTable.Cast<DxfSymbolTableRecord>()
                : table == "LTYPE" ? c.Database.LinetypeTable.Cast<DxfSymbolTableRecord>()
                : table == "STYLE" ? c.Database.TextStyleTable.Cast<DxfSymbolTableRecord>()
                : c.Database.DimStyleTable.Cast<DxfSymbolTableRecord>();
            return records
                .Where(r => r.IsDependent && !string.IsNullOrEmpty(r.Name))
                .Select(r => r.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ThenBy(n => n, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Binds the layers, linetypes, text styles or dimension styles of a table that the patterns name; how many.</summary>
        private static int BindSymbols(CadCommandContext c, string table, string[] patterns)
        {
            List<string> matched = DependentNames(c, table)
                .Where(n => patterns.Any(p => CadWildcard.IsMatch(n, p, true)))
                .ToList();

            if (matched.Count == 0)
            {
                return 0;
            }

            int bound = 0;
            CadEditResult result = c.Apply(
                new[]
                {
                    new CadEditRecord(CadEditRecordKind.TableChanged, null, "XBIND", string.Join(",", matched), null),
                },
                s => bound = s.Database.BindDependentSymbols(table, matched)
            );
            return result.Succeeded ? bound : 0;
        }

        /// <summary>
        /// Binds the blocks of the drawings the external references name, <c>grid|BLK</c> for the block BLK of the drawing grid
        /// references, as the blocks <c>grid$0$BLK</c> holding a copy of what it draws; a block of that name the drawing was
        /// left, by AutoCAD, as a record of the reference goes. How many.
        /// </summary>
        private static int BindBlocks(CadCommandContext c, string[] patterns)
        {
            var plan = new List<Tuple<string, DxfBlockTableRecord, DxfDatabase>>();
            CadXrefManager xrefs = c.Document.Xrefs;

            foreach (
                DxfBlockTableRecord xref in c.Database.BlockTable.Where(b => b.IsFromExternalReference && !b.IsLayout)
            )
            {
                string found = xrefs.Locate(xref.PathName, c.Document.DrawingFolder);
                DxfDatabase source = found == null ? null : xrefs.Read(found).Database;

                if (source == null)
                {
                    continue;
                }

                foreach (DxfBlockTableRecord block in source.BlockTable)
                {
                    string name = xref.Name + "|" + block.Name;

                    if (
                        !block.IsLayout
                        && !block.IsAnonymous
                        && !block.Name.StartsWith("*", StringComparison.Ordinal)
                        && !block.IsFromExternalReference
                        && !block.IsDependent
                        && patterns.Any(p => CadWildcard.IsMatch(name, p, true))
                    )
                    {
                        plan.Add(Tuple.Create(name, block, source));
                    }
                }
            }

            if (plan.Count == 0)
            {
                return 0;
            }

            plan = plan.OrderBy(p => p.Item1, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Item1, StringComparer.Ordinal)
                .ToList();
            int bound = 0;
            CadEditResult result = c.Apply(
                new[]
                {
                    new CadEditRecord(
                        CadEditRecordKind.TableChanged,
                        null,
                        "XBIND",
                        string.Join(",", plan.Select(p => p.Item1)),
                        null
                    ),
                },
                s =>
                {
                    foreach (Tuple<string, DxfBlockTableRecord, DxfDatabase> step in plan)
                    {
                        string name = null;

                        for (int i = 0; name == null; i++)
                        {
                            string candidate = step.Item1.Replace(
                                "|",
                                "$" + i.ToString(CultureInfo.InvariantCulture) + "$"
                            );
                            name = s.Database.BlockTable.Has(candidate) ? null : candidate;
                        }

                        // What AutoCAD left of the reference's block, its record with a | in its name, goes with the binding.
                        if (s.Database.BlockTable.Has(step.Item1))
                        {
                            s.Database.BlockTable[step.Item1].Remove();
                        }

                        var made = new DxfBlockTableRecord { Name = name, Origin = step.Item2.Origin };
                        s.Database.BlockTable.Add(made);
                        step.Item3.WblockCloneObjects(new List<DxfEntity>(step.Item2), made);
                        bound++;
                    }
                }
            );
            return result.Succeeded ? bound : 0;
        }

        #endregion

        #region SAVE

        /// <summary>
        /// SAVE: the window's Save Drawing As dialog; with FILEDIA 0 or no window, "Save drawing as &lt;file&gt;:" and, where the
        /// file is there already, "Do you want to replace it? &lt;N&gt;", as AutoCAD 2021 asks them. The drawing is known by
        /// the new name afterwards, as after SAVEAS: a name ending in .dwg is saved as a DWG of AutoCAD 2018, any other as a DXF
        /// of the drawing's own version.
        /// </summary>
        private static IEnumerable<CadPrompt> Save(CadCommandContext c)
        {
            if (c.Session.FileDialogs && c.Session.Request("SAVEAS"))
            {
                yield break;
            }

            string offered = c.Document.FileName ?? c.Document.Title;
            bool wasDwg = CadDrawingFile.IsDwgName(offered);
            yield return CadPrompt.ForString("Save drawing as", offered);
            string file = Typed(c, offered).Trim('"');

            if (file.Length == 0)
            {
                yield break;
            }

            bool dxf =
                file.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase)
                || (!wasDwg && !file.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase));

            if (!file.EndsWith(dxf ? ".dxf" : ".dwg", StringComparison.OrdinalIgnoreCase))
            {
                file += dxf ? ".dxf" : ".dwg";
            }

            if (File.Exists(file))
            {
                c.Write("A drawing with this name already exists.");
                yield return CadPrompt.ForString("Do you want to replace it?", "N");

                if (!Typed(c, "N").StartsWith("Y", StringComparison.OrdinalIgnoreCase))
                {
                    yield break;
                }
            }

            string own = c.Document.SaveVersion ?? c.Document.Database.Version;
            string version = dxf ? (wasDwg ? "AC1032" : DxfDefault(own)) : null;
            var job = new CadExportJob(
                "Save Drawing As",
                new[] { CadExportJob.Dwg }.Concat(CadExportJob.DxfSaveTypes).ToList(),
                dxf ? CadExportJob.DxfTypeOfVersion(version) : CadExportJob.Dwg,
                file
            );

            if (c.Session.Request("SAVEAS", c.Document, job))
            {
                yield break;
            }

            try
            {
                c.Document.SaveAs(file, version, null);
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                )
            {
                c.Write("Cannot save " + file + ": " + exception.Message);
                yield break;
            }

            c.Write("Saved " + c.Document.FileName + ".");
            SayLeftOut(c);
        }

        #endregion
    }
}

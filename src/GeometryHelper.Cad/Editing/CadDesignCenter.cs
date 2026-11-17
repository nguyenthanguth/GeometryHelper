using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>What <see cref="CadDesignCenter.Add"/> did: the names brought in, those the drawing kept as they were, and what went wrong.</summary>
    public sealed class CadDesignCenterResult
    {
        internal CadDesignCenterResult(
            IReadOnlyList<string> added,
            IReadOnlyList<string> kept,
            IReadOnlyList<string> failed
        )
        {
            Added = added;
            Kept = kept;
            Failed = failed;
        }

        /// <summary>Gets the names brought into the drawing, as the drawing has them now: a layout that took another name is listed by it.</summary>
        public IReadOnlyList<string> Added { get; }

        /// <summary>Gets the names the drawing had already and keeps as they are, as AutoCAD's DesignCenter keeps them.</summary>
        public IReadOnlyList<string> Kept { get; }

        /// <summary>Gets what went wrong, a sentence for each name that could not be brought in.</summary>
        public IReadOnlyList<string> Failed { get; }

        /// <summary>Gets whether nothing went wrong.</summary>
        public bool Succeeded => Failed.Count == 0;
    }

    /// <summary>
    /// What DesignCenter does with the content of another drawing: layers, linetypes, text, dimension, multileader and table
    /// styles, blocks and layouts copied into the drawing open, a thing the drawing has by the name already kept as it is (the
    /// layer WALL of the drawing keeps its colour, whatever the other drawing's WALL has). Each call is one undo step.
    /// A block is inserted, and an external reference attached, by the commands that place them; the window starts those.
    /// </summary>
    public static class CadDesignCenter
    {
        /// <summary>
        /// Copies named items of a kind from a drawing into the document: a layer with its colour, linetype (copied too where
        /// the document has none), lineweight, transparency, state and description; a linetype with its dashes, texts and
        /// shapes; a text style with its font; a dimension, multileader or table style with the text styles, linetypes and
        /// blocks it draws by; a block definition with what it is drawn from; a layout with its viewports and objects, taking
        /// another name where the document has one of that name (<c>Layout3-Plan</c>), as the template command names it.
        /// </summary>
        /// <param name="document">The document the items come into.</param>
        /// <param name="from">The drawing they come from.</param>
        /// <param name="kind">The kind; Xrefs are attached, not copied, and are refused.</param>
        /// <param name="names">The names of the items, as <see cref="CadDesignCenterDrawing.Items"/> lists them.</param>
        /// <returns>What was added, kept and what failed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document, the drawing or the names are null.</exception>
        public static CadDesignCenterResult Add(
            CadDocument document,
            CadDesignCenterDrawing from,
            CadDesignCenterKind kind,
            IEnumerable<string> names
        )
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (from == null)
            {
                throw new ArgumentNullException(nameof(from));
            }

            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }

            var added = new List<string>();
            var kept = new List<string>();
            var failed = new List<string>();
            List<string> wanted = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (kind == CadDesignCenterKind.Xrefs)
            {
                failed.Add("An external reference is attached, not copied: Attach as Xref places it.");
                return new CadDesignCenterResult(added, kept, failed);
            }

            document.BeginGroup("ADCENTER");

            try
            {
                foreach (string name in wanted)
                {
                    DxfDatabase target = document.Database;

                    if (kind != CadDesignCenterKind.Layouts && Has(target, kind, name))
                    {
                        kept.Add(name);
                        continue;
                    }

                    if (!Has(from.Database, kind, name))
                    {
                        failed.Add("The drawing " + from.Name + " has no " + Noun(kind) + " \"" + name + "\".");
                        continue;
                    }

                    CadEditResult result = Copy(document, from, kind, name, added);

                    if (!result.Succeeded)
                    {
                        failed.Add("Cannot add " + Noun(kind) + " \"" + name + "\": " + result.Message);
                    }
                }
            }
            finally
            {
                document.EndGroup();
            }

            GeometryHelperLog.Info(
                "CadDesignCenter: "
                    + added.Count
                    + " "
                    + Noun(kind)
                    + "(s) added from "
                    + from.Name
                    + ", "
                    + kept.Count
                    + " kept, "
                    + failed.Count
                    + " failed."
            );
            return new CadDesignCenterResult(added, kept, failed);
        }

        private static bool Has(DxfDatabase database, CadDesignCenterKind kind, string name)
        {
            switch (kind)
            {
                case CadDesignCenterKind.Blocks:
                    return database.BlockTable.Has(name) && !database.BlockTable[name].IsLayout;
                case CadDesignCenterKind.Dimstyles:
                    return database.DimStyleTable.Has(name);
                case CadDesignCenterKind.Layers:
                    return database.LayerTable.Has(name);
                case CadDesignCenterKind.Layouts:
                    return database.Layouts.Any(l =>
                        l.BlockTableRecord != null
                        && string.Equals(l.LayoutName, name, StringComparison.OrdinalIgnoreCase)
                    );
                case CadDesignCenterKind.Linetypes:
                    return database.LinetypeTable.Has(name);
                case CadDesignCenterKind.Multileaderstyles:
                    return CadMLeaderStyleEditor.Find(database, name) != null;
                case CadDesignCenterKind.Tablestyles:
                    return CadTableStyleEditor.Find(database, name) != null;
                default:
                    return database.TextStyleTable.Has(name);
            }
        }

        private static string Noun(CadDesignCenterKind kind)
        {
            switch (kind)
            {
                case CadDesignCenterKind.Blocks:
                    return "block";
                case CadDesignCenterKind.Dimstyles:
                    return "dimension style";
                case CadDesignCenterKind.Layers:
                    return "layer";
                case CadDesignCenterKind.Layouts:
                    return "layout";
                case CadDesignCenterKind.Linetypes:
                    return "linetype";
                case CadDesignCenterKind.Multileaderstyles:
                    return "multileader style";
                case CadDesignCenterKind.Tablestyles:
                    return "table style";
                case CadDesignCenterKind.Textstyles:
                    return "text style";
                default:
                    return "external reference";
            }
        }

        private static CadEditResult Copy(
            CadDocument document,
            CadDesignCenterDrawing from,
            CadDesignCenterKind kind,
            string name,
            List<string> added
        )
        {
            DxfDatabase source = from.Database;
            CadEditResult result;

            switch (kind)
            {
                case CadDesignCenterKind.Layers:
                    result = document.ApplyEdit("ADCENTER", Table("Layer", name), s =>
                        CopyLayer(s.Database, source, name)
                    );
                    break;
                case CadDesignCenterKind.Linetypes:
                    result = document.ApplyEdit("ADCENTER", Table("Linetype", name), s =>
                        CadSymbolCopy.Linetype(s.Database, source, name)
                    );
                    break;
                case CadDesignCenterKind.Textstyles:
                    result = document.ApplyEdit("ADCENTER", Table("Text style", name), s =>
                        CadSymbolCopy.TextStyle(s.Database, source, name)
                    );
                    break;
                case CadDesignCenterKind.Blocks:
                    DxfDatabase working = from.WorkingCopy();
                    result = document.ApplyEdit("ADCENTER", Table("Block", name), s =>
                        CadSymbolCopy.Block(s.Database, working, name)
                    );
                    break;
                case CadDesignCenterKind.Layouts:
                    DxfLayout layout = source.Layouts.First(l =>
                        l.BlockTableRecord != null
                        && string.Equals(l.LayoutName, name, StringComparison.OrdinalIgnoreCase)
                    );
                    string made = null;
                    result = document.ApplyEdit("ADCENTER", Table("Layout", name), s =>
                    {
                        made = CadCommands.TemplateLayoutName(
                            CadCommands.LayoutNames(s.Database).ToList(),
                            layout.LayoutName
                        );
                        CadCommands.ImportLayout(s.Database, source, layout, made);
                    });

                    if (result.Succeeded)
                    {
                        added.Add(made);
                    }

                    return result;
                case CadDesignCenterKind.Dimstyles:
                    result = CopyDimStyle(document, from, name);
                    break;
                case CadDesignCenterKind.Multileaderstyles:
                    result = CopyMLeaderStyle(document, from, name);
                    break;
                default:
                    result = CopyTableStyle(document, from, name);
                    break;
            }

            if (result.Succeeded)
            {
                added.Add(name);
            }

            return result;
        }

        private static CadEditRecord[] Table(string what, string name) =>
            new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Add " + what, null, name) };

        /// <summary>A layer made in the target of the source layer's settings, its linetype copied where the target has none.</summary>
        private static void CopyLayer(DxfDatabase target, DxfDatabase source, string name)
        {
            DxfLayerTableRecord from = source.LayerTable[name];
            string linetype = from.LinetypeName ?? "Continuous";

            if (!target.LinetypeTable.Has(linetype) && !CadSymbolCopy.Linetype(target, source, linetype))
            {
                linetype = "Continuous";
            }

            target.LayerTable.Add(new DxfLayerTableRecord { Name = from.Name, ColorIndex = 7 });
            DxfLayerTableRecord made = target.LayerTable[from.Name];
            // A drawing older than AutoCAD 2004 has no true colours and no transparency: they are left out of the copy.
            bool modern = target.Version == null || string.CompareOrdinal(target.Version, "AC1018") >= 0;

            if (modern || !from.Color.IsByColor)
            {
                made.Color = from.Color;
            }

            made.LinetypeName = target.LinetypeTable[linetype].Name;
            made.LineWeight = from.LineWeight;
            made.IsOff = from.IsOff;
            made.IsFrozen = from.IsFrozen;
            made.IsLocked = from.IsLocked;
            made.IsPlottable = from.IsPlottable;

            if (!string.IsNullOrEmpty(from.Description))
            {
                made.Description = from.Description;
            }

            if (modern && from.Transparency.IsByAlpha && !from.Transparency.IsSolid)
            {
                made.Transparency = from.Transparency;
            }
        }

        /// <summary>The text styles, linetypes and blocks a set of names points to, copied into the document where it has none: what a style draws by comes with it.</summary>
        private static void BringNeeded(
            CadDocument document,
            CadDesignCenterDrawing from,
            IEnumerable<string> textStyles,
            IEnumerable<string> linetypes,
            IEnumerable<string> blocks
        )
        {
            DxfDatabase source = from.Database;
            DxfDatabase target = document.Database;

            foreach (
                string style in textStyles.Where(n =>
                    !string.IsNullOrEmpty(n) && !target.TextStyleTable.Has(n) && source.TextStyleTable.Has(n)
                )
            )
            {
                document.ApplyEdit("ADCENTER", Table("Text style", style), s =>
                    CadSymbolCopy.TextStyle(s.Database, source, style)
                );
            }

            foreach (
                string linetype in linetypes.Where(n =>
                    !string.IsNullOrEmpty(n) && !target.LinetypeTable.Has(n) && source.LinetypeTable.Has(n)
                )
            )
            {
                document.ApplyEdit("ADCENTER", Table("Linetype", linetype), s =>
                    CadSymbolCopy.Linetype(s.Database, source, linetype)
                );
            }

            foreach (
                string block in blocks.Where(n =>
                    !string.IsNullOrEmpty(n) && !target.BlockTable.Has(n) && source.BlockTable.Has(n)
                )
            )
            {
                DxfDatabase working = from.WorkingCopy();
                document.ApplyEdit("ADCENTER", Table("Block", block), s =>
                    CadSymbolCopy.Block(s.Database, working, block)
                );
            }
        }

        private static CadEditResult CopyDimStyle(CadDocument document, CadDesignCenterDrawing from, string name)
        {
            SortedDictionary<string, object> values = CadDimStyleEditor.Read(from.Database.DimStyleTable[name]);
            string Text(string key) => values.TryGetValue(key, out object value) ? value as string : null;

            BringNeeded(
                document,
                from,
                new[] { Text("Dimtxsty") },
                new[] { Text("Dimltype"), Text("Dimltex1"), Text("Dimltex2") },
                new[] { Text("Dimblk"), Text("Dimblk1"), Text("Dimblk2"), Text("Dimldrblk") }
            );
            string start = CadDimStyleEditor.Current(document.Database);
            return CadDimStyleEditor.Add(document, from.Database.DimStyleTable[name].Name, start, values);
        }

        private static CadEditResult CopyMLeaderStyle(CadDocument document, CadDesignCenterDrawing from, string name)
        {
            CadMLeaderStyleSettings settings = CadMLeaderStyleSettings.Of(
                CadMLeaderStyleEditor.Find(from.Database, name)
            );
            BringNeeded(
                document,
                from,
                new[] { settings.TextStyle },
                new[] { settings.LeaderLinetype },
                new[] { settings.BlockName }
            );
            CadEditResult made = CadMLeaderStyleEditor.Add(document, name, null);
            return made.Succeeded ? CadMLeaderStyleEditor.Apply(document, name, settings) : made;
        }

        private static CadEditResult CopyTableStyle(CadDocument document, CadDesignCenterDrawing from, string name)
        {
            CadTableStyleSettings settings = CadTableStyleSettings.Of(CadTableStyleEditor.Find(from.Database, name));
            BringNeeded(
                document,
                from,
                new[] { settings.Title.TextStyle, settings.Header.TextStyle, settings.Data.TextStyle },
                Array.Empty<string>(),
                Array.Empty<string>()
            );
            CadEditResult made = CadTableStyleEditor.Add(document, name, null);
            return made.Succeeded ? CadTableStyleEditor.Apply(document, name, settings) : made;
        }
    }
}

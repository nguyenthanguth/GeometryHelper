using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// BATTMAN, AutoCAD's Block Attribute Manager: the attribute definitions of a block listed, moved up and down, edited and
    /// removed in the window's dialog, then written into the block on Apply or OK, each one undo step; Sync writes them and
    /// brings every INSERT of the block in line as ATTSYNC does. AutoCAD has no command-line form of it, and neither has this.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// BATTMAN: the manager on the block of an INSERT selected first, else on the first block with attributes; Select
        /// block asks "Select a block:" and shows the manager again on the block picked. OK writes what is left to write.
        /// </summary>
        private static IEnumerable<CadPrompt> AttributeManager(CadCommandContext c)
        {
            List<string> blocks = AttributedBlocks(c.Database);

            if (blocks.Count == 0)
            {
                c.Write("This drawing contains no attributed blocks.");
                yield break;
            }

            string name = blocks[0];

            if (
                c.PreSelected.Count > 0
                && c.Document.FindEntity(c.PreSelected[0]) is DxfBlockReference first
                && first.BlockTableRecord != null
                && blocks.Contains(first.BlockTableRecord.Name)
            )
            {
                name = first.BlockTableRecord.Name;
            }

            c.ClearsSelection = c.PreSelected.Count > 0;

            while (true)
            {
                var request = new CadAttributeManagerRequest(c.Document, c.Session.AttributeManager, blocks);
                Load(c, request, name);
                request.Applying = sync => ApplyDefinitions(c, request, sync);
                request.Showing = block =>
                {
                    ApplyDefinitions(c, request, request.Settings.ApplyToExistingReferences);

                    if (blocks.Contains(block))
                    {
                        Load(c, request, block);
                    }
                };

                request.Action = CadDialogAction.Cancel;
                bool asked = c.Session.Request("BATTMAN", c.Document, request);
                request.Applying = null;
                request.Showing = null;

                if (!asked)
                {
                    c.Write("BATTMAN is a dialog, and there is no window to show it in: use ATTSYNC and -ATTEDIT.");
                    yield break;
                }

                if (request.Action == CadDialogAction.Ok)
                {
                    ApplyDefinitions(c, request, request.Settings.ApplyToExistingReferences);
                    yield break;
                }

                if (request.Action != CadDialogAction.SelectBlock)
                {
                    yield break;
                }

                // Select block: what was changed is applied, then the block picked is shown.
                ApplyDefinitions(c, request, request.Settings.ApplyToExistingReferences);
                name = request.Block;

                while (true)
                {
                    yield return CadPrompt.ForEntity("Select a block");

                    if (c.Input.Kind != CadInputKind.Selection)
                    {
                        break;
                    }

                    if (
                        c.Document.FindEntity(c.Input.Handles[0]) is DxfBlockReference picked
                        && picked.BlockTableRecord != null
                        && blocks.Contains(picked.BlockTableRecord.Name)
                    )
                    {
                        name = picked.BlockTableRecord.Name;
                        break;
                    }

                    c.Write("That object is not a block with attributes.");
                }
            }
        }

        /// <summary>The blocks a user names that hold attribute definitions, in ordinal order of their names.</summary>
        private static List<string> AttributedBlocks(DxfDatabase database) =>
            NamedBlocks(database).Where(b => b.OfType<DxfAttributeDefinition>().Any()).Select(b => b.Name).ToList();

        /// <summary>The rows of a block's attribute definitions read into a request, with the INSERTs of the block counted.</summary>
        private static void Load(CadCommandContext c, CadAttributeManagerRequest request, string name)
        {
            DxfBlockTableRecord record = c.Database.BlockTable[name];
            request.Block = record.Name;
            request.Rows.Clear();

            foreach (DxfAttributeDefinition definition in record.OfType<DxfAttributeDefinition>())
            {
                request.Rows.Add(RowOf(definition));
            }

            List<KeyValuePair<DxfBlockTableRecord, DxfBlockReference>> inserts = InsertsOf(c.Database, record.Name);
            request.FoundInDrawing = inserts.Count;
            request.FoundInModelSpace = inserts.Count(p =>
                string.Equals(p.Key.Name, DxfBlockTableRecord.ModelSpace, StringComparison.OrdinalIgnoreCase)
            );
        }

        /// <summary>A row of the manager of an attribute definition as the drawing has it.</summary>
        private static CadAttributeDefinitionRow RowOf(DxfAttributeDefinition definition) =>
            new CadAttributeDefinitionRow
            {
                Handle = definition.Handle,
                Tag = definition.Tag,
                Prompt = definition.Prompt ?? string.Empty,
                Default = definition.TextString ?? string.Empty,
                Invisible = definition.Invisible,
                Constant = definition.Constant,
                Verify = definition.Verifiable,
                Preset = definition.Preset,
                LockPosition = definition.LockPositionInBlock,
                MultipleLines = definition.IsMTextAttributeDefinition,
                TextStyle = definition.TextStyleName,
                Justification = definition.Justify,
                Height = definition.Height,
                Rotation = definition.Rotation,
                Backwards = definition.IsMirroredInX,
                UpsideDown = definition.IsMirroredInY,
                WidthFactor = definition.WidthFactor,
                Oblique = definition.Oblique,
                BoundaryWidth = definition.IsMTextAttributeDefinition
                    ? definition.MTextAttributeDefinition?.Width ?? 0.0
                    : 0.0,
                Layer = definition.Layer,
                Linetype = definition.Linetype,
                ColorIndex =
                    definition.Color.IsByLayer ? 256
                    : definition.Color.IsByBlock ? 0
                    : definition.Color.ColorIndex,
                LineWeight = (int)definition.LineWeight,
            };

        /// <summary>The text options and properties of a row as a row of the Enhanced Attribute Editor, which the editor's own writing writes.</summary>
        private static CadAttributeRow TextOf(CadAttributeDefinitionRow row) =>
            new CadAttributeRow
            {
                Value = row.Default,
                TextStyle = row.TextStyle,
                Justification = row.Justification,
                Height = row.Height,
                Rotation = row.Rotation,
                Backwards = row.Backwards,
                UpsideDown = row.UpsideDown,
                WidthFactor = row.WidthFactor,
                Oblique = row.Oblique,
                Layer = row.Layer,
                Linetype = row.Linetype,
                ColorIndex = row.ColorIndex,
                LineWeight = row.LineWeight,
            };

        private static bool SameDefinition(CadAttributeDefinitionRow a, CadAttributeDefinitionRow b) =>
            a.Tag == b.Tag
            && a.Prompt == b.Prompt
            && a.Invisible == b.Invisible
            && a.Verify == b.Verify
            && a.Preset == b.Preset
            && a.LockPosition == b.LockPosition
            && Same(TextOf(a), TextOf(b));

        /// <summary>
        /// Writes the rows of a request into its block, one undo step: each definition changed set as its row says, those of
        /// no row erased, and the definitions put in the order of the rows; then, where <paramref name="sync"/> says so, every
        /// INSERT of the block brought in line as ATTSYNC brings it. The rows are read again. Nothing is written where nothing
        /// changed and nothing is to be synchronized; a tag not of one word is refused, said why.
        /// </summary>
        private static void ApplyDefinitions(CadCommandContext c, CadAttributeManagerRequest request, bool sync)
        {
            string name = request.Block;
            DxfBlockTableRecord record = c.Database.BlockTable[name];
            List<DxfAttributeDefinition> definitions = record.OfType<DxfAttributeDefinition>().ToList();
            List<CadAttributeDefinitionRow> rows = request.Rows.ToList();
            var changed = new List<KeyValuePair<CadAttributeDefinitionRow, CadAttributeDefinitionRow>>();

            foreach (CadAttributeDefinitionRow row in rows)
            {
                DxfAttributeDefinition definition = definitions.FirstOrDefault(d =>
                    string.Equals(d.Handle, row.Handle, StringComparison.OrdinalIgnoreCase)
                );

                if (definition == null)
                {
                    continue;
                }

                CadAttributeDefinitionRow old = RowOf(definition);

                if (!SameDefinition(old, row))
                {
                    changed.Add(new KeyValuePair<CadAttributeDefinitionRow, CadAttributeDefinitionRow>(old, row));
                }
            }

            List<string> kept = rows.Select(r => r.Handle)
                .Where(h => definitions.Exists(d => string.Equals(d.Handle, h, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            bool removed = kept.Count < definitions.Count;
            bool reordered = !kept.SequenceEqual(
                definitions.Select(d => d.Handle).Where(h => kept.Contains(h, StringComparer.OrdinalIgnoreCase)),
                StringComparer.OrdinalIgnoreCase
            );
            bool written = changed.Count > 0 || removed || reordered;

            if (changed.Any(p => !IsTag(p.Value.Tag)))
            {
                c.Write("Invalid tag: a tag is one word, with no space.");
                return;
            }

            if (changed.Any(p => !(p.Value.Height > 0.0) || !(p.Value.WidthFactor > 0.0)))
            {
                c.Write("Value must be positive and nonzero.");
                return;
            }

            if (written)
            {
                CadEditResult result = c.Apply(
                    new[]
                    {
                        new CadEditRecord(CadEditRecordKind.TableChanged, null, "Attribute definitions", null, name),
                    },
                    s =>
                    {
                        DxfBlockTableRecord block = s.Database.BlockTable[name];
                        List<DxfAttributeDefinition> now = block.OfType<DxfAttributeDefinition>().ToList();
                        DxfAttributeDefinition Find(string handle) =>
                            now.First(d => string.Equals(d.Handle, handle, StringComparison.OrdinalIgnoreCase));

                        foreach (KeyValuePair<CadAttributeDefinitionRow, CadAttributeDefinitionRow> pair in changed)
                        {
                            WriteDefinition(Find(pair.Value.Handle), pair.Key, pair.Value, s.Database);
                        }

                        foreach (
                            DxfAttributeDefinition definition in now.Where(d =>
                                !kept.Contains(d.Handle, StringComparer.OrdinalIgnoreCase)
                            )
                        )
                        {
                            definition.Erase();
                        }

                        if (reordered)
                        {
                            // The definitions written again at the end of the block in the order of the rows, as AutoCAD's Move Up
                            // and Move Down leave them; the INSERTs follow that order once synchronized.
                            List<DxfAttributeDefinition> order = kept.Select(Find).ToList();

                            foreach (DxfAttributeDefinition definition in order)
                            {
                                block.AppendEntity(definition.Clone());
                            }

                            order.ForEach(d => d.Erase());
                        }
                    }
                );

                if (!result.Succeeded)
                {
                    return;
                }
            }

            if (sync && (written || InsertsOf(c.Database, name).Count > 0))
            {
                SyncAttributes(c, name);
            }

            c.CloseUndoStep();

            if (c.Database.BlockTable.Has(name))
            {
                Load(c, request, name);
            }
        }

        /// <summary>Sets on an attribute definition each value of a row that differs from what it was.</summary>
        private static void WriteDefinition(
            DxfAttributeDefinition definition,
            CadAttributeDefinitionRow old,
            CadAttributeDefinitionRow row,
            DxfDatabase database
        )
        {
            if (row.Tag != old.Tag)
            {
                definition.Tag = row.Tag.ToUpperInvariant();
            }

            if (row.Prompt != old.Prompt)
            {
                definition.Prompt = (row.Prompt ?? string.Empty).Replace("\r", string.Empty).Replace("\n", " ");
            }

            if (row.Invisible != old.Invisible)
            {
                definition.Invisible = row.Invisible;
            }

            if (row.Verify != old.Verify)
            {
                definition.Verifiable = row.Verify;
            }

            if (row.Preset != old.Preset)
            {
                definition.Preset = row.Preset;
            }

            if (row.LockPosition != old.LockPosition)
            {
                definition.LockPositionInBlock = row.LockPosition;
            }

            Write(definition, TextOf(old), TextOf(row), database);
        }
    }
}

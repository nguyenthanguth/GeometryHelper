using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Cad.Remote;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>The text styles, dimension styles and blocks of a drawing, read from outside.</summary>
    /// <remarks>
    /// A table record travels in the small drawing the objects travel in (<see cref="RemoteFragment"/>), copied into it as the
    /// library copies the records an object brings, with the records it points at: a dimension style comes with its text style,
    /// its linetypes and its arrowhead blocks. The answer names the records wanted, in order, as the fragment holds more
    /// (the Standard and ISO-25 of every new drawing, which the drawing's own take the place of).
    /// </remarks>
    public sealed partial class CadRemoteServer
    {
        private static JsonObject TextStyles(CadDocument document)
        {
            DxfDatabase source = document.Database;
            DxfDatabase fragment = new DxfDatabase(RemoteFragment.Version);
            List<string> names = Sorted(source.TextStyleTable.Select(s => s.Name));
            Carry(fragment, source, "STYLE", fragment.TextStyleTable, names);
            return Records(fragment, names);
        }

        private static JsonObject DimStyles(CadDocument document)
        {
            DxfDatabase source = document.Database;
            DxfDatabase fragment = new DxfDatabase(RemoteFragment.Version);
            List<DxfDimStyleTableRecord> styles = source
                .DimStyleTable.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Name, StringComparer.Ordinal)
                .ToList();

            // What the styles point at comes first, so that the copies point at copies: the text styles and linetypes they name,
            // and the blocks they draw their arrowheads by.
            Carry(
                fragment,
                source,
                "STYLE",
                fragment.TextStyleTable,
                Sorted(styles.Select(s => s.Dimtxsty?.Name).Where(n => n != null))
            );
            Carry(
                fragment,
                source,
                "LTYPE",
                fragment.LinetypeTable,
                Sorted(
                    styles
                        .SelectMany(s => new DxfLinetypeTableRecord[] { s.Dimltype, s.Dimltex1, s.Dimltex2 })
                        .Where(l => l != null)
                        .Select(l => l.Name)
                )
            );
            CarryBlocks(
                fragment,
                source,
                styles
                    .SelectMany(s => new DxfBlockTableRecord[] { s.Dimblk, s.Dimblk1, s.Dimblk2, s.Dimldrblk })
                    .Where(b => b != null && !b.IsDependent)
                    .GroupBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(b => b.Name, StringComparer.Ordinal)
                    .ToList()
            );

            List<string> names = styles.Select(s => s.Name).ToList();
            Carry(fragment, source, "DIMSTYLE", fragment.DimStyleTable, names);
            return Records(fragment, names);
        }

        private static JsonObject Blocks(CadDocument document)
        {
            DxfDatabase source = document.Database;
            DxfDatabase fragment = new DxfDatabase(RemoteFragment.Version);
            List<DxfBlockTableRecord> blocks = source
                .BlockTable.Where(b =>
                    !b.IsLayout && !b.IsAnonymous && !b.IsDependent && !b.Name.StartsWith("*", StringComparison.Ordinal)
                )
                .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(b => b.Name, StringComparer.Ordinal)
                .ToList();
            CarryBlocks(fragment, source, blocks);
            return Records(fragment, blocks.Select(b => b.Name).ToList());
        }

        private static JsonObject Records(DxfDatabase fragment, List<string> names)
        {
            var list = new JsonArray();

            foreach (string name in names)
            {
                list.Add(name);
            }

            return new JsonObject().Set("fragment", fragment.ToText()).Set("names", list);
        }

        /// <summary>Names sorted by case-blind order, then by ordinal order, each once.</summary>
        private static List<string> Sorted(IEnumerable<string> names) =>
            names
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ThenBy(n => n, StringComparer.Ordinal)
                .ToList();

        /// <summary>
        /// Copies the records of a table by name into the small drawing. A record the small drawing has of the name, one of
        /// the two it is made with, is taken out first, as the copy keeps the one a drawing has.
        /// </summary>
        private static void Carry(
            DxfDatabase fragment,
            DxfDatabase source,
            string table,
            DxfSymbolTable into,
            List<string> names
        )
        {
            foreach (string name in names)
            {
                if (into.Has(name))
                {
                    fragment.File.RemoveTableRecord(into[name].Item, table);
                }
            }

            fragment.File.CopyTableRecords(source.File, table, names);
        }

        /// <summary>
        /// Makes blocks in the small drawing, as <see cref="DxfDatabase.Insert(GeometryHelper.Geometry.GeoTransform3, DxfDatabase, bool)"/>
        /// merges them: every record first, then the objects of each, so that an INSERT of one block in another places the
        /// block the small drawing has of that name. An external reference is carried as its name and path alone.
        /// </summary>
        private static void CarryBlocks(DxfDatabase fragment, DxfDatabase source, List<DxfBlockTableRecord> blocks)
        {
            var made = new List<DxfBlockTableRecord>();

            foreach (DxfBlockTableRecord block in blocks)
            {
                if (fragment.BlockTable.Has(block.Name))
                {
                    continue;
                }

                var record = new DxfBlockTableRecord
                {
                    Name = block.Name,
                    Origin = block.Origin,
                    MadeFlags =
                        (block.HasAttributeDefinitions ? 2 : 0)
                        | (block.IsFromExternalReference ? 4 : 0)
                        | (block.IsFromOverlayReference ? 8 : 0),
                };

                if (block.IsFromExternalReference || block.IsFromOverlayReference)
                {
                    record.PathName = block.PathName;
                }
                else
                {
                    record.Comments = block.Comments;
                    record.Units = block.Units;
                    record.Explodable = block.Explodable;
                    record.BlockScaling = block.BlockScaling;
                }

                fragment.BlockTable.Add(record);

                if (block.Annotative == DxfAnnotativeStates.True)
                {
                    record.XData = block.GetXDataForApplication("AcadAnnotative");
                }

                if (!block.IsFromExternalReference && !block.IsFromOverlayReference)
                {
                    made.Add(block);
                }
            }

            foreach (DxfBlockTableRecord block in made)
            {
                var objects = new List<DxfEntity>(block);

                if (objects.Count > 0)
                {
                    source.WblockCloneObjects(objects, fragment.BlockTable[block.Name]);
                }
            }
        }
    }
}

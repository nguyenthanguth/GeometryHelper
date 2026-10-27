using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>The external references of the drawing, as AutoCAD's <c>Database</c> attaches, overlays and detaches them.</summary>
    public sealed partial class DxfDatabase
    {
        // The tables whose records an external reference brings, named after it with a | between.
        private static readonly string[] DependentTables = { "LAYER", "LTYPE", "STYLE", "DIMSTYLE" };

        /// <summary>
        /// Attaches another drawing as an external reference, as AutoCAD's <c>AttachXref</c>: a block of the name given whose
        /// BLOCK is flagged an external reference (70 of 4) and names the drawing's path (its 1), with its BLOCK_RECORD from R13
        /// on, and no entities: AutoCAD reads the drawing into it when it opens this one. An INSERT of the block places it, as
        /// <c>new DxfBlockReference(position, xref)</c> appended to model space; until then the block is
        /// <see cref="DxfXrefStatus.Unreferenced"/>.
        /// </summary>
        /// <remarks>
        /// The drawing is not read: the block's origin is the origin, and its <see cref="DxfBlockTableRecord.XrefStatus"/> says
        /// whether the path names a file. <c>db.AttachXref("column.dwg", "COLUMN")</c> writes the BLOCK <c>2 COLUMN, 70 4,
        /// 3 COLUMN, 1 column.dwg</c>.
        /// </remarks>
        /// <param name="fileName">The path of the drawing, as it is to be written: absolute, or from the folder of this one.</param>
        /// <param name="blockName">The name of the block.</param>
        /// <returns>The block.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path or the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the path is empty or has a line break in it, or the name is one AutoCAD
        /// does not take or that of a block of the drawing already, letters of either case the same.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing has no BLOCKS section.</exception>
        public DxfBlockTableRecord AttachXref(string fileName, string blockName) => Xref(fileName, blockName, 4);

        /// <summary>
        /// Overlays another drawing as an external reference, as AutoCAD's <c>OverlayXref</c>: as <see cref="AttachXref"/>
        /// attaches one, its BLOCK flagged an overlay as well (70 of 12), so that a drawing that references this one does not
        /// see it.
        /// </summary>
        /// <param name="fileName">The path of the drawing, as it is to be written.</param>
        /// <param name="blockName">The name of the block.</param>
        /// <returns>The block.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path or the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the path is empty or has a line break in it, or the name is one AutoCAD
        /// does not take or that of a block of the drawing already.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing has no BLOCKS section.</exception>
        public DxfBlockTableRecord OverlayXref(string fileName, string blockName) => Xref(fileName, blockName, 12);

        /// <summary>
        /// Detaches an external reference, as AutoCAD's <c>DetachXref</c>: every INSERT of it erased, in model space, paper
        /// space and the blocks, then the records it brought, those named after it with a | (the layer <c>COLUMN|steel</c> and
        /// the like, of the layer, linetype, text style, dimension style and block tables), and last its block.
        /// </summary>
        /// <param name="xrefBlock">The block of the external reference, of this drawing.</param>
        /// <exception cref="ArgumentNullException">Thrown when the block is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the block is of another drawing, or is no external reference.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block is erased.</exception>
        public void DetachXref(DxfBlockTableRecord xrefBlock)
        {
            if (xrefBlock == null)
            {
                throw new ArgumentNullException(nameof(xrefBlock));
            }

            if (xrefBlock.File != File)
            {
                throw new ArgumentException("The block " + xrefBlock.Name + " is not of this drawing.", nameof(xrefBlock));
            }

            if (xrefBlock.Gone)
            {
                throw new InvalidOperationException("The block " + xrefBlock.Name + " is erased already.");
            }

            if (!xrefBlock.IsFromExternalReference)
            {
                throw new ArgumentException("The block " + xrefBlock.Name + " is no external reference to detach.", nameof(xrefBlock));
            }

            string name = xrefBlock.Name;
            string prefix = name + "|";
            IReadOnlyList<DxfBlockReference> inserts = xrefBlock.GetBlockReferences(true);

            foreach (DxfBlockReference insert in inserts)
            {
                insert.Erase();
            }

            int dependents = 0;

            foreach (string table in DependentTables)
            {
                foreach (DxfItem item in File.TableItems(table).ToArray())
                {
                    if (DxfReader.NameOf(File.ValueOf(item, 2) ?? string.Empty).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string handle = item.Handle;
                        File.RemoveTableRecord(item, table);
                        dependents++;

                        // The head of the DIMSTYLE table lists its records' handles from R2000 on.
                        if (table == "DIMSTYLE")
                        {
                            DxfDimStyleTable.ListHandle(File, handle, false);
                        }
                    }
                }
            }

            foreach (DxfBlockTableRecord block in BlockTable)
            {
                if (block.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !block.IsLayout)
                {
                    block.Remove();
                    dependents++;
                }
            }

            xrefBlock.Remove();
            GeometryHelperLog.Info("DxfDatabase: the external reference " + name + " was detached, with " + inserts.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " INSERTs and " + dependents.ToString(System.Globalization.CultureInfo.InvariantCulture) + " records it brought.");
        }

        /// <summary>Adds the block of an external reference, its BLOCK's flags those given, as <see cref="AttachXref"/> says.</summary>
        private DxfBlockTableRecord Xref(string fileName, string blockName, int flags)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (blockName == null)
            {
                throw new ArgumentNullException(nameof(blockName));
            }

            if (fileName.Trim().Length == 0 || fileName.IndexOf('\n') >= 0 || fileName.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("The path of an external reference is given, on one line: '" + fileName + "' is not one.", nameof(fileName));
            }

            DxfSymbolTable.CheckName(blockName, nameof(blockName));

            if (BlockTable.Has(blockName))
            {
                throw new ArgumentException("The drawing has a block named " + blockName + " already, letters of either case the same.", nameof(blockName));
            }

            var block = new DxfBlockTableRecord { Name = blockName, MadeFlags = flags, MadePath = fileName };
            BlockTable.Add(block);
            GeometryHelperLog.Info("DxfDatabase: the drawing " + fileName + " was " + (flags == 4 ? "attached" : "overlaid") + " as the external reference " + blockName + ".");
            return block;
        }
    }
}

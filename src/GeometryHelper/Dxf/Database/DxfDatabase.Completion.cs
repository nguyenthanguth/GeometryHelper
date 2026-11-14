using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfDatabase
    {
        /// <summary>
        /// The drawing's file as it is saved when it holds tables or table styles written in this session that AutoCAD 2021 would
        /// have to build the round trip of when it reads the file: a copy of the file in which each such table has its
        /// TABLECONTENT, TABLEGEOMETRY and extension dictionary (<see cref="DxfTable"/>) and each such style, and the style a
        /// table names, its map of cell styles (<see cref="DxfTableStyle"/>). The drawing itself is not changed. Null where there
        /// is nothing to make: no table or style written, a drawing before R2007, or every one written already has its own.
        /// </summary>
        private DxfFile Completed()
        {
            if (!File.Supports(1021))
            {
                return null;
            }

            List<string> written = File.WrittenHandles("ACAD_TABLE", "TABLESTYLE");
            bool needed = false;

            foreach (string handle in written)
            {
                if (TryGetObject(handle, out DxfDBObject found) && Needs(found))
                {
                    needed = true;
                    break;
                }
            }

            if (!needed)
            {
                return null;
            }

            DxfDatabase copy = Parse(File.ToText());
            var styles = new List<string>();
            bool made = false;

            foreach (string handle in written)
            {
                if (copy.TryGetObject(handle, out DxfDBObject found) && found is DxfTable table && table.MakeContent())
                {
                    made = true;
                    string style = table.StyleHandleOf();

                    if (style != null && !styles.Contains(style))
                    {
                        styles.Add(style);
                    }
                }
                else if (found is DxfTableStyle && !styles.Contains(handle))
                {
                    styles.Add(handle);
                }
            }

            foreach (string handle in styles)
            {
                if (
                    copy.TryGetObject(handle, out DxfDBObject found)
                    && found is DxfTableStyle style
                    && style.MakeCellStyleMap()
                )
                {
                    made = true;
                }
            }

            return made ? copy.File : null;
        }

        private static bool Needs(DxfDBObject found) =>
            found is DxfTable table ? table.NeedsContent : found is DxfTableStyle style && style.NeedsCellStyleMap;
    }
}

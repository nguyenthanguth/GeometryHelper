using System;
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

            // A drawing saved again as it stands (to another version, say) is completed once: what was made is kept, weakly, for as
            // long as the file has had no change since.
            if (_completedAt == File.EditCount)
            {
                if (_completedFile == null)
                {
                    return null;
                }

                if (_completedFile.TryGetTarget(out DxfFile kept))
                {
                    return kept;
                }
            }

            DxfFile made = Complete();
            _completedAt = File.EditCount;
            _completedFile = made == null ? null : new WeakReference<DxfFile>(made);
            return made;
        }

        // What Completed made last, and the count of changes of the file once it had.
        private WeakReference<DxfFile> _completedFile;
        private int _completedAt = -1;

        /// <summary>The copy of the file with the content of its tables and styles made, as <see cref="Completed"/> describes it; null where there is nothing to make.</summary>
        private DxfFile Complete()
        {
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

            // The copy is opened from the bytes the file saves as, where they are its text; the text is the way where they are not.
            DxfFile bytes = File.CopyFromBytes();
            DxfDatabase copy = bytes != null ? new DxfDatabase(bytes) : Parse(File.ToText());

            // The tables written are found in the copy's records, not among its every entity.
            Dictionary<string, DxfItem> tables = copy.File.TableEntitiesByHandle(written);
            var styles = new List<string>();
            bool made = false;

            // The tables are listed in the drawing's ACDB_RECOMPOSE_DATA all at once, once each has been made.
            copy.File.RecomposeBatch = new List<string>();

            try
            {
                foreach (string handle in written)
                {
                    DxfDBObject found = tables.TryGetValue(handle, out DxfItem item)
                        ? DxfEntity.Of(item)
                        : copy.ObjectByHandle(handle)
                            ?? (copy.TryGetObject(handle, out DxfDBObject other) ? other : null);

                    if (found != null && found.IsErased)
                    {
                        found = null;
                    }

                    if (found is DxfTable table && table.MakeContent(out string style))
                    {
                        made = true;

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
                    if (copy.ObjectByHandle(handle) is DxfTableStyle style && style.MakeCellStyleMap())
                    {
                        made = true;
                    }
                }

                DxfTable.EndRecompose(copy.File);
            }
            finally
            {
                copy.File.RecomposeBatch = null;
            }

            return made ? copy.File : null;
        }

        private static bool Needs(DxfDBObject found) =>
            found is DxfTable table ? table.NeedsContent : found is DxfTableStyle style && style.NeedsCellStyleMap;
    }
}

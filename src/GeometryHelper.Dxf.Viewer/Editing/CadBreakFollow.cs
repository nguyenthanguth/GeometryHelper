using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The breaks DIMBREAK Auto made in a dimension or a multileader follow the objects they were made at, as AutoCAD 2021
    /// draws them again from the BREAKDATA (probed in its console, some 150 edits of level dimensions and multileaders with a
    /// line across): the object moved, turned, scaled, stretched or erased, or the dimension or multileader itself moved, turned
    /// or scaled, and the break is where the object crosses it now. A line across a level dimension from (0, 0) to (100, 0) at
    /// x 30, moved 10 to the right, breaks the dimension line at x 40 and no longer at 30, and moved off it leaves the line whole
    /// while the BREAKDATA still names it; erased, it is taken out of the BREAKDATA, which goes with its last object. An object
    /// the BREAKDATA does not name, drawn or moved across the dimension later, breaks nothing.
    /// </summary>
    internal static class CadBreakFollow
    {
        /// <summary>
        /// Follows an edit: each dimension and multileader that keeps breaks and whose object, or which itself, the edit changed
        /// is drawn again with its breaks where they stand now. Returns the records of those changed, for the edit's step; none
        /// where no dimension or multileader keeps a break.
        /// </summary>
        internal static List<CadEditRecord> Follow(
            CadDocument document,
            IReadOnlyList<CadEditRecord> planned,
            IReadOnlyList<CadEditRecord> made
        )
        {
            var followed = new List<CadEditRecord>();
            var moved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (IReadOnlyList<CadEditRecord> records in new[] { planned, made })
            {
                foreach (CadEditRecord record in records)
                {
                    if (record.Handle == null)
                    {
                        continue;
                    }

                    switch (record.Kind)
                    {
                        case CadEditRecordKind.Transformed:
                        case CadEditRecordKind.Reshaped:
                            moved.Add(record.Handle);
                            changed.Add(record.Handle);
                            break;
                        case CadEditRecordKind.PropertyChanged:
                        case CadEditRecordKind.Erased:
                            changed.Add(record.Handle);
                            break;
                    }
                }
            }

            if (changed.Count == 0)
            {
                return followed;
            }

            var hosts = new SortedSet<string>(StringComparer.Ordinal);

            foreach (string handle in changed)
            {
                DxfEntity entity = document.FindKept(handle);

                if (entity == null)
                {
                    continue;
                }

                if (moved.Contains(handle) && (entity is DxfDimension || entity is DxfMLeader))
                {
                    hosts.Add(entity.Handle.ToUpperInvariant());
                }

                foreach (string reactor in entity.GetPersistentReactorIds())
                {
                    if (
                        document.Database.TryGetObject(reactor, out DxfDBObject found)
                        && found.Item != null
                        && found.Item.Type == "BREAKDATA"
                    )
                    {
                        string host = CadCommands.BreakDataHost(found);

                        if (host != null)
                        {
                            hosts.Add(host.ToUpperInvariant());
                        }
                    }
                }
            }

            foreach (string handle in hosts)
            {
                DxfEntity host = document.FindEntity(handle);

                if (host == null || host.IsErased)
                {
                    continue;
                }

                try
                {
                    if (CadCommands.FollowBreaks(document, host))
                    {
                        followed.Add(
                            new CadEditRecord(CadEditRecordKind.Reshaped, host.Handle)
                            {
                                Space = document.FindSpace(handle)?.Name,
                            }
                        );
                    }
                }
                catch (Exception exception) when (!(exception is OutOfMemoryException))
                {
                    // The breaks of one dimension that cannot be made again leave the edit as it was.
                    GeometryHelperLog.Warn(
                        "CadBreakFollow: the breaks of " + host.Handle + " could not be made again.",
                        exception
                    );
                }
            }

            return followed;
        }
    }
}

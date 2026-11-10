using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The breaks DIMBREAK puts in a dimension, kept as AutoCAD 2021 keeps them: a BREAKDATA under ACAD_BREAKDATA in the
    /// dimension's extension dictionary, so that a dimension drawn anew is broken again, here and in AutoCAD.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AutoCAD 2021, probed in its console, writes a BREAKDATA (AcDbBreakData) of a 70 of 0, the dimension's handle (330) and how
    /// many breaks follow (90), each an AcDbBreakPointRef: 71 of 0 for a break at an object, its 91 -1, its points (10, 11)
    /// nought and the object's handle (331) with a 73 of 2 and a 92 of 0; 71 of 2 for one between two points (Manual), the
    /// points as picked and in 91 the line they break: 0 the first extension line, 1 the second, 3 the dimension line (the
    /// line of a radius and the arc of an angle too). Auto keeps one break for each object that crosses the dimension, none
    /// for those that do not; each object kept names the BREAKDATA among its reactors. Remove erases the BREAKDATA, the
    /// reactors with it, and the extension dictionary where it holds nothing else.
    /// </para>
    /// <para>
    /// A level dimension from (0, -100) to (100, -100), its line 20 above, broken by hand from (70, -80) to (80, -80) keeps one
    /// break of 71 2 and 91 3 at those points; broken by Auto where a line crosses its extension lines it keeps one break of 71
    /// 0 naming the line.
    /// </para>
    /// </remarks>
    internal static partial class CadCommands
    {
        private const string BreakDataKey = "ACAD_BREAKDATA";

        /// <summary>One break of a dimension as AutoCAD keeps it: at an object, or between two points on one of its lines.</summary>
        private sealed class DimensionBreak
        {
            internal DimensionBreak(string objectHandle)
            {
                ObjectHandle = objectHandle;
                Line = -1;
            }

            internal DimensionBreak(int line, GeoPoint3 first, GeoPoint3 second)
            {
                Line = line;
                First = first;
                Second = second;
            }

            /// <summary>Gets the handle of the object the break is at; null for one between two points.</summary>
            internal string ObjectHandle { get; }

            /// <summary>Gets the line broken between two points (91): 0 and 1 the extension lines, 3 the dimension line; -1 for one at an object.</summary>
            internal int Line { get; }

            internal GeoPoint3 First { get; }

            internal GeoPoint3 Second { get; }
        }

        /// <summary>Keeps a break at each object given that crosses the dimension, after those it keeps, an object kept already not again.</summary>
        private static void KeepObjectBreaks(DxfDimension dimension, List<DxfEntity> crossed)
        {
            List<DimensionBreak> breaks = StoredBreaks(dimension);
            bool added = false;

            foreach (DxfEntity entity in crossed)
            {
                if (entity.Handle != null && !breaks.Any(b => string.Equals(b.ObjectHandle, entity.Handle, StringComparison.OrdinalIgnoreCase)))
                {
                    breaks.Add(new DimensionBreak(entity.Handle));
                    added = true;
                }
            }

            if (added)
            {
                StoreBreaks(dimension, breaks);
            }
        }

        /// <summary>Keeps a break between two points, after those the dimension keeps, on the line they break.</summary>
        private static void KeepManualBreak(DxfDimension dimension, GeoPoint2 first, GeoPoint2 second)
        {
            List<DimensionBreak> breaks = StoredBreaks(dimension);
            var middle = new GeoPoint2((first.X + second.X) / 2.0, (first.Y + second.Y) / 2.0);
            breaks.Add(new DimensionBreak(LineBroken(dimension, middle), P3(first), P3(second)));
            StoreBreaks(dimension, breaks);
        }

        /// <summary>Takes out every break the dimension keeps (DIMBREAK Remove).</summary>
        private static void ForgetBreaks(DxfDimension dimension) => StoreBreaks(dimension, new List<DimensionBreak>());

        /// <summary>
        /// Breaks a dimension's picture drawn anew where the dimension keeps breaks: at each object it names that is still in the
        /// drawing, and between each two points it keeps.
        /// </summary>
        private static void BreakAgain(DxfDimension dimension)
        {
            List<DimensionBreak> breaks = StoredBreaks(dimension);

            if (breaks.Count == 0)
            {
                return;
            }

            DxfDatabase database = dimension.Item.File.Database;
            var cutters = new List<DxfEntity>();

            foreach (DimensionBreak stored in breaks)
            {
                if (stored.ObjectHandle != null && database != null && database.TryGetObject(stored.ObjectHandle, out DxfDBObject found)
                    && found is DxfEntity entity && !ReferenceEquals(entity, dimension) && (entity is DxfLine || entity is DxfPolyline || entity is DxfCircle || entity is DxfArc))
                {
                    cutters.Add(entity);
                }
            }

            if (cutters.Count > 0)
            {
                BreakAt(dimension, cutters, null);
            }

            foreach (DimensionBreak stored in breaks)
            {
                if (stored.ObjectHandle == null)
                {
                    BreakAt(dimension, null, new[] { P2(stored.First), P2(stored.Second) });
                }
            }
        }

        /// <summary>The breaks a dimension keeps, in their order; none where it keeps no BREAKDATA.</summary>
        private static List<DimensionBreak> StoredBreaks(DxfDimension dimension)
        {
            var breaks = new List<DimensionBreak>();
            DxfDBObject data = BreakData(dimension);

            if (data == null)
            {
                return breaks;
            }

            Dictionary<int, string> groups = null;

            foreach (DxfTypedValue group in data.Item.File.ObjectGroupsOf(data.Item))
            {
                if (group.TypeCode == 100)
                {
                    Read(groups, breaks);
                    groups = group.Value == "AcDbBreakPointRef" ? new Dictionary<int, string>() : null;
                }
                else if (groups != null && !groups.ContainsKey(group.TypeCode))
                {
                    groups[group.TypeCode] = group.Value;
                }
            }

            Read(groups, breaks);
            return breaks;
        }

        /// <summary>Adds the break of the groups of one AcDbBreakPointRef: at the object of its 331, or by hand (71 of 2) between its points.</summary>
        private static void Read(Dictionary<int, string> groups, List<DimensionBreak> breaks)
        {
            if (groups == null)
            {
                return;
            }

            if (groups.TryGetValue(331, out string handle) && !string.IsNullOrWhiteSpace(handle) && Whole(groups, 71) != 2)
            {
                breaks.Add(new DimensionBreak(handle.Trim()));
            }
            else if (Whole(groups, 71) == 2)
            {
                breaks.Add(new DimensionBreak(Whole(groups, 91), new GeoPoint3(Real(groups, 10), Real(groups, 20), Real(groups, 30)), new GeoPoint3(Real(groups, 11), Real(groups, 21), Real(groups, 31))));
            }
        }

        private static int Whole(Dictionary<int, string> groups, int code)
            => groups.TryGetValue(code, out string text) && int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;

        private static double Real(Dictionary<int, string> groups, int code)
            => groups.TryGetValue(code, out string text) && double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0.0;

        /// <summary>The BREAKDATA of a dimension, under ACAD_BREAKDATA in its extension dictionary; null where it has none.</summary>
        private static DxfDBObject BreakData(DxfDimension dimension)
        {
            DxfDBDictionary dictionary = dimension.Item == null ? null : dimension.ExtensionDictionary;
            DxfDBObject data = dictionary != null && dictionary.Contains(BreakDataKey) ? dictionary.GetAt(BreakDataKey) : null;
            return data?.Item != null && data.Item.Type == "BREAKDATA" ? data : null;
        }

        /// <summary>
        /// Writes the breaks of a dimension as AutoCAD 2021 writes them, in place of those it kept: a BREAKDATA under
        /// ACAD_BREAKDATA in its extension dictionary, each object broken at naming it among its reactors; for no breaks, no
        /// BREAKDATA, and no extension dictionary where it holds nothing else. Nothing is kept in a file older than AutoCAD 2008
        /// (AC1021), which has no BREAKDATA: the picture alone is broken there.
        /// </summary>
        private static void StoreBreaks(DxfDimension dimension, List<DimensionBreak> breaks)
        {
            DxfFile file = dimension.Item?.File;

            if (file == null)
            {
                return;
            }

            DxfDBObject old = BreakData(dimension);

            if (old != null)
            {
                foreach (DimensionBreak stored in StoredBreaks(dimension))
                {
                    SetBreakReactor(file, stored.ObjectHandle, old.Handle, false);
                }

                dimension.ExtensionDictionary.Remove(BreakDataKey);
            }

            if (breaks.Count == 0 || !file.Supports(1021))
            {
                if (dimension.ExtensionDictionary != null && dimension.ExtensionDictionary.Count == 0)
                {
                    dimension.ReleaseExtensionDictionary();
                }

                return;
            }

            string host = dimension.Handle;
            dimension.CreateExtensionDictionary();
            DxfItem made = dimension.ExtensionDictionary.Add(BreakDataKey, "BREAKDATA", b =>
            {
                b.Group(100, "AcDbBreakData");
                b.Group(70, "0");
                b.Group(330, host);
                b.Group(90, breaks.Count.ToString(CultureInfo.InvariantCulture));

                foreach (DimensionBreak stored in breaks)
                {
                    bool manual = stored.ObjectHandle == null;
                    GeoPoint3 first = manual ? stored.First : new GeoPoint3(0, 0, 0);
                    GeoPoint3 second = manual ? stored.Second : new GeoPoint3(0, 0, 0);
                    b.Group(100, "AcDbBreakPointRef");
                    b.Group(71, manual ? "2" : "0");
                    b.Group(72, "0");
                    b.Group(91, (manual ? stored.Line : -1).ToString(CultureInfo.InvariantCulture));
                    b.Group(10, Numbers.Exact(first.X));
                    b.Group(20, Numbers.Exact(first.Y));
                    b.Group(30, Numbers.Exact(first.Z));
                    b.Group(11, Numbers.Exact(second.X));
                    b.Group(21, Numbers.Exact(second.Y));
                    b.Group(31, Numbers.Exact(second.Z));

                    if (!manual)
                    {
                        b.Group(331, stored.ObjectHandle);
                        b.Group(73, "2");
                        b.Group(92, "0");
                    }
                }
            });

            file.AddClass("BREAKDATA", "AcDbBreakData", 1024);

            foreach (DimensionBreak stored in breaks)
            {
                SetBreakReactor(file, stored.ObjectHandle, made.Handle, true);
            }
        }

        /// <summary>Puts the handle of a BREAKDATA among the reactors of the object a break is at, or takes it out; nothing for a break by hand or an object gone.</summary>
        private static void SetBreakReactor(DxfFile file, string objectHandle, string breakData, bool present)
        {
            if (objectHandle != null && file.Database != null && file.Database.TryGetObject(objectHandle, out DxfDBObject found) && found.Item != null)
            {
                file.SetReactor(found.Item, breakData, present);
            }
        }

        /// <summary>
        /// The line of a dimension a break between two points is on, as AutoCAD numbers it (91): 0 and 1 the first and second
        /// extension lines of a linear or an aligned dimension, where the middle of the two points lies nearer one of them than
        /// the dimension line; else 3, the dimension line, as AutoCAD writes for a radius and an angle too.
        /// </summary>
        private static int LineBroken(DxfDimension dimension, GeoPoint2 middle)
        {
            GeoPoint2 p1;
            GeoPoint2 p2;
            GeoPoint2 onLine;

            if (dimension is DxfRotatedDimension rotated)
            {
                p1 = P2(rotated.XLine1Point);
                p2 = P2(rotated.XLine2Point);
                onLine = P2(rotated.DimLinePoint);
            }
            else if (dimension is DxfAlignedDimension aligned)
            {
                p1 = P2(aligned.XLine1Point);
                p2 = P2(aligned.XLine2Point);
                onLine = P2(aligned.DimLinePoint);
            }
            else
            {
                return 3;
            }

            double angle = LineAngleOf(dimension);
            var along = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
            double oblique = ObliqueOf(dimension);
            GeoPoint2 q1 = Foot(p1, onLine, along, oblique);
            GeoPoint2 q2 = Foot(p2, onLine, along, oblique);
            double toLine = Math.Abs(Cross(middle - q1, along));
            double toFirst = SegmentDistance(p1, q1, middle);
            double toSecond = SegmentDistance(p2, q2, middle);

            if (toFirst < toLine && toFirst <= toSecond)
            {
                return 0;
            }

            return toSecond < toLine ? 1 : 3;
        }
    }
}

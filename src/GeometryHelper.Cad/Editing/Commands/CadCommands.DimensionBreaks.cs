using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
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

        /// <summary>
        /// The box a dimension's picture fills where it stands, its points of definition among it, as AutoCAD 2021's
        /// <c>GeometricExtents</c> of a dimension (a level dimension from (0, 0) to (100, 0), its line at y 20, fills
        /// (0, 0) to (100, 20.18)), which <see cref="DxfDimension.GeometricExtents"/> gives; empty for a dimension with no
        /// picture.
        /// </summary>
        private static GeoAabb3 PictureBox(DxfDimension dimension)
        {
            try
            {
                return dimension.GeometricExtents;
            }
            catch (InvalidOperationException)
            {
                return GeoAabb3.Empty;
            }
        }

        /// <summary>The box of an object that breaks a dimension or a multileader; empty where it has none (an xline or a ray has none that is bounded).</summary>
        private static GeoAabb3 BoxOfObject(DxfEntity entity)
        {
            try
            {
                return entity is DxfDimension dimension ? PictureBox(dimension)
                    : entity is DxfXline || entity is DxfRay ? GeoAabb3.Empty
                    : entity.GeometricExtents;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is NotSupportedException)
            {
                return GeoAabb3.Empty;
            }
        }

        /// <summary>Whether an xline (from minus infinity) or a ray (from its base) meets a box.</summary>
        private static bool LineMeetsBox(DxfEntity line, GeoAabb3 box, double tolerance)
        {
            GeoPoint3 origin = line is DxfXline xline ? xline.BasePoint : ((DxfRay)line).BasePoint;
            GeoVector3 way = line is DxfXline xdir ? xdir.UnitDir : ((DxfRay)line).UnitDir;
            double low = line is DxfXline ? double.NegativeInfinity : 0.0;
            double high = double.PositiveInfinity;

            for (int axis = 0; axis < 2; axis++)
            {
                double start = axis == 0 ? origin.X : origin.Y;
                double step = axis == 0 ? way.X : way.Y;
                double min = (axis == 0 ? box.Min.X : box.Min.Y) - tolerance;
                double max = (axis == 0 ? box.Max.X : box.Max.Y) + tolerance;

                if (Math.Abs(step) < 1E-12)
                {
                    if (start < min || start > max)
                    {
                        return false;
                    }

                    continue;
                }

                double t1 = (min - start) / step;
                double t2 = (max - start) / step;
                low = Math.Max(low, Math.Min(t1, t2));
                high = Math.Min(high, Math.Max(t1, t2));
            }

            return low <= high;
        }

        /// <summary>
        /// The objects AutoCAD 2021 names in the BREAKDATA of an Auto: every object that can break the host whose box meets the
        /// host's, whether it crosses it or not (a line inside a level dimension that touches none of its lines is named; a point,
        /// a solid and a hatch are not), the newest first. AutoCAD takes a margin of about a pixel of its view round the boxes,
        /// which the presenter, with no view of its own, does not.
        /// </summary>
        private static List<DxfEntity> MeetingBoxes(GeoAabb3 host, IEnumerable<DxfEntity> cutters)
        {
            var named = new List<DxfEntity>();

            if (host.IsEmpty)
            {
                return named;
            }

            double tolerance = Tolerance.Default.EqualPoint;

            foreach (DxfEntity entity in cutters)
            {
                if (entity is DxfXline || entity is DxfRay)
                {
                    if (LineMeetsBox(entity, host, tolerance))
                    {
                        named.Add(entity);
                    }

                    continue;
                }

                GeoAabb3 box = BoxOfObject(entity);

                if (
                    !box.IsEmpty
                    && box.Min.X <= host.Max.X + tolerance
                    && box.Max.X >= host.Min.X - tolerance
                    && box.Min.Y <= host.Max.Y + tolerance
                    && box.Max.Y >= host.Min.Y - tolerance
                )
                {
                    named.Add(entity);
                }
            }

            named.Sort((a, b) => NewerHandle(b.Handle, a.Handle));
            return named;
        }

        /// <summary>What an Auto over the whole space names: the objects meeting the host's box and those that cross it, the newest first.</summary>
        private static List<DxfEntity> NamedByAuto(
            GeoAabb3 host,
            IReadOnlyList<DxfEntity> cutters,
            List<DxfEntity> crossed
        )
        {
            List<DxfEntity> named = MeetingBoxes(host, cutters);

            foreach (DxfEntity entity in crossed)
            {
                if (!named.Contains(entity))
                {
                    named.Add(entity);
                }
            }

            named.Sort((a, b) => NewerHandle(b.Handle, a.Handle));
            return named;
        }

        /// <summary>Compares two handles as the numbers they are written in, the greater first where it is the lesser's argument.</summary>
        private static int NewerHandle(string first, string second)
        {
            string a = first ?? string.Empty;
            string b = second ?? string.Empty;
            return a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
        }

        /// <summary>The handle of the dimension or multileader a BREAKDATA belongs to (its 330 after the marker AcDbBreakData); null where it names none.</summary>
        internal static string BreakDataHost(DxfDBObject data)
        {
            bool inside = false;

            foreach (DxfTypedValue group in data.Item.File.ObjectGroupsOf(data.Item))
            {
                if (group.TypeCode == 100)
                {
                    inside = group.Value == "AcDbBreakData";
                }
                else if (inside && group.TypeCode == 330 && !string.IsNullOrWhiteSpace(group.Value))
                {
                    return group.Value.Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// Draws a dimension or multileader again where it keeps breaks and an object it was broken at, or it itself, changed:
        /// an object erased is taken out of the breaks it keeps, and the breaks are made again at those left, where each
        /// object crosses now (AutoCAD 2021 draws a dimension again from its BREAKDATA after any such edit). A dimension kept
        /// by hand alone is left as the edit made it, its picture moved with it; a multileader is drawn from its BREAKDATA
        /// every time, the break between two points where they stand now (AutoCAD keeps the points as picked and takes the feet
        /// of them on the leader line moved). False where nothing was changed.
        /// </summary>
        internal static bool FollowBreaks(CadDocument document, DxfEntity host)
        {
            List<DimensionBreak> stored = StoredBreaks(host);

            if (stored.Count == 0)
            {
                return false;
            }

            var kept = new List<DimensionBreak>();

            foreach (DimensionBreak item in stored)
            {
                if (
                    item.ObjectHandle == null
                    || (document.FindEntity(item.ObjectHandle) is DxfEntity found && !found.IsErased)
                )
                {
                    kept.Add(item);
                }
            }

            bool dropped = kept.Count != stored.Count;

            if (dropped)
            {
                StoreBreaks(host, kept);
            }

            using (Measuring(document))
            {
                if (host is DxfMLeader mleader)
                {
                    BreakAgain(document, mleader, kept);
                    return true;
                }

                if (!(host is DxfDimension dimension) || (!dropped && !kept.Exists(b => b.ObjectHandle != null)))
                {
                    return dropped;
                }

                return Redraw(dimension) || dropped;
            }
        }

        /// <summary>
        /// Carries the breaks a dimension keeps by hand with it where it is moved, turned or scaled: their points go where the
        /// dimension's go, as AutoCAD 2021 keeps them (a break from (60, 20) to (70, 20) of a level dimension moved 10 to the
        /// right is kept from (70, 20) to (80, 20)). A multileader keeps the points as picked. A dimension scaled is drawn
        /// again, as AutoCAD 2021 draws it (its style's offsets and arrowheads are not scaled with it): a level dimension from
        /// (0, 0) to (100, 0) with a break by hand, scaled by 2, has its extension lines from 0.0625 off its points up to 0.18
        /// past its line at y 40, where the picture scaled would have them from 0.125 to 0.36 past.
        /// </summary>
        internal static void CarryManualBreaks(CadDocument document, DxfEntity entity, GeoTransform3 transform)
        {
            if (!(entity is DxfDimension dimension))
            {
                return;
            }

            List<DimensionBreak> breaks = StoredBreaks(entity);

            if (!breaks.Exists(b => b.ObjectHandle == null))
            {
                return;
            }

            StoreBreaks(
                entity,
                breaks
                    .Select(b =>
                        b.ObjectHandle != null
                            ? b
                            : new DimensionBreak(b.Line, transform.Transform(b.First), transform.Transform(b.Second))
                    )
                    .ToList()
            );

            // The breaks at objects are drawn again by the follow that comes after every edit.
            GeoPoint3 origin = transform.Transform(new GeoPoint3(0.0, 0.0, 0.0));

            if (
                !breaks.Exists(b => b.ObjectHandle != null)
                && Math.Abs(origin.GetVectorTo(transform.Transform(new GeoPoint3(1.0, 0.0, 0.0))).Length - 1.0) > 1E-9
            )
            {
                using (Measuring(document))
                {
                    Redraw(dimension);
                }
            }
        }

        /// <summary>
        /// Gives the copy of a dimension, made by <paramref name="transform"/>, breaks of its own, as AutoCAD 2021's COPY does:
        /// a BREAKDATA naming the same objects as the original's (each of them names it among its reactors in turn), the points
        /// of a break by hand carried by the move, and the copy drawn again where it keeps a break at an object, so that it is
        /// broken where those objects cross it now and not where they crossed the original. A level dimension from (0, 0) to
        /// (100, 0), its line at y 20, broken at a line at x 30, copied 100 to the right, is whole: the line does not cross it,
        /// though it is named in the copy's BREAKDATA.
        /// </summary>
        internal static void CopyBreaks(
            CadDocument document,
            DxfEntity original,
            DxfEntity copy,
            GeoTransform3 transform
        )
        {
            if (!(copy is DxfDimension))
            {
                return;
            }

            List<DimensionBreak> breaks = StoredBreaks(original);

            if (breaks.Count == 0)
            {
                return;
            }

            StoreBreaks(
                copy,
                breaks
                    .Select(b =>
                        b.ObjectHandle != null
                            ? b
                            : new DimensionBreak(b.Line, transform.Transform(b.First), transform.Transform(b.Second))
                    )
                    .ToList()
            );
            FollowBreaks(document, copy);
        }

        /// <summary>Keeps a break at each object given that crosses the dimension, after those it keeps, an object kept already not again.</summary>
        private static void KeepObjectBreaks(DxfEntity dimension, List<DxfEntity> crossed)
        {
            List<DimensionBreak> breaks = StoredBreaks(dimension);
            bool added = false;

            foreach (DxfEntity entity in crossed)
            {
                if (
                    entity.Handle != null
                    && !breaks.Any(b =>
                        string.Equals(b.ObjectHandle, entity.Handle, StringComparison.OrdinalIgnoreCase)
                    )
                )
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

        /// <summary>Keeps a break between two points, after those the dimension keeps, on the line they broke (<see cref="ManualBreak"/>).</summary>
        private static void KeepManualBreak(DxfEntity dimension, GeoPoint2 first, GeoPoint2 second, int line)
        {
            List<DimensionBreak> breaks = StoredBreaks(dimension);
            breaks.Add(new DimensionBreak(line, P3(first), P3(second)));
            StoreBreaks(dimension, breaks);
        }

        /// <summary>Takes out every break the dimension keeps (DIMBREAK Remove).</summary>
        private static void ForgetBreaks(DxfEntity dimension) => StoreBreaks(dimension, new List<DimensionBreak>());

        /// <summary>
        /// Breaks a dimension's picture drawn anew where the dimension keeps breaks: at each object it names that is still in the
        /// drawing, and between each two points it keeps.
        /// </summary>
        private static void BreakAgain(
            DxfDimension dimension,
            DxfBlockTableRecord block = null,
            double? dimscale = null
        )
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
                if (
                    stored.ObjectHandle != null
                    && database != null
                    && database.TryGetObject(stored.ObjectHandle, out DxfDBObject found)
                    && found is DxfEntity entity
                    && !ReferenceEquals(entity, dimension)
                    && !entity.IsErased
                    && IsCutter(entity)
                )
                {
                    cutters.Add(entity);
                }
            }

            if (cutters.Count > 0)
            {
                BreakAt(dimension, cutters, null, block, dimscale);
            }

            foreach (DimensionBreak stored in breaks)
            {
                if (stored.ObjectHandle == null)
                {
                    ManualBreak(
                        dimension,
                        block ?? dimension.DimBlock,
                        P2(stored.First),
                        P2(stored.Second),
                        stored.Line >= 0 ? stored.Line : (int?)null,
                        dimscale
                    );
                }
            }
        }

        /// <summary>
        /// Draws the pictures of an annotative dimension anew after its breaks changed, each broken at the size of its scale: the
        /// breaks are kept by the dimension, so every scale's picture takes them. A dimension that is not annotative is left.
        /// </summary>
        private static void RedrawAnnotativeBreaks(DxfDimension dimension)
        {
            if (dimension.Annotative == DxfAnnotativeStates.True && dimension.DimensionPictures().Count > 0)
            {
                RedrawAnnotative(dimension);
            }
        }

        /// <summary>The breaks a dimension keeps, in their order; none where it keeps no BREAKDATA.</summary>
        private static List<DimensionBreak> StoredBreaks(DxfEntity dimension)
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

            if (
                groups.TryGetValue(331, out string handle)
                && !string.IsNullOrWhiteSpace(handle)
                && Whole(groups, 71) != 2
            )
            {
                breaks.Add(new DimensionBreak(handle.Trim()));
            }
            else if (Whole(groups, 71) == 2)
            {
                breaks.Add(
                    new DimensionBreak(
                        Whole(groups, 91),
                        new GeoPoint3(Real(groups, 10), Real(groups, 20), Real(groups, 30)),
                        new GeoPoint3(Real(groups, 11), Real(groups, 21), Real(groups, 31))
                    )
                );
            }
        }

        private static int Whole(Dictionary<int, string> groups, int code) =>
            groups.TryGetValue(code, out string text)
            && int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : 0;

        private static double Real(Dictionary<int, string> groups, int code) =>
            groups.TryGetValue(code, out string text)
            && double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : 0.0;

        /// <summary>The BREAKDATA of a dimension, under ACAD_BREAKDATA in its extension dictionary; null where it has none.</summary>
        private static DxfDBObject BreakData(DxfEntity dimension)
        {
            DxfDBDictionary dictionary = dimension.Item == null ? null : dimension.ExtensionDictionary;
            DxfDBObject data =
                dictionary != null && dictionary.Contains(BreakDataKey) ? dictionary.GetAt(BreakDataKey) : null;
            return data?.Item != null && data.Item.Type == "BREAKDATA" ? data : null;
        }

        /// <summary>
        /// Writes the breaks of a dimension as AutoCAD 2021 writes them, in place of those it kept: a BREAKDATA under
        /// ACAD_BREAKDATA in its extension dictionary, each object broken at naming it among its reactors; for no breaks, no
        /// BREAKDATA, and no extension dictionary where it holds nothing else. Nothing is kept in a file older than AutoCAD 2008
        /// (AC1021), which has no BREAKDATA: the picture alone is broken there.
        /// </summary>
        private static void StoreBreaks(DxfEntity dimension, List<DimensionBreak> breaks)
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
            if (
                objectHandle != null
                && file.Database != null
                && file.Database.TryGetObject(objectHandle, out DxfDBObject found)
                && found.Item != null
            )
            {
                file.SetReactor(found.Item, breakData, present);
            }
        }
    }
}

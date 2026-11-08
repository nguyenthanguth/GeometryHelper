using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper;
using GeometryHelper.Drawing;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Geometry;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Makes the <see cref="DrawingSource"/> of what <see cref="DrawingSourceConvert"/> read from Tekla. Nothing here
    /// touches Tekla, so all of it is tested in code.
    /// </summary>
    internal static class DrawingSourceBuild
    {
        /// <summary>The sides a bolt or a hole is faceted into: a hole of 22 so faceted is drawn as a circle.</summary>
        internal const int Sides = 24;

        /// <summary>
        /// How far from a hole's axis, in its radius, the corners of a part are taken as the hole's: Tekla's facets of a
        /// round hole stand on its circle, and a hole is never that close to a part's edge.
        /// </summary>
        internal const double HoleReach = 1.1;

        /// <summary>The attribute a bolt's or a hole's group is written to, as the drawing's marks read it.</summary>
        internal const string BoltGroupAttribute = "BoltGroup";

        /// <summary>The attribute the diameter of a bolt, or of a hole, is written to, as the drawing's marks read it.</summary>
        internal const string DiameterAttribute = "Diameter";

        /// <summary>The attribute a bolt's length is written to, as the drawing's marks read it.</summary>
        internal const string LengthAttribute = "Length";

        /// <summary>The attribute the diameter of a bolt's hole is written to.</summary>
        internal const string HoleDiameterAttribute = "HoleDiameter";

        /// <summary>The attribute the number of bolts or holes in the group is written to.</summary>
        internal const string CountAttribute = "Count";

        /// <summary>The attribute the bolt standard is written to.</summary>
        internal const string StandardAttribute = "Standard";

        /// <summary>The attribute the GUIDs of the parts a bolt group bolts are written to, in ordinal order, comma between.</summary>
        internal const string BoltedPartsAttribute = "BoltedParts";

        /// <summary>
        /// Makes the source: each object once, by its id; the parts, then a bolt or a hole for each position of a bolt
        /// group, then the welds; in the frame of the part given, or by the rule of <see cref="DrawingSource"/> where it
        /// was not read. Writes one Debug line for what was left out and one Info line for the call.
        /// </summary>
        /// <param name="data">What was read.</param>
        /// <param name="frameOwnerId">The id of the part whose frame the source takes; null for the rule of the source.</param>
        /// <param name="call">The name of the public call, for the log.</param>
        /// <param name="given">What the call was given, for the log: "assembly ...", "part ...", "12 model objects".</param>
        /// <param name="tolerance">The tolerance.</param>
        internal static DrawingSource Build(TeklaDrawingData data, string frameOwnerId, string call, string given, Tolerance tolerance)
        {
            var objects = new List<DrawingObject>();
            var bodies = new Dictionary<string, GeoSolid3>(StringComparer.Ordinal);
            GeoCoordinateSystem3? frame = null;

            foreach (TeklaPartData part in Unique(data.Parts, p => p.Id))
            {
                if (part.Solid == null)
                {
                    data.Skip("parts whose solid could not be read", part.Id);
                    continue;
                }

                if (!part.Frame.HasValue)
                {
                    data.Skip("parts with no coordinate system", part.Id);
                    continue;
                }

                objects.Add(PartObject(part));
                bodies.Add(part.Id, part.Solid);

                if (string.Equals(part.Id, frameOwnerId, StringComparison.Ordinal))
                {
                    frame = part.Frame.Value;
                }
            }

            foreach (TeklaBoltGroupData group in Unique(data.BoltGroups, g => g.Id))
            {
                objects.AddRange(BoltObjects(group, bodies, data, tolerance));
            }

            foreach (TeklaWeldData weld in Unique(data.Welds, w => w.Id))
            {
                if (weld.Solid == null)
                {
                    data.Skip("welds whose solid could not be read", weld.Id);
                    continue;
                }

                objects.Add(WeldObject(weld));
            }

            if (data.SkippedCount > 0)
            {
                GeometryHelperLog.Debug(string.Format(CultureInfo.InvariantCulture, "{0}: left out {1}: {2}.", call, data.SkippedCount, data.DescribeSkipped()));
            }

            var counts = new int[Enum.GetValues(typeof(DrawingObjectKind)).Length];

            foreach (DrawingObject item in objects)
            {
                counts[(int)item.Kind]++;
            }

            GeometryHelperLog.Info(string.Format(
                CultureInfo.InvariantCulture,
                "{0}: {1} objects ({2} parts, {3} bolts, {4} holes, {5} welds) from {6}, {7} left out.",
                call,
                objects.Count,
                counts[(int)DrawingObjectKind.Part],
                counts[(int)DrawingObjectKind.Bolt],
                counts[(int)DrawingObjectKind.Hole],
                counts[(int)DrawingObjectKind.Weld],
                given,
                data.SkippedCount));

            return frame.HasValue ? new DrawingSource(objects, frame.Value) : new DrawingSource(objects);
        }

        /// <summary>
        /// The object of a part: its name, mark (PART_POS), profile and material, its assembly as its parent, and the
        /// report properties ASSEMBLY_POS, LENGTH and WEIGHT as attributes under those names, where Tekla gave them.
        /// </summary>
        internal static DrawingObject PartObject(TeklaPartData part)
        {
            var attributes = new List<KeyValuePair<string, string>>();
            AddText(attributes, "ASSEMBLY_POS", part.AssemblyPosition);

            if (part.Length.HasValue)
            {
                attributes.Add(new KeyValuePair<string, string>("LENGTH", Number(part.Length.Value)));
            }

            if (part.Weight.HasValue)
            {
                attributes.Add(new KeyValuePair<string, string>("WEIGHT", Number(part.Weight.Value)));
            }

            return new DrawingObject(
                part.Id,
                DrawingObjectKind.Part,
                part.Solid,
                part.Frame.Value,
                part.Name,
                part.Mark,
                part.Profile,
                part.Material,
                part.AssemblyId,
                part.IsMain,
                attributes);
        }

        /// <summary>
        /// The objects of a bolt group: a bolt for each position, or a hole where the group has no bolts, each a cylinder
        /// of 24 sides along the group's Z, as long as the body Tekla draws for the bolts reaches along that axis, or, for
        /// a hole or where that body gives nothing, as the hole runs through the parts bolted that are in the source.
        /// </summary>
        /// <param name="group">The bolt group.</param>
        /// <param name="bodies">The bodies of the parts in the source, by id.</param>
        /// <param name="data">Where a bolt or a hole left out is counted.</param>
        /// <param name="tolerance">The tolerance.</param>
        internal static List<DrawingObject> BoltObjects(TeklaBoltGroupData group, IReadOnlyDictionary<string, GeoSolid3> bodies, TeklaDrawingData data, Tolerance tolerance)
        {
            var objects = new List<DrawingObject>();
            double holeDiameter = group.Diameter + group.HoleTolerance;
            double radius = 0.5 * (group.HasBolt ? group.Diameter : holeDiameter);
            string what = group.HasBolt ? "bolts" : "holes";

            if (group.Positions.Count == 0)
            {
                data.Skip("bolt groups with no positions", group.Id);
                return objects;
            }

            if (!(radius > tolerance.EqualPoint))
            {
                data.Skip("bolt groups of no diameter", group.Id);
                return objects;
            }

            List<GeoPoint3> partCorners = PartCorners(group.BoltedPartIds, bodies);
            GeoVector3 axis = group.Frame.ZAxis;
            string[] bolted = Sorted(group.BoltedPartIds);

            for (int i = 0; i < group.Positions.Count; i++)
            {
                GeoPoint3 position = group.Positions[i];
                string id = group.Id + "-" + (i + 1).ToString(CultureInfo.InvariantCulture);
                double min = 0.0, max = 0.0;
                bool found = group.HasBolt && TrySpan(position, axis, group.Diameter, group.BodyCorners, out min, out max);

                if (!found)
                {
                    found = TryAxisSpan(position, axis, SortedBodies(group.BoltedPartIds, bodies), tolerance, out min, out max);
                }

                if (!found)
                {
                    found = TrySpan(position, axis, HoleReach * 0.5 * holeDiameter, partCorners, out min, out max);
                }

                if (!found || !(max - min > tolerance.EqualPoint))
                {
                    data.Skip(what + " of no length along their axis", id);
                    continue;
                }

                if (group.HasBolt && group.Length > 0.0 && max - min > group.Length + tolerance.EqualPoint)
                {
                    // The axis can run on through a part that lies the same way (a bolt into a column end): the bolt is no longer
                    // than Tekla says, from the end of the run nearest its position.
                    if (Math.Abs(min) <= Math.Abs(max))
                    {
                        max = min + group.Length;
                    }
                    else
                    {
                        min = max - group.Length;
                    }
                }

                GeoPoint3 start = position.Add(axis.Multiply(min));
                GeoPoint3 end = position.Add(axis.Multiply(max));
                GeoSolid3 body = GeoSolid3.Cylinder(start, end, radius, Sides, tolerance);
                var frame = new GeoCoordinateSystem3(start, axis, group.Frame.XAxis);

                var attributes = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>(BoltGroupAttribute, group.Id),
                    new KeyValuePair<string, string>(CountAttribute, group.Positions.Count.ToString(CultureInfo.InvariantCulture)),
                    new KeyValuePair<string, string>(DiameterAttribute, Number(2.0 * radius)),
                    new KeyValuePair<string, string>(HoleDiameterAttribute, Number(holeDiameter)),
                };

                if (group.HasBolt && group.Length > 0.0)
                {
                    attributes.Add(new KeyValuePair<string, string>(LengthAttribute, Number(group.Length)));
                }

                if (group.HasBolt)
                {
                    AddText(attributes, StandardAttribute, group.Standard);
                }

                if (bolted.Length > 0)
                {
                    attributes.Add(new KeyValuePair<string, string>(BoltedPartsAttribute, string.Join(",", bolted)));
                }

                objects.Add(new DrawingObject(
                    id,
                    group.HasBolt ? DrawingObjectKind.Bolt : DrawingObjectKind.Hole,
                    body,
                    frame,
                    name: group.HasBolt ? "BOLT" : "HOLE",
                    parentId: group.PartToBoltToId,
                    attributes: attributes));
            }

            return objects;
        }

        /// <summary>
        /// The object of a weld: its body, its frame or the world's where it has none that holds, the part welded to as
        /// its parent, and what the weld's symbol reads (see <see cref="WeldSymbol"/>).
        /// </summary>
        internal static DrawingObject WeldObject(TeklaWeldData weld)
        {
            return new DrawingObject(
                weld.Id,
                DrawingObjectKind.Weld,
                weld.Solid,
                weld.Frame ?? GeoCoordinateSystem3.Global,
                name: "WELD",
                parentId: weld.MainPartId,
                attributes: WeldAttributes(weld));
        }

        /// <summary>
        /// What a weld's symbol reads. Tekla's side above the line is taken as the arrow side and the side below as the
        /// other: a weld with a kind above and below is on both sides, and its kind, size and length are those above. The
        /// four kinds the symbol draws are written by the names of <see cref="WeldType"/>, any other by Tekla's own name,
        /// such as "WELD_TYPE_PLUG", which the symbol leaves out and logs. The size is the prefix and the size, "a5"; the
        /// length is the side's own, or where that is nought, as for a weld along the whole joint, Tekla's LENGTH.
        /// </summary>
        internal static List<KeyValuePair<string, string>> WeldAttributes(TeklaWeldData weld)
        {
            var attributes = new List<KeyValuePair<string, string>>();
            bool above = HasKind(weld.TypeAbove);
            bool below = HasKind(weld.TypeBelow);

            if (above || below)
            {
                WeldSide side = above && below ? WeldSide.Both : above ? WeldSide.Arrow : WeldSide.Other;
                string type = above ? weld.TypeAbove : weld.TypeBelow;
                double size = above ? weld.SizeAbove : weld.SizeBelow;
                double length = above ? weld.LengthAbove : weld.LengthBelow;
                string prefix = (above ? weld.PrefixAbove : weld.PrefixBelow) ?? string.Empty;

                attributes.Add(new KeyValuePair<string, string>(WeldSymbol.TypeAttribute, WeldTypeName(type)));
                attributes.Add(new KeyValuePair<string, string>(WeldSymbol.SideAttribute, side.ToString()));

                if (size > 0.0)
                {
                    attributes.Add(new KeyValuePair<string, string>(WeldSymbol.SizeAttribute, prefix.Trim() + Number(size)));
                }

                if (length > 0.0)
                {
                    attributes.Add(new KeyValuePair<string, string>(WeldSymbol.LengthAttribute, Number(length)));
                }
                else if (weld.Length.HasValue && weld.Length.Value > 0.0)
                {
                    attributes.Add(new KeyValuePair<string, string>(WeldSymbol.LengthAttribute, Number(weld.Length.Value)));
                }
            }

            AddText(attributes, WeldSymbol.SecondaryPartAttribute, weld.SecondaryPartId);
            return attributes;
        }

        /// <summary>
        /// The name a Tekla weld kind is written by: Fillet, SquareButt, VButt or BevelButt for the four the drawing's weld
        /// symbol draws, Tekla's own name for any other.
        /// </summary>
        internal static string WeldTypeName(string teklaName)
        {
            switch (teklaName)
            {
                case "WELD_TYPE_FILLET":
                    return WeldType.Fillet.ToString();
                case "WELD_TYPE_SQUARE_GROOVE_SQUARE_BUTT":
                    return WeldType.SquareButt.ToString();
                case "WELD_TYPE_BEVEL_GROOVE_SINGLE_V_BUTT":
                    return WeldType.VButt.ToString();
                case "WELD_TYPE_BEVEL_GROOVE_SINGLE_BEVEL_BUTT":
                    return WeldType.BevelButt.ToString();
                default:
                    return teklaName;
            }
        }

        /// <summary>
        /// The frame a bolt group is read with, from the axes Tekla gives its coordinate system. Tekla's X is the bolt's own
        /// axis (the way it runs through the parts, its length the length of the vector Tekla stores) and its Y points from
        /// the first position towards the second; the module wants X from the first position towards the second and Z along
        /// the bolts. Read from a running Tekla 2025: a group of four M20 with origin (7200, 29850, 7121), X (0, 0, -371) and
        /// Y (0, -1000, 0) comes out with Z straight down and X along -Y.
        /// </summary>
        /// <param name="origin">The origin of Tekla's coordinate system.</param>
        /// <param name="teklaX">Tekla's X axis: along the bolt, of any length.</param>
        /// <param name="teklaY">Tekla's Y axis: towards the second position, of any length.</param>
        /// <returns>The frame; null where the axes are of no length or lie along one another.</returns>
        internal static GeoCoordinateSystem3? BoltFrame(GeoPoint3 origin, GeoVector3 teklaX, GeoVector3 teklaY)
        {
            if (!(teklaX.Length > 0.0) || !(teklaY.Length > 0.0))
            {
                return null;
            }

            GeoVector3 along = teklaX.Multiply(1.0 / teklaX.Length);
            GeoVector3 toSecond = teklaY.Multiply(1.0 / teklaY.Length);
            GeoVector3 across = along.CrossProduct(toSecond);

            if (!(across.Length > 1E-9))
            {
                return null;
            }

            var frame = new GeoCoordinateSystem3(origin, toSecond, across.Multiply(1.0 / across.Length));
            return frame.IsValid ? frame : (GeoCoordinateSystem3?)null;
        }

        /// <summary>
        /// How far along an axis through a point the corners within a reach of that axis run, from the point; false where
        /// no corner is within the reach.
        /// </summary>
        internal static bool TrySpan(GeoPoint3 position, GeoVector3 axis, double reach, IEnumerable<GeoPoint3> corners, out double min, out double max)
        {
            min = double.PositiveInfinity;
            max = double.NegativeInfinity;

            foreach (GeoPoint3 corner in corners)
            {
                GeoVector3 from = position.GetVectorTo(corner);
                double along = axis.DotProduct(from);
                GeoVector3 across = from.Subtract(axis.Multiply(along));

                if (across.Length <= reach)
                {
                    min = Math.Min(min, along);
                    max = Math.Max(max, along);
                }
            }

            return min <= max;
        }

        /// <summary>
        /// A number as a text, to a hundredth, with no trailing zeros, whatever the culture: 2999.996 is "3000", 22.5 is
        /// "22.5".
        /// </summary>
        internal static string Number(double value)
        {
            double rounded = Math.Round(value * 100.0, MidpointRounding.AwayFromZero) / 100.0;
            string written = rounded.ToString("0.##", CultureInfo.InvariantCulture);
            return written == "-0" ? "0" : written;
        }

        /// <summary>
        /// The first of each id, in the ordinal order of the ids, so that an object read twice is made once and nothing
        /// follows the order Tekla gave.
        /// </summary>
        internal static List<T> Unique<T>(IEnumerable<T> items, Func<T, string> idOf)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var unique = new List<T>();

            foreach (T item in items)
            {
                if (seen.Add(idOf(item)))
                {
                    unique.Add(item);
                }
            }

            // The ids are unique now, so their order is a full order.
            unique.Sort((a, b) => string.CompareOrdinal(idOf(a), idOf(b)));
            return unique;
        }

        private static bool HasKind(string teklaName)
            => !string.IsNullOrEmpty(teklaName) && !string.Equals(teklaName, "WELD_TYPE_NONE", StringComparison.Ordinal);

        private static void AddText(List<KeyValuePair<string, string>> attributes, string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                attributes.Add(new KeyValuePair<string, string>(name, value));
            }
        }

        private static string[] Sorted(IReadOnlyList<string> ids)
        {
            var unique = new SortedSet<string>(StringComparer.Ordinal);

            foreach (string id in ids)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    unique.Add(id);
                }
            }

            var sorted = new string[unique.Count];
            unique.CopyTo(sorted);
            return sorted;
        }

        /// <summary>Every corner of the bodies of the parts bolted that are in the source, their holes' corners among them.</summary>
        private static List<GeoSolid3> SortedBodies(IReadOnlyList<string> boltedPartIds, IReadOnlyDictionary<string, GeoSolid3> bodies)
        {
            var solids = new List<GeoSolid3>();

            foreach (string id in Sorted(boltedPartIds))
            {
                if (bodies.TryGetValue(id, out GeoSolid3 body))
                {
                    solids.Add(body);
                }
            }

            return solids;
        }

        /// <summary>
        /// Where the bolt's axis runs through the material of the parts it bolts: the line through a position along the axis
        /// is cut by each part, the crossings are paired as in and out, the stretches inside are joined where they touch (a
        /// stack of plates is one run), and the run nearest the position is the bolt's. Unlike the corners near the axis
        /// (<see cref="TrySpan"/>), this stays right where a part runs the same way as the bolt: a column the bolt goes down
        /// has corners along its whole height near the axis, but the axis is inside it only for the flange.
        /// </summary>
        /// <param name="position">The bolt's position on its axis.</param>
        /// <param name="axis">The unit direction of the axis.</param>
        /// <param name="solids">The bodies of the parts bolted.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="min">Where the run starts along the axis from the position.</param>
        /// <param name="max">Where it ends.</param>
        /// <returns>False where the axis meets no material.</returns>
        internal static bool TryAxisSpan(GeoPoint3 position, GeoVector3 axis, IReadOnlyList<GeoSolid3> solids, Tolerance tolerance, out double min, out double max)
        {
            min = 0.0;
            max = 0.0;
            var runs = new List<double[]>();
            GeoXLine3 line = new GeoLine3(position, position.Add(axis)).ToXLine();

            foreach (GeoSolid3 solid in solids)
            {
                GeoPoint3[] hits = solid.GetIntersections(line, tolerance);
                var ts = new List<double>();

                foreach (GeoPoint3 hit in hits)
                {
                    ts.Add(axis.DotProduct(position.GetVectorTo(hit)));
                }

                ts.Sort();
                var distinct = new List<double>();

                foreach (double t in ts)
                {
                    if (distinct.Count == 0 || t - distinct[distinct.Count - 1] > tolerance.EqualPoint)
                    {
                        distinct.Add(t);
                    }
                }

                for (int i = 0; i + 1 < distinct.Count; i += 2)
                {
                    runs.Add(new[] { distinct[i], distinct[i + 1] });
                }
            }

            if (runs.Count == 0)
            {
                return false;
            }

            runs.Sort((a, b) =>
            {
                int byStart = a[0].CompareTo(b[0]);
                return byStart != 0 ? byStart : a[1].CompareTo(b[1]);
            });

            var merged = new List<double[]>();

            foreach (double[] run in runs)
            {
                double[] last = merged.Count == 0 ? null : merged[merged.Count - 1];

                if (last != null && run[0] <= last[1] + tolerance.EqualPoint)
                {
                    last[1] = Math.Max(last[1], run[1]);
                }
                else
                {
                    merged.Add(new[] { run[0], run[1] });
                }
            }

            double best = double.PositiveInfinity;

            foreach (double[] run in merged)
            {
                double distance = run[0] <= 0.0 && run[1] >= 0.0 ? 0.0 : Math.Min(Math.Abs(run[0]), Math.Abs(run[1]));

                if (distance < best)
                {
                    best = distance;
                    min = run[0];
                    max = run[1];
                }
            }

            return true;
        }

        private static List<GeoPoint3> PartCorners(IReadOnlyList<string> boltedPartIds, IReadOnlyDictionary<string, GeoSolid3> bodies)
        {
            var corners = new List<GeoPoint3>();

            foreach (string id in Sorted(boltedPartIds))
            {
                if (!bodies.TryGetValue(id, out GeoSolid3 body))
                {
                    continue;
                }

                foreach (GeoFace3 face in body.Faces)
                {
                    corners.AddRange(face.Boundary.Vertices);

                    foreach (GeoPolygon3 hole in face.Holes)
                    {
                        corners.AddRange(hole.Vertices);
                    }
                }
            }

            return corners;
        }
    }
}

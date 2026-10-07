using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Geometry;
using GeometryHelper.Takeoff;

namespace GeometryHelper.UnitTest.Takeoff
{
    /// <summary>
    /// What the tests of the volume takeoff share: boxes lined up with the axes, each built as the library builds a box, an
    /// oracle that works out by itself, with no call into the library, what each box keeps, and the check that holds a run
    /// to the oracle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The oracle cuts space into cells by every x, y and z any box or opening has, so that each cell lies wholly inside
    /// or wholly outside each box. A cell belongs to the highest-ranked box whose material holds it, the rank being
    /// (Priority descending, an Id before none, the larger Id first, index ascending), as the takeoff ranks its items. A box's net volume is the sum of the cells it owns,
    /// what it gives up to box k the sum of its cells k owns, and the union the sum of every cell owned at all.
    /// </para>
    /// <para>
    /// The corners are whole millimetres, so every cell is a whole number of cubic millimetres, at most some 10^10, and
    /// every sum here is exact: the oracle is the answer to the last digit, not an estimate of it.
    /// </para>
    /// </remarks>
    internal static class VolumeTakeoffTestKit
    {
        /// <summary>
        /// How far a run's volumes may stand from the oracle's, as a share of the item's gross volume: a part of a
        /// billion, as SPEC D14 and A10 put it.
        /// </summary>
        internal const double Relative = 1E-9;

        #region Boxes for the volume takeoff, built by the library's own box

        /// <summary>
        /// An axis-aligned box from (x0, y0, z0) to (x1, y1, z1), with any openings it carries, each a box itself.
        /// </summary>
        internal sealed class Box
        {
            internal Box(double x0, double y0, double z0, double x1, double y1, double z1, params Box[] openings)
            {
                Min = new[] { x0, y0, z0 };
                Max = new[] { x1, y1, z1 };
                Openings = openings ?? new Box[0];
            }

            internal double[] Min { get; }

            internal double[] Max { get; }

            internal IReadOnlyList<Box> Openings { get; }

            /// <summary>The box as a solid: <see cref="GeoObb3.ToSolid"/> of the box, its openings carried as openings.</summary>
            internal GeoSolid3 ToSolid()
            {
                GeoSolid3 solid = Outline(this);
                return Openings.Count == 0 ? solid : solid.WithOpenings(Openings.Select(Outline));
            }

            /// <summary>Whether the material holds a point: inside the box and inside none of its openings.</summary>
            internal bool Holds(double x, double y, double z)
                => Inside(this, x, y, z) && !Openings.Any(opening => Inside(opening, x, y, z));

            public override string ToString()
            {
                string text = string.Format(CultureInfo.InvariantCulture, "[{0} {1} {2} - {3} {4} {5}]", Min[0], Min[1], Min[2], Max[0], Max[1], Max[2]);
                return Openings.Count == 0 ? text : text + " less " + string.Join(", ", Openings);
            }

            private static GeoSolid3 Outline(Box box)
                => new GeoAabb3(new GeoPoint3(box.Min[0], box.Min[1], box.Min[2]), new GeoPoint3(box.Max[0], box.Max[1], box.Max[2])).ToObb().ToSolid();

            // Strictly inside: the oracle only asks at the middle of a cell, which never lies on a face.
            private static bool Inside(Box box, double x, double y, double z)
                => box.Min[0] < x && x < box.Max[0] && box.Min[1] < y && y < box.Max[1] && box.Min[2] < z && z < box.Max[2];
        }

        /// <summary>
        /// A set of boxes to take off, each with its priority and, where <see cref="Ids"/> gives one, its id; a null box stands
        /// for a null entry in the list of items. <see cref="SameInstanceAs"/> says, for each box, the index of an earlier box
        /// whose very solid it is to share, or -1.
        /// </summary>
        internal sealed class BoxSet
        {
            internal BoxSet(IReadOnlyList<Box> boxes, IReadOnlyList<int> priorities, IReadOnlyList<int> sameInstanceAs = null, IReadOnlyList<int?> ids = null)
            {
                Boxes = boxes;
                Priorities = priorities;
                SameInstanceAs = sameInstanceAs ?? boxes.Select(_ => -1).ToArray();
                Ids = ids ?? boxes.Select(_ => (int?)null).ToArray();
            }

            internal IReadOnlyList<Box> Boxes { get; }

            internal IReadOnlyList<int> Priorities { get; }

            internal IReadOnlyList<int> SameInstanceAs { get; }

            /// <summary>The id of each item, or null where it is built without one, by the constructor of three arguments.</summary>
            internal IReadOnlyList<int?> Ids { get; }

            /// <summary>The items to run: one per box, named by its index, with its id where it has one, null where the box is null.</summary>
            internal VolumeItem[] ToItems()
            {
                var solids = new GeoSolid3[Boxes.Count];
                var items = new VolumeItem[Boxes.Count];

                for (int i = 0; i < Boxes.Count; i++)
                {
                    if (Boxes[i] == null)
                    {
                        continue;
                    }

                    solids[i] = SameInstanceAs[i] >= 0 ? solids[SameInstanceAs[i]] : Boxes[i].ToSolid();
                    string name = "#" + i.ToString(CultureInfo.InvariantCulture);
                    items[i] = Ids[i].HasValue ? new VolumeItem(solids[i], name, Priorities[i], Ids[i].Value) : new VolumeItem(solids[i], name, Priorities[i]);
                }

                return items;
            }

            public override string ToString()
            {
                var text = new StringBuilder();
                for (int i = 0; i < Boxes.Count; i++)
                {
                    text.AppendFormat(CultureInfo.InvariantCulture, "  #{0} priority {1}{2}{3}: {4}\n", i, Priorities[i], Ids[i].HasValue ? " id " + Ids[i].Value.ToString(CultureInfo.InvariantCulture) : "", SameInstanceAs[i] >= 0 ? " (the solid of #" + SameInstanceAs[i] + ")" : "", Boxes[i]?.ToString() ?? "null");
                }

                return text.ToString();
            }
        }

        /// <summary>
        /// A random set for the fuzz, from its seed: 2 to 12 boxes with corners on a grid 100 apart in a cube 600 across,
        /// each 1 to 4 steps long on every axis, so that flush faces are common; one in four a copy of an earlier box,
        /// half of those its very solid; priorities from -2 to 2, so that equal priorities are common too.
        /// </summary>
        internal static BoxSet RandomSet(int seed)
        {
            var random = new Random(seed);
            int count = random.Next(2, 13);
            var boxes = new Box[count];
            var priorities = new int[count];
            var same = new int[count];

            for (int i = 0; i < count; i++)
            {
                priorities[i] = random.Next(-2, 3);
                same[i] = -1;

                if (i > 0 && random.Next(4) == 0)
                {
                    int of = random.Next(i);
                    boxes[i] = boxes[of];
                    same[i] = random.Next(2) == 0 ? of : -1;
                    continue;
                }

                var min = new double[3];
                var max = new double[3];
                for (int axis = 0; axis < 3; axis++)
                {
                    int from = random.Next(0, 6);
                    int to = Math.Min(6, from + random.Next(1, 5));
                    min[axis] = 100.0 * from;
                    max[axis] = 100.0 * to;
                }

                boxes[i] = new Box(min[0], min[1], min[2], max[0], max[1], max[2]);
            }

            return new BoxSet(boxes, priorities, same);
        }

        /// <summary>
        /// The set of <see cref="RandomSet"/> for the seed with an id given to each item, from a second stream of the same
        /// seed: one in four none, the rest one of <see cref="int.MinValue"/>, -1, 0, 1, 2 and <see cref="int.MaxValue"/>,
        /// so that equal ids, and items with an id beside items without, are common within one priority.
        /// </summary>
        internal static BoxSet RandomSetWithIds(int seed)
        {
            BoxSet set = RandomSet(seed);
            var random = new Random(~seed);
            int[] choices = { int.MinValue, -1, 0, 1, 2, int.MaxValue };
            var ids = new int?[set.Boxes.Count];

            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = random.Next(4) == 0 ? (int?)null : choices[random.Next(choices.Length)];
            }

            return new BoxSet(set.Boxes, set.Priorities, set.SameInstanceAs, ids);
        }

        /// <summary>
        /// A random set with openings as well, from its seed: 2 to 12 boxes laid as <see cref="RandomSet"/> lays them, one in
        /// three with an opening as <see cref="RandomSliceSet"/> gives one, flush with its faces or running past them and never
        /// all of it; one in four a copy of an earlier box, half of those its very solid; priorities from -2 to 2; and ids as
        /// <see cref="RandomSetWithIds"/> gives them.
        /// </summary>
        internal static BoxSet RandomSetWithOpenings(int seed)
        {
            var random = new Random(seed);
            int count = random.Next(2, 13);
            var boxes = new Box[count];
            var priorities = new int[count];
            var same = new int[count];
            var ids = new int?[count];
            int[] choices = { int.MinValue, -1, 0, 1, 2, int.MaxValue };

            for (int i = 0; i < count; i++)
            {
                priorities[i] = random.Next(-2, 3);
                ids[i] = random.Next(4) == 0 ? (int?)null : choices[random.Next(choices.Length)];
                same[i] = -1;

                if (i > 0 && random.Next(4) == 0)
                {
                    int of = random.Next(i);
                    boxes[i] = boxes[of];
                    same[i] = random.Next(2) == 0 ? of : -1;
                    continue;
                }

                boxes[i] = RandomBox(random);
            }

            return new BoxSet(boxes, priorities, same, ids);
        }

        #endregion

        #region The sets of the fixed cases

        /// <summary>
        /// A slab, a beam and a column meeting at a joint, listed slab first: the slab 3 000 by 3 000 by 200 at the top,
        /// priority 1; the beam 3 000 long, 400 wide and 400 deep under it, its top flush with the slab's, priority 2; and
        /// the column 300 by 400 and 1 000 high, its top flush too and its sides flush with the beam's, priority 3.
        /// </summary>
        /// <remarks>
        /// The block all three share is 300 by 400 by 200, 24 000 000. The column keeps all of itself, 120 000 000. The
        /// beam gives the column 300 by 400 by 400, 48 000 000, and keeps 432 000 000. The slab gives the column the
        /// shared block and the beam the rest of what lies over it, 3 000 by 400 by 200 less the block, 216 000 000, and
        /// keeps 1 800 000 000 less 240 000 000, 1 560 000 000. The union is 2 112 000 000.
        /// </remarks>
        internal static BoxSet Joint() => new BoxSet(
            new[]
            {
                new Box(-1000, -1000, 800, 2000, 2000, 1000),
                new Box(-1000, 0, 600, 2000, 400, 1000),
                new Box(0, 0, 0, 300, 400, 1000),
            },
            new[] { 1, 2, 3 });

        /// <summary>
        /// Four cubes 1 000 across, of one priority, laid 800 apart in a square so that each pair beside each other shares
        /// a slab 200 thick and all four share the post 200 by 200 by 1 000 at the middle.
        /// </summary>
        /// <remarks>
        /// In list order: the first keeps 1 000 000 000. The second gives the first 200 000 000. The third gives the
        /// first 200 000 000, and the second nothing, since all it shares with the second is the post the first has
        /// taken. The fourth gives the first the post, 40 000 000, and the second and the third 160 000 000 each, keeping
        /// 640 000 000. The union is the square 1 800 across, 3 240 000 000.
        /// </remarks>
        internal static BoxSet FourAtACorner() => new BoxSet(
            new[]
            {
                new Box(0, 0, 0, 1000, 1000, 1000),
                new Box(800, 0, 0, 1800, 1000, 1000),
                new Box(0, 800, 0, 1000, 1800, 1000),
                new Box(800, 800, 0, 1800, 1800, 1000),
            },
            new[] { 0, 0, 0, 0 });

        /// <summary>
        /// Three copies of a block 1 000 by 500 by 300, 150 000 000, of one priority, each a solid of its own.
        /// </summary>
        internal static BoxSet ThreeCopies() => new BoxSet(
            new[] { new Box(0, 0, 0, 1000, 500, 300), new Box(0, 0, 0, 1000, 500, 300), new Box(0, 0, 0, 1000, 500, 300) },
            new[] { 0, 0, 0 });

        /// <summary>
        /// A block 300 across, 27 000 000, held wholly inside a cube 1 000 across clear of its faces, the block listed
        /// first; the priorities as given.
        /// </summary>
        internal static BoxSet BlockInACube(int blockPriority, int cubePriority) => new BoxSet(
            new[] { new Box(200, 300, 400, 500, 600, 700), new Box(0, 0, 0, 1000, 1000, 1000) },
            new[] { blockPriority, cubePriority });

        /// <summary>
        /// A cube 1 000 across with four more about it touching it only: one flush against its face at x = 1 000, one
        /// along its edge at x = y = 1 000, one at its corner, and a post 400 by 400 standing on its top; all of one
        /// priority, the cube first.
        /// </summary>
        internal static BoxSet Touching() => new BoxSet(
            new[]
            {
                new Box(0, 0, 0, 1000, 1000, 1000),
                new Box(1000, 0, 0, 2000, 1000, 1000),
                new Box(1000, 1000, 0, 2000, 2000, 1000),
                new Box(1000, 1000, 1000, 2000, 2000, 2000),
                new Box(200, 200, 1000, 600, 600, 1400),
            },
            new[] { 0, 0, 0, 0, 0 });

        /// <summary>
        /// Two cubes 1 000 across, the second moved 500 along x, so that they share half of each, 500 000 000; the
        /// priorities as given.
        /// </summary>
        internal static BoxSet HalfOverlap(int firstPriority, int secondPriority) => new BoxSet(
            new[] { new Box(0, 0, 0, 1000, 1000, 1000), new Box(500, 0, 0, 1500, 1000, 1000) },
            new[] { firstPriority, secondPriority });

        /// <summary>
        /// The two cubes of <see cref="HalfOverlap"/>, the first of priority <see cref="int.MinValue"/> and the second of
        /// <see cref="int.MaxValue"/>, and a post 500 by 500 by 2 000 of priority 0 standing through both where they meet.
        /// </summary>
        /// <remarks>
        /// Ranked: the second cube, the post, the first cube. The second keeps 1 000 000 000. The post gives it 250 by 500 by
        /// 1 000, 125 000 000, and keeps 375 000 000. The first gives the second 500 000 000 and the post the other
        /// 125 000 000 it holds of it, keeping 375 000 000. The union is 1 750 000 000.
        /// </remarks>
        internal static BoxSet ExtremePriorities() => new BoxSet(
            new[] { new Box(0, 0, 0, 1000, 1000, 1000), new Box(500, 0, 0, 1500, 1000, 1000), new Box(250, 250, -500, 750, 750, 1500) },
            new[] { int.MinValue, int.MaxValue, 0 });

        /// <summary>
        /// A slab 3 000 by 3 000 by 200, priority 0, with a duct 600 by 600 through it running 100 past both faces, and a
        /// column 400 by 400 by 2 000, priority 3, through the slab across a corner of the duct.
        /// </summary>
        /// <remarks>
        /// The slab's material is 1 800 000 000 less the duct's 72 000 000, 1 728 000 000. The column crosses 400 by 400
        /// of the slab, 32 000 000, of which 200 by 200 by 200, 8 000 000, is in the duct: the slab gives it 24 000 000 and
        /// keeps 1 704 000 000. The column keeps all of itself, 320 000 000.
        /// </remarks>
        internal static BoxSet ColumnThroughADuct() => new BoxSet(
            new[]
            {
                new Box(0, 0, 0, 3000, 3000, 200, new Box(1000, 1000, -100, 1600, 1600, 300)),
                new Box(1400, 1400, -1000, 1800, 1800, 1000),
            },
            new[] { 0, 3 });

        /// <summary>
        /// A hollow column, 400 by 400 by 2 000 with a duct 200 by 200 through its length running 100 past both ends,
        /// priority 3, through a slab 2 400 by 2 400 by 200, priority 0.
        /// </summary>
        /// <remarks>
        /// The column's material is 400 by 400 less 200 by 200, 120 000, by 2 000: 240 000 000. Of the slab's
        /// 1 152 000 000 it takes 120 000 by 200, 24 000 000; the slab keeps the 8 000 000 inside the duct, and
        /// 1 128 000 000 in all. The union is 1 368 000 000.
        /// </remarks>
        internal static BoxSet HollowColumnThroughASlab() => new BoxSet(
            new[]
            {
                new Box(-1000, -1000, 0, 1400, 1400, 200),
                new Box(0, 0, -1000, 400, 400, 1000, new Box(100, 100, -1100, 300, 300, 1100)),
            },
            new[] { 0, 3 });

        /// <summary>
        /// A cube 1 000 across with a corner notch 500 by 500 cut through its height, priority 0, and a block filling the
        /// notch flush, priority 1: their boxes overlap by 500 by 500 by 1 000, their material not at all.
        /// </summary>
        /// <remarks>The notched cube holds 750 000 000, the block 250 000 000.</remarks>
        internal static BoxSet BlockInANotch() => new BoxSet(
            new[]
            {
                new Box(0, 0, 0, 1000, 1000, 1000, new Box(500, 500, -100, 1100, 1100, 1100)),
                new Box(500, 500, 0, 1000, 1000, 1000),
            },
            new[] { 0, 1 });

        /// <summary>
        /// Two copies of a block 100 by 200 by 300, 6 000 000, of priority 0, and a bar 400 by 100 by 100 of priority 1
        /// running through both, 1 000 000 of each: the later copy is taken by two parts, the bar and the first copy.
        /// </summary>
        /// <remarks>
        /// The first copy gives the bar 1 000 000 and keeps 5 000 000. The later gives the bar 1 000 000 and the first
        /// copy the 5 000 000 left, keeping nothing. Taken off by hand at the pin, as the spec takes it, the later copy's
        /// two deductions come to 9.3E-10 more than its gross, a part in 10^16: its net clamps to nought on rounding.
        /// </remarks>
        internal static BoxSet CopiesWithABarThrough() => new BoxSet(
            new[] { new Box(0, 0, 0, 100, 200, 300), new Box(0, 0, 0, 100, 200, 300), new Box(0, 0, 100, 400, 100, 200) },
            new[] { 0, 0, 1 });

        #endregion

        #region Bodies not lined up with the axes, each with a volume known by hand

        /// <summary>
        /// A box sizeX by sizeY by sizeZ about a centre, its local x axis turned by the given angle about z from the world's
        /// and then leant by the given angle about that axis, so that its long side, along local z, leans away from upright.
        /// </summary>
        internal static GeoSolid3 TurnedBox(GeoPoint3 centre, double sizeX, double sizeY, double sizeZ, double turnRad, double leanRad)
        {
            var axisX = new GeoVector3(Math.Cos(turnRad), Math.Sin(turnRad), 0);
            var across = new GeoVector3(-Math.Sin(turnRad), Math.Cos(turnRad), 0);
            GeoVector3 axisY = across * Math.Cos(leanRad) + new GeoVector3(0, 0, Math.Sin(leanRad));
            return new GeoObb3(centre, sizeX, sizeY, sizeZ, axisX, axisY).ToSolid();
        }

        /// <summary>
        /// The tetrahedron with corners at the origin and at a along x, b along y and c along z: abc / 6, its section at
        /// height z a right triangle (1 - z / c)^2 of its base. With <paramref name="leaveOutHalfTheSlantedFace"/> its one
        /// face that is not on a plane of the axes is split at the middle of its bottom edge and the half towards y left
        /// out, so that it does not close.
        /// </summary>
        internal static GeoSolid3 Tetrahedron(double a, double b, double c, bool leaveOutHalfTheSlantedFace = false)
        {
            var o = new GeoPoint3(0, 0, 0);
            var x = new GeoPoint3(a, 0, 0);
            var y = new GeoPoint3(0, b, 0);
            var z = new GeoPoint3(0, 0, c);
            var faces = new List<GeoFace3>
            {
                new GeoFace3(new GeoPolygon3(new[] { o, y, x }, Tolerance.Default)),
                new GeoFace3(new GeoPolygon3(new[] { o, x, z }, Tolerance.Default)),
                new GeoFace3(new GeoPolygon3(new[] { o, z, y }, Tolerance.Default)),
            };

            var middle = new GeoPoint3(a / 2.0, b / 2.0, 0);
            faces.Add(new GeoFace3(new GeoPolygon3(new[] { x, middle, z }, Tolerance.Default)));
            if (!leaveOutHalfTheSlantedFace)
            {
                faces.Add(new GeoFace3(new GeoPolygon3(new[] { middle, y, z }, Tolerance.Default)));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// A block standing on the rectangle (x0, y0) to (x1, y1) in plan, from the plane z = below(x, y) to the plane
        /// z = above(x, y), each given as (a, b, c) for a + b x + c y: its sides upright, its top and bottom leaning as the
        /// planes do.
        /// </summary>
        internal static GeoSolid3 BlockBetweenPlanes(double x0, double y0, double x1, double y1, (double A, double B, double C) below, (double A, double B, double C) above)
        {
            double[][] plan = { new[] { x0, y0 }, new[] { x1, y0 }, new[] { x1, y1 }, new[] { x0, y1 } };
            GeoPoint3 Low(int k) => new GeoPoint3(plan[k][0], plan[k][1], below.A + below.B * plan[k][0] + below.C * plan[k][1]);
            GeoPoint3 High(int k) => new GeoPoint3(plan[k][0], plan[k][1], above.A + above.B * plan[k][0] + above.C * plan[k][1]);
            var faces = new List<GeoFace3>
            {
                new GeoFace3(new GeoPolygon3(new[] { Low(0), Low(3), Low(2), Low(1) }, Tolerance.Default)),
                new GeoFace3(new GeoPolygon3(new[] { High(0), High(1), High(2), High(3) }, Tolerance.Default)),
            };

            for (int k = 0; k < 4; k++)
            {
                int next = (k + 1) % 4;
                faces.Add(new GeoFace3(new GeoPolygon3(new[] { Low(k), Low(next), High(next), High(k) }, Tolerance.Default)));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// A wedge: the right triangle (0, 0, 0), (length, 0, 0), (0, 0, height) in the plane y = 0, swept width along y, so
        /// that its slanted face leans over x and every level cuts it in a rectangle narrower as it rises.
        /// </summary>
        internal static GeoSolid3 Wedge(double length, double height, double width)
        {
            var triangle = new GeoPolygon3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(length, 0, 0), new GeoPoint3(0, 0, height) }, Tolerance.Default);
            return GeoSolid3.Extrude(triangle, new GeoVector3(0, width, 0), Tolerance.Default);
        }

        #endregion

        #region Curved bands a hair apart: a wall and a girder that break the boolean, and what they share worked out by hand

        /// <summary>
        /// A curved band: in plan, between two arcs about the origin, its corners on the arcs at chords + 1 angles evenly
        /// from one angle to another; standing from a bottom to a top, each corner raised by rise × k / chords along it. A
        /// wobble moves the inner corners off their arc by turns, out at the even ones and in at the odd ones.
        /// </summary>
        /// <remarks>
        /// Each quad of its faces is split into two triangles, the top's and the bottom's along the same diagonal, so a top
        /// warped by the rise lies over a bottom warped alike: the band is as thick as its depth everywhere, and its sides
        /// stand upright.
        /// </remarks>
        internal sealed class CurvedBand
        {
            internal CurvedBand(double inner, double outer, double fromRad, double toRad, int chords, double bottom, double top, double rise, double wobble = 0.0)
            {
                Wobble = wobble;
                Inner = inner;
                Outer = outer;
                FromRad = fromRad;
                ToRad = toRad;
                Chords = chords;
                Bottom = bottom;
                Top = top;
                Rise = rise;
            }

            internal double Inner { get; }

            internal double Outer { get; }

            internal double FromRad { get; }

            internal double ToRad { get; }

            internal int Chords { get; }

            internal double Bottom { get; }

            internal double Top { get; }

            internal double Rise { get; }

            internal double Wobble { get; }

            internal double Depth => Top - Bottom;

            /// <summary>The band as a solid of triangles.</summary>
            internal GeoSolid3 ToSolid()
            {
                var faces = new List<GeoFace3>();

                void Add(double[] a, double[] b, double[] c)
                    => faces.Add(new GeoFace3(new GeoPolygon3(new[] { a, b, c }.Select(p => new GeoPoint3(p[0], p[1], p[2])), Tolerance.Default)));

                void Quad(double[] a, double[] b, double[] c, double[] d)
                {
                    Add(a, b, c);
                    Add(a, c, d);
                }

                for (int k = 0; k < Chords; k++)
                {
                    Quad(Corner(false, k, Top), Corner(false, k + 1, Top), Corner(true, k + 1, Top), Corner(true, k, Top));
                    Quad(Corner(false, k, Bottom), Corner(true, k, Bottom), Corner(true, k + 1, Bottom), Corner(false, k + 1, Bottom));
                    Quad(Corner(false, k, Bottom), Corner(false, k + 1, Bottom), Corner(false, k + 1, Top), Corner(false, k, Top));
                    Quad(Corner(true, k, Bottom), Corner(true, k, Top), Corner(true, k + 1, Top), Corner(true, k + 1, Bottom));
                }

                Quad(Corner(true, 0, Bottom), Corner(false, 0, Bottom), Corner(false, 0, Top), Corner(true, 0, Top));
                Quad(Corner(false, Chords, Bottom), Corner(true, Chords, Bottom), Corner(true, Chords, Top), Corner(false, Chords, Top));
                return new GeoSolid3(faces).TurnOutwards();
            }

            /// <summary>The triangles of its top, each as its three corners (x, y, z), split as <see cref="ToSolid"/> splits them.</summary>
            internal IReadOnlyList<double[][]> TopTriangles()
            {
                var triangles = new List<double[][]>();
                for (int k = 0; k < Chords; k++)
                {
                    triangles.Add(new[] { Corner(false, k, Top), Corner(false, k + 1, Top), Corner(true, k + 1, Top) });
                    triangles.Add(new[] { Corner(false, k, Top), Corner(true, k + 1, Top), Corner(true, k, Top) });
                }

                return triangles;
            }

            private double[] Corner(bool inner, int k, double z)
            {
                double radius = inner ? Inner + (k % 2 == 0 ? Wobble : -Wobble) : Outer;
                double angle = FromRad + (ToRad - FromRad) * k / Chords;
                return new[] { radius * Math.Cos(angle), radius * Math.Sin(angle), z + Rise * k / Chords };
            }
        }

        /// <summary>
        /// A curved wall and a girder on it whose sides and tops lie a hair apart, as a wall and a girder bent along one
        /// curve in a Tekla model do: the pair the boolean cannot make a valid common part of, nor a valid cut.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The wall is the band from radius 5 600 to 5 900, 17 degrees long in 9 chords, 3 600 high, rising 1.2 along its
        /// length, so its top leans 0.04 degrees. The girder is the band from radius 5 600.175 to 6 100.175, its ends 20
        /// inside the wall's, in 7 chords, 900 deep, its top 0.7 under the wall's at its start and rising 3, 0.1 degrees:
        /// its top crosses the wall's two fifths of the way along, the two 0.06 degrees apart. Its inner side lies 0.175
        /// outside the wall's at its corners, and the chords, which sag 0.76 on the wall and 1.20 on the girder, cross each
        /// other back and forth along the length.
        /// </para>
        /// <para>
        /// At 4dde2c6, with the boolean within a thousandth, and with a contact of a hundredth, the common part of the wall
        /// with the girder comes out of 224 faces, not valid and 247 526 short, and the cut of the wall by the girder of
        /// 1 744 faces, not valid either and 13 552 331 short: the takeoff can only say the overlap could not be worked out.
        /// The common part is not valid either with the girder's side 0.165 or 0.185 out and its top 0.65 or 0.75 under,
        /// nine cases in all; with the contact, eight of the nine. So the case does not hang on its numbers.
        /// </para>
        /// <para>
        /// In 9 and 7 chords only 54 pairs of their faces stand beside each other, too few for the takeoff to send the
        /// pair straight to slicing, so the boolean is tried and fails. Built in more chords, the same two have many more.
        /// </para>
        /// </remarks>
        internal static (CurvedBand Wall, CurvedBand Girder) CurvedWallAndGirder(int wallChords = 9, int girderChords = 7)
        {
            const double radius = 5600.0;
            double span = 17.0 * Math.PI / 180.0, inset = 20.0 / radius;
            var wall = new CurvedBand(radius, radius + 300.0, 0.0, span, wallChords, 0.0, 3600.0, 1.2);
            var girder = new CurvedBand(radius + 0.175, radius + 500.175, inset, span - inset, girderChords, 3600.0 - 0.7 - 900.0, 3600.0 - 0.7, 3.0);
            return (wall, girder);
        }

        /// <summary>
        /// A curved wall and a girder on it in the same 90 chords at the same angles, so that each face of the one has a
        /// face of the other exactly parallel beside it: the wall from radius 5 600 to 5 900, 17 degrees long and 3 600
        /// high; the girder from 5 600 plus the gap given to 6 100, 900 deep, its top 0.5 under the wall's, and its inner
        /// corners wobbled as given; neither rises.
        /// </summary>
        internal static (CurvedBand Wall, CurvedBand Girder) ParallelWallAndGirder(double gap, double wobble)
        {
            const double radius = 5600.0;
            double span = 17.0 * Math.PI / 180.0;
            var wall = new CurvedBand(radius, radius + 300.0, 0.0, span, 90, 0.0, 3600.0, 0.0);
            var girder = new CurvedBand(radius + gap, radius + 500.0, 0.0, span, 90, 3600.0 - 0.5 - 900.0, 3600.0 - 0.5, 0.0, wobble);
            return (wall, girder);
        }

        /// <summary>The area two convex polygons in plan share, each given by its corners in turn.</summary>
        internal static double PlanOverlap(IReadOnlyList<double[]> a, IReadOnlyList<double[]> b) => Math.Abs(Area(ClipToConvex(a.ToList(), b)));

        /// <summary>
        /// A column 1 000 by 400 by 4 000 standing across the middle of the curved wall and girder, its long side of plan
        /// along the radius at the middle angle, from radius 5 250 to 6 250, and its height from -200 to 3 800, through both
        /// wholly; its plan as four corners counter-clockwise.
        /// </summary>
        internal static (GeoSolid3 Solid, double[][] Plan) ColumnAcrossTheCurve()
        {
            double angle = 17.0 * Math.PI / 360.0, middle = 5750.0;
            double ux = Math.Cos(angle), uy = Math.Sin(angle), vx = -uy, vy = ux;
            double cx = middle * ux, cy = middle * uy;
            GeoSolid3 solid = new GeoObb3(new GeoPoint3(cx, cy, 1800.0), 1000.0, 400.0, 4000.0, new GeoVector3(ux, uy, 0.0), new GeoVector3(vx, vy, 0.0)).ToSolid();
            double[][] plan =
            {
                new[] { cx - 500.0 * ux - 200.0 * vx, cy - 500.0 * uy - 200.0 * vy },
                new[] { cx + 500.0 * ux - 200.0 * vx, cy + 500.0 * uy - 200.0 * vy },
                new[] { cx + 500.0 * ux + 200.0 * vx, cy + 500.0 * uy + 200.0 * vy },
                new[] { cx - 500.0 * ux + 200.0 * vx, cy - 500.0 * uy + 200.0 * vy },
            };
            return (solid, plan);
        }

        /// <summary>
        /// The volume of a band, within a convex plan if one is given: its depth times its plan, since its top and bottom
        /// are warped alike. A plan given must lie across the band's whole height, as the column's does.
        /// </summary>
        internal static double BandVolume(CurvedBand band, IReadOnlyList<double[]> within = null)
        {
            double area = 0.0;
            foreach (double[][] triangle in band.TopTriangles())
            {
                area += Math.Abs(Area(ClipToConvex(Plan(triangle), within)));
            }

            return band.Depth * area;
        }

        /// <summary>
        /// What a band shares with one standing on it whose bottom lies above the lower's everywhere, within a convex plan
        /// if one is given: over their common plan, the upper band's depth, less where its top stands above the lower's.
        /// </summary>
        /// <remarks>
        /// Each top triangle of one band is clipped by each of the other's and by the plan given. Over each piece both tops
        /// are planes, so the height of the upper's top over the lower's is linear: it is clipped to where it is above
        /// nought, and its integral is the piece's area times its value at the piece's centroid. Nothing here calls the
        /// library but to read the corners, so the overlap is exact to rounding.
        /// </remarks>
        internal static double CurvedOverlap(CurvedBand lower, CurvedBand upper, IReadOnlyList<double[]> within = null)
        {
            double area = 0.0, above = 0.0;
            foreach (double[][] low in lower.TopTriangles())
            {
                Func<double[], double> lowTop = PlaneOver(low);
                foreach (double[][] high in upper.TopTriangles())
                {
                    List<double[]> piece = ClipToConvex(ClipToConvex(Plan(low), Plan(high)), within);
                    if (piece.Count < 3)
                    {
                        continue;
                    }

                    area += Math.Abs(Area(piece));
                    Func<double[], double> highTop = PlaneOver(high);
                    above += Integral(KeepWhere(piece, p => highTop(p) - lowTop(p)), p => highTop(p) - lowTop(p));
                }
            }

            return upper.Depth * area - above;
        }

        private static List<double[]> Plan(double[][] corners) => corners.Select(c => new[] { c[0], c[1] }).ToList();

        // The height over (x, y) of the plane through a triangle's three corners.
        private static Func<double[], double> PlaneOver(double[][] t)
        {
            double ux = t[1][0] - t[0][0], uy = t[1][1] - t[0][1], uz = t[1][2] - t[0][2];
            double vx = t[2][0] - t[0][0], vy = t[2][1] - t[0][1], vz = t[2][2] - t[0][2];
            double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            return p => t[0][2] - (nx * (p[0] - t[0][0]) + ny * (p[1] - t[0][1])) / nz;
        }

        // The signed area of a polygon in plan, counter-clockwise positive.
        private static double Area(IReadOnlyList<double[]> polygon)
        {
            double twice = 0.0;
            for (int k = 0; k < polygon.Count; k++)
            {
                double[] a = polygon[k], b = polygon[(k + 1) % polygon.Count];
                twice += a[0] * b[1] - a[1] * b[0];
            }

            return twice / 2.0;
        }

        // The part of a convex polygon where a linear function is at or above nought.
        private static List<double[]> KeepWhere(List<double[]> polygon, Func<double[], double> f)
        {
            var kept = new List<double[]>();
            for (int k = 0; k < polygon.Count; k++)
            {
                double[] p = polygon[k], q = polygon[(k + 1) % polygon.Count];
                double fp = f(p), fq = f(q);
                if (fp >= 0.0)
                {
                    kept.Add(p);
                }

                if ((fp >= 0.0) != (fq >= 0.0))
                {
                    double t = fp / (fp - fq);
                    kept.Add(new[] { p[0] + t * (q[0] - p[0]), p[1] + t * (q[1] - p[1]) });
                }
            }

            return kept;
        }

        // A convex polygon clipped to another, either way round; the first as it is where there is no second.
        private static List<double[]> ClipToConvex(List<double[]> polygon, IReadOnlyList<double[]> clip)
        {
            if (clip == null)
            {
                return polygon;
            }

            double turn = Math.Sign(Area(clip));
            for (int e = 0; e < clip.Count && polygon.Count > 0; e++)
            {
                double[] a = clip[e], b = clip[(e + 1) % clip.Count];
                polygon = KeepWhere(polygon, p => turn * ((b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])));
            }

            return polygon;
        }

        // The integral of a linear function over a polygon: its area times the function at its centroid.
        private static double Integral(List<double[]> polygon, Func<double[], double> f)
        {
            if (polygon.Count < 3)
            {
                return 0.0;
            }

            double area = Area(polygon);
            if (!(Math.Abs(area) > 0.0))
            {
                return 0.0;
            }

            double sx = 0.0, sy = 0.0;
            for (int k = 0; k < polygon.Count; k++)
            {
                double[] a = polygon[k], b = polygon[(k + 1) % polygon.Count];
                double cross = a[0] * b[1] - b[0] * a[1];
                sx += (a[0] + b[0]) * cross;
                sy += (a[1] + b[1]) * cross;
            }

            return Math.Abs(area) * f(new[] { sx / (6.0 * area), sy / (6.0 * area) });
        }

        #endregion

        #region The oracle: cells of the boxes, each owned by the highest-ranked box holding it

        /// <summary>What the oracle works out for a set of boxes.</summary>
        internal sealed class Oracle
        {
            private Oracle(int count)
            {
                Gross = new double[count];
                Net = new double[count];
                Taken = new double[count, count];
            }

            /// <summary>The material of each box; 0 for a null one.</summary>
            internal double[] Gross { get; }

            /// <summary>The cells each box owns.</summary>
            internal double[] Net { get; }

            /// <summary>Taken[i, k]: what box k, ranked above box i, owns of the material of box i.</summary>
            internal double[,] Taken { get; }

            /// <summary>The indices of the boxes not null, highest-ranked first.</summary>
            internal int[] Ranked { get; private set; }

            /// <summary>The volume of the union, every cell owned at all.</summary>
            internal double Union { get; private set; }

            /// <summary>What box i gives up, (keeper, volume), the keepers in rank order and none that takes nothing.</summary>
            internal IReadOnlyList<(int By, double Volume)> Deductions(int i)
                => Ranked.Where(k => Taken[i, k] > 0.0).Select(k => (k, Taken[i, k])).ToList();

            internal static Oracle Of(BoxSet set)
            {
                int count = set.Boxes.Count;
                var oracle = new Oracle(count);
                oracle.Ranked = Enumerable.Range(0, count).Where(i => set.Boxes[i] != null)
                    .OrderByDescending(i => set.Priorities[i]).ThenByDescending(i => set.Ids[i].HasValue).ThenByDescending(i => set.Ids[i] ?? 0).ThenBy(i => i).ToArray();

                double[][] cuts = Enumerable.Range(0, 3).Select(axis => Coordinates(set.Boxes, axis)).ToArray();

                for (int ix = 0; ix + 1 < cuts[0].Length; ix++)
                {
                    for (int iy = 0; iy + 1 < cuts[1].Length; iy++)
                    {
                        for (int iz = 0; iz + 1 < cuts[2].Length; iz++)
                        {
                            double x = (cuts[0][ix] + cuts[0][ix + 1]) / 2.0;
                            double y = (cuts[1][iy] + cuts[1][iy + 1]) / 2.0;
                            double z = (cuts[2][iz] + cuts[2][iz + 1]) / 2.0;
                            double volume = (cuts[0][ix + 1] - cuts[0][ix]) * (cuts[1][iy + 1] - cuts[1][iy]) * (cuts[2][iz + 1] - cuts[2][iz]);

                            int owner = -1;
                            foreach (int i in oracle.Ranked)
                            {
                                if (!set.Boxes[i].Holds(x, y, z))
                                {
                                    continue;
                                }

                                oracle.Gross[i] += volume;
                                if (owner < 0)
                                {
                                    owner = i;
                                    oracle.Net[i] += volume;
                                    oracle.Union += volume;
                                }
                                else
                                {
                                    oracle.Taken[i, owner] += volume;
                                }
                            }
                        }
                    }
                }

                return oracle;
            }

            internal static double[] Coordinates(IReadOnlyList<Box> boxes, int axis)
            {
                var all = new List<double>();
                foreach (Box box in boxes.Where(b => b != null))
                {
                    foreach (Box part in new[] { box }.Concat(box.Openings))
                    {
                        all.Add(part.Min[axis]);
                        all.Add(part.Max[axis]);
                    }
                }

                return all.Distinct().OrderBy(v => v).ToArray();
            }
        }

        #endregion

        #region Slicing: what of a subject lies within some boxes and outside others

        /// <summary>
        /// A subject, the boxes it is taken within and the boxes it is taken outside of, as the slicing measure reads them:
        /// the volume of the subject's material that lies in the material of a box within and in that of no box outside.
        /// <see cref="WithinIsSubject"/> and <see cref="OutsideIsSubject"/> say, for each box, whether its solid is to be
        /// the subject's very solid.
        /// </summary>
        internal sealed class SliceSet
        {
            internal SliceSet(Box subject, IReadOnlyList<Box> within, IReadOnlyList<Box> outside, IReadOnlyList<bool> withinIsSubject = null, IReadOnlyList<bool> outsideIsSubject = null)
            {
                Subject = subject;
                Within = within;
                Outside = outside;
                WithinIsSubject = withinIsSubject ?? within.Select(_ => false).ToArray();
                OutsideIsSubject = outsideIsSubject ?? outside.Select(_ => false).ToArray();
            }

            internal Box Subject { get; }

            internal IReadOnlyList<Box> Within { get; }

            internal IReadOnlyList<Box> Outside { get; }

            internal IReadOnlyList<bool> WithinIsSubject { get; }

            internal IReadOnlyList<bool> OutsideIsSubject { get; }

            /// <summary>The three as solids, each box built on its own but where it is to be the subject's very solid.</summary>
            internal (GeoSolid3 Subject, GeoSolid3[] Within, GeoSolid3[] Outside) ToSolids()
            {
                GeoSolid3 subject = Subject.ToSolid();
                GeoSolid3[] within = Within.Select((box, i) => WithinIsSubject[i] ? subject : box.ToSolid()).ToArray();
                GeoSolid3[] outside = Outside.Select((box, i) => OutsideIsSubject[i] ? subject : box.ToSolid()).ToArray();
                return (subject, within, outside);
            }

            /// <summary>
            /// What the cells give: the cells cut by every x, y and z of the boxes and their openings, each counted where the
            /// subject's material holds it, the material of some box within does, and the material of no box outside does.
            /// </summary>
            internal double Oracle()
            {
                Box[] all = new[] { Subject }.Concat(Within).Concat(Outside).ToArray();
                double[][] cuts = Enumerable.Range(0, 3).Select(axis => VolumeTakeoffTestKit.Oracle.Coordinates(all, axis)).ToArray();
                double volume = 0.0;

                for (int ix = 0; ix + 1 < cuts[0].Length; ix++)
                {
                    for (int iy = 0; iy + 1 < cuts[1].Length; iy++)
                    {
                        for (int iz = 0; iz + 1 < cuts[2].Length; iz++)
                        {
                            double x = (cuts[0][ix] + cuts[0][ix + 1]) / 2.0;
                            double y = (cuts[1][iy] + cuts[1][iy + 1]) / 2.0;
                            double z = (cuts[2][iz] + cuts[2][iz + 1]) / 2.0;

                            if (Subject.Holds(x, y, z) && Within.Any(box => box.Holds(x, y, z)) && !Outside.Any(box => box.Holds(x, y, z)))
                            {
                                volume += (cuts[0][ix + 1] - cuts[0][ix]) * (cuts[1][iy + 1] - cuts[1][iy]) * (cuts[2][iz + 1] - cuts[2][iz]);
                            }
                        }
                    }
                }

                return volume;
            }

            /// <summary>
            /// The volume of the subject's outline, openings and all: the scale an error is measured against, which an
            /// opening taking the subject whole, flush, cannot bring to nought.
            /// </summary>
            internal double SubjectOutline() => (Subject.Max[0] - Subject.Min[0]) * (Subject.Max[1] - Subject.Min[1]) * (Subject.Max[2] - Subject.Min[2]);

            public override string ToString()
            {
                var text = new StringBuilder();
                text.AppendFormat(CultureInfo.InvariantCulture, "  subject {0}\n", Subject);
                for (int i = 0; i < Within.Count; i++)
                {
                    text.AppendFormat(CultureInfo.InvariantCulture, "  within {0}{1}\n", Within[i], WithinIsSubject[i] ? " (the subject's solid)" : "");
                }

                for (int i = 0; i < Outside.Count; i++)
                {
                    text.AppendFormat(CultureInfo.InvariantCulture, "  outside {0}{1}\n", Outside[i], OutsideIsSubject[i] ? " (the subject's solid)" : "");
                }

                return text.ToString();
            }
        }

        /// <summary>
        /// A random set for the slicing, from its seed: a subject, 0 to 4 boxes within and 0 to 4 outside, with corners on a
        /// grid 100 apart in a cube 600 across, as <see cref="RandomSet"/> lays them, so that flush faces are common. One box
        /// in three carries an opening on a grid 50 apart, flush with its faces or running past them as often as not, and
        /// never all of it; one box within or outside in five is a copy of the subject, half of those its very solid.
        /// </summary>
        internal static SliceSet RandomSliceSet(int seed)
        {
            var random = new Random(seed);
            Box subject = RandomBox(random);
            var within = new List<Box>();
            var outside = new List<Box>();
            var withinIsSubject = new List<bool>();
            var outsideIsSubject = new List<bool>();

            foreach ((List<Box> boxes, List<bool> isSubject) in new[] { (within, withinIsSubject), (outside, outsideIsSubject) })
            {
                int count = random.Next(0, 5);
                for (int i = 0; i < count; i++)
                {
                    bool copy = random.Next(5) == 0;
                    boxes.Add(copy ? subject : RandomBox(random));
                    isSubject.Add(copy && random.Next(2) == 0);
                }
            }

            return new SliceSet(subject, within, outside, withinIsSubject, outsideIsSubject);
        }

        // A box 1 to 4 steps of 100 long on each axis in the cube 600 across, one in three with an opening.
        private static Box RandomBox(Random random)
        {
            var min = new double[3];
            var max = new double[3];
            for (int axis = 0; axis < 3; axis++)
            {
                int from = random.Next(0, 6);
                int to = Math.Min(6, from + random.Next(1, 5));
                min[axis] = 100.0 * from;
                max[axis] = 100.0 * to;
            }

            if (random.Next(3) != 0)
            {
                return new Box(min[0], min[1], min[2], max[0], max[1], max[2]);
            }

            // On each axis the opening runs from a step of 50 before the box's start, at it or inside it, to a step of 50
            // inside its end, at it or past it; on one axis it stops inside at the start, so that some of the box is left.
            var low = new double[3];
            var high = new double[3];
            int inside = random.Next(3);
            for (int axis = 0; axis < 3; axis++)
            {
                int steps = (int)Math.Round((max[axis] - min[axis]) / 50.0);
                int a = axis == inside ? random.Next(1, steps) : random.Next(-1, steps);
                int b = random.Next(a + 1, steps + 2);
                low[axis] = min[axis] + 50.0 * a;
                high[axis] = min[axis] + 50.0 * b;
            }

            return new Box(min[0], min[1], min[2], max[0], max[1], max[2], new Box(low[0], low[1], low[2], high[0], high[1], high[2]));
        }

        #endregion

        #region Holding a run to the oracle

        /// <summary>
        /// Every way the results of a run stand off the oracle, one line each; empty when they agree.
        /// </summary>
        /// <remarks>
        /// Checked: one result per item, in the order of the items, each holding its own item; the gross, net and each
        /// deduction within <see cref="Relative"/> of the oracle's gross; the deductions by exactly the keepers the oracle
        /// names, in rank order; the deducted volume the sum of the deductions in their order, and the net the gross less
        /// it, both to the bit; <see cref="VolumeTakeoffResult.IsExact"/> as the issues say; the nets summing to the
        /// union within <see cref="Relative"/> of the gross of them all; and no issue on any item, a part left nothing too: its
        /// deductions may come to a rounding more than its gross, and a clamp within the point tolerance times its area is
        /// no issue.
        /// </remarks>
        internal static List<string> Disagreements(VolumeItem[] items, IReadOnlyList<VolumeTakeoffResult> results, Oracle oracle)
        {
            var wrong = new List<string>();
            if (results.Count != items.Length)
            {
                wrong.Add($"{results.Count} results for {items.Length} items");
                return wrong;
            }

            double sumNet = 0.0;
            for (int i = 0; i < items.Length; i++)
            {
                VolumeTakeoffResult result = results[i];
                double scale = Relative * oracle.Gross[i];
                if (!ReferenceEquals(items[i], result.Item))
                {
                    wrong.Add($"#{i}: the result holds another item");
                }

                Near(wrong, $"#{i} gross", oracle.Gross[i], result.GrossVolume, scale);
                Near(wrong, $"#{i} net", oracle.Net[i], result.NetVolume, scale);

                IReadOnlyList<(int By, double Volume)> expected = oracle.Deductions(i);
                string by = string.Join(" ", result.Deductions.Select(d => d.ByIndex));
                string expectedBy = string.Join(" ", expected.Select(d => d.By));
                if (by != expectedBy)
                {
                    wrong.Add($"#{i}: deductions by [{by}], the oracle's by [{expectedBy}]");
                }
                else
                {
                    for (int d = 0; d < expected.Count; d++)
                    {
                        Near(wrong, $"#{i} deduction by #{expected[d].By}", expected[d].Volume, result.Deductions[d].Volume, scale);
                    }
                }

                double deducted = 0.0;
                foreach (VolumeDeduction deduction in result.Deductions)
                {
                    deducted += deduction.Volume;
                }

                if (!BitEqual(deducted, result.DeductedVolume))
                {
                    wrong.Add($"#{i}: deducted {result.DeductedVolume:R}, its deductions summing to {deducted:R}");
                }

                double net = Math.Min(Math.Max(result.GrossVolume - result.DeductedVolume, 0.0), result.GrossVolume);
                if (!BitEqual(net, result.NetVolume))
                {
                    wrong.Add($"#{i}: net {result.NetVolume:R}, gross less deducted {net:R}");
                }

                if (result.IsExact != (result.Issues.Count == 0))
                {
                    wrong.Add($"#{i}: IsExact {result.IsExact} with {result.Issues.Count} issues");
                }

                if (items[i] != null && result.Issues.Count > 0)
                {
                    wrong.Add($"#{i}: issues {string.Join(" / ", result.Issues)}");
                }

                sumNet += result.NetVolume;
            }

            Near(wrong, "the nets summed against the union", oracle.Union, sumNet, Relative * oracle.Gross.Sum());
            return wrong;
        }

        /// <summary>Every way two runs differ, field by field and bit for bit; empty when they are the same.</summary>
        internal static List<string> Differences(IReadOnlyList<VolumeTakeoffResult> a, IReadOnlyList<VolumeTakeoffResult> b)
        {
            var differ = new List<string>();
            if (a.Count != b.Count)
            {
                differ.Add($"{a.Count} results against {b.Count}");
                return differ;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (!BitEqual(a[i].GrossVolume, b[i].GrossVolume) || !BitEqual(a[i].DeductedVolume, b[i].DeductedVolume) || !BitEqual(a[i].NetVolume, b[i].NetVolume))
                {
                    differ.Add($"#{i}: gross/deducted/net {a[i].GrossVolume:R}/{a[i].DeductedVolume:R}/{a[i].NetVolume:R} against {b[i].GrossVolume:R}/{b[i].DeductedVolume:R}/{b[i].NetVolume:R}");
                }

                string da = string.Join(" ", a[i].Deductions.Select(d => d.ByIndex + ":" + d.Volume.ToString("R", CultureInfo.InvariantCulture)));
                string db = string.Join(" ", b[i].Deductions.Select(d => d.ByIndex + ":" + d.Volume.ToString("R", CultureInfo.InvariantCulture)));
                if (da != db)
                {
                    differ.Add($"#{i}: deductions [{da}] against [{db}]");
                }

                if (!a[i].Issues.SequenceEqual(b[i].Issues) || a[i].IsExact != b[i].IsExact)
                {
                    differ.Add($"#{i}: issues [{string.Join(" / ", a[i].Issues)}] against [{string.Join(" / ", b[i].Issues)}]");
                }
            }

            return differ;
        }

        private static void Near(List<string> wrong, string what, double expected, double actual, double within)
        {
            if (!(Math.Abs(actual - expected) <= within))
            {
                wrong.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1:R}, the oracle's {2:R}, off by {3:G3}", what, actual, expected, actual - expected));
            }
        }

        private static bool BitEqual(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Bodies whose faces do not bound a solid as they should, and bodies whose faces do, for the tests of closing: each
    /// built face by face from corners given exactly, so that what is wrong with it is known to the last digit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A box is its eight corners and the six loops of <see cref="BoxLoops"/>, each a face by the indices of its corners.
    /// The corners are numbered as <see cref="GeoObb3.GetCorners"/> numbers them, the first four round the bottom
    /// counter-clockwise seen from above and the last four the same four lifted to the top, and the faces come in the
    /// order <see cref="GeoObb3.GetFaces"/> gives them, so that a box built here is, face for face, the one an
    /// axis-aligned box makes.
    /// </para>
    /// <para>
    /// Each face reads its corners from the array it is given, so a corner moved for some faces and not for others is a
    /// copy of the array with that corner moved, as <see cref="BoxWithCornerMoved"/> makes one. The damaged boxes are of
    /// <see cref="Corners()"/>, 30 by 20 by 10: 6 000 in all, its top and bottom 600 each, its front and back 300 and its
    /// sides 200, 2 200 in all. Measured from its middle, each face holds a sixth of it, 1 000.
    /// </para>
    /// </remarks>
    internal static class ClosingTestBodies
    {
        /// <summary>The tolerance every face here is built within, and the closing tests read.</summary>
        internal static readonly Tolerance Fine = Tolerance.Default;

        /// <summary>The bottom of a box, facing down: the first of its faces.</summary>
        internal const int Bottom = 0;

        /// <summary>The top of a box, facing up.</summary>
        internal const int Top = 1;

        /// <summary>The front of a box, at its least y, facing along -y.</summary>
        internal const int Front = 2;

        /// <summary>The back of a box, at its greatest y, facing along +y.</summary>
        internal const int Back = 3;

        /// <summary>The right of a box, at its greatest x, facing along +x.</summary>
        internal const int Right = 4;

        /// <summary>The left of a box, at its least x, facing along -x.</summary>
        internal const int Left = 5;

        /// <summary>
        /// The six faces of a box by the indices of their corners, <see cref="Bottom"/> to <see cref="Left"/>, each
        /// counter-clockwise seen from outside, so that each edge is run once each way.
        /// </summary>
        internal static readonly int[][] BoxLoops =
        {
            new[] { 0, 3, 2, 1 },
            new[] { 4, 5, 6, 7 },
            new[] { 0, 1, 5, 4 },
            new[] { 2, 3, 7, 6 },
            new[] { 1, 2, 6, 5 },
            new[] { 3, 0, 4, 7 },
        };

        /// <summary>How long the box of the damaged bodies is along x.</summary>
        internal const double SizeX = 30.0;

        /// <summary>How long the box of the damaged bodies is along y.</summary>
        internal const double SizeY = 20.0;

        /// <summary>How high the box of the damaged bodies is.</summary>
        internal const double SizeZ = 10.0;

        /// <summary>The volume of the box of the damaged bodies, 6 000.</summary>
        internal const double Volume = SizeX * SizeY * SizeZ;

        /// <summary>The area of the faces of the box of the damaged bodies, 2 200.</summary>
        internal const double Area = 2.0 * (SizeX * SizeY + SizeX * SizeZ + SizeY * SizeZ);

        /// <summary>The corners of the box of the damaged bodies, from the origin to (30, 20, 10).</summary>
        internal static GeoPoint3[] Corners() => Corners(0.0, 0.0, 0.0, SizeX, SizeY, SizeZ);

        /// <summary>The eight corners of a box from its least corner to its greatest, as a box numbers them.</summary>
        internal static GeoPoint3[] Corners(double x0, double y0, double z0, double x1, double y1, double z1) => new[]
        {
            new GeoPoint3(x0, y0, z0),
            new GeoPoint3(x1, y0, z0),
            new GeoPoint3(x1, y1, z0),
            new GeoPoint3(x0, y1, z0),
            new GeoPoint3(x0, y0, z1),
            new GeoPoint3(x1, y0, z1),
            new GeoPoint3(x1, y1, z1),
            new GeoPoint3(x0, y1, z1),
        };

        /// <summary>The loop of some of the corners, in the order of their indices.</summary>
        internal static GeoPolygon3 Loop(IReadOnlyList<GeoPoint3> corners, int[] loop)
            => new GeoPolygon3(loop.Select(i => corners[i]), Fine);

        /// <summary>The face of a loop of some of the corners, in the order of their indices.</summary>
        internal static GeoFace3 Face(IReadOnlyList<GeoPoint3> corners, int[] loop) => new GeoFace3(Loop(corners, loop), null, Fine);

        /// <summary>The face of a loop of corners, in their order.</summary>
        internal static GeoFace3 Face(params GeoPoint3[] loop) => new GeoFace3(new GeoPolygon3(loop, Fine), null, Fine);

        /// <summary>The six faces of a box on its corners, <see cref="Bottom"/> to <see cref="Left"/>.</summary>
        internal static List<GeoFace3> BoxFaces(IReadOnlyList<GeoPoint3> corners) => BoxLoops.Select(loop => Face(corners, loop)).ToList();

        /// <summary>The box of the damaged bodies, whole and wound outwards.</summary>
        internal static GeoSolid3 Box() => new GeoSolid3(BoxFaces(Corners()));

        /// <summary>The box with one face left out: open along the four edges of the face.</summary>
        internal static GeoSolid3 BoxWithout(int face)
        {
            List<GeoFace3> faces = BoxFaces(Corners());
            faces.RemoveAt(face);
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The box with one face given twice, the same way round, the copy last: a face of its own on the same corners, so
        /// that only its corners say it is the same.
        /// </summary>
        internal static GeoSolid3 BoxWithFaceTwice(int face)
        {
            GeoPoint3[] corners = Corners();
            List<GeoFace3> faces = BoxFaces(corners);
            faces.Add(Face(corners, BoxLoops[face]));
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The box with a copy of one face turned over, lying back to back with it: the copy last, or in the face's own
        /// place with the face last.
        /// </summary>
        internal static GeoSolid3 BoxWithFaceAndItsFlip(int face, bool copyFirst)
        {
            GeoPoint3[] corners = Corners();
            List<GeoFace3> faces = BoxFaces(corners);
            GeoFace3 copy = Face(corners, BoxLoops[face]).Flip();

            if (copyFirst)
            {
                faces.Add(faces[face]);
                faces[face] = copy;
            }
            else
            {
                faces.Add(copy);
            }

            return new GeoSolid3(faces);
        }

        /// <summary>The box with one face wound the wrong way round, facing into the box.</summary>
        internal static GeoSolid3 BoxWithFaceFlipped(int face)
        {
            List<GeoFace3> faces = BoxFaces(Corners());
            faces[face] = faces[face].Flip();
            return new GeoSolid3(faces);
        }

        /// <summary>The box with every face wound the wrong way round: closed and wound alike, inwards.</summary>
        internal static GeoSolid3 BoxInsideOut() => new GeoSolid3(BoxFaces(Corners()).Select(face => face.Flip()));

        /// <summary>
        /// A fin: the face standing off an edge from one corner to another, as far off it as a reach, the edge first.
        /// </summary>
        internal static GeoFace3 Fin(GeoPoint3 from, GeoPoint3 to, GeoVector3 reach) => Face(from, to, to.Add(reach), from.Add(reach));

        /// <summary>
        /// The box with a fin standing off the edge between two of its corners: three faces meet on that edge.
        /// </summary>
        internal static GeoSolid3 BoxWithFin(int from, int to, GeoVector3 reach)
        {
            GeoPoint3[] corners = Corners();
            List<GeoFace3> faces = BoxFaces(corners);
            faces.Add(Fin(corners[from], corners[to], reach));
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The box with one corner moved for some of its faces, the others keeping it where it was.
        /// </summary>
        /// <param name="corner">The index of the corner.</param>
        /// <param name="by">How far it is moved, and which way.</param>
        /// <param name="movedIn">The faces that take it moved, by <see cref="Bottom"/> to <see cref="Left"/>.</param>
        internal static GeoSolid3 BoxWithCornerMoved(int corner, GeoVector3 by, params int[] movedIn)
        {
            GeoPoint3[] corners = Corners();
            var moved = (GeoPoint3[])corners.Clone();
            moved[corner] = moved[corner].Add(by);
            var faces = new List<GeoFace3>();

            for (int f = 0; f < BoxLoops.Length; f++)
            {
                faces.Add(Face(Array.IndexOf(movedIn, f) >= 0 ? moved : corners, BoxLoops[f]));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The faces of a cavity 2 by 2 by 2 in the middle of the box of the damaged bodies, wound inwards as a cavity is:
        /// with the box, a body of 5 992.
        /// </summary>
        internal static List<GeoFace3> CavityFaces() => BoxFaces(Corners(14, 9, 4, 16, 11, 6)).Select(face => face.Flip()).ToList();

        /// <summary>
        /// The box with a sheet inside it, two faces 20 by 10 lying back to back halfway up, touching none of its faces.
        /// </summary>
        internal static GeoSolid3 BoxWithASheetInside()
        {
            List<GeoFace3> faces = BoxFaces(Corners());
            GeoFace3 sheet = Face(new GeoPoint3(5, 5, 5), new GeoPoint3(25, 5, 5), new GeoPoint3(25, 15, 5), new GeoPoint3(5, 15, 5));
            faces.Add(sheet);
            faces.Add(sheet.Flip());
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// A prism over a plan, the plan's corners taken counter-clockwise seen from above, their heights ignored.
        /// </summary>
        internal static GeoSolid3 Prism(IReadOnlyList<GeoPoint3> plan, double z0, double z1)
        {
            GeoPoint3[] down = plan.Select(p => new GeoPoint3(p.X, p.Y, z0)).ToArray();
            GeoPoint3[] up = plan.Select(p => new GeoPoint3(p.X, p.Y, z1)).ToArray();
            var faces = new List<GeoFace3> { Face(down.Reverse().ToArray()), Face(up) };

            for (int i = 0; i < plan.Count; i++)
            {
                int j = (i + 1) % plan.Count;
                faces.Add(Face(down[i], down[j], up[j], up[i]));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// A prism 10 high over an L, 20 along each leg and 10 wide: its top and bottom are concave, 3 000 in all.
        /// </summary>
        internal static GeoSolid3 LShapedPrism() => Prism(
            new[]
            {
                new GeoPoint3(0, 0, 0), new GeoPoint3(20, 0, 0), new GeoPoint3(20, 10, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(10, 20, 0), new GeoPoint3(0, 20, 0),
            },
            0.0,
            10.0);

        /// <summary>The box of the damaged bodies and the same box 100 along x: two shells, 12 000.</summary>
        internal static GeoSolid3 TwoBoxesApart()
            => new GeoSolid3(BoxFaces(Corners()).Concat(BoxFaces(Corners(100, 0, 0, 130, 20, 10))));

        /// <summary>
        /// The box of the damaged bodies and the same box set off its corner at (30, 20): the two meet along the upright edge
        /// there, four faces on it, and nowhere else.
        /// </summary>
        internal static GeoSolid3 TwoBoxesAlongAnEdge()
            => new GeoSolid3(BoxFaces(Corners()).Concat(BoxFaces(Corners(30, 20, 0, 60, 40, 10))));

        /// <summary>
        /// A plate 30 by 30 by 10 with a hole 10 by 10 through its middle: its top and bottom carry the hole, and the four
        /// walls of the hole face into it. 8 000 in all.
        /// </summary>
        internal static GeoSolid3 PlateWithAHole()
        {
            GeoPoint3[] outer = Corners(0, 0, 0, 30, 30, 10);
            GeoPoint3[] hole = Corners(10, 10, 0, 20, 20, 10);
            var faces = new List<GeoFace3>
            {
                new GeoFace3(Loop(outer, BoxLoops[Bottom]), new[] { Loop(hole, BoxLoops[Bottom]) }, Fine),
                new GeoFace3(Loop(outer, BoxLoops[Top]), new[] { Loop(hole, BoxLoops[Top]) }, Fine),
            };

            foreach (int side in new[] { Front, Back, Right, Left })
            {
                faces.Add(Face(outer, BoxLoops[side]));
                faces.Add(Face(hole, BoxLoops[side]).Flip());
            }

            return new GeoSolid3(faces);
        }

        /// <summary>How far a point is from the nearest point of the segment between two others.</summary>
        internal static double DistanceToSegment(GeoPoint3 point, GeoPoint3 start, GeoPoint3 end)
        {
            GeoVector3 along = start.GetVectorTo(end);
            double t = start.GetVectorTo(point).DotProduct(along) / along.DotProduct(along);
            GeoPoint3 nearest = start.Add(along.Multiply(Math.Max(0.0, Math.Min(1.0, t))));
            return nearest.GetVectorTo(point).Length;
        }

        #region Bodies open by a hair, for welding

        /// <summary>
        /// How many corners of the rings of a body's faces have the corners either side of them one point within the
        /// tolerance: needles, rings running out to a corner and straight back, which read valid and are no face of a body.
        /// </summary>
        internal static int Needles(GeoSolid3 solid)
        {
            int needles = 0;

            foreach (GeoFace3 face in solid.Faces)
            {
                foreach (GeoPolygon3 ring in new[] { face.Boundary }.Concat(face.Holes))
                {
                    int count = ring.VertexCount;

                    for (int i = 0; i < count; i++)
                    {
                        if (ring[(i + count - 1) % count].DistanceTo(ring[(i + 1) % count]) <= Fine.EqualPoint)
                        {
                            needles++;
                        }
                    }
                }
            }

            return needles;
        }

        /// <summary>
        /// The unit vector in the plane of the top of a box out from its middle along the diagonal through one of its
        /// corners, 4 to 7.
        /// </summary>
        internal static GeoVector3 OutOfTheTop(int corner)
        {
            double x = corner == 5 || corner == 6 ? 1.0 : -1.0;
            double y = corner == 6 || corner == 7 ? 1.0 : -1.0;
            return new GeoVector3(x, y, 0.0).Multiply(1.0 / Math.Sqrt(2.0));
        }

        /// <summary>
        /// The box with corners of its top, 4 to 7, moved outwards in the top's plane by as much as given, along the diagonal
        /// out of its middle, the other faces keeping them where they were: open along the edges of the top at each, by a gap
        /// as wide as the move.
        /// </summary>
        internal static GeoSolid3 BoxWithTopCornersMovedOut(params (int Corner, double By)[] moves)
        {
            GeoPoint3[] corners = Corners();
            var top = (GeoPoint3[])corners.Clone();

            foreach ((int corner, double by) in moves)
            {
                top[corner] = top[corner].Add(OutOfTheTop(corner).Multiply(by));
            }

            List<GeoFace3> faces = BoxFaces(corners);
            faces[Top] = Face(top, BoxLoops[Top]);
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The box with its top carrying a corner of its own halfway along its front edge, standing outwards off the front's
        /// straight top edge by a distance, in the top's plane: the two are open along that edge.
        /// </summary>
        internal static GeoSolid3 BoxWithTheTopsFrontBentOut(double off)
        {
            GeoPoint3[] c = Corners();
            List<GeoFace3> faces = BoxFaces(c);
            faces[Top] = Face(c[4], new GeoPoint3(SizeX / 2.0, -off, SizeZ), c[5], c[6], c[7]);
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The box with every face on copies of the corners of its own, each copy moved in the plane of its face by a random
        /// vector no longer than given: every edge open, and no two copies of a corner further apart than twice that.
        /// </summary>
        internal static GeoSolid3 BoxOfMovedCopies(int seed, double most)
        {
            var random = new Random(seed);
            GeoPoint3[] corners = Corners();
            var faces = new List<GeoFace3>();

            for (int f = 0; f < BoxLoops.Length; f++)
            {
                // The axes of the face's plane: the bottom and top lie across x and y, the front and back across x and z, and
                // the sides across y and z.
                GeoVector3 u = f <= Back ? GeoVector3.XAxis : GeoVector3.YAxis;
                GeoVector3 v = f <= Top ? GeoVector3.YAxis : GeoVector3.ZAxis;
                var loop = new List<GeoPoint3>();

                foreach (int i in BoxLoops[f])
                {
                    double angle = 2.0 * Math.PI * random.NextDouble();
                    double length = most * random.NextDouble();
                    loop.Add(corners[i].Add(u.Multiply(length * Math.Cos(angle))).Add(v.Multiply(length * Math.Sin(angle))));
                }

                faces.Add(Face(loop.ToArray()));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// A prism over a plan, as <see cref="Prism"/> makes one, with one corner of its top moved, the sides keeping it where
        /// it was.
        /// </summary>
        internal static GeoSolid3 PrismWithTopCornerMoved(IReadOnlyList<GeoPoint3> plan, double z0, double z1, int corner, GeoVector3 by)
        {
            List<GeoFace3> faces = Prism(plan, z0, z1).Faces.ToList();
            faces[1] = Face(plan.Select((p, i) => new GeoPoint3(p.X, p.Y, z1).Add(i == corner ? by : GeoVector3.Zero)).ToArray());
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The plan of the box of the damaged bodies with its corner at (30, 20) cut off by a chamfer 0.05 along each side:
        /// the chamfer's ends, its corners 2 and 3, are 0.07 apart.
        /// </summary>
        internal static GeoPoint3[] ChamferedPlan() => new[]
        {
            new GeoPoint3(0, 0, 0), new GeoPoint3(30, 0, 0), new GeoPoint3(30, 19.95, 0), new GeoPoint3(29.95, 20, 0), new GeoPoint3(0, 20, 0),
        };

        /// <summary>
        /// The plan of the box of the damaged bodies with a slot as wide as given cut 10 into it from the middle of its back:
        /// its walls at x = 15 and 15 and the width, from y = 10 to the back.
        /// </summary>
        internal static GeoPoint3[] SlottedPlan(double width) => new[]
        {
            new GeoPoint3(0, 0, 0), new GeoPoint3(30, 0, 0), new GeoPoint3(30, 20, 0), new GeoPoint3(15 + width, 20, 0),
            new GeoPoint3(15 + width, 10, 0), new GeoPoint3(15, 10, 0), new GeoPoint3(15, 20, 0), new GeoPoint3(0, 20, 0),
        };

        #endregion

        #region Bodies cracked along an edge

        /// <summary>
        /// How many faces of a body lie back to back with another, the middle of each on a face facing the other way: a
        /// skin of no thickness, which reads valid and holds no material.
        /// </summary>
        internal static int BackToBack(GeoSolid3 solid)
        {
            IReadOnlyList<GeoFace3> faces = solid.Faces;
            int count = 0;

            for (int f = 0; f < faces.Count; f++)
            {
                GeoPoint3 middle = faces[f].Centroid;

                for (int g = 0; g < faces.Count; g++)
                {
                    if (g != f && faces[g].Normal.DotProduct(faces[f].Normal) < -0.999 && faces[g].Locate(middle, Fine) != Enums.PointLocation.OutSide)
                    {
                        count++;
                        break;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// The box with its top's front edge and its front's top edge each run through corners of their own between the
        /// corners over (0, 0) and (30, 0), the top's given from x = 0 on and the front's from x = 30 back: cracked between
        /// the two chains, and closed wherever they meet.
        /// </summary>
        internal static GeoSolid3 BoxCrackedAlongTheFront(IEnumerable<GeoPoint3> top, IEnumerable<GeoPoint3> front)
        {
            GeoPoint3[] c = Corners();
            List<GeoFace3> faces = BoxFaces(c);
            faces[Top] = Face(new[] { c[4] }.Concat(top).Concat(new[] { c[5], c[6], c[7] }).ToArray());
            faces[Front] = Face(new[] { c[0], c[1], c[5] }.Concat(front).Concat(new[] { c[4] }).ToArray());
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// Corners at even steps along the front edge of the top of the box, as many as given, bowed off it along a direction
        /// as a parabola, by as much as given at the middle: from x = 0 on, or from x = 30 back.
        /// </summary>
        internal static List<GeoPoint3> Bowed(int count, double sag, GeoVector3 toward, bool fromTheRight)
        {
            var points = new List<GeoPoint3>();

            for (int i = 1; i <= count; i++)
            {
                double t = (double)i / (count + 1);
                points.Add(new GeoPoint3(fromTheRight ? SizeX * (1.0 - t) : SizeX * t, 0, SizeZ).Add(toward.Multiply(4.0 * sag * t * (1.0 - t))));
            }

            return points;
        }

        /// <summary>
        /// A prism a unit high over a polygon of as many sides as given round a circle, its top a polygon of as many corners
        /// on the same circle: where the two do not share a corner, the top stands off the sides' top edges in its plane.
        /// </summary>
        internal static GeoSolid3 FacetedPrism(double radius, int sides, int topCorners)
        {
            GeoPoint3 At(double angle, double z) => new GeoPoint3(radius * Math.Cos(angle), radius * Math.Sin(angle), z);
            GeoPoint3[] down = Enumerable.Range(0, sides).Select(k => At(2.0 * Math.PI * k / sides, 0.0)).ToArray();
            GeoPoint3[] up = Enumerable.Range(0, sides).Select(k => At(2.0 * Math.PI * k / sides, 1.0)).ToArray();
            GeoPoint3[] top = Enumerable.Range(0, topCorners).Select(k => At(2.0 * Math.PI * k / topCorners, 1.0)).ToArray();
            var faces = new List<GeoFace3> { Face(down.Reverse().ToArray()), Face(top) };

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                faces.Add(Face(down[i], down[j], up[j], up[i]));
            }

            return new GeoSolid3(faces);
        }

        #endregion

        #region Bodies with faces left out, for filling

        /// <summary>A body with some of its faces left out, by their index, its openings kept.</summary>
        internal static GeoSolid3 WithoutFaces(GeoSolid3 body, params int[] faces)
            => new GeoSolid3(body.Faces.Where((face, i) => Array.IndexOf(faces, i) < 0), body.Openings);

        /// <summary>
        /// The corners of the box of the damaged bodies turned about the vertical through the origin by an angle, in radians,
        /// and then moved: corners that a point found along an edge between two of them lands on only to a rounding.
        /// </summary>
        internal static GeoPoint3[] TurnedCorners(double angle, GeoVector3 by)
        {
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            return Corners().Select(p => new GeoPoint3(p.X * cos - p.Y * sin, p.X * sin + p.Y * cos, p.Z).Add(by)).ToArray();
        }

        #endregion

        #region Bodies for checking the welding

        /// <summary>
        /// A prism 10 high over a triangle whose corner at the origin is as sharp as given, in degrees, its other corners at
        /// (30, 0) and up x = 30, with the top's copy of the sharp corner moved along the corner's bisector, into the
        /// triangle or out of it.
        /// </summary>
        internal static GeoSolid3 SharpPrism(double degrees, double move, bool inward)
        {
            double half = degrees * Math.PI / 360.0;
            var plan = new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(30, 0, 0), new GeoPoint3(30, 30 * Math.Tan(2.0 * half), 0) };
            var bisector = new GeoVector3(Math.Cos(half), Math.Sin(half), 0);
            return PrismWithTopCornerMoved(plan, 0, 10, 0, bisector.Multiply(inward ? move : -move));
        }

        /// <summary>
        /// The plate with a hole through it, the top's copy of the hole's corner over (10, 10) moved into the plate along
        /// the diagonal.
        /// </summary>
        internal static GeoSolid3 PlateWithItsHoleCornerMoved(double by)
        {
            List<GeoFace3> faces = PlateWithAHole().Faces.ToList();
            GeoPoint3[] hole = Corners(10, 10, 0, 20, 20, 10);
            hole[4] = hole[4].Add(new GeoVector3(-1, -1, 0).Multiply(by / Math.Sqrt(2.0)));
            faces[1] = new GeoFace3(Loop(Corners(0, 0, 0, 30, 30, 10), BoxLoops[Top]), new[] { Loop(hole, BoxLoops[Top]) }, Fine);
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The box with its top in two, a strip as wide as given missing between them along y, its middle at x = 15.
        /// </summary>
        internal static GeoSolid3 BoxWithAStripOfTheTopMissing(double width)
        {
            List<GeoFace3> faces = BoxFaces(Corners());
            double a = 15 - width / 2, b = 15 + width / 2;
            faces[Top] = Face(new GeoPoint3(0, 0, 10), new GeoPoint3(a, 0, 10), new GeoPoint3(a, 20, 10), new GeoPoint3(0, 20, 10));
            faces.Add(Face(new GeoPoint3(b, 0, 10), new GeoPoint3(30, 0, 10), new GeoPoint3(30, 20, 10), new GeoPoint3(b, 20, 10)));
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The prism over <see cref="SlottedPlan"/> with the slot's wall at x = 15 and the width split in two at y = 15, its
        /// corners there lying on the top's and the bottom's long edges, closed along them and paired with no corner of
        /// theirs; and the top's copy of the corner at the slot's mouth, (15, 20), moved out of the slot by as much as given.
        /// </summary>
        internal static GeoSolid3 SlotOfSplitFacesWithItsMouthMoved(double width, double by)
        {
            GeoPoint3[] plan = SlottedPlan(width);
            List<GeoFace3> faces = Prism(plan, 0, 10).Faces.ToList();

            if (by > 0.0)
            {
                GeoVector3 move = new GeoVector3(-1, 1, 0).Multiply(by / Math.Sqrt(2.0));
                faces[1] = Face(plan.Select((p, i) => new GeoPoint3(p.X, p.Y, 10).Add(i == 6 ? move : GeoVector3.Zero)).ToArray());
            }

            double x = 15 + width;
            faces[5] = Face(new GeoPoint3(x, 20, 0), new GeoPoint3(x, 15, 0), new GeoPoint3(x, 15, 10), new GeoPoint3(x, 20, 10));
            faces.Add(Face(new GeoPoint3(x, 15, 0), new GeoPoint3(x, 10, 0), new GeoPoint3(x, 10, 10), new GeoPoint3(x, 15, 10)));
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// A prism over a polygon of as many sides as given round a circle, upright, every face on copies of corners of its
        /// own, each moved in the face's plane by a random vector no longer than given: where the sides are many, the edges
        /// beside a corner run on nearly straight, and copies of them come within the tolerance of each other's lines.
        /// </summary>
        internal static GeoSolid3 PrismOfMovedCopies(int sides, double radius, double height, double most, int seed)
        {
            var random = new Random(seed);

            GeoPoint3 At(int i, double z)
            {
                double angle = 2.0 * Math.PI * (i % sides) / sides;
                return new GeoPoint3(radius * Math.Cos(angle), radius * Math.Sin(angle), z);
            }

            GeoPoint3 Moved(GeoPoint3 p, GeoVector3 u, GeoVector3 v)
            {
                double angle = 2.0 * Math.PI * random.NextDouble(), length = most * random.NextDouble();
                return p.Add(u.Multiply(length * Math.Cos(angle) / u.Length)).Add(v.Multiply(length * Math.Sin(angle) / v.Length));
            }

            var faces = new List<GeoFace3>();

            for (int i = 0; i < sides; i++)
            {
                GeoPoint3 a = At(i, 0), b = At(i + 1, 0), c = At(i + 1, height), d = At(i, height);
                GeoVector3 u = a.GetVectorTo(b), v = a.GetVectorTo(d);
                faces.Add(Face(Moved(a, u, v), Moved(b, u, v), Moved(c, u, v), Moved(d, u, v)));
            }

            faces.Add(Face(Enumerable.Range(0, sides).Reverse().Select(i => Moved(At(i, 0), GeoVector3.XAxis, GeoVector3.YAxis)).ToArray()));
            faces.Add(Face(Enumerable.Range(0, sides).Select(i => Moved(At(i, height), GeoVector3.XAxis, GeoVector3.YAxis)).ToArray()));
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The box with the top, the right and the back each on a copy of its own of the corner over (30, 20), each moved in
        /// its face's plane, every two of the copies as far apart as given.
        /// </summary>
        internal static GeoSolid3 ThreeCopiesOfACorner(double apart)
        {
            GeoPoint3[] c = Corners();
            List<GeoFace3> faces = BoxFaces(c);

            // Each copy moved as far as given along a diagonal of its face, the three diagonals at 60 degrees to each other:
            // every two of the copies then stand that far apart.
            GeoPoint3[] Moved(GeoVector3 along)
            {
                var m = (GeoPoint3[])c.Clone();
                m[6] = m[6].Add(along.Multiply(apart / along.Length));
                return m;
            }

            faces[Top] = Face(Moved(new GeoVector3(1, 1, 0)), BoxLoops[Top]);
            faces[Right] = Face(Moved(new GeoVector3(0, 1, 1)), BoxLoops[Right]);
            faces[Back] = Face(Moved(new GeoVector3(1, 0, 1)), BoxLoops[Back]);
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The corners of the box of the damaged bodies turned about an axis through the origin by an angle, in radians, and
        /// then moved.
        /// </summary>
        internal static GeoPoint3[] RotatedCorners(GeoVector3 axis, double angle, GeoVector3 by)
        {
            GeoVector3 k = axis.Multiply(1.0 / axis.Length);
            double cos = Math.Cos(angle), sin = Math.Sin(angle);

            return Corners().Select(p =>
            {
                var v = new GeoVector3(p.X, p.Y, p.Z);
                GeoVector3 r = v.Multiply(cos).Add(k.CrossProduct(v).Multiply(sin)).Add(k.Multiply(k.DotProduct(v) * (1.0 - cos)));
                return new GeoPoint3(r.X, r.Y, r.Z).Add(by);
            }).ToArray();
        }

        /// <summary>
        /// A box on given corners, the top's copy of corner 6 moved in the top's plane away from corner 4 by as much as given.
        /// </summary>
        internal static GeoSolid3 BoxWithTopCornerMovedAway(GeoPoint3[] corners, double by)
        {
            List<GeoFace3> faces = BoxFaces(corners);
            var moved = (GeoPoint3[])corners.Clone();
            GeoVector3 away = corners[4].GetVectorTo(corners[6]);
            moved[6] = moved[6].Add(away.Multiply(by / away.Length));
            faces[Top] = Face(moved, BoxLoops[Top]);
            return new GeoSolid3(faces);
        }

        #endregion

        #region Bodies with holes out of flat, for filling by triangles

        /// <summary>
        /// A box from the origin to (side, side, height) with its top left out and the corner over (side, side) lifted by as
        /// much as given in the right and the back, which stay flat: the rim of the top stands off the plane through its
        /// middle by a quarter of the lift, either way.
        /// </summary>
        internal static GeoSolid3 BoxWithoutItsTopLiftedAtACorner(double side, double height, double lift)
        {
            GeoPoint3[] corners = Corners(0, 0, 0, side, side, height);
            corners[6] = corners[6].Add(new GeoVector3(0, 0, lift));
            return new GeoSolid3(BoxLoops.Where((loop, f) => f != Top).Select(loop => Face(corners, loop)));
        }

        /// <summary>
        /// A prism over a polygon of as many sides as given round a circle, its top left out and the rim's corner on the x
        /// axis lifted by as much as given in the two sides beside it, which stay flat: the rim stands off flat by about the
        /// lift.
        /// </summary>
        internal static GeoSolid3 PrismWithoutItsTopLiftedAtACorner(int sides, double radius, double height, double lift)
        {
            GeoPoint3 At(int i, double z)
            {
                double angle = 2.0 * Math.PI * (i % sides) / sides;
                return new GeoPoint3(radius * Math.Cos(angle), radius * Math.Sin(angle), z);
            }

            GeoPoint3 Up(int i) => At(i, height).Add(i % sides == 0 ? new GeoVector3(0, 0, lift) : GeoVector3.Zero);

            var faces = new List<GeoFace3> { Face(Enumerable.Range(0, sides).Reverse().Select(i => At(i, 0)).ToArray()) };

            for (int i = 0; i < sides; i++)
            {
                faces.Add(Face(At(i, 0), At(i + 1, 0), Up(i + 1), Up(i)));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The plate of <see cref="PlateWithAHole"/> with its top left out, the corner over (30, 30) lifted by as much as
        /// given in the plate's right and back, and the hole's corner over (20, 20) as much in the hole's walls: the rim of
        /// the top and the rim of the hole inside it both stand off flat.
        /// </summary>
        internal static GeoSolid3 PlateWithAHoleWithoutItsTopLifted(double lift)
        {
            GeoPoint3[] outer = Corners(0, 0, 0, 30, 30, 10);
            GeoPoint3[] hole = Corners(10, 10, 0, 20, 20, 10);
            outer[6] = outer[6].Add(new GeoVector3(0, 0, lift));
            hole[6] = hole[6].Add(new GeoVector3(0, 0, lift));
            var faces = new List<GeoFace3> { new GeoFace3(Loop(outer, BoxLoops[Bottom]), new[] { Loop(hole, BoxLoops[Bottom]) }, Fine) };

            foreach (int side in new[] { Front, Back, Right, Left })
            {
                faces.Add(Face(outer, BoxLoops[side]));
                faces.Add(Face(hole, BoxLoops[side]).Flip());
            }

            return new GeoSolid3(faces);
        }

        #endregion

        #region Bodies with copies of a corner in one face or two, and meshes finer than the widest gap

        /// <summary>
        /// The box with its front in two, a crack as wide as given between the two at x = 15 from the bottom to the top, and
        /// the top's and the bottom's rings through the corners of both sides of it: each spans the crack by an edge of its
        /// own as long as the crack is wide, its two corners copies of one point.
        /// </summary>
        internal static GeoSolid3 BoxCrackedThroughTheFront(double width)
        {
            GeoPoint3[] c = Corners();
            List<GeoFace3> faces = BoxFaces(c);
            var lowLeft = new GeoPoint3(15, 0, 0);
            var lowRight = new GeoPoint3(15 + width, 0, 0);
            var highLeft = new GeoPoint3(15, 0, 10);
            var highRight = new GeoPoint3(15 + width, 0, 10);
            faces[Bottom] = Face(c[0], c[3], c[2], c[1], lowRight, lowLeft);
            faces[Top] = Face(c[4], highLeft, highRight, c[5], c[6], c[7]);
            faces[Front] = Face(c[0], lowLeft, highLeft, c[4]);
            faces.Add(Face(lowRight, c[1], c[5], highRight));
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The box with the top's ring through the corner over (30, 20) and then through a copy of it moved out of the top
        /// along its diagonal by as much as given: the right and the back meet at the corner, and the top has both.
        /// </summary>
        internal static GeoSolid3 BoxWithATopCornerDoubled(double by)
        {
            GeoPoint3[] c = Corners();
            List<GeoFace3> faces = BoxFaces(c);
            faces[Top] = Face(c[4], c[5], c[6], c[6].Add(OutOfTheTop(6).Multiply(by)), c[7]);
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The box with the top's corner over (30, 20) in two copies, one moved off the right's plane by as much as given and
        /// the other off the back's, the right and the back meeting at the corner itself.
        /// </summary>
        internal static GeoSolid3 BoxWithATopCornerInTwo(double by)
        {
            GeoPoint3[] c = Corners();
            List<GeoFace3> faces = BoxFaces(c);
            GeoPoint3 offTheRight = c[6].Add(new GeoVector3(by, by / 3.0, 0));
            GeoPoint3 offTheBack = c[6].Add(new GeoVector3(by / 3.0, by, 0));
            faces[Top] = Face(c[4], c[5], offTheRight, offTheBack, c[7]);
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The box with its top corner over (30, 20) cut off by a triangle as far along each edge as given, the triangle left
        /// out: each of its corners is a corner of the two faces meeting along that edge, exactly, and of no other, so that
        /// every two of them share a face.
        /// </summary>
        internal static GeoSolid3 BoxWithItsCornerCutOffOpen(double by)
        {
            GeoPoint3[] c = Corners();
            List<GeoFace3> faces = BoxFaces(c);
            var topAndRight = new GeoPoint3(30, 20 - by, 10);
            var rightAndBack = new GeoPoint3(30, 20, 10 - by);
            var backAndTop = new GeoPoint3(30 - by, 20, 10);
            faces[Top] = Face(c[4], c[5], topAndRight, backAndTop, c[7]);
            faces[Right] = Face(c[1], c[2], rightAndBack, topAndRight, c[5]);
            faces[Back] = Face(c[2], c[3], c[7], backAndTop, rightAndBack);
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The box with the edge between its top and its right chamfered as far along each as given and the chamfer left out:
        /// the front and the back each span the strip by a short edge from a corner of the top to one of the right.
        /// </summary>
        internal static GeoSolid3 BoxWithAChamferMissing(double by)
        {
            GeoPoint3[] c = Corners();
            List<GeoFace3> faces = BoxFaces(c);
            var topFront = new GeoPoint3(30 - by, 0, 10);
            var topBack = new GeoPoint3(30 - by, 20, 10);
            var rightFront = new GeoPoint3(30, 0, 10 - by);
            var rightBack = new GeoPoint3(30, 20, 10 - by);
            faces[Top] = Face(c[4], topFront, topBack, c[7]);
            faces[Right] = Face(c[1], c[2], rightBack, rightFront);
            faces[Front] = Face(c[0], c[1], rightFront, topFront, c[4]);
            faces[Back] = Face(c[2], c[3], c[7], topBack, rightBack);
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The box with its top's copy of the corner over (30, 0) moved out by 0.003, as
        /// <see cref="BoxWithTopCornersMovedOut"/> moves it, and a flap 0.004 by 0.004 standing out of the top's front edge
        /// beside it, from x = 29.992, at 45 degrees between the top and the front: a fin, open along three of its edges.
        /// </summary>
        internal static GeoSolid3 BoxWithAMovedCornerAndAFlapBesideIt()
        {
            List<GeoFace3> faces = BoxWithTopCornersMovedOut((5, 0.003)).Faces.ToList();
            var from = new GeoPoint3(29.992, 0, 10);
            var to = new GeoPoint3(29.996, 0, 10);
            GeoVector3 out_ = new GeoVector3(0, -1, 1).Multiply(0.004 / Math.Sqrt(2.0));
            faces.Add(Face(from, to, to.Add(out_), from.Add(out_)));
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The quads of a grid of n by n cells as wide as given, from an origin along two axes, each wound round the first
        /// axis's cross product with the second, row by row along the first.
        /// </summary>
        internal static IEnumerable<GeoPoint3[]> Grid(GeoPoint3 origin, GeoVector3 u, GeoVector3 v, int n, double cell)
        {
            GeoPoint3 At(int i, int j) => origin.Add(u.Multiply(i * cell)).Add(v.Multiply(j * cell));

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    yield return new[] { At(i, j), At(i + 1, j), At(i + 1, j + 1), At(i, j + 1) };
                }
            }
        }

        /// <summary>
        /// The box with a hole in its top from (15, 10) filled by a grid of n by n quads as wide as given, or by twice as many
        /// triangles, the one at the middle, the quad n / 2 cells from (15, 10) each way or the first triangle of it, wound
        /// the wrong way: no edge of it longer than the cell's diagonal.
        /// </summary>
        internal static GeoSolid3 BoxWithAFinePatchOneTurned(int n, double cell, bool triangles)
        {
            GeoPoint3[] c = Corners();
            List<GeoFace3> faces = BoxFaces(c);
            double side = n * cell;
            GeoPoint3[] hole = Corners(15, 10, 0, 15 + side, 10 + side, 10);
            faces[Top] = new GeoFace3(Loop(c, BoxLoops[Top]), new[] { Loop(hole, BoxLoops[Top]) }, Fine);
            int middle = (n / 2) * n + n / 2, k = 0;

            foreach (GeoPoint3[] q in Grid(new GeoPoint3(15, 10, 10), GeoVector3.XAxis, GeoVector3.YAxis, n, cell))
            {
                bool turned = k++ == middle;
                GeoPoint3[][] pieces = triangles ? new[] { new[] { q[0], q[1], q[2] }, new[] { q[0], q[2], q[3] } } : new[] { q };

                for (int p = 0; p < pieces.Length; p++)
                {
                    faces.Add(Face(turned && p == 0 ? pieces[p].Reverse().ToArray() : pieces[p]));
                }
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// A cube as wide as given from the origin, each of its faces a grid of n by n quads, every quad wound inwards.
        /// </summary>
        internal static GeoSolid3 FineCubeInsideOut(double size, int n)
        {
            var sides = new (GeoPoint3 Origin, GeoVector3 U, GeoVector3 V)[]
            {
                (new GeoPoint3(0, 0, size), GeoVector3.XAxis, GeoVector3.YAxis),
                (new GeoPoint3(0, 0, 0), GeoVector3.YAxis, GeoVector3.XAxis),
                (new GeoPoint3(size, 0, 0), GeoVector3.YAxis, GeoVector3.ZAxis),
                (new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, GeoVector3.YAxis),
                (new GeoPoint3(0, size, 0), GeoVector3.ZAxis, GeoVector3.XAxis),
                (new GeoPoint3(0, 0, 0), GeoVector3.XAxis, GeoVector3.ZAxis),
            };

            return new GeoSolid3(sides.SelectMany(s => Grid(s.Origin, s.U, s.V, n, size / n)).Select(q => Face(q.Reverse().ToArray())));
        }

        /// <summary>
        /// The box turned about (1, 1, 1) by 0.7 and moved by (0.1, 0.2, 0.3), every face two triangles on copies of corners
        /// of their own, each copy moved in its triangle's plane by a random vector no longer than given: no face lies across
        /// an axis, and no two triangles share a corner.
        /// </summary>
        internal static GeoSolid3 TurnedBoxOfTrianglesOnMovedCopies(int seed, double most)
        {
            var random = new Random(seed);
            GeoPoint3[] c = RotatedCorners(new GeoVector3(1, 1, 1), 0.7, new GeoVector3(0.1, 0.2, 0.3));
            var faces = new List<GeoFace3>();

            GeoFace3 Moved(params GeoPoint3[] ring)
            {
                GeoVector3 u = ring[0].GetVectorTo(ring[1]);
                GeoVector3 v = u.CrossProduct(ring[0].GetVectorTo(ring[2])).CrossProduct(u);
                u = u.Multiply(1.0 / u.Length);
                v = v.Multiply(1.0 / v.Length);
                return Face(ring.Select(p =>
                {
                    double angle = 2.0 * Math.PI * random.NextDouble(), length = most * random.NextDouble();
                    return p.Add(u.Multiply(length * Math.Cos(angle))).Add(v.Multiply(length * Math.Sin(angle)));
                }).ToArray());
            }

            foreach (int[] loop in BoxLoops)
            {
                faces.Add(Moved(c[loop[0]], c[loop[1]], c[loop[2]]));
                faces.Add(Moved(c[loop[0]], c[loop[2]], c[loop[3]]));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// A prism over a polygon of as many sides as given round a circle, upright, every face on copies of corners of its
        /// own: a side's copies moved along the rim by up to a distance either way and up or down by up to a lift, a cap's in
        /// its plane by up to the distance. Every copy of a corner of a cap stands within the lift of the cap's plane, so that
        /// whichever copy a corner is welded to, the cap's corners stay within the lift of it.
        /// </summary>
        internal static GeoSolid3 PrismOfCopiesMovedAlongTheRim(int sides, double radius, double height, double most, double lift, int seed)
        {
            var random = new Random(seed);

            GeoPoint3 At(int i, double z)
            {
                double angle = 2.0 * Math.PI * (i % sides) / sides;
                return new GeoPoint3(radius * Math.Cos(angle), radius * Math.Sin(angle), z);
            }

            GeoPoint3 Along(GeoPoint3 p, GeoVector3 rim)
            {
                double u = most * (2.0 * random.NextDouble() - 1.0), v = lift * (2.0 * random.NextDouble() - 1.0);
                return p.Add(rim.Multiply(u / rim.Length)).Add(new GeoVector3(0, 0, v));
            }

            GeoPoint3 InThePlane(GeoPoint3 p)
            {
                double angle = 2.0 * Math.PI * random.NextDouble(), r = most * random.NextDouble();
                return p.Add(new GeoVector3(r * Math.Cos(angle), r * Math.Sin(angle), 0));
            }

            var faces = new List<GeoFace3>();

            for (int i = 0; i < sides; i++)
            {
                GeoPoint3 a = At(i, 0), b = At(i + 1, 0), c = At(i + 1, height), d = At(i, height);
                GeoVector3 rim = a.GetVectorTo(b);
                faces.Add(Face(Along(a, rim), Along(b, rim), Along(c, rim), Along(d, rim)));
            }

            faces.Add(Face(Enumerable.Range(0, sides).Reverse().Select(i => InThePlane(At(i, 0))).ToArray()));
            faces.Add(Face(Enumerable.Range(0, sides).Select(i => InThePlane(At(i, height))).ToArray()));
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// A sphere of rings of quads and caps of triangles about the origin, bands from pole to pole by sides round, every
        /// face on copies of corners of its own, each moved in the face's plane by a random vector no longer than given. A
        /// patch of a few of its faces lies across the middle of the patch's own box, so that, measured from there, it seems
        /// to hold a volume as though wound inwards.
        /// </summary>
        internal static GeoSolid3 SphereOfMovedCopies(double radius, int bands, int sides, double most, int seed)
        {
            var random = new Random(seed);

            GeoPoint3 P(int k, int j)
            {
                double phi = Math.PI * k / bands, lambda = 2.0 * Math.PI * (j % sides) / sides;
                return new GeoPoint3(radius * Math.Sin(phi) * Math.Cos(lambda), radius * Math.Sin(phi) * Math.Sin(lambda), radius * Math.Cos(phi));
            }

            GeoFace3 Moved(params GeoPoint3[] ring)
            {
                GeoVector3 u = ring[0].GetVectorTo(ring[1]);
                GeoVector3 v = u.CrossProduct(ring[0].GetVectorTo(ring[2])).CrossProduct(u);
                u = u.Multiply(1.0 / u.Length);
                v = v.Multiply(1.0 / v.Length);
                return Face(ring.Select(p =>
                {
                    double angle = 2.0 * Math.PI * random.NextDouble(), length = most * random.NextDouble();
                    return p.Add(u.Multiply(length * Math.Cos(angle))).Add(v.Multiply(length * Math.Sin(angle)));
                }).ToArray());
            }

            var faces = new List<GeoFace3>();

            for (int k = 0; k < bands; k++)
            {
                for (int j = 0; j < sides; j++)
                {
                    if (k == 0)
                    {
                        faces.Add(Moved(new GeoPoint3(0, 0, radius), P(1, j), P(1, j + 1)));
                    }
                    else if (k == bands - 1)
                    {
                        faces.Add(Moved(P(k, j), new GeoPoint3(0, 0, -radius), P(k, j + 1)));
                    }
                    else
                    {
                        faces.Add(Moved(P(k, j), P(k + 1, j), P(k + 1, j + 1), P(k, j + 1)));
                    }
                }
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The corners of a polygon of as many sides as given round a circle about the vertical through the origin, at z = 0,
        /// counter-clockwise seen from above, the first on the x axis.
        /// </summary>
        internal static GeoPoint3[] RoundPlan(int sides, double radius) => Enumerable.Range(0, sides)
            .Select(i => new GeoPoint3(radius * Math.Cos(2.0 * Math.PI * i / sides), radius * Math.Sin(2.0 * Math.PI * i / sides), 0))
            .ToArray();

        #endregion

        #region Bodies with a hole through them whose walls are left out

        /// <summary>
        /// The corners of the rectangle from (x0, y0) to (x1, y1) at z = 0, counter-clockwise seen from above from the first.
        /// </summary>
        internal static GeoPoint3[] Rectangle(double x0, double y0, double x1, double y1)
            => new[] { new GeoPoint3(x0, y0, 0), new GeoPoint3(x1, y0, 0), new GeoPoint3(x1, y1, 0), new GeoPoint3(x0, y1, 0) };

        /// <summary>
        /// A plate 30 by 30 as thick as given with a hole through it whose walls are left out: its top carries the top rim at
        /// z = the thickness, and its bottom the bottom rim at z = 0, each given counter-clockwise seen from above, their
        /// heights ignored. The bottom's ring of the hole runs clockwise, as the bottom's boundary does, from the corner of
        /// it given. Two flat loops are left open, one at each end of the hole.
        /// </summary>
        internal static GeoSolid3 PlateWithAHoleWithoutItsWalls(double thickness, IReadOnlyList<GeoPoint3> top, IReadOnlyList<GeoPoint3> bottom, int bottomStart = 0)
        {
            GeoPoint3[] outer = Corners(0, 0, 0, 30, 30, thickness);
            var up = new GeoPolygon3(top.Select(p => new GeoPoint3(p.X, p.Y, thickness)), Fine);
            List<GeoPoint3> down = bottom.Select(p => new GeoPoint3(p.X, p.Y, 0)).Reverse().ToList();
            int start = ((bottomStart % down.Count) + down.Count) % down.Count;
            var under = new GeoPolygon3(down.Skip(start).Concat(down.Take(start)), Fine);
            var faces = new List<GeoFace3>
            {
                new GeoFace3(Loop(outer, BoxLoops[Bottom]), new[] { under }, Fine),
                new GeoFace3(Loop(outer, BoxLoops[Top]), new[] { up }, Fine),
            };

            foreach (int side in new[] { Front, Back, Right, Left })
            {
                faces.Add(Face(outer, BoxLoops[side]));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// A body made by running a section in x and z along y from 0 to a length: the section given clockwise seen with x to
        /// the right and z up, one face across each of its edges, the face across edge i running from corner i to corner
        /// i + 1, and one face at each end. The faces across the edges named are pierced by the holes given, each wound as
        /// the face's boundary is.
        /// </summary>
        internal static GeoSolid3 ExtrudedAlongY(IReadOnlyList<(double X, double Z)> section, double length, IReadOnlyDictionary<int, GeoPoint3[]> holes)
        {
            int n = section.Count;
            GeoPoint3 At(int i, double y) => new GeoPoint3(section[i % n].X, y, section[i % n].Z);
            var faces = new List<GeoFace3>
            {
                Face(Enumerable.Range(0, n).Reverse().Select(i => At(i, 0)).ToArray()),
                Face(Enumerable.Range(0, n).Select(i => At(i, length)).ToArray()),
            };

            for (int i = 0; i < n; i++)
            {
                var boundary = new GeoPolygon3(new[] { At(i, 0), At(i + 1, 0), At(i + 1, length), At(i, length) }, Fine);
                GeoPolygon3[] pierced = holes != null && holes.TryGetValue(i, out GeoPoint3[] hole) ? new[] { new GeoPolygon3(hole, Fine) } : null;
                faces.Add(new GeoFace3(boundary, pierced, Fine));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// A C 30 by 30 by 10 run along y, its section a bottom plate from z = 0 to 2 and a top plate from 8 to 10, both from
        /// x = 0 to 30, joined by a web from x = 0 to 2, 3 960 in all; its top and its bottom each pierced by a square hole
        /// from (12, 10) to (22, 20) whose walls are left out. A tube from the one rim to the other would run through both
        /// plates and across the gap between them, crossing the top plate's underside and the bottom plate's top.
        /// </summary>
        internal static GeoSolid3 ChannelPiercedTopAndBottom()
        {
            var section = new[] { (0.0, 0.0), (0.0, 10.0), (30.0, 10.0), (30.0, 8.0), (2.0, 8.0), (2.0, 2.0), (30.0, 2.0), (30.0, 0.0) };
            var holes = new Dictionary<int, GeoPoint3[]>
            {
                [1] = new[] { new GeoPoint3(12, 10, 10), new GeoPoint3(22, 10, 10), new GeoPoint3(22, 20, 10), new GeoPoint3(12, 20, 10) },
                [7] = new[] { new GeoPoint3(22, 10, 0), new GeoPoint3(12, 10, 0), new GeoPoint3(12, 20, 0), new GeoPoint3(22, 20, 0) },
            };
            return ExtrudedAlongY(section, 30, holes);
        }

        /// <summary>
        /// A U 30 by 30 by 20 run along y, its section a base from z = 0 to 2 and two arms up to z = 20, from x = 0 to 10
        /// and from 12 to 30, 16 920 in all; each arm's face into the gap 2 wide between them pierced by a square hole from
        /// y = 10 to 20 and z = 8 to 18, opposite each other. The two holes face each other across the gap: a tube between
        /// them would be a bar laid across it, not a hole through anything.
        /// </summary>
        internal static GeoSolid3 ChannelPiercedFacingAcrossItsGap()
        {
            var section = new[] { (0.0, 0.0), (0.0, 20.0), (10.0, 20.0), (10.0, 2.0), (12.0, 2.0), (12.0, 20.0), (30.0, 20.0), (30.0, 0.0) };
            var holes = new Dictionary<int, GeoPoint3[]>
            {
                [2] = new[] { new GeoPoint3(10, 10, 18), new GeoPoint3(10, 10, 8), new GeoPoint3(10, 20, 8), new GeoPoint3(10, 20, 18) },
                [4] = new[] { new GeoPoint3(12, 10, 8), new GeoPoint3(12, 10, 18), new GeoPoint3(12, 20, 18), new GeoPoint3(12, 20, 8) },
            };
            return ExtrudedAlongY(section, 30, holes);
        }

        /// <summary>
        /// The box sheared along x, its top moved 5 that way over the bottom, with its top and bottom left out: the rims are
        /// the one the other moved along a slant, and the faces between them are the sides it has. 6 000 in all.
        /// </summary>
        internal static GeoSolid3 ShearedBoxMissingItsTopAndBottom()
        {
            GeoPoint3[] c = Corners();

            for (int i = 4; i < 8; i++)
            {
                c[i] = c[i].Add(new GeoVector3(5, 0, 0));
            }

            return new GeoSolid3(BoxFaces(c).Where((face, f) => f != Top && f != Bottom));
        }

        #endregion

        #region Prisms with a concave top left out, lifted at a corner

        /// <summary>
        /// The plan of the L of <see cref="LShapedPrism"/>, 20 along each leg and 10 wide, 300 in all, counter-clockwise
        /// seen from above from the origin: corner 3, at (10, 10), is where it turns in.
        /// </summary>
        internal static GeoPoint3[] LShapedPlan() => new[]
        {
            new GeoPoint3(0, 0, 0), new GeoPoint3(20, 0, 0), new GeoPoint3(20, 10, 0),
            new GeoPoint3(10, 10, 0), new GeoPoint3(10, 20, 0), new GeoPoint3(0, 20, 0),
        };

        /// <summary>
        /// The plan of a U 30 by 20 with a notch 10 by 10 cut into the middle of its back, 500 in all, counter-clockwise seen
        /// from above from the origin: corners 4 and 5, at (20, 10) and (10, 10), are where it turns in.
        /// </summary>
        internal static GeoPoint3[] UShapedPlan() => new[]
        {
            new GeoPoint3(0, 0, 0), new GeoPoint3(30, 0, 0), new GeoPoint3(30, 20, 0), new GeoPoint3(20, 20, 0),
            new GeoPoint3(20, 10, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(10, 20, 0), new GeoPoint3(0, 20, 0),
        };

        /// <summary>
        /// A prism over a plan from z = 0 to a height, its top left out and the top's corner over the plan's corner given
        /// lifted by as much as given in the two sides beside it, which stand upright and stay flat.
        /// </summary>
        internal static GeoSolid3 PrismWithoutItsTopLifted(IReadOnlyList<GeoPoint3> plan, double height, int corner, double lift)
        {
            GeoPoint3[] down = plan.Select(p => new GeoPoint3(p.X, p.Y, 0)).ToArray();
            GeoPoint3[] up = plan.Select((p, i) => new GeoPoint3(p.X, p.Y, height + (i == corner ? lift : 0.0))).ToArray();
            var faces = new List<GeoFace3> { Face(down.Reverse().ToArray()) };

            for (int i = 0; i < plan.Count; i++)
            {
                int j = (i + 1) % plan.Count;
                faces.Add(Face(down[i], down[j], up[j], up[i]));
            }

            return new GeoSolid3(faces);
        }

        #endregion

        #region Bodies with shells inside shells

        /// <summary>
        /// The faces of a block 10 by 10 by 6 inside the box of the damaged bodies, from (10, 5, 2) to (20, 15, 8), touching
        /// none of its faces, wound outwards: 600, its faces 440 in all. With the box, both wound outwards, a solid inside a
        /// solid, as a real part came: 6 600.
        /// </summary>
        internal static List<GeoFace3> BlockFaces() => BoxFaces(Corners(10, 5, 2, 20, 15, 8));

        /// <summary>
        /// The faces of a block 4 by 4 by 4 inside the block of <see cref="BlockFaces"/>, from (13, 8, 3) to (17, 12, 7),
        /// touching none of its faces, wound outwards: 64, its faces 96 in all.
        /// </summary>
        internal static List<GeoFace3> InnerBlockFaces() => BoxFaces(Corners(13, 8, 3, 17, 12, 7));

        /// <summary>
        /// The faces of a block 6 by 5 by 6 in the notch of the L of <see cref="LShapedPrism"/>, from (12, 12, 2) to
        /// (18, 17, 8): inside the box of the L and outside the L, touching none of its faces, wound outwards: 180, its faces
        /// 192 in all.
        /// </summary>
        internal static List<GeoFace3> NotchBlockFaces() => BoxFaces(Corners(12, 12, 2, 18, 17, 8));

        /// <summary>The faces each turned over, wound the other way round, in their order.</summary>
        internal static List<GeoFace3> Turned(IEnumerable<GeoFace3> faces) => faces.Select(face => face.Flip()).ToList();

        #endregion

        #region Bodies a fill would cross, and bodies it would only touch

        /// <summary>
        /// The faces of a post 4 by 4 standing in the middle of the plan of the box of the damaged bodies, from (13, 8, z0)
        /// to (17, 12, z1), wound outwards and touching none of the box's faces: 16 times its height.
        /// </summary>
        internal static List<GeoFace3> PostFaces(double z0, double z1) => BoxFaces(Corners(13, 8, z0, 17, 12, z1));

        /// <summary>
        /// The faces of a ridge along x from 13 to 17 over the top's plane of the box of the damaged bodies, z = 10, wound
        /// outwards: a prism over a triangle in y and z, its corner down at y = 10 as far below that plane as given, and its
        /// top from y = 8 to 12 at z = 13. Its lowest edge, from x = 13 to 17, lies that far below the plane, in it where
        /// nought is given: 24, and eight times as much again as it is sunk.
        /// </summary>
        internal static List<GeoFace3> RidgeFaces(double sunk)
        {
            var low0 = new GeoPoint3(13, 10, 10 - sunk);
            var low1 = new GeoPoint3(17, 10, 10 - sunk);
            var front0 = new GeoPoint3(13, 8, 13);
            var front1 = new GeoPoint3(17, 8, 13);
            var back0 = new GeoPoint3(13, 12, 13);
            var back1 = new GeoPoint3(17, 12, 13);
            return new List<GeoFace3>
            {
                Face(low0, front0, back0),
                Face(low1, back1, front1),
                Face(low0, low1, front1, front0),
                Face(low0, back0, back1, low1),
                Face(front0, front1, back1, back0),
            };
        }

        /// <summary>
        /// The faces of a pyramid standing on its tip on the top's plane of the box of the damaged bodies, the tip at
        /// (x, y, 10) and the base the square 4 by 4 round it at z = 13, wound outwards: 16. Over (15, 10) the tip lies in
        /// the middle of the top, over (15, 0) on the top's front edge, and over (30, 20) on its corner.
        /// </summary>
        internal static List<GeoFace3> PyramidOnItsTipFaces(double x, double y)
        {
            var tip = new GeoPoint3(x, y, 10);
            GeoPoint3[] square =
            {
                new GeoPoint3(x - 2, y - 2, 13), new GeoPoint3(x + 2, y - 2, 13), new GeoPoint3(x + 2, y + 2, 13), new GeoPoint3(x - 2, y + 2, 13),
            };
            var faces = new List<GeoFace3> { Face(square) };

            for (int i = 0; i < 4; i++)
            {
                faces.Add(Face(tip, square[(i + 1) % 4], square[i]));
            }

            return faces;
        }

        /// <summary>
        /// A hopper missing its top: its rim the rectangle from (0, 0) to (30, 20) at z = 10, its bottom the one from (9, 9)
        /// to (21, 11) at z = 0, and four walls sloping in from the one to the other; the rim's corner given, 0 to 3
        /// counter-clockwise seen from above from the origin, lifted by as much as given, moved up along the edge between
        /// the two walls beside it so that both stay flat, and out by nine tenths of the lift along x and along y. 2 580
        /// with no lift and its top.
        /// </summary>
        internal static GeoSolid3 HopperMissingItsTop(int corner, double lift)
        {
            GeoPoint3[] bottom = Rectangle(9, 9, 21, 11);
            GeoPoint3[] rim = Rectangle(0, 0, 30, 20).Select(p => new GeoPoint3(p.X, p.Y, 10)).ToArray();
            rim[corner] = rim[corner].Add(bottom[corner].GetVectorTo(rim[corner]).Multiply(lift / 10.0));
            var faces = new List<GeoFace3> { Face(bottom.Reverse().ToArray()) };

            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                faces.Add(Face(bottom[i], bottom[j], rim[j], rim[i]));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The faces of a box over the square from (0, 0) to (6, 6) standing on z = 0, open at the top, its rim 16, 10, 16 and
        /// 14 high over the square's corners counter-clockwise from the origin: about 1.8 off flat. The triangles of least
        /// area across it, 51.08, join its corners 0 and 2, 16 high across the middle, and it holds 528 with them.
        /// </summary>
        internal static List<GeoFace3> BoxOpenUpwardsFaces() => BoxOpenAtARimFaces(new[] { 16.0, 10.0, 16.0, 14.0 }, 0.0);

        /// <summary>
        /// The faces of a box over the same square hanging from z = 25, open at the bottom, its rim 16.5, 14.5, 20 and 14.5
        /// high, 0.5 or more above the rim of <see cref="BoxOpenUpwardsFaces"/> at each corner: about 1.7 off flat. The
        /// triangles of least area across it, 49.37, join its corners 1 and 3, 14.5 high across the middle, below the other's
        /// there, and it holds 333 with them.
        /// </summary>
        internal static List<GeoFace3> BoxOpenDownwardsFaces() => BoxOpenAtARimFaces(new[] { 16.5, 14.5, 20.0, 14.5 }, 25.0);

        /// <summary>
        /// The faces of a box over the square from (0, 0) to (6, 6), its rim at the heights given over the square's corners
        /// counter-clockwise from the origin, and its other end flat at the height given, below the rim or above it.
        /// </summary>
        private static List<GeoFace3> BoxOpenAtARimFaces(double[] rim, double end)
        {
            GeoPoint3[] square = Rectangle(0, 0, 6, 6);
            GeoPoint3[] up = square.Select((p, i) => new GeoPoint3(p.X, p.Y, end < rim[i] ? rim[i] : end)).ToArray();
            GeoPoint3[] down = square.Select((p, i) => new GeoPoint3(p.X, p.Y, end < rim[i] ? end : rim[i])).ToArray();
            var faces = new List<GeoFace3> { end < rim[0] ? Face(down.Reverse().ToArray()) : Face(up) };

            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                faces.Add(Face(down[i], down[j], up[j], up[i]));
            }

            return faces;
        }

        #endregion

        #region Bodies a weld would close across a blade

        /// <summary>
        /// The faces of a blade as thick as given standing upright along y, its middle at x = 15, from y = 8 to 12 and from
        /// z = 5 to 15, wound outwards: through the top's plane of the box of the damaged bodies, standing in the strip of
        /// <see cref="BoxWithAStripOfTheTopMissing"/> where that is wider than the blade, and touching none of its faces. 40
        /// times its thickness.
        /// </summary>
        internal static List<GeoFace3> BladeFaces(double thickness) => BoxFaces(Corners(15 - (thickness / 2), 8, 5, 15 + (thickness / 2), 12, 15));

        /// <summary>
        /// The faces of the blade of <see cref="BladeFaces"/> moved out beyond the front of the box of the damaged bodies, in
        /// line with the strip of <see cref="BoxWithAStripOfTheTopMissing"/> and as far clear of the front as given: from
        /// y = -4 less that to y = less that.
        /// </summary>
        internal static List<GeoFace3> BladeBeyondTheFrontFaces(double thickness, double clear)
            => BoxFaces(Corners(15 - (thickness / 2), -4 - clear, 5, 15 + (thickness / 2), -clear, 15));

        #endregion
    }
}

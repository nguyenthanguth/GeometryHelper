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
    }
}

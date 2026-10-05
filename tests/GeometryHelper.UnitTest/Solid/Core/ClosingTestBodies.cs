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
    }
}

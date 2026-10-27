using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A cell a plane crossed and could not cut is judged by one point, and is misjudged where the other body lies one way to
    /// its material on one side of the plane and the other way to the rest; the check for it needs a point on each side.
    /// </summary>
    public class MisjudgedCellTests
    {
        private static readonly Tolerance Fine = Tolerance.Default;

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        private static readonly GeoPlane3 NearTheTop = new GeoPlane3(new GeoPoint3(0, 0, 9.5), GeoVector3.ZAxis);

        [Fact]
        public void APointAboveAPlaneNearTheTopOfABox_IsFound()
        {
            // The only triangles of the box above the plane are its top's, and the middle of the box under them is below
            // the plane: the point is the middle of what lies above it.
            Assert.True(Boolean3.TryGetPointBeside(Box(0, 0, 0, 10, 10, 10), NearTheTop, 1.0, Fine, out GeoPoint3 above));
            Assert.InRange(above.Z, 9.5 + Fine.EqualPlanar, 10.0);

            Assert.True(Boolean3.TryGetPointBeside(Box(0, 0, 0, 10, 10, 10), NearTheTop, -1.0, Fine, out GeoPoint3 below));
            Assert.InRange(below.Z, 0.0, 9.5 - Fine.EqualPlanar);
        }

        [Fact]
        public void ACellWhoseTopOnlyLiesBeyondTheOther_IsMisjudged()
        {
            // A slab of a Tekla model less a tool whose sloping top a plane of the slab's could not cut came out 18 litres
            // short: the cell along the plane was judged by a point within the tool, and the check found no point of it
            // beyond the tool's top, 131 millimetres thick there, and took the cut for consistent.
            GeoSolid3 cell = Box(0, 0, 0, 10, 10, 10);
            GeoSolid3 tool = Box(-1, -1, -1, 11, 11, 9.5);

            Assert.True(Boolean3.Misjudged(new List<GeoSolid3> { cell }, new List<GeoPlane3> { NearTheTop }, cell, tool, false, Fine));
        }

        // The box 0..10 with its top's corner over (10, 10) moved along x: open by copies of two edges that far apart, a
        // sliver, and welded closed where that is within four tolerances.
        private static GeoSolid3 BoxOpenAtACorner(double shift)
        {
            GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);
            var faces = new List<GeoFace3>
            {
                new GeoFace3(new GeoPolygon3(new[] { P(0, 0, 0), P(0, 10, 0), P(10, 10, 0), P(10, 0, 0) }, Fine)),
                new GeoFace3(new GeoPolygon3(new[] { P(0, 0, 10), P(10, 0, 10), P(10 + shift, 10, 10), P(0, 10, 10) }, Fine)),
                new GeoFace3(new GeoPolygon3(new[] { P(0, 0, 0), P(10, 0, 0), P(10, 0, 10), P(0, 0, 10) }, Fine)),
                new GeoFace3(new GeoPolygon3(new[] { P(10, 0, 0), P(10, 10, 0), P(10, 10, 10), P(10, 0, 10) }, Fine)),
                new GeoFace3(new GeoPolygon3(new[] { P(10, 10, 0), P(0, 10, 0), P(0, 10, 10), P(10, 10, 10) }, Fine)),
                new GeoFace3(new GeoPolygon3(new[] { P(0, 10, 0), P(0, 0, 0), P(0, 0, 10), P(0, 10, 10) }, Fine)),
            };
            return new GeoSolid3(faces);
        }

        [Fact]
        public void CutByEveryPlaneOfBoth_AMisjudgedCellTakesTheOpenResultOfCuttingOneBody_WeldedClosed()
        {
            // A slab of a Tekla model less a tool beneath it, cut one body at a time, came out open either way, the tool cut
            // holding what it should; cut by every plane of both, the slab kept a cell the tool's top could not cut, judged
            // within the tool, and the cells glued closed and three litres short. The open result welded closed is taken.
            GeoSolid3 open = BoxOpenAtACorner(0.0015);
            Assert.False(open.IsClosed(Fine));
            List<GeoFace3> cells = Box(0, 0, 0, 10, 10, 9).Faces.ToList();

            Assert.True(Boolean3.GlueOrKeep(new List<GeoFace3>(cells), null, open, true, Fine, out GeoSolid3 kept));
            Assert.True(kept.IsClosed(Fine));
            Assert.Equal(1000.0, kept.GetVolume(Fine), 1);

            // Not misjudged, what the cells glue into closed is taken, as before.
            Assert.True(Boolean3.GlueOrKeep(new List<GeoFace3>(cells), null, open, false, Fine, out GeoSolid3 glued));
            Assert.True(glued.IsClosed(Fine));
            Assert.Equal(900.0, glued.GetVolume(Fine), 6);
        }

        [Fact]
        public void CutByEveryPlaneOfBoth_AMisjudgedCellGluedIntoWhatTheOpenResultHolds_IsTaken()
        {
            // A cell can be judged apart across a plane by a hair of it beyond the plane: of slabs of a Tekla model cut one
            // after another, the cells so judged glued into what they should where the open result would not weld closed.
            GeoSolid3 open = BoxOpenAtACorner(0.006);
            Assert.False(Weld3.Sealed(open, Fine).IsClosed(Fine));

            Assert.True(Boolean3.GlueOrKeep(Box(0, 0, 0, 10, 10, 10).Faces.ToList(), null, open, true, Fine, out GeoSolid3 kept));
            Assert.True(kept.IsClosed(Fine));
            Assert.Equal(1000.0, kept.GetVolume(Fine), 6);
        }

        [Fact]
        public void CutByEveryPlaneOfBoth_AMisjudgedCellGluedIntoAnotherVolume_KeepsTheOpenResult()
        {
            // Open, it says so to Validate; closed with the wrong volume, it said nothing.
            GeoSolid3 open = BoxOpenAtACorner(0.006);

            Assert.True(Boolean3.GlueOrKeep(Box(0, 0, 0, 10, 10, 9).Faces.ToList(), null, open, true, Fine, out GeoSolid3 kept));
            Assert.Same(open, kept);
        }

        // A face of the corners given, wound to face along (ox, oy, oz).
        private static GeoFace3 Facing(double ox, double oy, double oz, params GeoPoint3[] corners)
        {
            double nx = 0, ny = 0, nz = 0;

            for (int i = 0; i < corners.Length; i++)
            {
                GeoPoint3 p = corners[i], q = corners[(i + 1) % corners.Length];
                nx += (p.Y - q.Y) * (p.Z + q.Z);
                ny += (p.Z - q.Z) * (p.X + q.X);
                nz += (p.X - q.X) * (p.Y + q.Y);
            }

            if (nx * ox + ny * oy + nz * oz < 0)
            {
                System.Array.Reverse(corners);
            }

            return new GeoFace3(new GeoPolygon3(corners));
        }

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        /// <summary>
        /// A U, x 0..30, y 0..10 and z 0..10 less a notch x from left to right down to floor, whose bottom is three faces
        /// meeting along cracks from x = a - slant to a + slant and from c - slant to c + slant across y, and whose outer sides
        /// are two faces each meeting along a crack from z = h - slant to h + slant; the two copies of each crack's edge are gap
        /// apart at y = 0, nought at y = 10. Closed within the default tolerance where the gap is.
        /// </summary>
        private static GeoSolid3 CrackedU(double left, double right, double floor, double a, double c, double h, double slant, double gap)
        {
            double xa = a - slant, xb = a + slant, xc = c - slant, xd = c + slant, za = h - slant, zb = h + slant;
            return new GeoSolid3(new List<GeoFace3>
            {
                Facing(0, 0, -1, P(0, 0, 0), P(xa, 0, 0), P(xb, 10, 0), P(0, 10, 0)),
                Facing(0, 0, -1, P(xa + gap, 0, 0), P(xc, 0, 0), P(xd, 10, 0), P(xb, 10, 0)),
                Facing(0, 0, -1, P(xc + gap, 0, 0), P(30, 0, 0), P(30, 10, 0), P(xd, 10, 0)),
                Facing(-1, 0, 0, P(0, 0, 0), P(0, 10, 0), P(0, 10, zb), P(0, 0, za)),
                Facing(-1, 0, 0, P(0, 0, za + gap), P(0, 10, zb), P(0, 10, 10), P(0, 0, 10)),
                Facing(1, 0, 0, P(30, 0, 0), P(30, 10, 0), P(30, 10, zb), P(30, 0, za)),
                Facing(1, 0, 0, P(30, 0, za + gap), P(30, 10, zb), P(30, 10, 10), P(30, 0, 10)),
                Facing(0, 0, 1, P(0, 0, 10), P(left, 0, 10), P(left, 10, 10), P(0, 10, 10)),
                Facing(0, 0, 1, P(right, 0, 10), P(30, 0, 10), P(30, 10, 10), P(right, 10, 10)),
                Facing(1, 0, 0, P(left, 0, floor), P(left, 0, 10), P(left, 10, 10), P(left, 10, floor)),
                Facing(-1, 0, 0, P(right, 0, floor), P(right, 10, floor), P(right, 10, 10), P(right, 0, 10)),
                Facing(0, 0, 1, P(left, 0, floor), P(left, 10, floor), P(right, 10, floor), P(right, 0, floor)),
                Facing(0, -1, 0, P(0, 0, 0), P(xa, 0, 0), P(xa + gap, 0, 0), P(xc, 0, 0), P(xc + gap, 0, 0), P(30, 0, 0), P(30, 0, za), P(30, 0, za + gap), P(30, 0, 10), P(right, 0, 10), P(right, 0, floor), P(left, 0, floor), P(left, 0, 10), P(0, 0, 10), P(0, 0, za + gap), P(0, 0, za)),
                Facing(0, 1, 0, P(0, 10, 0), P(xb, 10, 0), P(xd, 10, 0), P(30, 10, 0), P(30, 10, zb), P(30, 10, 10), P(right, 10, 10), P(right, 10, floor), P(left, 10, floor), P(left, 10, 10), P(0, 10, 10), P(0, 10, zb)),
            });
        }

        /// <summary>
        /// A box x0..x1, y0..y1, z0..z1 whose bottom is faces meeting along cracks from x = p - slant to p + slant across y,
        /// one for each p among the planes given that lies within it, and whose sides x = x0 and x1 are faces meeting along
        /// cracks from z = q - slant to q + slant, the copies of each crack's edge gap apart at y = y0.
        /// </summary>
        private static GeoSolid3 CrackedBox(double x0, double y0, double z0, double x1, double y1, double z1, double[] xs, double[] zs, double slant, double gap)
        {
            double[] px = xs.Where(p => p - slant > x0 + 0.05 && p + slant < x1 - 0.05).OrderBy(p => p).ToArray();
            double[] qz = zs.Where(q => q - slant > z0 + 0.05 && q + slant < z1 - 0.05).OrderBy(q => q).ToArray();
            var faces = new List<GeoFace3>();

            for (int k = 0; k <= px.Length; k++)
            {
                double a0 = k == 0 ? x0 : px[k - 1] - slant + gap, a1 = k == px.Length ? x1 : px[k] - slant;
                double b0 = k == 0 ? x0 : px[k - 1] + slant, b1 = k == px.Length ? x1 : px[k] + slant;
                faces.Add(Facing(0, 0, -1, P(a0, y0, z0), P(a1, y0, z0), P(b1, y1, z0), P(b0, y1, z0)));
            }

            foreach (double x in new[] { x0, x1 })
            {
                for (int k = 0; k <= qz.Length; k++)
                {
                    double a0 = k == 0 ? z0 : qz[k - 1] - slant + gap, a1 = k == qz.Length ? z1 : qz[k] - slant;
                    double b0 = k == 0 ? z0 : qz[k - 1] + slant, b1 = k == qz.Length ? z1 : qz[k] + slant;
                    faces.Add(Facing(x == x0 ? -1 : 1, 0, 0, P(x, y0, a0), P(x, y0, a1), P(x, y1, b1), P(x, y1, b0)));
                }
            }

            faces.Add(Facing(0, 0, 1, P(x0, y0, z1), P(x1, y0, z1), P(x1, y1, z1), P(x0, y1, z1)));
            var front = new List<GeoPoint3> { P(x0, y0, z0) };

            foreach (double p in px)
            {
                front.Add(P(p - slant, y0, z0));
                front.Add(P(p - slant + gap, y0, z0));
            }

            front.Add(P(x1, y0, z0));

            foreach (double q in qz)
            {
                front.Add(P(x1, y0, q - slant));
                front.Add(P(x1, y0, q - slant + gap));
            }

            front.Add(P(x1, y0, z1));
            front.Add(P(x0, y0, z1));

            foreach (double q in qz.Reverse())
            {
                front.Add(P(x0, y0, q - slant + gap));
                front.Add(P(x0, y0, q - slant));
            }

            faces.Add(Facing(0, -1, 0, front.ToArray()));
            var back = new List<GeoPoint3> { P(x0, y1, z0) };
            back.AddRange(px.Select(p => P(p + slant, y1, z0)));
            back.Add(P(x1, y1, z0));
            back.AddRange(qz.Select(q => P(x1, y1, q + slant)));
            back.Add(P(x1, y1, z1));
            back.Add(P(x0, y1, z1));
            back.AddRange(qz.Reverse().Select(q => P(x0, y1, q + slant)));
            faces.Add(Facing(0, 1, 0, back.ToArray()));
            return new GeoSolid3(faces);
        }

        [Fact]
        public void AUCellABoxStandsInTheNotchOf_IsNotMisjudged()
        {
            // The U is one cell, none of x = 10.5, x = 19.5 and z = 4.5 cutting it across its cracks, and the box in its notch
            // touches none of its material. The middles of the parts the planes make of a U need not lie in them: the middle of
            // the part above z = 4.5, its two arms, lies at (15, 5, 7.25) in the notch, and read as in the box it made the cell
            // part in the box and part out.
            GeoSolid3 u = CrackedU(10, 20, 4, 10.5, 19.5, 4.5, 0.01, 0.0001).TurnOutwards();
            GeoSolid3 box = Box(10.5, -1, 4.5, 19.5, 11, 15);
            var stuck = new List<GeoPlane3>
            {
                new GeoPlane3(P(10.5, 0, 0), GeoVector3.XAxis),
                new GeoPlane3(P(19.5, 0, 0), GeoVector3.XAxis),
                new GeoPlane3(P(0, 0, 4.5), GeoVector3.ZAxis),
            };

            Assert.True(u.IsClosed(Fine));
            Assert.False(Boolean3.Misjudged(new List<GeoSolid3> { u }, stuck, u, box, false, Fine));
            Assert.False(Boolean3.Misjudged(new List<GeoSolid3> { u }, stuck, u, box, true, Fine));
        }

        private static double Overlap(double a0, double a1, double b0, double b1) => System.Math.Max(0, System.Math.Min(a1, b1) - System.Math.Max(a0, b0));

        // The cracked U and the cracked box their cracks lie under the planes of, cut within the default tolerance: their
        // common part is either not worked out or what it is, never nothing.
        private static void AssertCommonPartIsNotNothing(double left, double right, double floor, double x0, double y0, double z0, double x1, double y1, double z1, double slant, double gap)
        {
            GeoSolid3 u = CrackedU(left, right, floor, x0, x1, z0, slant, gap);
            GeoSolid3 box = CrackedBox(x0, y0, z0, x1, y1, z1, new[] { left, right }, new[] { floor, 10.0 }, slant, gap);
            double across = Overlap(0, 10, y0, y1);
            double expected = Overlap(0, 30, x0, x1) * across * Overlap(0, 10, z0, z1) - Overlap(left, right, x0, x1) * across * Overlap(floor, 10, z0, z1);

            Assert.True(u.Validate(Fine).IsValid);
            Assert.True(box.Validate(Fine).IsValid);

            foreach ((GeoSolid3 first, GeoSolid3 second) in new[] { (u, box), (box, u) })
            {
                bool made = Boolean3.TryIntersect(first, second, out GeoSolid3 common, Fine, out BooleanOutcome outcome);

                Assert.NotEqual(BooleanOutcome.Empty, outcome);

                if (made)
                {
                    double bound = Fine.EqualPoint * common.GetSurfaceArea(Fine);
                    Assert.InRange(common.GetVolume(Fine), expected - bound, expected + bound);
                }
            }
        }

        [Fact]
        public void TheCommonPartOfACrackedUAndACrackedBoxEachMisjudgedCutByTheOther_IsNotNothing()
        {
            // Each body cracked under the other's planes, each cut misjudged and the one kept gluing into nothing: the common
            // part, 38.14 worked out from the two, came back as nothing either way round.
            AssertCommonPartIsNotNothing(
                8.280907636639153, 18.356958149167223, 5.179333057803722,
                9.050414103898412, 1.372809038205449, 4.4976956444781715, 17.094529168398367, 8.329253873941141, 12.254967074494328,
                0.02068292379742576, 7.422590825131827E-05);
        }

        [Fact]
        public void TheCommonPartOfACrackedUAndACrackedBoxWhoseOtherCutKeepsNothing_IsNotNothing()
        {
            // The box cut by the U's planes is misjudged, rightly: it held 2.02 where the two share 26.95. The U cut by the
            // box's planes, taken instead, kept no cell; the common part came back as 2.02, trusted, and read anew it would
            // have been given as nothing either way round.
            AssertCommonPartIsNotNothing(
                10.745874486279615, 21.38455722266089, 5.207928926315126,
                10.766737980519764, 1.612918351596649, 5.1846118671282255, 22.061734899115628, 9.29461147230799, 15.037116623500882,
                0.003847574732955505, 1.1092776846951594E-05);
        }

        [Fact]
        public void TheUnionOfACrackedUAndACrackedBoxOverItsNotch_IsRightOrNotMade()
        {
            // The box's faces on the U's, its floor on the notch's floor and its left side on the notch's: cut the U's way
            // the U was one cell read part in the box, and the union, settled the box's way, counted the 15.31 the two share
            // twice and held 3 181.22 where they hold 3 165.91.
            double left = 8.233979439471838, right = 19.098251008008724, floor = 2.5054290688156287;
            double x0 = left, y0 = -1, z0 = floor, x1 = 19.30259374277787, y1 = 11, z1 = 10;
            double slant = 0.009503411387274216, gap = 8.286630889101773E-05;
            GeoSolid3 u = CrackedU(left, right, floor, x0, x1, z0, slant, gap);
            GeoSolid3 box = CrackedBox(x0, y0, z0, x1, y1, z1, new[] { left, right }, new[] { floor, 10.0 }, slant, gap);
            double common = (x1 - right) * 10 * (10 - floor);
            double expected = 3000 - (right - left) * 10 * (10 - floor) + (x1 - x0) * (y1 - y0) * (z1 - z0) - common;

            foreach ((GeoSolid3 first, GeoSolid3 second) in new[] { (u, box), (box, u) })
            {
                if (Boolean3.TryUnion(first, second, out GeoSolid3 union, Fine))
                {
                    double bound = Fine.EqualPoint * union.GetSurfaceArea(Fine);
                    Assert.InRange(union.GetVolume(Fine), expected - bound, expected + bound);
                }
            }
        }

        [Fact]
        public void ACellLyingTheSameWayToTheOtherOnBothSides_IsNotMisjudged()
        {
            GeoSolid3 cell = Box(0, 0, 0, 10, 10, 10);

            Assert.False(Boolean3.Misjudged(new List<GeoSolid3> { cell }, new List<GeoPlane3> { NearTheTop }, cell, Box(-1, -1, -1, 11, 11, 11), false, Fine));
            Assert.False(Boolean3.Misjudged(new List<GeoSolid3> { cell }, new List<GeoPlane3> { NearTheTop }, cell, Box(20, 20, 20, 30, 30, 30), false, Fine));
        }
    }
}

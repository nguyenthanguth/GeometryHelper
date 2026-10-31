using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// What a boolean asked at nought of a body that does not close within the rounding of its coordinates is checked by,
    /// after G1z. The common part of two bodies neither of which closes was not checked at all, and came out missing most of
    /// what the two share; a union of a cracked cube and a slab thousands long was turned down for a volume the slab cannot be
    /// read to; and a body read closed within one tolerance is read again within another.
    /// </summary>
    public class RoundingLevelG1z2Tests
    {
        private static readonly Tolerance Nought = new Tolerance(0, 0, 0, 0);

        private static readonly Tolerance Thousandth = new Tolerance(1E-3, 1E-5, Tolerance.Default.EqualAngleRad, 1E-3);

        /// <summary>No turn.</summary>
        private static readonly double[] Upright = { 1, 0, 0, 0, 1, 0, 0, 0, 1 };

        /// <summary>A turn about space, rows of a matrix.</summary>
        private static readonly double[] Turned =
        {
            0.17157015775976592, 0.891327671403195, -0.41964111232962714,
            -0.05739901463557945, 0.43427764053506596, 0.8989484323642598,
            0.9834983650648095, -0.13014573801033125, 0.12567041335410917,
        };

        /// <summary>Another turn about space, rows of a matrix.</summary>
        private static readonly double[] TurnedOtherwise =
        {
            0.6583361672990727, -0.7476039854815083, 0.08764571705526819,
            -0.1895669485306236, -0.051983355637495704, 0.9804907458826185,
            -0.728462670869795, -0.6621072508505178, -0.17594352935079569,
        };

        private static readonly double[] AtTheOrigin = { 0, 0, 0 };

        /// <summary>Where the cube turned otherwise stands, about ninety thousand out.</summary>
        private static readonly double[] FarOut = { -18335.749543428305, 87874.15688292784, -62088.56448628407 };

        private static GeoPoint3 P(double[] turn, double[] offset, double x, double y, double z)
            => new GeoPoint3(
                turn[0] * x + turn[1] * y + turn[2] * z + offset[0],
                turn[3] * x + turn[4] * y + turn[5] * z + offset[1],
                turn[6] * x + turn[7] * y + turn[8] * z + offset[2]);

        private static GeoFace3 F(params GeoPoint3[] corners) => new GeoFace3(new GeoPolygon3(corners));

        /// <summary>
        /// A face on corners given, wound to face along a direction: turned round where the corners run the other way.
        /// </summary>
        private static GeoFace3 Facing(double nx, double ny, double nz, params double[][] corners)
        {
            GeoPoint3[] points = corners.Select(c => new GeoPoint3(c[0], c[1], c[2])).ToArray();
            double x = 0, y = 0, z = 0;

            for (int i = 0; i < points.Length; i++)
            {
                GeoPoint3 p = points[i], q = points[(i + 1) % points.Length];
                x += (p.Y - q.Y) * (p.Z + q.Z);
                y += (p.Z - q.Z) * (p.X + q.X);
                z += (p.X - q.X) * (p.Y + q.Y);
            }

            if (x * nx + y * ny + z * nz < 0)
            {
                Array.Reverse(points);
            }

            return F(points);
        }

        private static double[] Q(double x, double y, double z) => new[] { x, y, z };

        /// <summary>
        /// A cube 0 to 10, turned and moved, cracked a gap wide along its top, its bottom and its side at x = 0, each crack
        /// leaning 0.02 across the face: its faces close within the gap and not within nought.
        /// </summary>
        private static GeoSolid3 CrackedCube(double[] turn, double[] offset, double gap)
        {
            const double skew = 0.01;
            GeoPoint3 C(double x, double y, double z) => P(turn, offset, x, y, z);
            GeoPoint3 a = C(5 - skew, 0, 10), a2 = C(5 - skew + gap, 0, 10), b = C(5 + skew, 10, 10);
            GeoPoint3 c = C(0, 5 - skew, 0), c2 = C(0, 5 - skew + gap, 0), d = C(10, 5 + skew, 0);
            GeoPoint3 e = C(0, 0, 5 - skew), e2 = C(0, 0, 5 - skew + gap), f = C(0, 10, 5 + skew);

            return new GeoSolid3(
                F(C(0, 0, 10), a, b, C(0, 10, 10)),
                F(a2, C(10, 0, 10), C(10, 10, 10), b),
                F(C(0, 0, 0), c, d, C(10, 0, 0)),
                F(c2, C(0, 10, 0), C(10, 10, 0), d),
                F(e, f, C(0, 10, 0), c2, c, C(0, 0, 0)),
                F(C(0, 0, 10), C(0, 10, 10), f, e2),
                F(C(10, 0, 0), d, C(10, 10, 0), C(10, 10, 10), C(10, 0, 10)),
                F(C(0, 0, 0), C(10, 0, 0), C(10, 0, 10), a2, a, C(0, 0, 10), e2, e),
                F(f, C(0, 10, 10), b, C(10, 10, 10), C(10, 10, 0), C(0, 10, 0)));
        }

        /// <summary>A body of six four-sided faces on eight corners, each face given by the indices of its corners.</summary>
        private static GeoSolid3 Hexahedron(double[][] corners, int[][] faces)
            => new GeoSolid3(faces.Select(face => F(face.Select(i => new GeoPoint3(corners[i][0], corners[i][1], corners[i][2])).ToArray())));

        /// <summary>The faces of the slabs below, as indices of their corners.</summary>
        private static readonly int[][] SlabFaces =
        {
            new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 5, 2, 1, 6 }, new[] { 7, 0, 3, 4 }, new[] { 7, 6, 1, 0 }, new[] { 3, 2, 5, 4 },
        };

        /// <summary>
        /// The U 30 by 10 by 10 less its notch from x = nl to nr, above z = nf, cracked a gap wide across its bottom along
        /// x = xa and xc and up its ends along z = za, each crack leaning s either way across the face.
        /// </summary>
        private static GeoSolid3 CrackedU(double nl, double nr, double nf, double xa0, double xc0, double za0, double s, double gap)
        {
            double xa = xa0 - s, xb = xa0 + s, xc = xc0 - s, xd = xc0 + s, za = za0 - s, zb = za0 + s;

            return new GeoSolid3(new List<GeoFace3>
            {
                Facing(0, 0, -1, Q(0, 0, 0), Q(xa, 0, 0), Q(xb, 10, 0), Q(0, 10, 0)),
                Facing(0, 0, -1, Q(xa + gap, 0, 0), Q(xc, 0, 0), Q(xd, 10, 0), Q(xb, 10, 0)),
                Facing(0, 0, -1, Q(xc + gap, 0, 0), Q(30, 0, 0), Q(30, 10, 0), Q(xd, 10, 0)),
                Facing(-1, 0, 0, Q(0, 0, 0), Q(0, 10, 0), Q(0, 10, zb), Q(0, 0, za)),
                Facing(-1, 0, 0, Q(0, 0, za + gap), Q(0, 10, zb), Q(0, 10, 10), Q(0, 0, 10)),
                Facing(1, 0, 0, Q(30, 0, 0), Q(30, 10, 0), Q(30, 10, zb), Q(30, 0, za)),
                Facing(1, 0, 0, Q(30, 0, za + gap), Q(30, 10, zb), Q(30, 10, 10), Q(30, 0, 10)),
                Facing(0, 0, 1, Q(0, 0, 10), Q(nl, 0, 10), Q(nl, 10, 10), Q(0, 10, 10)),
                Facing(0, 0, 1, Q(nr, 0, 10), Q(30, 0, 10), Q(30, 10, 10), Q(nr, 10, 10)),
                Facing(1, 0, 0, Q(nl, 0, nf), Q(nl, 0, 10), Q(nl, 10, 10), Q(nl, 10, nf)),
                Facing(-1, 0, 0, Q(nr, 0, nf), Q(nr, 10, nf), Q(nr, 10, 10), Q(nr, 0, 10)),
                Facing(0, 0, 1, Q(nl, 0, nf), Q(nl, 10, nf), Q(nr, 10, nf), Q(nr, 0, nf)),
                Facing(0, -1, 0, Q(0, 0, 0), Q(xa, 0, 0), Q(xa + gap, 0, 0), Q(xc, 0, 0), Q(xc + gap, 0, 0), Q(30, 0, 0), Q(30, 0, za), Q(30, 0, za + gap), Q(30, 0, 10), Q(nr, 0, 10), Q(nr, 0, nf), Q(nl, 0, nf), Q(nl, 0, 10), Q(0, 0, 10), Q(0, 0, za + gap), Q(0, 0, za)),
                Facing(0, 1, 0, Q(0, 10, 0), Q(xb, 10, 0), Q(xd, 10, 0), Q(30, 10, 0), Q(30, 10, zb), Q(30, 10, 10), Q(nr, 10, 10), Q(nr, 10, nf), Q(nl, 10, nf), Q(nl, 10, 10), Q(0, 10, 10), Q(0, 10, zb)),
            });
        }

        /// <summary>
        /// A box from x0, y0, z0 to x1, y1, z1 whose ends x = x0 and x1 are cracked a gap wide across y along z = q for each
        /// height given, each crack leaning s either way across the end.
        /// </summary>
        private static GeoSolid3 BoxCrackedAtItsEnds(double x0, double y0, double z0, double x1, double y1, double z1, double[] heights, double s, double gap)
        {
            var faces = new List<GeoFace3> { Facing(0, 0, -1, Q(x0, y0, z0), Q(x1, y0, z0), Q(x1, y1, z0), Q(x0, y1, z0)) };

            foreach (double x in new[] { x0, x1 })
            {
                for (int k = 0; k <= heights.Length; k++)
                {
                    double a0 = k == 0 ? z0 : heights[k - 1] - s + gap, a1 = k == heights.Length ? z1 : heights[k] - s;
                    double b0 = k == 0 ? z0 : heights[k - 1] + s, b1 = k == heights.Length ? z1 : heights[k] + s;
                    faces.Add(Facing(x == x0 ? -1 : 1, 0, 0, Q(x, y0, a0), Q(x, y0, a1), Q(x, y1, b1), Q(x, y1, b0)));
                }
            }

            faces.Add(Facing(0, 0, 1, Q(x0, y0, z1), Q(x1, y0, z1), Q(x1, y1, z1), Q(x0, y1, z1)));

            var front = new List<double[]> { Q(x0, y0, z0), Q(x1, y0, z0) };
            front.AddRange(heights.SelectMany(q => new[] { Q(x1, y0, q - s), Q(x1, y0, q - s + gap) }));
            front.Add(Q(x1, y0, z1));
            front.Add(Q(x0, y0, z1));
            front.AddRange(heights.Reverse().SelectMany(q => new[] { Q(x0, y0, q - s + gap), Q(x0, y0, q - s) }));
            faces.Add(Facing(0, -1, 0, front.ToArray()));

            var back = new List<double[]> { Q(x0, y1, z0), Q(x1, y1, z0) };
            back.AddRange(heights.Select(q => Q(x1, y1, q + s)));
            back.Add(Q(x1, y1, z1));
            back.Add(Q(x0, y1, z1));
            back.AddRange(heights.Reverse().Select(q => Q(x0, y1, q + s)));
            faces.Add(Facing(0, 1, 0, back.ToArray()));

            return new GeoSolid3(faces);
        }

        [Fact]
        public void ACrackedUAndABoxCrackedAtItsEndsThroughItsNotch_AtNought_AreNotSaidToShareTooLittle()
        {
            // The U's notch runs from x = 11.646 to 20.101 above z = 4.751; the box from 11.607 to 18.768, -1 to 7.447 and
            // 4.142 to 15.200, takes in 0.61 of the U's floor and 0.04 of its wall, 34.02 they share. Each is cracked
            // 1.457E-5 wide, the U across its floor under the box's ends and up its own ends, the box across its ends at
            // the notch's floor and the U's top. Neither closes within the rounding, and nothing they gave was checked: their
            // common part came out 3.66, most of the floor missing, and was trusted. A corner of the box lies inside the U,
            // outside that common part.
            const double nl = 11.645725875927939, nr = 20.100847234065107, nf = 4.751073261141346;
            const double x0 = 11.606520162106733, y0 = -1, z0 = 4.14200430914853, x1 = 18.767925557712058, y1 = 7.447313225570746, z1 = 15.200404556095789;
            const double s = 0.0075987102026871, gap = 1.4570680549343777E-05;
            GeoSolid3 u = CrackedU(nl, nr, nf, x0, x1, z0, s, gap);
            GeoSolid3 box = BoxCrackedAtItsEnds(x0, y0, z0, x1, y1, z1, new[] { nf, 10.0 }, s, gap);

            Assert.False(Boolean3.TryIntersect(u, box, out GeoSolid3 common, Nought, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
            Assert.Null(common);
        }

        [Fact]
        public void ACrackedCubeTurnedJoinedToASlabThousandsLongThroughIt_AtNought_HoldsWhatTheTwoHold()
        {
            // The slab, 59 169 045.91 between planes a millionth of a radian off one another, runs a million long through the
            // cube cracked 0.0001 wide and shares 249.85 with it; the two hold 59 169 796.056. Its corners, where planes so
            // near one another meet, stand off its faces' planes, and what it holds moves with how its faces are taken: read
            // from a corner of its own, 0.57 short. The union, 59 169 796.053, was turned down for missing the two less their
            // common part by that.
            double[][] corners =
            {
                Q(-272276.40303743235, 583300.1903135272, 81545.9976691032),
                Q(3.2141922063817634, 6.3740884002007805, 4.888882488957654),
                Q(392300.3620810702, -840366.2580350575, -117476.7955481501),
                Q(120020.74485143147, -257072.4418099304, -35935.68676153586),
                Q(-139616.73899241042, 299116.58331774233, 41810.87486328465),
                Q(132662.87823722835, -284177.2329073848, -39730.23392332961),
                Q(-259634.2696516355, 556195.3992160729, 77751.45050730945),
                Q(-531913.8868812743, 1139489.2154412, 159292.5592939237),
            };
            GeoSolid3 cube = CrackedCube(Turned, AtTheOrigin, 0.0001);
            GeoSolid3 slab = Hexahedron(corners, SlabFaces);

            Assert.True(Boolean3.TryUnion(cube, slab, out GeoSolid3 union, Nought, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(59_169_796.056, union.GetVolume(Nought), 0.01);
        }

        [Fact]
        public void ACrackedCubeFarOutJoinedToASlabHolding36TooMuch_AtNought_IsStillNotWorkedOut()
        {
            // A guard on what the slab's faces off flat allow. The cube cracked 0.001 wide, turned and about ninety thousand
            // out, and a slab of 15 444 434.22 a million and a half long through it, sharing 104.50: the union within the
            // rounding held 36 more than the two, 15 445 329.72, and stays not worked out.
            double[][] corners =
            {
                Q(-40426.98534089797, -159236.10922119868, -17754.81343299229),
                Q(-18335.758141428483, 87877.85664287486, -62096.39453851859),
                Q(111203.84673771827, 1537034.621818314, -322139.3953707501),
                Q(89112.6195382488, 1289920.6559542406, -277797.8142652238),
                Q(42332.959611992075, 766564.9937794626, -183887.8870862122),
                Q(64424.186811461535, 1013678.959643536, -228229.46819173847),
                Q(-65115.418067685205, -435477.8055319033, 31813.53264049301),
                Q(-87206.64526715467, -682591.7713959767, 76155.11374601928),
            };
            int[][] faces =
            {
                new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 5, 2, 1, 6 }, new[] { 7, 0, 3, 4 }, new[] { 6, 1, 0, 7 }, new[] { 4, 3, 2, 5 },
            };
            GeoSolid3 cube = CrackedCube(TurnedOtherwise, FarOut, 0.001);
            GeoSolid3 slab = Hexahedron(corners, faces);

            Assert.False(Boolean3.TryUnion(cube, slab, out GeoSolid3 union, Nought, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
            Assert.Null(union);
        }

        [Fact]
        public void ACrackedCubeReadClosedWithinAThousandthAndOpenAtNought_IsReadSoEachTime()
        {
            // The cube cracked 0.0001 wide closes within a thousandth and not within nought; what was read within one is
            // not taken for the other, asked in turn.
            GeoSolid3 cube = CrackedCube(Upright, AtTheOrigin, 0.0001);

            Assert.False(Shells3.Closes(cube, Nought));
            Assert.True(Shells3.Closes(cube, Thousandth));
            Assert.False(Shells3.Closes(cube, Nought));
            Assert.True(Shells3.Closes(cube, Thousandth));
        }
    }
}

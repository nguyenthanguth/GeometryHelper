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
    /// What a boolean asked at nought of a body that does not close within the rounding of its coordinates gives where what
    /// it gives within the rounding does not hold up, after G1z2. Where the tolerance alone gave something too, the result
    /// within the rounding was kept, and came out wrong: a cracked cube less a slab thousands long through it came back whole.
    /// A difference or a union of two such bodies was not checked by the corners of their common part, a difference or a
    /// common part said to be nothing was not checked at all, and a union was let through for missing what it should hold
    /// by more than the cracks can move it.
    /// </summary>
    public class RoundingLevelG1z3Tests
    {
        private static readonly Tolerance Nought = new Tolerance(0, 0, 0, 0);

        /// <summary>A turn about space, rows of a matrix.</summary>
        private static readonly double[] Turned =
        {
            0.17157015775976592, 0.891327671403195, -0.41964111232962714,
            -0.05739901463557945, 0.43427764053506596, 0.8989484323642598,
            0.9834983650648095, -0.13014573801033125, 0.12567041335410917,
        };

        private static readonly double[] AtTheOrigin = { 0, 0, 0 };

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

        /// <summary>
        /// The slab of 59 169 045.91 a million long through the cube cracked and turned as <see cref="Turned"/>, sharing
        /// 249.85 with it, between planes a millionth of a radian off one another.
        /// </summary>
        private static GeoSolid3 SlabThousandsLong()
        {
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
            int[][] faces =
            {
                new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 5, 2, 1, 6 }, new[] { 7, 0, 3, 4 }, new[] { 7, 6, 1, 0 }, new[] { 3, 2, 5, 4 },
            };

            return Hexahedron(corners, faces);
        }

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
        /// A box from x0, y0, z0 to x1, y1, z1 cracked a gap wide across its bottom along x = p for each place given, and
        /// across its ends x = x0 and x1 along z = q for each height given, each crack leaning s either way across the face.
        /// </summary>
        private static GeoSolid3 BoxCracked(double x0, double y0, double z0, double x1, double y1, double z1, double[] places, double[] heights, double s, double gap)
        {
            var faces = new List<GeoFace3>();

            for (int k = 0; k <= places.Length; k++)
            {
                double a0 = k == 0 ? x0 : places[k - 1] - s + gap, a1 = k == places.Length ? x1 : places[k] - s;
                double b0 = k == 0 ? x0 : places[k - 1] + s, b1 = k == places.Length ? x1 : places[k] + s;
                faces.Add(Facing(0, 0, -1, Q(a0, y0, z0), Q(a1, y0, z0), Q(b1, y1, z0), Q(b0, y1, z0)));
            }

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

            var front = new List<double[]> { Q(x0, y0, z0) };
            front.AddRange(places.SelectMany(p => new[] { Q(p - s, y0, z0), Q(p - s + gap, y0, z0) }));
            front.Add(Q(x1, y0, z0));
            front.AddRange(heights.SelectMany(q => new[] { Q(x1, y0, q - s), Q(x1, y0, q - s + gap) }));
            front.Add(Q(x1, y0, z1));
            front.Add(Q(x0, y0, z1));
            front.AddRange(heights.Reverse().SelectMany(q => new[] { Q(x0, y0, q - s + gap), Q(x0, y0, q - s) }));
            faces.Add(Facing(0, -1, 0, front.ToArray()));

            var back = new List<double[]> { Q(x0, y1, z0) };
            back.AddRange(places.Select(p => Q(p + s, y1, z0)));
            back.Add(Q(x1, y1, z0));
            back.AddRange(heights.Select(q => Q(x1, y1, q + s)));
            back.Add(Q(x1, y1, z1));
            back.Add(Q(x0, y1, z1));
            back.AddRange(heights.Reverse().Select(q => Q(x0, y1, q + s)));
            faces.Add(Facing(0, 1, 0, back.ToArray()));

            return new GeoSolid3(faces);
        }

        [Fact]
        public void ACrackedCubeTurnedLessASlabThousandsLongThroughIt_AtNought_IsNotWorkedOutWhole()
        {
            // The cube cracked 0.0001 wide shares 249.85 with the slab, and 750.15 of it is left. Within the rounding it came
            // back whole, 999.998, which does not hold up against their common part; the tolerance alone gave something
            // too, as wrong, and the whole cube was kept and trusted.
            GeoSolid3 cube = CrackedCube(Turned, AtTheOrigin, 0.0001);
            GeoSolid3 slab = SlabThousandsLong();

            Assert.False(Boolean3.TrySubtract(cube, slab, out GeoSolid3 left, Nought, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
            Assert.Null(left);
        }

        [Fact]
        public void ACrackedCubeTurnedAndASlabThousandsLongThroughItsCorner_AtNought_AreNotSaidToShareNothing()
        {
            // The slab of 106 403.36, some 24 000 long, cuts 15.23 off a corner of the cube cracked 0.0001 wide.
            // Within the rounding their common part was said to be nothing, and trusted, though the slab's edges run through
            // the cube; the cube less the slab came back whole, 999.998, where 984.77 is left, and the two joined held
            // 107 403.36, the two whole, where they hold 107 388.12.
            double[] turn =
            {
                0.1029074193676296, -0.25418460907362284, -0.9616653511223043,
                -0.023517807616662917, -0.967149631598831, 0.2531175671958619,
                -0.9944128799266434, -0.0034314149174337755, -0.10550473746549938,
            };
            double[][] corners =
            {
                Q(2888.2729649729795, -765.365990111807, 311.9613013462984),
                Q(-5.568376438297069, -3.685142055885719, -5.523331573137566),
                Q(-13397.610013979554, 3512.1014020569037, -1474.5550245824575),
                Q(-10503.768672568278, 2750.4205540009825, -1157.0703916630214),
                Q(-20655.686734023016, 5422.496226471147, -2274.8828765089656),
                Q(-7263.645096481757, 1906.7096823583563, -805.8511834996455),
                Q(-4369.80375507048, 1145.0288343024351, -488.3665505802096),
                Q(-17761.845392611736, 4660.8153784152255, -1957.3982435895296),
            };
            int[][] faces =
            {
                new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 6, 5, 1, 0 }, new[] { 7, 3, 2, 4 }, new[] { 2, 1, 5, 4 }, new[] { 6, 0, 3, 7 },
            };
            GeoSolid3 cube = CrackedCube(turn, AtTheOrigin, 0.0001);
            GeoSolid3 slab = Hexahedron(corners, faces);

            Assert.False(Boolean3.TryIntersect(cube, slab, out GeoSolid3 common, Nought, out BooleanOutcome shared));
            Assert.Equal(BooleanOutcome.NotWorkedOut, shared);
            Assert.Null(common);
            Assert.False(Boolean3.TrySubtract(cube, slab, out GeoSolid3 left, Nought, out BooleanOutcome leaves));
            Assert.Equal(BooleanOutcome.NotWorkedOut, leaves);
            Assert.Null(left);
            Assert.False(Boolean3.TryUnion(cube, slab, out GeoSolid3 union, Nought, out BooleanOutcome joined));
            Assert.Equal(BooleanOutcome.NotWorkedOut, joined);
            Assert.Null(union);
        }

        [Fact]
        public void ACrackedCubeFarOutLessASlabWhoseFacesStandOffFlat_AtNought_IsNotWorkedOutWhole()
        {
            // The cube cracked 0.0001 wide, turned and about a hundred thousand out, and a slab of 48 340 393.27 five million
            // long through it, sharing 27.28, 972.72 of the cube left. Within the rounding the cube came back whole, 999.998,
            // missing the cube less their common part by 27.28, and was let through for what the slab's faces off flat move
            // its own volume, up to 44, though the slab's volume does not enter a difference.
            double[] turn =
            {
                -0.03230224852053026, 0.9899423217432111, -0.13773439789021252,
                0.024512616872631343, -0.1369801938347551, -0.9902704469542899,
                -0.9991775099323312, -0.035364192605719436, -0.019841308595253793,
            };
            double[] offset = { -71484.7089589968, -86493.5727727104, -82713.69868084494 };
            double[][] corners =
            {
                Q(-372944.12322327273, -2253993.5218828525, -126147.70846565269),
                Q(-71480.60614826008, -86499.0934935062, -82718.97771772015),
                Q(22841.08961064597, 591646.5930408798, -69131.47959490676),
                Q(-278622.4274643666, -1575847.835348466, -112560.21034283927),
                Q(-358944.4957720064, -2153283.467544228, -124120.91655678308),
                Q(-453266.1915309125, -2831429.1540786144, -137708.41467959646),
                Q(-754729.7086059251, -4998923.58246796, -181137.145427529),
                Q(-660408.0128470191, -4320777.895933575, -167549.6473047156),
            };
            int[][] faces =
            {
                new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 4, 2, 1, 5 }, new[] { 6, 0, 3, 7 }, new[] { 6, 5, 1, 0 }, new[] { 3, 2, 4, 7 },
            };
            GeoSolid3 cube = CrackedCube(turn, offset, 0.0001);
            GeoSolid3 slab = Hexahedron(corners, faces);

            Assert.False(Boolean3.TrySubtract(cube, slab, out GeoSolid3 left, Nought, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
            Assert.Null(left);
        }

        [Fact]
        public void ACrackedCubeTurnedJoinedToABoxThroughItsCorner_AtNought_IsNotWorkedOutForMissingTheTwoByMoreThanItsCracksMove()
        {
            // A box 69.47, turned with the cube cracked 0.0001 wide, shares 59.05 with it; the two hold 1 010.419, and the
            // cracks move what the cube holds by about 0.01. The union within the rounding held 1 010.4075, 0.0115 short,
            // and missed the two less their common part by 0.0098, within the open area of the cube times the diagonal of
            // its box, 0.0133, though no reading of a volume moves by more than a third of that and the union and the common
            // part, which share the cube's cracks, by as much again.
            double[] turn =
            {
                -0.24971186140271995, -0.7422389570660716, 0.6218724281460464,
                0.27523474632594647, 0.561322251651784, 0.7804890545135554,
                -0.9283802134307264, 0.3660582746149062, 0.06412112675458237,
            };
            double[][] corners =
            {
                Q(-3.6738602491986407, 5.797478400782058, -2.6761606479305167),
                Q(-1.848163894802914, 8.088842283434403, -2.4879135058203756),
                Q(1.1393140734161096, 5.8295458076614075, -3.9612813748676396),
                Q(-0.6863822809796174, 3.5381819250090625, -4.149528516977781),
                Q(0.7817488376513597, 1.9199940914806084, 1.3086979108841348),
                Q(2.607445192047087, 4.211357974132953, 1.4969450529942758),
                Q(-0.3800327761719373, 6.470654449905949, 2.97031292204154),
                Q(-2.205729130567664, 4.179290567253604, 2.782065779931399),
            };
            int[][] faces =
            {
                new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 7, 6, 1, 0 }, new[] { 3, 2, 5, 4 }, new[] { 2, 1, 6, 5 }, new[] { 4, 7, 0, 3 },
            };
            GeoSolid3 cube = CrackedCube(turn, AtTheOrigin, 0.0001);
            GeoSolid3 box = Hexahedron(corners, faces);

            Assert.False(Boolean3.TryUnion(cube, box, out GeoSolid3 union, Nought, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
            Assert.Null(union);
            Assert.True(Boolean3.TrySubtract(cube, box, out GeoSolid3 left, Nought));
            Assert.Equal(940.946, left.GetVolume(Nought), 0.01);
        }

        [Fact]
        public void ACrackedUAndABoxCrackedAtItsEndsDippingIntoItsFloor_AtNought_AreNotTakenOneFromTheOtherWhole()
        {
            // The U's notch runs from x = 11.446 to 18.748 above z = 6.995; the box, from 13.492 to 18.311, 0.060 to 8.280
            // and 6.512 to 13.190, dips 0.48 into the notch's floor and shares 19.13 with the U. Each is cracked 1.69E-5
            // wide and neither closes within the rounding. The common part is not worked out within it, and each
            // difference came back as the body it is taken from, 2 780.58 and 264.48, where 2 761.44 and 245.35 are left,
            // kept since the tolerance alone gave something too.
            const double nl = 11.445811532179738, nr = 18.74830835533715, nf = 6.99522524187119;
            const double x0 = 13.491829871289353, y0 = 0.06022558550360873, z0 = 6.512132127402412;
            const double x1 = 18.310594096598493, y1 = 8.279765735510628, z1 = 13.189656694973658;
            const double s = 0.026291002797838102, gap = 1.6883564993927295E-05;
            GeoSolid3 u = CrackedU(nl, nr, nf, x0, x1, z0, s, gap);
            GeoSolid3 box = BoxCracked(x0, y0, z0, x1, y1, z1, new double[0], new[] { nf, 10.0 }, s, gap);

            Assert.False(Boolean3.TrySubtract(u, box, out GeoSolid3 uLeft, Nought, out BooleanOutcome uOutcome));
            Assert.Equal(BooleanOutcome.NotWorkedOut, uOutcome);
            Assert.Null(uLeft);
            Assert.False(Boolean3.TrySubtract(box, u, out GeoSolid3 boxLeft, Nought, out BooleanOutcome boxOutcome));
            Assert.Equal(BooleanOutcome.NotWorkedOut, boxOutcome);
            Assert.Null(boxLeft);
        }

        [Fact]
        public void ACrackedUAndABoxCrackedThroughItsNotch_AtNought_AreNotTakenOneFromTheOtherOrJoinedWithACommonPartOutsideThem()
        {
            // The U's notch runs from x = 9.232 to 20.489 above z = 5.601; the box, from 8.629 to 20.357, -1 to 11 and 5.561
            // to 14.973, takes in 0.04 of the notch's floor and 0.6 of the U's wall left of it, 31.18 they share. Each is
            // cracked 2.7E-5 wide, the box across its bottom under the notch's left wall and across its ends at the U's top.
            // Neither closes within the rounding. Each difference and the union came out 25.6 more than they hold, 2 499.21
            // where 2 473.62 is left, 1 318.96 where 1 293.37 is, and 3 823.76 where the two hold 3 798.17, and added up
            // with the common part the volumes were checked by, which has a corner outside one of the two.
            const double nl = 9.231833620570523, nr = 20.488662402373116, nf = 5.600881993119085;
            const double x0 = 8.629009608332538, y0 = -1, z0 = 5.5611686935933164, x1 = 20.357151533317356, y1 = 11, z1 = 14.972641007962004;
            const double s = 0.007385488371459736, gap = 2.733860220431562E-05;
            GeoSolid3 u = CrackedU(nl, nr, nf, x0, x1, z0, s, gap);
            GeoSolid3 box = BoxCracked(x0, y0, z0, x1, y1, z1, new[] { nl }, new[] { 10.0 }, s, gap);

            Assert.False(Boolean3.TrySubtract(u, box, out GeoSolid3 uLeft, Nought, out BooleanOutcome uOutcome));
            Assert.Equal(BooleanOutcome.NotWorkedOut, uOutcome);
            Assert.Null(uLeft);
            Assert.False(Boolean3.TrySubtract(box, u, out GeoSolid3 boxLeft, Nought, out BooleanOutcome boxOutcome));
            Assert.Equal(BooleanOutcome.NotWorkedOut, boxOutcome);
            Assert.Null(boxLeft);
            Assert.False(Boolean3.TryUnion(u, box, out GeoSolid3 union, Nought, out BooleanOutcome unionOutcome));
            Assert.Equal(BooleanOutcome.NotWorkedOut, unionOutcome);
            Assert.Null(union);
        }

        [Fact]
        public void ACrackedULessABoxCrackedAtItsEndsInItsNotch_AtNought_IsNotSaidToLeaveNothing()
        {
            // The U's notch runs from x = 8.066 to 18.311 above z = 4.618; the box, from 10.164 to 16.565, 1.094 to 11 and
            // 3.807 to 13.155, dips 0.81 into the notch's floor and shares 46.24 with the U, and the U less it leaves
            // 2 402.34. Each is cracked 1.2E-5 wide and neither closes within the rounding. The difference was said to be
            // nothing and trusted; the common part it should add up with is not worked out within the rounding.
            const double nl = 8.065987931595178, nr = 18.31132494672729, nf = 4.617788979605674;
            const double x0 = 10.164214298531512, y0 = 1.0936564547445888, z0 = 3.806686961328, x1 = 16.56466194608466, y1 = 11, z1 = 13.155270512288096;
            const double s = 0.015683859741832045, gap = 1.1772979896621901E-05;
            GeoSolid3 u = CrackedU(nl, nr, nf, x0, x1, z0, s, gap);
            GeoSolid3 box = BoxCracked(x0, y0, z0, x1, y1, z1, new double[0], new[] { nf, 10.0 }, s, gap);

            Assert.False(Boolean3.TrySubtract(u, box, out GeoSolid3 left, Nought, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
            Assert.Null(left);
        }
    }
}

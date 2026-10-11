using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Common;
using Xunit;
using static GeometryHelper.UnitTest.Solid.MeetingInSpaceOracle;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Whether and where flat and straight shapes in space meet, asked from either side: two flat regions, a plane
    /// against a bounded shape and a ray, two rays, a ray against a flat region, a body against a flat region, the
    /// boxes through their body, and an edge against the chains.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cases are a table. Each row names whether the two meet and the points where they cross, worked by hand. A
    /// flat region is read filled, a face by its material. Two regions in planes that are not one cross where an edge
    /// of either reaches the other: the edges of the region asked first, ring by ring, each in order along it, and
    /// then the other's, a place found twice named once. Two regions in one plane, or within the tolerance of one,
    /// are answered by the plane half, in its order. A plane meets a shape whose heights over it change sign or reach
    /// nought, and crosses its edges edge by edge. A body crosses a region where the region's edges cross the surface
    /// of its material and where the body's own edges cross the region.
    /// </para>
    /// <para>
    /// The fixtures lie within 60 of the origin, so a point is held to its worked place within 1E-9, but where the
    /// shapes are tilted off one plane by under a thousandth of a degree, and the place is that of the plane half,
    /// within the point tolerance. Each row is also held to <see cref="MeetingInSpaceOracle"/>, which works by
    /// arithmetic of its own: dense samples for whether they meet, a fine subdivision for where they cross, the plane
    /// half's booleans for regions in one plane, and the booleans of a thin slab for a body.
    /// </para>
    /// <para>
    /// Where the spec leaves the order between the two sides of a pair of two kinds open, a row takes either: the
    /// asked shape's edges first, or the argument's. A pair of one kind has the asked shape's edges first.
    /// </para>
    /// </remarks>
    public class MeetingInSpaceTests
    {
        private static readonly Tolerance Within = Tolerance.Default;

        /// <summary>How near a point must be to its worked place: 1E-9, well past rounding at a size of 100.</summary>
        private const double Near = 1E-9;

        /// <summary>How near a point found by the plane half must be, for shapes a hair off one plane.</summary>
        private const double NearInBand = 1E-3;

        /// <summary>How far a point found by the oracle may be from the library's: the subdivision's halving and rounding.</summary>
        private const double NearOracle = 1E-6;

        /// <summary>Half the thickness of the slab a region is made into for the booleans.</summary>
        private const double SlabHalf = 0.5;

        private static readonly GeoVector3 Up = GeoVector3.ZAxis;

        private static readonly GeoVector3 AlongY = GeoVector3.YAxis;

        private static readonly Lazy<List<Row>> Table = new Lazy<List<Row>>(Build);

        public static TheoryData<string> WhetherRows() => Names(r => r.Collide != null && !r.BodyBreaks);

        public static TheoryData<string> WhereRows() => Names(r => r.Cross != null && r.Points != null);

        public static TheoryData<string> TriedRows() => Names(r => r.Try != null);

        public static TheoryData<string> SampledRows() => Names(r => r.SampledMeet != null && (r.Collide != null || r.Cross != null));

        public static TheoryData<string> SubdividedRows() => Names(r => r.Subdivided != null && r.Cross != null);

        public static TheoryData<string> PlaneHalfRows() => Names(r => r.PlaneHalfMeets != null);

        public static TheoryData<string> SlabRows() => Names(r => r.SlabMeets != null && r.Collide != null);

        public static TheoryData<string> BoxRows() => Names(r => r.Body != null);

        public static TheoryData<string> EdgeRows() => Names(r => r.Kind != null);

        [Theory]
        [MemberData(nameof(WhetherRows))]
        public void WhetherTheyMeetIsAsWorkedByHandFromEitherSide(string name)
        {
            Row row = Find(name);

            Assert.Equal(row.Collides, row.Collide(Within));
            Assert.Equal(row.Collides, row.CollideBack(Within));

            // The form without a tolerance is the one under the global tolerance, read and never set.
            Assert.Equal(row.Collide(Tolerance.Global), row.PlainCollide());
            Assert.Equal(row.CollideBack(Tolerance.Global), row.PlainCollideBack());
        }

        [Theory]
        [MemberData(nameof(WhereRows))]
        public void WhereTheyCrossIsAsWorkedByHandFromEitherSide(string name)
        {
            Row row = Find(name);
            GeoPoint3[] asked = row.Cross(Within);
            GeoPoint3[] back = row.CrossBack(Within);
            double near = row.Band ? NearInBand : Near;

            switch (row.Order)
            {
                case Order.AskedFirst:
                    // One kind: the asked region's edges first, then the argument's; asked the other way, the other way.
                    InOrder(row.Points, asked, near);
                    InOrder(row.PointsArgumentFirst, back, near);
                    break;

                case Order.EitherFirst:
                    // Two kinds: either region's edges may come first, and each side gives one of the two orders.
                    InOneOf(new[] { row.Points, row.PointsArgumentFirst }, asked, near);
                    InOneOf(new[] { row.Points, row.PointsArgumentFirst }, back, near);
                    break;

                case Order.Same:
                    // One shape has edges and the other none: edge by edge along it, from either side.
                    InOrder(row.Points, asked, near);
                    InOrder(row.Points, back, near);
                    break;

                case Order.PlaneHalf:
                    // In one plane: the plane half's points, in its order, laid out in the frame of either region.
                    AsSet(row.Points, asked, near);
                    AsSet(row.Points, back, near);
                    InOneOf(row.PlaneHalfOrders(), asked, near);
                    InOneOf(row.PlaneHalfOrdersBack(), back, near);
                    break;

                default:
                    AsSet(row.Points, asked, near);
                    AsSet(row.Points, back, near);
                    break;
            }

            MirrorTable.Same(row.Cross(Tolerance.Global), row.PlainCross());
            MirrorTable.Same(row.CrossBack(Tolerance.Global), row.PlainCrossBack());
        }

        [Theory]
        [MemberData(nameof(TriedRows))]
        public void TryIntersectWithSaysWhetherThereAreAnyAndGivesThem(string name)
        {
            Row row = Find(name);

            (bool hit, GeoPoint3[] points) = row.Try(Within);
            Assert.Equal(row.Cross(Within).Length > 0, hit);
            MirrorTable.Same(row.Cross(Within), points);

            (bool hitBack, GeoPoint3[] pointsBack) = row.TryBack(Within);
            Assert.Equal(row.CrossBack(Within).Length > 0, hitBack);
            MirrorTable.Same(row.CrossBack(Within), pointsBack);

            MirrorTable.Same(row.Try(Tolerance.Global), row.PlainTry());
            MirrorTable.Same(row.TryBack(Tolerance.Global), row.PlainTryBack());
        }

        [Theory]
        [MemberData(nameof(SampledRows))]
        public void DenseSamplesSayWhetherTheyMeet(string name)
        {
            Row row = Find(name);
            bool sampled = row.SampledMeet();

            // The fixture first: the hand and the samples agree.
            Assert.Equal(row.Collides, sampled);

            if (row.Collide != null)
            {
                Assert.Equal(sampled, row.Collide(Within));
            }
            else
            {
                // Where only the crossings are new: shapes the samples find apart cross nowhere, and every place
                // named lies on both.
                GeoPoint3[] crossings = row.Cross(Within);

                Assert.True(sampled || crossings.Length == 0, "apart by the samples, yet crossing at " + Show(crossings));
                Assert.All(crossings, p => Assert.True(row.OnBoth(p), "off one of the two: " + Show(new[] { p })));
            }
        }

        [Theory]
        [MemberData(nameof(SubdividedRows))]
        public void AFineSubdivisionFindsTheSamePlaces(string name)
        {
            Row row = Find(name);
            GeoPoint3[] oracle = row.Subdivided();

            if (row.Points != null)
            {
                AsSet(row.Points, oracle, NearOracle);
            }

            AsSet(oracle, row.Cross(Within), NearOracle);
            AsSet(oracle, row.CrossBack(Within), NearOracle);
        }

        [Theory]
        [MemberData(nameof(PlaneHalfRows))]
        public void ThePlaneHalfsBooleansSayWhetherAndWhereRegionsInOnePlaneMeet(string name)
        {
            Row row = Find(name);
            (bool meets, GeoPoint3[] onBothRims) = row.PlaneHalfMeets();

            Assert.Equal(row.Collides, meets);

            if (row.Collide != null)
            {
                Assert.Equal(meets, row.Collide(Within));
            }

            if (onBothRims != null)
            {
                AsSet(onBothRims, row.Cross(Within), NearInBand);
            }
        }

        [Theory]
        [MemberData(nameof(SlabRows))]
        public void AThinSlabCutFromTheBodySaysWhetherTheyMeet(string name)
        {
            Row row = Find(name);
            bool meets = row.SlabMeets();

            Assert.Equal(row.Collides, meets);
            Assert.Equal(meets, row.Collide(Within));
            Assert.Equal(meets, row.CollideBack(Within));
        }

        [Theory]
        [MemberData(nameof(BoxRows))]
        public void ABoxAnswersExactlyAsItsBodyDoes(string name)
        {
            Row row = Find(name);
            (bool? collides, bool? collidesBack, GeoPoint3[] crossings, GeoPoint3[] crossingsBack) = row.Body(Within);

            // The box asked, and the shape asked about the box, each as the same question with the box's body.
            if (collides.HasValue)
            {
                if (!row.BodyBreaks)
                {
                    Assert.Equal(row.Collides, collides.Value);
                }

                Assert.Equal(collides.Value, row.Collide(Within));
                Assert.Equal(collidesBack.Value, row.CollideBack(Within));
            }

            MirrorTable.Same(crossings, row.Cross(Within));
            MirrorTable.Same(crossingsBack, row.CrossBack(Within));
        }

        [Theory]
        [MemberData(nameof(EdgeRows))]
        public void AnEdgeIsAskedAsTheSegmentOrTheArcItIs(string name)
        {
            Row row = Find(name);
            (bool collides, GeoPoint3[] crossings) = row.Kind(Within);

            Assert.Equal(collides, row.Collide(Within));
            Assert.Equal(collides, row.CollideBack(Within));
            InOrder(crossings, row.Cross(Within), Near);
            InOrder(crossings, row.CrossBack(Within), Near);
        }

        #region The table

        private static List<Row> Build()
        {
            var rows = new List<Row>();

            AddFlatRegions(rows);
            AddPlanes(rows);
            AddRays(rows);
            AddBodies(rows);
            AddBoxes(rows);
            AddEdges(rows);

            return rows;
        }

        #region Two flat regions

        private enum K { Polygon, Triangle, Face }

        /// <summary>The square 40 by 40 in the plane z = 0, from the origin.</summary>
        private static GeoPolygon3 BaseSquare() => Square(0, 0, 40, 40);

        /// <summary>Half of it: the right angle at (40, 0), the long side along y = x.</summary>
        private static GeoTriangle3 BaseTriangle() => Tri(Pt(0, 0, 0), Pt(40, 0, 0), Pt(40, 40, 0));

        /// <summary>The square with a hole 10 by 10 in its middle.</summary>
        private static GeoFace3 BasePlate() => Holed(Square(0, 0, 40, 40), Square(15, 15, 25, 25));

        /// <summary>
        /// A probe of each kind: the rectangle as given, a triangle on its bottom side, and the rectangle with a small
        /// hole where it cannot reach the base.
        /// </summary>
        private sealed class Probe
        {
            internal GeoPolygon3 Polygon;
            internal GeoTriangle3 Triangle;
            internal GeoFace3 Face;
        }

        private sealed class FlatCase
        {
            internal string Name;
            internal Func<K, object> Asked;
            internal Func<K, object> Argument;
            internal Func<K, K, (bool Collides, GeoPoint3[] AskedFirst, GeoPoint3[] ArgumentFirst)> Expected;
            internal bool Coplanar;
            internal bool Band;
            internal bool Huge;
            internal (K Asked, K Argument)[] Kinds = AllKinds;
        }

        private static readonly (K, K)[] AllKinds =
        {
            (K.Polygon, K.Polygon), (K.Polygon, K.Triangle), (K.Polygon, K.Face),
            (K.Triangle, K.Triangle), (K.Triangle, K.Face), (K.Face, K.Face)
        };

        private static object Base(K kind) => kind == K.Polygon ? BaseSquare() : kind == K.Triangle ? (object)BaseTriangle() : BasePlate();

        private static Func<K, object> From(Probe p) => k => k == K.Polygon ? p.Polygon : k == K.Triangle ? (object)p.Triangle : p.Face;

        private static (bool, GeoPoint3[], GeoPoint3[]) Meet(params GeoPoint3[] points) => (true, points, points);

        private static (bool, GeoPoint3[], GeoPoint3[]) Apart() => (false, new GeoPoint3[0], new GeoPoint3[0]);

        private static void AddFlatRegions(List<Row> rows)
        {
            var cases = new List<FlatCase>();

            // Upright in the plane x = 20, piercing the base between y = 5 and y = 10. The rectangle's sides at y = 10
            // and y = 5 cross z = 0 there; the triangle's two slanted sides, rising from (y, z) = (10, -10) and
            // (5, -10) to (7.5, 10), cross it half way up, at y = 8.75 and 6.25. The base's edges meet the plane
            // x = 20 at y = 0, 20 or 40, and at the hole's rim at y = 15 and 25, all off the probe.
            Probe crossing = new Probe
            {
                Polygon = Upright(20, 5, -10, 10, 10),
                Triangle = Tri(Pt(20, 5, -10), Pt(20, 10, -10), Pt(20, 7.5, 10)),
                Face = Holed(Upright(20, 5, -10, 10, 10), Upright(20, 6, 4, 9, 8))
            };
            cases.Add(new FlatCase
            {
                Name = "crossing through the middle",
                Asked = Base,
                Argument = From(crossing),
                Expected = (a, b) => b == K.Triangle ? Meet(Pt(20, 8.75, 0), Pt(20, 6.25, 0)) : Meet(Pt(20, 10, 0), Pt(20, 5, 0))
            });

            // The same lifted 5 clear of the base.
            Probe above = new Probe
            {
                Polygon = Upright(20, 5, 5, 10, 25),
                Triangle = Tri(Pt(20, 5, 5), Pt(20, 10, 5), Pt(20, 7.5, 25)),
                Face = Holed(Upright(20, 5, 5, 10, 25), Upright(20, 6, 15, 9, 20))
            };
            cases.Add(new FlatCase { Name = "apart, 5 above", Asked = Base, Argument = From(above), Expected = (a, b) => Apart() });

            // A diamond and a triangle standing on one corner, (20, 8, 0), inside the base: the two sides from it name
            // the one place.
            Probe vertex = new Probe
            {
                Polygon = Polygon(Pt(20, 8, 0), Pt(20, 12, 4), Pt(20, 8, 8), Pt(20, 4, 4)),
                Triangle = Tri(Pt(20, 8, 0), Pt(20, 12, 6), Pt(20, 4, 6)),
                Face = Holed(Polygon(Pt(20, 8, 0), Pt(20, 16, 8), Pt(20, 8, 16), Pt(20, 0, 8)), Upright(20, 6, 6, 10, 10))
            };
            cases.Add(new FlatCase { Name = "touching at a corner", Asked = Base, Argument = From(vertex), Expected = (a, b) => Meet(Pt(20, 8, 0)) });

            // Standing on the base along its bottom side, from y = 5 to y = 10: that side lies in the base and crosses
            // nothing, and the two sides rising from its ends name them.
            Probe standing = new Probe
            {
                Polygon = Upright(20, 5, 0, 10, 10),
                Triangle = Tri(Pt(20, 5, 0), Pt(20, 10, 0), Pt(20, 7.5, 10)),
                Face = Holed(Upright(20, 5, 0, 10, 10), Upright(20, 6, 4, 9, 8))
            };
            cases.Add(new FlatCase
            {
                Name = "touching along an edge inside",
                Asked = Base,
                Argument = From(standing),
                Expected = (a, b) => Meet(Pt(20, 10, 0), Pt(20, 5, 0))
            });

            // Standing on the base along a bottom side from y = -10 to 50, past both rims: now the base's edges end the
            // shared stretch. The square's sides at y = 0 and 40 cross x = 20; the triangle's at y = 0 and its long
            // side y = x at y = 20; the plate's outline, and then its hole's sides at y = 15 and 25, ring by ring.
            Probe past = new Probe
            {
                Polygon = Upright(20, -10, 0, 50, 10),
                Triangle = Tri(Pt(20, -10, 0), Pt(20, 50, 0), Pt(20, 20, 30)),
                Face = Holed(Upright(20, -10, 0, 50, 10), Upright(20, 0, 4, 40, 8))
            };
            cases.Add(new FlatCase
            {
                Name = "touching along an edge that runs past the rims",
                Asked = Base,
                Argument = From(past),
                Expected = (a, b) => a == K.Polygon ? Meet(Pt(20, 0, 0), Pt(20, 40, 0))
                    : a == K.Triangle ? Meet(Pt(20, 0, 0), Pt(20, 20, 0))
                    : Meet(Pt(20, 0, 0), Pt(20, 40, 0), Pt(20, 15, 0), Pt(20, 25, 0))
            });

            // Through the plate's hole from y = 17 to 23, 2 clear of its rim at y = 15 and 25 either side: the probe's
            // sides cross z = 0 in the hole, and the plate's edges meet x = 20 at y = 0, 15, 25 and 40, off the probe.
            Probe throughHole = new Probe
            {
                Polygon = Upright(20, 17, -10, 23, 10),
                Triangle = Tri(Pt(20, 17, -10), Pt(20, 23, -10), Pt(20, 20, 10)),
                Face = Holed(Upright(20, 17, -10, 23, 10), Upright(20, 19, 4, 21, 8))
            };
            cases.Add(new FlatCase
            {
                Name = "through the asked plate's hole",
                Asked = k => BasePlate(),
                Argument = From(throughHole),
                Expected = (a, b) => Apart(),
                Kinds = new[] { (K.Face, K.Polygon), (K.Face, K.Triangle), (K.Face, K.Face) }
            });

            // The base through a hole from y = -5 to 45 and z = -5 to 5 in an upright plate 60 by 20: every edge of
            // the base meets x = 20 in the hole, and the plate's sides cross z = 0 at y = -10, -5, 45 and 50, off it.
            GeoFace3 frame = Holed(Upright(20, -10, -10, 50, 10), Upright(20, -5, -5, 45, 5));
            cases.Add(new FlatCase
            {
                Name = "through the argument plate's hole",
                Asked = Base,
                Argument = k => frame,
                Expected = (a, b) => Apart(),
                Kinds = new[] { (K.Polygon, K.Face), (K.Triangle, K.Face), (K.Face, K.Face) }
            });

            // In the base's plane over its side x = 40, from y = 10 to 20: the two outlines cross at (40, 10) and
            // (40, 20). The triangle's slanted side y = x - 20 meets x = 40 at y = 20. The probe's hole and the
            // plate's lie well apart.
            Probe overlap = new Probe
            {
                Polygon = Square(30, 10, 50, 20),
                Triangle = Tri(Pt(30, 10, 0), Pt(50, 10, 0), Pt(50, 30, 0)),
                Face = Holed(Square(30, 10, 50, 20), Square(44, 13, 48, 17))
            };
            cases.Add(new FlatCase
            {
                Name = "in one plane, overlapping",
                Asked = Base,
                Argument = From(overlap),
                Expected = (a, b) => Meet(Pt(40, 10, 0), Pt(40, 20, 0)),
                Coplanar = true
            });

            // In the base's plane, 10 clear of its side x = 40.
            Probe beside = new Probe
            {
                Polygon = Square(50, 10, 60, 20),
                Triangle = Tri(Pt(50, 10, 0), Pt(60, 10, 0), Pt(60, 20, 0)),
                Face = Holed(Square(50, 10, 60, 20), Square(53, 13, 57, 17))
            };
            cases.Add(new FlatCase { Name = "in one plane, apart", Asked = Base, Argument = From(beside), Expected = (a, b) => Apart(), Coplanar = true });

            // A probe inside the base, 8 to 18 each way, turned off its plane about the line x = 13 through its middle.
            // The plate here has its hole at 25 to 35, clear of the probe, and the triangle is the half below x + y = 40.
            AddNearlyFlat(cases, 0.001);
            AddNearlyFlat(cases, 0.1);
            AddNearlyFlat(cases, 0.9);

            // The same probe turned half a degree about the line through its first corner, (8, 8, 0), along (1, -1, 0):
            // every other corner rises, (18, 18) by 14.1 sin 0.5 degrees = 0.123, so the two touch at that corner only.
            // A test of one plane that reads the first corner alone would call them coplanar and miss it.
            Probe corner = new Probe
            {
                Polygon = Turned(Square(8, 8, 18, 18), Pt(8, 8, 0), new GeoVector3(1, -1, 0), 0.5),
                Triangle = Turned(Tri(Pt(8, 8, 0), Pt(18, 8, 0), Pt(13, 18, 0)), Pt(8, 8, 0), new GeoVector3(1, -1, 0), 0.5),
                Face = Turned(Holed(Square(8, 8, 18, 18), Square(12, 12, 14, 14)), Pt(8, 8, 0), new GeoVector3(1, -1, 0), 0.5)
            };
            cases.Add(new FlatCase
            {
                Name = "half a degree off one plane, turned about a corner",
                Asked = NearBase,
                Argument = From(corner),
                Expected = (a, b) => Meet(Pt(8, 8, 0))
            });

            // A plate 10 across, 5E-4 above a plate 40 000 across turned 2E-7 rad about the y axis: the small plate's
            // corners lie 5E-4 off the large one's plane, inside the planar tolerance, while the large one's corners lie
            // 4E-3 off the small one's. One lying in the other's plane makes them one plane (E2), and the plane half
            // finds the small plate inside the large: they meet, and their rims cross nowhere. Read as two planes, no
            // edge of either climbs enough to cross the other, and they would be apart. Too large to sample.
            const double hairAbove = 5E-4;
            cases.Add(new FlatCase
            {
                Name = "a hair above a large plate turned 2E-7 rad",
                Asked = LargeTurnedPlate,
                Argument = From(new Probe
                {
                    Polygon = Square(-5, -5, 5, 5, hairAbove),
                    Triangle = Tri(Pt(-5, -5, hairAbove), Pt(5, -5, hairAbove), Pt(0, 5, hairAbove)),
                    Face = Holed(Square(-5, -5, 5, 5, hairAbove), Square(-2, -2, 2, 2, hairAbove))
                }),
                Expected = (a, b) => (true, new GeoPoint3[0], new GeoPoint3[0]),
                Coplanar = true,
                Band = true,
                Huge = true
            });

            // Upright in x = 20 from y = -10 to 10: its side at y = 10 crosses the base at (20, 10, 0), the triangle's
            // slanted side at (20, 5, 0), and the base's side y = 0 crosses the probe at (20, 0, 0). Both regions name
            // a point, so the order shows whose edges come first.
            Probe both = new Probe
            {
                Polygon = Upright(20, -10, -10, 10, 10),
                Triangle = Tri(Pt(20, -10, -10), Pt(20, 10, -10), Pt(20, 0, 10)),
                Face = Holed(Upright(20, -10, -10, 10, 10), Upright(20, 2, 4, 6, 8))
            };
            cases.Add(new FlatCase
            {
                Name = "crossing, an edge of each reaching the other",
                Asked = Base,
                Argument = From(both),
                Expected = (a, b) =>
                {
                    GeoPoint3 probe = b == K.Triangle ? Pt(20, 5, 0) : Pt(20, 10, 0);
                    return (true, new[] { Pt(20, 0, 0), probe }, new[] { probe, Pt(20, 0, 0) });
                }
            });

            foreach (FlatCase c in cases)
            {
                foreach ((K a, K b) in c.Kinds)
                {
                    rows.Add(FlatRow(c, a, b));
                }
            }
        }

        /// <summary>A plate 40 000 across about the origin, turned 2E-7 rad about the y axis; the face's hole far off.</summary>
        private static object LargeTurnedPlate(K kind)
        {
            double degrees = 2E-7 * 180.0 / Math.PI;
            GeoPoint3 o = Pt(0, 0, 0);

            return kind == K.Polygon ? Turned(Square(-20000, -20000, 20000, 20000), o, AlongY, degrees)
                : kind == K.Triangle ? (object)Turned(Tri(Pt(-20000, -20000, 0), Pt(20000, -20000, 0), Pt(0, 20000, 0)), o, AlongY, degrees)
                : Turned(Holed(Square(-20000, -20000, 20000, 20000), Square(10000, 10000, 11000, 11000)), o, AlongY, degrees);
        }

        private static object NearBase(K kind)
            => kind == K.Polygon ? BaseSquare()
                : kind == K.Triangle ? (object)Tri(Pt(0, 0, 0), Pt(40, 0, 0), Pt(0, 40, 0))
                : Holed(Square(0, 0, 40, 40), Square(25, 25, 35, 35));

        private static void AddNearlyFlat(List<FlatCase> cases, double degrees)
        {
            // Turned about x = 13 the corners rise and fall 5 sin(angle): 8.7E-5 at 0.001 degrees, well inside the
            // planar tolerance of 1E-3, so the two lie in one plane and the plane half answers: the probe lies inside
            // the base and their rims cross nowhere. At 0.1 and 0.9 degrees, 8.7E-3 and 0.079, they do not: the
            // probe's sides at y = 8 and 18 cross the base where they pass x = 13.
            Probe p = new Probe
            {
                Polygon = Turned(Square(8, 8, 18, 18), Pt(13, 0, 0), AlongY, degrees),
                Triangle = Turned(Tri(Pt(8, 8, 0), Pt(18, 8, 0), Pt(13, 18, 0)), Pt(13, 0, 0), AlongY, degrees),
                Face = Turned(Holed(Square(8, 8, 18, 18), Square(9, 10, 11, 16)), Pt(13, 0, 0), AlongY, degrees)
            };
            bool band = degrees < 0.01;

            cases.Add(new FlatCase
            {
                Name = degrees.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " degrees off one plane, inside",
                Asked = NearBase,
                Argument = From(p),
                Expected = (a, b) => band ? (true, new GeoPoint3[0], new GeoPoint3[0]) : Meet(Pt(13, 8, 0), Pt(13, 18, 0)),
                Coplanar = band,
                Band = band
            });
        }

        private static Row FlatRow(FlatCase c, K a, K b)
        {
            object asked = c.Asked(a);
            object argument = c.Argument(b);
            (bool collides, GeoPoint3[] askedFirst, GeoPoint3[] argumentFirst) = c.Expected(a, b);

            Row row = FlatPair(asked, argument);
            row.Name = row.Pair + ": " + c.Name;
            row.Collides = collides;
            row.Points = askedFirst;
            row.PointsArgumentFirst = argumentFirst;
            row.Band = c.Band;
            row.Order = c.Coplanar ? Order.PlaneHalf : a == b ? Order.AskedFirst : Order.EitherFirst;

            Flat fa = FlatOf(asked);
            Flat fb = FlatOf(argument);

            row.SampledMeet = c.Huge ? null : (Func<bool>)(() => SampledGap(fa, fb) <= Spacing);
            row.OnBoth = p => DistanceTo(fa, p) <= NearInBand && DistanceTo(fb, p) <= NearInBand;

            if (c.Coplanar)
            {
                row.PlaneHalfOrders = () => PlaneHalfOrders(asked, argument);
                row.PlaneHalfOrdersBack = () => PlaneHalfOrders(argument, asked);
                row.PlaneHalfMeets = () => PlaneHalfBooleans(asked, argument);
            }
            else
            {
                row.Subdivided = () => EdgeCrossings(fa, fb);
            }

            return row;
        }

        private static Flat FlatOf(object region)
            => region is GeoPolygon3 p ? Of(p) : region is GeoTriangle3 t ? Of(t) : Of((GeoFace3)region);

        private static GeoPolygon3 OutlineOf(object region)
            => region is GeoPolygon3 p ? p : region is GeoTriangle3 t ? Polygon(t.A, t.B, t.C) : ((GeoFace3)region).Boundary;

        private static object To2(object region, GeoCoordinateSystem3 frame)
            => region is GeoPolygon3 p ? PlanarMap.ProjectToPolygon2(frame, p)
                : region is GeoTriangle3 t ? (object)PlanarMap.ProjectToTriangle2(frame, t)
                : PlanarMap.ProjectToFace2(frame, (GeoFace3)region);

        private static GeoFace2 AsFace2(object region2)
            => region2 is GeoPolygon2 p ? new GeoFace2(p)
                : region2 is GeoTriangle2 t ? new GeoFace2(new GeoPolygon2(t.A, t.B, t.C))
                : (GeoFace2)region2;

        private static GeoPoint2[] Crossings2(object a, object b, Tolerance t)
        {
            switch (a)
            {
                case GeoPolygon2 p when b is GeoPolygon2 q: return p.GetIntersections(q, t);
                case GeoPolygon2 p when b is GeoTriangle2 q: return p.GetIntersections(q, t);
                case GeoPolygon2 p when b is GeoFace2 q: return p.GetIntersections(q, t);
                case GeoTriangle2 p when b is GeoPolygon2 q: return p.GetIntersections(q, t);
                case GeoTriangle2 p when b is GeoTriangle2 q: return p.GetIntersections(q, t);
                case GeoTriangle2 p when b is GeoFace2 q: return p.GetIntersections(q, t);
                case GeoFace2 p when b is GeoPolygon2 q: return p.GetIntersections(q, t);
                case GeoFace2 p when b is GeoTriangle2 q: return p.GetIntersections(q, t);
                case GeoFace2 p when b is GeoFace2 q: return p.GetIntersections(q, t);
            }

            throw new ArgumentException("not a region");
        }

        /// <summary>The plane half's crossings of two regions, laid out in the frame of either, lifted back.</summary>
        private static GeoPoint3[][] PlaneHalfOrders(object asked, object argument)
        {
            return new[] { OutlineOf(asked), OutlineOf(argument) }
                .Select(PlanarMap.FrameOf)
                .Select(frame => Crossings2(To2(asked, frame), To2(argument, frame), Within).Select(p => PlanarMap.ToPoint3(frame, p)).ToArray())
                .ToArray();
        }

        /// <summary>
        /// Whether the plane half's booleans find the two meeting, and the corners of what they share that lie on both
        /// rims, which are the places their rims cross.
        /// </summary>
        private static (bool, GeoPoint3[]) PlaneHalfBooleans(object asked, object argument)
        {
            GeoCoordinateSystem3 frame = PlanarMap.FrameOf(OutlineOf(asked));
            GeoFace2 a = AsFace2(To2(asked, frame));
            GeoFace2 b = AsFace2(To2(argument, frame));
            GeoFace2[] shared = Boolean2.Intersect(a, b, Within);

            if (shared.Length == 0 || shared.Sum(f => f.Area) <= 1E-6)
            {
                return (false, new GeoPoint3[0]);
            }

            Flat fa = FlatOf(asked);
            Flat fb = FlatOf(argument);
            var rims = new List<GeoPoint3>();

            foreach (GeoFace2 piece in shared)
            {
                foreach (GeoPolygon2 ring in new[] { piece.Boundary }.Concat(piece.Holes))
                {
                    foreach (GeoPoint2 corner in ring.Vertices)
                    {
                        GeoPoint3 p = PlanarMap.ToPoint3(frame, corner);

                        if (OnRim(fa, p) && OnRim(fb, p) && !rims.Any(q => q.DistanceTo(p) <= 1E-3))
                        {
                            rims.Add(p);
                        }
                    }
                }
            }

            return (true, rims.ToArray());
        }

        private static bool OnRim(Flat f, GeoPoint3 p)
        {
            double best = double.MaxValue;

            foreach (double[][] ring in f.Rings)
            {
                for (int i = 0; i < ring.Length; i++)
                {
                    var line = new GeoLine3(new GeoPoint3(ring[i][0], ring[i][1], ring[i][2]), new GeoPoint3(ring[(i + 1) % ring.Length][0], ring[(i + 1) % ring.Length][1], ring[(i + 1) % ring.Length][2]));
                    GeoVector3 d = line.StartPoint.GetVectorTo(line.EndPoint);
                    double t = Math.Max(0.0, Math.Min(1.0, line.StartPoint.GetVectorTo(p).DotProduct(d) / d.DotProduct(d)));
                    best = Math.Min(best, line.StartPoint.Add(d.Multiply(t)).DistanceTo(p));
                }
            }

            return best <= 1E-3;
        }

        private static Row FlatPair(object asked, object argument)
        {
            switch (asked)
            {
                case GeoPolygon3 a when argument is GeoPolygon3 b:
                    return Both(a, b, "Polygon3 x Polygon3",
                        (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                        (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                        (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                        (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p));
                case GeoPolygon3 a when argument is GeoTriangle3 b:
                    return Both(a, b, "Polygon3 x Triangle3",
                        (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                        (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                        (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                        (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p));
                case GeoPolygon3 a when argument is GeoFace3 b:
                    return Both(a, b, "Polygon3 x Face3",
                        (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                        (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                        (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                        (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p));
                case GeoTriangle3 a when argument is GeoTriangle3 b:
                    // The two triangles' CollidesWith is the library's of old: only the crossings are new.
                    return Both(a, b, "Triangle3 x Triangle3",
                        null, null, null, null,
                        (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                        (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                        (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p));
                case GeoTriangle3 a when argument is GeoFace3 b:
                    return Both(a, b, "Triangle3 x Face3",
                        (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                        (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                        (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                        (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p));
                case GeoFace3 a when argument is GeoFace3 b:
                    return Both(a, b, "Face3 x Face3",
                        (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                        (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                        (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                        (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p));
                case GeoFace3 a when argument is GeoPolygon3 b:
                    return Both(a, b, "Face3 x Polygon3",
                        (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                        (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                        (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                        (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p));
                case GeoFace3 a when argument is GeoTriangle3 b:
                    return Both(a, b, "Face3 x Triangle3",
                        (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                        (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                        (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                        (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p));
            }

            throw new ArgumentException("not a pair of regions");
        }

        #endregion

        #region A plane

        private static GeoPlane3 Floor() => new GeoPlane3(Pt(0, 0, 0), Up);

        private static void AddPlanes(List<Row> rows)
        {
            GeoPlane3 floor = Floor();

            // Upright in y = 20, from z = -10 to 10: the rectangle's sides at x = 40 and 0 cross z = 0 there; the
            // triangle's slanted sides, from (40, -10) and (0, -10) to (20, 10), half way, at x = 30 and 10; the plate
            // its outline's two and then its hole's sides at x = 25 and 15.
            PlaneCase(rows, floor, "crossing, and through the plate's hole",
                Across(20, 0, -10, 40, 10), new[] { Pt(40, 20, 0), Pt(0, 20, 0) },
                Tri(Pt(0, 20, -10), Pt(40, 20, -10), Pt(20, 20, 10)), new[] { Pt(30, 20, 0), Pt(10, 20, 0) },
                Holed(Across(20, 0, -10, 40, 10), Across(20, 15, -5, 25, 5)), new[] { Pt(40, 20, 0), Pt(0, 20, 0), Pt(25, 20, 0), Pt(15, 20, 0) });

            PlaneCase(rows, floor, "apart, 5 above",
                Across(20, 0, 5, 40, 25), null,
                Tri(Pt(0, 20, 5), Pt(40, 20, 5), Pt(20, 20, 25)), null,
                Holed(Across(20, 0, 5, 40, 25), Across(20, 15, 10, 25, 20)), null);

            // Each stands on its corner (20, 20, 0): the sides from it name the one place.
            GeoPoint3 tip = Pt(20, 20, 0);
            PlaneCase(rows, floor, "through a corner",
                Polygon(tip, Pt(30, 20, 10), Pt(20, 20, 20), Pt(10, 20, 10)), new[] { tip },
                Tri(tip, Pt(30, 20, 10), Pt(10, 20, 10)), new[] { tip },
                Holed(Polygon(tip, Pt(30, 20, 10), Pt(20, 20, 20), Pt(10, 20, 10)), Across(20, 18, 8, 22, 12)), new[] { tip });

            // Standing on the plane along the bottom side: that side lies in it and is crossed nowhere; the sides from
            // its ends name them, (40, 20, 0) first, as the second side starts there.
            PlaneCase(rows, floor, "along an edge",
                Across(20, 0, 0, 40, 10), new[] { Pt(40, 20, 0), Pt(0, 20, 0) },
                Tri(Pt(0, 20, 0), Pt(40, 20, 0), Pt(20, 20, 10)), new[] { Pt(40, 20, 0), Pt(0, 20, 0) },
                Holed(Across(20, 0, 0, 40, 10), Across(20, 15, 3, 25, 7)), new[] { Pt(40, 20, 0), Pt(0, 20, 0) });

            PlaneCase(rows, floor, "lying in it",
                BaseSquare(), new GeoPoint3[0], BaseTriangle(), new GeoPoint3[0], BasePlate(), new GeoPoint3[0]);

            PlaneCase(rows, floor, "parallel, 5 above",
                Square(0, 0, 40, 40, 5), null,
                Tri(Pt(0, 0, 5), Pt(40, 0, 5), Pt(40, 40, 5)), null,
                Holed(Square(0, 0, 40, 40, 5), Square(15, 15, 25, 25, 5)), null);

            // Turned about x = 20 by a thousandth of a degree, a region 20 across rises and falls 1.7E-4 and no side
            // climbs more than 3.5E-4, under the tolerance of 1E-3: it lies in the plane, meets it, and crosses it
            // nowhere, as a segment lying in a plane crosses it nowhere.
            NearlyLevel(rows, floor, 0.001, true);

            // Half a degree, 0.087 either side: the sides at y = 10 and 30 cross at x = 20.
            NearlyLevel(rows, floor, 0.5, false);

            // A ray, read by its own sign test: crossing where it reaches the plane, from its origin when it starts on
            // it, and nowhere, but touching, when it lies in it.
            PlaneRay(rows, floor, "crossing", new GeoRay3(Pt(5, 5, 10), -Up), true, Pt(5, 5, 0));
            PlaneRay(rows, floor, "starting on it", new GeoRay3(Pt(5, 5, 0), new GeoVector3(1, 0, 1)), true, Pt(5, 5, 0));
            PlaneRay(rows, floor, "lying in it", new GeoRay3(Pt(5, 5, 0), GeoVector3.XAxis), true);
            PlaneRay(rows, floor, "parallel, 3 above", new GeoRay3(Pt(5, 5, 3), GeoVector3.XAxis), false);
            PlaneRay(rows, floor, "pointing away", new GeoRay3(Pt(5, 5, 3), Up), false);

            // Half a degree down from 1 above, it reaches the plane at 1 / tan(0.5 degrees) = 114.5886501.
            double shallow = 0.5 * Math.PI / 180.0;
            PlaneRay(rows, floor, "half a degree down from 1 above", new GeoRay3(Pt(0, 0, 1), new GeoVector3(Math.Cos(shallow), 0, -Math.Sin(shallow))), true, Pt(Math.Cos(shallow) / Math.Sin(shallow), 0, 0));

            // A body: the box 40 by 40 by 20 from the origin.
            var block = Body(Pt(0, 0, 0), Pt(40, 40, 20));
            PlaneSolid(rows, "through the middle", new GeoPlane3(Pt(0, 0, 10), Up), block.Solid, true, Piece(Pt(0, 0, 0), Pt(40, 40, 20)));
            // Through the corner at the origin only: every other corner lies on the far side of x + y + z = 0.
            PlaneSolid(rows, "through a corner", new GeoPlane3(Pt(0, 0, 0), new GeoVector3(1, 1, 1)), block.Solid, true, Piece(Pt(0, 0, 0), Pt(40, 40, 20)));
            PlaneSolid(rows, "along its top", new GeoPlane3(Pt(0, 0, 20), Up), block.Solid, true, Piece(Pt(0, 0, 0), Pt(40, 40, 20)));
            PlaneSolid(rows, "apart, 5 above", new GeoPlane3(Pt(0, 0, 25), Up), block.Solid, false, Piece(Pt(0, 0, 0), Pt(40, 40, 20)));

            // The box cut through by an opening from y = 18 to 22 is two pieces, with corners either side of y = 20,
            // and the plane y = 20 runs between them through no material. The plane y = 10 cuts the first.
            var halves = Body(Pt(0, 0, 0), Pt(40, 40, 20), (Pt(-5, 18, -5), Pt(45, 22, 25)));
            PlaneSolid(rows, "between the two pieces of a body cut in two", new GeoPlane3(Pt(0, 20, 0), AlongY), halves.Solid, false,
                Piece(Pt(0, 0, 0), Pt(40, 18, 20)), Piece(Pt(0, 22, 0), Pt(40, 40, 20)));
            PlaneSolid(rows, "through one of the two pieces of a body cut in two", new GeoPlane3(Pt(0, 10, 0), AlongY), halves.Solid, true,
                Piece(Pt(0, 0, 0), Pt(40, 18, 20)), Piece(Pt(0, 22, 0), Pt(40, 40, 20)));
        }

        private static void PlaneCase(List<Row> rows, GeoPlane3 plane, string name,
            GeoPolygon3 polygon, GeoPoint3[] polygonPoints, GeoTriangle3 triangle, GeoPoint3[] trianglePoints, GeoFace3 face, GeoPoint3[] facePoints)
        {
            rows.Add(PlaneRow(PlaneWith(plane, polygon), name, Of(polygon), plane, polygonPoints));
            rows.Add(PlaneRow(PlaneWith(plane, triangle), name, Of(triangle), plane, trianglePoints));
            rows.Add(PlaneRow(PlaneWith(plane, face), name, Of(face), plane, facePoints));
        }

        private static void NearlyLevel(List<Row> rows, GeoPlane3 plane, double degrees, bool band)
        {
            GeoPoint3 pivot = Pt(20, 0, 0);
            GeoPolygon3 polygon = Turned(Square(10, 10, 30, 30), pivot, AlongY, degrees);
            GeoTriangle3 triangle = Turned(Tri(Pt(10, 10, 0), Pt(30, 10, 0), Pt(20, 30, 0)), pivot, AlongY, degrees);
            GeoFace3 face = Turned(Holed(Square(10, 10, 30, 30), Square(12, 14, 16, 26)), pivot, AlongY, degrees);
            GeoPoint3[] points = band ? new GeoPoint3[0] : new[] { Pt(20, 10, 0), Pt(20, 30, 0) };
            string name = degrees.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " degrees off it, through its middle";

            foreach (Row row in new[]
            {
                PlaneRow(PlaneWith(plane, polygon), name, Of(polygon), plane, points),
                PlaneRow(PlaneWith(plane, triangle), name, Of(triangle), plane, points),
                PlaneRow(PlaneWith(plane, face), name, Of(face), plane, points)
            })
            {
                row.Band = band;

                if (band)
                {
                    // The tolerance, not the geometry, makes these cross nowhere: the oracle is not asked where.
                    row.Subdivided = null;
                }

                rows.Add(row);
            }
        }

        private static Row PlaneWith(GeoPlane3 plane, GeoPolygon3 polygon)
            => Both(plane, polygon, "Plane3 x Polygon3",
                (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p));

        private static Row PlaneWith(GeoPlane3 plane, GeoTriangle3 triangle)
            => Both(plane, triangle, "Plane3 x Triangle3",
                (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p));

        private static Row PlaneWith(GeoPlane3 plane, GeoFace3 face)
            => Both(plane, face, "Plane3 x Face3",
                (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p));

        private static Row PlaneRow(Row row, string name, Flat region, GeoPlane3 plane, GeoPoint3[] points)
        {
            row.Name = row.Pair + ": " + name;
            row.Collides = points != null;
            row.Points = points ?? new GeoPoint3[0];
            row.Order = Order.Same;

            // Meeting where the samples' heights change sign or reach nought; crossing where a side's do.
            row.SampledMeet = () =>
            {
                (double low, double high) = SampledHeights(region, plane);
                return low <= 1E-9 && high >= -1E-9;
            };
            row.Subdivided = () => PlaneCrossings(region, plane);

            return row;
        }

        private static void PlaneRay(List<Row> rows, GeoPlane3 plane, string name, GeoRay3 ray, bool collides, params GeoPoint3[] points)
        {
            Row row = Both(plane, ray, "Plane3 x Ray3",
                (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                null, null, null, null);
            row.Name = row.Pair + ": " + name;
            row.Collides = collides;
            row.Points = points;
            row.Order = Order.Same;
            rows.Add(row);
        }

        private static (GeoPoint3 Min, GeoPoint3 Max) Piece(GeoPoint3 min, GeoPoint3 max) => (min, max);

        private static void PlaneSolid(List<Row> rows, string name, GeoPlane3 plane, GeoSolid3 solid, bool collides, params (GeoPoint3 Min, GeoPoint3 Max)[] pieces)
        {
            Row row = Both(plane, solid, "Plane3 x Solid3",
                (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                null, null, null, null, null, null, null, null);
            row.Name = row.Pair + ": " + name;
            row.Collides = collides;

            // The material as the boxes it is made of: a plane meets a box exactly where the box's corners' heights
            // over it change sign or reach nought, and a body where it meets one of its pieces.
            row.SampledMeet = () => pieces.Any(piece =>
            {
                double[] heights = new[] { piece.Min.X, piece.Max.X }
                    .SelectMany(x => new[] { piece.Min.Y, piece.Max.Y }.SelectMany(y => new[] { piece.Min.Z, piece.Max.Z }.Select(z => plane.SignedDistanceTo(Pt(x, y, z)))))
                    .ToArray();
                return heights.Min() <= 1E-9 && heights.Max() >= -1E-9;
            });
            rows.Add(row);
        }

        #endregion

        #region Rays

        private static void AddRays(List<Row> rows)
        {
            var along = new GeoRay3(Pt(0, 0, 0), GeoVector3.XAxis);

            RayRay(rows, "crossing ahead of both", along, new GeoRay3(Pt(10, -10, 0), AlongY), true, Pt(10, 0, 0));
            RayRay(rows, "one starting on the other", along, new GeoRay3(Pt(10, 0, 0), new GeoVector3(0, 1, 1)), true, Pt(10, 0, 0));
            RayRay(rows, "from one origin", along, new GeoRay3(Pt(0, 0, 0), AlongY), true, Pt(0, 0, 0));
            // The lines meet at (10, 0, 0), 5 behind the second ray's origin.
            RayRay(rows, "the lines meeting behind one origin", along, new GeoRay3(Pt(10, 5, 0), AlongY), false);
            RayRay(rows, "skew, 3 apart", along, new GeoRay3(Pt(10, -10, 3), AlongY), false);
            // 0.0004 apart, under the point tolerance of 1E-3: one place, within the tolerance of (10, 0, 0).
            Row skew = RayRay(rows, "skew, within the tolerance", along, new GeoRay3(Pt(10, -10, 0.0004), AlongY), true, Pt(10, 0, 0));
            skew.Band = true;
            RayRay(rows, "parallel, 5 apart", along, new GeoRay3(Pt(0, 5, 0), GeoVector3.XAxis), false);
            // Along one line they meet along a length, and name no place, as two segments along each other name none.
            RayRay(rows, "along one line, the same way", along, new GeoRay3(Pt(10, 0, 0), GeoVector3.XAxis), true);
            RayRay(rows, "along one line, facing each other", along, new GeoRay3(Pt(10, 0, 0), -GeoVector3.XAxis), true);
            RayRay(rows, "along one line, from one origin, opposite ways", along, new GeoRay3(Pt(0, 0, 0), -GeoVector3.XAxis), true);
            RayRay(rows, "along one line, pointing apart", along, new GeoRay3(Pt(-10, 0, 0), -GeoVector3.XAxis), false);
            // Half a degree apart, from 1 below the first: the second reaches y = 0 at x = 1 / tan(0.5 degrees).
            double shallow = 0.5 * Math.PI / 180.0;
            RayRay(rows, "half a degree apart", along, new GeoRay3(Pt(0, -1, 0), new GeoVector3(Math.Cos(shallow), Math.Sin(shallow), 0)), true,
                Pt(Math.Cos(shallow) / Math.Sin(shallow), 0, 0));

            // A ray against a flat region: as a segment against it with the far end taken away. The base square, its
            // triangle half below y = x, and the plate with its hole from 15 to 25.
            RayRegion(rows, "crossing", new GeoRay3(Pt(25, 3, 10), -Up), Pt(25, 3, 0), Pt(25, 3, 0), Pt(25, 3, 0));
            RayRegion(rows, "starting on it", new GeoRay3(Pt(25, 3, 0), new GeoVector3(0, 1, 1)), Pt(25, 3, 0), Pt(25, 3, 0), Pt(25, 3, 0));
            RayRegion(rows, "starting inside it, lying in its plane", new GeoRay3(Pt(25, 3, 0), GeoVector3.XAxis), null, null, null);
            RayRegion(rows, "pointing away", new GeoRay3(Pt(25, 3, 2), Up), null, null, null);
            RayRegion(rows, "through the plate's hole", new GeoRay3(Pt(21, 19, 10), -Up), Pt(21, 19, 0), Pt(21, 19, 0), null);
            RayRegion(rows, "at a corner", new GeoRay3(Pt(40, 0, 10), -Up), Pt(40, 0, 0), Pt(40, 0, 0), Pt(40, 0, 0));
        }

        private static Row RayRay(List<Row> rows, string name, GeoRay3 a, GeoRay3 b, bool collides, params GeoPoint3[] points)
        {
            Row row = Both(a, b, "Ray3 x Ray3",
                (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p));
            row.Name = row.Pair + ": " + name;
            row.Collides = collides;
            row.Points = points;
            row.Order = Order.Set;
            rows.Add(row);
            return row;
        }

        private static void RayRegion(List<Row> rows, string name, GeoRay3 ray, GeoPoint3? onSquare, GeoPoint3? onTriangle, GeoPoint3? onPlate)
        {
            Row square = Both(BaseSquare(), ray, "Polygon3 x Ray3",
                null, null, null, null,
                (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                null, null, null, null);
            Row triangle = Both(BaseTriangle(), ray, "Triangle3 x Ray3",
                null, null, null, null,
                (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                null, null, null, null);
            Row plate = Both(BasePlate(), ray, "Face3 x Ray3",
                null, null, null, null,
                (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                null, null, null, null);

            foreach ((Row row, GeoPoint3? at) in new[] { (square, onSquare), (triangle, onTriangle), (plate, onPlate) })
            {
                row.Name = row.Pair + ": " + name;
                row.Points = at.HasValue ? new[] { at.Value } : new GeoPoint3[0];
                row.Order = Order.Same;
                rows.Add(row);
            }
        }

        #endregion

        #region A body against a flat region

        private static void AddBodies(List<Row> rows)
        {
            // The box 40 by 40 by 20 from the origin, and the same pierced by an opening 10 by 10 from x, y = 15 to 25.
            var block = Body(Pt(0, 0, 0), Pt(40, 40, 20));
            var pierced = Body(Pt(0, 0, 0), Pt(40, 40, 20), (Pt(15, 15, -5), Pt(25, 25, 25)));

            // Upright in y = 10, poking through the top: the rectangle's sides at x = 30 and 10 cross z = 20; the
            // triangle's slanted sides, from (30, 10) and (10, 10) up to (20, 30), half way, at x = 25 and 15. The
            // body's own edges meet y = 10 at x = 0 and 40, off the region.
            BodyCase(rows, block, "poking through the top", true, false,
                Across(10, 10, 10, 30, 30), new[] { Pt(30, 10, 20), Pt(10, 10, 20) },
                Tri(Pt(10, 10, 10), Pt(30, 10, 10), Pt(20, 10, 30)), new[] { Pt(25, 10, 20), Pt(15, 10, 20) },
                Holed(Across(10, 10, 10, 30, 30), Across(10, 14, 24, 26, 28)), new[] { Pt(30, 10, 20), Pt(10, 10, 20) });

            BodyCase(rows, block, "wholly inside", true, false,
                Across(10, 10, 5, 30, 15), new GeoPoint3[0],
                Tri(Pt(10, 10, 5), Pt(30, 10, 5), Pt(20, 10, 15)), new GeoPoint3[0],
                Holed(Across(10, 10, 5, 30, 15), Across(10, 14, 8, 26, 12)), new GeoPoint3[0]);

            BodyCase(rows, block, "apart, 5 above", false, false,
                Across(10, 10, 25, 30, 45), new GeoPoint3[0],
                Tri(Pt(10, 10, 25), Pt(30, 10, 25), Pt(20, 10, 45)), new GeoPoint3[0],
                Holed(Across(10, 10, 25, 30, 45), Across(10, 14, 30, 26, 40)), new GeoPoint3[0]);

            GeoPoint3 tip = Pt(20, 10, 20);
            BodyCase(rows, block, "touching the top at a corner", true, true,
                Polygon(tip, Pt(30, 10, 30), Pt(20, 10, 40), Pt(10, 10, 30)), new[] { tip },
                Tri(tip, Pt(30, 10, 30), Pt(10, 10, 30)), new[] { tip },
                Holed(Polygon(tip, Pt(30, 10, 30), Pt(20, 10, 40), Pt(10, 10, 30)), Across(10, 18, 28, 22, 32)), new[] { tip });

            BodyCase(rows, block, "standing on the top along an edge", true, true,
                Across(10, 10, 20, 30, 30), new[] { Pt(30, 10, 20), Pt(10, 10, 20) },
                Tri(Pt(10, 10, 20), Pt(30, 10, 20), Pt(20, 10, 30)), new[] { Pt(30, 10, 20), Pt(10, 10, 20) },
                Holed(Across(10, 10, 20, 30, 30), Across(10, 14, 24, 26, 28)), new[] { Pt(30, 10, 20), Pt(10, 10, 20) });

            // Lying on the top: no side crosses the surface and no edge of the body crosses the region, and the two
            // touch over the whole region.
            BodyCase(rows, block, "lying on the top", true, true,
                Square(5, 5, 15, 15, 20), new GeoPoint3[0],
                Tri(Pt(5, 5, 20), Pt(15, 5, 20), Pt(5, 15, 20)), new GeoPoint3[0],
                Holed(Square(5, 5, 15, 15, 20), Square(8, 8, 12, 12, 20)), new GeoPoint3[0]);

            // Upright in y = 10 and wider than the body: its bottom side crosses the walls x = 0 and 40 at z = 10, and
            // the body's top edges along y at x = 0 and 40 cross the region at z = 20. Edges of a triangulation of the
            // faces are not the body's edges: the top's diagonal would cross at (10, 10, 20), a side's at (0, 10, 15).
            BodyCase(rows, block, "cut across, the body's edges crossing the region", true, false,
                Across(10, -10, 10, 50, 30), new[] { Pt(0, 10, 10), Pt(40, 10, 10), Pt(0, 10, 20), Pt(40, 10, 20) },
                Tri(Pt(-10, 10, 10), Pt(50, 10, 10), Pt(20, 10, 40)), new[] { Pt(0, 10, 10), Pt(40, 10, 10), Pt(40, 10, 20), Pt(0, 10, 20) },
                Holed(Across(10, -10, 10, 50, 30), Across(10, 14, 24, 26, 28)), new[] { Pt(0, 10, 10), Pt(40, 10, 10), Pt(0, 10, 20), Pt(40, 10, 20) });

            // Upright in y = 20 through the opening, 2 clear of its walls at x = 15 and 25: the top and bottom faces run
            // across the opening, and the region passes them where there is no material.
            BodyCase(rows, pierced, "through the opening without touching", false, false,
                Across(20, 17, -10, 23, 30), new GeoPoint3[0],
                Tri(Pt(17, 20, -10), Pt(23, 20, -10), Pt(20, 20, 30)), new GeoPoint3[0],
                Holed(Across(20, 17, -10, 23, 30), Across(20, 19, 0, 21, 5)), new GeoPoint3[0]);

            // In the opening, its side x = 15 lying on the opening's wall: the sides along x leave the wall at z = 5
            // and 15.
            BodyCase(rows, pierced, "in the opening, against its wall", true, true,
                Across(20, 15, 5, 20, 15), new[] { Pt(15, 20, 5), Pt(15, 20, 15) },
                Tri(Pt(15, 20, 5), Pt(20, 20, 10), Pt(15, 20, 15)), new[] { Pt(15, 20, 5), Pt(15, 20, 15) },
                Holed(Across(20, 15, 5, 20, 15), Across(20, 16, 8, 19, 12)), new[] { Pt(15, 20, 5), Pt(15, 20, 15) });
        }

        private static void BodyCase(List<Row> rows, (GeoSolid3 Solid, MeetingInSpaceOracle.Boxes Held) body, string name, bool collides, bool touching,
            GeoPolygon3 polygon, GeoPoint3[] polygonPoints, GeoTriangle3 triangle, GeoPoint3[] trianglePoints, GeoFace3 face, GeoPoint3[] facePoints)
        {
            GeoSolid3 solid = body.Solid;
            GeoSolid3 material = solid.Openings.Count == 0 ? solid : solid.TryCutOpenings(out GeoSolid3 cut, Within) ? cut : null;

            Row withTriangle = Both(solid, triangle, "Solid3 x Triangle3",
                (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p));

            // A body and a polygon or a face could say whether they meet already: only where they cross is new.
            Row withPolygon = Both(solid, polygon, "Solid3 x Polygon3",
                null, null, null, null,
                (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p));

            Row withFace = Both(solid, face, "Solid3 x Face3",
                null, null, null, null,
                (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p));

            foreach ((Row row, Flat region, GeoPoint3[] points, Func<GeoSolid3> slab) in new[]
            {
                (withTriangle, Of(triangle), trianglePoints, (Func<GeoSolid3>)(() => Slab(Polygon(triangle.A, triangle.B, triangle.C)))),
                (withPolygon, Of(polygon), polygonPoints, () => Slab(polygon)),
                (withFace, Of(face), facePoints, () => Slab(face))
            })
            {
                row.Name = row.Pair + ": " + name;
                row.Collides = collides;
                row.Points = points;
                row.Order = Order.Set;
                row.SampledMeet = () => SampledInMaterial(region, body.Held);
                row.OnBoth = p => DistanceTo(region, p) <= Near && body.Held.InMaterial(new[] { p.X, p.Y, p.Z });
                row.Subdivided = () => BodyCrossings(region, body.Held, material);

                if (!touching)
                {
                    // Where the two touch, a slab around the region shares no volume with the body; elsewhere it shares
                    // a slab's worth or none.
                    row.SlabMeets = () => solid.TryIntersect(slab(), out GeoSolid3 shared, Within) && shared != null && shared.GetVolume(Within) > 1E-6;
                }

                rows.Add(row);
            }
        }

        /// <summary>A region made into a slab 1 thick, half either side of its plane.</summary>
        private static GeoSolid3 Slab(GeoPolygon3 polygon)
        {
            GeoVector3 n = polygon.Normal.Multiply(SlabHalf);
            return GeoSolid3.Extrude(new GeoPolygon3(polygon.Vertices.Select(p => p.Subtract(n))), n.Multiply(2.0), Within);
        }

        private static GeoSolid3 Slab(GeoFace3 face)
        {
            GeoVector3 n = face.Boundary.Normal.Multiply(SlabHalf);
            Func<GeoPolygon3, GeoPolygon3> down = ring => new GeoPolygon3(ring.Vertices.Select(p => p.Subtract(n)));
            return GeoSolid3.Extrude(new GeoFace3(down(face.Boundary), face.Holes.Select(down)), n.Multiply(2.0), Within);
        }

        #endregion

        #region Boxes, through their body

        private static void AddBoxes(List<Row> rows)
        {
            var aabb = new GeoAabb3(Pt(0, 0, 0), Pt(40, 40, 20));
            GeoSolid3 aabbBody = aabb.ToObb().ToSolid();

            // A box turned 30 degrees about z, centred where the aligned one is.
            double turn = 30.0 * Math.PI / 180.0;
            var obb = new GeoObb3(Pt(20, 20, 10), 40, 40, 20, new GeoVector3(Math.Cos(turn), Math.Sin(turn), 0), new GeoVector3(-Math.Sin(turn), Math.Cos(turn), 0));
            GeoSolid3 obbBody = obb.ToSolid();

            var regions = new (string Name, bool Collides, GeoPolygon3 Polygon, GeoTriangle3 Triangle, GeoFace3 Face)[]
            {
                ("poking through the top", true, Across(10, 10, 10, 30, 30), Tri(Pt(10, 10, 10), Pt(30, 10, 10), Pt(20, 10, 30)), Holed(Across(10, 10, 10, 30, 30), Across(10, 14, 24, 26, 28))),
                ("apart, 5 above", false, Across(10, 10, 25, 30, 45), Tri(Pt(10, 10, 25), Pt(30, 10, 25), Pt(20, 10, 45)), Holed(Across(10, 10, 25, 30, 45), Across(10, 14, 30, 26, 40))),
                ("touching the top at a corner", true, Polygon(Pt(20, 10, 20), Pt(30, 10, 30), Pt(20, 10, 40), Pt(10, 10, 30)), Tri(Pt(20, 10, 20), Pt(30, 10, 30), Pt(10, 10, 30)),
                    Holed(Polygon(Pt(20, 10, 20), Pt(30, 10, 30), Pt(20, 10, 40), Pt(10, 10, 30)), Across(10, 18, 28, 22, 32))),
                ("cut across", true, Across(10, -10, 10, 50, 30), Tri(Pt(-10, 10, 10), Pt(50, 10, 10), Pt(20, 10, 40)), Holed(Across(10, -10, 10, 50, 30), Across(10, 14, 24, 26, 28)))
            };

            foreach (var r in regions)
            {
                rows.Add(BoxRow(Both(aabb, r.Polygon, "Aabb3 x Polygon3",
                    (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                    (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                    (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                    (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p)),
                    r.Name, r.Collides, t => (aabbBody.CollidesWith(r.Polygon, t), r.Polygon.CollidesWith(aabbBody, t), aabbBody.GetIntersections(r.Polygon, t), r.Polygon.GetIntersections(aabbBody, t))));
                rows.Add(BoxRow(Both(aabb, r.Triangle, "Aabb3 x Triangle3",
                    (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                    (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                    (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                    (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p)),
                    r.Name, r.Collides, t => (aabbBody.CollidesWith(r.Triangle, t), r.Triangle.CollidesWith(aabbBody, t), aabbBody.GetIntersections(r.Triangle, t), r.Triangle.GetIntersections(aabbBody, t))));
                rows.Add(BoxRow(Both(aabb, r.Face, "Aabb3 x Face3",
                    (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                    (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                    (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                    (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p)),
                    r.Name, r.Collides, t => (aabbBody.CollidesWith(r.Face, t), r.Face.CollidesWith(aabbBody, t), aabbBody.GetIntersections(r.Face, t), r.Face.GetIntersections(aabbBody, t))));

                // The turned box's polygon CollidesWith is the library's of old.
                rows.Add(BoxRow(Both(obb, r.Polygon, "Obb3 x Polygon3",
                    null, null, null, null,
                    (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                    (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                    (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p)),
                    r.Name, r.Collides, t => ((bool?)null, (bool?)null, obbBody.GetIntersections(r.Polygon, t), r.Polygon.GetIntersections(obbBody, t))));
                rows.Add(BoxRow(Both(obb, r.Triangle, "Obb3 x Triangle3",
                    (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                    (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                    (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                    (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p)),
                    r.Name, r.Collides, t => (obbBody.CollidesWith(r.Triangle, t), r.Triangle.CollidesWith(obbBody, t), obbBody.GetIntersections(r.Triangle, t), r.Triangle.GetIntersections(obbBody, t))));
                rows.Add(BoxRow(Both(obb, r.Face, "Obb3 x Face3",
                    (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                    (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                    (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                    (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p)),
                    r.Name, r.Collides, t => (obbBody.CollidesWith(r.Face, t), r.Face.CollidesWith(obbBody, t), obbBody.GetIntersections(r.Face, t), r.Face.GetIntersections(obbBody, t))));
            }

            var planes = new (string Name, bool Collides, GeoPlane3 Plane)[]
            {
                ("through the middle", true, new GeoPlane3(Pt(0, 0, 10), Up)),
                ("along its top", true, new GeoPlane3(Pt(0, 0, 20), Up)),
                ("apart, 5 above", false, new GeoPlane3(Pt(0, 0, 25), Up))
            };

            foreach (var p in planes)
            {
                rows.Add(BoxRow(Both(aabb, p.Plane, "Aabb3 x Plane3",
                    (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                    (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                    (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] q, t), q), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] q), q),
                    (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] q, t), q), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] q), q)),
                    p.Name, p.Collides, t => (aabbBody.CollidesWith(p.Plane, t), p.Plane.CollidesWith(aabbBody, t), aabbBody.GetIntersections(p.Plane, t), p.Plane.GetIntersections(aabbBody, t))));
                rows.Add(BoxRow(Both(obb, p.Plane, "Obb3 x Plane3",
                    (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                    (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                    (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] q, t), q), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] q), q),
                    (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] q, t), q), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] q), q)),
                    p.Name, p.Collides, t => (obbBody.CollidesWith(p.Plane, t), p.Plane.CollidesWith(obbBody, t), obbBody.GetIntersections(p.Plane, t), p.Plane.GetIntersections(obbBody, t))));
            }
        }

        private static Row BoxRow(Row row, string name, bool collides, Func<Tolerance, (bool?, bool?, GeoPoint3[], GeoPoint3[])> body)
        {
            row.Name = row.Pair + ": " + name;
            row.Collides = collides;
            row.Body = body;

            // A face with a hole touching a body at a corner: GeoSolid3.CollidesWith(GeoFace3) says false at the pin.
            row.BodyBreaks = row.Pair.EndsWith("Face3", StringComparison.Ordinal) && name == "touching the top at a corner";
            return row;
        }

        #endregion

        #region An edge against the chains

        private static void AddEdges(List<Row> rows)
        {
            var upright = new GeoEdge3(Pt(0, -20, 0), Pt(0, 20, 0));
            var lifted = new GeoEdge3(Pt(0, -20, 5), Pt(0, 20, 5));
            // Radius 10 about the origin, through (0, 10, 0).
            var bow = new GeoEdge3(Pt(10, 0, 0), Pt(-10, 0, 0), 1.0, Up);

            var u = new GeoPolyline3(Pt(-10, -5, 0), Pt(10, -5, 0), Pt(10, 5, 0), Pt(-10, 5, 0));
            var bar = new GeoPolyline3(Pt(-20, 5, 0), Pt(20, 5, 0), Pt(20, 30, 0));
            // Along x from -30 to -10, then bent through (0, -10, 0) to (10, 0, 0).
            var hook = new GeoPolylineArc3(new[] { Pt(-30, 0, 0), Pt(-10, 0, 0), Pt(10, 0, 0) }, new[] { 0.0, 1.0 }, new[] { Up, Up });
            var stadium = new GeoPolygonArc3(new[] { Pt(-10, -5, 0), Pt(10, -5, 0), Pt(10, 5, 0), Pt(-10, 5, 0) }, new[] { 0.0, 1.0, 0.0, 1.0 }, new[] { Up, Up, Up, Up });

            EdgeChain(rows, "crossing its two sides", upright, u, Pt(0, -5, 0), Pt(0, 5, 0));
            EdgeChain(rows, "5 above it", lifted, u);
            EdgeChain(rows, "touching its last corner", new GeoEdge3(Pt(-10, 5, 0), Pt(-10, 20, 0)), u, Pt(-10, 5, 0));
            // y = 5 meets the circle of radius 10 at x = sqrt(75) either side.
            EdgeChain(rows, "a bend crossing it", bow, bar, Pt(-Math.Sqrt(75), 5, 0), Pt(Math.Sqrt(75), 5, 0));
            EdgeChain(rows, "a bend 5 below it", bow, new GeoPolyline3(Pt(-20, 5, -5), Pt(20, 5, -5)));

            EdgeChain(rows, "crossing its bend", upright, hook, Pt(0, -10, 0));
            EdgeChain(rows, "5 above it", lifted, hook);
            EdgeChain(rows, "crossing its straight run", new GeoEdge3(Pt(-20, -5, 0), Pt(-20, 5, 0)), hook, Pt(-20, 0, 0));
            EdgeChain(rows, "a bend 5 above it", new GeoEdge3(Pt(10, 0, 5), Pt(-10, 0, 5), 1.0, Up), hook);

            EdgeChain(rows, "crossing its two straight sides", upright, stadium, Pt(0, -5, 0), Pt(0, 5, 0));
            EdgeChain(rows, "5 above it", lifted, stadium);
            EdgeChain(rows, "a bend crossing it", bow, stadium, null);
        }

        private static void EdgeChain(List<Row> rows, string name, GeoEdge3 edge, GeoPolyline3 chain, params GeoPoint3[] points)
        {
            Row row = Both(edge, chain, "Edge3 x Polyline3",
                (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p));
            EdgeRow(rows, row, name, points, t => edge.IsArc ? (edge.ToArc().CollidesWith(chain, t), edge.ToArc().GetIntersections(chain, t)) : (edge.ToLine().CollidesWith(chain, t), edge.ToLine().GetIntersections(chain, t)));
        }

        private static void EdgeChain(List<Row> rows, string name, GeoEdge3 edge, GeoPolylineArc3 chain, params GeoPoint3[] points)
        {
            Row row = Both(edge, chain, "Edge3 x PolylineArc3",
                (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p));
            EdgeRow(rows, row, name, points, t => edge.IsArc ? (edge.ToArc().CollidesWith(chain, t), edge.ToArc().GetIntersections(chain, t)) : (edge.ToLine().CollidesWith(chain, t), edge.ToLine().GetIntersections(chain, t)));
        }

        private static void EdgeChain(List<Row> rows, string name, GeoEdge3 edge, GeoPolygonArc3 chain, params GeoPoint3[] points)
        {
            Row row = Both(edge, chain, "Edge3 x PolygonArc3",
                (x, y, t) => x.CollidesWith(y, t), (x, y) => x.CollidesWith(y), (x, y, t) => y.CollidesWith(x, t), (x, y) => y.CollidesWith(x),
                (x, y, t) => x.GetIntersections(y, t), (x, y) => x.GetIntersections(y), (x, y, t) => y.GetIntersections(x, t), (x, y) => y.GetIntersections(x),
                (x, y, t) => (x.TryIntersectWith(y, out GeoPoint3[] p, t), p), (x, y) => (x.TryIntersectWith(y, out GeoPoint3[] p), p),
                (x, y, t) => (y.TryIntersectWith(x, out GeoPoint3[] p, t), p), (x, y) => (y.TryIntersectWith(x, out GeoPoint3[] p), p));
            EdgeRow(rows, row, name, points, t => edge.IsArc ? (edge.ToArc().CollidesWith(chain, t), edge.ToArc().GetIntersections(chain, t)) : (edge.ToLine().CollidesWith(chain, t), edge.ToLine().GetIntersections(chain, t)));
        }

        private static void EdgeRow(List<Row> rows, Row row, string name, GeoPoint3[] points, Func<Tolerance, (bool, GeoPoint3[])> kind)
        {
            row.Name = row.Pair + ": " + name;
            row.Kind = kind;

            if (points != null)
            {
                row.Collides = points.Length > 0;
                row.Points = points;
                row.Order = Order.Set;
            }
            else
            {
                // The bend meets the stadium's straight top at x = sqrt(75) either side; where it meets the rounded
                // ends is the kind's to say, so only whether is worked by hand.
                row.Collides = true;
            }

            rows.Add(row);
        }

        #endregion

        #endregion

        #region Rows

        private enum Order { Set, Same, AskedFirst, EitherFirst, PlaneHalf }

        private sealed class Row
        {
            internal string Name;
            internal string Pair;
            internal bool Collides;
            internal GeoPoint3[] Points;
            internal GeoPoint3[] PointsArgumentFirst;
            internal Order Order;
            internal bool Band;

            /// <summary>The body's own answer is wrong here (BodyCollisionBreakTests), so the box's is held to it alone.</summary>
            internal bool BodyBreaks;

            internal Func<Tolerance, bool> Collide;
            internal Func<Tolerance, bool> CollideBack;
            internal Func<bool> PlainCollide;
            internal Func<bool> PlainCollideBack;
            internal Func<Tolerance, GeoPoint3[]> Cross;
            internal Func<Tolerance, GeoPoint3[]> CrossBack;
            internal Func<GeoPoint3[]> PlainCross;
            internal Func<GeoPoint3[]> PlainCrossBack;
            internal Func<Tolerance, (bool, GeoPoint3[])> Try;
            internal Func<Tolerance, (bool, GeoPoint3[])> TryBack;
            internal Func<(bool, GeoPoint3[])> PlainTry;
            internal Func<(bool, GeoPoint3[])> PlainTryBack;

            internal Func<bool> SampledMeet;
            internal Func<GeoPoint3, bool> OnBoth;
            internal Func<GeoPoint3[]> Subdivided;
            internal Func<GeoPoint3[][]> PlaneHalfOrders;
            internal Func<GeoPoint3[][]> PlaneHalfOrdersBack;
            internal Func<(bool, GeoPoint3[])> PlaneHalfMeets;
            internal Func<bool> SlabMeets;
            internal Func<Tolerance, (bool?, bool?, GeoPoint3[], GeoPoint3[])> Body;
            internal Func<Tolerance, (bool, GeoPoint3[])> Kind;
        }

        /// <summary>A row asking <paramref name="a"/> about <paramref name="b"/> and back; a question left null is not new for the pair.</summary>
        private static Row Both<TA, TB>(TA a, TB b, string pair,
            Func<TA, TB, Tolerance, bool> collide, Func<TA, TB, bool> plainCollide, Func<TA, TB, Tolerance, bool> collideBack, Func<TA, TB, bool> plainCollideBack,
            Func<TA, TB, Tolerance, GeoPoint3[]> cross, Func<TA, TB, GeoPoint3[]> plainCross, Func<TA, TB, Tolerance, GeoPoint3[]> crossBack, Func<TA, TB, GeoPoint3[]> plainCrossBack,
            Func<TA, TB, Tolerance, (bool, GeoPoint3[])> any, Func<TA, TB, (bool, GeoPoint3[])> plainAny,
            Func<TA, TB, Tolerance, (bool, GeoPoint3[])> anyBack, Func<TA, TB, (bool, GeoPoint3[])> plainAnyBack)
        {
            return new Row
            {
                Pair = pair,
                Collide = collide == null ? null : (Func<Tolerance, bool>)(t => collide(a, b, t)),
                PlainCollide = plainCollide == null ? null : (Func<bool>)(() => plainCollide(a, b)),
                CollideBack = collideBack == null ? null : (Func<Tolerance, bool>)(t => collideBack(a, b, t)),
                PlainCollideBack = plainCollideBack == null ? null : (Func<bool>)(() => plainCollideBack(a, b)),
                Cross = cross == null ? null : (Func<Tolerance, GeoPoint3[]>)(t => cross(a, b, t)),
                PlainCross = plainCross == null ? null : (Func<GeoPoint3[]>)(() => plainCross(a, b)),
                CrossBack = crossBack == null ? null : (Func<Tolerance, GeoPoint3[]>)(t => crossBack(a, b, t)),
                PlainCrossBack = plainCrossBack == null ? null : (Func<GeoPoint3[]>)(() => plainCrossBack(a, b)),
                Try = any == null ? null : (Func<Tolerance, (bool, GeoPoint3[])>)(t => any(a, b, t)),
                PlainTry = plainAny == null ? null : (Func<(bool, GeoPoint3[])>)(() => plainAny(a, b)),
                TryBack = anyBack == null ? null : (Func<Tolerance, (bool, GeoPoint3[])>)(t => anyBack(a, b, t)),
                PlainTryBack = plainAnyBack == null ? null : (Func<(bool, GeoPoint3[])>)(() => plainAnyBack(a, b))
            };
        }

        private static Row Find(string name) => Table.Value.Single(r => r.Name == name);

        private static TheoryData<string> Names(Func<Row, bool> which)
        {
            var data = new TheoryData<string>();

            foreach (Row row in Table.Value.Where(which))
            {
                data.Add(row.Name);
            }

            return data;
        }

        private static void InOrder(GeoPoint3[] expected, GeoPoint3[] actual, double near)
        {
            Assert.True(expected.Length == actual.Length, "expected " + Show(expected) + ", got " + Show(actual));

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(expected[i].DistanceTo(actual[i]) <= near, "expected " + Show(expected) + ", got " + Show(actual));
            }
        }

        private static void InOneOf(GeoPoint3[][] orders, GeoPoint3[] actual, double near)
        {
            bool any = orders.Any(o => o.Length == actual.Length && o.Zip(actual, (e, a) => e.DistanceTo(a) <= near).All(x => x));

            Assert.True(any, "expected one of " + string.Join(" or ", orders.Select(Show)) + ", got " + Show(actual));
        }

        private static void AsSet(GeoPoint3[] expected, GeoPoint3[] actual, double near)
        {
            bool same = expected.Length == actual.Length
                && expected.All(e => actual.Any(a => e.DistanceTo(a) <= near))
                && actual.All(a => expected.Any(e => e.DistanceTo(a) <= near));

            Assert.True(same, "expected " + Show(expected) + " in any order, got " + Show(actual));
        }

        private static string Show(GeoPoint3[] points)
            => "[" + string.Join(", ", points.Select(p => "(" + p.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ", "
                + p.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ", " + p.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ")")) + "]";

        #endregion
    }
}

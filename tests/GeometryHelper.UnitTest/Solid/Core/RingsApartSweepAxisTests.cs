using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Before a face is meshed as its surface, a sweep of the edges of its rings along the axis of the face's frame that
    /// leaves the fewest pairs checks that no two come within the point tolerance; the faces come out meshed as they did
    /// when the sweep ran along X, to the last bit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A face of the plane z = 0 has the X of its frame along the world's Y, so a plate with its slots along the world's
    /// Y has every long edge overlapping every other along the frame's X; swapped, X with Y in every corner, the long
    /// edges lie along the frame's Y. The slots are 2 wide and 4 apart, 1 000 long; the comb is one ring of 60 teeth 1 000
    /// long and 5 wide, 5 apart. Rings that come within the tolerance are refused by the check and the face is cut into
    /// strips instead, so the triangles show what it found. The hashes are of the triangles at 3a2da08, where the sweep
    /// ran along X.
    /// </para>
    /// </remarks>
    public class RingsApartSweepAxisTests
    {
        private static readonly Tolerance Within = Tolerance.Default;

        private const double SlotLength = 1000.0;

        /// <summary>
        /// A corner of a face, X and Y swapped when asked, on the plane z = 0 or on z = x / 4 + y / 2.
        /// </summary>
        private static GeoPoint3 At(double x, double y, bool swapped, bool sloping)
        {
            double u = swapped ? y : x, v = swapped ? x : y;
            return new GeoPoint3(u, v, sloping ? 0.25 * u + 0.5 * v : 0.0);
        }

        private static GeoPolygon3 Ring(bool swapped, bool sloping, params (double X, double Y)[] corners)
            => new GeoPolygon3(corners.Select(c => At(c.X, c.Y, swapped, sloping)), Within);

        /// <summary>
        /// A plate with 40 slots, the tenth moved along by a shift towards the eleventh, which has 4 between them
        /// unmoved, and the last a gap from the plate's right edge.
        /// </summary>
        private static GeoFace3 Slotted(bool swapped, bool sloping, double shift, double lastGap)
        {
            const int slots = 40;
            double width = 10.0 + 6.0 * (slots - 1) + 2.0 + lastGap;
            GeoPolygon3 boundary = Ring(swapped, sloping, (0, 0), (width, 0), (width, SlotLength + 20), (0, SlotLength + 20));
            var holes = new List<GeoPolygon3>();

            for (int h = 0; h < slots; h++)
            {
                double x = 10.0 + 6.0 * h + (h == 9 ? shift : 0.0);
                holes.Add(Ring(swapped, sloping, (x, 10), (x + 2, 10), (x + 2, 10 + SlotLength), (x, 10 + SlotLength)));
            }

            return new GeoFace3(boundary, holes, Within);
        }

        /// <summary>
        /// The plate with two triangular holes between its slots, meeting at a corner, or a gap apart.
        /// </summary>
        private static GeoFace3 TrianglesMeeting(bool swapped, bool sloping, double gap)
        {
            GeoFace3 plate = Slotted(swapped, sloping, 0.0, 10.0);
            var holes = plate.Holes.ToList();
            double y = SlotLength + 15.0;
            holes.Add(Ring(swapped, sloping, (100, y - 3), (110, y - 3), (105, y)));
            holes.Add(Ring(swapped, sloping, (105, y + gap), (110, y + 3 + gap), (100, y + 3 + gap)));

            double width = 10.0 + 6.0 * 39 + 2.0 + 10.0;
            GeoPolygon3 boundary = Ring(swapped, sloping, (0, 0), (width, 0), (width, SlotLength + 30), (0, SlotLength + 30));

            return new GeoFace3(boundary, holes, Within);
        }

        /// <summary>
        /// One ring of 60 teeth, counter-clockwise from above, the thirtieth pinched at its middle to a width, and the
        /// fortieth ending in a spike 500 long, whose two edges come within the tolerance of each other only where they meet
        /// at its tip.
        /// </summary>
        private static GeoFace3 Comb(bool swapped, bool sloping, double pinch)
        {
            const int teeth = 60;
            var corners = new List<(double, double)> { (0.0, 0.0), (10.0 * (teeth - 1) + 5.0, 0.0) };

            for (int i = teeth - 1; i >= 0; i--)
            {
                double left = 10.0 * i, right = left + 5.0;

                if (i == 29)
                {
                    double middle = 0.5 * (left + right);
                    corners.Add((right, 500.0));
                    corners.Add((middle + 0.5 * pinch, 510.0));
                    corners.Add((right, 520.0));
                    corners.Add((right, SlotLength));
                    corners.Add((left, SlotLength));
                    corners.Add((left, 520.0));
                    corners.Add((middle - 0.5 * pinch, 510.0));
                    corners.Add((left, 500.0));
                }
                else if (i == 39)
                {
                    corners.Add((right, SlotLength));
                    corners.Add((left + 2.5, SlotLength + 500.0));
                    corners.Add((left, SlotLength));
                }
                else
                {
                    corners.Add((right, SlotLength));
                    corners.Add((left, SlotLength));
                }

                if (i > 0)
                {
                    corners.Add((left, 20.0));
                    corners.Add((left - 5.0, 20.0));
                }
            }

            return new GeoFace3(Ring(swapped, sloping, corners.ToArray()), null, Within);
        }

        /// <summary>
        /// Faces about points scattered over the plane from a fixed seed, of 8 to 200 corners, stretched along x; half of
        /// them with up to 12 square holes 5 across in a row along x, a few of those a whisker from the next, either side
        /// of the tolerance.
        /// </summary>
        private static List<GeoFace3> Scattered(bool swapped, bool sloping)
        {
            var random = new Random(4000);
            var faces = new List<GeoFace3>();

            for (int k = 0; k < 60; k++)
            {
                int count = 8 + random.Next(193);
                var corners = new (double X, double Y)[count];

                for (int i = 0; i < count; i++)
                {
                    double s = (i + 0.2 + 0.6 * random.NextDouble()) * 8.0 / count;
                    double r = 60.0 + 40.0 * random.NextDouble();
                    double u = s < 2.0 ? 1.0 : s < 4.0 ? 3.0 - s : s < 6.0 ? -1.0 : s - 7.0;
                    double v = s < 2.0 ? s - 1.0 : s < 4.0 ? 1.0 : s < 6.0 ? 5.0 - s : -1.0;
                    corners[i] = (20.0 * r * u, r * v);
                }

                var holes = new List<GeoPolygon3>();

                if (random.Next(2) == 0)
                {
                    int holeCount = 1 + random.Next(12);
                    double x = -60.0 * holeCount * 0.5;

                    for (int h = 0; h < holeCount; h++)
                    {
                        holes.Add(Ring(swapped, sloping, (x, -2.5), (x + 5, -2.5), (x + 5, 2.5), (x, 2.5)));
                        int pick = random.Next(6);
                        x += 5.0 + (pick == 0 ? 0.0006 : pick == 1 ? 0.0014 : 55.0);
                    }
                }

                faces.Add(new GeoFace3(Ring(swapped, sloping, corners), holes, Within));
            }

            return faces;
        }

        private static string Hash(IEnumerable<GeoFace3> faces)
        {
            var text = new StringBuilder();

            foreach (GeoFace3 face in faces)
            {
                SweepAxisText.Append(text, face.TriangulateSurface(Within));
            }

            return SweepAxisText.Hash(text);
        }

        private static IEnumerable<GeoFace3> Plates(bool swapped, bool sloping)
        {
            yield return Slotted(swapped, sloping, 0.0, 10.0);

            // The tenth slot 0.0005 from the eleventh, within the tolerance, and 0.0015, clear of it.
            yield return Slotted(swapped, sloping, 1.9995, 10.0);
            yield return Slotted(swapped, sloping, 1.9985, 10.0);

            // The last slot 0.0005 from the plate's edge, and 0.0015.
            yield return Slotted(swapped, sloping, 0.0, 0.0005);
            yield return Slotted(swapped, sloping, 0.0, 0.0015);

            yield return TrianglesMeeting(swapped, sloping, 0.0);
            yield return TrianglesMeeting(swapped, sloping, 0.0015);
        }

        [Theory]
        [InlineData(false, false, "12fba5e10a5e1d74631b1a507b44edc3677d615a90d9c01a4395a4bf714945d9")]
        [InlineData(true, false, "032f765c163eef8828aab3fa604ee09b6827e89987d0e300e75a5eda0d830f98")]
        [InlineData(false, true, "018c46140d810a67be6aed03c446edc2f529d9d060a20e9b038b796698bf2983")]
        [InlineData(true, true, "bfc7dd4af734cfe1c9f86a3f9ede02d9a57cad8c2318aa81e820f5f7c09acfe1")]
        public void PlatesWithSlotsTouchingAndClear_AreMeshedAsBeforeTheAxisWasChosen(bool swapped, bool sloping, string expected)
        {
            Assert.Equal(expected, Hash(Plates(swapped, sloping)));
        }

        [Theory]
        [InlineData(false, false, "5f8625c7f341c7c56e6aa84431924c5cd18f19d3b6b5fa58d6be554727531f9e")]
        [InlineData(true, false, "5e6236983c4152fa4730d9b59cb6564c721d64bbf2e4fe0f16149614ceddabe8")]
        [InlineData(false, true, "a416e7c4443262b935515f25ac1f576377c72ce4243771ef885aac10906694e1")]
        [InlineData(true, true, "0f98cca8ed0389b6ead7db9123efebcdd79d0d7ca12f480c1b81feb9f55f12d9")]
        public void CombsPinchedAndSpiked_AreMeshedAsBeforeTheAxisWasChosen(bool swapped, bool sloping, string expected)
        {
            Assert.Equal(expected, Hash(new[] { Comb(swapped, sloping, 2.0), Comb(swapped, sloping, 0.0005), Comb(swapped, sloping, 0.0015) }));
        }

        [Theory]
        [InlineData(false, false, "81cb73d603dad3e34f2c74b6d3890d636c347581db4dbb8ed3d650b5535b9b45")]
        [InlineData(true, false, "cc3ee8a7bfc58a9ab758f472ec485fa773e8c8a32daf2b1b74a78fa6af588df8")]
        [InlineData(false, true, "ca6b6e4c290e4780572f533371ec4d0b0cdc4d79c0fb99fefdfd5b2ff3c9015a")]
        [InlineData(true, true, "9761c6ae5728b4557901f8caa6d3ce2a820793b4b3e7251d53cdf97e1634220f")]
        public void ScatteredFacesWithHoles_AreMeshedAsBeforeTheAxisWasChosen(bool swapped, bool sloping, string expected)
        {
            Assert.Equal(expected, Hash(Scattered(swapped, sloping)));
        }

        private static EarClipping.Node Node(double x, double y, bool swapped)
            => swapped ? new EarClipping.Node(y, x, new GeoPoint3(y, x, 0)) : new EarClipping.Node(x, y, new GeoPoint3(x, y, 0));

        private static List<EarClipping.Node> Loop(bool swapped, params (double X, double Y)[] corners)
            => corners.Select(c => Node(c.X, c.Y, swapped)).ToList();

        /// <summary>
        /// The edges of the rings as the check takes them: every edge of each ring, in the order of the rings, then
        /// sorted by where they start along X.
        /// </summary>
        private static List<EarClipping.RingEdge> Edges(List<EarClipping.Node> outer, List<List<EarClipping.Node>> holes)
        {
            var edges = new List<EarClipping.RingEdge>();
            var rings = new List<List<EarClipping.Node>> { outer };
            rings.AddRange(holes);

            for (int r = 0; r < rings.Count; r++)
            {
                List<EarClipping.Node> ring = rings[r];

                for (int i = 0; i < ring.Count; i++)
                {
                    EarClipping.Node a = ring[i], b = ring[(i + 1) % ring.Count];
                    edges.Add(new EarClipping.RingEdge { A = a, B = b, Ring = r, Index = i, Count = ring.Count, MinX = Math.Min(a.X, b.X), MaxX = Math.Max(a.X, b.X) });
                }
            }

            edges.Sort((left, right) => left.MinX.CompareTo(right.MinX));
            return edges;
        }

        /// <summary>
        /// Whether two edges of the rings come within a distance of each other, every pair tried as the sweep left to
        /// right tried them: the later of the two in its order taken as inside the other's span across or not.
        /// </summary>
        private static bool EveryPair(List<EarClipping.Node> outer, List<List<EarClipping.Node>> holes, double distance)
        {
            List<EarClipping.RingEdge> edges = Edges(outer, holes);

            for (int i = 0; i < edges.Count; i++)
            {
                for (int j = i + 1; j < edges.Count; j++)
                {
                    EarClipping.RingEdge e = edges[i], f = edges[j];

                    if (f.MinX > e.MaxX + distance)
                    {
                        continue;
                    }

                    double minY = Math.Min(e.A.Y, e.B.Y) - distance, maxY = Math.Max(e.A.Y, e.B.Y) + distance;

                    if (Math.Max(f.A.Y, f.B.Y) < minY || Math.Min(f.A.Y, f.B.Y) > maxY)
                    {
                        continue;
                    }

                    if (e.Ring == f.Ring && (Math.Abs(e.Index - f.Index) == 1 || Math.Abs(e.Index - f.Index) == e.Count - 1))
                    {
                        continue;
                    }

                    if (EarClipping.SegmentsWithin(e.A, e.B, f.A, f.B, distance))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// A plate 1 020 long along X with 20 slots along it, 2 wide and 4 apart, X and Y swapped when asked; the tenth
        /// slot moved up towards the eleventh by a shift, and the last a gap from the plate's top edge.
        /// </summary>
        private static (List<EarClipping.Node> Outer, List<List<EarClipping.Node>> Holes) SlotsAlongX(bool swapped, double shift, double lastGap)
        {
            const int slots = 20;
            double height = 10.0 + 6.0 * (slots - 1) + 2.0 + lastGap;
            List<EarClipping.Node> outer = Loop(swapped, (0, 0), (1020, 0), (1020, height), (0, height));
            var holes = new List<List<EarClipping.Node>>();

            for (int h = 0; h < slots; h++)
            {
                double y = 10.0 + 6.0 * h + (h == 9 ? shift : 0.0);
                holes.Add(Loop(swapped, (10, y), (1010, y), (1010, y + 2), (10, y + 2)));
            }

            return (outer, holes);
        }

        [Theory]
        [InlineData(false, 1)]
        [InlineData(true, 0)]
        public void TheRingsAreSweptAlongTheAxisTheSlotsStandSideBySideAlong(bool swapped, int expected)
        {
            (List<EarClipping.Node> outer, List<List<EarClipping.Node>> holes) = SlotsAlongX(swapped, 0.0, 10.0);

            Assert.Equal(expected, EarClipping.SweepAxis(Edges(outer, holes), Within.EqualPoint));
        }

        [Fact]
        public void ASquareIsSweptAlongX_WhereYLeavesAsManyPairs()
        {
            List<EarClipping.Node> square = Loop(false, (0, 0), (10, 0), (10, 10), (0, 10));

            Assert.Equal(0, EarClipping.SweepAxis(Edges(square, new List<List<EarClipping.Node>>()), Within.EqualPoint));
        }

        [Theory]
        [InlineData(false, 0.0, 10.0, false)]
        [InlineData(true, 0.0, 10.0, false)]
        [InlineData(false, 3.9995, 10.0, true)]
        [InlineData(true, 3.9995, 10.0, true)]
        [InlineData(false, 3.9985, 10.0, false)]
        [InlineData(true, 3.9985, 10.0, false)]
        [InlineData(false, 0.0, 0.0005, true)]
        [InlineData(true, 0.0, 0.0005, true)]
        [InlineData(false, 0.0, 0.0015, false)]
        [InlineData(true, 0.0, 0.0015, false)]
        [InlineData(false, 4.0, 10.0, true)]
        [InlineData(true, 4.0, 10.0, true)]
        public void SlotsTouchingEachOtherOrThePlate_MeetEitherWayRound(bool swapped, double shift, double lastGap, bool meet)
        {
            (List<EarClipping.Node> outer, List<List<EarClipping.Node>> holes) = SlotsAlongX(swapped, shift, lastGap);

            Assert.Equal(meet, EarClipping.RingsMeet(outer, holes, Within.EqualPoint));
            Assert.Equal(meet, EveryPair(outer, holes, Within.EqualPoint));
        }

        /// <summary>
        /// One ring of 20 teeth 1 000 long along X, 5 wide and 5 apart, the tenth pinched in its middle to a width, and
        /// the fifteenth ending in a spike 500 long, whose two edges come within the tolerance of each other only where
        /// they meet at its tip.
        /// </summary>
        private static List<EarClipping.Node> TeethAlongX(bool swapped, double pinch)
        {
            var corners = new List<(double, double)> { (0.0, 0.0) };

            for (int i = 0; i < 20; i++)
            {
                double bottom = 10.0 * i, top = bottom + 5.0, middle = bottom + 2.5;

                if (i == 9)
                {
                    corners.AddRange(new[] { (500.0, bottom), (510.0, middle - 0.5 * pinch), (520.0, bottom), (1000.0, bottom), (1000.0, top), (520.0, top), (510.0, middle + 0.5 * pinch), (500.0, top) });
                }
                else if (i == 14)
                {
                    corners.AddRange(new[] { (1000.0, bottom), (1500.0, middle), (1000.0, top) });
                }
                else
                {
                    corners.AddRange(new[] { (1000.0, bottom), (1000.0, top) });
                }

                if (i < 19)
                {
                    corners.Add((20.0, top));
                    corners.Add((20.0, bottom + 10.0));
                }
            }

            corners.Add((0.0, 195.0));
            return Loop(swapped, corners.ToArray());
        }

        [Theory]
        [InlineData(false, 2.0, false)]
        [InlineData(true, 2.0, false)]
        [InlineData(false, 0.0005, true)]
        [InlineData(true, 0.0005, true)]
        [InlineData(false, 0.0015, false)]
        [InlineData(true, 0.0015, false)]
        public void ATeethRingPinchedOrSpiked_MeetsItselfOnlyAtThePinch(bool swapped, double pinch, bool meet)
        {
            List<EarClipping.Node> ring = TeethAlongX(swapped, pinch);
            var holes = new List<List<EarClipping.Node>>();

            Assert.Equal(swapped ? 0 : 1, EarClipping.SweepAxis(Edges(ring, holes), Within.EqualPoint));
            Assert.Equal(meet, EarClipping.RingsMeet(ring, holes, Within.EqualPoint));
            Assert.Equal(meet, EveryPair(ring, holes, Within.EqualPoint));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RandomSlottedPlates_MeetAsEveryPairTried(bool swapped)
        {
            var random = new Random(6000);
            int met = 0;

            for (int k = 0; k < 200; k++)
            {
                // 10 to 40 slots along X of lengths and widths of their own; now and then a gap of nothing, the
                // tolerance or twice it, and the plate's top edge as near the last slot.
                int slots = 10 + random.Next(31);
                var holes = new List<List<EarClipping.Node>>();
                double y = 1.0 + random.Next(3) * 0.0005;

                for (int h = 0; h < slots; h++)
                {
                    double left = 10.0 + random.Next(50), right = 900.0 + random.Next(100), width = 1.0 + random.Next(3);
                    holes.Add(Loop(swapped, (left, y), (right, y), (right, y + width), (left, y + width)));
                    y += width + (random.Next(15) == 0 ? random.Next(3) * 0.001 : 3.0);
                }

                double top = y - 3.0 + 1.0 + random.Next(3) * 0.0005;
                List<EarClipping.Node> outer = Loop(swapped, (0, 0), (1020, 0), (1020, top), (0, top));

                bool expected = EveryPair(outer, holes, Within.EqualPoint);
                Assert.Equal(expected, EarClipping.RingsMeet(outer, holes, Within.EqualPoint));
                met += expected ? 1 : 0;
            }

            // Both answers come up often.
            Assert.InRange(met, 40, 160);
        }
    }
}

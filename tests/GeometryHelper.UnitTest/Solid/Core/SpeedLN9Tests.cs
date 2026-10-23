using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Work that grew with the square of its size and no longer does, each held to a time several times what it takes now and
    /// well under what it took: a box fitted round points on a sphere, a comb face cut across its teeth, and a comb in a plane
    /// in space offset along its teeth.
    /// </summary>
    public class SpeedLN9Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        /// <summary>
        /// A comb in the XY plane turned into space: a back 1 000 long, and teeth 900 long and 3 wide, 2 apart, running along
        /// its first edge.
        /// </summary>
        private static List<GeoPoint3> Comb(int teeth, bool startOnTheBack)
        {
            var points = new List<GeoPoint2> { new GeoPoint2(0, 0), new GeoPoint2(1000, 0) };
            double y = 0;

            for (int i = 0; i < teeth; i++)
            {
                points.Add(new GeoPoint2(1000, y + 3));
                points.Add(new GeoPoint2(100, y + 3));
                points.Add(new GeoPoint2(100, y + 5));
                y += 5;
            }

            points.Add(new GeoPoint2(1000, y));
            points.Add(new GeoPoint2(1000, y + 3));
            points.Add(new GeoPoint2(0, y + 3));

            if (!startOnTheBack)
            {
                // The same loop started on its short side, so that its frame's X runs across the teeth.
                points.Insert(0, points[points.Count - 1]);
                points.RemoveAt(points.Count - 1);
            }

            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(5, 7, 11), new GeoVector3(1, 2, 3), 0.3);
            return points.Select(p => new GeoPoint3(p.X, p.Y, 0).TransformBy(turn)).ToList();
        }

        [Fact]
        public void ABoxFittedRound3000PointsOnASphere_HoldsThemInUnderFiveSeconds()
        {
            // Each of the hull's 6 000 faces sorted the 18 000 copies of its corners: 15 seconds. The corners are taken once,
            // and a face that cannot stand a box as small as one found is passed over.
            var random = new Random(1);
            var points = new List<GeoPoint3>();

            while (points.Count < 3000)
            {
                double x = random.NextDouble() * 2 - 1, y = random.NextDouble() * 2 - 1, z = random.NextDouble() * 2 - 1;
                double length = Math.Sqrt(x * x + y * y + z * z);

                if (length >= 0.1 && length <= 1)
                {
                    points.Add(new GeoPoint3(100 * x / length + 3, 100 * y / length - 7, 100 * z / length + 11));
                }
            }

            var watch = Stopwatch.StartNew();
            GeoObb3 box = GeoObb3.Fit(points, Thousandth);
            watch.Stop();

            Assert.All(points, point => Assert.True(box.Contains(point, Thousandth)));
            Assert.InRange(box.Volume, 4.0 / 3.0 * Math.PI * 1E6, 8E6);
            Assert.True(watch.Elapsed.TotalSeconds < 5, watch.Elapsed.ToString());
        }

        [Fact]
        public void ACombFaceOf5000TeethCutAcrossThem_ComesApartInUnderTwoSeconds()
        {
            // The 5 001 pieces on one side were each tried against every other for which lay inside which: 7 seconds.
            var face = new GeoFace3(new GeoPolygon3(Comb(5000, true)));
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(5, 7, 11), new GeoVector3(1, 2, 3), 0.3);
            var cutter = new GeoPlane3(new GeoPoint3(500, 0, 0).TransformBy(turn), GeoVector3.XAxis.TransformBy(turn));

            var watch = Stopwatch.StartNew();
            Assert.True(Splition3.TrySplitBy(face, cutter, out GeoFace3[] above, out GeoFace3[] below, Thousandth));
            watch.Stop();

            Assert.Equal(5001, above.Length);
            Assert.Single(below);
            Assert.InRange(above.Sum(piece => piece.Area) + below[0].Area, face.Area * (1 - 1E-12), face.Area * (1 + 1E-12));
            Assert.True(watch.Elapsed.TotalSeconds < 2, watch.Elapsed.ToString());
        }

        [Fact]
        public void ACombInSpaceOf4000TeethAlongItsFrame_IsOffsetInUnderTwoSeconds()
        {
            // The region solver swept the teeth along their length, where every edge overlaps every other: 4.8 seconds. Swept
            // across them, it finds the same crossings in the same order.
            var along = new GeoPolygon3(Comb(4000, true));
            var across = new GeoPolygon3(Comb(4000, false));

            var watch = Stopwatch.StartNew();
            GeoPolygon3[] offset = Offset3.Offset(along, 0.5, OffsetOptions.Default, Thousandth);
            watch.Stop();

            GeoPolygon3[] turned = Offset3.Offset(across, 0.5, OffsetOptions.Default, Thousandth);

            Assert.Single(offset);
            Assert.Single(turned);
            Assert.InRange(offset[0].Area, turned[0].Area * (1 - 1E-12), turned[0].Area * (1 + 1E-12));
            Assert.True(watch.Elapsed.TotalSeconds < 2, watch.Elapsed.ToString());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Clash;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Checking many parts at once finds what checking every pair by hand finds, and says what each clash is.
    /// </summary>
    public class Clash3Tests
    {
        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        /// <summary>
        /// A beam through two plates, a column on a footing, a pin through a bolt hole and two blocks three
        /// apart, each group well away from the others.
        /// </summary>
        private static List<GeoSolid3> Model()
        {
            Assert.True(Box(0, 0, 0, 30, 100, 30).TryUnion(Box(70, 0, 0, 100, 100, 30), out GeoSolid3 plates));

            return new List<GeoSolid3>
            {
                plates,                                                                         // 0
                Box(-10, 40, 10, 110, 60, 20),                                                  // 1: the beam
                Box(1000, 0, 0, 1200, 200, 50),                                                 // 2: the footing
                Box(1050, 50, 50, 1150, 150, 500),                                              // 3: the column
                Box(2000, 0, 0, 2100, 100, 20).WithOpenings(new[] { Box(2040, 40, -1, 2060, 60, 21) }), // 4
                Box(2045, 45, -50, 2055, 55, 50),                                               // 5: the pin
                Box(3000, 0, 0, 3010, 10, 10),                                                  // 6
                Box(3013, 0, 0, 3023, 10, 10),                                                  // 7
            };
        }

        [Fact]
        public void TheBeamClashesTwiceAndTheColumnBears()
        {
            ClashResult[] found = Clash3.Find(Model());

            Assert.Equal(2, found.Length);

            ClashResult beam = found[0];
            Assert.Equal((0, 1), (beam.First, beam.Second));
            Assert.Equal(ClashKind.Hard, beam.Kind);
            Assert.Equal(2, beam.Overlaps.Count);
            Assert.Equal(2 * 30.0 * 20 * 10, beam.Volume, 6);
            Assert.True(beam.Location.DistanceTo(new GeoPoint3(50, 50, 15)) < 1E-6);

            ClashResult column = found[1];
            Assert.Equal((2, 3), (column.First, column.Second));
            Assert.Equal(ClashKind.Touch, column.Kind);
            Assert.Equal(100.0 * 100, column.ContactArea, 6);
            Assert.True(column.Location.DistanceTo(new GeoPoint3(1100, 100, 50)) < 1E-6);
            Assert.Equal(0.0, column.Volume);
        }

        [Fact]
        public void AClearanceAlsoFindsWhatComesTooNear()
        {
            ClashResult[] found = Clash3.Find(Model(), new ClashOptions(clearance: 6.0));

            Assert.Equal(4, found.Length);

            ClashResult pin = found.Single(r => r.First == 4);
            Assert.Equal(ClashKind.Clearance, pin.Kind);
            Assert.Equal(5, pin.Second);
            Assert.Equal(5.0, pin.Distance, 6);
            Assert.True(pin.Gap.HasValue);
            Assert.Equal(5.0, pin.Gap.Value.Length, 6);

            ClashResult blocks = found.Single(r => r.First == 6);
            Assert.Equal(ClashKind.Clearance, blocks.Kind);
            Assert.Equal(3.0, blocks.Distance, 6);
            Assert.True(blocks.Location.DistanceTo(new GeoPoint3(3011.5, blocks.Location.Y, blocks.Location.Z)) < 1E-6);
        }

        [Fact]
        public void TouchingCanBeLeftOut()
        {
            ClashResult[] found = Clash3.Find(Model(), new ClashOptions(includeTouching: false));

            ClashResult only = Assert.Single(found);
            Assert.Equal(ClashKind.Hard, only.Kind);
        }

        [Fact]
        public void OneSetAgainstAnotherIndexesEachInItsOwnSet()
        {
            List<GeoSolid3> model = Model();
            GeoSolid3[] beams = { model[1], model[7] };
            GeoSolid3[] others = { model[6], model[0], model[3] };

            ClashResult[] found = Clash3.Find(beams, others, new ClashOptions(clearance: 5.0));

            Assert.Equal(2, found.Length);
            Assert.Equal((0, 1, ClashKind.Hard), (found[0].First, found[0].Second, found[0].Kind));
            Assert.Equal((1, 0, ClashKind.Clearance), (found[1].First, found[1].Second, found[1].Kind));
        }

        [Fact]
        public void PreparedPartsCanBeCheckedAgain()
        {
            GeoPreparedSolid3[] prepared = Model().Select(part => part.Prepare()).ToArray();

            ClashResult[] once = Clash3.Find(prepared);
            ClashResult[] again = Clash3.Find(prepared, new ClashOptions(maxDegreeOfParallelism: 1));

            Assert.Equal(once.Select(r => (r.First, r.Second, r.Kind)), again.Select(r => (r.First, r.Second, r.Kind)));
        }

        [Fact]
        public void RandomPartsGiveWhatEveryPairCheckedByHandGives()
        {
            var rng = new Random(2027);
            var parts = new List<GeoSolid3>();

            for (int k = 0; k < 120; k++)
            {
                double x = rng.Next(0, 40) * 10, y = rng.Next(0, 40) * 10, z = rng.Next(0, 10) * 10;
                GeoSolid3 box = Box(x, y, z, x + rng.Next(1, 6) * 10, y + rng.Next(1, 6) * 10, z + rng.Next(1, 6) * 10);

                if (rng.NextDouble() < 0.3)
                {
                    GeoPoint3 centre = box.GetAabb().Center;
                    box = box.TransformBy(GeoTransform3.RotationAxis(centre, new GeoVector3(rng.NextDouble(), rng.NextDouble(), 1), rng.NextDouble()));
                }

                parts.Add(box);
            }

            ClashResult[] found = Clash3.Find(parts);
            var byHand = new List<(int, int, ClashKind, double)>();

            for (int i = 0; i < parts.Count; i++)
            {
                for (int j = i + 1; j < parts.Count; j++)
                {
                    if (!parts[i].CollidesWith(parts[j]))
                    {
                        continue;
                    }

                    double shared = parts[i].Intersect(parts[j]).Sum(piece => piece.Volume);
                    byHand.Add((i, j, shared > 0 ? ClashKind.Hard : ClashKind.Touch, shared));
                }
            }

            Assert.True(byHand.Count > 20, $"only {byHand.Count} pairs clash; the test needs more");
            Assert.Equal(byHand.Select(p => (p.Item1, p.Item2, p.Item3)), found.Select(r => (r.First, r.Second, r.Kind)));

            for (int k = 0; k < found.Length; k++)
            {
                Assert.Equal(byHand[k].Item4, found[k].Volume, 6);
            }

            Assert.DoesNotContain(found, r => r.Kind == ClashKind.Unresolved);
        }

        /// <summary>
        /// An I-beam along X and the bars a check in Tekla would set against it: one straight through the web, one
        /// bent over the top flange, a square bar lying on the flange and one running above it too near. What is
        /// reported is what the pieces mesh to, so what is drawn from them shows what was measured.
        /// </summary>
        [Fact]
        public void BarsAgainstABeamMeshToWhatTheyReport()
        {
            var section = new GeoPolygon2(
                new GeoPoint2(-100, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 15), new GeoPoint2(5, 15),
                new GeoPoint2(5, 285), new GeoPoint2(100, 285), new GeoPoint2(100, 300), new GeoPoint2(-100, 300),
                new GeoPoint2(-100, 285), new GeoPoint2(-5, 285), new GeoPoint2(-5, 15), new GeoPoint2(-100, 15));
            GeoSolid3 beam = GeoSolid3.Extrude(section, new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.YAxis, GeoVector3.ZAxis), 2000);

            GeoSolid3 through = GeoSolid3.Pipe(new GeoPolyline3(new GeoPoint3(500, -300, 150), new GeoPoint3(500, 300, 150)), 8, 0.1);
            GeoPolylineArc3 hooked = new GeoPolyline3(new GeoPoint3(800, 60, 450), new GeoPoint3(800, 60, 200), new GeoPoint3(800, 400, 200)).Fillet(40);
            GeoSolid3 bent = GeoSolid3.Pipe(hooked, 8, 0.1);
            GeoSolid3 lying = Box(1200, -20, 300, 1400, 20, 330);
            GeoSolid3 above = GeoSolid3.Pipe(new GeoPolyline3(new GeoPoint3(1500, -50, 318), new GeoPoint3(1900, -50, 318)), 8, 0.1);

            ClashResult[] found = Clash3.Find(new[] { through, bent, lying, above }, new[] { beam }, new ClashOptions(clearance: 25.0));

            Assert.Equal(
                new[] { (0, ClashKind.Hard), (1, ClashKind.Hard), (2, ClashKind.Touch), (3, ClashKind.Clearance) },
                found.Select(r => (r.First, r.Kind)));

            // The straight runs cross the web and the flange square, so each shares its section times the thickness
            // crossed; the bend lies clear of the steel.
            double sectionArea = through.Volume / 600;
            Assert.Equal(sectionArea * 10, found[0].Volume, 6);
            Assert.Equal(sectionArea * 15, found[1].Volume, 6);

            foreach (ClashResult hard in found.Take(2))
            {
                Assert.Equal(hard.Volume, hard.Overlaps.Sum(piece => EnclosedVolume(piece.TriangulateSurface())), 6);
            }

            ClashResult touching = found[2];
            Assert.Equal(200.0 * 40, touching.ContactArea, 6);
            Assert.Equal(touching.ContactArea, touching.Contact.Sum(patch => patch.TriangulateSurface().Sum(t => t.Area)), 6);

            // The section's flat sides lie inside its circle, so the bar is a little further off than its axis says.
            ClashResult near = found[3];
            Assert.InRange(near.Distance, 10.0, 10.1);
            Assert.Equal(near.Distance, near.Gap.Value.Length, 9);
            Assert.Equal(300.0, Math.Min(near.Gap.Value.StartPoint.Z, near.Gap.Value.EndPoint.Z), 9);
        }

        /// <summary>
        /// The volume a closed mesh wound outwards encloses: the signed tetrahedra from one of its corners.
        /// </summary>
        private static double EnclosedVolume(GeoTriangle3[] triangles)
        {
            GeoPoint3 apex = triangles[0].A;

            return triangles.Sum(t => (t.A - apex).DotProduct((t.B - apex).CrossProduct(t.C - apex))) / 6.0;
        }

        [Fact]
        public void OptionsRefuseWhatIsNotAClearanceOrAThreadCount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClashOptions(clearance: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClashOptions(clearance: double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClashOptions(maxDegreeOfParallelism: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClashOptions(maxDegreeOfParallelism: -2));
            Assert.Equal(new ClashOptions(2.0, false, 4), new ClashOptions(2.0, false, 4));
            Assert.Throws<ArgumentNullException>(() => Clash3.Find(new GeoSolid3[] { Box(0, 0, 0, 1, 1, 1), null }));
        }
    }
}

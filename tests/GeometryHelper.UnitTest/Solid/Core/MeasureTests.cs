using System;
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
    /// A body measured each way: over flat faces every method gives the same; over a face a hair out of flat they part
    /// as far as the face leaves its volume open, and the comparison says so.
    /// </summary>
    public class MeasureTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static readonly VolumeMethod[] Methods = { VolumeMethod.Fan, VolumeMethod.Surface, VolumeMethod.FlatFaces };

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(P(x0, y0, z0), P(x1, y1, z1)).ToObb().ToSolid();

        /// <summary>
        /// A box 100 by 100 by 10 whose top corner over (100, 100) is lifted, its top face's loop starting at a corner: the
        /// sides through that corner stay flat, as it rises in their planes, and only the top is out of flat.
        /// </summary>
        private static GeoSolid3 LiftedBox(double lift, int start)
        {
            GeoPoint3[] top = { P(0, 0, 10), P(100, 0, 10), P(100, 100, 10 + lift), P(0, 100, 10) };
            GeoPoint3[][] faces =
            {
                Enumerable.Range(0, 4).Select(i => top[(start + i) % 4]).ToArray(),
                new[] { P(0, 0, 0), P(0, 100, 0), P(100, 100, 0), P(100, 0, 0) },
                new[] { P(0, 0, 0), P(100, 0, 0), P(100, 0, 10), P(0, 0, 10) },
                new[] { P(100, 0, 0), P(100, 100, 0), P(100, 100, 10 + lift), P(100, 0, 10) },
                new[] { P(100, 100, 0), P(0, 100, 0), P(0, 100, 10), P(100, 100, 10 + lift) },
                new[] { P(0, 100, 0), P(0, 0, 0), P(0, 0, 10), P(0, 100, 10) },
            };

            return new GeoSolid3(faces.Select(f => new GeoFace3(new GeoPolygon3(f, Tolerance))));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(3E6)]
        public void EveryMethodGivesTheMeasuresOfAFlatBody(double far)
        {
            // An L, its notch making one face concave, moved out as far as a coordinate of a site plan goes.
            var outline = new GeoPolygon3(P(0, 0, 0), P(100, 0, 0), P(100, 40, 0), P(40, 40, 0), P(40, 60, 0), P(0, 60, 0));
            GeoSolid3 l = GeoSolid3.Extrude(outline, new GeoVector3(0, 0, 20), Tolerance).TransformBy(GeoTransform3.Translation(new GeoVector3(far, -far, 0.5 * far)));
            double volume = (100.0 * 40 + 40 * 20) * 20;
            double area = 2 * (100.0 * 40 + 40 * 20) + 20 * (100 + 40 + 60 + 20 + 40 + 60);
            GeoPoint3 centroid = P(far + (100.0 * 40 * 50 + 40 * 20 * 20) / (100 * 40 + 40 * 20), -far + (100.0 * 40 * 20 + 40 * 20 * 50) / (100 * 40 + 40 * 20), 0.5 * far + 10);

            foreach (VolumeMethod method in Methods)
            {
                Assert.InRange(Measure3.Volume(l, method, Tolerance) / volume, 1 - 1E-9, 1 + 1E-9);
                Assert.InRange(Measure3.Mass(l, 7.85E-6, method, Tolerance) / (7.85E-6 * volume), 1 - 1E-9, 1 + 1E-9);
                Assert.True(Measure3.Centroid(l, method, Tolerance).DistanceTo(centroid) < 1E-6);
                Assert.Equal(method, Measure3.MassProperties(l, 2.0, method, Tolerance).Method);
            }

            Assert.Equal(area, Measure3.SurfaceArea(l, AreaMethod.Faces, Tolerance), 6);
            Assert.Equal(area, Measure3.SurfaceArea(l, AreaMethod.Surface, Tolerance), 6);

            MeasureComparison3 compared = Measure3.Compare(l, Tolerance);
            Assert.True(compared.OpeningsCut);
            Assert.True(compared.VolumeSpread < 1E-9 * volume, $"volumes {compared.VolumeSpread} apart");
            Assert.True(compared.ReferenceSpread < 1E-9 * volume, $"from the corners {compared.ReferenceSpread} apart");
            Assert.True(compared.CentroidSpread < 1E-6);
            Assert.True(compared.AreaSpread < 1E-9 * area);
        }

        [Fact]
        public void TheMaterialIsMeasuredWithItsOpeningsCutIn()
        {
            // A plate with a hole 20 square through it, its walls 20 deep.
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20).WithOpenings(new[] { Box(60, 40, -1, 80, 60, 21) });
            double gross = 100.0 * 100 * 20, hole = 20.0 * 20 * 20, net = gross - hole;

            foreach (VolumeMethod method in Methods)
            {
                Assert.Equal(net, Measure3.Volume(plate, method, Tolerance), 6);
                Assert.True(Measure3.Centroid(plate, method, Tolerance).DistanceTo(P((gross * 50 - hole * 70) / net, 50, 10)) < 1E-9);
            }

            foreach (AreaMethod method in new[] { AreaMethod.Faces, AreaMethod.Surface })
            {
                Assert.Equal(2 * (10000.0 - 400) + 4 * 100 * 20 + 4 * 20 * 20, Measure3.SurfaceArea(plate, method, Tolerance), 6);
            }

            // The body measures the same material: its fan, and its faces read flat.
            Assert.Equal(plate.GetVolume(Tolerance), Measure3.Volume(plate, VolumeMethod.Fan, Tolerance), 6);
            Assert.Equal(plate.GetSurfaceArea(Tolerance), Measure3.SurfaceArea(plate, AreaMethod.Faces, Tolerance), 9);
            Assert.True(plate.GetCentroid(Tolerance).DistanceTo(Measure3.Centroid(plate, VolumeMethod.Fan, Tolerance)) < 1E-9);
        }

        [Fact]
        public void TheBodyAndMeasure3MeasureOneCut()
        {
            // An opening that cannot be cut in, beside one that can: the body is cut once, the one warning is of the first,
            // and every measure after it is of the same material, the hole out and the broken opening left in.
            List<GeoFace3> faces = Box(10, 10, -1, 30, 30, 21).Faces.ToList();
            int top = faces.FindIndex(f => f.Boundary.Normal.Z > 0.5);
            faces[top] = new GeoFace3(GeoPolygon3.FromValidated(faces[top].Boundary.Vertices.ToArray(), new GeoVector3(0, 0, 0), faces[top].Area));
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20).WithOpenings(new[] { new GeoSolid3(faces), Box(60, 40, -1, 80, 60, 21) });
            var warnings = new List<string>();
            bool enabled = GeometryHelperLog.Enable;
            GeometryHelperLog.Enable = true;
            GeometryHelperLog.Writer = (level, message, exception) =>
            {
                if (level == GeometryHelperLogLevel.Warn)
                {
                    warnings.Add(message);
                }
            };

            try
            {
                Assert.Equal(192000.0, plate.GetVolume(Tolerance), 6);
                MeasureComparison3 compared = Measure3.Compare(plate, Tolerance);

                Assert.False(compared.OpeningsCut);
                Assert.Contains("openings not cut", compared.ToString());
                Assert.Equal(192000.0, compared.GetVolume(VolumeMethod.Surface), 6);
                Assert.Equal(192000.0 * 7.85E-6, Measure3.Mass(plate, 7.85E-6, VolumeMethod.Fan, Tolerance), 9);
                Assert.Equal(plate.GetSurfaceArea(Tolerance), Measure3.SurfaceArea(plate, AreaMethod.Faces, Tolerance), 9);
                Assert.Equal(192000.0, plate.GetMassProperties(1.0, Tolerance).Volume, 6);
                Assert.Single(warnings);
            }
            finally
            {
                GeometryHelperLog.Writer = null;
                GeometryHelperLog.Enable = enabled;
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void AFaceOfFourCornersWithOneLiftedHoldsWhatItsReadingSays(int start)
        {
            // The top, 100 square, has a corner lifted 0.008: split along the diagonal through that corner it holds a third
            // of the lift times its area more, 26.67; along the other a sixth, 13.33; read flat a quarter, 20, between.
            // The fan from the top's first corner splits it through that corner when the loop starts at (0, 0) and along
            // the other diagonal when it starts at (100, 0).
            GeoSolid3 box = LiftedBox(0.008, start);
            double third = 1E4 * 0.008 / 3, sixth = 1E4 * 0.008 / 6, quarter = 1E4 * 0.008 / 4;

            Assert.True(box.IsClosed(Tolerance));
            Assert.Equal(1E5 + (start == 0 ? third : sixth), Measure3.Volume(box, VolumeMethod.Fan, Tolerance), 6);
            Assert.Equal(box.GetVolume(), Measure3.Volume(box, VolumeMethod.Fan, Tolerance), 6);
            Assert.Equal(1E5 + quarter, Measure3.Volume(box, VolumeMethod.FlatFaces, Tolerance), 5);

            // The fan reads the top flat through its first corner, which stands a quarter of the lift off the top's middle
            // plane, above it from (0, 0) and below it from (100, 0): a third of that times the area apart from flat.
            double fan = Measure3.Volume(box, VolumeMethod.Fan, Tolerance), flat = Measure3.Volume(box, VolumeMethod.FlatFaces, Tolerance);
            Assert.Equal((start == 0 ? 1 : -1) * 1E4 * (0.008 / 4) / 3, fan - flat, 6);

            double surface = Measure3.Volume(box, VolumeMethod.Surface, Tolerance);
            Assert.True(Math.Abs(surface - (1E5 + third)) < 1E-6 || Math.Abs(surface - (1E5 + sixth)) < 1E-6, $"{surface} is neither split");

            // The comparison sets them side by side: a sixth of the lift times the area apart, and the faces still close.
            MeasureComparison3 compared = Measure3.Compare(box, Tolerance);
            Assert.Equal(third - sixth, compared.VolumeSpread, 6);
            Assert.True(compared.ReferenceSpread < 1E-6);

            // The top bent along a diagonal is a hair larger than read flat.
            Assert.True(compared.GetSurfaceArea(AreaMethod.Surface) > compared.GetSurfaceArea(AreaMethod.Faces));
            Assert.True(compared.AreaSpread < 1E-4);
        }

        [Fact]
        public void TheHalvesOfACutHoldThePieceReadAsTheTrianglesInTheirFaces()
        {
            // A piece of a column with a ledge cut through a corner of the ledge's tip: the faces round the section are
            // concave and a hair out of flat. Read as the triangles lying in them, the halves hold the piece; read as fans
            // they hold 560 mm3 more, and read flat 87 more.
            GeoSolid3 piece = CapAHairOffThePlaneTests.Piece();
            Assert.True(piece.TrySplitBy(CapAHairOffThePlaneTests.Plane(), out GeoSolid3 above, out GeoSolid3 below, Tolerance));

            double Sum(VolumeMethod method) => Measure3.Volume(above, method, Tolerance) + Measure3.Volume(below, method, Tolerance);
            double whole = Measure3.Volume(piece, VolumeMethod.Surface, Tolerance);

            Assert.InRange(Sum(VolumeMethod.Surface) / whole, 1 - 1E-7, 1 + 1E-7);
            Assert.InRange(Sum(VolumeMethod.Fan) - whole, 400, 700);
            Assert.InRange(Sum(VolumeMethod.FlatFaces) - whole, 60, 120);

            // The piece's faces are flat, and its methods agree; the halves' are not, and theirs part.
            Assert.True(Measure3.Compare(piece, Tolerance).VolumeSpread < 1E-6);
            Assert.True(Measure3.Compare(above, Tolerance).VolumeSpread > 100);
            Assert.True(Measure3.Compare(below, Tolerance).VolumeSpread > 100);
        }

        [Fact]
        public void FacesThatMeetWithinTheToleranceShowInTheReferenceSpread()
        {
            // Two faces of a piece share an edge on copies of it 0.0045 and 0.0063 apart at its ends. The faces are flat and
            // every method gives the same volume, but where it is measured from moves it by tens of cubic millimetres.
            MeasureComparison3 cracked = Measure3.Compare(EdgeSharedWithinTheToleranceTests.Piece(), Tolerance);

            Assert.True(cracked.VolumeSpread < 1E-6, $"volumes {cracked.VolumeSpread} apart");
            Assert.InRange(cracked.ReferenceSpread, 10, 1000);
            Assert.True(Measure3.Compare(Box(0, 0, 0, 100, 60, 20), Tolerance).ReferenceSpread < 1E-6);
        }

        [Fact]
        public void ABodyWoundInwardsMeasuresAsItsOutwardSelf()
        {
            GeoSolid3 box = Box(0, 0, 0, 100, 60, 20);
            var inwards = new GeoSolid3(box.Faces.Select(f => new GeoFace3(f.Boundary.Flip())));

            foreach (VolumeMethod method in Methods)
            {
                Assert.Equal(120000.0, Measure3.Volume(inwards, method, Tolerance), 6);
                Assert.True(Measure3.Centroid(inwards, method, Tolerance).DistanceTo(P(50, 30, 10)) < 1E-9);
            }

            Assert.Equal(Measure3.MassProperties(box, 1.0, VolumeMethod.Surface, Tolerance).Izz, Measure3.MassProperties(inwards, 1.0, VolumeMethod.Surface, Tolerance).Izz, 3);
        }

        [Fact]
        public void ABodyItsOpeningsTakeWholeHoldsNothing()
        {
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20).WithOpenings(new[] { Box(-10, -10, -10, 110, 110, 30) });

            foreach (VolumeMethod method in Methods)
            {
                Assert.Equal(0.0, Measure3.Volume(plate, method, Tolerance));
                Assert.Equal(0.0, Measure3.Mass(plate, 7.85E-6, method, Tolerance));
                Assert.Equal(P(50, 50, 10), Measure3.Centroid(plate, method, Tolerance));
            }

            Assert.Equal(0.0, Measure3.SurfaceArea(plate, AreaMethod.Faces, Tolerance));
            MeasureComparison3 compared = Measure3.Compare(plate, Tolerance);
            Assert.True(compared.OpeningsCut);
            Assert.Equal(0.0, compared.GetVolume(VolumeMethod.Surface));
            Assert.Equal(0.0, compared.VolumeSpread);
        }

        [Fact]
        public void TheMassPropertiesOfABodyAreItsSurfaceReading()
        {
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20).WithOpenings(new[] { Box(60, 40, -1, 80, 60, 21) });
            MassProperties3 given = plate.GetMassProperties(7.85E-6, Tolerance);
            MassProperties3 surface = Measure3.MassProperties(plate, 7.85E-6, VolumeMethod.Surface, Tolerance);

            Assert.Equal(VolumeMethod.Surface, given.Method);
            Assert.Equal(surface.Volume, given.Volume);
            Assert.Equal(surface.Mass, given.Mass);
            Assert.Equal(surface.Centroid, given.Centroid);
            Assert.Equal(surface.Izz, given.Izz);
            Assert.Equal(surface.SurfaceArea, given.SurfaceArea);
        }

        [Fact]
        public void NullsUnknownMethodsAndDensitiesThatAreNoneAreRefused()
        {
            GeoSolid3 box = Box(0, 0, 0, 1, 1, 1);

            Assert.Throws<ArgumentNullException>(() => Measure3.Volume(null, VolumeMethod.Fan, Tolerance));
            Assert.Throws<ArgumentNullException>(() => Measure3.Mass(null, 1.0, VolumeMethod.Fan, Tolerance));
            Assert.Throws<ArgumentNullException>(() => Measure3.Centroid(null, VolumeMethod.Fan, Tolerance));
            Assert.Throws<ArgumentNullException>(() => Measure3.MassProperties(null, 1.0, VolumeMethod.Fan, Tolerance));
            Assert.Throws<ArgumentNullException>(() => Measure3.SurfaceArea(null, AreaMethod.Faces, Tolerance));
            Assert.Throws<ArgumentNullException>(() => Measure3.Compare(null, Tolerance));

            Assert.Throws<ArgumentOutOfRangeException>(() => Measure3.Volume(box, (VolumeMethod)7, Tolerance));
            Assert.Throws<ArgumentOutOfRangeException>(() => Measure3.Volume(box, (VolumeMethod)(-1), Tolerance));
            Assert.Throws<ArgumentOutOfRangeException>(() => Measure3.SurfaceArea(box, (AreaMethod)5, Tolerance));
            Assert.Throws<ArgumentOutOfRangeException>(() => Measure3.Compare(box, Tolerance).GetVolume((VolumeMethod)3));
            Assert.Throws<ArgumentOutOfRangeException>(() => Measure3.Compare(box, Tolerance).GetSurfaceArea((AreaMethod)2));

            foreach (double density in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => Measure3.Mass(box, density, VolumeMethod.Fan, Tolerance));
                Assert.Throws<ArgumentOutOfRangeException>(() => Measure3.MassProperties(box, density, VolumeMethod.Fan, Tolerance));
            }
        }

        [Fact]
        public void TheComparisonReadsAsItsMeasures()
        {
            string text = Measure3.Compare(Box(0, 0, 0, 100, 60, 20), Tolerance).ToString();

            Assert.Contains("Fan 120000", text);
            Assert.Contains("FlatFaces 120000", text);
            Assert.Contains("Faces 18400", text);
            Assert.DoesNotContain("openings not cut", text);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A body measured as its material: the volume, the area and the centroid with every opening cut in, the walls of
    /// the openings part of the surface, all three of one cut, and where an opening will not come out, said so.
    /// </summary>
    public class MaterialMeasureTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoPoint2 Q(double x, double y) => new GeoPoint2(x, y);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(P(x0, y0, z0), P(x1, y1, z1)).ToObb().ToSolid();

        private static GeoSolid3 Plate() => Box(0, 0, 0, 100, 100, 20);

        /// <summary>A body with every face turned over, wound inwards.</summary>
        private static GeoSolid3 InsideOut(GeoSolid3 body) => new GeoSolid3(body.Faces.Select(f => f.Flip()));

        /// <summary>
        /// A box whose top face says its normal is nought, which no face made the usual way can: the planes of its faces
        /// cannot all be made, so neither cutting it in nor taking it out by a difference can be worked out.
        /// </summary>
        private static GeoSolid3 Broken(double x0, double y0, double z0, double x1, double y1, double z1)
        {
            List<GeoFace3> faces = Box(x0, y0, z0, x1, y1, z1).Faces.ToList();
            int top = faces.FindIndex(f => f.Boundary.Normal.Z > 0.5);
            faces[top] = new GeoFace3(GeoPolygon3.FromValidated(faces[top].Boundary.Vertices.ToArray(), new GeoVector3(0, 0, 0), faces[top].Area));
            return new GeoSolid3(faces);
        }

        /// <summary>Runs an action and gives back the warnings it wrote.</summary>
        private static List<string> Warnings(Action action)
        {
            var written = new List<string>();
            bool enabled = GeometryHelperLog.Enable;
            GeometryHelperLog.Enable = true;
            GeometryHelperLog.Writer = (level, message, exception) =>
            {
                if (level == GeometryHelperLogLevel.Warn)
                {
                    written.Add(message);
                }
            };

            try
            {
                action();
            }
            finally
            {
                GeometryHelperLog.Writer = null;
                GeometryHelperLog.Enable = enabled;
            }

            return written;
        }

        [Fact]
        public void AHoleThroughAPlateComesOutOfItsVolumeItsAreaAndItsCentroid()
        {
            // A hole 20 square through a plate 100 square and 20 thick, drawn past both faces: only its 20 deep in the
            // plate is taken out, and its four walls are surface where the material ends.
            GeoSolid3 plate = Plate().WithOpenings(new[] { Box(60, 40, -1, 80, 60, 21) });
            double gross = 100.0 * 100 * 20, hole = 20.0 * 20 * 20, net = gross - hole;

            Assert.Equal(net, plate.GetVolume(Tolerance), 6);
            Assert.Equal(2 * (10000.0 - 400) + 4 * 100 * 20 + 4 * 20 * 20, plate.GetSurfaceArea(Tolerance), 6);
            Assert.True(plate.GetCentroid(Tolerance).DistanceTo(P((gross * 50 - hole * 70) / net, 50, 10)) < 1E-9);

            Assert.True(plate.TryGetVolume(out double volume, Tolerance));
            Assert.Equal(plate.GetVolume(Tolerance), volume);
            Assert.Equal(plate.GetVolume(Tolerance.Global), plate.GetVolume());
            Assert.Equal(plate.GetSurfaceArea(Tolerance.Global), plate.GetSurfaceArea());
            Assert.Equal(plate.GetCentroid(Tolerance.Global), plate.GetCentroid());
        }

        [Fact]
        public void OverlappingOpeningsAreTakenOutOnce()
        {
            // Two holes 30 by 20 sharing 10 by 20 of it: 50 by 20 of the plate is gone, not 60 by 20, and the walls are the
            // rim of the 50 by 20 the two leave, not of each.
            GeoSolid3 plate = Plate().WithOpenings(new[] { Box(10, 10, -1, 40, 30, 21), Box(30, 10, -1, 60, 30, 21) });
            double gross = 100.0 * 100 * 20, holes = 50.0 * 20 * 20, net = gross - holes;

            Assert.Equal(net, plate.GetVolume(Tolerance), 6);
            Assert.Equal(2 * (10000.0 - 1000) + 4 * 100 * 20 + (2 * 50 + 2 * 20) * 20, plate.GetSurfaceArea(Tolerance), 6);
            Assert.True(plate.GetCentroid(Tolerance).DistanceTo(P((gross * 50 - holes * 35) / net, (gross * 50 - holes * 20) / net, 10)) < 1E-9);
            Assert.True(plate.TryGetVolume(out _, Tolerance));
        }

        [Fact]
        public void AnOpeningInsideTheBodyLeavesACavityWhoseWallsAreSurface()
        {
            GeoSolid3 block = Box(0, 0, 0, 10, 10, 4).WithOpenings(new[] { Box(4, 4, 1, 6, 6, 3) });

            Assert.Equal(400.0 - 8.0, block.GetVolume(Tolerance), 9);
            Assert.Equal(2 * 100.0 + 4 * 40 + 6 * 4, block.GetSurfaceArea(Tolerance), 9);
            Assert.True(block.GetCentroid(Tolerance).DistanceTo(P(5, 5, 2)) < 1E-9);
            Assert.True(block.TryGetVolume(out _, Tolerance));
        }

        [Fact]
        public void ABodyWithoutOpeningsIsMeasuredByItsFacesAndNotCut()
        {
            // An L far out on a site plan, its notch making one face concave: no openings, so nothing is cut, and the
            // measures are the faces' to the last bit.
            var outline = new GeoPolygon3(P(0, 0, 0), P(100, 0, 0), P(100, 40, 0), P(40, 40, 0), P(40, 60, 0), P(0, 60, 0));
            GeoSolid3 l = GeoSolid3.Extrude(outline, new GeoVector3(0, 0, 20), Tolerance).Translate(new GeoVector3(3E6, -3E6, 1.5E6));

            Assert.Same(l, l.GetMaterial(Tolerance, out bool whole));
            Assert.True(whole);
            Assert.Equal(l.GrossVolume, l.GetVolume(Tolerance));
            Assert.Equal(l.GrossSurfaceArea, l.GetSurfaceArea(Tolerance));
            Assert.Equal(l.GrossCentroid, l.GetCentroid(Tolerance));
            Assert.True(l.TryGetVolume(out double volume, Tolerance));
            Assert.Equal(l.GrossVolume, volume);
        }

        [Fact]
        public void ABodyItsOpeningsTakeWholeHoldsNothing()
        {
            GeoSolid3 plate = Plate().WithOpenings(new[] { Box(-10, -10, -10, 110, 110, 30) });

            Assert.Equal(0.0, plate.GetVolume(Tolerance));
            Assert.Equal(0.0, plate.GetSurfaceArea(Tolerance));
            Assert.Equal(P(50, 50, 10), plate.GetCentroid(Tolerance));

            // Nothing left is an answer, not a failure.
            Assert.True(plate.TryGetVolume(out double volume, Tolerance));
            Assert.Equal(0.0, volume);
        }

        [Fact]
        public void AnOpeningThatMissesTheBodyTakesNothing()
        {
            GeoSolid3 plate = Plate().WithOpenings(new[] { Box(200, 200, 0, 210, 210, 20) });

            Assert.Equal(200000.0, plate.GetVolume(Tolerance), 6);
            Assert.Equal(2 * 10000.0 + 4 * 100 * 20, plate.GetSurfaceArea(Tolerance), 6);
            Assert.True(plate.GetCentroid(Tolerance).DistanceTo(P(50, 50, 10)) < 1E-9);
        }

        [Fact]
        public void TheMaterialIsCutOnceForATolerance()
        {
            GeoSolid3 plate = Plate().WithOpenings(new[] { Box(60, 40, -1, 80, 60, 21) });
            GeoSolid3 material = plate.GetMaterial(Tolerance, out bool whole);

            Assert.True(whole);
            Assert.Empty(material.Openings);
            Assert.Same(material, plate.GetMaterial(Tolerance, out _));

            // Another tolerance is another cut, and a copy of the body starts without any.
            GeoSolid3 finer = plate.GetMaterial(new Tolerance(1E-3, 1E-3), out _);
            Assert.NotSame(material, finer);
            Assert.Equal(192000.0, finer.GetVolume(), 6);
            Assert.NotSame(material, plate.Clone().GetMaterial(Tolerance, out _));
        }

        [Fact]
        public void AnOpeningTheCutLeavesOpenIsTakenOutByADifference()
        {
            // A sliver of an opening at the step of an L, most of it outside: cut in with the others it leaves the
            // material open along the step; taken out by a difference, which tries more ways, the material closes.
            GeoSolid3 sliver = GeometryHelper.UnitTest.Meshing.CellTests.OpeningSliver();
            var plan = new GeoPolygon3(new[] { Q(0, 0), Q(65, 0), Q(65, 62), Q(40, 62), Q(40, 142), Q(0, 142) }.Select(q => P(q.X, q.Y, 0)), Tolerance);
            GeoSolid3 ell = GeoSolid3.Extrude(plan, new GeoVector3(0, 0, 1777), Tolerance);
            GeoSolid3 pierced = ell.WithOpenings(new[] { sliver });

            Assert.True(GeometryHelper.Core.Boolean3.TrySubtract(ell, sliver, out GeoSolid3 difference, Tolerance));
            Assert.True(difference.IsClosed(Tolerance));

            GeoSolid3 material = pierced.GetMaterial(Tolerance, out bool whole);
            Assert.True(whole);
            Assert.True(material.IsClosed(Tolerance));
            Assert.True(pierced.TryGetVolume(out double volume, Tolerance));
            Assert.Equal(difference.GetVolume(), volume, 6);
            Assert.InRange(volume, ell.GetVolume() - sliver.GetVolume(), ell.GetVolume());
        }

        [Fact]
        public void AnOpeningThatWillNotComeOutIsLeftInAndSaidSo()
        {
            // The broken opening stops the openings being cut in at once; taken out one at a time, the hole comes out and
            // the broken one stays in, warned of, and the volume is said not to be the material's.
            GeoSolid3 plate = Plate().WithOpenings(new[] { Broken(10, 10, -1, 30, 30, 21), Box(60, 40, -1, 80, 60, 21) });
            double volume = 0.0;
            bool sound = true;

            List<string> warnings = Warnings(() => sound = plate.TryGetVolume(out volume, Tolerance));

            Assert.False(sound);
            Assert.Equal(0.0, volume);
            Assert.Single(warnings);
            Assert.Contains("could not be cut", warnings[0]);

            // Measured as it is, nothing cut again and nothing warned of twice.
            Assert.Empty(Warnings(() => volume = plate.GetVolume(Tolerance)));
            Assert.Equal(200000.0 - 8000.0, volume, 6);
            Assert.False(plate.GetMaterial(Tolerance, out _).Openings.Any());
        }

        [Fact]
        public void AnOpeningWoundInwardsIsTakenOutOneAtATimeAsItsOutwardSelf()
        {
            // Taken out one at a time, as the broken opening makes it, a hole wound inwards is turned outwards first: a
            // difference reads its faces' winding, and took the inside-out hole for material, adding to the plate.
            GeoSolid3 plate = Plate().WithOpenings(new[] { Broken(10, 10, -1, 30, 30, 21), InsideOut(Box(60, 40, -1, 80, 60, 21)) });
            double volume = 0.0;

            Warnings(() => volume = plate.GetVolume(Tolerance));

            Assert.Equal(200000.0 - 8000.0, volume, 6);
        }

        [Fact]
        public void ABodyWoundInwardsIsMeasuredAsItsOutwardSelf()
        {
            GeoSolid3 plate = InsideOut(Plate()).WithOpenings(new[] { Box(60, 40, -1, 80, 60, 21) });

            Assert.Equal(192000.0, plate.GetVolume(Tolerance), 6);
            Assert.Equal(28800.0, plate.GetSurfaceArea(Tolerance), 6);
            Assert.True(plate.GetCentroid(Tolerance).DistanceTo(P((200000.0 * 50 - 8000.0 * 70) / 192000.0, 50, 10)) < 1E-9);
        }

        [Fact]
        public void ABodyThatDoesNotCloseHasNoVolumeToTrust()
        {
            // A box without its top: its volume is a number, but not of anything, and TryGetVolume says so.
            GeoSolid3 box = Plate();
            var open = new GeoSolid3(box.Faces.Where(f => f.Boundary.Normal.Z < 0.5));

            Assert.False(open.TryGetVolume(out double volume, Tolerance));
            Assert.Equal(0.0, volume);
            Assert.False(open.WithOpenings(new[] { Box(60, 40, -1, 80, 60, 21) }).TryGetVolume(out _, Tolerance));
        }
    }
}

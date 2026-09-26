using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Where an arc in space crosses a flat region. Which of the region's two parts gives the answer depends
    /// on how the arc lies: piercing it is about the material, running in its plane is about the boundary.
    /// </summary>
    public class Arc3FlatTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>
        /// A half turn standing upright in the plane y = 0, over the top from (-50, 0, 0) to (50, 0, 0).
        /// Its middle is (0, 0, 50), so it arches over the plate below.
        /// </summary>
        private static GeoArc3 Arching() => GeoArc3.FromThreePoints(
            new GeoPoint3(-50, 0, 0), new GeoPoint3(0, 0, 50), new GeoPoint3(50, 0, 0));

        /// <summary>
        /// A hundred square plate lying in z = 0, centred on the origin.
        /// </summary>
        private static GeoPolygon3 Plate() => new GeoPolygon3(
            new GeoPoint3(-50, -50, 0), new GeoPoint3(50, -50, 0),
            new GeoPoint3(50, 50, 0), new GeoPoint3(-50, 50, 0));

        /// <summary>
        /// The same plate with a twenty-wide hole in the middle of it.
        /// </summary>
        private static GeoFace3 Pierced() => new GeoFace3(
            Plate(),
            new[]
            {
                new GeoPolygon3(
                    new GeoPoint3(-10, -10, 0), new GeoPoint3(10, -10, 0),
                    new GeoPoint3(10, 10, 0), new GeoPoint3(-10, 10, 0))
            });

        [Fact]
        public void AnArcPiercingAPlateIsCreditedWithThePointsThePlateHolds()
        {
            GeoArc3 arch = Arching();
            GeoPolygon3 plate = Plate();

            // Both feet of the arch land on the plate, at (-50, 0, 0) and (50, 0, 0).
            GeoPoint3[] crossings = Arc3.GetIntersections(arch, plate);

            Assert.Equal(2, crossings.Length);

            double[] xs = crossings.Select(point => Math.Round(point.X, 6)).OrderBy(x => x).ToArray();

            Assert.Equal(new[] { -50.0, 50.0 }, xs);
            Assert.All(crossings, point => Assert.Equal(0.0, point.Z, 6));
            Assert.True(Arc3.CollidesWith(arch, plate));

            // Lift the arch clear of the plate and it pierces the plane nowhere the plate holds.
            GeoArc3 above = arch.Translate(new GeoVector3(0, 0, 200));

            Assert.Empty(Arc3.GetIntersections(above, plate));
            Assert.False(Arc3.CollidesWith(above, plate));

            // Slide it sideways so its feet land off the plate.
            GeoArc3 beside = arch.Translate(new GeoVector3(0, 500, 0));

            Assert.Empty(Arc3.GetIntersections(beside, plate));
            Assert.False(Arc3.CollidesWith(beside, plate));
        }

        [Fact]
        public void AnArcDownAHoleTouchesNothing()
        {
            GeoFace3 plate = Pierced();

            // An arch whose feet land inside the hole: it pierces the plane of the face where there is no
            // face, so it reaches no material.
            GeoArc3 inHole = GeoArc3.FromThreePoints(
                new GeoPoint3(-5, 0, 0), new GeoPoint3(0, 0, 20), new GeoPoint3(5, 0, 0));

            Assert.Empty(Arc3.GetIntersections(inHole, plate));
            Assert.False(Arc3.CollidesWith(inHole, plate));

            // The same arch against the plate without the hole does land on it.
            Assert.Equal(2, Arc3.GetIntersections(inHole, Plate()).Length);

            // And one whose feet straddle the hole lands on material both sides.
            GeoArc3 straddling = Arching();

            Assert.Equal(2, Arc3.GetIntersections(straddling, plate).Length);
            Assert.True(Arc3.CollidesWith(straddling, plate));
        }

        [Fact]
        public void AnArcInThePlaneOfARegionCutsItsBoundaryInstead()
        {
            GeoPolygon3 plate = Plate();

            // A half turn of radius 60 lying in z = 0: wide enough to leave the square through its sides and
            // come back in over the top, so it cuts the outline four times -- at x = +-50 where y = 33.166,
            // and at y = 50 where x = +-33.166.
            GeoArc3 cutting = GeoArc3.FromThreePoints(
                new GeoPoint3(60, 0, 0), new GeoPoint3(0, 60, 0), new GeoPoint3(-60, 0, 0));

            GeoPoint3[] crossings = Arc3.GetIntersections(cutting, plate);

            Assert.Equal(4, crossings.Length);
            Assert.All(crossings, point => Assert.Equal(0.0, point.Z, 6));
            Assert.All(crossings, point => Assert.True(cutting.IsPointOn(point, Loose)));
            Assert.All(crossings, point => Assert.True(Containment3.IsPointOn(plate, point, Loose),
                $"{point} is not on the outline"));

            // A circle in the same plane that is simply too big encircles the plate and touches none of it.
            // The plate's farthest corner is 70.711 out, so a radius of 100 clears it everywhere.
            GeoArc3 around = GeoArc3.FromThreePoints(
                new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0), new GeoPoint3(-100, 0, 0));

            Assert.Empty(Arc3.GetIntersections(around, plate));
            Assert.False(Arc3.CollidesWith(around, plate));

            // A small arc lying wholly inside the plate cuts nothing and still touches it.
            GeoArc3 inside = GeoArc3.FromThreePoints(
                new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0), new GeoPoint3(-10, 0, 0));

            Assert.Empty(Arc3.GetIntersections(inside, plate));
            Assert.True(Arc3.CollidesWith(inside, plate));

            // And one lying in the plane but clear of the plate touches nothing.
            GeoArc3 outside = inside.Translate(new GeoVector3(500, 0, 0));

            Assert.Empty(Arc3.GetIntersections(outside, plate));
            Assert.False(Arc3.CollidesWith(outside, plate));
        }

        [Fact]
        public void ATriangleIsReadTheSameWay()
        {
            var flat = new GeoTriangle3(
                new GeoPoint3(-50, -50, 0), new GeoPoint3(50, -50, 0), new GeoPoint3(0, 50, 0));

            // An arch whose feet land inside the triangle.
            GeoArc3 arch = GeoArc3.FromThreePoints(
                new GeoPoint3(-10, -20, 0), new GeoPoint3(0, -20, 30), new GeoPoint3(10, -20, 0));

            Assert.Equal(2, Arc3.GetIntersections(arch, flat).Length);
            Assert.True(Arc3.CollidesWith(arch, flat));

            // Beyond its corner and it lands nowhere.
            Assert.Empty(Arc3.GetIntersections(arch.Translate(new GeoVector3(0, 500, 0)), flat));
        }

        [Fact]
        public void ACircleIsTheWholeTurnHereToo()
        {
            GeoPolygon3 plate = Plate();

            // An upright circle of radius 50 about the origin in y = 0 meets z = 0 at (+-50, 0, 0), both on
            // the plate. A half turn would reach only one of them if it swept only the top.
            var upright = new GeoCircle3(GeoPoint3.Origin, new GeoVector3(0, 1, 0), 50.0);

            GeoPoint3[] crossings = Arc3.GetIntersections(upright, plate);

            Assert.Equal(2, crossings.Length);
            Assert.Equal(new[] { -50.0, 50.0 }, crossings.Select(point => Math.Round(point.X, 6)).OrderBy(x => x));

            Assert.True(Arc3.CollidesWith(upright, plate));
            Assert.True(Arc3.CollidesWith(upright, Pierced()));

            // A flat circle inside the plate cuts no outline and still touches.
            var lying = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 10.0);

            Assert.Empty(Arc3.GetIntersections(lying, plate));
            Assert.True(Arc3.CollidesWith(lying, plate));
        }

        [Fact]
        public void TheTypesAnswerTheSameFromEitherSide()
        {
            GeoArc3 arch = Arching();
            GeoPolygon3 plate = Plate();
            GeoFace3 pierced = Pierced();
            var flat = new GeoTriangle3(
                new GeoPoint3(-50, -50, 0), new GeoPoint3(50, -50, 0), new GeoPoint3(0, 50, 0));
            var upright = new GeoCircle3(GeoPoint3.Origin, new GeoVector3(0, 1, 0), 50.0);

            Assert.Equal(arch.GetIntersections(plate).Length, plate.GetIntersections(arch).Length);
            Assert.Equal(arch.GetIntersections(pierced).Length, pierced.GetIntersections(arch).Length);
            Assert.Equal(arch.GetIntersections(flat).Length, flat.GetIntersections(arch).Length);
            Assert.Equal(upright.GetIntersections(plate).Length, plate.GetIntersections(upright).Length);

            Assert.Equal(arch.CollidesWith(plate), plate.CollidesWith(arch));
            Assert.Equal(arch.CollidesWith(pierced), pierced.CollidesWith(arch));
            Assert.Equal(arch.CollidesWith(flat), flat.CollidesWith(arch));
            Assert.Equal(upright.CollidesWith(pierced), pierced.CollidesWith(upright));

            Assert.True(arch.TryIntersectWith(plate, out GeoPoint3[] a));
            Assert.True(plate.TryIntersectWith(arch, out GeoPoint3[] b));
            Assert.Equal(a.Length, b.Length);
        }

        [Fact]
        public void EveryPairTakesAToleranceAndNothingIsAskedOfNothing()
        {
            Tolerance global = Tolerance.Global;
            GeoArc3 arch = Arching();
            GeoPolygon3 plate = Plate();
            GeoFace3 pierced = Pierced();
            var flat = new GeoTriangle3(
                new GeoPoint3(-50, -50, 0), new GeoPoint3(50, -50, 0), new GeoPoint3(0, 50, 0));
            var upright = new GeoCircle3(GeoPoint3.Origin, new GeoVector3(0, 1, 0), 50.0);

            Assert.Equal(arch.GetIntersections(plate).Length, arch.GetIntersections(plate, global).Length);
            Assert.Equal(arch.GetIntersections(pierced).Length, arch.GetIntersections(pierced, global).Length);
            Assert.Equal(arch.GetIntersections(flat).Length, arch.GetIntersections(flat, global).Length);
            Assert.Equal(upright.GetIntersections(plate).Length, upright.GetIntersections(plate, global).Length);
            Assert.Equal(arch.CollidesWith(plate), arch.CollidesWith(plate, global));
            Assert.Equal(plate.CollidesWith(arch), plate.CollidesWith(arch, global));
            Assert.Equal(pierced.CollidesWith(upright), pierced.CollidesWith(upright, global));

            Assert.Throws<ArgumentNullException>(() => Arc3.GetIntersections(arch, (GeoPolygon3)null));
            Assert.Throws<ArgumentNullException>(() => Arc3.GetIntersections(arch, (GeoFace3)null));
            Assert.Throws<ArgumentNullException>(() => Arc3.CollidesWith(arch, (GeoPolygon3)null));
            Assert.Throws<ArgumentNullException>(() => arch.GetIntersections((GeoFace3)null));
        }
    }
}

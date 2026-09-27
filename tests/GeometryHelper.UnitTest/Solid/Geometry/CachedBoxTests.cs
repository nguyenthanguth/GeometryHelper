using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A polygon and a body measure their box once, when they are made. Whatever way they were made, the box kept
    /// must be the one measuring the corners again gives.
    /// </summary>
    public class CachedBoxTests
    {
        private static GeoAabb3 Measured(GeoPolygon3 polygon) => GeoAabb3.FromPoints(polygon.Vertices);

        private static GeoAabb3 Measured(GeoSolid3 solid)
        {
            GeoAabb3 box = GeoAabb3.Empty;

            foreach (GeoFace3 face in solid.Faces)
            {
                box = box.Union(Measured(face.Boundary));
            }

            return box;
        }

        private static void AssertKept(GeoPolygon3 polygon)
        {
            Assert.Equal(Measured(polygon).Min, polygon.GetAabb().Min);
            Assert.Equal(Measured(polygon).Max, polygon.GetAabb().Max);
        }

        private static void AssertKept(GeoSolid3 solid)
        {
            Assert.Equal(Measured(solid).Min, solid.GetAabb().Min);
            Assert.Equal(Measured(solid).Max, solid.GetAabb().Max);

            foreach (GeoFace3 face in solid.Faces)
            {
                AssertKept(face.Boundary);
                Assert.Equal(Measured(face.Boundary).Min, face.GetAabb().Min);

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    AssertKept(hole);
                }
            }
        }

        [Fact]
        public void APolygonKeepsTheBoxOfItsCornersHoweverItWasMade()
        {
            var rng = new Random(3);

            for (int round = 0; round < 50; round++)
            {
                var centre = new GeoPoint3(rng.Next(-1000, 1000), rng.Next(-1000, 1000), rng.Next(-1000, 1000));
                var circle = new GeoCircle3(centre, new GeoVector3(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() + 0.1), rng.Next(1, 300));
                GeoPolygon3 polygon = circle.ToPolygon(3 + rng.Next(0, 20));

                AssertKept(polygon);
                AssertKept(polygon.Flip());
                AssertKept(polygon.Clone());
                AssertKept(new GeoPolygon3(polygon.Vertices.Reverse()));
                AssertKept(polygon.TransformBy(GeoTransform3.RotationAxis(centre, GeoVector3.ZAxis, rng.NextDouble() * 6)));
            }
        }

        [Fact]
        public void ABodyKeepsTheBoxOfItsFacesHoweverItWasMade()
        {
            GeoSolid3 box = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 60, 20)).ToObb().ToSolid();
            GeoSolid3 bar = GeoSolid3.Cylinder(new GeoPoint3(50, 30, -30), new GeoPoint3(55, 35, 50), 8, 16);
            GeoSolid3 plate = box.WithOpenings(new[] { bar });

            var bodies = new List<GeoSolid3>
            {
                box,
                bar,
                plate,
                plate.Clone(),
                box.TransformBy(GeoTransform3.RotationAxis(new GeoPoint3(50, 30, 10), new GeoVector3(1, 2, 3), 0.7)),
                GeoSolid3.Extrude(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(40, 0), new GeoPoint2(40, 10), new GeoPoint2(10, 10), new GeoPoint2(10, 40), new GeoPoint2(0, 40)),
                    new GeoCoordinateSystem3(new GeoPoint3(5, 5, 5), GeoVector3.XAxis, GeoVector3.YAxis), 30),
            };

            Assert.True(plate.TryCutOpenings(out GeoSolid3 material));
            bodies.Add(material);

            Assert.True(box.TryUnion(bar, out GeoSolid3 union));
            bodies.Add(union);
            bodies.AddRange(box.Intersect(bar));

            foreach (GeoSolid3 body in bodies)
            {
                AssertKept(body);

                foreach (GeoSolid3 opening in body.Openings)
                {
                    AssertKept(opening);
                }
            }

            // The openings take material away and do not widen the box.
            GeoSolid3 poking = box.WithOpenings(new[] { GeoSolid3.Cylinder(new GeoPoint3(50, 30, -500), new GeoPoint3(50, 30, 500), 5, 8) });
            Assert.Equal(box.GetAabb().Min, poking.GetAabb().Min);
            Assert.Equal(box.GetAabb().Max, poking.GetAabb().Max);
        }
    }
}

using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class CurveTests
    {
        private static SceneCurve Only(SceneWriter writer)
        {
            Scene scene = writer.Scene;
            return ExactGeometry.Build(scene, Primitives.Build(scene)).Curves.Single();
        }

        /// <summary>
        /// The least distance from a line to a curve found by trying a hundred thousand parameters, which the exact answer must not exceed
        /// and can only beat by what the spacing of them allows.
        /// </summary>
        private static double BruteForce(SceneCurve curve, Vec3 origin, Vec3 direction, bool forwardOnly)
        {
            double best = double.PositiveInfinity;
            const int Steps = 200000;

            for (int i = 0; i <= Steps; i++)
            {
                Vec3 p = curve.PointAt(curve.StartAngle + curve.Sweep * i / Steps);
                Vec3 w = p - origin;
                double along = Vec3.Dot(w, direction);

                if (forwardOnly && along < 0.0)
                {
                    continue;
                }

                best = Math.Min(best, (w - direction * along).Length);
            }

            return best;
        }

        [Fact]
        public void TheNearestPointOfACurveToALine_IsTheBruteForceMinimum_ForCirclesArcsAndEllipses()
        {
            var shapes = new SceneWriter()
                .Add(new GeoCircle3(new GeoPoint3(10, -20, 5), new GeoVector3(0.3, -0.5, 0.8), 60.0))
                .Add(new GeoArc3(new GeoPoint3(-30, 40, 0), new GeoVector3(1, 2, 3), 45.0, 0.4, 2.9))
                .Add(new GeoArc2(new GeoPoint2(5, 5), 30.0, 5.0, 1.0))
                .Add(new GeoEllipse3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), new GeoVector3(1, 1, 0), 80.0, 15.0));
            Scene scene = shapes.Scene;
            ExactGeometry geometry = ExactGeometry.Build(scene, Primitives.Build(scene));
            var random = new Random(11);
            int checks = 0;

            foreach (SceneCurve curve in geometry.Curves)
            {
                for (int k = 0; k < 12; k++)
                {
                    Vec3 origin = new Vec3(random.NextDouble() * 300 - 150, random.NextDouble() * 300 - 150, random.NextDouble() * 300 - 150);
                    Vec3 direction = new Vec3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5).Normalized();
                    bool forward = k % 2 == 0;

                    double exact = curve.DistanceToLine(origin, direction, forward, out Vec3 point);
                    double brute = BruteForce(curve, origin, direction, forward);

                    if (double.IsInfinity(brute))
                    {
                        Assert.True(double.IsInfinity(exact));
                        continue;
                    }

                    // The samples are a few thousandths of a millimetre apart along the longest curve, so brute force can only be over the exact answer by about that.
                    Assert.True(exact <= brute + 1E-9, "exact " + exact + " is further than the sampled " + brute);
                    Assert.True(brute - exact < 5E-3, "exact " + exact + " is far under the sampled " + brute);

                    // The point given is on the curve, and as far from the line as the distance says.
                    Vec3 w = point - origin;
                    Assert.Equal(exact, (w - direction * Vec3.Dot(w, direction)).Length, 9);
                    checks++;
                }
            }

            Assert.True(checks > 30);
        }

        [Fact]
        public void TheNearestPointOfAnEllipseToAPoint_IsTheBruteForceMinimum()
        {
            SceneCurve ellipse = Only(new SceneWriter().Add(new GeoEllipse2(new GeoPoint2(3, 4), new GeoVector2(2, 1), 50.0, 12.0)));
            var random = new Random(5);

            for (int k = 0; k < 20; k++)
            {
                Vec3 point = new Vec3(random.NextDouble() * 200 - 100, random.NextDouble() * 200 - 100, random.NextDouble() * 10 - 5);
                double brute = Enumerable.Range(0, 100001).Min(i => (ellipse.PointAt(2.0 * Math.PI * i / 100000.0) - point).Length);
                double found = (ellipse.ClosestPointTo(point) - point).Length;

                Assert.True(found <= brute + 1E-9);
                Assert.True(brute - found < 1E-3);
            }
        }

        [Fact]
        public void TheNearestPointOfAnArcToAPoint_IsAnEndWhereThePointIsOffTheEndOfTheArc()
        {
            SceneCurve arc = Only(new SceneWriter().Add(new GeoArc2(new GeoPoint2(0, 0), 10.0, 0.0, Math.PI / 2.0)));

            // Below the arc's start the nearest point is the start; beside the middle it is the radius out.
            Assert.Equal(new Vec3(10, 0, 0), arc.ClosestPointTo(new Vec3(10, -5, 0)));
            Assert.Equal(10.0, arc.ClosestPointTo(new Vec3(20, 20, 0)).Length, 12);
            Assert.Equal(new Vec3(0, 10, 0).X, arc.ClosestPointTo(new Vec3(-4, 30, 0)).X, 12);
        }

        [Fact]
        public void TheCurvesOfAScene_AreReadFromTheNumbersOfItsItems()
        {
            Scene scene = new SceneWriter()
                .Add(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10)))
                .Add(new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(20, 5) }, new[] { 0.0, 0.5, 0.0 }))
                .Add(new GeoRay2(new GeoPoint2(0, 0), new GeoVector2(1, 1)))
                .Add(new GeoPoint3(1, 2, 3))
                .Add(new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 2, 3)))
                .Scene;
            Primitives primitives = Primitives.Build(scene);

            ExactGeometry with = ExactGeometry.Build(scene, primitives);
            ExactGeometry without = ExactGeometry.Build(scene, null);

            Assert.Equal(4, with.Curves.Count(c => c.ItemIndex == 0));
            Assert.Equal(2, with.Curves.Count(c => c.ItemIndex == 1));
            Assert.Equal(CurveShape.Conic, with.Curves.Single(c => c.ItemIndex == 1 && c.Shape == CurveShape.Conic).Shape);
            Assert.Equal(12, with.Curves.Count(c => c.ItemIndex == 4));

            // A ray is drawn out to the length the primitives give it, and a ray with no primitives has no curve, only its origin.
            Assert.Single(with.Curves, c => c.ItemIndex == 2);
            Assert.DoesNotContain(without.Curves, c => c.ItemIndex == 2);
            Assert.Contains(without.Points, p => p.ItemIndex == 2 && p.At.Equals(new Vec3(0, 0, 0)));
            Assert.Contains(with.Points, p => p.ItemIndex == 3 && p.At.Equals(new Vec3(1, 2, 3)));
            Assert.Empty(with.Warnings);
        }

        [Fact]
        public void AnItemWhoseNumbersAreWrong_LeavesTheRestOfTheSceneItsCurves()
        {
            const string json = "{\"items\":[{\"name\":\"bad\",\"layer\":\"l\",\"kind\":\"GeoPolygon2\",\"data\":{\"points\":[[0,0],[1]]}},{\"name\":\"good\",\"layer\":\"l\",\"kind\":\"GeoLine2\",\"data\":{\"a\":[0,0],\"b\":[5,5]}}]}";
            Scene scene = SceneReader.ReadJson(json);

            ExactGeometry geometry = ExactGeometry.Build(scene, Primitives.Build(scene));

            Assert.Single(geometry.Curves);
            Assert.Equal(1, geometry.Curves[0].ItemIndex);
            Assert.Single(geometry.Warnings);
        }

        [Fact]
        public void ALabelIsDrawnAtItsOwnHeightWhileThatIsReadable_AndAtAReadableOneAtEitherEndOfTheZoom()
        {
            // 5 mm high: 50 pixels where a pixel is 0.1 mm, and 0.5 pixel where it is 10 mm.
            Assert.Equal(LabelSize.MaxPixels, LabelSize.Pixels(5.0, 0.1));
            Assert.Equal(25.0, LabelSize.Pixels(5.0, 0.2), 12);
            Assert.Equal(LabelSize.MinPixels, LabelSize.Pixels(5.0, 10.0));

            // No height of its own, or no pixel to measure it by: the size that suits the screen.
            Assert.Equal(LabelSize.DefaultPixels, LabelSize.Pixels(0.0, 0.1));
            Assert.Equal(LabelSize.DefaultPixels, LabelSize.Pixels(5.0, 0.0));
            Assert.Equal(LabelSize.DefaultPixels, LabelSize.Pixels(double.NaN, 0.1));
            Assert.Equal(LabelSize.DefaultPixels, LabelSize.Pixels(5.0, double.PositiveInfinity));
        }
    }
}

using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class RenderSpaceTests
    {
        private static double DrawnWidth(DrawPolyline needle, Vec3 origin)
        {
            Vec3F[] drawn = needle.Points.Select(p => RenderSpace.Relative(p, origin)).ToArray();
            return drawn.Max(p => (double)p.Y) - drawn.Min(p => (double)p.Y);
        }

        [Fact]
        public void ANeedle0_0005WideThirtyThousandFromTheCentre_IsDrawnWrongFromTheCentreAndRightOnceTheOriginMovesToIt()
        {
            // A label at the origin and a needle 20 long and 0.0005 wide far out make a scene 30 000 across.
            // Built at a point tolerance of 1E-5, which the scene keeps: at the default 1E-3 the library takes a needle this thin for a line.
            Scene scene;

            using (Tolerance.Use(new Tolerance(1E-5, 1E-7)))
            {
                scene = new SceneWriter()
                    .Text(new GeoPoint3(0, 0, 0), "origin")
                    .Add(new GeoPolygon2(new GeoPoint2(30000, 12345.6789), new GeoPoint2(30020, 12345.6789), new GeoPoint2(30020, 12345.6794), new GeoPoint2(30000, 12345.6794)), name: "needle")
                    .Scene;
            }

            Primitives primitives = Primitives.Build(scene);
            DrawPolyline needle = primitives.Items[1].Polylines.Single();
            Vec3 middleOfScene = primitives.Bounds.Center;

            // Measured from the middle of the scene a float steps by 0.00049 here: the needle comes out nought, 0.00049 or 0.00098
            // wide, never 0.0005 to the 1E-7 asked for below.
            double before = DrawnWidth(needle, middleOfScene);
            Assert.True(Math.Abs(before - 0.0005) > 1E-6, "drawn " + before);

            // Shift+F on the needle: the camera looks at it, close, and the origin has strayed too far to keep.
            var camera = new ViewCamera();
            camera.Fit(primitives.Items[1].Bounds, 1.5);
            Assert.True(RenderSpace.NeedsRebase(middleOfScene, camera, 800.0));

            double after = DrawnWidth(needle, camera.Target);
            Assert.True(Math.Abs(after - 0.0005) < 1E-7, "drawn " + after);
            Assert.False(RenderSpace.NeedsRebase(camera.Target, camera, 800.0));
        }

        [Fact]
        public void TheWholeScene_NeedsNoMoveWhileTheTargetIsNearTheOrigin()
        {
            Scene scene = new SceneWriter().Text(new GeoPoint3(0, 0, 0), "a").Add(new GeoLine2(0, 0, 60000, 100)).Scene;
            Primitives primitives = Primitives.Build(scene);
            var camera = new ViewCamera();

            camera.Fit(primitives.Bounds, 1.5);

            Assert.False(RenderSpace.NeedsRebase(primitives.Bounds.Center, camera, 800.0));
        }

        [Fact]
        public void TheDistanceAFloatIsGoodFor_IsAThousandthOfAPixelOverTwoHalfUnitsInTheLastPlace()
        {
            var camera = new ViewCamera { Distance = 1000.0 };
            double pixel = 2.0 * 1000.0 * Math.Tan(Math.PI / 8.0) / 800.0;

            Assert.Equal(pixel * 1E-3 / (2.0 / 16777216.0), RenderSpace.MaxOffset(camera, 800.0), 6);

            // At that distance the float of a coordinate is off by under a thousandth of a pixel.
            double offset = RenderSpace.MaxOffset(camera, 800.0);
            double error = Math.Abs((double)(float)(offset + 0.37) - (offset + 0.37));
            Assert.True(error <= pixel * 1E-3);
        }

        [Theory]
        [InlineData(1.0, 2)]
        [InlineData(0.1, 3)]
        [InlineData(0.01, 4)]
        [InlineData(0.00001, 7)]
        [InlineData(100000.0, 0)]
        [InlineData(1E-30, 9)]
        public void ACoordinateIsShownWithEnoughDecimalsToTellAHundredthOfAPixel(double pixel, int decimals)
        {
            Assert.Equal(decimals, RenderSpace.DecimalsFor(pixel));
        }
    }
}

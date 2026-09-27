using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The end faces of rolled sections are meshed within their outline, not across it.
    /// </summary>
    /// <remarks>
    /// Clipping the ears of an I took the flanges off first and left a corner of the web lying on the edge the
    /// last ear had cut. The test for an ear counted only corners strictly inside it, so the next ear reached
    /// across the web and the gap beside it; with nothing left to clip, the face fell back to the fan of its
    /// outline. The end of every I-column was meshed over the gaps between its flanges, four times its area, and a
    /// prepared column said a point in the plane of its end, a flange's width from the steel, lay on its surface.
    /// </remarks>
    public class EarClippingSectionTests
    {
        private static readonly Tolerance Tol = Tolerance.Global;

        private static GeoPolygon2 I(double h, double b, double tw, double tf)
        {
            double x = b / 2, y = h / 2, w = tw / 2, f = y - tf;
            return new GeoPolygon2(
                new GeoPoint2(-x, -y), new GeoPoint2(x, -y), new GeoPoint2(x, -f), new GeoPoint2(w, -f),
                new GeoPoint2(w, f), new GeoPoint2(x, f), new GeoPoint2(x, y), new GeoPoint2(-x, y),
                new GeoPoint2(-x, f), new GeoPoint2(-w, f), new GeoPoint2(-w, -f), new GeoPoint2(-x, -f));
        }

        private static GeoPolygon2 Channel(double h, double b, double tw, double tf)
            => new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(b, 0), new GeoPoint2(b, tf), new GeoPoint2(tw, tf),
                new GeoPoint2(tw, h - tf), new GeoPoint2(b, h - tf), new GeoPoint2(b, h), new GeoPoint2(0, h));

        private static GeoPolygon2 Tee(double h, double b, double tw, double tf)
            => new GeoPolygon2(
                new GeoPoint2(-b / 2, h), new GeoPoint2(-b / 2, h - tf), new GeoPoint2(-tw / 2, h - tf), new GeoPoint2(-tw / 2, 0),
                new GeoPoint2(tw / 2, 0), new GeoPoint2(tw / 2, h - tf), new GeoPoint2(b / 2, h - tf), new GeoPoint2(b / 2, h));

        private static GeoPolygon2 Angle(double a, double b, double t)
            => new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(b, 0), new GeoPoint2(b, t), new GeoPoint2(t, t), new GeoPoint2(t, a), new GeoPoint2(0, a));

        private static GeoPolygon2 Zed(double h, double b, double t)
            => new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(b, 0), new GeoPoint2(b, t), new GeoPoint2(t, t), new GeoPoint2(t, h),
                new GeoPoint2(t - b, h), new GeoPoint2(t - b, h - t), new GeoPoint2(0, h - t));

        private static IEnumerable<(string Name, GeoPolygon2 Outline)> Sections()
        {
            yield return ("I 300x300", I(300, 300, 10, 15));
            yield return ("I 400x200", I(400, 200, 8, 13));
            yield return ("I 900x300", I(900, 300, 16, 28));
            yield return ("channel", Channel(300, 90, 9, 13));
            yield return ("tee", Tee(150, 300, 10, 15));
            yield return ("angle", Angle(150, 100, 12));
            yield return ("zed", Zed(200, 75, 3));
        }

        /// <summary>
        /// Every section's end, turned any way in space: meshed within its outline, to its area exactly.
        /// </summary>
        [Fact]
        public void TheEndOfEverySectionIsMeshedWithinItsOutline()
        {
            var rng = new Random(31);

            foreach ((string name, GeoPolygon2 outline) in Sections())
            {
                for (int k = 0; k < 8; k++)
                {
                    var frame = new GeoCoordinateSystem3(
                        new GeoPoint3(rng.Next(-5000, 5000), rng.Next(-5000, 5000), rng.Next(-5000, 5000)),
                        GeoVector3.XAxis, GeoVector3.YAxis);
                    GeoSolid3 bar = GeoSolid3.Extrude(outline, frame, 1000)
                        .TransformBy(GeoTransform3.RotationAxis(frame.Origin, new GeoVector3(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5), rng.NextDouble() * 6));

                    foreach (GeoFace3 face in bar.Faces)
                    {
                        Assert.True(EarClipping.TryTriangulate(face, Tol, out GeoTriangle3[] triangles), $"{name}: the ear clipping gave up");
                        Assert.Equal(face.Area, triangles.Sum(t => t.Area), 6);
                        Assert.Equal(face.Area, face.TriangulateSurface(Tol).Sum(t => t.Area), 6);

                        foreach (GeoTriangle3 triangle in triangles)
                        {
                            Assert.NotEqual(PointLocation.OutSide, face.Locate(triangle.Centroid));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A column's end, between its flanges: a flange's width from the steel, not on it.
        /// </summary>
        [Fact]
        public void APointBetweenTheFlangesInThePlaneOfTheEndIsNotOnTheColumn()
        {
            GeoSolid3 column = GeoSolid3.Extrude(I(300, 300, 10, 15), new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis), 4000);
            GeoPreparedSolid3 prepared = column.Prepare();

            foreach (double z in new[] { 0.0, 4000.0 })
            {
                var between = new GeoPoint3(-150, 100, z);

                Assert.Equal(PointLocation.OutSide, prepared.Locate(between));
                Assert.Equal(35.0, prepared.DistanceTo(between), 9);
            }

            // A block standing clear in the gap, level with the top: nothing touches it.
            GeoSolid3 block = new GeoAabb3(new GeoPoint3(-140, -100, 3900), new GeoPoint3(-20, 100, 4000)).ToObb().ToSolid();
            Assert.False(prepared.CollidesWith(block.Prepare()));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Drawing.UnitTest
{
    public class ViewFrameTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static readonly ViewKind[] Standard = { ViewKind.Front, ViewKind.Top, ViewKind.Bottom, ViewKind.Left, ViewKind.Right, ViewKind.Back, ViewKind.Isometric };

        private static void AssertView(ViewFrame3 view, GeoPoint3 point, double x, double y, double depth)
        {
            GeoPoint2 seen = view.ToView(point, out double d);
            Assert.Equal(x, seen.X, 9);
            Assert.Equal(y, seen.Y, 9);
            Assert.Equal(depth, d, 9);
        }

        [Theory]
        [InlineData(ViewKind.Front, 30, 20, 10)]
        [InlineData(ViewKind.Back, -30, 20, -10)]
        [InlineData(ViewKind.Top, 30, -10, 20)]
        [InlineData(ViewKind.Bottom, 30, 10, -20)]
        [InlineData(ViewKind.Left, 10, 20, -30)]
        [InlineData(ViewKind.Right, -10, 20, 30)]
        public void TheFarCornerOfABoxComesOutWhereADraughtsmanDrawsItInEachViewAlongTheAxes(ViewKind kind, double x, double y, double depth)
        {
            // A box 30 by 20 by 10 from the origin of the world, drawn in the world's frame: X along its 30, Y up its 20,
            // its front at Z = 10. Its near corner stays at the origin of every view.
            ViewFrame3 view = StandardViews.GetFrame(GeoCoordinateSystem3.Global, kind);

            AssertView(view, new GeoPoint3(30, 20, 10), x, y, depth);
            AssertView(view, GeoPoint3.Origin, 0, 0, 0);
        }

        [Fact]
        public void InTheTopViewTheFrontOfTheBoxIsAtTheBottomAndInTheRightViewOnTheLeft()
        {
            // Third-angle: each view's side next to the front view is the box's front, Z = 10.
            ViewFrame3 top = StandardViews.GetFrame(GeoCoordinateSystem3.Global, ViewKind.Top);
            ViewFrame3 right = StandardViews.GetFrame(GeoCoordinateSystem3.Global, ViewKind.Right);

            Assert.True(top.ToView(new GeoPoint3(0, 0, 10), out _).Y < top.ToView(GeoPoint3.Origin, out _).Y);
            Assert.True(right.ToView(new GeoPoint3(0, 0, 10), out _).X < right.ToView(GeoPoint3.Origin, out _).X);
        }

        [Fact]
        public void EveryStandardViewIsRightHandedAndShowsTheFaceTurnedToItAnticlockwiseAsFromOutside()
        {
            GeoSolid3 box = GeoSolid3.Extrude(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(30, 0), new GeoPoint2(30, 20), new GeoPoint2(0, 20)), GeoCoordinateSystem3.Global, 10, Tolerance);
            var skew = new GeoCoordinateSystem3(new GeoPoint3(7, -3, 2), new GeoVector3(1, 2, 3), new GeoVector3(-2, 1, 0));

            foreach (GeoCoordinateSystem3 frame in new[] { GeoCoordinateSystem3.Global, skew })
            {
                GeoSolid3 placed = box.TransformBy(GeoTransform3.FromCoordinateSystem(frame));

                foreach (ViewKind kind in Standard)
                {
                    ViewFrame3 view = StandardViews.GetFrame(frame, kind);
                    GeoCoordinateSystem3 system = view.CoordinateSystem;

                    Assert.True(system.IsValid, kind.ToString());
                    Assert.True(system.XAxis.CrossProduct(system.YAxis).IsEqualTo(system.ZAxis, Tolerance), kind.ToString());
                    Assert.True(view.Direction.IsEqualTo(system.ZAxis.Negate(), Tolerance), kind.ToString());

                    // Every face turned towards the viewer, its outline wound outwards, runs anticlockwise in the view.
                    GeoFace3[] facing = placed.Faces.Where(f => f.Normal.DotProduct(system.ZAxis) > Tolerance.EqualVector).ToArray();
                    Assert.NotEmpty(facing);
                    foreach (GeoFace3 face in facing)
                    {
                        var outline = new GeoPolygon2(face.Boundary.Vertices.Select(p => view.ToView(p, out _)));
                        Assert.True(outline.SignedArea > 0, kind + " " + face);
                    }
                }
            }
        }

        [Fact]
        public void TheIsometricViewShowsTheLengthDownToTheRightAndTheFrontDownToTheLeftAt30Degrees()
        {
            ViewFrame3 view = StandardViews.GetFrame(GeoCoordinateSystem3.Global, ViewKind.Isometric);

            GeoPoint2 along = view.ToView(new GeoPoint3(100, 0, 0), out _);
            GeoPoint2 front = view.ToView(new GeoPoint3(0, 0, 100), out _);
            GeoPoint2 up = view.ToView(new GeoPoint3(0, 100, 0), out _);

            Assert.Equal(-Math.Tan(Math.PI / 6), along.Y / along.X, 9);
            Assert.True(along.X > 0);
            Assert.Equal(Math.Tan(Math.PI / 6), front.Y / front.X, 9);
            Assert.True(front.X < 0);
            Assert.Equal(0.0, up.X, 9);
            Assert.True(up.Y > 0);
            Assert.True(view.Direction.IsEqualTo(new GeoVector3(-1, -1, -1).Normalize(), Tolerance));
        }

        [Theory]
        [InlineData(ViewKind.Section)]
        [InlineData(ViewKind.Detail)]
        [InlineData(ViewKind.Free)]
        public void ASectionADetailAndAFreeViewHaveNoFrameOfTheirOwn(ViewKind kind)
        {
            Assert.Throws<ArgumentException>(() => StandardViews.GetFrame(GeoCoordinateSystem3.Global, kind));
            Assert.Throws<ArgumentException>(() => StandardViews.GetSlot(kind, ProjectionMethod.ThirdAngle, out _, out _));
        }

        [Theory]
        [InlineData(ViewKind.Front, 0, 0, 0, 0)]
        [InlineData(ViewKind.Top, 0, 1, 0, -1)]
        [InlineData(ViewKind.Bottom, 0, -1, 0, 1)]
        [InlineData(ViewKind.Left, -1, 0, 1, 0)]
        [InlineData(ViewKind.Right, 1, 0, -1, 0)]
        [InlineData(ViewKind.Back, 2, 0, 2, 0)]
        public void ThirdAnglePutsEachViewOnTheSideItLooksFromAndFirstAngleOnTheOther(ViewKind kind, int thirdColumn, int thirdRow, int firstColumn, int firstRow)
        {
            StandardViews.GetSlot(kind, default(ProjectionMethod), out int column, out int row);
            Assert.Equal(thirdColumn, column);
            Assert.Equal(thirdRow, row);

            StandardViews.GetSlot(kind, ProjectionMethod.FirstAngle, out column, out row);
            Assert.Equal(firstColumn, column);
            Assert.Equal(firstRow, row);
        }

        [Fact]
        public void TheDefaultProjectionMethodIsThirdAngle()
        {
            Assert.Equal(ProjectionMethod.ThirdAngle, default(ProjectionMethod));
        }

        [Fact]
        public void ASectionLooksAlongItsDirectionAndSeesOnlyBehindItsCutAsDeepAsItIsSet()
        {
            // A section across a beam along X, cut at 1 000 and looking back towards the origin, 200 deep.
            var section = new ViewFrame3(new GeoPoint3(1000, 0, 0), new GeoVector3(-5, 0, 0), GeoVector3.ZAxis, 0, 200, Tolerance);

            Assert.True(section.Direction.IsEqualTo(GeoVector3.XAxis.Negate(), Tolerance));
            Assert.True(section.CutPlane.Normal.IsEqualTo(GeoVector3.XAxis, Tolerance));
            AssertView(section, new GeoPoint3(900, 50, 120), 50, 120, -100);

            Assert.True(section.IsWithinDepth(-100, Tolerance));
            Assert.True(section.IsWithinDepth(0.0005, Tolerance));
            Assert.False(section.IsWithinDepth(0.002, Tolerance));
            Assert.True(section.IsWithinDepth(-200.0005, Tolerance));
            Assert.False(section.IsWithinDepth(-200.002, Tolerance));
            Assert.True(StandardViews.GetFrame(GeoCoordinateSystem3.Global, ViewKind.Front).IsWithinDepth(-1E9, Tolerance));
        }

        [Fact]
        public void AnUpNotSquareToTheDirectionIsTakenAsItsPartSquareToIt()
        {
            var view = new ViewFrame3(GeoPoint3.Origin, GeoVector3.YAxis, new GeoVector3(0, 3, 4), double.PositiveInfinity, double.PositiveInfinity, Tolerance);

            Assert.True(view.CoordinateSystem.YAxis.IsEqualTo(GeoVector3.ZAxis, Tolerance));
            Assert.True(view.CoordinateSystem.XAxis.IsEqualTo(GeoVector3.XAxis, Tolerance));
        }

        [Fact]
        public void ANoDirectionAnUpAlongItOrANegativeDepthIsRefused()
        {
            Assert.Throws<ArgumentException>(() => new ViewFrame3(GeoPoint3.Origin, GeoVector3.Zero, GeoVector3.ZAxis, 0, 0, Tolerance));
            Assert.Throws<ArgumentException>(() => new ViewFrame3(GeoPoint3.Origin, GeoVector3.XAxis, new GeoVector3(-2, 1E-9, 0), 0, 0, Tolerance));
            Assert.Throws<ArgumentException>(() => new ViewFrame3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.Zero, 0, 0, Tolerance));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ViewFrame3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.ZAxis, -1, 0, Tolerance));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ViewFrame3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.ZAxis, 0, double.NaN, Tolerance));
        }

        [Fact]
        public void TheViewsOfTC2AreTheSameWhicheverWayItLiesInTheModel()
        {
            TestBody lying = TestBodies.TC2();
            List<GeoPoint3>[] expected = Standard.Select(k => Seen(lying, k)).ToArray();

            var placements = new[]
            {
                new GeoCoordinateSystem3(new GeoPoint3(0, 5000, 0), GeoVector3.YAxis, GeoVector3.XAxis.Negate()),       // along Y
                new GeoCoordinateSystem3(new GeoPoint3(-200, 0, 3000), GeoVector3.ZAxis, GeoVector3.XAxis),             // standing along Z
                new GeoCoordinateSystem3(new GeoPoint3(12345, -6789, 432), new GeoVector3(1, 2, 3), new GeoVector3(-2, 1, 0)), // skew
            };

            foreach (GeoCoordinateSystem3 placement in placements)
            {
                TestBody moved = lying.MovedTo(placement);

                for (int k = 0; k < Standard.Length; k++)
                {
                    List<GeoPoint3> seen = Seen(moved, Standard[k]);
                    Assert.Equal(expected[k].Count, seen.Count);

                    for (int i = 0; i < seen.Count; i++)
                    {
                        Assert.True(seen[i].IsEqualTo(expected[k][i], Tolerance), Standard[k] + ": " + seen[i] + " against " + expected[k][i]);
                    }
                }
            }
        }

        [Fact]
        public void TheFrontViewOfTC2ShowsTheBeamAcrossTheSheetAndItsEndPlateAtItsRight()
        {
            DrawingSource source = TestBodies.TC2().MovedTo(new GeoCoordinateSystem3(new GeoPoint3(0, 5000, 0), GeoVector3.YAxis, GeoVector3.ZAxis)).ToSource();
            ViewFrame3 front = StandardViews.GetFrame(source.Frame, ViewKind.Front);

            source.TryGetObject("TC2-beam", out DrawingObject beam);
            source.TryGetObject("TC2-endplate", out DrawingObject plate);

            AssertBox(front, beam, 0, -150, 3000, 150);
            AssertBox(front, plate, 3000, -170, 3020, 170);
        }

        private static void AssertBox(ViewFrame3 view, DrawingObject item, double minX, double minY, double maxX, double maxY)
        {
            GeoPoint2[] seen = item.Solid.Faces.SelectMany(f => f.Boundary.Vertices).Select(p => view.ToView(p, out _)).ToArray();
            Assert.Equal(minX, seen.Min(p => p.X), 6);
            Assert.Equal(minY, seen.Min(p => p.Y), 6);
            Assert.Equal(maxX, seen.Max(p => p.X), 6);
            Assert.Equal(maxY, seen.Max(p => p.Y), 6);
        }

        /// <summary>Every corner of every face of a body, in the order of its objects and faces, taken into one of its views as (x, y, depth).</summary>
        private static List<GeoPoint3> Seen(TestBody body, ViewKind kind)
        {
            DrawingSource source = body.ToSource();
            ViewFrame3 view = StandardViews.GetFrame(source.Frame, kind);
            var seen = new List<GeoPoint3>();

            foreach (DrawingObject item in source.Objects)
            {
                foreach (GeoFace3 face in item.Solid.Faces)
                {
                    foreach (GeoPoint3 corner in face.Boundary.Vertices.Concat(face.Holes.SelectMany(h => h.Vertices)))
                    {
                        GeoPoint2 p = view.ToView(corner, out double depth);
                        seen.Add(new GeoPoint3(p.X, p.Y, depth));
                    }
                }
            }

            return seen;
        }
    }
}

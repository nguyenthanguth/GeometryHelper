using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A shape with a coordinate that is not a finite number meets nothing: <c>GetIntersections</c> gives no points and
    /// the <c>Try</c> members answer false, where they gave points of NaN, in the plane and in space. Found by a reflection
    /// fuzz of every public <c>Try</c> and <c>GetIntersections</c> member of the geometry types, one shape given a NaN or an
    /// infinite coordinate: 352 members gave NaN, none now.
    /// </summary>
    public class ShapesOfNaNMeetNothingTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-3, 1E-5);

        private static readonly GeoPolygon3 Floor = new GeoPolygon3(
            new GeoPoint3(0, 0, 0),
            new GeoPoint3(100, 0, 0),
            new GeoPoint3(100, 100, 0),
            new GeoPoint3(0, 100, 0)
        );

        [Fact]
        public void ASegmentOfNaN_CrossesNoSideInThePlane()
        {
            // The sides of a triangle, a rectangle, a polygon and a face were each given a point of NaN.
            var line = new GeoLine2(new GeoPoint2(double.NaN, 20), new GeoPoint2(150, 30));
            var triangle = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(0, 100));
            var square = new GeoPolygon2(
                new GeoPoint2(0, 0),
                new GeoPoint2(100, 0),
                new GeoPoint2(100, 100),
                new GeoPoint2(0, 100)
            );

            Assert.Empty(line.GetIntersections(triangle, Tol));
            Assert.Empty(line.GetIntersections(square, Tol));
            Assert.Empty(line.GetIntersections(new GeoFace2(square), Tol));
            Assert.Empty(line.GetIntersections(new GeoRectangle2(new GeoPoint2(50, 50), 100, 100), Tol));
            Assert.Empty(
                new GeoCircle2(new GeoPoint2(double.NaN, 0), 30).GetIntersections(
                    new GeoCircle2(new GeoPoint2(20, 0), 30),
                    Tol
                )
            );
        }

        [Fact]
        public void ASegmentARayOrAPlaneOfNaN_MeetsNothingInSpace()
        {
            var nan = new GeoLine3(new GeoPoint3(double.NaN, 20, -5), new GeoPoint3(40, 30, 5));
            var across = new GeoLine3(new GeoPoint3(20, 20, -5), new GeoPoint3(40, 30, 5));
            var ray = new GeoRay3(new GeoPoint3(20, 20, double.NaN), new GeoVector3(0, 0, -1));
            var tilted = new GeoPlane3(new GeoPoint3(double.NaN, 0, 0), new GeoVector3(0, 0, 1));
            var triangle = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0));

            Assert.Empty(nan.GetIntersections(GeoPlane3.XY, Tol));
            Assert.Empty(nan.GetIntersections(triangle, Tol));
            Assert.Empty(nan.GetIntersections(Floor, Tol));
            Assert.Empty(nan.GetIntersections(across, Tol));
            Assert.Empty(across.GetIntersections(tilted, Tol));
            Assert.False(GeoPlane3.XY.TryIntersectWith(ray, out GeoPoint3 _, Tol));
            Assert.False(tilted.TryIntersectWith(across, out GeoPoint3 _, Tol));
            Assert.Empty(tilted.GetIntersections(triangle, Tol));

            // A finite one still crosses.
            Assert.Single(across.GetIntersections(GeoPlane3.XY, Tol));
        }

        [Fact]
        public void AnArcOrAnEllipseAboutACentreOfNaN_MeetsNothing()
        {
            var arc = new GeoArc3(new GeoPoint3(double.NaN, 50, 0), new GeoVector3(0, 1, 0), 30, 0.3, 2.8);
            var circle = new GeoCircle3(new GeoPoint3(50, double.NaN, 0), new GeoVector3(0, 1, 0), 30);
            var ellipse = new GeoEllipse3(
                new GeoPoint3(50, 50, double.NaN),
                new GeoVector3(0, 1, 0),
                new GeoVector3(1, 0, 0),
                40,
                20
            );

            Assert.Empty(arc.GetIntersections(Floor, Tol));
            Assert.Empty(circle.GetIntersections(new GeoFace3(Floor), Tol));
            Assert.False(circle.TryIntersectWith(Floor, out GeoPoint3[] _, Tol));
            Assert.Empty(ellipse.GetIntersections(GeoPlane3.XY, Tol));
        }

        [Fact]
        public void ASplineAndAChainWithACornerAtInfinity_MeetNowhere_RatherThanHang()
        {
            // Under .NET Framework 4.8 the search for where they meet never ended, for a chain or a triangle of NaN or
            // infinity; under .NET 10 it gave nothing.
            GeoSpline2 spline = GeoSpline2.FromControlPoints(
                3,
                new[] { new GeoPoint2(0, 0), new GeoPoint2(30, 60), new GeoPoint2(60, -20), new GeoPoint2(100, 40) }
            );
            var chain = new GeoPolyline2(
                new GeoPoint2(double.PositiveInfinity, 10),
                new GeoPoint2(50, 10),
                new GeoPoint2(50, 90)
            );
            var triangle = new GeoTriangle2(new GeoPoint2(double.NaN, 0), new GeoPoint2(100, 0), new GeoPoint2(0, 100));

            Assert.Empty(spline.GetIntersections(chain, Tol));
            Assert.Empty(spline.GetIntersections(triangle, Tol));
            Assert.False(spline.TryIntersectWith(chain, out GeoPoint2[] _, Tol));

            // A finite chain still meets it.
            Assert.NotEmpty(
                spline.GetIntersections(new GeoPolyline2(new GeoPoint2(50, -50), new GeoPoint2(50, 90)), Tol)
            );
        }

        [Fact]
        public void TheShapingTryMembers_AnswerFalseForAShapeOfNaN()
        {
            var line = new GeoLine3(new GeoPoint3(0, 0, double.NaN), new GeoPoint3(100, 0, 0));
            var chain = new GeoPolyline3(
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, double.NaN, 0),
                new GeoPoint3(100, 100, 0)
            );
            var arc = new GeoArc2(new GeoPoint2(0, 0), 30, 0.3, 2.8);

            Assert.False(line.TrySplitAtDistance(30, out GeoLine3[] _, Tol));
            Assert.False(
                line.TryExtendTo(
                    new GeoPlane3(new GeoPoint3(200, 0, 0), new GeoVector3(1, 0, 0)),
                    LineEnd.End,
                    out _,
                    Tol
                )
            );
            Assert.False(line.TryExtendTo(new GeoPoint3(200, 0, 0), LineEnd.End, out _, Tol));
            Assert.False(chain.TrySplitAtDistance(30, out GeoPolyline3[] _, Tol));
            Assert.False(chain.TryChamferAt(1, 5, 5, out GeoPolyline3 _, Tol));
            Assert.False(
                new GeoPolylineArc3(chain).TrySplitBy(
                    new GeoPlane3(new GeoPoint3(50, 0, 0), new GeoVector3(1, 0, 0)),
                    out GeoPolylineArc3[] _,
                    Tol
                )
            );
            Assert.False(new GeoArc2(new GeoPoint2(double.NaN, 0), 30, 0.3, 2.8).TryOffset(5, out GeoArc2 _, Tol));
            Assert.False(arc.TryOffset(double.NaN, out GeoArc2 _, Tol));
            Assert.False(
                new GeoCircle3(new GeoPoint3(double.NaN, 0, 0), new GeoVector3(0, 0, 1), 30).TryOffset(
                    5,
                    out GeoCircle3 _,
                    Tol
                )
            );
            Assert.False(
                new GeoTriangle3(
                    new GeoPoint3(0, 0, 0),
                    new GeoPoint3(100, 0, 0),
                    new GeoPoint3(0, 100, 0)
                ).TryGetBarycentric(new GeoPoint3(double.NaN, 5, 0), out _, out _, out _)
            );

            // Finite ones answer as before.
            Assert.True(arc.TryOffset(5, out GeoArc2 grown, Tol));
            Assert.Equal(35.0, grown.Radius, 12);
        }
    }
}

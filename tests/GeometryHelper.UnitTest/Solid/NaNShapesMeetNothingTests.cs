using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The part of a shape that has a coordinate that is not a finite number meets nothing: it does not collide, it is not
    /// nought from anything, it holds no point and it is not the nearest of anything; where nothing of the shape is left to
    /// answer, a distance is not a number and a body is the empty one. Found by a reflection fuzz that made one
    /// argument, or all of one shape, NaN.
    /// </summary>
    public class NaNShapesMeetNothingTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-3, 1E-5);

        private const string NotFinite = "The shape has a coordinate that is not a finite number.";

        private static readonly GeoPoint2 NaN2 = new GeoPoint2(double.NaN, double.NaN);

        private static readonly GeoPoint3 NaN3 = new GeoPoint3(double.NaN, double.NaN, double.NaN);

        private static readonly GeoLine2 Across = new GeoLine2(new GeoPoint2(0, 50), new GeoPoint2(100, 50));

        private static readonly GeoPolygon2 Square = new GeoPolygon2(
            new GeoPoint2(0, 0),
            new GeoPoint2(100, 0),
            new GeoPoint2(100, 100),
            new GeoPoint2(0, 100)
        );

        private static readonly GeoPolygon2 SquareOfNaN = new GeoPolygon2(NaN2, NaN2, NaN2, NaN2);

        private static readonly GeoPolygon2 SquareWithACornerOfNaN = new GeoPolygon2(
            new GeoPoint2(double.NaN, 0),
            new GeoPoint2(100, 0),
            new GeoPoint2(100, 100),
            new GeoPoint2(0, 100)
        );

        [Fact]
        public void ASegmentOfNaN_CollidesWithNothing_AndIsNotANumberAway()
        {
            // The two directions' cross product and the parameters of the crossing were NaN, and no test of them said no:
            // the segments were nought apart.
            var ofNaN = new GeoLine2(NaN2, NaN2);

            Assert.False(Collision2.CollidesWith(ofNaN, Across, Tol));
            Assert.False(Collision2.CollidesWith(Across, ofNaN, Tol));
            Assert.True(double.IsNaN(Distance2.DistanceTo(ofNaN, Across, Tol)));
            Assert.False(Collision2.CollidesWith(Square, ofNaN, Tol));
            Assert.False(Collision2.CollidesWith(SquareOfNaN, Across, Tol));

            // The segment across the square still collides with it, and is nought from it.
            Assert.True(Collision2.CollidesWith(Square, Across, Tol));
            Assert.Equal(0.0, Distance2.DistanceTo(Square, Across), 12);
        }

        [Fact]
        public void APolygonOfNaN_IsNotANumberAwayFromAnArc_NotTheLargestNumber()
        {
            var arc = new GeoArc2(new GeoPoint2(50, 50), 30, 0.3, 2.8);
            var arcOfNaN = new GeoArc2(NaN2, 30, 0.3, 2.8);

            Assert.True(double.IsNaN(Distance2.DistanceTo(SquareOfNaN, arc, Tol)));
            Assert.True(double.IsNaN(Distance2.DistanceTo(Square, arcOfNaN, Tol)));
            Assert.True(double.IsNaN(Distance2.DistanceTo(SquareOfNaN, Across)));
        }

        [Fact]
        public void AFaceOfNaN_IsNotNoughtAwayFromASegment()
        {
            var face = new GeoFace2(SquareOfNaN);

            Assert.True(double.IsNaN(Face2.DistanceTo(face, Across, Tol)));
            Assert.True(double.IsNaN(Face2.DistanceTo(new GeoFace2(Square), new GeoLine2(NaN2, NaN2), Tol)));
            Assert.Equal(0.0, Face2.DistanceTo(new GeoFace2(Square), Across, Tol), 12);
        }

        [Fact]
        public void ABoxOfNaN_CollidesWithNoBox_AndIsNotANumberAway()
        {
            var box = new GeoObb3(
                new GeoPoint3(50, 50, 0),
                60,
                40,
                30,
                new GeoVector3(1, 0.2, 0),
                new GeoVector3(-0.2, 1, 0)
            );
            var boxOfNaN = new GeoObb3(NaN3, 60, 40, 30, new GeoVector3(1, 0.2, 0), new GeoVector3(-0.2, 1, 0));

            Assert.False(Collision3.CollidesWith(boxOfNaN, box, Tol));
            Assert.False(Collision3.CollidesWith(box, boxOfNaN, Tol));
            Assert.True(double.IsNaN(Distance3.DistanceTo(boxOfNaN, box, Tol)));
            Assert.True(Collision3.CollidesWith(box, box, Tol));
        }

        [Fact]
        public void APlaneOfNaN_IsNotANumberAwayFromAPlane()
        {
            var plane = new GeoPlane3(new GeoPoint3(10, 20, 5), new GeoVector3(0.1, 0.2, 1));
            var tilted = new GeoPlane3(NaN3, new GeoVector3(0.3, 0.2, 1));

            Assert.True(double.IsNaN(Distance3.DistanceTo(tilted, plane, Tol)));
            Assert.True(double.IsNaN(Distance3.DistanceTo(plane, tilted, Tol)));
            Assert.Equal(
                0.0,
                Distance3.DistanceTo(plane, new GeoPlane3(plane.Origin, new GeoVector3(0.3, 0.2, 1)), Tol),
                12
            );
        }

        [Fact]
        public void AnEllipseOfNaN_TouchesNothing_AndIsNotANumberAway()
        {
            var ellipse = new GeoEllipse2(new GeoPoint2(50, 50), new GeoVector2(1, 0.2), 40, 20);
            var ofNaN = new GeoEllipse2(NaN2, new GeoVector2(1, 0.2), 40, 20);

            Assert.False(Ellipse2.CollidesWith(ofNaN, ellipse, Tol));
            Assert.False(Ellipse2.CollidesWith(ellipse, ofNaN, Tol));
            Assert.True(double.IsNaN(Ellipse2.DistanceTo(ofNaN, ellipse, Tol)));
            Assert.True(Ellipse2.CollidesWith(ellipse, ellipse, Tol));

            var inSpace = new GeoEllipse3(NaN3, new GeoVector3(0, 0, 1), new GeoVector3(1, 0.2, 0), 40, 20);

            Assert.False(
                Ellipse3.CollidesWith(inSpace, new GeoPlane3(new GeoPoint3(50, 50, 0), new GeoVector3(1, 0, 0)), Tol)
            );
        }

        [Fact]
        public void ALoopOfArcsOfNaN_HoldsNoPoint()
        {
            // The piece an arc cuts off its chord was found to hold the point when every test of it failed.
            var loop = new GeoPolygonArc2(new List<GeoPoint2> { NaN2, NaN2, NaN2, NaN2 }, new[] { 0.0, 0.3, 0.0, 0.0 });
            var point = new GeoPoint2(50, 50);

            Assert.False(Containment2.Contains(loop, point, Tol));
            Assert.Equal(PointLocation.OutSide, Containment2.Locate(loop, point, Tol));
            Assert.False(Containment2.Contains(loop, NaN2, Tol));
        }

        [Fact]
        public void APolygonWithACornerOfNaN_HoldsNoPoint_ButItsFiniteSidesAreStillOn()
        {
            // Counting the crossings of the sides that were left put (-10, 50), a long way outside the square, inside it.
            Assert.False(Containment2.Contains(SquareWithACornerOfNaN, new GeoPoint2(-10, 50), Tol));
            Assert.False(Containment2.Contains(SquareWithACornerOfNaN, new GeoPoint2(50, 50), Tol));
            Assert.Equal(
                PointLocation.OutSide,
                Containment2.Locate(SquareWithACornerOfNaN, new GeoPoint2(50, 50), Tol)
            );

            // The side from (100, 0) to (100, 100) is finite, and a point on it is on it.
            Assert.Equal(
                PointLocation.OnSide,
                Containment2.Locate(SquareWithACornerOfNaN, new GeoPoint2(100, 50), Tol)
            );

            // The square without the NaN holds what it held.
            Assert.True(Containment2.Contains(Square, new GeoPoint2(50, 50), Tol));
            Assert.False(Containment2.Contains(Square, new GeoPoint2(-10, 50), Tol));
        }

        [Fact]
        public void ARegionWithAVertexOfNaN_CoversNothingInTheBooleans()
        {
            // A vertex of NaN was laid on Clipper2's grid as the origin of the frame, and the booleans worked with a corner
            // that was nowhere in the shape.
            var other = new GeoPolygon2(
                new GeoPoint2(50, 50),
                new GeoPoint2(150, 50),
                new GeoPoint2(150, 150),
                new GeoPoint2(50, 150)
            );

            Assert.Empty(Boolean2.Intersect(other, SquareWithACornerOfNaN, Tol));
            Assert.Empty(Boolean2.Intersect(SquareWithACornerOfNaN, other, Tol));

            // Nothing taken out of the other square, nothing added to it, and nothing of it left where the square is the NaN.
            GeoFace2[] subtracted = Boolean2.Subtract(other, SquareWithACornerOfNaN, Tol);
            GeoFace2[] united = Boolean2.Union(other, SquareWithACornerOfNaN, Tol);

            Assert.Single(subtracted);
            Assert.Equal(10000.0, subtracted[0].Area, 6);
            Assert.Single(united);
            Assert.Equal(10000.0, united[0].Area, 6);
            Assert.Empty(Boolean2.Subtract(SquareWithACornerOfNaN, other, Tol));

            // The finite pair overlaps in 2 500.
            Assert.Equal(2500.0, Boolean2.Intersect(other, Square, Tol)[0].Area, 6);
        }

        [Fact]
        public void ATriangleOfNaNInSpace_MeetsNoArc()
        {
            var triangle = new GeoTriangle3(NaN3, NaN3, NaN3);
            var arc = new GeoArc3(new GeoPoint3(50, 50, 0), new GeoVector3(0, 0, 1), 30, 0.3, 2.8);

            Assert.Empty(Arc3.GetIntersections(arc, triangle, Tol));
            Assert.False(Arc3.TryIntersectWith(arc, triangle, out GeoPoint3[] found, Tol));
            Assert.Empty(found);
        }

        [Fact]
        public void ABallOrAPlaneOfNaN_HasNoSegmentOfNoLengthWhereItWouldMeet()
        {
            var ball = new GeoSphere3(NaN3, 30);
            var point = new GeoPoint3(10, 20, 5);
            var ray = new GeoRay3(new GeoPoint3(0, 0, -10), new GeoVector3(1, 0.6, 0.3));
            var chain = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 10), new GeoPoint3(100, 0, -10));
            var plane = new GeoPlane3(NaN3, new GeoVector3(0, 0, 1));

            Assert.True(double.IsNaN(ball.GetShortestLineTo(point, Tol).Length));
            Assert.True(double.IsNaN(Projection3.GetShortestLineTo(ray, plane, Tol).Length));
            Assert.True(double.IsNaN(Projection3.GetShortestLineTo(chain, plane, Tol).Length));

            // A finite pair still meets: the chain starts on the floor, at the origin.
            GeoLine3 floor = Projection3.GetShortestLineTo(chain, GeoPlane3.XY, Tol);

            Assert.Equal(0.0, floor.Length, 12);
        }

        [Fact]
        public void TheClosestEdgeToAPointOfNaN_NamesThePoint_AndOfAShapeOfNaN_SaysWhatIsNotFinite()
        {
            ArgumentException refused = Assert.Throws<ArgumentException>(() =>
                ClosestEdge2.GetClosestEdge(Square, NaN2)
            );

            Assert.Equal("point", refused.ParamName);
            Assert.Equal(
                NotFinite,
                Assert
                    .Throws<InvalidOperationException>(() =>
                        ClosestEdge2.GetClosestEdge(SquareOfNaN, new GeoPoint2(5, 5))
                    )
                    .Message
            );

            // A corner of NaN leaves two sides of NaN and two that are finite: the nearer of those is the edge.
            GeoLine2 nearest = ClosestEdge2.GetClosestEdge(SquareWithACornerOfNaN, new GeoPoint2(95, 50));

            Assert.Equal(100.0, nearest.StartPoint.X, 12);
            Assert.Equal(100.0, nearest.EndPoint.X, 12);
        }

        [Fact]
        public void ADirectionAlongASegment_IsNamedAsTheDirection_NotAsANormalNobodyGave()
        {
            var line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));

            ArgumentException refused = Assert.Throws<ArgumentException>(() =>
                line.GetPlaneWith(new GeoVector3(1, 0, 0))
            );

            Assert.Equal("inPlaneDirection", refused.ParamName);
            Assert.Contains("along the segment", refused.Message);
            Assert.Equal(
                "inPlaneDirection",
                Assert.Throws<ArgumentException>(() => line.GetPlaneWith(new GeoVector3(-3, 0, 0))).ParamName
            );
            Assert.Equal(1.0, line.GetPlaneWith(new GeoVector3(0, 1, 0)).Normal.Z, 12);
        }

        [Fact]
        public void WhatANonFiniteShapeMakesOfItself_ThrowsTheOneMessage()
        {
            var spline = GeoSpline3.FromControlPoints(
                3,
                new[]
                {
                    new GeoPoint3(0, 0, 0),
                    new GeoPoint3(30, 60, 0),
                    new GeoPoint3(60, -20, 10),
                    new GeoPoint3(100, 40, 0),
                }
            );
            var flat = GeoSpline2.FromControlPoints(
                3,
                new[] { new GeoPoint2(0, 0), new GeoPoint2(30, 60), new GeoPoint2(60, -20), new GeoPoint2(100, 40) }
            );
            var circle = new GeoCircle3(NaN3, new GeoVector3(0, 0, 1), 30);
            var ellipse = new GeoEllipse2(NaN2, new GeoVector2(1, 0.2), 40, 20);
            var triangle = new GeoTriangle3(NaN3, new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0));
            var box = new GeoObb3(NaN3, 60, 40, 30, new GeoVector3(1, 0.2, 0), new GeoVector3(-0.2, 1, 0));
            var chain = new GeoPolyline3(
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                new GeoPoint3(double.PositiveInfinity, 100, 0)
            );

            Assert.Equal(
                NotFinite,
                Assert.Throws<InvalidOperationException>(() => spline.GetShortestLineTo(circle)).Message
            );
            Assert.Equal(
                NotFinite,
                Assert.Throws<InvalidOperationException>(() => flat.GetShortestLineTo(ellipse)).Message
            );
            Assert.Equal(NotFinite, Assert.Throws<InvalidOperationException>(() => triangle.GetPlane()).Message);
            Assert.Equal(NotFinite, Assert.Throws<InvalidOperationException>(() => box.GetFaces()).Message);
            Assert.Equal(
                NotFinite,
                Assert.Throws<InvalidOperationException>(() => new GeoVector3(double.NaN, 1, 0).Normalize()).Message
            );
            Assert.Equal(
                NotFinite,
                Assert.Throws<InvalidOperationException>(() => chain.OffsetInPlane(5, new GeoVector3(0, 0, 1))).Message
            );

            // A vector of no length is still said to have none.
            Assert.Contains(
                "zero-length",
                Assert.Throws<InvalidOperationException>(() => new GeoVector3(0, 0, 0).Normalize()).Message
            );
        }

        [Fact]
        public void AnArgumentThatIsNotFinite_IsRefusedByItsOwnName()
        {
            GeoTransform3 ofNaN = GeoTransform3.Translation(new GeoVector3(double.NaN, 0, 0));
            var plane = new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1));
            var circle = new GeoCircle3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 30);
            var frame = new GeoCoordinateSystem3(NaN3, new GeoVector3(1, 0, 0), new GeoVector3(0, 1, 0));
            var spline = GeoSpline3.FromControlPoints(
                3,
                new[]
                {
                    new GeoPoint3(0, 0, 0),
                    new GeoPoint3(30, 60, 0),
                    new GeoPoint3(60, -20, 10),
                    new GeoPoint3(100, 40, 0),
                }
            );

            Assert.Equal("transform", Assert.Throws<ArgumentException>(() => plane.TransformBy(ofNaN)).ParamName);
            Assert.Equal("transform", Assert.Throws<ArgumentException>(() => circle.TransformBy(ofNaN)).ParamName);
            Assert.Equal("frame", Assert.Throws<ArgumentException>(() => spline.ProjectToSpline2(frame)).ParamName);
            Assert.Equal(
                "line",
                Assert
                    .Throws<ArgumentException>(() => GeoTransform2.Mirror(new GeoLine2(NaN2, new GeoPoint2(1, 1))))
                    .ParamName
            );
            Assert.Equal(
                "xAxis",
                Assert
                    .Throws<ArgumentException>(() =>
                        GeoTransform2.FromFrame(GeoPoint2.Origin, new GeoVector2(double.NaN, 1))
                    )
                    .ParamName
            );
            Assert.Equal(
                "axis",
                Assert
                    .Throws<ArgumentException>(() =>
                        new GeoVector3(1, 0, 0).RotateBy(0.5, new GeoVector3(double.NaN, 0, 1))
                    )
                    .ParamName
            );
        }
    }
}

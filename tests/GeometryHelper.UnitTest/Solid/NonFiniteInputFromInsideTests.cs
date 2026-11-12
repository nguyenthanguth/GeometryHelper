using System;
using System.Threading.Tasks;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A member given a shape or a number that is not finite either answers as the documented rule says (a <c>Try</c>
    /// member false, <c>GetIntersections</c> nothing, a distance NaN) or refuses the argument by its own name; it no longer
    /// throws from inside, naming a radius, a box size or a control point of its own working, nor runs on for ever. Found
    /// by a reflection fuzz of every public member of the geometry types and the Core classes, one argument at a time made
    /// NaN or infinite.
    /// </summary>
    public class NonFiniteInputFromInsideTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-3, 1E-5);

        private static readonly GeoVector3 NaNVector = new GeoVector3(double.NaN, 0, 0);

        private static readonly GeoPolygon3 Floor = new GeoPolygon3(
            new GeoPoint3(0, 0, 0),
            new GeoPoint3(100, 0, 0),
            new GeoPoint3(100, 100, 0),
            new GeoPoint3(0, 100, 0)
        );

        private static readonly GeoPolygon2 SquareOfNaN = new GeoPolygon2(
            new GeoPoint2(double.NaN, 0),
            new GeoPoint2(100, 0),
            new GeoPoint2(100, 100),
            new GeoPoint2(0, 100)
        );

        [Fact]
        public void AShapeWhoseConstructorRefusesNaN_IsNotMovedToIt_AndTheVectorOrTheTransformationIsNamed()
        {
            // The polygon, the face and the body came back with vertices of NaN, every query of them then throwing for
            // a plane of no normal; the splines likewise, which their constructors refuse; the edge and the loop of arcs
            // threw for an end point and for vertices of their own working.
            GeoSolid3 block = GeoSolid3.Extrude(Floor, new GeoVector3(0, 0, 40), Tol);
            GeoSpline3 spline = GeoSpline3.FromControlPoints(
                3,
                new[]
                {
                    new GeoPoint3(0, 0, 0),
                    new GeoPoint3(30, 60, 0),
                    new GeoPoint3(60, -20, 10),
                    new GeoPoint3(100, 40, 0),
                }
            );
            var edge = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), 0.5, new GeoVector3(0, 0, 1));
            GeoTransform3 ofNaN = GeoTransform3.Translation(NaNVector);

            Assert.Equal("vector", Assert.Throws<ArgumentException>(() => Floor.Translate(NaNVector)).ParamName);
            Assert.Equal("transform", Assert.Throws<ArgumentException>(() => Floor.TransformBy(ofNaN)).ParamName);
            Assert.Equal(
                "vector",
                Assert.Throws<ArgumentException>(() => new GeoFace3(Floor).Translate(NaNVector)).ParamName
            );
            Assert.Equal("vector", Assert.Throws<ArgumentException>(() => block.Translate(NaNVector)).ParamName);
            Assert.Equal("transform", Assert.Throws<ArgumentException>(() => block.TransformBy(ofNaN)).ParamName);
            Assert.Equal("vector", Assert.Throws<ArgumentException>(() => spline.Translate(NaNVector)).ParamName);
            Assert.Equal("vector", Assert.Throws<ArgumentException>(() => edge.Translate(NaNVector)).ParamName);

            // A finite move is the move it was.
            Assert.Equal(100.0, Floor.Translate(new GeoVector3(100, 0, 0)).Vertices[0].X, 12);
        }

        [Fact]
        public void ABoxWithACornerOfNaN_MeetsNothing_AndIsNotANumberAway()
        {
            // Every query of such a box went through its oriented box, and threw for a box size of NaN.
            var box = new GeoAabb3(new GeoPoint3(double.NaN, 30, -10), new GeoPoint3(80, 70, 20));
            var arc = new GeoArc3(new GeoPoint3(50, 50, 0), new GeoVector3(0, 0, 1), 30, 0.3, 2.8);
            var line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 60, 20));

            Assert.Empty(Arc3.GetIntersections(arc, box, Tol));
            Assert.False(Arc3.TryIntersectWith(arc, box, out GeoPoint3[] _, Tol));
            Assert.False(Collision3.CollidesWith(line, box, Tol));
            Assert.True(double.IsNaN(Distance3.DistanceTo(line, box, Tol)));

            GeoObb3 obb = box.ToObb();
            Assert.False(obb.Center.IsValid);
            Assert.Equal(40.0, obb.SizeY, 12);

            // A ray, which is cut as far as the box reaches, meets it nowhere either.
            Assert.Empty(box.GetIntersections(new GeoRay3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 1, 0)), Tol));
        }

        [Fact(Timeout = 30000)]
        public async Task ChamferingALoopOrAChainOfNaN_IsRefusedByName_RatherThanRunningOnForEver()
        {
            Exception loop = await Task.Run(() => Record.Exception(() => Corner2.Chamfer(SquareOfNaN, 5, Tol)));
            Exception chain = await Task.Run(() =>
                Record.Exception(() => Corner2.Chamfer(new GeoPolyline2(SquareOfNaN.Vertices), 5, Tol))
            );

            Assert.Equal("polygon", Assert.IsType<ArgumentException>(loop).ParamName);
            Assert.Equal("polyline", Assert.IsType<ArgumentException>(chain).ParamName);
        }

        [Fact]
        public void ADistanceGivenOnce_IsNamedAsGiven()
        {
            // The forms taking one distance passed it on twice, and a distance of NaN was refused as distance1.
            var square = new GeoPolygon2(
                new GeoPoint2(0, 0),
                new GeoPoint2(100, 0),
                new GeoPoint2(100, 100),
                new GeoPoint2(0, 100)
            );
            var arcs = new GeoPolygonArc2(square);

            Assert.Equal(
                "distance",
                Assert.Throws<ArgumentOutOfRangeException>(() => Corner2.Chamfer(square, double.NaN, Tol)).ParamName
            );
            Assert.Equal(
                "distance",
                Assert.Throws<ArgumentOutOfRangeException>(() => Corner3.Chamfer(Floor, double.NaN, Tol)).ParamName
            );
            Assert.Equal(
                "distance",
                Assert.Throws<ArgumentOutOfRangeException>(() => arcs.Chamfer(double.PositiveInfinity)).ParamName
            );
            Assert.Equal(
                "distance",
                Assert
                    .Throws<ArgumentOutOfRangeException>(() =>
                        new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0)).Extend(double.NaN, LineEnd.End, Tol)
                    )
                    .ParamName
            );
            Assert.Equal(
                "distance",
                Assert
                    .Throws<ArgumentOutOfRangeException>(() =>
                        Offset2.Offset(new GeoPolylineArc2(square.Vertices), double.PositiveInfinity, Tol)
                    )
                    .ParamName
            );
        }

        [Fact]
        public void AnArcOrAnEllipseOfNaN_MeetsNoEdgeAndNoSpline()
        {
            // The arc of an edge with an end of NaN has a radius of NaN, and its circle threw for it; a spline turned an
            // arc, a circle or an ellipse about a centre of NaN into control points of NaN, and threw for those.
            var edge = new GeoEdge2(new GeoPoint2(double.NaN, 0), new GeoPoint2(100, 60), 0.3);
            var arc = new GeoArc2(new GeoPoint2(50, 50), 30, 0.3, 2.8);
            GeoSpline3 spline = GeoSpline3.FromControlPoints(
                3,
                new[]
                {
                    new GeoPoint3(0, 0, 0),
                    new GeoPoint3(30, 60, 0),
                    new GeoPoint3(60, -20, 0),
                    new GeoPoint3(100, 40, 0),
                }
            );
            var arcOfNaN = new GeoArc3(new GeoPoint3(double.NaN, 50, 0), new GeoVector3(0, 0, 1), 30, 0.3, 2.8);
            var ellipseOfNaN = new GeoEllipse3(
                new GeoPoint3(50, double.NaN, 0),
                new GeoVector3(0, 0, 1),
                new GeoVector3(1, 0.2, 0),
                40,
                20
            );

            Assert.False(arc.TryIntersectWith(edge, out GeoPoint2[] _, Tol));
            Assert.False(edge.ToArc().GetCircle().Center.IsValid);
            Assert.Empty(spline.GetIntersections(arcOfNaN, Tol));
            Assert.False(spline.TryIntersectWith(arcOfNaN, out GeoPoint3[] _, Tol));
            Assert.False(spline.TryIntersectWith(ellipseOfNaN, out GeoPoint3[] _, Tol));
            Assert.False(spline.CollidesWith(arcOfNaN, Tol));
        }

        [Fact]
        public void TheTryMembers_AnswerFalseForAShapeOfNaN()
        {
            var arc = new GeoArc2(new GeoPoint2(double.NaN, 50), 30, 0.3, 2.8);
            var edge = new GeoEdge2(new GeoPoint2(double.NaN, 0), new GeoPoint2(100, 60), 0.3);
            var box = new GeoObb3(new GeoPoint3(double.NaN, 50, 0), 60, 40, 30);
            var tilted = new GeoPlane3(new GeoPoint3(double.NaN, 20, 5), new GeoVector3(0.1, 0.2, 1));
            var across = new GeoLine2(new GeoPoint2(double.NaN, 50), new GeoPoint2(150, 50));
            var square = new GeoPolygon2(
                new GeoPoint2(0, 0),
                new GeoPoint2(100, 0),
                new GeoPoint2(100, 100),
                new GeoPoint2(0, 100)
            );

            // Each of these answered true with pieces, points, a box or a line of NaN.
            Assert.False(Arc2.TrySplitAt(arc, 0.4, out GeoArc2[] _, Tol));
            Assert.False(ConvexHull2.TryOf(SquareOfNaN.Vertices, out GeoPolygon2 _, Tol));
            Assert.False(edge.TrySplitAtParameter(0.4, out GeoEdge2 _, out GeoEdge2 _, Tol));
            Assert.False(box.TryExpand(5, out GeoObb3 _));
            Assert.False(tilted.TryGetIntersectionLine(GeoPlane3.XY, out GeoXLine3 _, Tol));
            Assert.False(
                new GeoPoint3(double.NaN, 5, 0).TryToPoint2(GeoCoordinateSystem3.Global, out GeoPoint2 _, Tol)
            );

            // These threw for a plane of no normal, and for edges that do not run end to end.
            Assert.False(square.TrySplitBy(across, out GeoPolygon2[] _, out GeoPolygon2[] _, Tol));
            var chain = new GeoPolylineArc3(
                new[] { new GeoPoint3(double.NaN, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0) },
                new[] { 0.0, 0.3 },
                new[] { new GeoVector3(0, 0, 1), new GeoVector3(0, 0, 1) }
            );
            Assert.False(PlanarMap.TryToPolylineArc2(GeoCoordinateSystem3.Global, chain, out GeoPolylineArc2 _, Tol));
        }

        [Fact]
        public void ATriangleOfNaN_MeetsNoBody_AndIsNotANumberAwayFromAPolygon()
        {
            var triangle = new GeoTriangle3(
                new GeoPoint3(double.NaN, -50, 50),
                new GeoPoint3(300, -50, 50),
                new GeoPoint3(-50, 300, 50)
            );
            GeoSolid3 block = GeoSolid3.Extrude(Floor, new GeoVector3(0, 0, 100), Tol);

            // The two sides of the triangle through its other corners were given as crossing the block.
            Assert.Empty(Intersection3.GetIntersections(triangle, block, Tol));
            Assert.True(double.IsNaN(Floor.GetShortestLineTo(triangle, Tol).Length));
        }

        [Fact]
        public void TheFactoriesAndConstructors_NameTheArgumentThatIsNotFinite()
        {
            var nan2 = new GeoPoint2(double.NaN, 0);
            var nan3 = new GeoPoint3(double.NaN, 0, 0);
            var frameOfNaN = new GeoCoordinateSystem3(nan3, new GeoVector3(1, 0, 0), new GeoVector3(0, 1, 0));
            var square = new GeoPolygon2(
                new GeoPoint2(0, 0),
                new GeoPoint2(100, 0),
                new GeoPoint2(100, 100),
                new GeoPoint2(0, 100)
            );

            Assert.Equal("through", Name(() => new GeoRay2(new GeoPoint2(0, 0), nan2)));
            Assert.Equal("origin", Name(() => new GeoXLine3(nan3, new GeoPoint3(100, 0, 0))));
            Assert.Equal(
                "p2",
                Name(() => GeoPlane3.FromThreePoints(new GeoPoint3(0, 0, 0), nan3, new GeoPoint3(0, 9, 0)))
            );
            Assert.Equal("middle", Name(() => GeoArc2.FromThreePoints(new GeoPoint2(0, 0), nan2, new GeoPoint2(9, 0))));
            Assert.Equal(
                "endAngle",
                Name(() => new GeoArc3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 30, 0.3, double.NaN))
            );
            Assert.Equal(
                "startPoint",
                Name(() => new GeoEdge3(nan3, new GeoPoint3(100, 0, 0), 0.5, new GeoVector3(0, 0, 1)))
            );
            Assert.Equal(
                "axisX",
                Name(() =>
                    new GeoObb3(new GeoPoint3(0, 0, 0), 60, 40, 30, new GeoVector3(double.NaN, 0, 0), GeoVector3.YAxis)
                )
            );
            Assert.Equal("points", Name(() => GeoRectangle2.Fit(SquareOfNaN.Vertices, Tol)));
            Assert.Equal(
                "point",
                Name(() =>
                    new GeoRectangle2(new GeoPoint2(50, 50), 60, 40).Combine(new GeoPoint2(double.PositiveInfinity, 0))
                )
            );
            Assert.Equal("placement", Name(() => GeoSolid3.Extrude(square, frameOfNaN, 40, Tol)));
            Assert.Equal(
                "angleRad",
                Name(() => GeoSolid3.Revolve(square, GeoCoordinateSystem3.Global, double.NaN, 0.5))
            );
            Assert.Equal("start", Name(() => GeoSolid3.Cylinder(nan3, new GeoPoint3(0, 0, 100), 10, 16, Tol)));
            Assert.Equal("arc", Name(() => GeoSpline2.FromArc(new GeoArc2(nan2, 30, 0.3, 2.8))));
            Assert.Equal(
                "ellipse",
                Name(() =>
                    Ellipse3.ToPolygon(
                        new GeoEllipse3(nan3, new GeoVector3(0, 0, 1), new GeoVector3(1, 0, 0), 40, 20),
                        16
                    )
                )
            );
            Assert.Equal("frame", Name(() => PlanarMap.ToPolygon3(frameOfNaN, square)));
            Assert.Equal(
                "bulges",
                Name(() =>
                    new GeoPolylineArc3(
                        new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0) },
                        new[] { double.NaN },
                        new[] { new GeoVector3(0, 0, 1) }
                    )
                )
            );
            Assert.Equal(
                "inPlaneDirection",
                Name(() => new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)).GetPlaneWith(NaNVector))
            );
        }

        private static string Name(Action action) =>
            Assert.IsAssignableFrom<ArgumentException>(Record.Exception(action)).ParamName;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What <c>TransformBy</c> does to a POINT's symbol angle, an MTEXT's direction, a heavy POLYLINE's and a LEADER's normal,
    /// and the nought below nought a mirror leaves in a normal, as AutoCAD 2021 does it (probed in its console on 10 points,
    /// 10 heavy polylines, 5 leaders and 18 annotative MTEXTs, under 10 transformations: every group, to 1E-9).
    /// </summary>
    public class DxfMirror9Tests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-9, 1E-9);

        private static GeoTransform3 Across(GeoPoint3 point, GeoVector3 normal) =>
            GeoTransform3.Mirror(new GeoPlane3(point, normal.Normalize()));

        private static void Near(GeoPoint3 expected, GeoPoint3 actual) =>
            Assert.True(actual.IsEqualTo(expected, Tol), actual + " is not " + expected);

        private static void Near(GeoVector3 expected, GeoVector3 actual) =>
            Assert.True(actual.IsEqualTo(expected, Tol), actual + " is not " + expected);

        private static string RecordOf(DxfDatabase db, DxfEntity entity) =>
            Records(db.ToText()).Single(r => HandleOf(r) == entity.Handle);

        private static bool Has(string record, int code) =>
            record.Replace("\r\n", "\n").Contains("\n" + code.ToString().PadLeft(3) + "\n");

        private static DxfDBPoint Point(DxfDatabase db, GeoPoint3 at, double degrees)
        {
            var point = new DxfDBPoint(at);
            db.ModelSpace.AppendEntity(point);
            point.EcsRotation = degrees * Math.PI / 180.0;
            return point;
        }

        [Fact]
        public void APointMirroredAcrossAPlaneAlongY_HasItsSymbolTurnedHalfACircle()
        {
            var db = new DxfDatabase("AC1027");
            DxfDBPoint point = Point(db, new GeoPoint3(70, 5, 3), 0.0);

            point.TransformBy(Across(new GeoPoint3(40, 0, 0), new GeoVector3(1, 0, 0)));

            // AutoCAD 2021 wrote 50 of 180 where the library wrote none, and no normal.
            Near(new GeoPoint3(10, 5, 3), point.Position);
            Assert.Equal(Math.PI, point.EcsRotation, 12);
            Assert.False(Has(RecordOf(db, point), 210));
        }

        [Fact]
        public void APointOfAnAngleMirroredAcrossAPlaneAlongY_HasItsAngleTakenFromHalfACircle()
        {
            var db = new DxfDatabase("AC1027");
            DxfDBPoint point = Point(db, new GeoPoint3(20, 30, 2), 30.0);

            point.TransformBy(Across(new GeoPoint3(40, 0, 0), new GeoVector3(1, 0, 0)));

            // AutoCAD 2021: 150, the point at (60, 30, 2).
            Near(new GeoPoint3(60, 30, 2), point.Position);
            Assert.Equal(150.0, point.EcsRotation * 180.0 / Math.PI, 9);
        }

        [Fact]
        public void APointMirroredAcrossASlantingPlane_HasItsSymbolTurnedTwiceTheAngleOfTheLine()
        {
            var db = new DxfDatabase("AC1027");
            DxfDBPoint point = Point(db, new GeoPoint3(70, 5, 3), 0.0);

            point.TransformBy(Across(new GeoPoint3(20, 5, 0), new GeoVector3(-15, 10, 0)));

            // AutoCAD 2021: 112.61986, twice 56.31 degrees, where the plane's line stands.
            Assert.Equal(2.0 * Math.Atan2(15.0, 10.0), point.EcsRotation, 9);
        }

        [Fact]
        public void APointMirroredAcrossItsOwnPlane_StandsOnTheNormalDown_WithItsSymbolTurnedHalfACircle()
        {
            var db = new DxfDatabase("AC1027");
            DxfDBPoint point = Point(db, new GeoPoint3(70, 5, 3), 0.0);

            point.TransformBy(Across(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1)));

            // AutoCAD 2021: the normal (0, 0, -1), 50 of 180 (the x axis of its system points the other way).
            Near(new GeoVector3(0, 0, -1), point.Normal);
            Assert.Equal(Math.PI, point.EcsRotation, 12);
        }

        [Fact]
        public void APointTurnedByThirtyDegrees_HasItsSymbolTurnedThirty()
        {
            var db = new DxfDatabase("AC1027");
            DxfDBPoint point = Point(db, new GeoPoint3(70, 5, 3), 0.0);

            point.TransformBy(GeoTransform3.RotationZ(30.0 * Math.PI / 180.0));

            Assert.Equal(30.0, point.EcsRotation * 180.0 / Math.PI, 9);
        }

        [Fact]
        public void APointMoved_HasNoAngleWritten()
        {
            var db = new DxfDatabase("AC1027");
            DxfDBPoint point = Point(db, new GeoPoint3(70, 5, 3), 0.0);

            point.TransformBy(GeoTransform3.Translation(new GeoVector3(100, 50, 0)));

            Assert.False(Has(RecordOf(db, point), 50));
        }

        [Fact]
        public void ALineMirroredAcrossAPlaneAlongY_HasNoNormalWritten_AsAMirrorLeavesNoughtBelowNought()
        {
            var db = new DxfDatabase("AC1027");
            var line = new DxfLine(new GeoPoint3(10, 10, 0), new GeoPoint3(30, 20, 0));
            db.ModelSpace.AppendEntity(line);

            line.TransformBy(Across(new GeoPoint3(40, 0, 0), new GeoVector3(1, 0, 0)));

            // The matrix of the mirror holds -0 where it takes (0, 0, 1) to (-0, 0, 1): AutoCAD writes no normal for it.
            Assert.False(Has(RecordOf(db, line), 210));
        }

        [Fact]
        public void AnMTextAlongXTurnedAboutY_IsWrittenWithItsDirection_AsItsXAxisPutIntoThePlaneIsNotTheWorldsOwn()
        {
            var db = new DxfDatabase("AC1027");
            var words = new DxfMText
            {
                Contents = "ABC",
                TextHeight = 3.0,
                Location = new GeoPoint3(20, 40, 0),
            };
            db.ModelSpace.AppendEntity(words);

            words.TransformBy(GeoTransform3.RotationY(25.0 * Math.PI / 180.0));

            // AutoCAD 2021 wrote 11 of (0.906, 0, -0.423), where the library left it out for the x axis put into the new plane.
            Assert.True(Has(RecordOf(db, words), 11));
            Near(
                new GeoVector3(Math.Cos(25.0 * Math.PI / 180.0), 0, -Math.Sin(25.0 * Math.PI / 180.0)),
                words.Direction
            );
        }

        [Fact]
        public void AnMTextAlongTheWorldsXAxisInAnotherPlane_MovedAgain_IsWrittenWithNoDirection()
        {
            var db = new DxfDatabase("AC1027");
            var words = new DxfMText
            {
                Contents = "ABC",
                TextHeight = 3.0,
                Location = new GeoPoint3(20, 7, 40),
                Normal = new GeoVector3(0, 1, 0),
                Direction = new GeoVector3(1, 0, 0),
            };
            db.ModelSpace.AppendEntity(words);

            words.TransformBy(GeoTransform3.Translation(new GeoVector3(100, 50, 0)));

            // AutoCAD 2021 left the 11 of (1, 0, 0) out, whether the record had one or not.
            Assert.False(Has(RecordOf(db, words), 11));
            Near(new GeoVector3(1, 0, 0), words.Direction);
        }

        [Fact]
        public void AHeavyPolylineMirroredAcrossAPlaneAlongY_KeepsItsNormal_AndRunsItsArcsTheOtherWay()
        {
            var db = new DxfDatabase("AC1027");
            var points = new[] { new GeoPoint3(60, 10, 0), new GeoPoint3(80, 10, 0), new GeoPoint3(80, 30, 0) };
            var polyline = new DxfPolyline2d(
                DxfPoly2dType.SimplePoly,
                points,
                3.0,
                false,
                0.0,
                0.0,
                new[] { 0.0, 0.5, -0.3 }
            );
            db.ModelSpace.AppendEntity(polyline);

            polyline.TransformBy(Across(new GeoPoint3(40, 0, 0), new GeoVector3(1, 0, 0)));

            // AutoCAD 2021: the normal (0, 0, 1) and the elevation 3 kept, the vertices (20, 10), (0, 10), (0, 30) with the
            // bulges -0.5 and 0.3, where the library turned the normal to (0, 0, -1) and kept the bulges.
            Near(new GeoVector3(0, 0, 1), polyline.Normal);
            DxfVertex2d[] vertices = polyline.ToArray();
            Near(new GeoPoint3(20, 10, 3), polyline.VertexPosition(vertices[0]));
            Near(new GeoPoint3(0, 10, 3), polyline.VertexPosition(vertices[1]));
            Near(new GeoPoint3(0, 30, 3), polyline.VertexPosition(vertices[2]));
            Assert.Equal(-0.5, vertices[1].Bulge, 12);
            Assert.Equal(0.3, vertices[2].Bulge, 12);
        }

        [Fact]
        public void AHeavyPolylineMirroredAcrossItsOwnPlane_IsTurnedOverToTheNormalDown()
        {
            var db = new DxfDatabase("AC1027");
            var points = new[] { new GeoPoint3(60, 10, 0), new GeoPoint3(80, 10, 0) };
            var polyline = new DxfPolyline2d(
                DxfPoly2dType.SimplePoly,
                points,
                3.0,
                false,
                0.0,
                0.0,
                new[] { 0.5, 0.0 }
            );
            db.ModelSpace.AppendEntity(polyline);

            polyline.TransformBy(Across(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1)));

            // AutoCAD 2021: the normal is the image of (0, 0, 1), (0, 0, -1), and the arc runs the other way round in its system.
            Near(new GeoVector3(0, 0, -1), polyline.Normal);
            Assert.Equal(-0.5, polyline.ToArray()[0].Bulge, 12);
        }

        [Fact]
        public void ALeaderMirroredAcrossAPlaneAlongY_KeepsItsNormal()
        {
            var db = new DxfDatabase("AC1027");
            var leader = new DxfLeader();
            leader.AppendVertex(new GeoPoint3(10, 10, 2));
            leader.AppendVertex(new GeoPoint3(20, 20, 2));
            leader.AppendVertex(new GeoPoint3(30, 20, 2));
            db.ModelSpace.AppendEntity(leader);

            leader.TransformBy(Across(new GeoPoint3(40, 0, 0), new GeoVector3(1, 0, 0)));

            // AutoCAD 2021 left the normal (0, 0, 1), the vertices at (70, 10, 2), (60, 20, 2) and (50, 20, 2); the library
            // turned it to (0, 0, -1).
            Near(new GeoVector3(0, 0, 1), leader.Normal);
            Near(new GeoPoint3(70, 10, 2), leader.VertexAt(0));
            Near(new GeoPoint3(50, 20, 2), leader.VertexAt(2));
        }

        [Fact]
        public void ALeaderMirroredAcrossItsOwnPlane_StandsOnTheNormalDown()
        {
            var db = new DxfDatabase("AC1027");
            var leader = new DxfLeader();
            leader.AppendVertex(new GeoPoint3(10, 10, 2));
            leader.AppendVertex(new GeoPoint3(20, 20, 2));
            db.ModelSpace.AppendEntity(leader);

            leader.TransformBy(Across(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1)));

            Near(new GeoVector3(0, 0, -1), leader.Normal);
        }
    }
}

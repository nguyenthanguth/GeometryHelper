using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What a mirror does to the normal of a circle, an arc, a polyline, a SOLID and an MTEXT, and what it and a turn do to a
    /// HATCH's pattern scale and angle and the context of one made annotative, as AutoCAD 2021's <c>TransformBy</c> does them
    /// (probed in its console on 50 entities of the planes (0, 0, 1), (0, 1, 0), (1, 1, 1), (1, 2, 3) and (0, 0, -1), under 13
    /// transformations: mirrors across x = 40, y = 30, z = 0, z = 5, a tilted plane and a slanting line, a mirror turned about
    /// z, one that scales by 2, and turns, a move and a scaling): the new normal is the image of the old one, so that a mirror
    /// across a plane that holds the normal leaves it, and one across the plane of a circle turns it over with the centre
    /// where it was.
    /// </summary>
    public class DxfMirrorNormalTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-9, 1E-9);

        private static GeoTransform3 Across(GeoPoint3 point, GeoVector3 normal) =>
            GeoTransform3.Mirror(new GeoPlane3(point, normal.Normalize()));

        private static GeoTransform3 AcrossX40 => Across(new GeoPoint3(40, 0, 0), new GeoVector3(1, 0, 0));

        private static GeoTransform3 AcrossXy => Across(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1));

        private static void Near(GeoPoint3 expected, GeoPoint3 actual) =>
            Assert.True(actual.IsEqualTo(expected, Tol), actual + " is not " + expected);

        private static void Near(GeoVector3 expected, GeoVector3 actual) =>
            Assert.True(actual.IsEqualTo(expected, Tol), actual + " is not " + expected);

        private static void Near(double expected, double actual) =>
            Assert.True(Math.Abs(expected - actual) <= 1E-9, "Expected " + expected + " but was " + actual + ".");

        private static double Radians(double degrees) => degrees * Math.PI / 180.0;

        // ---- circles and arcs -----------------------------------------------------------------------------------------------

        [Fact]
        public void ACircleMirroredAcrossAPlaneThatHoldsItsNormal_KeepsItsNormal()
        {
            var database = new DxfDatabase("AC1027");
            var circle = new DxfCircle(new GeoPoint3(50, 50, 0), new GeoVector3(0, 0, 1), 5.0);
            database.ModelSpace.AppendEntity(circle);

            circle.TransformBy(AcrossX40);

            // AutoCAD 2021: centre (30, 50, 0), no 210 group, the normal still (0, 0, 1).
            Near(new GeoVector3(0, 0, 1), circle.Normal);
            Near(new GeoPoint3(30, 50, 0), circle.Center);
            Near(5.0, circle.Radius);
        }

        [Fact]
        public void ACircleMirroredAcrossItsOwnPlane_HasItsNormalTurnedOver_ItsCentreWhereItWas()
        {
            var database = new DxfDatabase("AC1027");
            var circle = new DxfCircle(new GeoPoint3(50, 50, 0), new GeoVector3(0, 0, 1), 5.0);
            database.ModelSpace.AppendEntity(circle);

            circle.TransformBy(AcrossXy);

            // AutoCAD 2021 wrote 10 of (-50, 50, 0) on the normal (0, 0, -1): the centre (50, 50, 0) in its system.
            Near(new GeoVector3(0, 0, -1), circle.Normal);
            Near(new GeoPoint3(50, 50, 0), circle.Center);
        }

        [Fact]
        public void ACircleOnATiltedNormalMirroredAcrossATiltedPlane_TakesTheImageOfItsNormal()
        {
            var database = new DxfDatabase("AC1027");
            GeoVector3 normal = new GeoVector3(1, 1, 1).Normalize();
            var circle = new DxfCircle(new GeoPoint3(10, 20, 30), normal, 5.0);
            database.ModelSpace.AppendEntity(circle);
            GeoTransform3 mirror = Across(new GeoPoint3(10, 10, 3), new GeoVector3(1, 2, 3));

            circle.TransformBy(mirror);

            // AutoCAD 2021: the normal (0.3108809, 0.7549965, 0.5773503), where the cross product of the images of the axes
            // is its opposite.
            Near(mirror.Transform(normal), circle.Normal);
            Near(mirror.Transform(new GeoPoint3(10, 20, 30)), circle.Center);
        }

        [Fact]
        public void ACircleWithAThickness_KeepsItAcrossAMirror()
        {
            var database = new DxfDatabase("AC1027");
            var circle = new DxfCircle(new GeoPoint3(50, 50, 0), new GeoVector3(0, 0, 1), 5.0) { Thickness = 2.0 };
            database.ModelSpace.AppendEntity(circle);

            circle.TransformBy(AcrossXy);

            // The thickness runs along the new normal: 2, not -2, as AutoCAD 2021 writes it.
            Near(2.0, circle.Thickness);
        }

        [Fact]
        public void AnArcMirroredAcrossAPlaneThatHoldsItsNormal_KeepsItsNormal_AndRunsTheOtherWayRound()
        {
            var database = new DxfDatabase("AC1027");
            var arc = new DxfArc(new GeoPoint3(60, 30, 0), new GeoVector3(0, 0, 1), 8.0, Radians(30), Radians(120));
            database.ModelSpace.AppendEntity(arc);

            arc.TransformBy(AcrossX40);

            // AutoCAD 2021: centre (20, 30, 0), the normal still (0, 0, 1), from 60 to 150 degrees.
            Near(new GeoVector3(0, 0, 1), arc.Normal);
            Near(new GeoPoint3(20, 30, 0), arc.Center);
            Near(Radians(60), arc.StartAngle);
            Near(Radians(150), arc.EndAngle);
        }

        [Fact]
        public void AnArcMirroredAcrossItsOwnPlane_HasItsNormalTurnedOver_AndItsAnglesInTheNewSystem()
        {
            var database = new DxfDatabase("AC1027");
            var arc = new DxfArc(new GeoPoint3(60, 30, 0), new GeoVector3(0, 0, 1), 8.0, Radians(30), Radians(120));
            database.ModelSpace.AppendEntity(arc);

            arc.TransformBy(AcrossXy);

            // AutoCAD 2021: 10 of (-60, 30, 0) on the normal (0, 0, -1), from 60 to 150 degrees: the arc from 30 to 120 where
            // it was, its angles taken from the x axis of the new system, which runs against the world's.
            Near(new GeoVector3(0, 0, -1), arc.Normal);
            Near(new GeoPoint3(60, 30, 0), arc.Center);
            Near(Radians(60), arc.StartAngle);
            Near(Radians(150), arc.EndAngle);
        }

        [Fact]
        public void AnArcOnTheNormalXAxis_MirroredAcrossTheYzPlane_KeepsItsNormal()
        {
            var database = new DxfDatabase("AC1027");
            var arc = new DxfArc(new GeoPoint3(60, 30, 2), new GeoVector3(0, 1, 0), 8.0, Radians(300), Radians(40));
            database.ModelSpace.AppendEntity(arc);

            arc.TransformBy(AcrossX40);

            // AutoCAD 2021: the normal still (0, 1, 0), the centre (20, 30, 2), from 140 to 240 degrees.
            Near(new GeoVector3(0, 1, 0), arc.Normal);
            Near(new GeoPoint3(20, 30, 2), arc.Center);
            Near(Radians(140), arc.StartAngle);
            Near(Radians(240), arc.EndAngle);
        }

        // ---- polylines and solids ---------------------------------------------------------------------------------------------

        private static DxfPolyline Polyline(DxfDatabase database)
        {
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, new GeoPoint2(60, 10), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(1, new GeoPoint2(80, 10), 0.5, 0.0, 0.0);
            polyline.AddVertexAt(2, new GeoPoint2(80, 30), -0.3, 0.0, 0.0);
            polyline.AddVertexAt(3, new GeoPoint2(65, 35), 0.0, 0.0, 0.0);
            database.ModelSpace.AppendEntity(polyline);
            return polyline;
        }

        [Fact]
        public void APolylineMirroredAcrossAPlaneThatHoldsItsNormal_KeepsItsNormal_AndFlipsItsBulges()
        {
            var database = new DxfDatabase("AC1027");
            DxfPolyline polyline = Polyline(database);

            polyline.TransformBy(AcrossX40);

            // AutoCAD 2021: the vertices (20, 10), (0, 10), (0, 30), (15, 35), the bulges 0, -0.5, 0.3, 0, no 210.
            Near(new GeoVector3(0, 0, 1), polyline.Normal);
            Near(20.0, polyline.GetPoint2dAt(0).X);
            Near(0.0, polyline.GetPoint2dAt(1).X);
            Near(15.0, polyline.GetPoint2dAt(3).X);
            Near(35.0, polyline.GetPoint2dAt(3).Y);
            Near(0.0, polyline.GetBulgeAt(0));
            Near(-0.5, polyline.GetBulgeAt(1));
            Near(0.3, polyline.GetBulgeAt(2));
        }

        [Fact]
        public void APolylineMirroredAcrossItsOwnPlane_HasItsNormalTurnedOver_AndItsVerticesInTheNewSystem()
        {
            var database = new DxfDatabase("AC1027");
            DxfPolyline polyline = Polyline(database);

            polyline.TransformBy(AcrossXy);

            // AutoCAD 2021: the normal (0, 0, -1), the vertices (-60, 10), (-80, 10), (-80, 30), (-65, 35) in its system, the
            // bulges -0.5 and 0.3: the same curve, run the other way round about the normal turned over.
            Near(new GeoVector3(0, 0, -1), polyline.Normal);
            Near(-60.0, polyline.GetPoint2dAt(0).X);
            Near(-80.0, polyline.GetPoint2dAt(2).X);
            Near(30.0, polyline.GetPoint2dAt(2).Y);
            Near(-0.5, polyline.GetBulgeAt(1));
            Near(0.3, polyline.GetBulgeAt(2));
        }

        [Fact]
        public void APolylineAtAnElevation_MirroredAcrossAPlaneAbove_TakesTheElevationOfItsFirstVertex()
        {
            var database = new DxfDatabase("AC1027");
            DxfPolyline polyline = Polyline(database);
            polyline.Elevation = 2.0;

            polyline.TransformBy(Across(new GeoPoint3(0, 0, 5), new GeoVector3(0, 0, 1)));

            // AutoCAD 2021: the plane z = 5 takes the plane z = 2 to z = 8, and the normal to (0, 0, -1), whose system holds the
            // elevation as -8.
            Near(new GeoVector3(0, 0, -1), polyline.Normal);
            Near(-8.0, polyline.Elevation);
        }

        [Fact]
        public void ASolidMirroredAcrossAPlaneThatHoldsItsNormal_KeepsItsNormal()
        {
            var database = new DxfDatabase("AC1027");
            var solid = new DxfSolid(
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(5, 0, 0),
                new GeoPoint3(0, 5, 0),
                new GeoPoint3(5, 5, 0)
            );
            database.ModelSpace.AppendEntity(solid);

            solid.TransformBy(AcrossX40);

            // AutoCAD 2021: the corners (80, 0, 0), (75, 0, 0), (80, 5, 0), (75, 5, 0), no 210.
            Near(new GeoVector3(0, 0, 1), solid.Normal);
            Near(new GeoPoint3(80, 0, 0), solid.GetPointAt(0));
            Near(new GeoPoint3(75, 0, 0), solid.GetPointAt(1));
            Near(new GeoPoint3(80, 5, 0), solid.GetPointAt(2));
            Near(new GeoPoint3(75, 5, 0), solid.GetPointAt(3));
        }

        [Fact]
        public void ASolidMirroredAcrossItsOwnPlane_HasItsNormalTurnedOver_ItsCornersWhereTheyWere()
        {
            var database = new DxfDatabase("AC1027");
            var solid = new DxfSolid(
                new GeoPoint3(0, 0, 3),
                new GeoPoint3(5, 0, 3),
                new GeoPoint3(0, 5, 3),
                new GeoPoint3(5, 5, 3)
            );
            solid.Thickness = 2.0;
            database.ModelSpace.AppendEntity(solid);

            solid.TransformBy(AcrossXy);

            // AutoCAD 2021: the normal (0, 0, -1), the corners at z = -3 in the world, the thickness still 2.
            Near(new GeoVector3(0, 0, -1), solid.Normal);
            Near(new GeoPoint3(5, 0, -3), solid.GetPointAt(1));
            Near(2.0, solid.Thickness);
        }

        // ---- MTEXT ----------------------------------------------------------------------------------------------------------

        private static DxfMText Text(DxfDatabase database, GeoVector3 direction, DxfAttachmentPoint attachment)
        {
            var text = new DxfMText();
            database.ModelSpace.AppendEntity(text);
            text.Contents = "ABC DEF";
            text.TextHeight = 3.0;
            text.Width = 20.0;
            text.Location = new GeoPoint3(20, 40, 0);
            text.Direction = direction;
            text.Attachment = attachment;
            return text;
        }

        [Fact]
        public void AnMTextAlongX_MirroredAcrossTheYzPlane_ReadsAlongXStill_HeldByTheOppositeSide()
        {
            var database = new DxfDatabase("AC1027");
            DxfMText text = Text(database, new GeoVector3(1, 0, 0), DxfAttachmentPoint.TopLeft);

            text.TransformBy(AcrossX40);

            // AutoCAD 2021: the words stay readable, turned over across their upright: at (60, 40), still reading along
            // (1, 0, 0) with the normal (0, 0, 1), held by the top right where they were held by the top left.
            Near(new GeoPoint3(60, 40, 0), text.Location);
            Near(new GeoVector3(1, 0, 0), text.Direction);
            Near(new GeoVector3(0, 0, 1), text.Normal);
            Assert.Equal(DxfAttachmentPoint.TopRight, text.Attachment);
        }

        [Fact]
        public void AnMTextAlongX_MirroredAcrossAHorizontalPlane_ReadsAlongXStill_HeldByTheOppositeEdge()
        {
            var database = new DxfDatabase("AC1027");
            DxfMText text = Text(database, new GeoVector3(1, 0, 0), DxfAttachmentPoint.TopLeft);

            text.TransformBy(Across(new GeoPoint3(0, 30, 0), new GeoVector3(0, 1, 0)));

            // AutoCAD 2021: turned over across its baseline, at (20, 20), reading along (1, 0, 0), held by the bottom left.
            Near(new GeoPoint3(20, 20, 0), text.Location);
            Near(new GeoVector3(1, 0, 0), text.Direction);
            Near(new GeoVector3(0, 0, 1), text.Normal);
            Assert.Equal(DxfAttachmentPoint.BottomLeft, text.Attachment);
        }

        [Fact]
        public void AnMTextMirroredAcrossItsOwnPlane_HasItsNormalTurnedOver_ReadsAlongTheOtherWay()
        {
            var database = new DxfDatabase("AC1027");
            DxfMText text = Text(database, new GeoVector3(1, 0, 0), DxfAttachmentPoint.TopLeft);

            text.TransformBy(AcrossXy);

            // AutoCAD 2021: the normal (0, 0, -1), the direction (-1, 0, 0), the point (20, 40, 0), held by the top right, so
            // that seen from below the words read left to right from where the box starts.
            Near(new GeoPoint3(20, 40, 0), text.Location);
            Near(new GeoVector3(0, 0, -1), text.Normal);
            Near(new GeoVector3(-1, 0, 0), text.Direction);
            Assert.Equal(DxfAttachmentPoint.TopRight, text.Attachment);
        }

        [Fact]
        public void AnMTextAt34Degrees_MirroredAcrossALineAt56Degrees_IsTurnedOverAcrossItsBaseline()
        {
            var database = new DxfDatabase("AC1027");
            DxfMText text = Text(
                database,
                new GeoVector3(Math.Cos(0.6), Math.Sin(0.6), 0.0),
                DxfAttachmentPoint.TopLeft
            );

            // The mirror across the line through (20, 5) and (30, 20), in the drawing's plane.
            GeoTransform3 mirror = Across(new GeoPoint3(20, 5, 0), new GeoVector3(-15, 10, 0));
            text.TransformBy(mirror);

            // AutoCAD 2021: the direction mirrored as it is, (0.20377, 0.97902, 0), held by the bottom left where it was held
            // by the top left.
            Near(mirror.Transform(new GeoVector3(Math.Cos(0.6), Math.Sin(0.6), 0.0)), text.Direction);
            Assert.Equal(DxfAttachmentPoint.BottomLeft, text.Attachment);
        }

        [Fact]
        public void AnMTextScaled_KeepsItsDirectionOfUnitLength()
        {
            var database = new DxfDatabase("AC1027");
            DxfMText text = Text(
                database,
                new GeoVector3(Math.Cos(0.6), Math.Sin(0.6), 0.0),
                DxfAttachmentPoint.TopLeft
            );

            text.TransformBy(GeoTransform3.Scaling(2.0, 2.0, 2.0));

            // AutoCAD 2021 wrote 11 of (0.82534, 0.56464, 0), not (1.65067, 1.12928, 0); its box and its height are doubled.
            Near(1.0, text.Direction.Length);
            Near(6.0, text.TextHeight);
            Near(40.0, text.Width);
            string[] lines = database.ToText().Replace("\r\n", "\n").Split('\n');
            int at = Array.FindIndex(lines, l => l.Trim() == "MTEXT");
            int x = Array.FindIndex(lines, at, l => l.Trim() == "11");
            Near(Math.Cos(0.6), double.Parse(lines[x + 1], CultureInfo.InvariantCulture));
        }

        // ---- HATCH ----------------------------------------------------------------------------------------------------------

        private static DxfHatch UserHatch(DxfDatabase database, GeoVector3? normal)
        {
            var hatch = new DxfHatch();

            if (normal.HasValue)
            {
                hatch.Normal = normal.Value;
            }

            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternAngle = Radians(30);
            hatch.PatternSpace = 2.0;
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(42, 50), new GeoPoint2(58, 50), new GeoPoint2(58, 58), new GeoPoint2(42, 58) },
                null
            );
            hatch.AppendSeedPoint(new GeoPoint2(45, 52));
            database.ModelSpace.AppendEntity(hatch);
            return hatch;
        }

        /// <summary>The groups of the record of a handle as "code=value".</summary>
        private static List<string> Groups(DxfDatabase database, string handle)
        {
            string[] lines = database.ToText().Replace("\r\n", "\n").Split('\n');
            var groups = new List<string>();
            bool found = false;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1].Trim();

                if (code == 0 && found)
                {
                    break;
                }

                if (code == 0)
                {
                    groups.Clear();
                }

                groups.Add(code + "=" + value);
                found |= code == 5 && value == handle;
            }

            return found ? groups : null;
        }

        private static double Value(IEnumerable<string> groups, int code) =>
            double.Parse(
                groups.First(g => g.StartsWith(code + "=", StringComparison.Ordinal)).Substring((code + "=").Length),
                CultureInfo.InvariantCulture
            );

        [Fact]
        public void AHatchMirroredAndScaled_LeavesItsPatternScale_WhereAScalingAloneDoublesIt()
        {
            var database = new DxfDatabase("AC1027");
            DxfHatch mirrored = UserHatch(database, null);
            DxfHatch scaled = UserHatch(database, null);
            GeoTransform3 mirror = Across(new GeoPoint3(20, 5, 0), new GeoVector3(-15, 10, 0));

            mirrored.TransformBy(GeoTransform3.Scaling(2.0, 2.0, 2.0).Multiply(mirror));
            scaled.TransformBy(GeoTransform3.Scaling(2.0, 2.0, 2.0));

            // AutoCAD 2021: 41 of 2 stays 2 under a mirror scaled by 2, and is 4 under a scaling of 2.
            Near(2.0, Value(Groups(database, mirrored.Handle), 41));
            Near(4.0, Value(Groups(database, scaled.Handle), 41));
        }

        [Theory]
        [InlineData(200, 210.0)]
        [InlineData(250, 210.0)]
        [InlineData(300, -150.0)]
        [InlineData(350, -150.0)]
        public void AHatchPointingDown_TurnedAboutXByMoreThanHalfACircle_HasItsPatternAngleWrittenAsAutoCadWritesIt(
            double degrees,
            double expected
        )
        {
            var database = new DxfDatabase("AC1027");
            DxfHatch hatch = UserHatch(database, new GeoVector3(0, 0, -1));

            hatch.TransformBy(GeoTransform3.RotationX(Radians(degrees)));

            // AutoCAD 2021: a pattern at 30 degrees of a hatch of normal (0, 0, -1) is turned by half a circle, written +180
            // while the new normal points up and -180 once it points down (the change at 270 degrees).
            Near(expected, Value(Groups(database, hatch.Handle), 52));
        }

        [Fact]
        public void AHatchOfNormalUp_TurnedAboutXByHalfACircleAndMore_StaysAt210()
        {
            var database = new DxfDatabase("AC1027");
            DxfHatch hatch = UserHatch(database, null);

            hatch.TransformBy(GeoTransform3.RotationX(Radians(300)));

            Near(210.0, Value(Groups(database, hatch.Handle), 52));
        }

        [Fact]
        public void AHatchMadeAnnotativeOnANormalOffTheZAxis_HoldsTheDirectionOfItsPatternAsAVectorOfTheWorld()
        {
            var database = new DxfDatabase("AC1027");
            database
                .ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES")
                .AddContext(
                    new DxfAnnotationScale
                    {
                        Name = "1:1",
                        PaperUnits = 1,
                        DrawingUnits = 1,
                    }
                );
            DxfHatch hatch = UserHatch(database, new GeoVector3(0, -1, 0));

            hatch.Annotative = DxfAnnotativeStates.True;

            // AutoCAD 2021's -HATCH in a UCS turned 90 degrees about x (the hatch of normal (0, -1, 0)) wrote 10, 20, 30 of its
            // context as (0.866, 0, 0.5): the pattern at 30 degrees along the x and y axes of the hatch's own system, which are
            // (1, 0, 0) and (0, 0, 1) here; not (0.866, 0.5, 0) of the plane.
            var manager = (DxfDBDictionary)hatch.ExtensionDictionary.GetAt("AcDbContextDataManager");
            var scales = (DxfDBDictionary)manager.GetAt("ACDB_ANNOTATIONSCALES");
            List<string> context = Groups(database, scales.GetAt("*A1").Handle);
            Near(0.8660254037844387, Value(context, 10));
            Near(0.0, Value(context, 20));
            Near(0.5, Value(context, 30));
        }
    }
}

using System;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Associative dimensions as AutoCAD 2021 makes and follows them, beyond the earlier rounds: an end where several objects
    /// meet tied to the one drawn last, the line selected second as the first of an angle of two lines, a linear dimension's
    /// line kept as far from the other origin where the nearer one comes too close, the words of a radius grown from their
    /// leader's end, angles of three points along one ray, the second point of an angle of a circle on an arc of a polyline,
    /// and arc length dimensions tied to their arcs. The numbers are those AutoCAD 2021's console gave for the same drawings,
    /// made with its own template (Standard in arial.ttf, text 0.18 high).
    /// </summary>
    public class DimensionAssociativeFollowUps2Tests
    {
        /// <summary>A drawing whose text style Standard is arial.ttf, the dimension style's words in it, as acad.dwt has them.</summary>
        private static CadDocument Arial()
        {
            CadDocument document = Blank();
            DxfTextStyleTableRecord standard = document.Database.TextStyleTable["Standard"];
            standard.FileName = "arial.ttf";
            document.Database.DimStyleTable["Standard"].Dimtxsty = standard;
            StandardCurrent(document.Database);
            return document;
        }

        private static void Snap(CadCommandSession session, CadSnapModes mode, GeoPoint2 cursor)
        {
            var snaps = new ObjectSnap(Tight) { Modes = mode };
            CadSnapResult result = snaps.Snap(
                session.Document.GetPickIndex(session.Document.ModelSpace),
                cursor,
                3.0,
                session.Prompt.BasePoint,
                session.Prompt.SnapFrom
            );
            Assert.NotEqual(CadSnapKind.None, result.Kind);
            Assert.True(session.Feed(result, false));
        }

        private static string Last(CadDocument document) => document.ModelSpace.Block.Last(e => !e.IsErased).Handle;

        private static T Made<T>(CadDocument document)
            where T : DxfDimension => (T)document.ModelSpace.Block.Last(e => !e.IsErased && e is DxfDimension);

        private static GeoPoint2 Polar(double x, double y, double radius, double degrees) =>
            P(x + radius * Math.Cos(degrees * Math.PI / 180.0), y + radius * Math.Sin(degrees * Math.PI / 180.0));

        private static double Written42(CadDocument document, string handle)
        {
            string[] lines = document.Database.ToText().Split('\n');

            for (int i = 0; i + 3 < lines.Length; i += 2)
            {
                if (lines[i].Trim() != "0" || lines[i + 2].Trim() != "5" || lines[i + 3].Trim() != handle)
                {
                    continue;
                }

                for (int j = i + 2; j + 1 < lines.Length && lines[j].Trim() != "0"; j += 2)
                {
                    if (lines[j].Trim() == "42")
                    {
                        return double.Parse(lines[j + 1].Trim(), CultureInfo.InvariantCulture);
                    }
                }
            }

            return double.NaN;
        }

        private static void Move(CadCommandSession session, string handle, double x, double y)
        {
            session.Document.Selection.Set(new[] { handle });
            Run(session, "MOVE", P(0, 0), P(x, y));
        }

        private static DxfOsnapPointRef Tie(DxfDimension dimension, int point) =>
            dimension.DimAssoc.PointReferences.Single(r => r.PointIndex == point);

        [Fact]
        public void AnEndSnappedWhereLinesMeet_IsTiedToTheLineDrawnLast_ThoughTheCursorStoodOnAnother_AsAutoCad()
        {
            // AutoCAD 2021: lines (0, 0)-(100, 0) and then (0, 0)-(0, 100), DIMLINEAR _end at (2, 0): the start of the second;
            // three lines from (0, 400), _end at (2, 400): the start of the third.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0), P(100, 0), Enter, "LINE", P(0, 0), P(0, 100), Enter);
            string upright = Last(document);
            Run(session, "DIMLINEAR");
            Snap(session, CadSnapModes.Endpoint, P(2, 0));
            Run(session, P(50, -40), P(25, -55));
            DxfOsnapPointRef tie = Tie(Made<DxfRotatedDimension>(document), 0);
            Assert.Equal(upright, tie.ObjectHandle);
            Assert.Equal(DxfOsnapType.StartPoint, tie.OsnapType);

            Run(session, "LINE", P(0, 400), P(100, 400), Enter, "LINE", P(0, 400), P(0, 500), Enter);
            Run(session, "LINE", P(0, 400), P(70, 470), Enter);
            string third = Last(document);
            Run(session, "DIMLINEAR");
            Snap(session, CadSnapModes.Endpoint, P(2, 400));
            Run(session, P(50, 360), P(25, 345));
            Assert.Equal(third, Tie(Made<DxfRotatedDimension>(document), 0).ObjectHandle);
        }

        [Fact]
        public void AnEndSnappedWhereALineMeetsAnArcOrAPolylineDrawnAfterIt_IsTiedToThatObject_AsAutoCad()
        {
            // AutoCAD 2021: the arc about (150, 250) from (150, 200), drawn after a line from there: StartPoint of the arc at
            // 4.712389; a polyline drawn after a line, ending where it starts: Endpoint of the polyline's first segment.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(150, 200), P(250, 200), Enter, "ARC", "C", P(150, 250), P(150, 200), P(200, 250));
            string arc = Last(document);
            Run(session, "DIMLINEAR");
            Snap(session, CadSnapModes.Endpoint, P(152, 200));
            Run(session, P(200, 160), P(175, 145));
            DxfOsnapPointRef tie = Tie(Made<DxfRotatedDimension>(document), 0);
            Assert.Equal(arc, tie.ObjectHandle);
            Assert.Equal(DxfOsnapType.StartPoint, tie.OsnapType);
            Assert.Equal(4.712389, tie.NearParameter, 6);

            Run(session, "LINE", P(400, -4000), P(500, -4000), Enter);
            Run(session, "PLINE", P(400, -3900), P(400, -4000), P(350, -4050), Enter);
            string polyline = Last(document);
            Run(session, "DIMLINEAR");
            Snap(session, CadSnapModes.Endpoint, P(402, -4000));
            Run(session, P(450, -3950), P(470, -3970));
            tie = Tie(Made<DxfRotatedDimension>(document), 0);
            Assert.Equal(polyline, tie.ObjectHandle);
            Assert.Equal(DxfOsnapType.Endpoint, tie.OsnapType);
            Assert.Equal(1, tie.GsMarker);
        }

        [Fact]
        public void AnAngleOfTwoLines_TakesTheLineSelectedSecondAsItsFirst_AsAutoCad()
        {
            // AutoCAD 2021: (1000, 0)-(1100, 0) selected, then (1000, 0)-(1060, 80), the arc at (1060, 30): 13 and 14 of the
            // second line, 15 and 10 of the first, 16 (1063.9029, 20.4063), 42 0.927295; the other way round, the other way.
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(1000, 0), P(1100, 0), Enter, "LINE", P(1000, 0), P(1060, 80), Enter);
            string level = All<DxfLine>(document.ModelSpace)[0].Handle;
            string slope = All<DxfLine>(document.ModelSpace)[1].Handle;

            Run(session, "DIMANGULAR", P(1070, 0), P(1030, 40), P(1060, 30));

            var angle = Made<DxfLineAngularDimension2>(document);
            Near(new GeoPoint3(1060, 80, 0), angle.XLine1End);
            Near(new GeoPoint3(1100, 0, 0), angle.XLine2End);
            Near(new GeoPoint3(1063.9029, 20.4063, 0), angle.ArcPoint, 1E-4);
            Assert.Equal(0.927295, Written42(document, angle.Handle), 6);
            Assert.Equal(slope, Tie(angle, 0).ObjectHandle);
            Assert.Equal(level, Tie(angle, 2).ObjectHandle);

            Run(session, "DIMANGULAR", P(1030, 40), P(1070, 0), P(1060, 30));

            angle = Made<DxfLineAngularDimension2>(document);
            Near(new GeoPoint3(1100, 0, 0), angle.XLine1End);
            Near(new GeoPoint3(1060, 80, 0), angle.XLine2End);
            Near(new GeoPoint3(1063.9029, 20.4063, 0), angle.ArcPoint, 1E-4);
            Assert.Equal(level, Tie(angle, 0).ObjectHandle);
            Assert.Equal(slope, Tie(angle, 2).ObjectHandle);
        }

        [Theory]
        [InlineData(0, 0, 20, 1, -40, -20)]
        [InlineData(0, 0, 20, 1, -150, -130)]
        [InlineData(0, 40, 20, 1, 5, 60)]
        [InlineData(0, 40, 20, 1, 30, 60)]
        [InlineData(0, 40, 20, 2, -5, 55)]
        [InlineData(0, 40, 19, 1, 5, 59)]
        [InlineData(0, 40, 21, 1, 5, -14)]
        public void ADimLinearWhoseOriginMoves_KeepsItsLineFromTheNearerOrigin_OrFromTheOtherWhereThatComesTooNear_AsAutoCad(
            double first,
            double second,
            double line,
            int moved,
            double by,
            double expected
        )
        {
            // AutoCAD 2021: origins at the ends of two lines, (0, y1) and (60, y2), a horizontal DIMLINEAR at y = line; one line
            // moved up by the amount given, the dimension's line went to the height given.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(-20, first), P(0, first), Enter);
            string a = Last(document);
            Run(session, "LINE", P(60, second), P(80, second), Enter);
            string b = Last(document);
            Run(session, "DIMLINEAR");
            Snap(session, CadSnapModes.Endpoint, P(0, first));
            Snap(session, CadSnapModes.Endpoint, P(60, second));
            Run(session, "H", P(30, line));
            var dimension = Made<DxfRotatedDimension>(document);

            Move(session, moved == 1 ? a : b, 0, by);

            Assert.Equal(expected, Entity<DxfRotatedDimension>(document, dimension.Handle).DimLinePoint.Y, 9);
        }

        [Fact]
        public void AnUprightDimLinearBetweenItsOrigins_KeepsItsLineFromTheOriginItLiesAcrossFrom_AsAutoCad()
        {
            // AutoCAD 2021: origins (0, -4000) and (40, -3940), a vertical DIMLINEAR at x = 20, 20 from each; the first moved
            // to (5, -4000): the line to x = -15, 20 from the second's side of the first.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, -4000), P(0, -4020), Enter);
            string a = Last(document);
            Run(session, "LINE", P(40, -3940), P(40, -3920), Enter, "DIMLINEAR");
            Snap(session, CadSnapModes.Endpoint, P(0, -4000));
            Snap(session, CadSnapModes.Endpoint, P(40, -3940));
            Run(session, "V", P(20, -3970));
            var dimension = Made<DxfRotatedDimension>(document);

            Move(session, a, 5, 0);

            Assert.Equal(-15, Entity<DxfRotatedDimension>(document, dimension.Handle).DimLinePoint.X, 9);
        }

        [Theory]
        [InlineData(false, 0, 9, 20, 10, 1.5, 20.7295, 20.7994)]
        [InlineData(false, 300, 9, 2, 1, 1.5, 301.2705, 301.2006)]
        [InlineData(true, 200, 40, 60, 30, 1.5, 260.8112, 260.8811)]
        [InlineData(true, 400, 9, 2, 1, 15, 401.1888, 401.1189)]
        public void TheWordsOfARadiusOrADiameterGainingADigit_GrowFromTheirEndOnTheLeadersSide_AsAutoCad(
            bool diameter,
            double x,
            double radius,
            double right,
            double up,
            double scale,
            double made,
            double scaled
        )
        {
            // AutoCAD 2021: the circle about (x, -500) picked at 30 degrees, its words put (right, up) from its center; the circle
            // scaled about its center: "R9.0000" to "R13.5000", "%%c80.0000" to "%%c120.0000", "%%c18.0000" to "%%c270.0000".
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            Run(session, "CIRCLE", P(x, -500), radius.ToString(CultureInfo.InvariantCulture));
            string circle = Last(document);
            Run(session, diameter ? "DIMDIAMETER" : "DIMRADIUS", Polar(x, -500, radius, 30), P(x + right, -500 + up));
            DxfDimension dimension = Made<DxfDimension>(document);
            Assert.Equal(made, dimension.TextPosition.X, 4);
            document.Selection.Set(new[] { circle });

            Run(session, "SCALE", P(x, -500), scale.ToString(CultureInfo.InvariantCulture));

            dimension = Entity<DxfDimension>(document, dimension.Handle);
            Assert.Equal(scaled, dimension.TextPosition.X, 4);
            Assert.Equal(-500 + up, dimension.TextPosition.Y, 9);
        }

        [Theory]
        [InlineData(150, -980)]
        [InlineData(-150, -980)]
        [InlineData(150, -1000)]
        public void ThreePointsAlongOneRay_MeasureNothing_TheArcOnTheRayAsFarOutAsPut_AsAutoCad(double x, double y)
        {
            // AutoCAD 2021: DIMANGULAR of the vertex (0, -1000) and (100, -1000), (200, -1000), its arc put at (150, -980), at
            // (-150, -980) behind the vertex or at (150, -1000) on the ray: 42 0, the arc point that far out along the ray.
            CadDocument document = Arial();
            CadCommandSession session = Session(document);

            Run(session, "DIMANGULAR", Enter, P(0, -1000), P(100, -1000), P(200, -1000), P(x, y));

            var angle = Made<DxfPoint3AngularDimension>(document);
            double far = Math.Sqrt(x * x + (y + 1000) * (y + 1000));
            Near(new GeoPoint3(far, -1000, 0), angle.ArcPoint, 1E-9);
            Assert.Equal(0, Written42(document, angle.Handle), 9);
            Near(new GeoPoint3(far - 0.3771, -1000.36, 0), angle.TextPosition, 1E-3);
            AssertReopens(document);
        }

        [Fact]
        public void AThreePointAngleWhoseLegsComeToLieAlongOneRay_MeasuresNothing_ItsWordsBackFromTheArc_AsAutoCad()
        {
            // AutoCAD 2021: DIMANGULAR of the POINT (300, -1000), the ends (400, -1000) and (400, -940), the arc at (380, -985);
            // the second line moved by (100, -60): 42 0, the arc point (381.3941, -1000), the words at (381.0167, -1000.36).
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            Run(session, "POINT", P(300, -1000), "LINE", P(350, -1000), P(400, -1000), Enter);
            Run(session, "LINE", P(350, -940), P(400, -940), Enter);
            string moved = Last(document);
            Run(session, "DIMANGULAR", Enter);
            Snap(session, CadSnapModes.Node, P(300, -1000));
            Snap(session, CadSnapModes.Endpoint, P(400, -1000));
            Snap(session, CadSnapModes.Endpoint, P(400, -940));
            Run(session, P(380, -985));
            var angle = Made<DxfPoint3AngularDimension>(document);

            Move(session, moved, 100, -60);

            angle = Entity<DxfPoint3AngularDimension>(document, angle.Handle);
            Near(new GeoPoint3(500, -1000, 0), angle.XLine2Point);
            Near(new GeoPoint3(381.3941, -1000, 0), angle.ArcPoint, 1E-4);
            Assert.Equal(0, Written42(document, angle.Handle), 9);
            Near(new GeoPoint3(381.0167, -1000.36, 0), angle.TextPosition, 1E-3);
        }

        [Theory]
        [InlineData(0, -5, -2, -3.5, 2.5592, -3000.6176)]
        [InlineData(300, 100, 103, 101.5, 300.2133, -2997.0044)]
        [InlineData(600, 200, 202, 201, 596.7001, -3000.681)]
        public void TheWordsOfASmallAnglePutPastItsFirstRay_RunOutTheWayTheArcRunsOn_AsAutoCad(
            double x,
            double from,
            double to,
            double at,
            double wordsX,
            double wordsY
        )
        {
            // AutoCAD 2021: the vertex (x, -3000), points 50 out at the angles given, the arc put 3 out: words too wide for it
            // past the first ray, level, to the left of the arc run on down from -5 degrees, to the right of that from 100.
            CadDocument document = Arial();
            CadCommandSession session = Session(document);

            Run(
                session,
                "DIMANGULAR",
                Enter,
                P(x, -3000),
                Polar(x, -3000, 50, from),
                Polar(x, -3000, 50, to),
                Polar(x, -3000, 3, at)
            );

            Near(new GeoPoint3(wordsX, wordsY, 0), Made<DxfDimension>(document).TextPosition, 1E-3);
        }

        [Fact]
        public void TheSecondPointOfAnAngleOfACircleOnAnArcOfAPolyline_IsTiedToItsSegmentAtNought_AsAutoCad()
        {
            // AutoCAD 2021: the circle of 25 about (400, -1500), a polyline from (440, -1560) to (480, -1560) and round to
            // (480, -1500), _nea (510, -1530), the arc at (450, -1470): the second point tied as Nearest of segment 2 at 0; the
            // polyline moved up 10, 14 went to (510, -1520), 42 6.103332, 10 (373.9232, -1447.8464).
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            Run(
                session,
                "CIRCLE",
                P(400, -1500),
                "25",
                "PLINE",
                P(440, -1560),
                P(480, -1560),
                "A",
                P(480, -1500),
                Enter
            );
            string polyline = Last(document);
            Run(session, "DIMANGULAR", P(425, -1500));
            Snap(session, CadSnapModes.Nearest, P(510, -1530));
            Run(session, P(450, -1470));
            var angle = Made<DxfPoint3AngularDimension>(document);
            DxfOsnapPointRef second = Tie(angle, 1);
            Assert.Equal(polyline, second.ObjectHandle);
            Assert.Equal(DxfOsnapType.Nearest, second.OsnapType);
            Assert.Equal(2, second.GsMarker);
            Assert.Equal(0, second.NearParameter, 12);

            Move(session, polyline, 0, 10);

            angle = Entity<DxfPoint3AngularDimension>(document, angle.Handle);
            Near(new GeoPoint3(510, -1520, 0), angle.XLine2Point, 1E-9);
            Assert.Equal(6.103332, Written42(document, angle.Handle), 6);
            Near(new GeoPoint3(373.9232, -1447.8464, 0), angle.ArcPoint, 1E-4);
            Near(new GeoPoint3(341.9261, -1494.7635, 0), angle.TextPosition, 1E-3);
        }

        [Fact]
        public void TheSecondPointOfAnAngleOfACircleOnTheCircleItself_IsTiedAtItsAngle_AndFollowsTheCircle_AsAutoCad()
        {
            // AutoCAD 2021: the circle of 25 about (200, -1500), _nea at 45 degrees, the arc at (240, -1460): Nearest of the
            // circle at 0.785398; the circle moved down 10, 14 (217.6777, -1492.3223), 10 (254.641, -1495.359).
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            Run(session, "CIRCLE", P(200, -1500), "25");
            string circle = Last(document);
            Run(session, "DIMANGULAR", P(225, -1500));
            Snap(session, CadSnapModes.Nearest, P(217.67767, -1482.32233));
            Run(session, P(240, -1460));
            var angle = Made<DxfPoint3AngularDimension>(document);
            Assert.Equal(circle, Tie(angle, 1).ObjectHandle);
            Assert.Equal(Math.PI / 4.0, Tie(angle, 1).NearParameter, 6);

            Move(session, circle, 0, -10);

            angle = Entity<DxfPoint3AngularDimension>(document, angle.Handle);
            Near(new GeoPoint3(217.6777, -1492.3223, 0), angle.XLine2Point, 1E-4);
            Near(new GeoPoint3(254.641, -1495.359, 0), angle.ArcPoint, 1E-3);
        }

        [Fact]
        public void DimArc_IsTiedToTheEndsOfItsLengthAndTheArcsCentre_AsAutoCad()
        {
            // AutoCAD 2021: DIMARC of the arc of 50 about (0, -2000) from 0 to 120 degrees: StartPoint at 0, Endpoint at
            // 2.094395, Center; put on the rest of the circle, 13 and 14 still the arc's start and end, 42 209.43951; partial from
            // 100 degrees to 20: Nearest at 1.745329 and 0.349066, 13 the first point given, 40 1.745329.
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            Run(session, "ARC", "C", P(0, -2000), P(50, -2000), "A", "120");
            string arc = Last(document);

            Run(session, "DIMARC", P(25, -1956.69873), P(0, -1920));

            var length = Made<DxfArcDimension>(document);
            Assert.Equal(DxfOsnapType.StartPoint, Tie(length, 0).OsnapType);
            Assert.Equal(DxfOsnapType.Endpoint, Tie(length, 1).OsnapType);
            Assert.Equal(2.094395, Tie(length, 1).NearParameter, 6);
            Assert.Equal(DxfOsnapType.Center, Tie(length, 2).OsnapType);
            Assert.All(length.DimAssoc.PointReferences, r => Assert.Equal(arc, r.ObjectHandle));
            AssertReopens(document);

            Run(session, "DIMARC", P(25, -1956.69873), Polar(0, -2000, 80, 240));

            length = Made<DxfArcDimension>(document);
            Near(new GeoPoint3(50, -2000, 0), length.XLine1Point, 1E-9);
            Assert.Equal(209.43951, Written42(document, length.Handle), 5);
            Assert.Equal(DxfOsnapType.StartPoint, Tie(length, 0).OsnapType);

            Run(session, "DIMARC", P(25, -1956.69873), "P", Polar(0, -2000, 50, 100), Polar(0, -2000, 50, 20));
            Run(session, Polar(0, -2000, 80, 60));

            length = Made<DxfArcDimension>(document);
            Near(new GeoPoint3(Polar(0, -2000, 50, 100).X, Polar(0, -2000, 50, 100).Y, 0), length.XLine1Point, 1E-9);
            Assert.Equal(1.745329, length.ArcStartParam, 6);
            Assert.Equal(69.81317, Written42(document, length.Handle), 5);
            Assert.Equal(DxfOsnapType.Nearest, Tie(length, 0).OsnapType);
            Assert.Equal(1.745329, Tie(length, 0).NearParameter, 6);
            Assert.Equal(0.349066, Tie(length, 1).NearParameter, 6);
        }

        [Fact]
        public void DimArc_FollowsItsArcMovedTurnedScaledAndLengthened_AsAutoCad()
        {
            // AutoCAD 2021: arcs of 50 from 0 to 120 degrees dimensioned 80 out. Moved by (10, 5): every point with it. Then
            // turned 30 degrees about its centre: 13 (53.3013, -1970), 10 (37.3616, -1919.8246), 11 (10, -1915), the leader's 16
            // left. Another scaled by 1.5: 10 and 11 where they were, 42 157.079633. Another lengthened by 10 at its end: 14
            // (1166.8957, -1962.5286), 10 (1257.7218, -1944.6087), 11 (1232.8835, -1927.0708), 42 114.719755.
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            Run(session, "ARC", "C", P(0, -2000), P(50, -2000), "A", "120");
            string turned = Last(document);
            Run(session, "DIMARC", P(25, -1956.69873), P(0, -1920));
            var first = Made<DxfArcDimension>(document);
            Run(session, "ARC", "C", P(1500, -2000), P(1550, -2000), "A", "120");
            string scaled = Last(document);
            Run(session, "DIMARC", P(1525, -1956.69873), P(1500, -1920));
            var second = Made<DxfArcDimension>(document);
            Run(session, "ARC", "C", P(1200, -2000), P(1250, -2000), "A", "120");
            Run(session, "DIMARC", P(1225, -1956.69873), P(1200, -1920));
            var third = Made<DxfArcDimension>(document);

            Move(session, turned, 10, 5);

            first = Entity<DxfArcDimension>(document, first.Handle);
            Near(new GeoPoint3(10, -1995, 0), first.CenterPoint, 1E-9);
            Near(new GeoPoint3(71.2836, -1943.577, 0), first.ArcPoint, 1E-4);
            Near(new GeoPoint3(60, -1995, 0), first.Leader1Point, 1E-9);
            Near(new GeoPoint3(50, -1925.718, 0), first.TextPosition, 1E-3);

            document.Selection.Set(new[] { turned });
            Run(session, "ROTATE", P(10, -1995), "30");

            first = Entity<DxfArcDimension>(document, first.Handle);
            Near(new GeoPoint3(53.3013, -1970, 0), first.XLine1Point, 1E-4);
            Near(new GeoPoint3(-33.3013, -1970, 0), first.XLine2Point, 1E-4);
            Near(new GeoPoint3(37.3616, -1919.8246, 0), first.ArcPoint, 1E-4);
            Near(new GeoPoint3(10, -1915, 0), first.TextPosition, 1E-3);
            Near(new GeoPoint3(60, -1995, 0), first.Leader1Point, 1E-9);
            Assert.Equal(0, first.ArcStartParam, 9);

            document.Selection.Set(new[] { scaled });
            Run(session, "SCALE", P(1500, -2000), "1.5");

            second = Entity<DxfArcDimension>(document, second.Handle);
            Near(new GeoPoint3(1575, -2000, 0), second.XLine1Point, 1E-9);
            Near(new GeoPoint3(1561.2836, -1948.577, 0), second.ArcPoint, 1E-4);
            Near(new GeoPoint3(1540, -1930.718, 0), second.TextPosition, 1E-3);
            Assert.Equal(157.079633, Written42(document, second.Handle), 6);

            document.Selection.Set(new string[0]);
            Run(session, "LENGTHEN", "DE", "10", P(1182.89899, -1953.01537), Enter);

            third = Entity<DxfArcDimension>(document, third.Handle);
            Near(new GeoPoint3(1166.8957, -1962.5286, 0), third.XLine2Point, 1E-4);
            Near(new GeoPoint3(1257.7218, -1944.6087, 0), third.ArcPoint, 1E-4);
            Near(new GeoPoint3(1232.8835, -1927.0708, 0), third.TextPosition, 1E-3);
            Assert.Equal(114.719755, Written42(document, third.Handle), 6);
        }
    }
}

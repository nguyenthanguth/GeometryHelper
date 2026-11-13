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
    /// Arc length and angular dimensions as AutoCAD 2021 writes them, beyond the earlier rounds: the arc point of an arc length
    /// dimension under 90 degrees of sweep and put outside the arc, of a partial length, and of one whose words do not fit;
    /// the order and parameters of a partial length's points and ties; DIMARC of the arc segment of a polyline; the arc point
    /// of an angle whose words stand outside. The numbers are those AutoCAD 2021's console gave for the same drawings, made
    /// with its own template (Standard in arial.ttf, text 0.18 high).
    /// </summary>
    public class DimensionAssociativeFollowUps3Tests
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

        private static GeoPoint2 Polar(double x, double y, double radius, double degrees) =>
            P(x + radius * Math.Cos(degrees * Math.PI / 180.0), y + radius * Math.Sin(degrees * Math.PI / 180.0));

        private static string Last(CadDocument document) => document.ModelSpace.Block.Last(e => !e.IsErased).Handle;

        private static T Made<T>(CadDocument document)
            where T : DxfDimension => (T)document.ModelSpace.Block.Last(e => !e.IsErased && e is DxfDimension);

        private static DxfOsnapPointRef Tie(DxfDimension dimension, int point) =>
            dimension.DimAssoc.PointReferences.Single(r => r.PointIndex == point);

        private static void Move(CadCommandSession session, string handle, double x, double y)
        {
            session.Document.Selection.Set(new[] { handle });
            Run(session, "MOVE", P(0, 0), P(x, y));
        }

        private static string[] GroupLines(CadDocument document, string handle)
        {
            string[] lines = document.Database.ToText().Split('\n');

            for (int i = 0; i + 3 < lines.Length; i += 2)
            {
                if (lines[i].Trim() == "0" && lines[i + 2].Trim() == "5" && lines[i + 3].Trim() == handle)
                {
                    int end = i + 2;

                    while (end + 1 < lines.Length && lines[end].Trim() != "0")
                    {
                        end += 2;
                    }

                    return lines.Skip(i).Take(end - i).Select(l => l.Trim()).ToArray();
                }
            }

            return new string[0];
        }

        /// <summary>The second value written under a group code of an entity (group 40 and 41 of an arc dimension come after the common ones).</summary>
        private static double Written(CadDocument document, string handle, int code, int occurrence = 0)
        {
            string[] lines = GroupLines(document, handle);
            int seen = 0;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i] == code.ToString(CultureInfo.InvariantCulture) && seen++ == occurrence)
                {
                    return double.Parse(lines[i + 1], CultureInfo.InvariantCulture);
                }
            }

            return double.NaN;
        }

        private static GeoPoint3 Offset(double x, double y, double dx, double dy) => new GeoPoint3(x + dx, y + dy, 0);

        private static void ArcAndLength(
            CadCommandSession session,
            double x,
            double y,
            double from,
            double sweep,
            double distance,
            string[] partial = null,
            double[] points = null
        )
        {
            Run(session, "ARC", "C", P(x, y), Polar(x, y, 50, from), "A", sweep.ToString(CultureInfo.InvariantCulture));
            double mid = from + sweep / 2.0;

            if (points == null)
            {
                Run(session, "DIMARC", Polar(x, y, 50, mid), Polar(x, y, distance, mid));
            }
            else
            {
                Run(
                    session,
                    "DIMARC",
                    Polar(x, y, 50, mid),
                    "P",
                    Polar(x, y, 50, points[0]),
                    Polar(x, y, 50, points[1]),
                    Polar(x, y, distance, (points[0] + points[1]) / 2.0)
                );
            }
        }

        [Fact]
        public void DimArcUnderNinetyDegreesPutOutsideItsArc_PutsItsArcPointAThirdOfTheWayBetweenWhereTheExtensionLinesMeetItsArc_AsAutoCad()
        {
            // AutoCAD 2021: arcs of 50 from 10 degrees, dimensioned along their bisector: 30 degrees 70 out, 60 degrees 70 out,
            // 89 degrees 100 out put group 10 where a third of the way between the ends of the extension lines on the
            // dimension's arc comes; 30 degrees put 30 out (inside the arc) it puts where it was.
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            double y = -1000;
            ArcAndLength(session, 0, y, 10, 30, 70);
            Near(Offset(0, y, 65.152129, 25.596877), Made<DxfArcDimension>(document).ArcPoint, 1E-4);
            ArcAndLength(session, 300, y, 10, 60, 70);
            Near(Offset(300, y, 58.690262, 38.150402), Made<DxfArcDimension>(document).ArcPoint, 1E-4);
            ArcAndLength(session, 600, y, 10, 89, 100);
            Near(Offset(600, y, 67.350775, 73.918017), Made<DxfArcDimension>(document).ArcPoint, 1E-4);
            ArcAndLength(session, 900, y, 10, 30, 30);
            Near(Offset(900, y, 28.858475, 8.648642), Made<DxfArcDimension>(document).ArcPoint, 1E-4);
        }

        [Fact]
        public void DimArcOfAPartialLengthUnderNinetyDegrees_PutsItsArcPointWhereAutoCadDoes()
        {
            // AutoCAD 2021: the part from 20 to 50 degrees of the arc of 50 from 0 to 200 degrees, put 70 out at 35 degrees.
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            double y = -2000;
            ArcAndLength(session, 0, y, 0, 200, 70, null, new[] { 20.0, 50.0 });
            Near(Offset(0, y, 59.717471, 36.521551), Made<DxfArcDimension>(document).ArcPoint, 1E-4);
        }

        [Fact]
        public void DimArcOfAPartialLength_WritesTheLaterPointFirstAndTheParametersOfThePointsAsGivenCountedOnFromTheArcsStart_AsAutoCad()
        {
            // AutoCAD 2021: the part 20 to 50 degrees of the arc from 0 to 200, then 50 to 20, wrote group 13 at 50 degrees and
            // 14 at 20 either way; 40 and 41 the parameters of the points in the order given (0.349066, 0.872665; 0.872665,
            // 0.349066), the ties' the later point's first. The arc from 300 to 100 degrees, the part 20 to 60 degrees: 13 at 60
            // and 14 at 20, 40 and 41 6.632251 and 7.330383 (counted on from the start past a turn), its ties 7.330383 and
            // 6.632251 and the centre's 5.235988, the arc's start; the part 350 to 20: 13 at 20, 40 6.108652, 41 6.632251.
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            double y = -2000;
            ArcAndLength(session, 0, y, 0, 200, 30, null, new[] { 20.0, 50.0 });
            var first = Made<DxfArcDimension>(document);
            Near(Offset(0, y, 32.13938, 38.302222), first.XLine1Point, 1E-5);
            Near(Offset(0, y, 46.984631, 17.101007), first.XLine2Point, 1E-5);
            Assert.Equal(0.349066, first.ArcStartParam, 6);
            Assert.Equal(0.872665, first.ArcEndParam, 6);
            Assert.Equal(0.872665, Tie(first, 0).NearParameter, 6);
            Assert.Equal(0.349066, Tie(first, 1).NearParameter, 6);
            Assert.Equal(0.0, Tie(first, 2).NearParameter, 6);

            ArcAndLength(session, 300, y, 0, 200, 30, null, new[] { 50.0, 20.0 });
            var second = Made<DxfArcDimension>(document);
            Near(Offset(300, y, 32.13938, 38.302222), second.XLine1Point, 1E-5);
            Near(Offset(300, y, 46.984631, 17.101007), second.XLine2Point, 1E-5);
            Assert.Equal(0.872665, second.ArcStartParam, 6);
            Assert.Equal(0.349066, second.ArcEndParam, 6);

            ArcAndLength(session, 600, y, 300, 160, 30, null, new[] { 20.0, 60.0 });
            var over = Made<DxfArcDimension>(document);
            Near(Offset(600, y, 25.0, 43.30127), over.XLine1Point, 1E-5);
            Near(Offset(600, y, 46.984631, 17.101007), over.XLine2Point, 1E-5);
            Assert.Equal(6.632251, over.ArcStartParam, 6);
            Assert.Equal(7.330383, over.ArcEndParam, 6);
            Assert.Equal(7.330383, Tie(over, 0).NearParameter, 6);
            Assert.Equal(6.632251, Tie(over, 1).NearParameter, 6);
            Assert.Equal(5.235988, Tie(over, 2).NearParameter, 6);

            ArcAndLength(session, 900, y, 300, 160, 30, null, new[] { 350.0, 380.0 });
            var across = Made<DxfArcDimension>(document);
            Near(Offset(900, y, 46.984631, 17.101007), across.XLine1Point, 1E-5);
            Near(Offset(900, y, 49.240388, -8.682409), across.XLine2Point, 1E-5);
            Assert.Equal(6.108652, across.ArcStartParam, 6);
            Assert.Equal(6.632251, across.ArcEndParam, 6);
            AssertReopens(document);
        }

        [Fact]
        public void DimArcOfAnArcOverZeroDegrees_TiesItsEndAtTheParameterCountedOnFromItsStart_AndItsCentreAtItsStart_AsAutoCad()
        {
            // AutoCAD 2021: the arc of 50 from 300 to 100 degrees: StartPoint at 5.235988, Endpoint at 8.02851 (100 degrees
            // plus a turn), Center at 5.235988; groups 40 and 41 5.235988 and 1.745329.
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            ArcAndLength(session, 0, -2000, 300, 160, 70);
            var length = Made<DxfArcDimension>(document);
            Assert.Equal(DxfOsnapType.StartPoint, Tie(length, 0).OsnapType);
            Assert.Equal(5.235988, Tie(length, 0).NearParameter, 6);
            Assert.Equal(DxfOsnapType.Endpoint, Tie(length, 1).OsnapType);
            Assert.Equal(8.02851, Tie(length, 1).NearParameter, 5);
            Assert.Equal(DxfOsnapType.Center, Tie(length, 2).OsnapType);
            Assert.Equal(5.235988, Tie(length, 2).NearParameter, 6);
            Assert.Equal(5.235988, length.ArcStartParam, 6);
            Assert.Equal(1.745329, length.ArcEndParam, 6);
        }

        [Fact]
        public void DimArcOfThePolylinesArcSegmentOfAQuarterTurn_IsMadeAndTiedToItsSegment_AsAutoCad()
        {
            // AutoCAD 2021: the polyline (0, 0), (100, 0) and round to (150, 50), its arc picked at (135.355, 14.645),
            // DIMARC put at (140, -40): 13 (100, 0), 14 (150, 50), 15 (100, 50), 10 (149.244, -35.2936), measured 78.5398;
            // tied to its second segment: StartPoint at 4.712389, Endpoint at 6.283185, Center at 4.712389.
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            Run(session, "PLINE", P(0, 0), P(100, 0), "A", P(150, 50), Enter);
            string polyline = Last(document);

            Run(session, "DIMARC", Polar(100, 50, 50, -45), P(140, -40));

            var length = Made<DxfArcDimension>(document);
            Near(new GeoPoint3(100, 0, 0), length.XLine1Point, 1E-6);
            Near(new GeoPoint3(150, 50, 0), length.XLine2Point, 1E-6);
            Near(new GeoPoint3(100, 50, 0), length.CenterPoint, 1E-6);
            Near(new GeoPoint3(149.244, -35.2936, 0), length.ArcPoint, 1E-3);
            Assert.Equal(78.5398, Written(document, length.Handle, 42), 3);
            Assert.All(length.DimAssoc.PointReferences, r => Assert.Equal(polyline, r.ObjectHandle));
            Assert.All(length.DimAssoc.PointReferences, r => Assert.Equal(2, r.GsMarker));
            Assert.Equal(DxfOsnapType.StartPoint, Tie(length, 0).OsnapType);
            Assert.Equal(4.712389, Tie(length, 0).NearParameter, 6);
            Assert.Equal(DxfOsnapType.Endpoint, Tie(length, 1).OsnapType);
            Assert.Equal(6.283185, Tie(length, 1).NearParameter, 6);
            Assert.Equal(DxfOsnapType.Center, Tie(length, 2).OsnapType);
            Assert.Equal(4.712389, Tie(length, 2).NearParameter, 6);
            AssertReopens(document);
        }

        [Fact]
        public void DimArcOfAPolylinesArcRunClockwise_StartsAtTheEndOfTheSegment_AsAutoCad()
        {
            // AutoCAD 2021: the segment from (391.318, 149.240) round clockwise to (446.985, 82.899) of bulge -0.57735 about
            // (400, 100): the arc as it runs counter-clockwise starts at the segment's end, 13 at 340 degrees and 14 at 100; the
            // first tie an Endpoint at 5.934119, the second a StartPoint at 8.02851, the Center at 5.934119; group 10 at (465.778,
            // 123.941) for words put 70 out at 40 degrees.
            CadDocument document = Arial();
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, P(320, 40), 0, 0, 0);
            polyline.AddVertexAt(1, P(391.317591, 149.240388), -Math.Tan(Math.PI / 6.0 * 1.0), 0, 0);
            polyline.AddVertexAt(2, P(446.984631, 82.898993), 0, 0, 0);
            document.Database.ModelSpace.AppendEntity(polyline);
            CadCommandSession session = Session(document);

            Run(session, "DIMARC", Polar(400, 100, 50, 40), Polar(400, 100, 70, 40));

            var length = Made<DxfArcDimension>(document);
            Near(new GeoPoint3(446.984631, 82.898993, 0), length.XLine1Point, 1E-4);
            Near(new GeoPoint3(391.317591, 149.240388, 0), length.XLine2Point, 1E-4);
            Near(new GeoPoint3(465.778483, 123.94141, 0), length.ArcPoint, 1E-4);
            Assert.Equal(DxfOsnapType.Endpoint, Tie(length, 0).OsnapType);
            Assert.Equal(5.934119, Tie(length, 0).NearParameter, 6);
            Assert.Equal(DxfOsnapType.StartPoint, Tie(length, 1).OsnapType);
            Assert.Equal(8.02851, Tie(length, 1).NearParameter, 5);
            Assert.Equal(5.934119, Tie(length, 2).NearParameter, 6);
            Assert.Equal(5.934119, length.ArcStartParam, 6);
            Assert.Equal(1.745329, length.ArcEndParam, 6);
        }

        [Fact]
        public void DimArcOfAPartLengthOnAPolylinesArc_TiesNearestPointsAtTheParametersCountedOnFromTheArcsStart_AsAutoCad()
        {
            // AutoCAD 2021: the arc from 30 to 130 degrees of 50 about (1000, 100) (a polyline segment of bulge 0.466308 from
            // (1043.301, 125) to (967.861, 138.302)), the part from 55 to 105 degrees put 70 out at 80: group 10 (1019.126,
            // 167.336), 13 at 105 degrees and 14 at 55, 40 0.959931 and 41 1.832596, Nearest ties 1.832596 and 0.959931 of the
            // second segment, the Center's 0.523599.
            CadDocument document = Arial();
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, P(920, 40), 0, 0, 0);
            polyline.AddVertexAt(1, P(1043.30127, 125), Math.Tan(25.0 * Math.PI / 180.0), 0, 0);
            polyline.AddVertexAt(2, P(967.860622, 138.302223), 0, 0, 0);
            document.Database.ModelSpace.AppendEntity(polyline);
            CadCommandSession session = Session(document);

            Run(
                session,
                "DIMARC",
                Polar(1000, 100, 50, 80),
                "P",
                Polar(1000, 100, 50, 55),
                Polar(1000, 100, 50, 105),
                Polar(1000, 100, 70, 80)
            );

            var length = Made<DxfArcDimension>(document);
            Assert.True(length.IsPartial);
            Near(new GeoPoint3(987.059048, 148.296291, 0), length.XLine1Point, 1E-4);
            Near(new GeoPoint3(1028.678822, 140.957602, 0), length.XLine2Point, 1E-4);
            Near(new GeoPoint3(1019.12621, 167.33638, 0), length.ArcPoint, 1E-4);
            Assert.Equal(0.959931, length.ArcStartParam, 6);
            Assert.Equal(1.832596, length.ArcEndParam, 6);
            Assert.Equal(DxfOsnapType.Nearest, Tie(length, 0).OsnapType);
            Assert.Equal(1.832596, Tie(length, 0).NearParameter, 6);
            Assert.Equal(0.959931, Tie(length, 1).NearParameter, 6);
            Assert.Equal(0.523599, Tie(length, 2).NearParameter, 6);
            Assert.All(length.DimAssoc.PointReferences, r => Assert.Equal(2, r.GsMarker));
        }

        [Fact]
        public void DimArcOfAPolylinesArc_FollowsItsPolylineMovedTurnedAndScaled_AsAutoCad()
        {
            // AutoCAD 2021: the polyline (x, 100), (x + 100, 100) and round to (x + 150, 150), DIMARC put at (x + 140, 60). Moved by
            // (10, 5): 13 (x + 110, 105), 14 (x + 160, 155), 10 (x + 159.244, 69.706). Turned 30 degrees about the arc's centre
            // (x + 100, 150): 13 (x + 125, 106.699), 14 (x + 143.301, 175), 10 (x + 185.294, 100.756). Scaled by 1.5 about its first
            // vertex: 13 (x + 150, 100), 14 (x + 225, 175), 10 (x + 199.244, 89.706).
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            var made = new DxfArcDimension[3];
            var polylines = new string[3];

            for (int i = 0; i < 3; i++)
            {
                double x = 200.0 * i;
                Run(session, "PLINE", P(x, 100), P(x + 100, 100), "A", P(x + 150, 150), Enter);
                polylines[i] = Last(document);
                Run(session, "DIMARC", P(x + 100 + 50 * Math.Sqrt(0.5), 150 - 50 * Math.Sqrt(0.5)), P(x + 140, 60));
                made[i] = Made<DxfArcDimension>(document);
            }

            Move(session, polylines[0], 10, 5);
            var moved = Entity<DxfArcDimension>(document, made[0].Handle);
            Near(new GeoPoint3(110, 105, 0), moved.XLine1Point, 1E-3);
            Near(new GeoPoint3(160, 155, 0), moved.XLine2Point, 1E-3);
            Near(new GeoPoint3(159.24428, 69.706383, 0), moved.ArcPoint, 1E-3);

            document.Selection.Set(new[] { polylines[1] });
            Run(session, "ROTATE", P(300, 150), "30");
            var turned = Entity<DxfArcDimension>(document, made[1].Handle);
            Near(new GeoPoint3(325, 106.69873, 0), turned.XLine1Point, 1E-3);
            Near(new GeoPoint3(343.30127, 175, 0), turned.XLine2Point, 1E-3);
            Near(new GeoPoint3(385.293607, 100.755701, 0), turned.ArcPoint, 1E-3);

            document.Selection.Set(new[] { polylines[2] });
            Run(session, "SCALE", P(400, 100), "1.5");
            var scaled = Entity<DxfArcDimension>(document, made[2].Handle);
            Near(new GeoPoint3(550, 100, 0), scaled.XLine1Point, 1E-3);
            Near(new GeoPoint3(625, 175, 0), scaled.XLine2Point, 1E-3);
            Near(new GeoPoint3(599.244299, 89.706364, 0), scaled.ArcPoint, 1E-3);
        }

        [Fact]
        public void ARadiusAndADiameterTiedToTheirCircle_KeepFlag128OfGroup70_WhenTheCircleMovesOrGrows_AsAutoCad()
        {
            // AutoCAD 2021: DIMRADIUS writes 164 and DIMDIAMETER 163 (flag 128: the words stand where they were put) for words put
            // 35 out, 10 in or 21 out of a circle of 20, and writes the same after the circle moved by (10, 5) or was scaled by 1.5:
            // the radius's words (31.110, 17.5) from the centre moved with it, a diameter's (57.849, 5) kept where they stood.
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            Run(session, "CIRCLE", P(0, 100), "20");
            string moved = Last(document);
            Run(session, "DIMRADIUS", Polar(0, 100, 20, 30), Polar(0, 100, 35, 30));
            var radius = Made<DxfRadialDimension>(document);
            Assert.Equal(164, Written(document, radius.Handle, 70), 6);
            Run(session, "CIRCLE", P(1350, 100), "20");
            string grown = Last(document);
            Run(session, "DIMDIAMETER", Polar(1350, 100, 20, 30), Polar(1350, 100, 10, 30));
            var diameter = Made<DxfDiametricDimension>(document);
            Assert.Equal(163, Written(document, diameter.Handle, 70), 6);

            Move(session, moved, 10, 5);
            radius = Entity<DxfRadialDimension>(document, radius.Handle);
            Assert.Equal(164, Written(document, radius.Handle, 70), 6);
            Near(new GeoPoint3(41.110268, 122.5, 0), radius.TextPosition, 1E-5);

            document.Selection.Set(new[] { grown });
            Run(session, "SCALE", P(1350, 100), "1.5");
            diameter = Entity<DxfDiametricDimension>(document, diameter.Handle);
            Assert.Equal(163, Written(document, diameter.Handle, 70), 6);
            Near(new GeoPoint3(1357.849088, 105, 0), diameter.TextPosition, 1E-5);
        }

        [Fact]
        public void DimArcOfAStraightSegmentOfAPolyline_IsRefused()
        {
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            Run(session, "PLINE", P(0, 0), P(100, 0), "A", P(150, 50), Enter);
            int before = document.ModelSpace.Block.Count(e => !e.IsErased);

            Run(session, "DIMARC", P(50, 0));

            Assert.Equal(before, document.ModelSpace.Block.Count(e => !e.IsErased));
            Assert.DoesNotContain(document.ModelSpace.Block, e => e is DxfDimension);
        }

        [Fact]
        public void DimArcWhoseWordsStandOutsideItsArc_PutsItsArcPointTwoThirdsOfTheWayAlong_AsAutoCad()
        {
            // AutoCAD 2021: arcs of 1 and 3 from 10 degrees, text 0.18 high, put 1, 1.2 and 2 times their radius out: where the
            // words do not fit between the extension lines their arc point goes two thirds of the way along, not a third:
            // the arc of 1 through 20 degrees put 1.2 out; the arc of 1 through 40 degrees put 1 out; the arc of 3 through 5
            // degrees put 0.8 times its radius out (the arc moved in) and 2 times out.
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            double y = -4000;

            Run(session, "ARC", "C", P(280, y), Polar(280, y, 1, 10), "A", "20");
            Run(session, "DIMARC", Polar(280, y, 1, 20), Polar(280, y, 1.2, 20));
            Near(Offset(280, y, 1.106451, 0.464506), Made<DxfArcDimension>(document).ArcPoint, 1E-5);

            Run(session, "ARC", "C", P(180, y), Polar(180, y, 1, 10), "A", "40");
            Run(session, "DIMARC", Polar(180, y, 1, 30), Polar(180, y, 1.0, 30));
            Near(Offset(180, y, 0.802123, 0.597159), Made<DxfArcDimension>(document).ArcPoint, 1E-5);

            Run(session, "ARC", "C", P(480, y), Polar(480, y, 3, 10), "A", "5");
            Run(session, "DIMARC", Polar(480, y, 3, 12.5), Polar(480, y, 2.4, 12.5));
            Near(Offset(480, y, 2.333357, 0.561984), Made<DxfArcDimension>(document).ArcPoint, 1E-5);

            Run(session, "ARC", "C", P(840, y), Polar(840, y, 3, 10), "A", "5");
            Run(session, "DIMARC", Polar(840, y, 3, 12.5), Polar(840, y, 6, 12.5));
            Near(Offset(840, y, 5.84818, 1.341192), Made<DxfArcDimension>(document).ArcPoint, 1E-5);
        }

        [Fact]
        public void DimArcScaledSoItsWordsGoInOrOut_PutsItsArcPointAgainByWhereItsWordsStandNow_AsAutoCad()
        {
            // AutoCAD 2021: the arc of 1 through 20 degrees put 1.2 out (words outside) scaled by 5 about its centre: group 10
            // from (1.106, 0.465) to (1.218, 0.134) off the centre, a third of the way again as its words fit now; the arc of 3
            // through 40 degrees put 3.6 out (a third, between the extension lines) scaled by 0.2: from (3.276, 1.492) to (3.083,
            // 1.859), two thirds of the way now its words do not fit.
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            Run(session, "ARC", "C", P(100, 100), Polar(100, 100, 1, 10), "A", "20");
            string small = Last(document);
            Run(session, "DIMARC", Polar(100, 100, 1, 20), Polar(100, 100, 1.2, 20));
            var out1 = Made<DxfArcDimension>(document);
            Run(session, "ARC", "C", P(300, 100), Polar(300, 100, 3, 10), "A", "40");
            string wide = Last(document);
            Run(session, "DIMARC", Polar(300, 100, 3, 30), Polar(300, 100, 3.6, 30));
            var in1 = Made<DxfArcDimension>(document);
            Near(Offset(300, 100, 3.27638, 1.491755), in1.ArcPoint, 1E-5);

            document.Selection.Set(new[] { small });
            Run(session, "SCALE", P(100, 100), "5");
            out1 = Entity<DxfArcDimension>(document, out1.Handle);
            Near(new GeoPoint3(104.924039, 100.868241, 0), out1.XLine1Point, 1E-5);

            // AutoCAD 2021 moves the arc in by a rule of its own that is not reproduced to 1E-5: 0.001 off here, up to 0.04.
            Near(Offset(100, 100, 1.218112, 0.133974), out1.ArcPoint, 2E-3);

            document.Selection.Set(new[] { wide });
            Run(session, "SCALE", P(300, 100), "0.2");
            in1 = Entity<DxfArcDimension>(document, in1.Handle);
            Near(new GeoPoint3(300.590885, 100.104189, 0), in1.XLine1Point, 1E-5);
            Near(Offset(300, 100, 3.08291, 1.858943), in1.ArcPoint, 1E-5);
        }

        [Theory]
        [InlineData(1.0, 12.0, 1.188322, 0.167008)]
        [InlineData(1.0, 20.0, 1.167654, 0.276739)]
        [InlineData(1.0, 40.0, 1.167654, 0.276739)]
        [InlineData(2.0, 12.0, 2.376643, 0.334015)]
        [InlineData(2.0, 20.0, 2.383772, 0.278623)]
        [InlineData(5.0, 5.0, 5.989849, 0.348869)]
        [InlineData(5.0, 8.0, 5.993503, 0.279152)]
        public void AnAngleOfThreePoints_PutsItsArcPointTwoThirdsRoundWhereItsWordsStandOutside_AndAThirdWhereTheyFit_AsAutoCad(
            double radius,
            double degrees,
            double x,
            double y
        )
        {
            // AutoCAD 2021, text 0.18 high, the arc put 1.2 times the radius out through the middle of the angle: of 12 and 20
            // degrees at radius 1 the words stand outside (8 and 13.33 degrees round, two thirds), of 40 degrees they fit (13.33
            // degrees, a third); radius 2 puts 12 degrees outside, 20 inside; radius 5 puts 5 outside and 8 inside.
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            Run(session, "DIMANGULAR", Enter, P(0, 0), P(radius, 0), Polar(0, 0, radius, degrees));
            Run(session, Polar(0, 0, 1.2 * radius, degrees / 2.0));

            Near(new GeoPoint3(x, y, 0), Made<DxfPoint3AngularDimension>(document).ArcPoint, 1E-5);
        }

        [Theory]
        [InlineData(10.0, 2.383772, 0.278623)]
        [InlineData(20.0, 2.383772, 0.278623)]
        [InlineData(5.0, 2.39594, 0.139548)]
        public void AnAngleOfTwoLines_PutsItsArcPointTwoThirdsRoundWhereItsWordsStandOutside_AndAThirdWhereTheyFit_AsAutoCad(
            double degrees,
            double x,
            double y
        )
        {
            // AutoCAD 2021, text 0.18 high, two lines from one point, the arc put 2.4 out through the middle: 5 and 10 degrees
            // put the words outside (two thirds round: 3.33 and 6.67 degrees), 20 degrees has them inside (6.67, a third).
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            session.PickAperture = 0.05;
            Run(session, "LINE", P(0, 0), P(4, 0), Enter, "LINE", P(0, 0), Polar(0, 0, 4, degrees), Enter);
            Run(session, "DIMANGULAR", P(3.5, 0), Polar(0, 0, 3.5, degrees), Polar(0, 0, 2.4, degrees / 2.0));

            Near(new GeoPoint3(x, y, 0), Made<DxfLineAngularDimension2>(document).ArcPoint, 1E-5);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Rows under DIMCONTINUEMODE 0, the point a radius or a diameter records its words by, words DIMTMOVE 2 puts off the line
    /// of one, and words DIMTOH keeps level past a line within 15 degrees of level, as AutoCAD 2021 draws them. The drawings
    /// are blank ones with acad.dwt's Standard, its text style Arial, as accoreconsole made them: "R20" is 1.058759 wide and
    /// the sizes those of Standard, arrowheads and words 0.18, DIMGAP 0.09.
    /// </summary>
    public class DimensionFollowUps4Tests
    {
        /// <summary>The groups the dimension's overrides name, in the order they are written.</summary>
        private static string Groups(DxfDimension dimension)
        {
            IReadOnlyList<DxfTypedValue> data = dimension.GetXDataForApplication("ACAD");

            if (data == null)
            {
                return string.Empty;
            }

            int open = data.ToList().FindIndex(v => v.TypeCode == 1002 && v.Value == "{");
            var groups = new List<string>();

            for (int i = open + 1; i < data.Count && data[i].TypeCode == 1070; i += 2)
            {
                groups.Add(data[i].Value.Trim());
            }

            return string.Join(" ", groups);
        }

        /// <summary>A blank drawing whose Standard is current, its text style Arial, with the variables set as given.</summary>
        private static CadCommandSession Standard(Action<DxfDatabase> set)
        {
            CadDocument document = Blank();
            document.Database.TextStyleTable["Standard"].FileName = "arial.ttf";
            StandardCurrent(document.Database);
            set(document.Database);
            return Session(document);
        }

        /// <summary>A radius or a diameter of a circle of 20 about the origin, its words put at a point.</summary>
        private static DxfDimension Round(CadCommandSession session, string command, GeoPoint2 at)
        {
            Run(session, "C", P(0, 0), "20");
            string circle = session.Document.ModelSpace.Block.Cast<DxfEntity>().Last().Handle;
            Run(session, command, new[] { circle }, at);
            return session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
        }

        private static GeoPoint2 Polar(double degrees, double distance) =>
            new GeoPoint2(
                distance * Math.Cos(degrees * Math.PI / 180.0),
                distance * Math.Sin(degrees * Math.PI / 180.0)
            );

        private static DxfMText Words(DxfDimension dimension) => dimension.DimBlock.OfType<DxfMText>().Single();

        private static List<DxfLine> Lines(DxfDimension dimension) =>
            dimension.DimBlock.OfType<DxfLine>().Where(l => !l.IsErased).ToList();

        private static bool Same(GeoPoint3 a, GeoPoint2 b) => Math.Abs(a.X - b.X) < 1E-5 && Math.Abs(a.Y - b.Y) < 1E-5;

        private static void HasLine(DxfDimension dimension, GeoPoint2 a, GeoPoint2 b) =>
            Assert.Contains(Lines(dimension), l =>
                (Same(l.StartPoint, a) && Same(l.EndPoint, b)) || (Same(l.StartPoint, b) && Same(l.EndPoint, a))
            );

        [Fact]
        public void UnderDimcontinuemode0_ARowGoesOnInTheCurrentStyleScaleAndLayer_AsAutoCad()
        {
            // AutoCAD 2021 (probe GHCM): under DIMCONTINUEMODE 0 a row from a dimension made on L1 at DIMSCALE 2 with its
            // own DIMASZ goes on the current layer at the current DIMSCALE in the current variables, DIMCONTINUE still
            // writing 279 1 and DIMBASELINE nothing, its line DIMDLI times the current scale further out.
            CadCommandSession session = Standard(db =>
            {
                db.LayerTable.Add(new DxfLayerTableRecord { Name = "L1" });
                db.LayerTable.Add(new DxfLayerTableRecord { Name = "L2" });
            });
            DxfDatabase db = session.Document.Database;
            db.Clayer = "L1";
            db.Dimscale = 2.0;
            db.Dimasz = 0.3;
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, 20));
            db.Clayer = "L2";
            db.Dimscale = 1.0;
            db.Dimasz = 0.18;

            Run(session, "DIMCONTINUEMODE", "0");
            Run(session, "DCO", P(200, 0), Enter, Enter);
            Run(session, "DBA", P(300, 0), Enter, Enter);

            List<DxfDimension> made = All<DxfDimension>(session.Document.ModelSpace);
            Assert.Equal(new[] { "40 41", "279", "" }, made.Select(Groups));
            Assert.Equal(new[] { "L1", "L2", "L2" }, made.Select(d => d.Layer));
            Assert.Equal(1.0, made[2].Dimscale, 9);
            Near(new GeoPoint3(200, 20.38, 0), made[2].TextPosition, 1E-9);
        }

        [Fact]
        public void ARadiusUnderDimtmove1_RecordsItsWordsByThePointTheyWerePutAt_AsAutoCad()
        {
            // AutoCAD 2021 (probe GHRD): "R20" put 40 out at 30 degrees under DIMTMOVE 1, kept level by DIMTOH past a
            // landing, has its middle at (35.440395, 20) and writes (34.641016, 20), the point given, as group 11.
            CadCommandSession session = Standard(db =>
            {
                db.Dimtmove = 1;
                db.Dimtoh = true;
                db.Dimtad = 0;
            });
            DxfDimension radius = Round(session, "DRA", Polar(30, 40));
            Near(new GeoPoint3(35.440395, 20, 0), Words(radius).Location, 1E-6);
            Near(new GeoPoint3(34.641016, 20, 0), radius.TextPosition, 1E-6);
        }

        [Fact]
        public void WordsAboveARadiusLine_AreRecordedByThePointOfTheLineTheyStandBy_AsAutoCad()
        {
            // DIMTAD 1 lifts "R20" put 40 out at 30 degrees 0.18 off its line; AutoCAD 2021 writes the point of the line,
            // (34.641016, 20), as group 11, and under DIMTOH the point of the landing under the words' middle.
            CadCommandSession session = Standard(db =>
            {
                db.Dimtmove = 0;
                db.Dimtoh = false;
                db.Dimtad = 1;
            });
            DxfDimension radius = Round(session, "DRA", Polar(30, 40));
            Near(new GeoPoint3(34.550065, 20.157533, 0), Words(radius).Location, 1E-6);
            Near(new GeoPoint3(34.641016, 20, 0), radius.TextPosition, 1E-6);

            session = Standard(db =>
            {
                db.Dimtmove = 0;
                db.Dimtoh = true;
                db.Dimtad = 1;
            });
            radius = Round(session, "DRA", Polar(30, 40));
            Near(new GeoPoint3(35.260395, 20.181903, 0), Words(radius).Location, 1E-6);
            Near(new GeoPoint3(35.260395, 20, 0), radius.TextPosition, 1E-6);
        }

        [Fact]
        public void WordsPutOffARadiusUnderDimtmove2_StandLevelUnderDimtih_TheLineBrokenAboutTheirBox()
        {
            // AutoCAD 2021 keeps "R20" level where DIMTIH says so, whatever DIMTOH says, and breaks the line where it runs
            // through the words' box grown by DIMGAP: put 10 out at 30 degrees, from 9.636 to 10.364 along it.
            CadCommandSession session = Standard(db =>
            {
                db.Dimtmove = 2;
                db.Dimtoh = false;
                db.Dimtih = true;
                db.Dimtad = 0;
            });
            DxfDimension radius = Round(session, "DRA", Polar(30, 10));
            Assert.Equal(0.0, Words(radius).Rotation, 9);
            HasLine(radius, P(0, 0), P(8.345189, 4.818097));
            HasLine(radius, P(17.164624, 9.91), P(8.97532, 5.181903));
        }

        [Fact]
        public void WordsPutOverTheRimUnderDimtmove2_PutTheArrowheadOutside_AndMarkTheCentreWithoutDimtofl()
        {
            // "R20" put 20.4 out at 30 degrees, along the line, runs over the rim: AutoCAD 2021 puts the arrowhead outside on a
            // line an arrowhead long and, DIMTOFL off, marks the centre and draws no line from it. Kept level, 19.6 out, the
            // words reach past the back of the arrowhead inside and the line is drawn from it out to their end.
            CadCommandSession session = Standard(db =>
            {
                db.Dimtmove = 2;
                db.Dimtih = false;
                db.Dimtofl = false;
                db.Dimtad = 0;
            });
            DxfDimension radius = Round(session, "DRA", Polar(30, 20.4));
            Assert.Equal(3, Lines(radius).Count);
            HasLine(radius, P(17.476393, 10.09), P(17.632277, 10.18));
            HasLine(radius, P(-0.09, 0), P(0.09, 0));
            HasLine(radius, P(0, -0.09), P(0, 0.09));

            session = Standard(db =>
            {
                db.Dimtmove = 2;
                db.Dimtih = true;
                db.Dimtofl = false;
                db.Dimtad = 0;
            });
            radius = Round(session, "DRA", Polar(30, 19.6));
            Assert.Equal(2, Lines(radius).Count);
            HasLine(radius, P(0, 0), P(16.659032, 9.618097));
            HasLine(radius, P(17.164624, 9.91), P(17.289163, 9.981903));
        }

        [Fact]
        public void LevelWordsPastALineWithin15DegreesOfLevel_StandWithoutALanding_AsAutoCad()
        {
            // AutoCAD 2021 (probe GHLV): "1111" kept level by DIMTOH past an aligned dimension of 0.1 at 5 degrees stands a
            // gap past the end of the line run on, with no landing; at 30 degrees it stands past a landing an arrowhead long.
            DxfDimension Aligned(double degrees)
            {
                CadCommandSession session = Standard(db =>
                {
                    db.Dimtmove = 0;
                    db.Dimtoh = true;
                    db.Dimtad = 0;
                    db.Dimatfit = 3;
                });
                GeoPoint2 end = Polar(degrees, 0.1);
                GeoPoint2 middle = Polar(degrees, 0.05);
                GeoPoint2 off = Polar(degrees + 90, 1);
                Run(session, "DAL", P(0, 0), end, "T", "1111", P(middle.X + off.X, middle.Y + off.Y));
                return session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            }

            DxfDimension five = Aligned(5);
            Near(new GeoPoint3(0.70402, 1.036286, 0), Words(five).Location, 1E-5);
            Assert.Equal(4, Lines(five).Count);

            DxfDimension thirty = Aligned(30);
            Assert.Equal(5, Lines(thirty).Count);
            HasLine(thirty, P(-0.101628, 1.096025), P(0.078372, 1.096025));

            // A radius at 5 degrees alike: "R20" put 40 out stands a gap past the end of its line, which writes it as group 11.
            CadCommandSession round = Standard(db =>
            {
                db.Dimtmove = 0;
                db.Dimtoh = true;
                db.Dimtad = 0;
            });
            DxfDimension radius = Round(round, "DRA", Polar(5, 40));
            Near(new GeoPoint3(40.467167, 3.48623, 0), Words(radius).Location, 1E-5);
            Near(new GeoPoint3(39.847788, 3.48623, 0), radius.TextPosition, 1E-5);
            Assert.Equal(3, Lines(radius).Count);
        }
    }
}

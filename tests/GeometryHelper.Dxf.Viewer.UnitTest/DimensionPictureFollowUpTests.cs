using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The pictures of dimensions as AutoCAD 2021 builds them in their blocks, probed in its console: DIMTFILL's mask and
    /// DIMTXTDIRECTION on the words, an ordinate's jog under DIMFXLON, the words moved by their position under DIMTMOVE 0,
    /// and DIMLINEAR's text middle, measurement and its options Mtext, Angle and Rotated.
    /// </summary>
    public class DimensionPictureFollowUpTests
    {
        private const double Gap = 0.625;
        private const double Height = 2.5;
        private const double Arrow = 2.5;
        private const double Lift = Gap + Height / 2.0;

        /// <summary>A blank drawing whose Standard style has ISO-25's sizes, whole numbers, and the variables set as given.</summary>
        private static CadCommandSession Metric(Action<DxfDimStyleTableRecord> set)
        {
            CadDocument document = Blank();
            DxfDimStyleTableRecord style = document.Database.DimStyleTable["Standard"];
            style.Dimscale = 1.0;
            style.Dimasz = Arrow;
            style.Dimtxt = Height;
            style.Dimgap = Gap;
            style.Dimexo = 0.625;
            style.Dimexe = 1.25;
            style.Dimdec = 0;
            style.Dimtad = 1;
            style.Dimtih = false;
            style.Dimtoh = false;
            style.Dimtofl = true;
            style.Dimatfit = 3;
            set(style);
            StandardCurrent(document.Database);
            return Session(document);
        }

        private static DxfDimension Made(CadCommandSession session) =>
            session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();

        private static DxfMText Words(DxfDimension dimension) => dimension.DimBlock.OfType<DxfMText>().Single();

        private static List<DxfLine> Lines(DxfDimension dimension) =>
            dimension.DimBlock.OfType<DxfLine>().Where(l => !l.IsErased && l.Layer != "Defpoints").ToList();

        /// <summary>The lines of a picture on the level y, its dimension line, ordered from the left.</summary>
        private static List<DxfLine> OnLevel(DxfDimension dimension, double y) =>
            Lines(dimension)
                .Where(l => Math.Abs(l.StartPoint.Y - y) < 1E-9 && Math.Abs(l.EndPoint.Y - y) < 1E-9)
                .OrderBy(l => Math.Min(l.StartPoint.X, l.EndPoint.X))
                .ToList();

        private static double Left(DxfLine line) => Math.Min(line.StartPoint.X, line.EndPoint.X);

        private static double Right(DxfLine line) => Math.Max(line.StartPoint.X, line.EndPoint.X);

        /// <summary>The value of a group of the record of a handle, as the drawing writes it; null where the record has none.</summary>
        private static string Group(CadDocument document, string handle, int code)
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
                    if (lines[j].Trim() == code.ToString(CultureInfo.InvariantCulture))
                    {
                        return lines[j + 1].Trim();
                    }
                }

                return null;
            }

            return null;
        }

        [Fact]
        public void Dimtfill1_MasksTheWordsWithTheDrawingsBackground_AsAutoCadWritesTheirMText()
        {
            // AutoCAD 2021 writes the MTEXT of a DIMTFILL 1 dimension with 90 = 3, 63 = 9, 421 = 200,200,200 and 45 = 1.25.
            CadCommandSession session = Metric(s => s.Dimtfill = 1);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));

            DxfMText words = Words(Made(session));
            Assert.True(words.BackgroundFill);
            Assert.True(words.UseBackgroundColor);
            Assert.Equal(1.25, words.BackgroundScaleFactor, 12);
            Assert.Equal(9, words.BackgroundFillColor.ColorIndex);
            Assert.Equal(0xC8C8C8, words.BackgroundFillColor.ColorValue);
            AssertReopens(session.Document);
        }

        [Fact]
        public void Dimtfill2_MasksTheWordsInDimtfillclr_ByLayerAsAutoCadWritesIt()
        {
            CadCommandSession session = Metric(s =>
            {
                s.Dimtfill = 2;
                s.Dimtfillclr = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);
            });
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));

            DxfMText words = Words(Made(session));
            Assert.True(words.BackgroundFill);
            Assert.False(words.UseBackgroundColor);
            Assert.Equal(1.25, words.BackgroundScaleFactor, 12);
            Assert.True(words.BackgroundFillColor.IsByAci);
            Assert.Equal(1, words.BackgroundFillColor.ColorIndex);

            // AutoCAD 2021 writes DIMTFILLCLR BYLAYER as the index 256.
            session.Document.Database.DimStyleTable["Standard"].Dimtfillclr = DxfColor.ByLayer;
            StandardCurrent(session.Document.Database);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -60));
            words = Words(Made(session));
            Assert.True(words.BackgroundFill);
            Assert.True(words.BackgroundFillColor.IsByLayer);
            AssertReopens(session.Document);
        }

        [Fact]
        public void AFillSetInThePalette_DrawsTheMaskInThePicture_AndNoneTakesItOut()
        {
            CadCommandSession session = Metric(s => { });
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));
            string handle = Made(session).Handle;
            Assert.False(Words(Made(session)).BackgroundFill);

            Assert.True(CadPropertyEditor.Set(session.Document, new[] { handle }, "Fill color", "Red").Succeeded);
            DxfMText words = Words(Entity<DxfDimension>(session.Document, handle));
            Assert.True(words.BackgroundFill);
            Assert.Equal(1, words.BackgroundFillColor.ColorIndex);

            Assert.True(CadPropertyEditor.Set(session.Document, new[] { handle }, "Fill color", "None").Succeeded);
            Assert.False(Words(Entity<DxfDimension>(session.Document, handle)).BackgroundFill);
            AssertReopens(session.Document);
        }

        [Fact]
        public void Dimtxtdirection_TurnsTheWordsHalfRound_OnEveryKind_AsAutoCadWritesThem()
        {
            // AutoCAD 2021: a level dimension's words at 180 degrees, an upright one's (not kept level) and an X datum's at 270,
            // words turned to 30 by the dimension at 210; where they stand does not change.
            CadCommandSession plain = Metric(s => { });
            CadCommandSession session = Metric(s => s.Dimtxtdirection = true);

            foreach (CadCommandSession s in new[] { plain, session })
            {
                Run(s, "DLI", P(0, 0), P(100, 0), P(50, -30));
                Run(s, "DLI", P(0, 0), P(0, 100), P(-30, 50));
                Run(s, "DOR", P(300, 40), P(300, 80));
            }

            List<DxfDimension> turned = session.Document.ModelSpace.Block.OfType<DxfDimension>().ToList();
            List<DxfDimension> kept = plain.Document.ModelSpace.Block.OfType<DxfDimension>().ToList();
            Assert.Equal(Math.PI, Words(turned[0]).Rotation, 9);
            Assert.Equal(1.5 * Math.PI, Words(turned[1]).Rotation, 9);
            Assert.Equal(1.5 * Math.PI, Words(turned[2]).Rotation, 9);

            for (int i = 0; i < 3; i++)
            {
                Near(Words(kept[i]).Location, Words(turned[i]).Location);
            }

            Run(session, "DIMEDIT", "R", "30", new[] { turned[0].Handle }, Enter);
            Assert.Equal(
                210.0,
                Words(Entity<DxfDimension>(session.Document, turned[0].Handle)).Rotation * 180 / Math.PI,
                9
            );
            AssertReopens(session.Document);
        }

        [Fact]
        public void Dimtxtdirection_UnderDimtmove1_PutsTheWordsOnTheOtherSideOfTheirPoint()
        {
            // AutoCAD 2021: "100.0000" 1.0094 wide moved to 30 under DIMTMOVE 1 stands about 29.4953 left to right, 30.5047
            // right to left; the leader and its landing are the same.
            CadCommandSession plain = Metric(s => s.Dimtmove = 1);
            CadCommandSession session = Metric(s =>
            {
                s.Dimtmove = 1;
                s.Dimtxtdirection = true;
            });

            foreach (CadCommandSession s in new[] { plain, session })
            {
                Run(s, "DLI", P(0, 0), P(100, 0), P(50, -20));
                Run(s, "DIMTEDIT", new[] { Made(s).Handle }, P(40, -10));
            }

            double left = Words(Made(plain)).Location.X;
            double right = Words(Made(session)).Location.X;
            Assert.Equal(40 - 2.625, left, 9);
            Assert.Equal(40 + 2.625, right, 9);
            Assert.Equal(Lines(Made(plain)).Count, Lines(Made(session)).Count);
        }

        [Fact]
        public void AJoggedOrdinateUnderDimfxlon_StartsItsLeaderDimfxlBeforeTheJog_AsAutoCad()
        {
            // AutoCAD 2021, arrowheads 0.18: the point (350, 900), the end (370, 940), DIMFXL 10: the first leg from 929.28 to the
            // jog at 939.28; DIMFXL 50 from DIMEXO off the point, 900.0625.
            CadCommandSession session = Metric(s =>
            {
                s.DimfxlenOn = true;
                s.Dimfxlen = 10;
            });
            Run(session, "DOR", P(150, 0), P(170, 40));

            List<DxfLine> leader = Lines(Made(session));
            Assert.Equal(3, leader.Count);
            double jog = 40 - 4 * Arrow;
            Near(new GeoPoint3(150, jog - 10, 0), leader[0].StartPoint);
            Near(new GeoPoint3(150, jog, 0), leader[0].EndPoint);

            session.Document.Database.DimStyleTable["Standard"].Dimfxlen = 50;
            StandardCurrent(session.Document.Database);
            Run(session, "DOR", P(250, 0), P(270, 40));
            Near(new GeoPoint3(250, 0.625, 0), Lines(Made(session))[0].StartPoint);
        }

        [Fact]
        public void TheTextPositionSetInThePaletteUnderDimtmove0_MovesTheDimensionLineThroughIt_AsAutoCad()
        {
            // AutoCAD 2021, DIMTAD 1: X set to 30 keeps Y, the words' middle above the line, and the line moves up to it, the
            // words above it again; Y set moves the line to it. Under DIMTMOVE 1 the line stays.
            CadCommandSession session = Metric(s => { });
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, 20));
            string handle = Made(session).Handle;

            Assert.True(CadPropertyEditor.Set(session.Document, new[] { handle }, "Text position X", "30").Succeeded);
            var dimension = Entity<DxfRotatedDimension>(session.Document, handle);
            Assert.Equal(20 + Lift, dimension.DimLinePoint.Y, 9);
            Near(new GeoPoint3(30, 20 + Lift, 0), dimension.TextPosition);
            Near(new GeoPoint3(30, 20 + 2 * Lift, 0), Words(dimension).Location);

            Assert.True(CadPropertyEditor.Set(session.Document, new[] { handle }, "Text position Y", "35").Succeeded);
            dimension = Entity<DxfRotatedDimension>(session.Document, handle);
            Assert.Equal(35, dimension.DimLinePoint.Y, 9);
            Near(new GeoPoint3(30, 35 + Lift, 0), Words(dimension).Location);

            session = Metric(s => s.Dimtmove = 1);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, 20));
            handle = Made(session).Handle;
            Assert.True(CadPropertyEditor.Set(session.Document, new[] { handle }, "Text position Y", "35").Succeeded);
            Assert.Equal(20, Entity<DxfRotatedDimension>(session.Document, handle).DimLinePoint.Y, 9);
            AssertReopens(session.Document);
        }

        [Fact]
        public void DimLinear_WritesTheMiddleOfItsWordsAndItsMeasurement_AsAutoCad()
        {
            // AutoCAD 2021 writes group 11 at the middle of the words and 42 the measurement, the dimension at its default place.
            CadCommandSession session = Metric(s => { });
            Run(session, "DLI", P(0, 0), P(100, 40), P(50, 70));

            DxfDimension dimension = Made(session);
            Assert.True(dimension.UsingDefaultTextPosition);
            Near(Words(dimension).Location, dimension.TextPosition);
            Near(new GeoPoint3(50, 70 + Lift, 0), dimension.TextPosition);
            Assert.Equal(
                100.0,
                double.Parse(Group(session.Document, dimension.Handle, 42), CultureInfo.InvariantCulture)
            );
            AssertReopens(session.Document);
        }

        [Fact]
        public void DimLinear_HasAutoCadsOptions_RotatedMeasuresAlongTheAngleGiven()
        {
            // AutoCAD 2021: 0,0 to 100,0 rotated 30 measures 86.6025, its line through the point given.
            CadCommandSession session = Metric(s => { });
            Run(session, "DLI", P(0, 0), P(100, 0));
            Assert.Equal(
                "Specify dimension line location or [Mtext/Text/Angle/Horizontal/Vertical/Rotated]:",
                session.PromptText
            );
            Run(session, "R");
            Assert.Equal("Specify angle of dimension line <0>:", session.PromptText);
            Run(session, "30", P(50, 40));

            var dimension = (DxfRotatedDimension)Made(session);
            Assert.Equal(30.0, dimension.Rotation * 180 / Math.PI, 9);
            Assert.Equal(100 * Math.Cos(Math.PI / 6), dimension.Measurement, 9);
            Assert.StartsWith("Dimension text = 87", session.History.Last(), StringComparison.Ordinal);

            // The line through the point: (50, 40) on it, along 30 degrees.
            GeoPoint3 on = dimension.DimLinePoint;
            Assert.Equal(0.0, (on.X - 50) * Math.Sin(Math.PI / 6) - (on.Y - 40) * Math.Cos(Math.PI / 6), 9);
            AssertReopens(session.Document);
        }

        [Fact]
        public void DimLinear_AngleTurnsTheWords_CentredOnTheLine_ItBrokenByTheirTurnedBox_AsAutoCad()
        {
            // AutoCAD 2021, DIMTAD 1, words turned 45: their middle on the line, which is broken where the turned box and the gap
            // cross it, min((w/2 + gap)/cos 45, (h/2 + gap)/sin 45) each side.
            CadCommandSession session = Metric(s => { });
            Run(session, "DLI", P(0, 0), P(100, 0), "A");
            Assert.Equal("Specify angle of dimension text:", session.PromptText);
            Run(session, "45", P(50, -30));

            DxfDimension dimension = Made(session);
            Assert.Equal(45.0, dimension.TextRotation * 180 / Math.PI, 9);
            Assert.Equal(Math.PI / 4, Words(dimension).Rotation, 9);
            Near(new GeoPoint3(50, -30, 0), Words(dimension).Location);
            double half = Math.Min((2.625 + Gap) / Math.Cos(Math.PI / 4), (Height / 2 + Gap) / Math.Sin(Math.PI / 4));
            List<DxfLine> line = OnLevel(dimension, -30);
            Assert.Equal(2, line.Count);
            Assert.Equal(50 - half, Right(line[0]), 9);
            Assert.Equal(50 + half, Left(line[1]), 9);

            // Drawn again from its variables, the same.
            Assert.True(
                CadPropertyEditor.Set(session.Document, new[] { dimension.Handle }, "Dim line color", "Green").Succeeded
            );
            line = OnLevel(Entity<DxfDimension>(session.Document, dimension.Handle), -30);
            Assert.Equal(50 - half, Right(line[0]), 9);
            AssertReopens(session.Document);
        }

        [Fact]
        public void DimLinearMtext_AsksForTheWordsAsText_AndDimAlignedHasMtextTextAndAngle()
        {
            CadCommandSession session = Metric(s => { });
            Run(session, "DLI", P(0, 0), P(100, 0), "M");
            Assert.Equal("Enter dimension text <<>>:", session.PromptText);
            Run(session, "AB <>", P(50, -30));
            Assert.Equal("AB <>", Made(session).DimensionText);

            Run(session, "DAL", P(0, 0), P(100, 40));
            Assert.Equal("Specify dimension line location or [Mtext/Text/Angle]:", session.PromptText);
            Run(session, "A", "15", P(40, 60));
            Assert.Equal(15.0, Made(session).TextRotation * 180 / Math.PI, 9);
        }
    }
}

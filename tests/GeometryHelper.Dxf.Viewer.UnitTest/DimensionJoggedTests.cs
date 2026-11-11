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
    /// DIMJOGGED, its picture and its grips, against AutoCAD 2021's: a circle of 50 about the origin, its centre drawn at
    /// (0, -100), with ISO-25's sizes (arrowheads 2.5, DIMGAP 0.625) and DIMJOGANG 45 degrees.
    /// </summary>
    public class DimensionJoggedTests
    {
        private static CadCommandSession Metric(Action<DxfDimStyleTableRecord> set)
        {
            CadDocument document = Blank();
            DxfDimStyleTableRecord style = document.Database.DimStyleTable["Standard"];
            style.Dimscale = 1.0;
            style.Dimasz = 2.5;
            style.Dimtxt = 2.5;
            style.Dimgap = 0.625;
            style.Dimdec = 0;
            style.Dimtad = 1;
            style.Dimtih = false;
            style.Dimtoh = false;
            style.Dimjogang = Math.PI / 4.0;
            set(style);
            return Session(document);
        }

        private static string Circle(CadCommandSession session)
        {
            Run(session, "C", P(0, 0), "50");
            return session.Document.ModelSpace.Block.Cast<DxfEntity>().Last().Handle;
        }

        private static DxfRadialDimensionLarge Made(CadCommandSession session) =>
            session.Document.ModelSpace.Block.OfType<DxfRadialDimensionLarge>().Last();

        private static List<DxfLine> Lines(DxfDimension dimension) =>
            dimension.DimBlock.OfType<DxfLine>().Where(l => !l.IsErased).ToList();

        private static GeoPoint2 P2(GeoPoint3 p) => new GeoPoint2(p.X, p.Y);

        private static bool Same(GeoPoint2 a, GeoPoint2 b) => (a - b).Length <= 1E-3;

        private static void HasLine(DxfDimension dimension, GeoPoint2 a, GeoPoint2 b) =>
            Assert.Contains(Lines(dimension), l =>
                (Same(P2(l.StartPoint), a) && Same(P2(l.EndPoint), b))
                || (Same(P2(l.StartPoint), b) && Same(P2(l.EndPoint), a))
            );

        [Fact]
        public void DimJogged_AsksAsAutoCadAsks_AndSaysWhenTheObjectIsNoCircle()
        {
            CadCommandSession session = Metric(s => { });
            Run(session, "L", P(200, 0), P(300, 0), Enter);
            string line = session.Document.ModelSpace.Block.Cast<DxfEntity>().Last().Handle;
            string circle = Circle(session);

            Run(session, "DIMJOGGED");
            Assert.Equal("Select arc or circle:", session.PromptText);
            Run(session, (object)new[] { line });
            Assert.Equal("Object selected is not a circle or arc.", session.History.Last());
            Assert.Equal("Select arc or circle:", session.PromptText);
            Run(session, (object)new[] { circle });
            Assert.Equal("Specify center location override:", session.PromptText);
            Run(session, P(0, -100));
            Assert.Contains(session.History, h => h.StartsWith("Dimension text = 50", StringComparison.Ordinal));
            Assert.Equal("Specify dimension line location or [Mtext/Text/Angle]:", session.PromptText);
            Run(session, P(30, 0));
            Assert.Equal("Specify jog location:", session.PromptText);
            Run(session, P(15, -50));

            Assert.False(session.IsRunning);
            Assert.Equal("DIMJOGGED", session.Document.NextUndo.Name);
        }

        [Fact]
        public void AJoggedRadius_RunsFromTheCentreDrawnAlongItsParallel_JogsAcrossAndRunsOnToTheRim()
        {
            // AutoCAD 2021: (0, -100) to (65, -100), on to (-35, 0) and on to the arrowhead's back at (47.5, 0); "R50" above
            // the line at (30, 1.901); the jog's middle at (15, -50).
            CadCommandSession session = Metric(s => { });
            Run(session, "DIMJOGGED", new[] { Circle(session) }, P(0, -100), P(30, 0), P(15, -50));

            DxfRadialDimensionLarge jogged = Made(session);
            Assert.Equal(50.0, jogged.Measurement, 9);
            Near(P(50, 0), P2(jogged.ChordPoint), 1E-9);
            Near(P(0, -100), P2(jogged.OverrideCenter), 1E-9);
            Near(P(15, -50), P2(jogged.JogPoint), 1E-9);
            Near(P(30, 0), P2(jogged.TextPosition), 1E-9);
            Assert.Equal(0.0, jogged.JogAngle);
            HasLine(jogged, P(0, -100), P(65, -100));
            HasLine(jogged, P(65, -100), P(-35, 0));
            HasLine(jogged, P(-35, 0), P(47.5, 0));
            Assert.Equal(3, Lines(jogged).Count);
            DxfMText words = jogged.DimBlock.OfType<DxfMText>().Single();
            Assert.Equal("R50", words.Contents);
            Assert.Equal(0.0, words.Location.X - 30.0, 9);
            Assert.True(words.Location.Y > 0.0);
        }

        [Fact]
        public void WordsPutOnTheOtherSideOfTheRim_GoThreeQuartersOfTheWayFromTheCentreDrawnToTheRim()
        {
            // Words put 80 out at 14.04 degrees: the foot of the centre drawn lies inside the rim, the words outside, so they
            // go to (36.38, -15.905) and the radius turns to -23.61 degrees, as AutoCAD 2021 draws them.
            CadCommandSession session = Metric(s => { });
            Run(session, "DIMJOGGED", new[] { Circle(session) }, P(0, -100), P(80, 20), P(40, -50));

            DxfRadialDimensionLarge jogged = Made(session);
            Near(new GeoPoint2(36.380, -15.905), P2(jogged.TextPosition), 1E-3);
            Assert.Equal(-23.614, Math.Atan2(jogged.ChordPoint.Y, jogged.ChordPoint.X) * 180.0 / Math.PI, 3);

            // Put 30 out at -30 degrees the foot is on the rim itself: they go to (32.476, -43.75), the radius at -53.41.
            session = Metric(s => { });
            Run(session, "DIMJOGGED", new[] { Circle(session) }, P(0, -100), P(25.980762, -15), P(12.990381, -57.5));
            jogged = Made(session);
            Near(new GeoPoint2(32.476, -43.75), P2(jogged.TextPosition), 1E-3);
            Near(new GeoPoint2(8.016, -60.800), P2(jogged.JogPoint), 1E-3);
        }

        [Fact]
        public void AJogGivenOffItsMiddleLine_IsPutOnIt_AsFarFromWhereALineFromTheCentreDrawnMeetsIt()
        {
            // The jog given at (6, -80) stands at (13.53, -50): 63.53 from (-50, -50), as (6, -80) is; AutoCAD 2021 the same.
            CadCommandSession session = Metric(s => { });
            Run(session, "DIMJOGGED", new[] { Circle(session) }, P(0, -100), P(30, 0), P(6, -80));
            Near(new GeoPoint2(13.530, -50), P2(Made(session).JogPoint), 1E-3);

            // Under DIMJOGANG 30 the jog is longer along the line, 86.6 for the 100 between the lines.
            session = Metric(s => s.Dimjogang = Math.PI / 6.0);
            Run(session, "DIMJOGGED", new[] { Circle(session) }, P(0, -100), P(30, 0), P(15, -50));
            HasLine(Made(session), P(15 + 86.6025, -100), P(15 - 86.6025, 0));
        }

        [Fact]
        public void TheGripsOfAJoggedRadius_AreItsChordPointCentreDrawnJogAndWords_AndTheJogStaysOnItsMiddleLine()
        {
            CadCommandSession session = Metric(s => { });
            Run(session, "DIMJOGGED", new[] { Circle(session) }, P(0, -100), P(30, 0), P(15, -50));
            DxfRadialDimensionLarge jogged = Made(session);

            IReadOnlyList<CadGrip> grips = CadGrips.Of(jogged);
            Assert.Equal(
                new[] { CadGripKind.DefinitionPoint, CadGripKind.DefinitionPoint, CadGripKind.Jog, CadGripKind.Text },
                grips.Select(g => g.Kind)
            );
            Near(P(15, -50), grips[2].Point, 1E-9);

            // The jog dragged 20 down is put back on the middle line, as the command puts it.
            CadGrips.Drag(session.Document, jogged.Handle, 2, P(15, -70));
            jogged = Made(session);
            Assert.Equal(-50.0, jogged.JogPoint.Y, 9);
            Assert.True(jogged.JogPoint.X > 15.0);
        }

        [Fact]
        public void AJoggedRadiusOfAFile_IsDrawnAgainFromItsPoints_WhenItsStyleChanges()
        {
            CadCommandSession session = Metric(s => { });
            Run(session, "DIMJOGGED", new[] { Circle(session) }, P(0, -100), P(30, 0), P(15, -50));
            CadDocument document = CadDocument.From(DxfDatabase.Parse(session.Document.Database.ToText()), "read.dxf");
            DxfRadialDimensionLarge again = document.ModelSpace.Block.OfType<DxfRadialDimensionLarge>().Single();
            Near(P(15, -50), P2(again.JogPoint), 1E-9);

            var values = new Dictionary<string, object> { ["Dimjogang"] = Math.PI / 3.0 };
            Assert.True(CadDimStyleEditor.Apply(document, "Standard", values).Succeeded);

            // At 60 degrees the jog runs 57.735 along the line for the 100 across.
            again = document.ModelSpace.Block.OfType<DxfRadialDimensionLarge>().Single();
            HasLine(again, P(15 + 28.8675, -100), P(15 - 28.8675, 0));
            Assert.Equal(3, Lines(again).Count);
        }
    }
}

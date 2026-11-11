using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// MLEADERSCALE and CMLEADERSTYLE through SETVAR, as AutoCAD 2021 keeps them (probed through accoreconsole): making a
    /// style current gives the variable the style's scale, setting the variable leaves the style as it is and scales the
    /// multileaders drawn next, nought gives 1 in model space and the viewport's scale through a viewport.
    /// </summary>
    public class MLeaderScaleTests
    {
        /// <summary><see cref="BlankWithMLeaderStyle"/> with a second style, "Five", a copy of Standard of scale 5.</summary>
        private static CadDocument WithStyleOfFive()
        {
            CadDocument document = BlankWithMLeaderStyle();
            Assert.True(CadMLeaderStyleEditor.Add(document, "Five", "Standard").Succeeded);
            var five = CadMLeaderStyleSettings.Of(CadMLeaderStyleEditor.Find(document.Database, "Five"));
            five.Scale = 5;
            Assert.True(CadMLeaderStyleEditor.Apply(document, "Five", five).Succeeded);
            return document;
        }

        [Fact]
        public void MakingAStyleCurrent_GivesMLeaderscaleItsScale_AgainWhenItIsCurrentAlready()
        {
            CadDocument document = WithStyleOfFive();
            CadCommandSession session = Session(document);

            Run(session, "SETVAR", "CMLEADERSTYLE", "five");
            Assert.Equal("Five", CadMLeaderStyleEditor.Current(document.Database));
            Assert.Equal(5.0, document.Database.MLeaderscale);

            Run(session, "MLEADERSCALE");
            Assert.Equal("Enter new value for MLEADERSCALE <5.0000>:", session.Prompt.Text);
            Run(session, "2");
            Assert.Equal(2.0, document.Database.MLeaderscale);
            Assert.Equal(5.0, CadMLeaderStyleEditor.Find(document.Database, "Five").Scale);

            Run(session, "CMLEADERSTYLE", "Five");
            Assert.Equal(5.0, document.Database.MLeaderscale);
            Run(session, "CMLEADERSTYLE", "Standard");
            Assert.Equal(1.0, document.Database.MLeaderscale);

            // One undo step each: back to Five and its 5.
            Assert.True(document.Undo());
            Assert.Equal("Five", CadMLeaderStyleEditor.Current(document.Database));
            Assert.Equal(5.0, document.Database.MLeaderscale);
        }

        [Fact]
        public void ANameOfNoStyle_AndAScaleBelowNought_AreRefusedAsAutoCadRefusesThem_AndNotAskedAgain()
        {
            CadDocument document = WithStyleOfFive();
            CadCommandSession session = Session(document);

            Run(session, "CMLEADERSTYLE", "Nope");
            Assert.Equal("Cannot set CMLEADERSTYLE to that value.", session.History.Last());
            Assert.False(session.IsRunning);

            Run(session, "MLEADERSCALE", "-1");
            Assert.Equal("Cannot set MLEADERSCALE to that value.", session.History.Last());
            Assert.False(session.IsRunning);
            Assert.Equal(1.0, document.Database.MLeaderscale);
        }

        [Fact]
        public void MLeaderscaleSet_IsTheScaleOfTheNextMultileader_TheStylesOwnUntouched()
        {
            CadDocument document = WithStyleOfFive();
            CadCommandSession session = Session(document);
            Run(session, "CMLEADERSTYLE", "Five");

            Run(session, "MLD", P(0, 0), P(30, 20), "FIVE");
            Run(session, "MLEADERSCALE", "2");
            Run(session, "MLD", P(0, 40), P(30, 60), "TWO");
            Run(session, "MLEADERSCALE", "0");
            Run(session, "MLD", P(0, 80), P(30, 100), "ONE");

            DxfMLeader[] made = All<DxfMLeader>(document.ModelSpace).ToArray();
            Assert.Equal(new[] { 5.0, 2.0, 1.0 }, made.Select(m => m.Scale));
            Assert.Equal(new[] { 20.0, 8.0, 4.0 }, made.Select(m => m.MText.TextHeight));
            Assert.All(made, m => Assert.Equal("Five", m.MLeaderStyle));
            Assert.Equal(5.0, CadMLeaderStyleEditor.Find(document.Database, "Five").Scale);
            AssertReopens(document);
        }

        [Fact]
        public void MLeaderscaleNought_ThroughAViewportOf1To10_GivesScale10()
        {
            CadDocument document = BlankWithMLeaderStyle();
            CadCommandSession session = Session(document);
            Run(session, "REC", P(0, 0), P(2000, 1000));
            document.CurrentSpace = document.Spaces[1];
            Run(session, "MV", P(10, 10), P(210, 110));
            Run(session, "MS");
            Assert.NotNull(document.ActiveViewport);

            Run(session, "MLEADERSCALE", "0");
            Run(session, "MLD", P(100, 100), P(600, 300), "VP");

            DxfMLeader made = Assert.Single(All<DxfMLeader>(document.ModelSpace));
            Assert.Equal(10.0, made.Scale, 9);
            Assert.Equal(40.0, made.MText.TextHeight, 9);
        }
    }
}

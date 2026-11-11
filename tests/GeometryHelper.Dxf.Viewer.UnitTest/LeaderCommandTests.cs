using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// LEADER and QLEADER on the command line, as AutoCAD 2021 draws them (probed through accoreconsole): the LEADER through
    /// the points picked, a hook DIMASZ long where the last segment is steeper than 15 degrees, the MTEXT DIMGAP from its
    /// end, the dimension variables other than the style's written as the leader's overrides.
    /// </summary>
    public class LeaderCommandTests
    {
        /// <summary>The presenter's metric drawing, its dimension style ISO-25 with DIMTAD nought, in the style and the header alike.</summary>
        private static DxfDatabase MetricDatabase()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.DimStyleTable[database.Dimstyle].Dimtad = 0;
            database.Dimtad = 0;
            return database;
        }

        private static CadDocument Metric() => CadDocument.From(MetricDatabase(), "metric.dxf");

        private static string Overrides(DxfLeader leader) =>
            string.Join(
                " ",
                (leader.GetXDataForApplication("ACAD") ?? Array.Empty<DxfTypedValue>())
                    .Skip(1)
                    .Select(v => v.TypeCode + ":" + v.Value)
            );

        [Fact]
        public void Leader_ThroughAHorizontalLastSegment_HasNoHook_AndItsWordsStandDimgapPastItsEnd()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            DxfDatabase db = document.Database;

            Run(session, "LEADER", P(0, 0));
            Assert.Equal("Specify next point:", session.Prompt.Text);
            Run(session, P(10, 10));
            Assert.Equal("Specify next point or [Annotation/Format/Undo] <Annotation>:", session.Prompt.Text);
            Run(session, P(20, 10), Enter);
            Assert.Equal("Enter first line of annotation text or <options>:", session.Prompt.Text);
            Run(session, "Hello");
            Assert.Equal("Enter next line of annotation text:", session.Prompt.Text);
            Run(session, Enter);

            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Assert.Equal(3, leader.NumVertices);
            Assert.Equal(new GeoPoint3(20, 10, 0), leader.LastVertex);
            Assert.True(leader.HasArrowHead);
            Assert.False(leader.IsSplined);
            Assert.False(leader.HasHookLine);
            Assert.Equal(DxfAnnotationType.MText, leader.AnnoType);
            Assert.Equal(string.Empty, Overrides(leader));

            DxfMText text = Assert.IsType<DxfMText>(leader.Annotation);
            Assert.Equal("\\A1;Hello", text.Contents);
            Assert.Equal(DxfAttachmentPoint.MiddleLeft, text.Attachment);
            Assert.Equal(db.Dimtxt, text.TextHeight);
            Near(new GeoPoint3(20 + db.Dimgap, 10, 0), text.Location);
            Assert.True(leader.AnnoHeight > 0.0 && leader.AnnoWidth > 0.0);
            AssertReopens(document);

            // One undo step takes the leader and its words back.
            Assert.True(document.Undo());
            Assert.Empty(All<DxfLeader>(document.ModelSpace));
            Assert.Empty(All<DxfMText>(document.ModelSpace));
        }

        [Fact]
        public void ALastSegmentSteeperThan15Degrees_GetsAHookDimaszLong_TheWordsOnTheSideItRunsTo()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            DxfDatabase db = document.Database;
            double rise149 = 10 * Math.Tan(14.9 * Math.PI / 180);
            double rise151 = 10 * Math.Tan(15.1 * Math.PI / 180);

            Run(session, "LEADER", P(0, 0), P(10, rise149), Enter, "a", Enter);
            Run(session, "LEADER", P(0, 20), P(10, 20 + rise151), Enter, "b", Enter);
            Run(session, "LEADER", P(0, 40), P(-10, 30), Enter, "Two", "lines", Enter);
            Run(session, "LEADER", P(0, 60), P(0, 70), Enter, "Up", Enter);

            DxfLeader[] leaders = All<DxfLeader>(document.ModelSpace).ToArray();
            Assert.Equal(2, leaders[0].NumVertices);
            Assert.False(leaders[0].HasHookLine);
            Assert.Equal(3, leaders[1].NumVertices);
            Assert.True(leaders[1].HasHookLine);
            Near(new GeoPoint3(10 - db.Dimasz, 20 + rise151, 0), leaders[1].VertexAt(1));

            // Down to the left: the hook runs left, the words right-attached left of the end.
            Near(new GeoPoint3(-10 + db.Dimasz, 30, 0), leaders[2].VertexAt(1));
            DxfMText left = (DxfMText)leaders[2].Annotation;
            Assert.Equal("\\A1;Two\\Plines", left.Contents);
            Assert.Equal(DxfAttachmentPoint.MiddleRight, left.Attachment);
            Near(new GeoPoint3(-10 - db.Dimgap, 30, 0), left.Location);

            // Straight up counts as running right, as AutoCAD 2021 hooks (0, 0) to (0, 10) from the left.
            Near(new GeoPoint3(-db.Dimasz, 70, 0), leaders[3].VertexAt(1));
            Assert.Equal(DxfAttachmentPoint.MiddleLeft, ((DxfMText)leaders[3].Annotation).Attachment);
            AssertReopens(document);
        }

        [Fact]
        public void Dimscale2_IsWrittenAsTheLeadersOverride_AndScalesTheHookTheGapAndTheWords()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            DxfDatabase db = document.Database;
            Run(session, "DIMSCALE", "2");

            Run(session, "LEADER", P(0, 20), P(10, 30), Enter, "Scaled", Enter);

            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Assert.Equal("1000:DSTYLE 1002:{ 1070:40 1040:2 1002:}", Overrides(leader));
            Assert.Equal(2.0, leader.Dimscale);
            Near(new GeoPoint3(10 - 2 * db.Dimasz, 30, 0), leader.VertexAt(1));
            DxfMText text = (DxfMText)leader.Annotation;
            Assert.Equal(2 * db.Dimtxt, text.TextHeight);
            Near(new GeoPoint3(10 + 2 * db.Dimgap, 30, 0), text.Location);
            AssertReopens(document);
        }

        [Fact]
        public void DimtadAndDimgapOtherThanTheStyles_AreOverridesInTheOrderOfTheirGroups_TheWordsAboveTheHook()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            DxfDatabase db = document.Database;
            double gap = db.Dimgap / 2;
            db.Dimgap = gap;
            db.Dimtad = 1;

            Run(session, "LEADER", P(0, 0), P(-10, 10), Enter, "Tad", Enter);

            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Assert.StartsWith("1000:DSTYLE 1002:{ 1070:77 1070:1 1070:147 1040:", Overrides(leader));
            DxfMText text = (DxfMText)leader.Annotation;
            Assert.Equal(DxfAttachmentPoint.BottomRight, text.Attachment);
            Near(new GeoPoint3(-10 - gap, 10 + gap, 0), text.Location);
        }

        [Fact]
        public void FormatAndUndo_MakeASplineOfNoArrow_AndTakeTheLastPointBack()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);

            Run(session, "LEADER", P(0, 0), P(10, 10), P(30, 30), "U");
            Run(session, "F");
            Assert.Equal("Enter leader format option [Spline/STraight/Arrow/None] <Exit>:", session.Prompt.Text);
            Run(session, "S", "F", "N", P(20, 5), Enter, "Spl", Enter);

            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Assert.True(leader.IsSplined);
            Assert.False(leader.HasArrowHead);
            Assert.False(leader.HasHookLine);
            Assert.Equal(3, leader.NumVertices);
            Assert.Equal(new GeoPoint3(20, 5, 0), leader.LastVertex);
            AssertReopens(document);
        }

        [Fact]
        public void TheAnnotationOptions_NoneGivesNoWords_ToleranceIsRefused_CopyTakesAText()
        {
            DxfDatabase database = MetricDatabase();
            database.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    Position = new GeoPoint3(100, 100, 0),
                    Height = 3,
                    TextString = "Copied",
                }
            );
            CadDocument document = CadDocument.From(database, "copy.dxf");
            CadCommandSession session = Session(document);
            string source = Assert.Single(All<DxfDBText>(document.ModelSpace)).Handle;

            Run(session, "LEADER", P(0, 0), P(10, 10), Enter, Enter);
            Assert.Equal("Enter an annotation option [Tolerance/Copy/Block/None/Mtext] <Mtext>:", session.Prompt.Text);
            Run(session, "N");
            DxfLeader none = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Assert.Equal(DxfAnnotationType.NoAnnotation, none.AnnoType);
            Assert.Equal(2, none.NumVertices);

            Run(session, "LEADER", P(0, 20), P(10, 30), Enter, Enter, "T");
            Assert.Equal("Enter an annotation option [Tolerance/Copy/Block/None/Mtext] <Mtext>:", session.Prompt.Text);
            Run(session, "C", new[] { source });

            DxfLeader copied = All<DxfLeader>(document.ModelSpace).Last();
            DxfMText text = Assert.IsType<DxfMText>(copied.Annotation);
            Assert.Equal("Copied", text.Contents);
            Assert.Equal(3.0, text.TextHeight);
            Assert.True(copied.HasHookLine);
            Assert.NotNull(document.FindEntity(source));
            AssertReopens(document);
        }

        [Fact]
        public void TheBlockOption_PlacesAnInsert_ThatTheLeaderPointsTo_WithNoHook()
        {
            DxfDatabase database = MetricDatabase();
            var block = new DxfBlockTableRecord { Name = "B1", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(block);
            block.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 1));
            CadDocument document = CadDocument.From(database, "block.dxf");
            CadCommandSession session = Session(document);

            Run(session, "LEADER", P(0, 60), P(10, 70), Enter, Enter, "B");
            Assert.StartsWith("Enter block name or [?]", session.Prompt.Text);
            Run(session, "B1", P(12, 70), Enter, Enter, Enter);

            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            DxfBlockReference insert = Assert.IsType<DxfBlockReference>(leader.Annotation);
            Assert.Equal(new GeoPoint3(12, 70, 0), insert.Position);
            Assert.Equal(DxfAnnotationType.BlockRef, leader.AnnoType);
            Assert.False(leader.HasHookLine);
            Assert.Equal(2, leader.NumVertices);
        }

        [Fact]
        public void QLeader_TakesThreePointsAtMost_AndSetsTheMiddleOfTheTopLineAtItsEnd_OverridingDimgapAndDimtad()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            DxfDatabase db = document.Database;

            Run(session, "LE");
            Assert.Equal("Specify first leader point, or [Settings] <Settings>:", session.Prompt.Text);
            Run(session, P(0, 20), P(10, 30), P(20, 30));
            Assert.Equal("Specify text width <0.0000>:", session.Prompt.Text);
            Run(session, "5");
            Assert.Equal("Enter first line of annotation text <Mtext>:", session.Prompt.Text);
            Run(session, "Q1", "Q2", Enter);

            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Assert.Equal(3, leader.NumVertices);
            Assert.StartsWith("1000:DSTYLE 1002:{ 1070:147 1040:", Overrides(leader));
            Assert.EndsWith("1070:77 1070:0 1002:}", Overrides(leader));
            DxfMText text = (DxfMText)leader.Annotation;
            Assert.Equal("\\A1;Q1\\PQ2", text.Contents);
            Assert.Equal(DxfAttachmentPoint.TopLeft, text.Attachment);
            Assert.Equal(5.0, text.Width);
            Near(new GeoPoint3(20 + db.Dimgap, 30 + db.Dimtxt / 2, 0), text.Location);

            // To the left, the middle of the bottom line; two points and Enter, then Enter for no width.
            Run(session, "QLEADER", P(0, 80), P(-10, 90), Enter, Enter, "Left", Enter);
            DxfLeader left = All<DxfLeader>(document.ModelSpace).Last();
            Assert.True(left.HasHookLine);
            DxfMText words = (DxfMText)left.Annotation;
            Assert.Equal(DxfAttachmentPoint.BottomRight, words.Attachment);
            Near(new GeoPoint3(-10 - db.Dimgap, 90 - db.Dimtxt / 2, 0), words.Location);
            AssertReopens(document);
        }

        [Fact]
        public void QLeaderSettings_SaysTheDialogIsNotThere_AndAsksThePointAgain()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);

            Run(session, "QLEADER", Enter);

            Assert.Equal(
                "The Leader Settings dialog is not in the presenter yet: QLEADER draws with its default settings.",
                session.History.Last()
            );
            Assert.Equal("Specify first leader point, or [Settings] <Settings>:", session.Prompt.Text);
        }
    }
}

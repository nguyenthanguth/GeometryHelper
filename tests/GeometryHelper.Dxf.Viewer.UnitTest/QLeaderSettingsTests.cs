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
    /// QLEADER by the Leader Settings a drawing keeps, as AutoCAD 2021 draws by them (probed through accoreconsole with the
    /// "AcadDim" XRECORD written by its API): each setting followed, the record read as AutoCAD reads it, Settings shown
    /// through the window's request and written as one undo step.
    /// </summary>
    public class QLeaderSettingsTests
    {
        private static DxfDatabase MetricDatabase()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.DimStyleTable[database.Dimstyle].Dimtad = 0;
            database.Dimtad = 0;
            return database;
        }

        /// <summary>A metric drawing with the settings of a record: the defaults AutoCAD's dialog writes, the groups given changed.</summary>
        private static CadDocument WithSettings(params int[] codeValue)
        {
            DxfDatabase database = MetricDatabase();
            WriteSettings(database, codeValue);
            return CadDocument.From(database, "metric.dxf");
        }

        private static void WriteSettings(DxfDatabase database, params int[] codeValue)
        {
            var values = new Dictionary<int, int>
            {
                [60] = 0,
                [61] = 0,
                [62] = 1,
                [63] = 3,
                [64] = 0,
                [65] = 0,
                [66] = 0,
                [67] = 3,
                [68] = 1,
                [69] = 0,
                [70] = 0,
                [71] = 0,
                [72] = 0,
                [170] = 0,
            };

            for (int i = 0; i < codeValue.Length; i += 2)
            {
                values[codeValue[i]] = codeValue[i + 1];
            }

            var data = new List<DxfTypedValue>
            {
                new DxfTypedValue(90, "990106"),
                new DxfTypedValue(3, string.Empty),
                new DxfTypedValue(40, "0.0"),
            };
            data.AddRange(
                values
                    .OrderBy(p => p.Key)
                    .Select(p => new DxfTypedValue(p.Key, p.Value.ToString(CultureInfo.InvariantCulture)))
            );
            database.NamedObjectsDictionary.SetAt("AcadDim", new DxfXrecord { Data = data });
        }

        private static string Overrides(DxfLeader leader) =>
            string.Join(
                " ",
                (leader.GetXDataForApplication("ACAD") ?? Array.Empty<DxfTypedValue>())
                    .Skip(1)
                    .Select(v => v.TypeCode + ":" + v.Value)
            );

        private static DxfMText Words(CadDocument document) =>
            (DxfMText)All<DxfLeader>(document.ModelSpace).Last().Annotation;

        private static string Group(DxfXrecord record, int code) =>
            record.Data.Where(v => v.TypeCode == code).Select(v => v.Value).FirstOrDefault();

        [Fact]
        public void Group62IsTheRightSide_And63TheLeft_EachRowHangingTheWordsAsAutoCadDoes()
        {
            // Top of top line on the right, bottom of bottom line on the left: both at the leader's height.
            CadDocument document = WithSettings(62, 0, 63, 4);
            CadCommandSession session = Session(document);
            DxfDatabase db = document.Database;

            Run(session, "QLEADER", P(0, 0), P(50, 50), P(100, 50), "0", "ABC", Enter);
            DxfMText right = Words(document);
            Assert.Equal(DxfAttachmentPoint.TopLeft, right.Attachment);
            Near(new GeoPoint3(100 + db.Dimgap, 50, 0), right.Location);

            Run(session, "QLEADER", P(0, 100), P(-50, 150), P(-100, 150), "0", "ABC", Enter);
            DxfMText left = Words(document);
            Assert.Equal(DxfAttachmentPoint.BottomRight, left.Attachment);
            Near(new GeoPoint3(-100 - db.Dimgap, 150, 0), left.Location);

            // Bottom of bottom line on the right, top of top line on the left; the middle of the text by its middle.
            document = WithSettings(62, 4, 63, 0);
            session = Session(document);
            Run(session, "QLEADER", P(0, 0), P(50, 50), P(100, 50), "0", "ABC", Enter);
            Assert.Equal(DxfAttachmentPoint.BottomLeft, Words(document).Attachment);
            Run(session, "QLEADER", P(0, 100), P(-50, 150), P(-100, 150), "0", "ABC", Enter);
            Assert.Equal(DxfAttachmentPoint.TopRight, Words(document).Attachment);

            document = WithSettings(62, 3, 63, 1);
            session = Session(document);
            Run(session, "QLEADER", P(0, 0), P(50, 50), P(100, 50), "0", "ABC", Enter);
            Assert.Equal(DxfAttachmentPoint.BottomLeft, Words(document).Attachment);
            Near(new GeoPoint3(100 + db.Dimgap, 50 - db.Dimtxt / 2, 0), Words(document).Location);
            Run(session, "QLEADER", P(0, 100), P(-50, 150), P(-100, 150), "0", "ABC", Enter);
            Assert.Equal(DxfAttachmentPoint.TopRight, Words(document).Attachment);
            Near(new GeoPoint3(-100 - db.Dimgap, 150 + db.Dimtxt / 2, 0), Words(document).Location);

            document = WithSettings(62, 2);
            session = Session(document);
            Run(session, "QLEADER", P(0, 0), P(50, 50), P(100, 50), "0", "ABC", Enter);
            Assert.Equal(DxfAttachmentPoint.MiddleLeft, Words(document).Attachment);
            Near(new GeoPoint3(100 + db.Dimgap, 50, 0), Words(document).Location);
        }

        [Fact]
        public void NoAnnotation_EndsTheLeaderAtItsLastPoint_AndNoLimitAsksPointsUntilEnter()
        {
            CadDocument document = WithSettings(60, 4, 66, 1);
            CadCommandSession session = Session(document);

            Run(session, "QLEADER", P(0, 0), P(50, 50), P(100, 50), P(150, 50));
            Assert.Equal("Specify next point:", session.Prompt.Text);
            Run(session, Enter);

            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Assert.Equal(4, leader.NumVertices);
            Assert.Equal(DxfAnnotationType.NoAnnotation, leader.AnnoType);
            Assert.Empty(All<DxfMText>(document.ModelSpace));
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void MaximumTwoPoints_AsksTheWidthAfterTheSecond()
        {
            CadDocument document = WithSettings(67, 2);
            CadCommandSession session = Session(document);

            Run(session, "QLEADER", P(0, 0), P(50, 0));

            Assert.Equal("Specify text width <0.0000>:", session.Prompt.Text);
        }

        [Fact]
        public void PromptForWidthOff_OrAlwaysLeftJustify_AsksNoWidth_AndLeftJustifiedWordsEndDimgapShortOfTheLeader()
        {
            CadDocument document = WithSettings(68, 0);
            CadCommandSession session = Session(document);
            Run(session, "QLEADER", P(0, 0), P(50, 50), P(100, 50));
            Assert.Equal("Enter first line of annotation text <Mtext>:", session.Prompt.Text);
            Run(session, Esc);

            // AutoCAD 2021 asks no width with Always left justify, Prompt for width on.
            document = WithSettings(69, 1);
            session = Session(document);
            DxfDatabase db = document.Database;
            Run(session, "QLEADER", P(0, 0), P(-50, 50), P(-100, 50));
            Assert.Equal("Enter first line of annotation text <Mtext>:", session.Prompt.Text);
            Run(session, "ABC", "DE", Enter);

            DxfMText words = Words(document);
            Assert.Equal(DxfAttachmentPoint.BottomLeft, words.Attachment);
            double across = All<DxfLeader>(document.ModelSpace).Last().AnnoWidth;
            Assert.True(across > 0.0);
            Near(new GeoPoint3(-100 - db.Dimgap - across, 50 - db.Dimtxt / 2, 0), words.Location, 1E-6);
        }

        [Fact]
        public void FrameText_WritesDimgapBelowNought_AndUnderline_DimtadOneWithTheWordsDimgapAboveTheEnd()
        {
            CadDocument document = WithSettings(72, 1);
            CadCommandSession session = Session(document);
            DxfDatabase db = document.Database;
            string gap = db.Dimgap.ToString("R", CultureInfo.InvariantCulture);

            Run(session, "QLEADER", P(0, 0), P(50, 50), P(100, 50), "0", "ABC", Enter);
            DxfLeader framed = All<DxfLeader>(document.ModelSpace).Last();
            Assert.StartsWith("1000:DSTYLE 1002:{ 1070:147 1040:-" + gap, Overrides(framed));

            document = WithSettings(64, 1);
            session = Session(document);
            Run(session, "QLEADER", P(0, 0), P(-50, 50), P(-100, 50), "0", "ABC", "DE", Enter);
            DxfLeader underlined = All<DxfLeader>(document.ModelSpace).Last();
            Assert.Contains("1070:77 1070:1", Overrides(underlined));
            DxfMText words = Words(document);
            Assert.Equal(DxfAttachmentPoint.BottomRight, words.Attachment);
            Near(new GeoPoint3(-100 - db.Dimgap, 50 + db.Dimgap, 0), words.Location);
        }

        [Fact]
        public void Spline_MakesASplinedLeader_WithNoHook()
        {
            CadDocument document = WithSettings(65, 1, 67, 2);
            CadCommandSession session = Session(document);

            Run(session, "QLEADER", P(0, 0), P(50, 50), "0", "ABC", Enter);

            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Assert.True(leader.IsSplined);
            Assert.False(leader.HasHookLine);
            Assert.Equal(2, leader.NumVertices);
        }

        [Fact]
        public void AngleConstraints_BringEachPointSquareOntoTheNearestRay_AsAutoCadPutsThem()
        {
            // AutoCAD 2021: first level, second 45 degrees: (100, 30) to (100, 0), (150, 100) to (175, 75).
            CadDocument document = WithSettings(70, 1, 71, 3);
            CadCommandSession session = Session(document);
            Run(session, "QLEADER", P(0, 0), P(100, 30), P(150, 100), "0", "ABC", Enter);
            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Near(new GeoPoint3(100, 0, 0), leader.VertexAt(1), 1E-9);
            Near(new GeoPoint3(175, 75, 0), leader.LastVertex, 1E-9);

            // 90 then 30 degrees: (30, 100) to (0, 100), (100, 150) to (96.650635, 155.80127).
            document = WithSettings(70, 2, 71, 4);
            session = Session(document);
            Run(session, "QLEADER", P(0, 0), P(30, 100), P(100, 150), "0", "ABC", Enter);
            leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Near(new GeoPoint3(0, 100, 0), leader.VertexAt(1), 1E-9);
            Near(new GeoPoint3(96.650635, 155.80127, 0), leader.LastVertex, 1E-6);

            // 15 degrees then level, as AutoCAD: (100, 20) to (98.30127, 26.339746), (150, 40) to (150, 26.339746).
            document = WithSettings(70, 5, 71, 1);
            session = Session(document);
            Run(session, "QLEADER", P(0, 0), P(100, 20), P(150, 40), "0", "ABC", Enter);
            leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Near(new GeoPoint3(98.30127, 26.339746, 0), leader.VertexAt(1), 1E-6);
            Near(new GeoPoint3(150, 26.339746, 0), leader.LastVertex, 1E-6);
        }

        [Fact]
        public void ReuseCurrent_CopiesTheWordsKept_AskingNoWidthOrWords()
        {
            CadDocument document = WithSettings();
            DxfDatabase db = document.Database;
            var kept = new DxfMText
            {
                Contents = "KEPT",
                TextHeight = 5.0,
                Location = new GeoPoint3(0, -500, 0),
            };
            db.ModelSpace.AppendEntity(kept);
            var record = (DxfXrecord)db.NamedObjectsDictionary.GetAt("AcadDim");
            var data = record
                .Data.Select(v => v.TypeCode == 61 ? new DxfTypedValue(61, "2") : v)
                .Concat(new[] { new DxfTypedValue(340, kept.Handle) })
                .ToList();
            record.Data = data;
            CadCommandSession session = Session(document);

            Run(session, "QLEADER", P(0, 0), P(50, 50), P(100, 50));

            Assert.False(session.IsRunning);
            DxfMText copy = Words(document);
            Assert.NotEqual(kept.Handle, copy.Handle);
            Assert.Equal("KEPT", copy.Contents);
            Assert.Equal(DxfAttachmentPoint.TopLeft, copy.Attachment);
            Near(new GeoPoint3(100 + db.Dimgap, 52.5, 0), copy.Location);
        }

        [Fact]
        public void ReuseNext_KeepsTheNextWords_TheSettingsTurnedToReuseCurrent()
        {
            CadDocument document = WithSettings(61, 1);
            CadCommandSession session = Session(document);

            Run(session, "QLEADER", P(0, 0), P(50, 50), P(100, 50), "0", "R1", Enter);
            DxfMText first = Words(document);
            var record = (DxfXrecord)document.Database.NamedObjectsDictionary.GetAt("AcadDim");
            Assert.Equal("2", Group(record, 61));
            Assert.Equal(first.Handle, Group(record, 340));

            Run(session, "QLEADER", P(0, 100), P(50, 150), P(100, 150));

            Assert.False(session.IsRunning);
            Assert.Equal(2, All<DxfLeader>(document.ModelSpace).Count);
            Assert.Equal("\\A1;R1", Words(document).Contents);
            Assert.NotEqual(first.Handle, Words(document).Handle);
        }

        [Fact]
        public void CopyAnObject_AsksForTheObject_AndStandsTheCopyByItsMiddle()
        {
            DxfDatabase db = MetricDatabase();
            var original = new DxfMText
            {
                Contents = "COPYME",
                TextHeight = 2.5,
                Location = new GeoPoint3(0, -500, 0),
            };
            db.ModelSpace.AppendEntity(original);
            WriteSettings(db, 60, 1);
            CadDocument document = CadDocument.From(db, "copy.dxf");
            CadCommandSession session = Session(document);

            Run(session, "QLEADER", P(0, 0), P(50, 50), P(100, 50));
            Assert.Equal("Select an object to copy:", session.Prompt.Text);
            Run(session, (object)new[] { Assert.Single(All<DxfMText>(document.ModelSpace)).Handle });
            Assert.False(session.IsRunning);

            DxfMText copy = Words(document);
            Assert.Equal("COPYME", copy.Contents);
            Assert.Equal(DxfAttachmentPoint.MiddleLeft, copy.Attachment);
            Near(new GeoPoint3(100 + db.Dimgap, 50, 0), copy.Location);
        }

        [Fact]
        public void BlockReference_AsksTheBlockName_AndTolerance_DrawsNoAnnotation()
        {
            CadDocument document = WithSettings(60, 3);
            CadCommandSession session = Session(document);
            Run(session, "QLEADER", P(0, 0), P(50, 50), P(100, 50));
            Assert.Equal("Enter block name or [?]:", session.Prompt.Text);
            Run(session, Esc);

            document = WithSettings(60, 2);
            session = Session(document);
            Run(session, "QLEADER", P(0, 0), P(50, 50), P(100, 50));
            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Assert.Equal(DxfAnnotationType.NoAnnotation, leader.AnnoType);
            Assert.Contains(session.History, line => line.StartsWith("Tolerance needs the Geometric Tolerance dialog"));
        }

        [Fact]
        public void TheDrawingsLeaderArrow_IsWrittenAsTheLeadersDimldrblkOverride()
        {
            CadDocument document = WithSettings();
            DxfDatabase db = document.Database;
            CadCommandSession session = Session(document);
            document.ApplyEdit("arrow", null, s => s.Database.Dimldrblk = CadArrowheads.Ensure(s.Database, "Dot"));

            Run(session, "QLEADER", P(0, 0), P(50, 50), P(100, 50), "0", "ABC", Enter);

            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Assert.Equal("_Dot", leader.Dimldrblk?.Name);
            Assert.EndsWith("1070:341 1005:" + db.BlockTable["_Dot"].Handle + " 1002:}", Overrides(leader));
        }

        [Fact]
        public void Settings_WithNoWindow_SaysSo_AndAsksThePointAgain()
        {
            CadDocument document = WithSettings();
            CadCommandSession session = Session(document);

            Run(session, "QLEADER", Enter);

            Assert.Equal(
                "Leader Settings is shown by the window: QLEADER draws by the settings the drawing has.",
                session.History.Last()
            );
            Assert.Equal("Specify first leader point, or [Settings] <Settings>:", session.Prompt.Text);
        }

        [Fact]
        public void Settings_OkWritesTheRecordAndDimldrblk_AsOneUndoStep_AndQLeaderFollowsThem()
        {
            CadDocument document = CadDocument.From(MetricDatabase(), "metric.dxf");
            CadCommandSession session = Session(document);
            CadQLeaderRequest seen = null;
            string arrowShown = null;
            session.UiRequested += (s, e) =>
            {
                if (e.Payload is CadQLeaderRequest request)
                {
                    seen = request;
                    arrowShown = request.Arrow;
                    request.Settings.Annotation = CadQLeaderAnnotation.None;
                    request.Settings.MaxPoints = 2;
                    request.Settings.RightSide = CadQLeaderAttachment.BottomOfBottomLine;
                    request.Arrow = "Architectural tick";
                    request.Action = CadDialogAction.Ok;
                    e.Handled = true;
                }
            };

            Run(session, "QLEADER", "S");

            Assert.NotNull(seen);
            Assert.Equal(CadArrowheads.ClosedFilled, arrowShown);
            Assert.Equal(20, seen.Arrows.Count);
            var record = (DxfXrecord)document.Database.NamedObjectsDictionary.GetAt("AcadDim");
            Assert.Equal("990106", Group(record, 90));
            Assert.Equal("4", Group(record, 60));
            Assert.Equal("2", Group(record, 67));
            Assert.Equal("4", Group(record, 62));
            Assert.Equal("_ArchTick", document.Database.Dimldrblk?.Name);
            CadQLeaderSettings read = CadQLeaderSettings.Read(document.Database);
            Assert.Equal(CadQLeaderAnnotation.None, read.Annotation);
            Assert.Equal(CadQLeaderAttachment.MiddleOfBottomLine, read.LeftSide);

            // Back at the first point, the leader takes two points and no annotation.
            Assert.Equal("Specify first leader point, or [Settings] <Settings>:", session.Prompt.Text);
            Run(session, P(0, 0), P(50, 50));
            Assert.False(session.IsRunning);
            Assert.Equal(DxfAnnotationType.NoAnnotation, Assert.Single(All<DxfLeader>(document.ModelSpace)).AnnoType);
            AssertReopens(document);

            // Two undo steps: the leader, then the settings.
            Run(session, "U");
            Run(session, "U");
            Assert.False(document.Database.NamedObjectsDictionary.Contains("AcadDim"));
            Assert.Null(document.Database.Dimldrblk);
        }

        [Fact]
        public void Read_GivesTheDefaultsWithNoRecord_AndEachGroupOfOne()
        {
            CadQLeaderSettings defaults = CadQLeaderSettings.Read(MetricDatabase());
            Assert.Equal(CadQLeaderAnnotation.MText, defaults.Annotation);
            Assert.Equal(CadQLeaderAttachment.MiddleOfTopLine, defaults.RightSide);
            Assert.Equal(CadQLeaderAttachment.MiddleOfBottomLine, defaults.LeftSide);
            Assert.Equal(3, defaults.MaxPoints);
            Assert.True(defaults.PromptForWidth);

            CadDocument document = WithSettings(60, 3, 61, 1, 64, 1, 65, 1, 66, 1, 67, 5, 70, 2, 71, 5, 72, 1, 170, 2);
            CadQLeaderSettings read = CadQLeaderSettings.Read(document.Database);
            Assert.Equal(CadQLeaderAnnotation.BlockReference, read.Annotation);
            Assert.Equal(CadQLeaderReuse.ReuseNext, read.Reuse);
            Assert.True(read.Underline);
            Assert.True(read.Spline);
            Assert.True(read.NoLimit);
            Assert.Equal(5, read.MaxPoints);
            Assert.Equal(CadQLeaderAngle.Ninety, read.FirstSegment);
            Assert.Equal(CadQLeaderAngle.Fifteen, read.SecondSegment);
            Assert.True(read.FrameText);
            Assert.Equal(2, read.Tab);
        }
    }
}

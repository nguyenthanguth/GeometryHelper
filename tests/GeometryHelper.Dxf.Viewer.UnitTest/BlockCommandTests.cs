using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The block commands of C13: BLOCK, INSERT, WBLOCK, ATTDEF, EATTEDIT, ATTEDIT, EXPLODE of an INSERT and BASE, each
    /// driven with no window, its dialog answered through the request it hands the window, its values measured, and one undo
    /// step giving the drawing back byte for byte.
    /// </summary>
    public class BlockCommandTests
    {
        /// <summary>
        /// A metric drawing with a circle C1 of radius 10 about (100, 100) and a line L1 from (100, 90) to (100, 70), the
        /// bubble of a grid line.
        /// </summary>
        private static CadDocument Bubble()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(100, 100, 0), new GeoVector3(0, 0, 1), 10.0));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(100, 90, 0), new GeoPoint3(100, 70, 0)));
            return CadDocument.From(database, "blocks.dxf");
        }

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        /// <summary>Answers each request of a name in turn, as the window's dialog would, the request then handled.</summary>
        private static void Answer<T>(CadCommandSession session, string name, params Action<T>[] steps)
        {
            int next = 0;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == name && next < steps.Length)
                {
                    steps[next++]((T)e.Payload);
                    e.Handled = true;
                }
            };
        }

        private static string[] Handles(CadDocument document) => document.ModelSpace.Block.Select(e => e.Handle).ToArray();

        private static void UndoesToStart(CadDocument document, string start, string step)
        {
            Assert.Equal(step, document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        /// <summary>Makes the block GB of the bubble with the attribute GRID at its centre by the typed commands, base point (100, 70).</summary>
        private static CadDocument WithGridBubble()
        {
            CadDocument document = Bubble();
            Start(document, "-ATT", Enter, "grid", "Grid letter", "A", "J", "MC", P(100, 100), "3.5", "0");
            Start(document, "-B", "GB", P(100, 70), Handles(document), Enter);
            return document;
        }

        [Fact]
        public void TheBlockDialog_PicksItsBasePointAndObjectsOnTheCanvas_AndConvertsThemToAnInsert_AsOneUndoStep()
        {
            CadDocument document = Bubble();
            string start = document.Database.ToText();
            string[] objects = Handles(document);
            CadCommandSession session = Session(document);
            Answer<CadBlockDefinitionRequest>(
                session,
                "BLOCK",
                r => r.Action = CadDialogAction.SelectObjects,
                r => r.Action = CadDialogAction.PickPoint,
                r =>
                {
                    Assert.Equal(objects.OrderBy(h => h, StringComparer.OrdinalIgnoreCase), r.Objects);
                    Assert.Equal(new GeoPoint3(100, 70, 0), r.BasePoint);
                    Assert.Equal(CadBlockObjectAction.ConvertToBlock, r.ObjectAction);
                    Assert.Equal(DxfUnits.Millimeters, r.Units);
                    r.Name = "GRID_BUBBLE";
                    r.Description = "Grid bubble, 10 mm circle";
                    r.ScaleUniformly = true;
                    r.AllowExploding = false;
                    r.Action = CadDialogAction.Ok;
                });

            Run(session, "B");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, objects, Enter);
            Assert.Equal("Specify insertion base point:", session.PromptText);
            Run(session, P(100, 70));

            Assert.False(session.IsRunning);
            DxfBlockTableRecord block = document.Database.BlockTable["GRID_BUBBLE"];
            Assert.Equal(new GeoPoint3(100, 70, 0), block.Origin);
            Assert.Equal("Grid bubble, 10 mm circle", block.Comments);
            Assert.Equal(DxfBlockScaling.Uniform, block.BlockScaling);
            Assert.False(block.Explodable);
            Assert.Equal(DxfUnits.Millimeters, block.Units);
            Assert.Equal(2, block.Count);
            Near(new GeoPoint3(100, 100, 0), Assert.Single(block.OfType<DxfCircle>()).Center);

            DxfBlockReference insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace));
            Assert.Single(document.ModelSpace.Block);
            Assert.Equal("GRID_BUBBLE", insert.Name);
            Near(new GeoPoint3(100, 70, 0), insert.Position);
            AssertReopens(document);
            UndoesToStart(document, start, "BLOCK");
        }

        [Fact]
        public void BlockWrittenAsAutoCadWritesIt_ItsRecordAndBlockGroups_AndAnInsertWithAttribsAndItsSeqend()
        {
            CadDocument document = WithGridBubble();
            Start(document, "-I", "GB", P(500, 500), Enter, Enter, Enter, "C");

            string text = document.Database.ToText();
            Assert.Contains("  0\nBLOCK_RECORD\n", text, StringComparison.Ordinal);
            Assert.Contains("\n  2\nGB\n", text, StringComparison.Ordinal);
            Assert.Contains("100\nAcDbBlockBegin\n  2\nGB\n 70\n2\n", text, StringComparison.Ordinal);
            Assert.Contains("  0\nENDBLK\n", text, StringComparison.Ordinal);
            Assert.Contains("  0\nATTDEF\n", text, StringComparison.Ordinal);

            DxfBlockReference insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace));
            int at = text.IndexOf("  0\nINSERT\n  5\n" + insert.Handle + "\n", StringComparison.Ordinal);
            Assert.True(at > 0, text);
            int attrib = text.IndexOf("  0\nATTRIB\n", at, StringComparison.Ordinal);
            int seqend = text.IndexOf("  0\nSEQEND\n", at, StringComparison.Ordinal);
            string head = text.Substring(at, attrib - at);
            Assert.Contains(" 66\n1\n", head, StringComparison.Ordinal);
            Assert.Contains("100\nAcDbBlockReference\n", head, StringComparison.Ordinal);
            Assert.True(attrib < seqend);
            string record = text.Substring(attrib, seqend - attrib);
            Assert.Contains("100\nAcDbAttribute\n", record, StringComparison.Ordinal);
            Assert.Contains("  1\nC\n", record, StringComparison.Ordinal);
            Assert.Contains("  2\nGRID\n", record, StringComparison.Ordinal);
            AssertReopens(document);
        }

        [Fact]
        public void TypedAttDefBlockAndInsert_AskTheAttributeByItsPromptAndDefault_AndPlaceItWhereTheInsertPutsItsDefinition()
        {
            CadDocument document = Bubble();
            CadCommandSession session = Start(document, "-ATTDEF");
            Assert.Contains("Current attribute modes: Invisible=N Constant=N Verify=N Preset=N Lock position=Y Annotative=N Multiple lines=N", session.History);
            Assert.Equal("Enter an option to change [Invisible/Constant/Verify/Preset/Lock position/Multiple lines] <done>:", session.PromptText);
            Run(session, Enter, "grid", "Grid letter", "A");
            Assert.Equal("Specify start point of text or [Justify/Style]:", session.PromptText);
            Run(session, "J", "MC", P(100, 100), "3.5", "0");

            DxfAttributeDefinition definition = Assert.Single(All<DxfAttributeDefinition>(document.ModelSpace));
            Assert.Equal("GRID", definition.Tag);
            Assert.Equal("Grid letter", definition.Prompt);
            Assert.Equal("A", definition.TextString);
            Assert.Equal(3.5, definition.Height);
            Assert.Equal(DxfAttachmentPoint.MiddleCenter, definition.Justify);
            Assert.True(definition.LockPositionInBlock);
            Near(new GeoPoint3(100, 100, 0), definition.AlignmentPoint);

            session = Start(document, "-B", "GB", P(100, 70), Handles(document), Enter);
            Assert.Empty(document.ModelSpace.Block);
            Assert.True(document.Database.BlockTable["GB"].HasAttributeDefinitions);

            string start = document.Database.ToText();
            session = Start(document, "-INSERT");
            Assert.Equal("Enter block name or [?]:", session.PromptText);
            Run(session, "GB");
            Assert.Contains("Units: Millimeters   Conversion:   1.0000", session.History);
            Assert.Equal("Specify insertion point or [Basepoint/Scale/X/Y/Z/Rotate]:", session.PromptText);
            Run(session, P(500, 500));
            Assert.Equal("Enter X scale factor, specify opposite corner, or [Corner/XYZ] <1>:", session.PromptText);
            Run(session, "2");
            Assert.Equal("Enter Y scale factor <use X scale factor>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Specify rotation angle <0>:", session.PromptText);
            Run(session, "90");
            Assert.Contains("Enter attribute values", session.History);
            Assert.Equal("Grid letter <A>:", session.PromptText);
            Run(session, "C");

            Assert.False(session.IsRunning);
            DxfBlockReference insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace));
            Assert.Equal(new GeoVector3(2, 2, 2), insert.ScaleFactors);
            Assert.Equal(Math.PI / 2.0, insert.Rotation, 12);
            DxfAttributeReference attribute = Assert.Single(insert.AttributeCollection);
            Assert.Equal("GRID", attribute.Tag);
            Assert.Equal("C", attribute.TextString);
            Assert.Equal(7.0, attribute.Height, 12);

            // The definition's (100, 100) is 30 above the base point (100, 70): scaled by 2 and turned a quarter, 60 to the left of (500, 500).
            Near(new GeoPoint3(440, 500, 0), attribute.AlignmentPoint, 1E-9);
            AssertReopens(document);
            UndoesToStart(document, start, "-INSERT");
        }

        [Fact]
        public void TheBlocksPalette_PlacesTheBlockChosen_AtItsScaleAndRotation_AndAgainWithRepeatPlacement()
        {
            CadDocument document = WithGridBubble();
            string start = document.Database.ToText();
            CadCommandSession session = Session(document);
            Answer<CadInsertRequest>(session, "INSERT", r =>
            {
                r.Name = "GB";
                r.Scale = new GeoVector3(0.5, 0.5, 0.5);
                r.Rotation = Math.PI;
                r.Repeat = true;
                r.Action = CadDialogAction.Ok;
            });

            Run(session, "I");
            Assert.Equal("Specify insertion point or [Basepoint/Scale/X/Y/Z/Rotate]:", session.PromptText);
            Run(session, P(0, 0), Enter, P(50, 0), "B");
            Run(session, Enter);

            Assert.False(session.IsRunning);
            List<DxfBlockReference> inserts = All<DxfBlockReference>(document.ModelSpace);
            Assert.Equal(2, inserts.Count);
            Assert.Equal("A", inserts[0].AttributeCollection[0].TextString);
            Assert.Equal("B", inserts[1].AttributeCollection[0].TextString);

            // The centre of the circle, 30 above the base point, halved and turned half round: 15 below (50, 0).
            Near(new GeoPoint3(50, -15, 0), inserts[1].BlockTransform.Transform(new GeoPoint3(100, 100, 0)), 1E-9);
            UndoesToStart(document, start, "INSERT");
        }

        [Fact]
        public void ABlockOfAnotherDrawing_IsBroughtInWithItsDefinition_AndTheWholeFileMadeOneBlockOfItsName()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-c13-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                CadDocument source = WithGridBubble();
                string file = Path.Combine(folder, "bubbles.dxf");
                source.Database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0)));
                source.Database.Insbase = new GeoPoint3(0, 0, 0);
                source.Database.Save(file);

                CadDocument document = Bubble();
                CadCommandSession session = Session(document);
                Answer<CadInsertRequest>(
                    session,
                    "INSERT",
                    r =>
                    {
                        r.FileName = file;
                        r.Name = "GB";
                        r.SpecifyPointOnScreen = false;
                        r.Point = new GeoPoint3(10, 20, 0);
                        r.Action = CadDialogAction.Ok;
                    },
                    r =>
                    {
                        r.FileName = file;
                        r.Name = string.Empty;
                        r.SpecifyPointOnScreen = false;
                        r.Point = new GeoPoint3(200, 0, 0);
                        r.Action = CadDialogAction.Ok;
                    });

                Run(session, "INSERT", "D");
                Run(session, "INSERT");

                Assert.True(document.Database.BlockTable.Has("GB"));
                Assert.True(document.Database.BlockTable.Has("bubbles"));
                List<DxfBlockReference> inserts = All<DxfBlockReference>(document.ModelSpace);
                Assert.Equal(new[] { "GB", "bubbles" }, inserts.Select(i => i.Name).ToArray());
                Assert.Equal("D", inserts[0].AttributeCollection[0].TextString);
                Near(new GeoPoint3(10, 50, 0), inserts[0].BlockTransform.Transform(new GeoPoint3(100, 100, 0)), 1E-9);
                Assert.Single(document.Database.BlockTable["bubbles"].OfType<DxfLine>());
                AssertReopens(document);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void WriteBlockOfObjects_WritesANewMetricDrawing_TheBasePointAtItsOrigin_AndKeepsTheObjects()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-c13-" + Guid.NewGuid().ToString("N"));

            try
            {
                CadDocument document = Bubble();
                string start = document.Database.ToText();
                string file = Path.Combine(folder, "bubble.dxf");
                CadCommandSession session = Session(document);
                Answer<CadWriteBlockRequest>(
                    session,
                    "WBLOCK",
                    r => r.Action = CadDialogAction.SelectObjects,
                    r => r.Action = CadDialogAction.PickPoint,
                    r =>
                    {
                        Assert.Equal(CadWriteBlockSource.Objects, r.Source);
                        Assert.Equal(CadBlockObjectAction.Retain, r.ObjectAction);
                        r.FileName = file;
                        r.Action = CadDialogAction.Ok;
                    });

                Run(session, "W", Handles(document), Enter, P(100, 70));

                Assert.False(session.IsRunning);
                Assert.Equal(start, document.Database.ToText());
                DxfDatabase written = DxfDatabase.Open(file);
                Assert.Equal(DxfUnits.Millimeters, written.Insunits);
                Near(new GeoPoint3(0, 30, 0), Assert.Single(written.ModelSpace.OfType<DxfCircle>()).Center);
                DxfLine line = Assert.Single(written.ModelSpace.OfType<DxfLine>());
                Near(new GeoPoint3(0, 20, 0), line.StartPoint);
                Near(new GeoPoint3(0, 0, 0), line.EndPoint);
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
        }

        [Fact]
        public void TypedWriteBlockOfABlock_WritesItsEntitiesFromItsOrigin_AndOfObjectsErasesThem()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-c13-" + Guid.NewGuid().ToString("N"));

            try
            {
                CadDocument document = WithGridBubble();
                string block = Path.Combine(folder, "gb.dxf");
                CadCommandSession session = Start(document, "-WBLOCK", block, "GB");
                Assert.False(session.IsRunning);
                DxfDatabase written = DxfDatabase.Open(block);
                Near(new GeoPoint3(0, 30, 0), Assert.Single(written.ModelSpace.OfType<DxfCircle>()).Center);
                Assert.Equal("GRID", Assert.Single(written.ModelSpace.OfType<DxfAttributeDefinition>()).Tag);

                CadDocument other = Bubble();
                string objects = Path.Combine(folder, "objects");
                Start(other, "-W", objects, Enter, P(0, 0), Handles(other), Enter);
                Assert.Empty(other.ModelSpace.Block);
                Assert.Equal(2, DxfDatabase.Open(objects + ".dxf").ModelSpace.Count);
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
        }

        [Fact]
        public void ExplodeOfAnInsertScaledUnlike_GivesAnEllipseForItsCircle_AndItsAttributeBackAsItsDefinition()
        {
            CadDocument document = WithGridBubble();
            Start(document, "-I", "GB", "X", "2", P(0, 0), Enter, Enter);
            DxfBlockReference insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace));
            Assert.Equal(new GeoVector3(2, 1, 1), insert.ScaleFactors);
            string start = document.Database.ToText();

            CadCommandSession session = Start(document, "X", new[] { insert.Handle }, Enter);

            Assert.False(session.IsRunning);
            Assert.Empty(All<DxfBlockReference>(document.ModelSpace));
            DxfEllipse ellipse = Assert.Single(All<DxfEllipse>(document.ModelSpace));
            Near(new GeoPoint3(0, 30, 0), ellipse.Center, 1E-9);
            Assert.Equal(20.0, ellipse.MajorAxis.Length, 9);
            Assert.Equal(0.5, ellipse.RadiusRatio, 9);
            DxfLine line = Assert.Single(All<DxfLine>(document.ModelSpace));
            Near(new GeoPoint3(0, 20, 0), line.StartPoint, 1E-9);
            DxfAttributeDefinition definition = Assert.Single(All<DxfAttributeDefinition>(document.ModelSpace));
            Assert.Equal("GRID", definition.Tag);
            Assert.Equal("A", definition.TextString);
            UndoesToStart(document, start, "EXPLODE");
        }

        [Fact]
        public void ExplodeOfABlockNotAllowedToExplode_IsRefused()
        {
            CadDocument document = WithGridBubble();
            document.Database.BlockTable["GB"].Explodable = false;
            Start(document, "-I", "GB", P(0, 0), Enter, Enter, Enter, Enter);
            string handle = Assert.Single(All<DxfBlockReference>(document.ModelSpace)).Handle;

            CadCommandSession session = Start(document, "EXPLODE", new[] { handle }, Enter);

            Assert.Equal("1 was not able to be exploded.", session.History.Last());
            Assert.NotNull(document.FindEntity(handle));
        }

        [Fact]
        public void TheEnhancedAttributeEditor_AppliesTheValueAndTextOptionsChanged_AndOkEndsIt_AsOneUndoStep()
        {
            CadDocument document = WithGridBubble();
            Start(document, "-I", "GB", P(0, 0), Enter, Enter, Enter, "C");
            string start = document.Database.ToText();
            CadCommandSession session = Session(document);
            Answer<CadAttributeEditRequest>(
                session,
                "EATTEDIT",
                r =>
                {
                    Assert.Equal("GB", r.Block);
                    CadAttributeRow row = Assert.Single(r.Rows);
                    Assert.Equal("GRID", row.Tag);
                    Assert.Equal("Grid letter", row.Prompt);
                    Assert.Equal("C", row.Value);
                    row.Value = "D";
                    r.Action = CadDialogAction.Apply;
                },
                r =>
                {
                    Assert.Equal("D", r.Rows[0].Value);
                    r.Rows[0].Height = 5.0;
                    r.Rows[0].ColorIndex = 1;
                    r.Action = CadDialogAction.Ok;
                });

            Run(session, "EATTEDIT");
            Assert.Equal("Select a block:", session.PromptText);
            Run(session, P(0, 40));

            Assert.False(session.IsRunning);
            DxfAttributeReference attribute = Assert.Single(All<DxfBlockReference>(document.ModelSpace)).AttributeCollection.Single();
            Assert.Equal("D", attribute.TextString);
            Assert.Equal(5.0, attribute.Height);
            Assert.Equal(1, attribute.Color.ColorIndex);
            AssertReopens(document);
            UndoesToStart(document, start, "EATTEDIT");
        }

        [Fact]
        public void TypedAttEdit_OneAtATime_ReplacesAValue_AndMovesAnAttribute()
        {
            CadDocument document = WithGridBubble();
            Start(document, "-I", "GB", P(0, 0), Enter, Enter, Enter, "C");
            string handle = Assert.Single(All<DxfBlockReference>(document.ModelSpace)).Handle;

            CadCommandSession session = Start(document, "-ATE", Enter, Enter, "GR*", Enter, new[] { handle }, Enter);
            Assert.Contains("1 attributes selected.", session.History);
            Assert.Equal("Enter an option [Value/Position/Height/Angle/Style/Layer/Color/Next] <Next>:", session.PromptText);
            Run(session, "V", "R", "E", "P", P(10, 40), Enter);

            Assert.False(session.IsRunning);
            DxfAttributeReference attribute = Assert.Single(All<DxfBlockReference>(document.ModelSpace)).AttributeCollection.Single();
            Assert.Equal("E", attribute.TextString);
            Near(new GeoPoint3(10, 40, 0), attribute.AlignmentPoint, 1E-9);
            Assert.Equal("-ATTEDIT", document.NextUndo.Name);
        }

        [Fact]
        public void TypedBlockOfAUsedName_AsksToRedefineIt_AndInsertsShowTheNewDefinition()
        {
            CadDocument document = WithGridBubble();
            Start(document, "-I", "GB", P(0, 0), Enter, Enter, Enter, Enter);
            document.Database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(5, 0, 0)));
            document.Regen();
            string line = All<DxfLine>(document.ModelSpace).Single().Handle;

            CadCommandSession session = Start(document, "-BLOCK", "GB");
            Assert.Equal("Block \"GB\" already exists. Redefine it? [Yes/No] <No>:", session.PromptText);
            Run(session, "Y", P(0, 0), new[] { line }, Enter);

            DxfBlockTableRecord block = document.Database.BlockTable["GB"];
            Assert.Equal(new GeoPoint3(0, 0, 0), block.Origin);
            Assert.Single(block);
            Assert.IsType<DxfLine>(block.Single());
        }

        [Fact]
        public void TheAttributeDefinitionDialog_TakesItsHeightPickedOnTheCanvas_AndAsksTheStartPointAfterOk()
        {
            CadDocument document = Bubble();
            CadCommandSession session = Session(document);
            Answer<CadAttributeDefinitionRequest>(
                session,
                "ATTDEF",
                r => r.Action = CadDialogAction.PickHeight,
                r =>
                {
                    Assert.Equal(5.0, r.Height, 12);
                    r.Tag = "level";
                    r.Prompt = "Level name";
                    r.Default = "L2";
                    r.Invisible = true;
                    r.Action = CadDialogAction.Ok;
                });

            Run(session, "ATT");
            Assert.Equal("Specify text height <2.5000>:", session.PromptText);
            Run(session, "5");
            Assert.Equal("Specify start point:", session.PromptText);
            Run(session, P(0, -10));

            DxfAttributeDefinition definition = Assert.Single(All<DxfAttributeDefinition>(document.ModelSpace));
            Assert.Equal("LEVEL", definition.Tag);
            Assert.True(definition.Invisible);
            Assert.Equal(5.0, definition.Height, 12);
            Near(new GeoPoint3(0, -10, 0), definition.Position);
            Assert.Equal("ATTDEF", document.NextUndo.Name);
        }

        [Fact]
        public void BaseSetsTheInsertionBasePointOfTheDrawing_AsOneUndoStep()
        {
            CadDocument document = Bubble();
            string start = document.Database.ToText();

            CadCommandSession session = Start(document, "BASE");
            Assert.Equal("Enter base point <0.0000,0.0000,0.0000>:", session.PromptText);
            Run(session, P(100, 70));

            Assert.Equal(new GeoPoint3(100, 70, 0), document.Database.Insbase);
            UndoesToStart(document, start, "BASE");
        }
    }
}

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
    /// LEADER and QLEADER past their first round, as AutoCAD 2021 draws them (probed through accoreconsole, probes GhLd2a and
    /// GhLd2b): every dimension variable other than the style's written as an override in the order of its group, the words
    /// in DIMCLRT; the hook along the X axis of a UCS other than the world's; a copy of an INSERT at the leader's end, and
    /// Reuse Current of one; the leader previewed at the cursor, its segment held to its angle; a user arrow chosen in Leader
    /// Settings.
    /// </summary>
    public class LeaderFollowUpTests
    {
        private static DxfDatabase MetricDatabase()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.DimStyleTable[database.Dimstyle].Dimtad = 0;
            database.Dimtad = 0;
            return database;
        }

        private static string Overrides(DxfLeader leader) =>
            string.Join(
                " ",
                (leader.GetXDataForApplication("ACAD") ?? Array.Empty<DxfTypedValue>())
                    .Skip(1)
                    .Select(v => v.TypeCode + ":" + v.Value)
            );

        private static void WriteSettings(DxfDatabase database, string reused, params int[] codeValue)
        {
            var values = new SortedDictionary<int, int>
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
            data.AddRange(values.Select(p => new DxfTypedValue(p.Key, p.Value.ToString(CultureInfo.InvariantCulture))));

            if (reused != null)
            {
                data.Add(new DxfTypedValue(340, reused));
            }

            database.NamedObjectsDictionary.SetAt("AcadDim", new DxfXrecord { Data = data });
        }

        /// <summary>A metric drawing with the block B1 (a circle) and an INSERT of it at (500, 500), scaled 1.5 by 2.5, turned 0.3.</summary>
        private static DxfDatabase WithInsert(out DxfBlockReference insert)
        {
            DxfDatabase database = MetricDatabase();
            var block = new DxfBlockTableRecord { Name = "B1", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(block);
            block.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 1));
            insert = new DxfBlockReference(new GeoPoint3(500, 500, 0), block)
            {
                ScaleFactors = new GeoVector3(1.5, 2.5, 1),
                Rotation = 0.3,
            };
            database.ModelSpace.AppendEntity(insert);
            return database;
        }

        // ---- Overrides

        [Fact]
        public void Leader_WritesEveryVariableOtherThanTheStyles_InTheOrderOfTheirGroups_TheWordsInDimclrt()
        {
            // AutoCAD 2021, DIMEXO, DIMCLRD, DIMCLRT, DIMLDRBLK and DIMLWD other than the style's: 42, 176, 178, 341, 371.
            CadDocument document = CadDocument.From(MetricDatabase(), "metric.dxf");
            DxfDatabase db = document.Database;
            CadCommandSession session = Session(document);
            document.ApplyEdit("variables", null, s =>
            {
                s.Database.Dimexo = 0.5;
                s.Database.Dimclrd = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);
                s.Database.Dimclrt = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 2);
                s.Database.Dimlwd = DxfLineWeight.LineWeight030;
                s.Database.Dimldrblk = CadArrowheads.Ensure(s.Database, "Dot");
            });
            string dot = db.BlockTable["_Dot"].Handle;

            Run(session, "LEADER", P(0, 0), P(10, 10), P(20, 10), Enter, "Hello", Enter);

            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Assert.Equal(
                "1000:DSTYLE 1002:{ 1070:42 1040:0.5 1070:176 1070:1 1070:178 1070:2 1070:341 1005:"
                    + dot
                    + " 1070:371 1070:30 1002:}",
                Overrides(leader)
            );
            Assert.Equal(1, leader.Dimclrd.ColorIndex);
            Assert.Equal(2, ((DxfMText)leader.Annotation).Color.ColorIndex);

            // QLEADER writes DIMGAP and DIMTAD first, then the others in the same order.
            Run(session, "QLEADER", P(0, 100), P(10, 110), P(20, 110), "0", "Hi", Enter);
            DxfLeader quick = All<DxfLeader>(document.ModelSpace).Last();
            string gap = db.Dimgap.ToString("R", CultureInfo.InvariantCulture);
            Assert.Equal(
                "1000:DSTYLE 1002:{ 1070:147 1040:"
                    + gap
                    + " 1070:77 1070:0 1070:42 1040:0.5 1070:176 1070:1 1070:178 1070:2 1070:341 1005:"
                    + dot
                    + " 1070:371 1070:30 1002:}",
                Overrides(quick)
            );
            Assert.Equal(2, ((DxfMText)quick.Annotation).Color.ColorIndex);
            AssertReopens(document);
        }

        [Fact]
        public void DimclrtByBlock_LeavesTheWordsInTheCurrentColour()
        {
            CadDocument document = CadDocument.From(MetricDatabase(), "metric.dxf");
            CadCommandSession session = Session(document);
            document.ApplyEdit("colour", null, s => s.Database.Dimclrt = DxfColor.ByBlock);

            Run(session, "LEADER", P(0, 0), P(10, 10), P(20, 10), Enter, "Hello", Enter);

            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Assert.True(((DxfMText)leader.Annotation).Color.IsByLayer);
        }

        // ---- The hook along the UCS

        [Fact]
        public void InAUcsTurned30Degrees_TheHookRunsAlongItsX_ItsOwnVertexDimaszBack_AndTheWordsTurnWithIt()
        {
            CadDocument document = CadDocument.From(MetricDatabase(), "metric.dxf");
            DxfDatabase db = document.Database;
            CadUcs ucs = CadUcs.InPlane(P(0, 0), Math.PI / 6.0);
            document.Ucs.SetCurrent(ucs);
            CadCommandSession session = Session(document);
            GeoVector2 x = ucs.ToWorld(new GeoVector2(1, 0));
            GeoVector2 y = ucs.ToWorld(new GeoVector2(0, 1));

            // AutoCAD 2021 (GHTH3): from the UCS's (0, 0) to its (10, 10), three vertices, the hook's DIMASZ back along its X,
            // the words DIMGAP along it.
            GeoPoint2 right = ucs.ToWorld(P(10, 10));
            Run(session, "LEADER", ucs.ToWorld(P(0, 0)), right, Enter, "R", Enter);
            DxfLeader first = All<DxfLeader>(document.ModelSpace).Last();
            Assert.Equal(3, first.NumVertices);
            Near(new GeoPoint3(right.X - db.Dimasz * x.X, right.Y - db.Dimasz * x.Y, 0), first.VertexAt(1));
            Assert.True(first.HasHookLine);
            DxfMText words = (DxfMText)first.Annotation;
            Assert.Equal(DxfAttachmentPoint.MiddleLeft, words.Attachment);
            Near(new GeoPoint3(right.X + db.Dimgap * x.X, right.Y + db.Dimgap * x.Y, 0), words.Location);
            Assert.Equal(x.X, words.Direction.X, 12);
            Assert.Equal(x.Y, words.Direction.Y, 12);

            // Up to the left in the UCS: the words end DIMGAP short of the end, against its X.
            GeoPoint2 left = ucs.ToWorld(P(-10, 60));
            Run(session, "LEADER", ucs.ToWorld(P(0, 50)), left, Enter, "L", Enter);
            DxfLeader second = All<DxfLeader>(document.ModelSpace).Last();
            Assert.Equal(3, second.NumVertices);
            Near(new GeoPoint3(left.X + db.Dimasz * x.X, left.Y + db.Dimasz * x.Y, 0), second.VertexAt(1));
            Assert.True(second.HasHookLine);
            Assert.Equal(DxfAttachmentPoint.MiddleRight, ((DxfMText)second.Annotation).Attachment);
            Near(
                new GeoPoint3(left.X - db.Dimgap * x.X, left.Y - db.Dimgap * x.Y, 0),
                ((DxfMText)second.Annotation).Location
            );

            // Level in the UCS, 30 degrees steep in the world: no hook.
            Run(session, "LEADER", ucs.ToWorld(P(0, 100)), ucs.ToWorld(P(10, 100)), Enter, "V", Enter);
            Assert.False(All<DxfLeader>(document.ModelSpace).Last().HasHookLine);

            // DIMTAD 1: the words DIMGAP above the end along the UCS's Y.
            db.Dimtad = 1;
            GeoPoint2 above = ucs.ToWorld(P(10, 160));
            Run(session, "LEADER", ucs.ToWorld(P(0, 150)), above, Enter, "T", Enter);
            DxfMText raised = (DxfMText)All<DxfLeader>(document.ModelSpace).Last().Annotation;
            Assert.Equal(DxfAttachmentPoint.BottomLeft, raised.Attachment);
            Near(
                new GeoPoint3(
                    above.X + db.Dimgap * x.X + db.Dimgap * y.X,
                    above.Y + db.Dimgap * x.Y + db.Dimgap * y.Y,
                    0
                ),
                raised.Location
            );
            AssertReopens(document);
        }

        [Fact]
        public void InAUcsOnlyMoved_TheHookHasItsOwnVertex_AsInTheWorld()
        {
            // AutoCAD 2021 (GHTH3), UCS origin (5, 5): from (5, 205) to (15, 215), three vertices, the hook's at (14.82, 215)
            // in AutoCAD's Standard, DIMASZ short of the end.
            CadDocument document = CadDocument.From(MetricDatabase(), "metric.dxf");
            document.Ucs.SetCurrent(CadUcs.InPlane(P(5, 5), 0.0));
            CadCommandSession session = Session(document);

            Run(session, "LEADER", P(5, 205), P(15, 215), Enter, "M", Enter);

            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Assert.Equal(3, leader.NumVertices);
            Near(new GeoPoint3(15 - document.Database.Dimasz, 215, 0), leader.VertexAt(1));
            Assert.True(leader.HasHookLine);
            Near(new GeoPoint3(15 + document.Database.Dimgap, 215, 0), ((DxfMText)leader.Annotation).Location);
        }

        [Fact]
        public void QLeaderInATurnedUcs_SetsItsWordsAlongItsX_RaisedAlongTheWorldsY_AsAutoCadDoes()
        {
            // AutoCAD 2021: Middle of top line half DIMTXT up the world's Y, whatever the UCS.
            CadDocument document = CadDocument.From(MetricDatabase(), "metric.dxf");
            DxfDatabase db = document.Database;
            CadUcs ucs = CadUcs.InPlane(P(0, 0), Math.PI / 6.0);
            document.Ucs.SetCurrent(ucs);
            CadCommandSession session = Session(document);
            GeoVector2 x = ucs.ToWorld(new GeoVector2(1, 0));

            GeoPoint2 right = ucs.ToWorld(P(10, 160));
            Run(session, "QLEADER", ucs.ToWorld(P(0, 150)), right, Enter, "0", "Q", Enter);
            DxfMText words = (DxfMText)All<DxfLeader>(document.ModelSpace).Last().Annotation;
            Assert.Equal(DxfAttachmentPoint.TopLeft, words.Attachment);
            Near(
                new GeoPoint3(right.X + db.Dimgap * x.X, right.Y + db.Dimgap * x.Y + db.Dimtxt / 2, 0),
                words.Location
            );

            GeoPoint2 left = ucs.ToWorld(P(-10, 210));
            Run(session, "QLEADER", ucs.ToWorld(P(0, 200)), left, Enter, "0", "QL", Enter);
            DxfLeader leader = All<DxfLeader>(document.ModelSpace).Last();
            Assert.Equal(3, leader.NumVertices);
            Assert.True(leader.HasHookLine);
            DxfMText leftWords = (DxfMText)leader.Annotation;
            Assert.Equal(DxfAttachmentPoint.BottomRight, leftWords.Attachment);
            Near(
                new GeoPoint3(left.X - db.Dimgap * x.X, left.Y - db.Dimgap * x.Y - db.Dimtxt / 2, 0),
                leftWords.Location
            );
        }

        // ---- Copy and Reuse Current of an INSERT

        [Fact]
        public void CopyOfAnInsert_StandsItsCopyAtTheEnd_TurnedToTheUcs_ScaledAsItIs_WithNoHook()
        {
            DxfDatabase database = WithInsert(out DxfBlockReference original);
            CadDocument document = CadDocument.From(database, "insert.dxf");
            CadCommandSession session = Session(document);
            string source = original.Handle;

            // AutoCAD 2021: the copy's insertion point at (10, 320), its scale kept, its rotation 0; two vertices, no hook.
            Run(session, "LEADER", P(0, 300), P(10, 320), Enter, Enter, "C", new[] { source });

            Assert.False(session.IsRunning);
            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Assert.Equal(DxfAnnotationType.BlockRef, leader.AnnoType);
            Assert.Equal(2, leader.NumVertices);
            Assert.False(leader.HasHookLine);
            DxfBlockReference copy = Assert.IsType<DxfBlockReference>(leader.Annotation);
            Assert.NotEqual(source, copy.Handle);
            Near(new GeoPoint3(10, 320, 0), copy.Position);
            Assert.Equal(0.0, copy.Rotation, 12);
            Assert.Equal(new GeoVector3(1.5, 2.5, 1), copy.ScaleFactors);
            Assert.Equal(new GeoPoint3(500, 500, 0), original.Position);
            Assert.Equal(0.3, original.Rotation);

            // In a UCS turned 30 degrees the copy is turned 30 degrees, as AutoCAD turns it.
            CadUcs ucs = CadUcs.InPlane(P(0, 0), Math.PI / 6.0);
            document.Ucs.SetCurrent(ucs);
            GeoPoint2 end = ucs.ToWorld(P(20, 110));
            Run(session, "LEADER", ucs.ToWorld(P(0, 100)), ucs.ToWorld(P(10, 110)), end, Enter, Enter, "C");
            Run(session, (object)new[] { source });
            DxfBlockReference turned = (DxfBlockReference)All<DxfLeader>(document.ModelSpace).Last().Annotation;
            Near(new GeoPoint3(end.X, end.Y, 0), turned.Position);
            Assert.Equal(Math.PI / 6.0, turned.Rotation, 12);
            AssertReopens(document);
        }

        [Fact]
        public void CopyAnObject_OfQLeader_CopiesAnInsertToo()
        {
            DxfDatabase database = WithInsert(out DxfBlockReference original);
            WriteSettings(database, null, 60, 1);
            CadDocument document = CadDocument.From(database, "insert.dxf");
            CadCommandSession session = Session(document);

            Run(session, "QLEADER", P(0, 1000), P(10, 1010), P(20, 1010));
            Run(session, (object)new[] { original.Handle });

            Assert.False(session.IsRunning);
            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            DxfBlockReference copy = Assert.IsType<DxfBlockReference>(leader.Annotation);
            Near(new GeoPoint3(20, 1010, 0), copy.Position);
            Assert.False(leader.HasHookLine);
            Assert.StartsWith("1000:DSTYLE 1002:{ 1070:147 1040:", Overrides(leader));
        }

        [Fact]
        public void ReuseCurrentOfAnInsert_CopiesItToTheEnd_AskingNothing()
        {
            // AutoCAD 2021: AcadDim 61 of 2 and 340 the INSERT: a copy at (20, 1210), scale kept, rotation 0, no hook.
            DxfDatabase database = WithInsert(out DxfBlockReference original);
            WriteSettings(database, original.Handle, 61, 2);
            CadDocument document = CadDocument.From(database, "insert.dxf");
            CadCommandSession session = Session(document);

            Run(session, "QLEADER", P(0, 1200), P(10, 1210), P(20, 1210));

            Assert.False(session.IsRunning);
            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            DxfBlockReference copy = Assert.IsType<DxfBlockReference>(leader.Annotation);
            Assert.NotEqual(original.Handle, copy.Handle);
            Near(new GeoPoint3(20, 1210, 0), copy.Position);
            Assert.Equal(0.0, copy.Rotation, 12);
            Assert.Equal(new GeoVector3(1.5, 2.5, 1), copy.ScaleFactors);
            Assert.Equal(2, All<DxfBlockReference>(document.ModelSpace).Count);
            Assert.True(document.Undo());
            Assert.Single(All<DxfBlockReference>(document.ModelSpace));
        }

        // ---- Previews

        [Fact]
        public void QLeader_PreviewsTheLeaderAtTheCursor_ItsSegmentHeldToTheAngle_AndMakesWhatItPreviews()
        {
            // First segment held level: the cursor at (100, 30) previews what (100, 0) does.
            DxfDatabase database = MetricDatabase();
            WriteSettings(database, null, 60, 4, 67, 2, 70, 1);
            CadDocument document = CadDocument.From(database, "metric.dxf");
            CadCommandSession session = Session(document);

            Run(session, "QLEADER", P(0, 0));
            CadPreview preview = session.Prompt.Preview;
            Assert.NotNull(preview);
            Assert.False(preview.ShowsBand);
            List<string> held = PreviewTests.Shapes(preview.Items(P(100, 30)));
            Assert.NotEmpty(held);
            Assert.Equal(held, PreviewTests.Shapes(preview.Items(P(100, 0))));
            Assert.NotEqual(held, PreviewTests.Shapes(preview.Items(P(120, 30))));

            // The preview at the cursor is the leader the click makes, arrow and line alike.
            PreviewTests.PreviewMakes(session, P(100, 30));
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void Leader_PreviewsItsArrowAndLineToTheCursor_ThroughThePointsPicked()
        {
            CadDocument document = CadDocument.From(MetricDatabase(), "metric.dxf");
            CadCommandSession session = Session(document);

            Run(session, "LEADER", P(0, 0));
            List<string> first = PreviewTests.Shapes(session.Prompt.Preview.Items(P(10, 10)));
            Assert.Contains(first, s => s.StartsWith("DisplayFill2", StringComparison.Ordinal));
            Assert.False(session.Prompt.Preview.ShowsBand);

            Run(session, P(10, 10));
            List<string> second = PreviewTests.Shapes(session.Prompt.Preview.Items(P(20, 10)));
            Assert.Contains(second, s => s.EndsWith("20.000000 10.000000", StringComparison.Ordinal));
            Assert.Empty(All<DxfLeader>(document.ModelSpace));
        }

        // ---- A user arrow

        [Fact]
        public void LeaderSettings_OffersTheDrawingsBlocksAsAUserArrow_AndOkWritesItAsDimldrblkAndGroup3()
        {
            DxfDatabase database = WithInsert(out _);
            CadDocument document = CadDocument.From(database, "insert.dxf");
            CadCommandSession session = Session(document);
            IReadOnlyList<string> blocks = null;
            session.UiRequested += (s, e) =>
            {
                if (e.Payload is CadQLeaderRequest request)
                {
                    blocks = request.Blocks;
                    request.Arrow = "B1";
                    request.Settings.UserArrow = "B1";
                    request.Action = CadDialogAction.Ok;
                    e.Handled = true;
                }
            };

            Run(session, "QLEADER", "S");

            Assert.Contains("B1", blocks);
            Assert.DoesNotContain(blocks, b => b.StartsWith("*", StringComparison.Ordinal));
            Assert.Equal("B1", document.Database.Dimldrblk?.Name);
            var record = (DxfXrecord)document.Database.NamedObjectsDictionary.GetAt("AcadDim");
            Assert.Equal("B1", record.Data.First(v => v.TypeCode == 3).Value);

            // The leader drawn next names the block as its DIMLDRBLK override.
            Run(session, P(0, 0), P(10, 10), P(20, 10), "0", "U", Enter);
            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Assert.Equal("B1", leader.Dimldrblk?.Name);
            Assert.Contains("1070:341 1005:" + document.Database.BlockTable["B1"].Handle, Overrides(leader));
        }
    }
}

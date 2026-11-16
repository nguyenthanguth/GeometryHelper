using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The associative arrays of ARRAYRECT, ARRAYPOLAR and ARRAYPATH (ASsociative Yes) and ARRAYEDIT, as AutoCAD 2021 stores and
    /// edits them: an INSERT of an anonymous block of items, the source objects in a block of their own, and the parameters in an
    /// action of the drawing's associative network. The numbers are what AutoCAD 2021's console gave for the same commands (its
    /// <c>AssocArray</c> API read back the items of the file written here, and of an edit of it, with the same origins).
    /// </summary>
    public class AssociativeArrayTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static CadDocument Circle(double x, double y, double radius, out string handle)
        {
            var database = new DxfDatabase("AC1027");
            var circle = new DxfCircle(new GeoPoint3(x, y, 0.0), new GeoVector3(0.0, 0.0, 1.0), radius);
            database.ModelSpace.AppendEntity(circle);
            handle = circle.Handle;
            return CadDocument.From(database, "array.dxf");
        }

        private static DxfBlockReference TheArray(CadDocument document) =>
            All<DxfBlockReference>(document.ModelSpace).Single();

        private static CadAssocArray Read(CadDocument document, out List<CadArrayItem> items)
        {
            DxfBlockReference insert = TheArray(document);
            CadArrays.Record record = CadArrays.Find(document.Database, insert);
            Assert.NotNull(record);
            return CadArrays.ReadArray(document, record, out items);
        }

        private static int Count(CadDocument document, string type)
        {
            string[] lines = document.Database.ToText().Split((char)10);
            int count = 0;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() == "0" && lines[i + 1].Trim() == type)
                {
                    count++;
                }
            }

            return count;
        }

        private static void Near(double expected, double actual) =>
            Assert.InRange(actual, expected - 1E-6, expected + 1E-6);

        private static void At(CadArrayItem item, double x, double y, double z = 0.0)
        {
            Near(x, item.Matrix[0, 3]);
            Near(y, item.Matrix[1, 3]);
            Near(z, item.Matrix[2, 3]);
        }

        /// <summary>A rectangular array of a circle of radius 5 at (10, 10): 3 columns 20 apart and 4 rows 30 apart, as ARRAYRECT was run in AutoCAD.</summary>
        private static CadDocument Rectangular(out string circle)
        {
            CadDocument document = Circle(10, 10, 5, out circle);
            Start(document, "ARRAYRECT", new[] { circle }, Enter, "AS", "Y", "COU", "3", "4", "S", "20", "30", "X");
            return document;
        }

        [Fact]
        public void ArrayRectAssociativeYes_MakesOneInsertOfAnAnonymousBlock_AndTheObjectsOfTheNetwork()
        {
            CadDocument document = Circle(10, 10, 5, out string circle);
            string start = document.Database.ToText();
            CadCommandSession session = Start(document, "ARRAYRECT", new[] { circle }, Enter);
            Assert.Contains("Type = Rectangular  Associative = No", session.History);
            Run(session, "AS");
            Assert.Equal("Create associative array [Yes/No] <No>:", session.PromptText);
            Run(session, "Y", "COU", "3", "4", "S", "20", "30", "X");

            // The circle is taken into the array: model space holds the INSERT alone, at the base point (the box's middle).
            Assert.Empty(All<DxfCircle>(document.ModelSpace));
            DxfBlockReference insert = TheArray(document);
            Assert.StartsWith("*U", insert.Name, StringComparison.Ordinal);
            Near(10.0, insert.Position.X);
            Near(10.0, insert.Position.Y);
            Assert.Equal(12, insert.BlockTableRecord.OfType<DxfBlockReference>().Count());

            CadAssocArray array = Read(document, out List<CadArrayItem> items);
            Assert.Equal(CadArrayKind.Rectangular, array.Kind);
            Assert.Equal(3, array.Columns);
            Assert.Equal(4, array.Rows);
            Near(20.0, array.ColumnSpacing);
            Near(30.0, array.RowSpacing);
            Near(Math.PI / 2.0, array.AxesAngle);
            Assert.Equal(12, items.Count);
            At(items[0], 0, 0);
            At(items[1], 0, 30);
            At(items[4], 20, 0);
            At(items[11], 40, 90);

            // The source is held from the base point: the circle at the origin of its own block.
            DxfCircle source = Assert.IsType<DxfCircle>(CadArrays.SourcesOf(insert).Single());
            Near(0.0, source.Center.X);
            Near(0.0, source.Center.Y);

            Assert.Equal(1, Count(document, "ACDBASSOCARRAYACTIONBODY"));
            Assert.Equal(1, Count(document, "ACDBASSOCACTION"));
            Assert.Equal(3, Count(document, "ACDBASSOCDEPENDENCY"));
            Assert.Equal(3, Count(document, "ACDBASSOCVERTEXACTIONPARAM"));
            Assert.Equal(2, Count(document, "ACDBASSOCNETWORK"));
            Assert.Equal("ARRAYRECT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void TheArrayIsWrittenAndReadBack_WithItsParametersAndItems()
        {
            CadDocument document = Rectangular(out _);
            string text = document.Database.ToText();
            CadDocument again = CadDocument.From(DxfDatabase.Parse(text), "again.dxf");
            Assert.Equal(text, again.Database.ToText());

            CadAssocArray array = Read(again, out List<CadArrayItem> items);
            Assert.Equal(3, array.Columns);
            Assert.Equal(4, array.Rows);
            Assert.Equal(12, items.Count);
            At(items[11], 40, 90);
        }

        [Fact]
        public void ARowAxisAngleOfTheUcs_IsKeptAsTheRowProfile_AndLevelsAndElevationsAreBuilt()
        {
            CadDocument document = Circle(10, 10, 5, out string circle);
            Start(
                document,
                "ARRAYRECT",
                new[] { circle },
                Enter,
                "AS",
                "Y",
                "COU",
                "3",
                "2",
                "S",
                "20",
                "25",
                "ROW",
                "2",
                "25",
                "4",
                "L",
                "2",
                "7",
                "X"
            );

            CadAssocArray array = Read(document, out List<CadArrayItem> items);
            Assert.Equal(2, array.Levels);
            Near(7.0, array.LevelSpacing);
            Near(4.0, array.RowElevation);
            Assert.Equal(12, items.Count);

            // AutoCAD 2021: (0, 25, 4) for the second row of the first column, (0, 25, 11) one level up.
            CadArrayItem row = items.Single(i => i.Index == 0 && i.Row == 1 && i.Level == 0);
            At(row, 0, 25, 4);
            At(items.Single(i => i.Index == 0 && i.Row == 1 && i.Level == 1), 0, 25, 11);
        }

        [Fact]
        public void ArrayPolarAssociativeYes_KeepsTheCentreAsTheInsertAndTheItemsTurnedAboutIt()
        {
            CadDocument document = Circle(60, 0, 5, out string circle);
            Start(document, "ARRAYPOLAR", new[] { circle }, Enter, P(0, 0), "AS", "Y", "X");

            DxfBlockReference insert = TheArray(document);
            Near(0.0, insert.Position.X);
            Near(0.0, insert.Position.Y);
            CadAssocArray array = Read(document, out List<CadArrayItem> items);
            Assert.Equal(CadArrayKind.Polar, array.Kind);
            Assert.Equal(6, array.Items);
            Near(Math.PI / 3.0, array.Between);
            Near(60.0, array.Radius);
            Assert.True(array.Rotate);
            Assert.False(array.Clockwise);

            // AutoCAD 2021: the second item at (30, 51.961524), its X axis 60 degrees round.
            At(items[1], 30, 51.961524);
            Near(0.5, items[1].Matrix[0, 0]);
            Near(0.866025, items[1].Matrix[1, 0]);
            At(items[3], -60, 0);
        }

        [Fact]
        public void ArrayPolarClockwiseWithRowsLevelsAndNoRotation_IsKeptInTheParameters()
        {
            CadDocument document = Circle(60, 10, 5, out string circle);
            Start(
                document,
                "ARRAYPOLAR",
                new[] { circle },
                Enter,
                P(0, 0),
                "AS",
                "Y",
                "I",
                "8",
                "F",
                "-120",
                "ROW",
                "2",
                "10",
                Enter,
                "ROT",
                "N",
                "L",
                "2",
                "7",
                "X"
            );

            CadAssocArray array = Read(document, out List<CadArrayItem> items);
            Assert.Equal(8, array.Items);
            Assert.True(array.Clockwise);
            Assert.False(array.Rotate);
            Near(120.0 * Math.PI / 180.0 / 7.0, array.Between);
            Assert.Equal(32, items.Count);

            // AutoCAD 2021: the second item of the first row at (60.28192, -8.129582), the row beyond it 10 further out.
            CadArrayItem second = items.Single(i => i.Index == 1 && i.Row == 0 && i.Level == 0);
            At(second, 60.28192, -8.129582);
            At(items.Single(i => i.Index == 1 && i.Row == 1 && i.Level == 1), 70.192207, -9.466078, 7);
            Near(1.0, second.Matrix[0, 0]);
        }

        [Fact]
        public void ArrayPathAssociativeYes_KeepsThePathAndAddsADependencyOnIt()
        {
            var database = new DxfDatabase("AC1027");
            var source = new DxfCircle(new GeoPoint3(0, 5, 0), new GeoVector3(0, 0, 1), 2);
            var path = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            database.ModelSpace.AppendEntity(source);
            database.ModelSpace.AppendEntity(path);
            CadDocument document = CadDocument.From(database, "path.dxf");

            Start(document, "ARRAYPATH", new[] { source.Handle }, Enter, P(50, 0), "AS", "Y", "I", "20", Enter, "X");

            Assert.Single(All<DxfLine>(document.ModelSpace));
            CadAssocArray array = Read(document, out List<CadArrayItem> items);
            Assert.Equal(CadArrayKind.Path, array.Kind);
            Assert.Equal(6, items.Count);
            Near(20.0, array.Spacing);
            At(items[0], 0, 0);
            At(items[5], 100, 0);
            Assert.Equal(4, Count(document, "ACDBASSOCDEPENDENCY"));
            Assert.Equal(1, Count(document, "ACDBASSOCEDGEACTIONPARAM"));
            Assert.Same(
                document.FindEntity(path.Handle),
                document.FindEntity(CadArrays.Find(document.Database, TheArray(document)).PathHandle)
            );
        }

        [Fact]
        public void ArrayPathAlongAnArcFromItsEnd_TurnsTheItemsWithThePath()
        {
            var database = new DxfDatabase("AC1027");
            var source = new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 2);
            var arc = new DxfArc(new GeoPoint3(50, 0, 0), new GeoVector3(0, 0, 1), 50, 0, Math.PI);
            database.ModelSpace.AppendEntity(source);
            database.ModelSpace.AppendEntity(arc);
            CadDocument document = CadDocument.From(database, "arc.dxf");

            Start(document, "ARRAYPATH", new[] { source.Handle }, Enter, P(50, 50), "AS", "Y", "M", "D", "I", "7", "X");

            CadAssocArray array = Read(document, out List<CadArrayItem> items);
            Assert.True(array.Divide);
            Assert.Equal(7, items.Count);

            // AutoCAD 2021 for the same: the middle item at the top of the arc with its X axis along the path.
            At(items[3], 50, 50);
            Near(1.0, items[3].Matrix[0, 0]);
            At(items[1], 6.69873, 25);
            Near(0.5, items[1].Matrix[0, 0]);
        }

        [Fact]
        public void AnAssociativeArrayInADrawingOlderThanR2010_IsRefused_AndTheItemsAreMadeAsCopies()
        {
            var database = new DxfDatabase("AC1015");
            var circle = new DxfCircle(new GeoPoint3(10, 10, 0), new GeoVector3(0, 0, 1), 5);
            database.ModelSpace.AppendEntity(circle);
            CadDocument document = CadDocument.From(database, "old.dxf");

            CadCommandSession session = Start(document, "ARRAYRECT", new[] { circle.Handle }, Enter, "AS", "Y");
            Assert.Contains(
                "Associative arrays need a drawing of R2010 or later: the items are made as separate objects.",
                session.History
            );
            Run(session, "COU", "2", "2", "S", "20", "30", "X");
            Assert.Equal(4, All<DxfCircle>(document.ModelSpace).Count);
        }

        [Fact]
        public void ArrayEditOfARectangularArray_ChangesTheColumnsRowsAndLevels_OneUndoStepEach()
        {
            CadDocument document = Rectangular(out _);
            string handle = TheArray(document).Handle;
            string start = document.Database.ToText();

            CadCommandSession session = Start(document, "ARRAYEDIT");
            Assert.Equal("Select array:", session.PromptText);
            Run(session, new object[] { new[] { handle } });
            Assert.Equal(
                "Enter an option [Source/REPlace/Base point/Rows/Columns/Levels/RESet/eXit] <eXit>:",
                session.PromptText
            );
            Run(session, "COL");
            Assert.Equal("Enter the number of columns or [Expression] <3>:", session.PromptText);
            Run(session, "5");
            Assert.Equal("Specify the distance between columns or [Total/Expression] <20.0000>:", session.PromptText);
            Run(session, "40", "ROW", "2", "25", "3", "L", "2", "7", "X");

            CadAssocArray array = Read(document, out List<CadArrayItem> items);
            Assert.Equal(5, array.Columns);
            Near(40.0, array.ColumnSpacing);
            Assert.Equal(2, array.Rows);
            Near(25.0, array.RowSpacing);
            Near(3.0, array.RowElevation);
            Assert.Equal(2, array.Levels);

            // AutoCAD 2021's ARRAYEDIT of the same: 5 columns at 0, 40, 80, 120 and 160, the second row at (0, 25, 3).
            Assert.Equal(20, items.Count);
            At(items.Single(i => i.Index == 4 && i.Row == 0 && i.Level == 0), 160, 0);
            At(items.Single(i => i.Index == 1 && i.Row == 1 && i.Level == 1), 40, 25, 10);

            // The INSERT is the same one with its handle, and the network holds the one action still.
            Assert.Equal(handle, TheArray(document).Handle);
            Assert.Equal(1, Count(document, "ACDBASSOCACTION"));
            Assert.Equal(1, Count(document, "ACDBASSOCARRAYACTIONBODY"));
            Assert.Equal("ARRAYEDIT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void ArrayEditBasePointOfARectangularArray_MovesTheInsertAndKeepsTheItemsWhereTheyAre()
        {
            CadDocument document = Rectangular(out _);
            string handle = TheArray(document).Handle;
            DxfBlockReference insert = TheArray(document);
            GeoAabb3 before = TheBox(document);

            Start(document, "ARRAYEDIT", new[] { handle }, "B", P(50, 50), "X");

            insert = TheArray(document);
            Near(50.0, insert.Position.X);
            Near(50.0, insert.Position.Y);
            GeoAabb3 after = TheBox(document);
            Near(before.Min.X, after.Min.X);
            Near(before.Max.Y, after.Max.Y);
            DxfCircle source = Assert.IsType<DxfCircle>(CadArrays.SourcesOf(insert).Single());
            Near(-40.0, source.Center.X);
            Near(-40.0, source.Center.Y);
        }

        /// <summary>The box of every item's circle in the drawing: where the items stand.</summary>
        private static GeoAabb3 TheBox(CadDocument document)
        {
            DxfBlockReference insert = TheArray(document);
            CadAssocArray array = Read(document, out List<CadArrayItem> items);
            DxfCircle source = Assert.IsType<DxfCircle>(CadArrays.SourcesOf(insert).Single());
            GeoAabb3 box = GeoAabb3.Empty;

            foreach (CadArrayItem item in items)
            {
                GeoPoint3 centre = insert.BlockTransform.Transform(item.Matrix.Transform(source.Center));
                var one = new GeoAabb3(
                    new GeoPoint3(centre.X - 5, centre.Y - 5, 0),
                    new GeoPoint3(centre.X + 5, centre.Y + 5, 0)
                );
                box = box.IsEmpty ? one : box.Union(one);
            }

            Assert.NotNull(array);
            return box;
        }

        [Fact]
        public void ArrayEditOfAPolarArray_ChangesItemsAngleFillAndRotation()
        {
            CadDocument document = Circle(60, 10, 5, out string circle);
            Start(document, "ARRAYPOLAR", new[] { circle }, Enter, P(0, 0), "AS", "Y", "X");
            string handle = TheArray(document).Handle;

            CadCommandSession session = Start(document, "ARRAYEDIT", new[] { handle });
            Assert.Equal(
                "Enter an option [Source/REPlace/Base point/Items/Angle between/Fill angle/Rows/Levels/ROTate items/RESet/eXit] <eXit>:",
                session.PromptText
            );
            Run(session, "I", "8", "A", "20", "F", "120", "ROT", "N", "X");

            CadAssocArray array = Read(document, out List<CadArrayItem> items);
            Assert.Equal(8, array.Items);
            Near(120.0 * Math.PI / 180.0 / 7.0, array.Between);
            Assert.False(array.Rotate);
            Assert.Equal(8, items.Count);

            // AutoCAD 2021 gave an angle between of 17.142857 degrees for 8 items filling 120.
            Near(17.142857, array.Between * 180.0 / Math.PI);
        }

        [Fact]
        public void ArrayEditOfAPathArray_ChangesMethodItemsAndAlignment()
        {
            var database = new DxfDatabase("AC1027");
            var source = new DxfCircle(new GeoPoint3(0, 5, 0), new GeoVector3(0, 0, 1), 2);
            var path = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            database.ModelSpace.AppendEntity(source);
            database.ModelSpace.AppendEntity(path);
            CadDocument document = CadDocument.From(database, "path.dxf");
            Start(document, "ARRAYPATH", new[] { source.Handle }, Enter, P(50, 0), "AS", "Y", "I", "5", "15", "X");
            string handle = TheArray(document).Handle;

            CadCommandSession session = Start(document, "ARRAYEDIT", new[] { handle });
            Assert.Equal(
                "Enter an option [Source/REPlace/Method/Base point/Items/Rows/Levels/Align items/Z direction/RESet/eXit] <eXit>:",
                session.PromptText
            );
            Run(session, "M", "D", "I", "6", "A", "N", "X");

            CadAssocArray array = Read(document, out List<CadArrayItem> items);
            Assert.True(array.Divide);
            Assert.False(array.Align);
            Assert.Equal(6, items.Count);

            // AutoCAD 2021: 6 items 20 apart along a path of 100, the last at its end.
            At(items[1], 20, 0);
            At(items[5], 100, 0);
        }

        [Fact]
        public void AnArrayOfMoreThanOneHundredThousandItems_IsRefused_AndTheCommandIsTakenBack()
        {
            CadDocument document = Rectangular(out _);
            string handle = TheArray(document).Handle;
            string start = document.Database.ToText();

            // 3 columns by 400 rows would be fine; 400 columns by 400 rows are 160 000 items.
            CadCommandSession session = Start(
                document,
                "ARRAYEDIT",
                new[] { handle },
                "COL",
                "400",
                Enter,
                "ROW",
                "400",
                Enter,
                Enter
            );

            Assert.Contains("An array of more than 100000 items is too large.", session.History);
            Assert.Equal(
                "Enter an option [Source/REPlace/Base point/Rows/Columns/Levels/RESet/eXit] <eXit>:",
                session.PromptText
            );
            Run(session, "X");

            // The edit that failed takes the command's earlier edits back with it, as AutoCAD takes a command back.
            Assert.Equal(start, document.Database.ToText());
            Assert.Equal(3, Read(document, out _).Columns);
        }

        [Fact]
        public void ArrayEditOfAnObjectThatIsNoArray_SaysSo_AndAsksAgain()
        {
            CadDocument document = Circle(10, 10, 5, out string circle);
            CadCommandSession session = Start(document, "ARRAYEDIT", new[] { circle });
            Assert.Contains("Object selected is not an associative array.", session.History);
            Assert.Contains("1 was filtered out", session.History);
            Assert.Equal("Select array:", session.PromptText);
        }

        [Fact]
        public void ArrayEditReplaceSourceObjects_PutsTheReplacementInEveryItem()
        {
            CadDocument document = Rectangular(out _);
            string handle = TheArray(document).Handle;
            Start(document, "CIRCLE", P(100, 100), "3");
            DxfCircle replacement = All<DxfCircle>(document.ModelSpace).Single();

            Start(
                document,
                "ARRAYEDIT",
                new[] { handle },
                "REP",
                new[] { replacement.Handle },
                Enter,
                P(100, 100),
                "S",
                "X"
            );

            Assert.Empty(All<DxfCircle>(document.ModelSpace));
            DxfCircle source = Assert.IsType<DxfCircle>(CadArrays.SourcesOf(TheArray(document)).Single());
            Near(3.0, source.Radius);
            Near(0.0, source.Center.X);
        }

        [Fact]
        public void ExplodingAnArray_GivesTheSourceObjectOfEachItem_AndTakesTheObjectsOutOfTheNetwork()
        {
            CadDocument document = Rectangular(out _);
            string handle = TheArray(document).Handle;
            string start = document.Database.ToText();

            Start(document, "EXPLODE", new[] { handle }, Enter);

            Assert.Empty(All<DxfBlockReference>(document.ModelSpace));
            List<DxfCircle> circles = All<DxfCircle>(document.ModelSpace);
            Assert.Equal(12, circles.Count);
            Assert.Contains(circles, c => Math.Abs(c.Center.X - 50) < 1E-6 && Math.Abs(c.Center.Y - 100) < 1E-6);
            Assert.Contains(circles, c => Math.Abs(c.Center.X - 10) < 1E-6 && Math.Abs(c.Center.Y - 10) < 1E-6);
            Assert.Equal(0, Count(document, "ACDBASSOCACTION"));
            Assert.Equal(0, Count(document, "ACDBASSOCARRAYACTIONBODY"));
            Assert.Equal(0, Count(document, "ACDBASSOCDEPENDENCY"));
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void ErasingAnArray_TakesItsActionOutOfTheNetwork()
        {
            CadDocument document = Rectangular(out _);
            string handle = TheArray(document).Handle;

            Start(document, "ERASE", new[] { handle }, Enter);

            Assert.Empty(All<DxfBlockReference>(document.ModelSpace));
            Assert.Equal(0, Count(document, "ACDBASSOCACTION"));
            Assert.Equal(0, Count(document, "ACDBASSOCARRAYACTIONBODY"));
        }

        [Fact]
        public void MovingAnArray_KeepsItAssociative_AndItsMatrixInTheBodyFollows()
        {
            CadDocument document = Rectangular(out _);
            string handle = TheArray(document).Handle;

            Start(document, "MOVE", new[] { handle }, Enter, P(0, 0), P(100, 50));

            DxfBlockReference insert = TheArray(document);
            Near(110.0, insert.Position.X);
            Near(60.0, insert.Position.Y);
            CadArrays.Record record = CadArrays.Find(document.Database, insert);
            Assert.NotNull(record);
            // The 16 numbers that end the body are the INSERT's matrix by rows.
            GeoTransform3 kept = CadArrays.StoredTransform(record);
            Near(110.0, kept[0, 3]);
            Near(60.0, kept[1, 3]);
            Near(1.0, kept[3, 3]);
        }

        [Fact]
        public void CopyingAnArray_MakesAnArrayOfItsOwn_AChangeOfTheOneLeavesTheOther()
        {
            CadDocument document = Rectangular(out _);
            string handle = TheArray(document).Handle;

            Start(document, "COPY", new[] { handle }, Enter, P(0, 0), P(200, 0), Enter);
            List<DxfBlockReference> both = All<DxfBlockReference>(document.ModelSpace);
            Assert.Equal(2, both.Count);
            Assert.Equal(2, Count(document, "ACDBASSOCARRAYACTIONBODY"));
            Assert.NotEqual(both[0].Name, both[1].Name);

            Start(document, "ARRAYEDIT", new[] { handle }, "COL", "5", Enter, "X");
            CadArrays.Record first = CadArrays.Find(document.Database, document.FindEntity(handle));
            CadArrays.Record second = CadArrays.Find(
                document.Database,
                document.FindEntity(both.Single(b => b.Handle != handle).Handle)
            );
            Assert.Equal(5, CadArrays.ReadArray(document, first, out _).Columns);
            Assert.Equal(3, CadArrays.ReadArray(document, second, out _).Columns);
        }

        [Fact]
        public void AnArrayThroughDwg_KeepsItsPicture_ItsObjectsOfTheNetworkNotBeingWrittenToDwgYet()
        {
            CadDocument document = Rectangular(out _);
            string path = Path.Combine(Path.GetTempPath(), "array-" + Guid.NewGuid().ToString("N") + ".dwg");

            try
            {
                DwgWriter.Write(document.Database, path);
                DxfDatabase back = DwgReader.Open(path);
                DxfBlockReference insert = back.ModelSpace.OfType<DxfBlockReference>().Single();
                Assert.Equal(12, insert.BlockTableRecord.OfType<DxfBlockReference>().Count());
                DxfBlockReference first = insert.BlockTableRecord.OfType<DxfBlockReference>().First();
                Assert.IsType<DxfCircle>(first.BlockTableRecord.Single());
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}

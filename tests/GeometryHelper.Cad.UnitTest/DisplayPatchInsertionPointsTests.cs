using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.TestDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The insertion points of the entities of a display list that is patched after an edit: the points of the entities left as
    /// they were come with the older list whole, and those of an entity drawn again, erased or hidden are taken out as the
    /// patch comes to it, so that each handle has the points a REGEN of the drawing gives it, and the runs of the entities that
    /// kept some say so.
    /// </summary>
    public class DisplayPatchInsertionPointsTests
    {
        private static string Bits(double d) =>
            BitConverter.DoubleToInt64Bits(d).ToString("X16", CultureInfo.InvariantCulture);

        private static readonly string[] Handles = { "A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8", "A9", "ZZ" };

        /// <summary>
        /// Lines A1 and A2, texts A3 and A4 on Steel and Notes, an INSERT A5 of a plain block, an INSERT A6 of the block that
        /// follows its attributes, A7 with two ATTRIBs and A8, an INSERT that nothing but a text in a block stands for, and
        /// a text A9 on Bolts.
        /// </summary>
        private static CadDocument Plan()
        {
            string layers = Layer("Steel", 1) + Layer("Notes", 2) + Layer("Bolts", 3);
            string blocks =
                Block("PLATE", Line(0, 0, 10, 0), Circle(5, 5, 2), Text(1, 1, 2, "IN BLOCK"))
                + Block("TAGGED", Line(0, 0, 4, 4));
            string tagged =
                "0\nINSERT\n5\nA7\n8\nSteel\n66\n1\n2\nTAGGED\n10\n60\n20\n10\n30\n0\n"
                + "0\nATTRIB\n5\nB1\n8\nSteel\n10\n62\n20\n12\n30\n0\n40\n2\n1\nONE\n2\nT1\n70\n0\n"
                + "0\nATTRIB\n5\nB2\n8\nSteel\n10\n64\n20\n14\n30\n0\n40\n2\n1\nTWO\n2\nT2\n70\n0\n"
                + "0\nSEQEND\n5\nB3\n8\nSteel\n";
            DxfDatabase database = Drawing(
                layers,
                blocks,
                Line(0, 0, 100, 0, "Steel", handle: "A1"),
                Line(0, 10, 100, 10, "Notes", handle: "A2"),
                Text(0, 80, 5, "PLAN", "Steel", handle: "A3"),
                Text(30, 80, 5, "NOTE", "Notes", handle: "A4"),
                Insert("PLATE", 200, 0, 2.0, 30.0, "Steel", handle: "A5"),
                Insert("TAGGED", 40, 40, 1.0, 0.0, "Notes", handle: "A6"),
                tagged,
                Insert("PLATE", 300, 0, 1.0, 0.0, "Bolts", handle: "A8"),
                Text(0, 120, 5, "BOLTS", "Bolts", handle: "A9")
            );
            return CadDocument.From(database, "plan.dxf");
        }

        /// <summary>The points of every handle, each to the bit, in the order the list keeps them.</summary>
        private static List<string> Points(DisplayList2 list) =>
            Handles
                .Select(h =>
                    h + ": " + string.Join(" ", list.InsertionPointsOf(h).Select(p => Bits(p.X) + "," + Bits(p.Y)))
                )
                .ToList();

        /// <summary>The points of the list as it stands, then those of the list a REGEN makes: the two must be one.</summary>
        private static List<string> AssertSameAsRegen(CadDocument document)
        {
            List<string> patched = Points(document.GetDisplay(document.ModelSpace));
            document.Regen();
            List<string> whole = Points(document.GetDisplay(document.ModelSpace));

            Assert.Equal(whole, patched);
            return patched;
        }

        private static void Move(CadDocument document, params string[] handles)
        {
            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(7, 3, 0));
            Assert.True(
                document
                    .ApplyEdit(
                        "MOVE",
                        handles.Select(h => new CadEditRecord(CadEditRecordKind.Transformed, h)).ToList(),
                        s =>
                        {
                            foreach (string handle in handles)
                            {
                                s.Document.FindEntity(handle).TransformBy(move);
                            }
                        }
                    )
                    .Succeeded
            );
        }

        [Fact]
        public void APlanDrawnWhole_KeepsThePointsOfItsTextsItsInsertsAndTheirAttributes()
        {
            CadDocument document = Plan();
            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            Assert.Empty(list.InsertionPointsOf("A1"));
            Assert.Empty(list.InsertionPointsOf("A2"));
            Assert.Single(list.InsertionPointsOf("A3"));
            Assert.Single(list.InsertionPointsOf("A4"));
            Assert.Single(list.InsertionPointsOf("A5"));
            Assert.Single(list.InsertionPointsOf("A8"));
            Assert.Single(list.InsertionPointsOf("A9"));
            Assert.Empty(list.InsertionPointsOf("ZZ"));
        }

        [Fact]
        public void TheRunsOfTheEntitiesThatKeptAPoint_SayTheyDid()
        {
            CadDocument document = Plan();
            DisplayList2 list = document.GetDisplay(document.ModelSpace);
            var inserted = new Dictionary<string, bool>();

            foreach (DisplayRun run in list.Runs)
            {
                inserted[run.Handle] = run.Inserted;
            }

            Assert.False(inserted["A1"]);
            Assert.False(inserted["A2"]);
            Assert.True(inserted["A3"]);
            Assert.True(inserted["A4"]);
            Assert.True(inserted["A5"]);
            Assert.True(inserted["A8"]);
            Assert.True(inserted["A9"]);
        }

        [Fact]
        public void AMoveOfATextAndAnInsert_LeavesThePointsARegenMakes()
        {
            CadDocument document = Plan();
            List<string> first = Points(document.GetDisplay(document.ModelSpace));

            Move(document, "A3", "A5", "A1");
            List<string> after = AssertSameAsRegen(document);

            Assert.NotEqual(first, after);
        }

        [Fact]
        public void AMoveOfInsertsWithAttributes_LeavesThePointsARegenMakes()
        {
            CadDocument document = Plan();
            document.GetDisplay(document.ModelSpace);

            Move(document, "A6", "A7", "A4");
            AssertSameAsRegen(document);
        }

        [Fact]
        public void AnEraseOfATextAndAnInsert_LeavesNoPointsOfThem()
        {
            CadDocument document = Plan();
            document.GetDisplay(document.ModelSpace);

            Assert.True(
                document
                    .ApplyEdit(
                        "ERASE",
                        new[]
                        {
                            new CadEditRecord(CadEditRecordKind.Erased, "A3"),
                            new CadEditRecord(CadEditRecordKind.Erased, "A5"),
                        },
                        s =>
                        {
                            s.Document.FindEntity("A3").Erase();
                            s.Document.FindEntity("A5").Erase();
                        }
                    )
                    .Succeeded
            );
            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            Assert.Empty(list.InsertionPointsOf("A3"));
            Assert.Empty(list.InsertionPointsOf("A5"));
            Assert.Single(list.InsertionPointsOf("A4"));
            AssertSameAsRegen(document);
        }

        [Fact]
        public void AnAddedText_HasItsPoint_AndTheOthersKeepTheirs()
        {
            CadDocument document = Plan();
            document.GetDisplay(document.ModelSpace);
            string added = null;

            Assert.True(
                document
                    .ApplyEdit("TEXT", null, s => added = s.Append(
                            s.Document.ModelSpace,
                            new DxfDBText
                            {
                                Position = new GeoPoint3(55, 66, 0),
                                Height = 3.0,
                                TextString = "NEW",
                            }
                        ).Handle)
                    .Succeeded
            );
            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            Assert.Equal(new GeoPoint2(55, 66), Assert.Single(list.InsertionPointsOf(added)));
            Assert.Single(list.InsertionPointsOf("A3"));
        }

        [Fact]
        public void ALayerSwitchedOffAndOn_LeavesThePointsARegenMakes_OfTheEntitiesOnIt()
        {
            CadDocument document = Plan();
            document.GetDisplay(document.ModelSpace);

            CadLayerEditor.SetOn(document, "Steel", false);
            AssertSameAsRegen(document);
            CadLayerEditor.SetOn(document, "Steel", true);
            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            Assert.Single(list.InsertionPointsOf("A3"));
            Assert.Single(list.InsertionPointsOf("A5"));
            AssertSameAsRegen(document);
        }

        [Fact]
        public void TheOlderList_KeepsItsPoints_AfterAPatchThatChangesThem()
        {
            // The patched list shares the older list's table until it changes one; the older list is never the changed.
            CadDocument document = Plan();
            DisplayList2 older = document.GetDisplay(document.ModelSpace);
            List<string> kept = Points(older);

            Move(document, "A3", "A6");
            DisplayList2 patched = document.GetDisplay(document.ModelSpace);

            Assert.NotSame(older, patched);
            Assert.Equal(kept, Points(older));
            Assert.NotEqual(kept, Points(patched));

            // A patch that moves no entity with a point shares the table and so has the same points.
            Move(document, "A1");
            DisplayList2 again = document.GetDisplay(document.ModelSpace);

            Assert.NotSame(patched, again);
            Assert.Equal(Points(patched), Points(again));
            AssertSameAsRegen(document);
        }

        [Fact]
        public void AnUndoAndARedo_LeaveThePointsARegenMakes()
        {
            CadDocument document = Plan();
            List<string> first = Points(document.GetDisplay(document.ModelSpace));
            Move(document, "A3", "A4", "A5", "A6");
            List<string> moved = Points(document.GetDisplay(document.ModelSpace));

            Assert.True(document.Undo());
            Assert.Equal(first, Points(document.GetDisplay(document.ModelSpace)));
            Assert.True(document.Redo());
            Assert.Equal(moved, Points(document.GetDisplay(document.ModelSpace)));
            AssertSameAsRegen(document);
        }

        [Fact]
        public void ManyEditsInARow_LeaveThePointsARegenMakes_EachTime()
        {
            CadDocument document = Plan();
            document.GetDisplay(document.ModelSpace);

            foreach (
                string[] handles in new[]
                {
                    new[] { "A3" },
                    new[] { "A5", "A9" },
                    new[] { "A7", "A1", "A4" },
                    new[] { "A8" },
                }
            )
            {
                Move(document, handles);
                document.GetDisplay(document.ModelSpace);
            }

            AssertSameAsRegen(document);
        }
    }
}

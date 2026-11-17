using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest;
using Xunit;
using static GeometryHelper.Cad.UnitTest.TestDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// A display list patched after an edit takes the runs the edit left alone by stretches, and finds the entities it drew
    /// again by their places where the entities of the space are those the list was made from. Each test checks the patched
    /// list, its box and the insertion points of every entity against a drawing that gets the same edits and is regenerated
    /// after each of them.
    /// </summary>
    public class DisplayPatchPlacesTests
    {
        private static CadDocument Open(string text) =>
            CadDocument.Open(new MemoryStream(new UTF8Encoding(false).GetBytes(text)), "places.dxf");

        private static string[] HandlesOf(CadDocument document) =>
            document.ModelSpace.Block.Select(e => e.Handle).Where(h => h != null).ToArray();

        private static string Bits(double d) =>
            BitConverter.DoubleToInt64Bits(d).ToString("X16", CultureInfo.InvariantCulture);

        /// <summary>Every item, the box of the list to the bit, and the insertion points of every entity.</summary>
        private static List<string> State(CadDocument document)
        {
            DisplayList2 list = document.GetDisplay(document.ModelSpace);
            List<string> state = DisplayPatchTests.Describe(list);
            state.Add(
                "box "
                    + Bits(list.Bounds.MinX)
                    + " "
                    + Bits(list.Bounds.MinY)
                    + " "
                    + Bits(list.Bounds.MaxX)
                    + " "
                    + Bits(list.Bounds.MaxY)
            );

            foreach (string handle in HandlesOf(document))
            {
                state.Add(
                    handle
                        + ": "
                        + string.Join(" ", list.InsertionPointsOf(handle).Select(p => Bits(p.X) + "," + Bits(p.Y)))
                );
            }

            return state;
        }

        /// <summary>The state of the drawing as it is patched, then that of a drawing made whole from the same records.</summary>
        private static void AssertSameAsRegen(CadDocument patched, CadDocument whole, string step)
        {
            whole.Regen();
            List<string> expected = State(whole);
            List<string> got = State(patched);

            if (expected.Count != got.Count)
            {
                Assert.Fail(step + ": " + got.Count + " lines where a REGEN gives " + expected.Count);
            }

            for (int i = 0; i < expected.Count; i++)
            {
                if (!string.Equals(expected[i], got[i], StringComparison.Ordinal))
                {
                    Assert.Fail(step + ": line " + i + "\n  patched " + got[i] + "\n  regen   " + expected[i]);
                }
            }
        }

        private static void Both(CadDocument a, CadDocument b, Action<CadDocument> edit)
        {
            edit(a);
            edit(b);
        }

        private static void Move(CadDocument document, string[] handles, GeoVector3 by)
        {
            Assert.True(
                document
                    .ApplyEdit(
                        "MOVE",
                        handles.Select(h => new CadEditRecord(CadEditRecordKind.Transformed, h)).ToList(),
                        s =>
                        {
                            foreach (string handle in handles)
                            {
                                s.Document.FindEntity(handle).TransformBy(GeoTransform3.Translation(by));
                            }
                        }
                    )
                    .Succeeded
            );
        }

        private static void Erase(CadDocument document, string[] handles)
        {
            Assert.True(
                document
                    .ApplyEdit(
                        "ERASE",
                        handles.Select(h => new CadEditRecord(CadEditRecordKind.Erased, h)).ToList(),
                        s =>
                        {
                            foreach (string handle in handles)
                            {
                                s.Document.FindEntity(handle).Erase();
                            }
                        }
                    )
                    .Succeeded
            );
        }

        private static void Copy(CadDocument document, string[] handles, GeoVector3 by)
        {
            Assert.True(
                document
                    .ApplyEdit("COPY", null, s =>
                    {
                        foreach (string handle in handles)
                        {
                            s.Append(
                                s.Document.ModelSpace,
                                s.Document.FindEntity(handle).GetTransformedCopy(GeoTransform3.Translation(by))
                            );
                        }
                    })
                    .Succeeded
            );
        }

        /// <summary>
        /// A drawing of 600 entities, lines, arcs, circles, polylines, texts and INSERTs on three layers, edited 45 times at
        /// random from a fixed seed, by a move of one to four entities, an erase, a copy, a layer switched off and on and an undo
        /// and redo, each time checked against a drawing regenerated after the same edit.
        /// </summary>
        [Theory]
        [InlineData(7)]
        [InlineData(2024)]
        public void FortyFiveRandomEdits_LeaveTheListARegenMakes(int seed)
        {
            string text = BigDrawing.Text(600);
            CadDocument patched = Open(text);
            CadDocument whole = Open(text);
            patched.GetDisplay(patched.ModelSpace);
            var random = new Random(seed);
            string[] layers = { "Steel", "Notes", "Bolts" };
            var off = new HashSet<string>();
            int edits = 0,
                inPlace = 0;

            for (int step = 0; step < 45; step++)
            {
                string[] handles = HandlesOf(patched);
                string what;
                int kind = random.Next(10);

                if (kind < 4)
                {
                    string[] some = Enumerable
                        .Range(0, 1 + random.Next(4))
                        .Select(_ => handles[random.Next(handles.Length)])
                        .Distinct()
                        .ToArray();
                    var by = new GeoVector3(random.Next(-40, 40), random.Next(-40, 40), 0);
                    what = "move " + string.Join(",", some);
                    Both(patched, whole, d => Move(d, some, by));
                }
                else if (kind < 6)
                {
                    string[] some = Enumerable
                        .Range(0, 1 + random.Next(3))
                        .Select(_ => handles[random.Next(handles.Length)])
                        .Distinct()
                        .ToArray();
                    what = "erase " + string.Join(",", some);
                    Both(patched, whole, d => Erase(d, some));
                }
                else if (kind < 8)
                {
                    string[] some = Enumerable
                        .Range(0, 1 + random.Next(3))
                        .Select(_ => handles[random.Next(handles.Length)])
                        .ToArray();
                    var by = new GeoVector3(random.Next(-90, 90), random.Next(-90, 90), 0);
                    what = "copy " + string.Join(",", some);
                    Both(patched, whole, d => Copy(d, some, by));
                }
                else if (kind < 9)
                {
                    string layer = layers[random.Next(layers.Length)];
                    bool on = off.Contains(layer);
                    what = "layer " + layer + (on ? " on" : " off");
                    Both(patched, whole, d => Assert.True(CadLayerEditor.SetOn(d, layer, on).Succeeded));

                    if (on)
                    {
                        off.Remove(layer);
                    }
                    else
                    {
                        off.Add(layer);
                    }
                }
                else
                {
                    what = random.Next(2) == 0 ? "undo" : "redo";
                    bool undo = what == "undo";
                    Both(patched, whole, d => _ = undo ? d.Undo() : d.Redo());
                }

                if (kind < 9)
                {
                    edits++;
                    inPlace += patched.GetDisplay(patched.ModelSpace).PatchedInPlace ? 1 : 0;
                }

                AssertSameAsRegen(patched, whole, "step " + step + " " + what);
            }

            // The edits that were not an undo or a redo were mostly patched by place, not by a walk.
            Assert.InRange(inPlace, edits / 2, edits);
        }

        [Fact]
        public void AMoveOfTheEntityThatSetsTheBox_ShrinksTheBoxAsARegenDoes()
        {
            string layers = Layer("Steel", 1);
            string text = DxfOf(
                layers,
                Line(0, 0, 10, 10, "Steel", handle: "A1"),
                Line(2, 2, 90, 40, "Steel", handle: "A2"),
                Line(5, 1, 20, 8, "Steel", handle: "A3")
            );
            CadDocument patched = Open(text);
            CadDocument whole = Open(text);
            DisplayList2 first = patched.GetDisplay(patched.ModelSpace);
            Assert.Equal(90.0, first.Bounds.MaxX, 9);

            Both(patched, whole, d => Move(d, new[] { "A2" }, new GeoVector3(-60, -30, 0)));
            DisplayList2 after = patched.GetDisplay(patched.ModelSpace);

            Assert.Equal(30.0, after.Bounds.MaxX, 9);
            AssertSameAsRegen(patched, whole, "box shrunk");
        }

        [Fact]
        public void AMoveOfAnEntityInsideTheBox_LeavesTheBoxToTheBit()
        {
            string text = DxfOf(
                Layer("Steel", 1),
                Line(0, 0, 100, 100, "Steel", handle: "A1"),
                Line(20, 20, 30, 30, "Steel", handle: "A2"),
                Line(40, 10, 60, 20, "Steel", handle: "A3")
            );
            CadDocument patched = Open(text);
            CadDocument whole = Open(text);
            DisplayBox before = patched.GetDisplay(patched.ModelSpace).Bounds;

            Both(patched, whole, d => Move(d, new[] { "A2" }, new GeoVector3(15, 25, 0)));
            DisplayBox after = patched.GetDisplay(patched.ModelSpace).Bounds;

            Assert.Equal(Bits(before.MinX), Bits(after.MinX));
            Assert.Equal(Bits(before.MaxY), Bits(after.MaxY));
            AssertSameAsRegen(patched, whole, "inner move");
        }

        [Fact]
        public void ALineMovedOutBeyondTheBox_GrowsTheBoxAsARegenDoes()
        {
            string text = DxfOf(
                Layer("Steel", 1),
                Line(0, 0, 100, 100, "Steel", handle: "A1"),
                Line(20, 20, 30, 30, "Steel", handle: "A2")
            );
            CadDocument patched = Open(text);
            CadDocument whole = Open(text);
            patched.GetDisplay(patched.ModelSpace);

            Both(patched, whole, d => Move(d, new[] { "A2" }, new GeoVector3(-50, 400, 0)));

            Assert.Equal(430.0, patched.GetDisplay(patched.ModelSpace).Bounds.MaxY, 9);
            AssertSameAsRegen(patched, whole, "grown");
        }

        [Fact]
        public void LinesWhoseBoxSidesAreZeroOfEitherSign_KeepTheSignARegenGives()
        {
            // The box of a list is folded from its items' with Math.Min and Math.Max: where a side is a zero of either sign, which
            // of the two it keeps follows the order of the fold, so a patch that does not fold the items again must not change it.
            string text = DxfOf(
                Layer("Steel", 1),
                Line(0, -0.0, 50, 50, "Steel", handle: "A1"),
                Line(-0.0, 10, 40, 40, "Steel", handle: "A2"),
                Line(0, 0, 30, 20, "Steel", handle: "A3"),
                Line(10, 10, 20, 20, "Steel", handle: "A4")
            );
            CadDocument patched = Open(text);
            CadDocument whole = Open(text);
            patched.GetDisplay(patched.ModelSpace);
            AssertSameAsRegen(patched, whole, "opened");

            Both(patched, whole, d => Move(d, new[] { "A4" }, new GeoVector3(5, 5, 0)));
            AssertSameAsRegen(patched, whole, "inner line moved");

            Both(patched, whole, d => Move(d, new[] { "A2" }, new GeoVector3(0, 3, 0)));
            AssertSameAsRegen(patched, whole, "zero-sided line moved");

            Both(patched, whole, d => Copy(d, new[] { "A3" }, new GeoVector3(0, 0, 0)));
            AssertSameAsRegen(patched, whole, "line copied in place");
        }

        [Fact]
        public void ALayerSwitchedOffAndOn_AfterAnEraseAndACopy_IsPatchedLikeARegen()
        {
            string text = BigDrawing.Text(400);
            CadDocument patched = Open(text);
            CadDocument whole = Open(text);
            patched.GetDisplay(patched.ModelSpace);
            string[] handles = HandlesOf(patched);

            Both(patched, whole, d => Erase(d, new[] { handles[10], handles[200] }));
            AssertSameAsRegen(patched, whole, "erased");

            Both(patched, whole, d => Copy(d, new[] { handles[11], handles[12] }, new GeoVector3(3, 4, 0)));
            AssertSameAsRegen(patched, whole, "copied");

            Both(patched, whole, d => Assert.True(CadLayerEditor.SetOn(d, "Steel", false).Succeeded));
            AssertSameAsRegen(patched, whole, "layer off");

            Both(patched, whole, d => Move(d, new[] { handles[300] }, new GeoVector3(-8, 9, 0)));
            AssertSameAsRegen(patched, whole, "moved with the layer off");

            Both(patched, whole, d => Assert.True(CadLayerEditor.SetOn(d, "Steel", true).Succeeded));
            AssertSameAsRegen(patched, whole, "layer on");
        }

        [Fact]
        public void AMoveAnEraseACopyAndALayer_AreEachPatchedByPlace_AndAnUndoByAWalk()
        {
            string text = BigDrawing.Text(900);
            CadDocument patched = Open(text);
            CadDocument whole = Open(text);
            patched.GetDisplay(patched.ModelSpace);
            string[] handles = HandlesOf(patched);

            Both(patched, whole, d => Move(d, new[] { handles[30], handles[31] }, new GeoVector3(4, 5, 0)));
            Assert.True(patched.GetDisplay(patched.ModelSpace).PatchedInPlace, "move");
            AssertSameAsRegen(patched, whole, "move");

            Both(patched, whole, d => Erase(d, new[] { handles[500] }));
            Assert.True(patched.GetDisplay(patched.ModelSpace).PatchedInPlace, "erase");
            AssertSameAsRegen(patched, whole, "erase");

            Both(patched, whole, d =>
                Copy(d, new[] { handles[40], handles[41], handles[42] }, new GeoVector3(7, 7, 0))
            );
            Assert.True(patched.GetDisplay(patched.ModelSpace).PatchedInPlace, "copy");
            AssertSameAsRegen(patched, whole, "copy");

            // An erase and a copy in one edit, then one of the entities added moved and an old one erased with the layer off.
            Both(patched, whole, d =>
                Assert.True(
                    d.ApplyEdit(
                        "ERASE AND COPY",
                        new[] { new CadEditRecord(CadEditRecordKind.Erased, handles[600]) },
                        s =>
                        {
                            s.Document.FindEntity(handles[600]).Erase();
                            s.Append(
                                s.Document.ModelSpace,
                                s.Document.FindEntity(handles[50])
                                    .GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(1, 1, 0)))
                            );
                        }
                    ).Succeeded
                )
            );
            Assert.True(patched.GetDisplay(patched.ModelSpace).PatchedInPlace, "erase and copy");
            AssertSameAsRegen(patched, whole, "erase and copy");

            string added = HandlesOf(patched).Last();
            Both(patched, whole, d => Move(d, new[] { added }, new GeoVector3(-3, 2, 0)));
            Assert.True(patched.GetDisplay(patched.ModelSpace).PatchedInPlace, "move of an entity added");
            AssertSameAsRegen(patched, whole, "move of an entity added");

            Both(patched, whole, d => Assert.True(CadLayerEditor.SetOn(d, "Bolts", false).Succeeded));
            Assert.True(patched.GetDisplay(patched.ModelSpace).PatchedInPlace, "layer");
            AssertSameAsRegen(patched, whole, "layer");

            Both(patched, whole, d => Assert.True(d.Undo()));
            Assert.False(patched.GetDisplay(patched.ModelSpace).PatchedInPlace, "undo");
            AssertSameAsRegen(patched, whole, "undo");

            Both(patched, whole, d => Move(d, new[] { handles[70] }, new GeoVector3(1, 0, 0)));
            Assert.True(patched.GetDisplay(patched.ModelSpace).PatchedInPlace, "move after an undo");
            AssertSameAsRegen(patched, whole, "move after an undo");
        }

        /// <summary>
        /// An edit that names more entities than the places are looked for of, or erases a long stretch of them, is patched by a
        /// walk of the entities: the runs of those erased are passed over a few at a time, and by the handle of every run where
        /// the stretch is longer than that.
        /// </summary>
        [Fact]
        public void ManyEntitiesMovedErasedAndCopied_ArePatchedByAWalk_LikeARegen()
        {
            string text = BigDrawing.Text(700);
            CadDocument patched = Open(text);
            CadDocument whole = Open(text);
            patched.GetDisplay(patched.ModelSpace);
            string[] handles = HandlesOf(patched);

            string[] many = Enumerable.Range(0, 30).Select(i => handles[i * 20]).ToArray();
            Both(patched, whole, d => Move(d, many, new GeoVector3(3, -2, 0)));
            Assert.False(patched.GetDisplay(patched.ModelSpace).PatchedInPlace, "thirty moved");
            AssertSameAsRegen(patched, whole, "thirty moved");

            string[] scattered = Enumerable.Range(0, 20).Select(i => handles[i * 25 + 3]).ToArray();
            Both(patched, whole, d => Erase(d, scattered));
            Assert.False(patched.GetDisplay(patched.ModelSpace).PatchedInPlace, "twenty erased");
            AssertSameAsRegen(patched, whole, "twenty erased");

            string[] alive = HandlesOf(patched);
            string[] stretch = Enumerable.Range(0, 60).Select(i => alive[300 + i]).ToArray();
            Both(patched, whole, d => Erase(d, stretch));
            AssertSameAsRegen(patched, whole, "sixty in a row erased");

            alive = HandlesOf(patched);
            string[] copied = Enumerable.Range(0, 25).Select(i => alive[100 + i * 3]).ToArray();
            Both(patched, whole, d => Copy(d, copied, new GeoVector3(-5, 5, 0)));
            Assert.True(patched.GetDisplay(patched.ModelSpace).PatchedInPlace, "twenty-five copied");
            AssertSameAsRegen(patched, whole, "twenty-five copied");

            Both(patched, whole, d => Move(d, new[] { alive[500] }, new GeoVector3(9, 9, 0)));
            Assert.True(patched.GetDisplay(patched.ModelSpace).PatchedInPlace, "one moved after the walks");
            AssertSameAsRegen(patched, whole, "one moved after the walks");
        }

        [Fact]
        public void ThePlacesOfTheRuns_FollowTheEntities_AcrossAnEraseInTheMiddleAndACopyAtTheEnd()
        {
            string text = BigDrawing.Text(600);
            CadDocument document = Open(text);
            DisplayList2 before = document.GetDisplay(document.ModelSpace);
            string[] handles = HandlesOf(document);
            DisplayItem2 kept = before.Items.First(i => i.Handle == handles[400]);

            Erase(document, new[] { handles[100] });
            Copy(document, new[] { handles[7] }, new GeoVector3(1, 2, 0));
            DisplayList2 after = document.GetDisplay(document.ModelSpace);

            Assert.DoesNotContain(after.Items, i => i.Handle == handles[100]);
            Assert.Same(kept, after.Items.First(i => i.Handle == handles[400]));
            Assert.Equal(HandlesOf(document).Length, after.Runs.Count);

            for (int run = 0; run < after.Runs.Count; run++)
            {
                DisplayRun r = after.Runs[run];

                for (int k = r.Start; k < r.Start + r.Count; k++)
                {
                    Assert.Equal(r.Handle, after.Items[k].Handle, StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        [Fact]
        public void TheItemsOfAList_AreReadByPlaceAndByWalk_AsTheyWereAdded()
        {
            var list = new DisplayList2();
            var style = new DisplayStyle(0xFF0000, 0.25, null);

            for (int i = 0; i < 40; i++)
            {
                list.Add(new DisplayPolyline2(new[] { i, 0.0, i + 1.0, 1.0 }, false, style, "H" + i, "0"));
            }

            Assert.Equal(40, list.Count);
            Assert.Equal(40, list.Items.Count);
            Assert.Equal("H17", list.Items[17].Handle);
            Assert.Equal(Enumerable.Range(0, 40).Select(i => "H" + i), list.Items.Select(i => i.Handle));
            Assert.Throws<ArgumentOutOfRangeException>(() => list.Items[40]);
            Assert.Throws<ArgumentOutOfRangeException>(() => list.Items[-1]);
            Assert.Equal(0.0, list.Bounds.MinX);
            Assert.Equal(40.0, list.Bounds.MaxX);
        }

        /// <summary>A drawing of lines on the given layers as DXF text, with the layer table and no blocks.</summary>
        private static string DxfOf(string layers, params string[] entities)
        {
            var stream = new MemoryStream();
            Drawing(layers, string.Empty, entities).Save(stream);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// A move in a drawing of 100 000 entities is displayed by a patch that finds the entity by its place, not by a walk of
        /// every entity: the limit is about ten times what the patch takes alone, and under what a walk of every entity takes.
        /// </summary>
        [Fact]
        public void AMoveInADrawingOfAHundredThousandEntities_IsDisplayedWithoutWalkingTheEntities()
        {
            CadDocument document = Open(BigDrawing.Text(100000));
            document.GetDisplay(document.ModelSpace);
            string handle = document.ModelSpace.Block.OfType<DxfLine>().First().Handle;
            int turn = 0;

            SpeedGuard.AssertWithin(
                0.1,
                () =>
                {
                    Move(document, new[] { handle }, new GeoVector3(++turn % 2 == 0 ? -5 : 5, 0, 0));

                    return document;
                },
                d => d.GetDisplay(d.ModelSpace),
                5
            );
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.TestDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// After an edit the display list of a space is patched, not made anew: the entities the edit names, and those that read
    /// a layer it changed, are drawn again, and every other entity keeps the items it had. Each test checks the patched list
    /// item for item against the list a REGEN makes from the drawing as it then stands.
    /// </summary>
    public class DisplayPatchTests
    {
        private static readonly Tolerance Exact = new Tolerance(1E-9, 1E-9);

        /// <summary>
        /// A plan on three layers: lines A1 and A2 on Steel, a circle A3 on Notes, a text A4 on Bolts, and an INSERT A5 on
        /// Steel of a block whose line on layer 0 takes the INSERT's layer, whose circle is on Notes, and which holds an INSERT
        /// of a second block with an arc on Bolts.
        /// </summary>
        private static CadDocument Plan()
        {
            string layers = Layer("Steel", 1) + Layer("Notes", 2) + Layer("Bolts", 3);
            string blocks =
                Block("INNER", Arc(0, 0, 4, 0, 180, "Bolts"))
                + Block("OUTER", Line(0, 0, 10, 0), Circle(5, 5, 3, "Notes"), Insert("INNER", 20, 0));
            DxfDatabase database = Drawing(
                layers,
                blocks,
                Line(0, 0, 100, 0, "Steel", handle: "A1"),
                Line(0, 10, 100, 10, "Steel", handle: "A2"),
                Circle(50, 50, 20, "Notes", handle: "A3"),
                Text(0, 80, 5, "PLAN", "Bolts", handle: "A4"),
                Insert("OUTER", 200, 0, 2.0, 30.0, "Steel", handle: "A5")
            );
            return CadDocument.From(database, "plan.dxf");
        }

        private static CadEditResult Move(CadDocument document, params string[] handles)
        {
            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(7, 3, 0));
            return document.ApplyEdit(
                "MOVE",
                handles.Select(h => new CadEditRecord(CadEditRecordKind.Transformed, h)).ToList(),
                s =>
                {
                    foreach (string handle in handles)
                    {
                        s.Document.FindEntity(handle).TransformBy(move);
                    }
                }
            );
        }

        /// <summary>The list of model space as it stands, then the one a REGEN makes, which the first must equal item for item.</summary>
        private static DisplayList2 AssertSameAsRegen(CadDocument document)
        {
            DisplayList2 patched = document.GetDisplay(document.ModelSpace);
            List<string> items = Describe(patched);
            document.Regen();
            DisplayList2 whole = document.GetDisplay(document.ModelSpace);

            Assert.Equal(Describe(whole), items);
            return patched;
        }

        [Fact]
        public void AMoveOfTwoEntities_DrawsThemAgain_AndKeepsTheItemsOfTheOthers()
        {
            CadDocument document = Plan();
            DisplayList2 before = document.GetDisplay(document.ModelSpace);
            DisplayItem2 circle = Assert.Single(before.Items, i => i.Handle == "A3");
            DisplayItem2 line = Assert.Single(before.Items, i => i.Handle == "A1");

            Assert.True(Move(document, "A1", "A5").Succeeded);
            DisplayList2 after = document.GetDisplay(document.ModelSpace);

            Assert.NotSame(before, after);
            Assert.Same(circle, Assert.Single(after.Items, i => i.Handle == "A3"));
            Assert.NotSame(line, Assert.Single(after.Items, i => i.Handle == "A1"));
            Assert.Equal(107.0, Assert.Single(after.Items, i => i.Handle == "A1").Bounds.MaxX, 9);
            AssertSameAsRegen(document);
        }

        [Fact]
        public void AnEraseAndAnAdd_LeaveTheListARegenMakes_TheNewLineLast()
        {
            CadDocument document = Plan();
            document.GetDisplay(document.ModelSpace);
            string added = null;

            Assert.True(
                document
                    .ApplyEdit("ERASE", new[] { new CadEditRecord(CadEditRecordKind.Erased, "A2") }, s =>
                        s.Document.FindEntity("A2").Erase()
                    )
                    .Succeeded
            );
            Assert.True(
                document
                    .ApplyEdit("LINE", null, s =>
                        added = s.Append(
                            s.Document.ModelSpace,
                            new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(-40, -40, 0))
                        ).Handle
                    )
                    .Succeeded
            );
            DisplayList2 after = AssertSameAsRegen(document);

            Assert.DoesNotContain(after.Items, i => i.Handle == "A2");
            Assert.Equal(added, after.Items[after.Count - 1].Handle);
        }

        [Fact]
        public void AnUndoAndARedo_ArePatchedFromTheDrawingReadBack()
        {
            CadDocument document = Plan();
            List<string> first = Describe(document.GetDisplay(document.ModelSpace));
            Assert.True(Move(document, "A3", "A4", "A5").Succeeded);
            List<string> moved = Describe(document.GetDisplay(document.ModelSpace));

            Assert.True(document.Undo());
            Assert.Equal(first, Describe(document.GetDisplay(document.ModelSpace)));
            Assert.True(document.Redo());
            Assert.Equal(moved, Describe(document.GetDisplay(document.ModelSpace)));
            AssertSameAsRegen(document);
        }

        [Fact]
        public void ALayerSwitchedOff_TakesAwayWhatReadsIt_AnInsertOnItWhole_AndKeepsTheRest()
        {
            CadDocument document = Plan();
            DisplayList2 before = document.GetDisplay(document.ModelSpace);
            DisplayItem2 text = Assert.Single(before.Items, i => i.Handle == "A4");

            Assert.True(CadLayerEditor.SetOn(document, "Steel", false).Succeeded);
            DisplayList2 off = document.GetDisplay(document.ModelSpace);

            Assert.DoesNotContain(off.Items, i => i.Handle == "A1" || i.Handle == "A2" || i.Handle == "A5");
            Assert.Same(text, Assert.Single(off.Items, i => i.Handle == "A4"));
            AssertSameAsRegen(document);

            Assert.True(CadLayerEditor.SetOn(document, "Steel", true).Succeeded);
            Assert.Equal(Describe(before), Describe(document.GetDisplay(document.ModelSpace)));
        }

        [Fact]
        public void ALayerOnlyANestedBlockReads_GivenAColour_RedrawsTheInsertThatHoldsIt()
        {
            CadDocument document = Plan();
            document.GetDisplay(document.ModelSpace);

            Assert.True(CadLayerEditor.SetColor(document, "Bolts", 6).Succeeded);
            DisplayList2 after = AssertSameAsRegen(document);

            Assert.Contains(after.Items, i => i.Handle == "A5" && i.Style.Rgb == 0xFF00FF);
        }

        [Fact]
        public void ALayerFrozenAndAMoveBeforeTheNextDrawing_AreBothPatched()
        {
            CadDocument document = Plan();
            document.GetDisplay(document.ModelSpace);

            Assert.True(CadLayerEditor.SetFrozen(document, "Notes", true).Succeeded);
            Assert.True(Move(document, "A1").Succeeded);
            DisplayList2 after = AssertSameAsRegen(document);

            Assert.DoesNotContain(after.Items, i => i.Handle == "A3");
            Assert.Equal(107.0, Assert.Single(after.Items, i => i.Handle == "A1").Bounds.MaxX, 9);
        }

        [Fact]
        public void AMoveInALayout_IsPatchedThere_AndModelSpaceIsKept()
        {
            CadDocument document = EditingDrawings.Blank();
            Assert.True(
                document
                    .ApplyEdit("LINE", null, s =>
                        s.Append(s.Document.ModelSpace, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0)))
                    )
                    .Succeeded
            );
            CadSpace sheet = Assert.Single(document.Spaces, s => s.Name == "Layout1");
            DisplayList2 model = document.GetDisplay(document.ModelSpace);
            string mark = null;
            Assert.True(
                document
                    .ApplyEdit("LINE", null, s =>
                        mark = s.Append(sheet, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0))).Handle
                    )
                    .Succeeded
            );
            document.GetDisplay(sheet);

            Assert.True(Move(document, mark).Succeeded);
            DisplayList2 patched = document.GetDisplay(sheet);
            List<string> items = Describe(patched);

            Assert.Same(model, document.GetDisplay(document.ModelSpace));
            Assert.Equal(17.0, Assert.Single(patched.Items, i => i.Handle == mark).Bounds.MaxX, 9);
            document.Regen();
            Assert.Equal(Describe(document.GetDisplay(sheet)), items);
        }

        [Fact]
        public void ACopyOfADimensionSharesItsPicture_SoAMoveOfTheOriginalDrawsTheCopyAgainToo()
        {
            // The original keeps its picture as its own (flag 32) and moves it; the copy, which draws the same block, shows the
            // block where the move left it, as a REGEN draws it.
            var database = new DxfDatabase("AC1027");
            var picture = new DxfBlockTableRecord { Name = "*D" };
            database.BlockTable.Add(picture);
            picture.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 12, 0)));
            picture.AppendEntity(new DxfLine(new GeoPoint3(20, 0, 0), new GeoPoint3(20, 12, 0)));
            picture.AppendEntity(new DxfLine(new GeoPoint3(0, 10, 0), new GeoPoint3(20, 10, 0)));
            var dimension = new DxfRotatedDimension(
                0.0,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(20, 0, 0),
                new GeoPoint3(20, 10, 0),
                string.Empty,
                null
            )
            {
                DimBlock = picture,
            };
            database.ModelSpace.AppendEntity(dimension);

            // As AutoCAD writes one: its block its own (flag 32) and placed at no point of its own (no group 12).
            DxfDatabase written = Rewritten(database, "DIMENSION", (code, value) => code == "70"
                    ? (int.Parse(value, CultureInfo.InvariantCulture) | 32).ToString(CultureInfo.InvariantCulture)
                : code == "12" || code == "22" || code == "32" ? null
                : value);
            CadDocument document = CadDocument.From(written, "dimensions.dxf");
            string original = dimension.Handle;
            document.GetDisplay(document.ModelSpace);
            string copy = null;
            GeoTransform3 up = GeoTransform3.Translation(new GeoVector3(0, 50, 0));

            Assert.True(
                document
                    .ApplyEdit("COPY", null, s =>
                        copy = s.Append(
                            document.ModelSpace,
                            s.Document.FindEntity(original).GetTransformedCopy(up)
                        ).Handle
                    )
                    .Succeeded
            );
            AssertSameAsRegen(document);
            Assert.True(Move(document, original).Succeeded);
            AssertSameAsRegen(document);
            Assert.True(Move(document, copy).Succeeded);
            AssertSameAsRegen(document);
        }

        [Fact]
        public void ALeaderOfWordsAboveItsLine_IsDrawnAgainWhenItsWordsMove_ItsHookRunningOnToTheirFarEnd()
        {
            // The hook of a leader of words above its line runs on to the far end of the MTEXT it points to, as the drawing's
            // fonts measure it: moving the words alone draws the leader again, as a REGEN draws it.
            string folder = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "gh-patch-" + Guid.NewGuid().ToString("N")
            );
            System.IO.Directory.CreateDirectory(folder);

            try
            {
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, "txt.shx"), ShxFiles.Unifont());
                var database = new DxfDatabase("AC1032") { FontSearchPath = folder };
                var words = new DxfMText
                {
                    Contents = "IOI",
                    TextHeight = 2.5,
                    Attachment = DxfAttachmentPoint.MiddleLeft,
                    Location = new GeoPoint3(22, 10, 0),
                };
                database.ModelSpace.AppendEntity(words);
                var leader = new DxfLeader { HasArrowHead = true };
                leader.AppendVertex(new GeoPoint3(0, 0, 0));
                leader.AppendVertex(new GeoPoint3(18.875, 8.125, 0));
                leader.AppendVertex(new GeoPoint3(21.375, 8.125, 0));
                leader.Dimasz = 2.5;
                leader.Dimgap = 0.625;
                leader.Dimtad = 1;
                database.ModelSpace.AppendEntity(leader);
                leader.Annotation = words;

                // Of a hook (75 of 1), as AutoCAD 2021 writes the leader of words it lands at their side.
                DxfDatabase hooked = Rewritten(database, "LEADER", (code, value) => code == "75" ? "1" : value);
                hooked.FontSearchPath = folder;
                CadDocument document = CadDocument.From(hooked, "leader.dxf");
                double before = Assert
                    .Single(document.GetDisplay(document.ModelSpace).Items, i =>
                        i.Handle == leader.Handle && i is DisplayPolyline2
                    )
                    .Bounds.MaxX;

                Assert.True(Move(document, words.Handle).Succeeded);
                DisplayList2 after = AssertSameAsRegen(document);

                Assert.True(before > 21.375 + 1E-9, "the hook runs on to " + before);
                Assert.Equal(
                    before + 7.0,
                    Assert.Single(after.Items, i => i.Handle == leader.Handle && i is DisplayPolyline2).Bounds.MaxX,
                    9
                );
            }
            finally
            {
                System.IO.Directory.Delete(folder, true);
            }
        }

        /// <summary>
        /// A drawing saved and read back with the groups of each entity of a type rewritten as AutoCAD writes them: the value
        /// the function gives for each code and value, the pair left out where it gives null.
        /// </summary>
        private static DxfDatabase Rewritten(DxfDatabase database, string type, Func<string, string, string> value)
        {
            var stream = new System.IO.MemoryStream();
            database.Save(stream);
            string[] lines = System.Text.Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n").Split('\n');
            var kept = new List<string>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                kept.Add(lines[i]);
                kept.Add(lines[i + 1]);

                if (lines[i].Trim() != "0" || lines[i + 1].Trim() != type)
                {
                    continue;
                }

                for (i += 2; i + 1 < lines.Length && lines[i].Trim() != "0"; i += 2)
                {
                    string written = value(lines[i].Trim(), lines[i + 1].Trim());

                    if (written != null)
                    {
                        kept.Add(lines[i]);
                        kept.Add(written);
                    }
                }

                i -= 2;
            }

            return DxfDatabase.Parse(string.Join("\n", kept) + "\n");
        }

        /// <summary>Each item as a line of text: its kind and every number, text and list it shows through its public properties.</summary>
        internal static List<string> Describe(DisplayList2 list)
        {
            var lines = list.Items.Select(i => Describe(i, 0)).ToList();
            lines.Add(Describe(list.Bounds, 0));
            return lines;
        }

        private static string Describe(object value, int depth)
        {
            switch (value)
            {
                case null:
                    return "null";
                case string text:
                    return "\"" + text + "\"";
                case double number:
                    return number.ToString("R", CultureInfo.InvariantCulture);
                case IFormattable formattable when value.GetType().IsPrimitive || value is Enum:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                case bool flag:
                    return flag ? "true" : "false";
                case DisplayList2 model:
                    return "list of " + model.Count;
                case IEnumerable items:
                    var parts = items.Cast<object>().Select(o => Describe(o, depth + 1)).ToList();

                    if (value is ISet<string>)
                    {
                        parts.Sort(StringComparer.Ordinal);
                    }

                    return "(" + string.Join(", ", parts) + ")";
            }

            if (depth > 5)
            {
                return "...";
            }

            IEnumerable<string> fields = value
                .GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0)
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => p.Name + " " + Describe(p.GetValue(value), depth + 1));
            return value.GetType().Name + " {" + string.Join("; ", fields) + "}";
        }
    }
}

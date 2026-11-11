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
    /// The managers of round six, at the level of the command line, their dialogs answered by a handler of the session's
    /// requests: BATTMAN over the attribute definitions of a block, the Edit Attributes dialog INSERT shows with ATTDIA 1,
    /// PURGE's dialog with nested items and the reasons an object cannot be purged, -PURGE purging nested items too, and
    /// CLASSICGROUP's Object Grouping.
    /// </summary>
    public class ManagerDialogTests
    {
        private static readonly GeoVector3 Up = new GeoVector3(0, 0, 1);

        /// <summary>
        /// A drawing of R2013 with the block GB: the attributes GRID (Grid letter, A), LEVEL (Level name, L1, invisible and
        /// verified) and REV (constant 0), a circle of radius 5; two INSERTs of it in model space, valued B and C, and one
        /// nested in the block PAIR.
        /// </summary>
        private static CadDocument Bubbles()
        {
            var db = new DxfDatabase("AC1027");
            var record = new DxfBlockTableRecord { Name = "GB" };
            db.BlockTable.Add(record);
            record.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), Up, 5));
            record.AppendEntity(
                new DxfAttributeDefinition(new GeoPoint3(0, 0, 0), "A", "GRID", "Grid letter", null) { Height = 2.5 }
            );
            record.AppendEntity(
                new DxfAttributeDefinition(new GeoPoint3(0, -4, 0), "L1", "LEVEL", "Level name", null)
                {
                    Height = 2.5,
                    Invisible = true,
                    Verifiable = true,
                }
            );
            record.AppendEntity(
                new DxfAttributeDefinition(new GeoPoint3(0, -8, 0), "0", "REV", "Revision", null)
                {
                    Height = 2.5,
                    Constant = true,
                }
            );
            Place(db.ModelSpace, record, new GeoPoint3(0, 0, 0), "B", "L2");
            Place(db.ModelSpace, record, new GeoPoint3(50, 0, 0), "C", "L3");
            var pair = new DxfBlockTableRecord { Name = "PAIR" };
            db.BlockTable.Add(pair);
            Place(pair, record, new GeoPoint3(0, 0, 0), "D", "L4");
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(100, 0, 0), pair));
            return CadDocument.From(db, "bubbles.dxf");
        }

        private static void Place(
            DxfBlockTableRecord owner,
            DxfBlockTableRecord record,
            GeoPoint3 at,
            params string[] values
        )
        {
            var insert = new DxfBlockReference(at, record);
            owner.AppendEntity(insert);
            int i = 0;

            foreach (
                DxfAttributeDefinition definition in record.OfType<DxfAttributeDefinition>().Where(d => !d.Constant)
            )
            {
                var attribute = new DxfAttributeReference();
                attribute.SetAttributeFromBlock(definition, insert.BlockTransform);
                attribute.TextString = values[i++];
                insert.AttributeCollection.AppendAttribute(attribute);
            }
        }

        private static List<DxfAttributeDefinition> Definitions(CadDocument document, string block = "GB") =>
            document.Database.BlockTable[block].OfType<DxfAttributeDefinition>().ToList();

        private static DxfBlockReference FirstInsert(CadDocument document) =>
            All<DxfBlockReference>(document.ModelSpace).First(r => r.Name == "GB");

        /// <summary>A session whose window answers a request of a name with a handler; true where it handled it.</summary>
        private static CadCommandSession Answering(
            CadDocument document,
            string name,
            Func<CadUiRequestEventArgs, bool> handler
        )
        {
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == name)
                {
                    e.Handled = handler(e);
                }
            };
            return session;
        }

        [Fact]
        public void BattmanListsTheDefinitionsOfTheBlockInItsOrder_WithTheirModeLetters_AndCountsItsInserts()
        {
            CadDocument document = Bubbles();
            CadAttributeManagerRequest shown = null;
            CadCommandSession session = Answering(document, "BATTMAN", e =>
            {
                shown = (CadAttributeManagerRequest)e.Payload;
                return true;
            });

            Run(session, "BATTMAN");

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { "GB" }, shown.Blocks);
            Assert.Equal("GB", shown.Block);
            Assert.Equal(new[] { "GRID", "LEVEL", "REV" }, shown.Rows.Select(r => r.Tag));
            Assert.Equal(new[] { "Grid letter", "Level name", "Revision" }, shown.Rows.Select(r => r.Prompt));
            Assert.Equal(new[] { "A", "L1", "0" }, shown.Rows.Select(r => r.Default));
            Assert.Equal("IV", shown.Rows[1].Modes.Replace("L", string.Empty));
            Assert.StartsWith("C", shown.Rows[2].Modes, StringComparison.Ordinal);
            Assert.Equal(3, shown.FoundInDrawing);
            Assert.Equal(2, shown.FoundInModelSpace);
        }

        [Fact]
        public void BattmanMoveAndEdit_WriteTheBlockOnOkAsOneUndoStep_AndTheInsertsChangeOnlyOnSync()
        {
            CadDocument document = Bubbles();
            string start = document.Database.ToText();
            CadCommandSession session = Answering(document, "BATTMAN", e =>
            {
                var request = (CadAttributeManagerRequest)e.Payload;
                CadAttributeDefinitionRow grid = request.Rows[0];
                request.Rows.RemoveAt(0);
                request.Rows.Insert(1, grid);
                grid.Prompt = "Grid";
                grid.Height = 3.5;
                request.Action = CadDialogAction.Ok;
                return true;
            });

            Run(session, "BATTMAN");

            Assert.Equal(new[] { "LEVEL", "GRID", "REV" }, Definitions(document).Select(d => d.Tag));
            Assert.Equal("Grid", Definitions(document)[1].Prompt);
            Assert.Equal(3.5, Definitions(document)[1].Height, 9);
            Assert.Equal(new[] { "GRID", "LEVEL" }, FirstInsert(document).AttributeCollection.Select(a => a.Tag));
            Assert.Equal("BATTMAN", document.NextUndo.Name);
            AssertReopens(document);

            // Sync brings every INSERT in line with the definitions as ATTSYNC does, the values kept.
            session = Answering(document, "BATTMAN", e =>
            {
                ((CadAttributeManagerRequest)e.Payload).Sync();
                return true;
            });
            Run(session, "BATTMAN");

            Assert.Equal(new[] { "LEVEL", "GRID" }, FirstInsert(document).AttributeCollection.Select(a => a.Tag));
            Assert.Equal(new[] { "L2", "B" }, FirstInsert(document).AttributeCollection.Select(a => a.TextString));
            Assert.Equal(3.5, FirstInsert(document).AttributeCollection[1].Height, 9);
            Assert.Equal(
                new[] { "LEVEL", "GRID" },
                document
                    .Database.BlockTable["PAIR"]
                    .OfType<DxfBlockReference>()
                    .Single()
                    .AttributeCollection.Select(a => a.Tag)
            );
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void BattmanApply_IsAnUndoStepOfItsOwn_AndApplyChangesToExistingReferences_SyncsTheInsertsAtOnce()
        {
            CadDocument document = Bubbles();
            CadCommandSession session = Answering(document, "BATTMAN", e =>
            {
                var request = (CadAttributeManagerRequest)e.Payload;
                request.Settings.ApplyToExistingReferences = true;
                request.Rows.RemoveAt(1);
                request.Apply();
                Assert.Equal(new[] { "GRID", "REV" }, request.Rows.Select(r => r.Tag));
                Assert.Equal(new[] { "GRID" }, FirstInsert(document).AttributeCollection.Select(a => a.Tag));

                request.Rows[0].Default = "Z";
                request.Action = CadDialogAction.Ok;
                return true;
            });

            Run(session, "BATTMAN");

            Assert.Equal("Z", Definitions(document)[0].TextString);
            Assert.Equal("B", FirstInsert(document).AttributeCollection.Single().TextString);
            Assert.True(session.AttributeManager.ApplyToExistingReferences);
            Assert.True(document.Undo());
            Assert.Equal("A", Definitions(document)[0].TextString);
            Assert.Equal(new[] { "GRID", "REV" }, Definitions(document).Select(d => d.Tag));
            Assert.True(document.Undo());
            Assert.Equal(new[] { "GRID", "LEVEL", "REV" }, Definitions(document).Select(d => d.Tag));
            Assert.Equal(new[] { "GRID", "LEVEL" }, FirstInsert(document).AttributeCollection.Select(a => a.Tag));
        }

        [Fact]
        public void BattmanCancel_WritesNothing_SelectBlockShowsTheBlockPicked_AndATagOfTwoWordsIsRefused()
        {
            CadDocument document = Bubbles();
            string start = document.Database.ToText();
            int shown = 0;
            CadCommandSession session = Answering(document, "BATTMAN", e =>
            {
                var request = (CadAttributeManagerRequest)e.Payload;
                shown++;
                request.Rows[0].Tag = shown == 1 ? "GRID LETTER" : request.Rows[0].Tag;
                request.Action = shown == 1 ? CadDialogAction.SelectBlock : CadDialogAction.Cancel;
                return true;
            });

            Run(session, "BATTMAN");
            Assert.Equal("Select a block:", session.PromptText);
            Assert.Contains("Invalid tag: a tag is one word, with no space.", session.History);
            session.FeedSelection(new[] { FirstInsert(document).Handle });

            Assert.False(session.IsRunning);
            Assert.Equal(2, shown);
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void BattmanWithNoWindow_SaysItIsADialog_AndOfADrawingWithNoAttributedBlocksSaysSo()
        {
            CadCommandSession session = Run(Session(Bubbles()), "BATTMAN");
            Assert.Equal(
                "BATTMAN is a dialog, and there is no window to show it in: use ATTSYNC and -ATTEDIT.",
                session.History.Last()
            );

            session = Run(Session(Blank()), "BATTMAN");
            Assert.Equal("This drawing contains no attributed blocks.", session.History.Last());
        }

        [Fact]
        public void AttdiaOne_AsksTheValuesInTheEditAttributesDialog_ConstantsLeftOut_AndAVerifiedValueAskedOnce()
        {
            CadDocument document = Bubbles();
            CadAttributeValuesRequest asked = null;
            CadCommandSession session = Answering(document, "ATTDIA", e =>
            {
                asked = (CadAttributeValuesRequest)e.Payload;
                asked.Rows[0].Value = "Q";
                asked.Rows[1].Value = "L9";
                asked.Action = CadDialogAction.Ok;
                return true;
            });

            Run(session, "ATTDIA", "1");
            Run(session, "-INSERT", "GB", P(200, 0), Enter, Enter, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal("GB", asked.Block);
            Assert.Equal(new[] { "GRID", "LEVEL" }, asked.Rows.Select(r => r.Tag));
            Assert.Equal(new[] { "Grid letter", "Level name" }, asked.Rows.Select(r => r.Prompt));
            DxfBlockReference placed = All<DxfBlockReference>(document.ModelSpace).Last();
            Assert.Equal(new[] { "Q", "L9" }, placed.AttributeCollection.Select(a => a.TextString));
            Assert.Equal("1", session.Registry.FindVariable("ATTDIA").GetValue(session));
        }

        [Fact]
        public void AttdiaOne_CancelPlacesTheDefaults_AndAttdiaZeroAsksOnTheCommandLine()
        {
            CadDocument document = Bubbles();
            CadCommandSession session = Answering(document, "ATTDIA", e => true);
            Assert.Equal("0", session.Registry.FindVariable("ATTDIA").GetValue(session));

            Run(session, "ATTDIA", "1", "-INSERT", "GB", P(200, 0), Enter, Enter, Enter);
            Assert.False(session.IsRunning);
            Assert.Equal(
                new[] { "A", "L1" },
                All<DxfBlockReference>(document.ModelSpace).Last().AttributeCollection.Select(a => a.TextString)
            );

            Run(session, "ATTDIA", "0", "-INSERT", "GB", P(300, 0), Enter, Enter, Enter);
            Assert.Equal("Grid letter <A>:", session.PromptText);
        }

        /// <summary>
        /// A drawing whose block OUTER, inserted nowhere, holds the only INSERT of INNER, whose circle is on layer DEEP; the
        /// layers WALLS (current, two lines of model space and the circle of GB) and TMP (nothing on it); a line of no length.
        /// </summary>
        private static CadDocument Nested()
        {
            var db = new DxfDatabase("AC1027");
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "DEEP" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "TMP" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS" });
            db.Clayer = "WALLS";
            var inner = new DxfBlockTableRecord { Name = "INNER" };
            db.BlockTable.Add(inner);
            inner.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), Up, 5) { Layer = "DEEP" });
            var outer = new DxfBlockTableRecord { Name = "OUTER" };
            db.BlockTable.Add(outer);
            outer.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), inner));
            var gb = new DxfBlockTableRecord { Name = "GB" };
            db.BlockTable.Add(gb);
            gb.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), Up, 5) { Layer = "WALLS" });
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(500, 0, 0), gb));
            db.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)) { Layer = "WALLS" }
            );
            db.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 10, 0), new GeoPoint3(100, 10, 0)) { Layer = "WALLS" }
            );
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 20, 0), new GeoPoint3(0.0002, 20, 0)));
            return CadDocument.From(db, "nested.dxf");
        }

        private static CadPurgeItem Item(IReadOnlyList<CadPurgeCategory> categories, string kind, string name) =>
            categories.Single(k => k.Name == kind).Items.Single(i => i.Name == name);

        [Fact]
        public void PurgeDialog_ListsWhatNothingUses_AndPurgeNestedPurgesWhatThePurgeLeftUnused_AsOneUndoStep()
        {
            CadDocument document = Nested();
            string start = document.Database.ToText();
            var asked = new List<string>();
            int purged = -1;
            bool reached = false;
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "PURGEITEM")
                {
                    asked.Add((string)e.Payload);
                    e.Answer = "Yes";
                    e.Handled = true;
                }
                else if (e.Name == "PURGE")
                {
                    var request = (CadPurgeRequest)e.Payload;
                    Assert.Equal(
                        new[] { "OUTER" },
                        request.Purgeable.Single(k => k.Name == "Blocks").Items.Select(i => i.Name)
                    );
                    Assert.Equal(
                        new[] { "TMP" },
                        request.Purgeable.Single(k => k.Name == "Layers").Items.Select(i => i.Name)
                    );
                    Assert.Equal(1, request.ZeroLength);
                    Assert.Equal(0, request.EmptyText);
                    Assert.True(request.ConfirmEach);
                    Assert.True(request.PurgeNested);

                    purged = request.Purge(new[] { Item(request.Purgeable, "Blocks", "OUTER") });

                    Assert.Empty(request.Purgeable.Single(k => k.Name == "Blocks").Items);
                    Assert.Equal(
                        new[] { "TMP" },
                        request.Purgeable.Single(k => k.Name == "Layers").Items.Select(i => i.Name)
                    );
                    reached = true;
                    e.Handled = true;
                }
            };

            Run(session, "PURGE");

            // An assertion that fails in the handler fails the command, which the session catches: the handler says it got through.
            Assert.True(reached);
            Assert.False(session.IsRunning);
            Assert.Equal(3, purged);
            Assert.Equal(new[] { "Purge block \"OUTER\"?", "Purge block \"INNER\"?", "Purge layer \"DEEP\"?" }, asked);
            Assert.False(document.Database.BlockTable.Has("INNER"));
            Assert.False(document.Database.LayerTable.Has("DEEP"));
            Assert.True(document.Database.LayerTable.Has("TMP"));
            Assert.Equal("PURGE", document.NextUndo.Name);
            AssertReopens(document);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void PurgeDialog_WithoutNestedPurgesOnlyWhatIsChecked_ANoKeepsIt_AndZeroLengthGeometryGoesWithIt()
        {
            CadDocument document = Nested();
            CadCommandSession session = Session(document);
            bool reached = false;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "PURGEITEM")
                {
                    e.Answer = ((string)e.Payload).Contains("TMP") ? "No" : "Yes";
                    e.Handled = true;
                }
                else if (e.Name == "PURGE")
                {
                    var request = (CadPurgeRequest)e.Payload;
                    request.PurgeNested = false;
                    request.PurgeZeroLength = true;
                    Assert.Equal(
                        1,
                        request.Purge(
                            new[]
                            {
                                Item(request.Purgeable, "Blocks", "OUTER"),
                                Item(request.Purgeable, "Layers", "TMP"),
                            }
                        )
                    );
                    Assert.Equal(
                        new[] { "INNER" },
                        request.Purgeable.Single(k => k.Name == "Blocks").Items.Select(i => i.Name)
                    );
                    Assert.Equal(0, request.ZeroLength);
                    reached = true;
                    e.Handled = true;
                }
            };

            Run(session, "PURGE");

            Assert.True(reached);
            Assert.True(document.Database.BlockTable.Has("INNER"));
            Assert.True(document.Database.LayerTable.Has("TMP"));
            Assert.Equal(2, All<DxfLine>(document.ModelSpace).Count);
        }

        [Fact]
        public void PurgeDialog_FindNonPurgeable_SaysWhyAnObjectIsInUse_AndSelectObjectsSelectsWhatUsesIt()
        {
            CadDocument document = Nested();
            CadPurgeRequest request = null;
            CadCommandSession session = Answering(document, "PURGE", e =>
            {
                request = (CadPurgeRequest)e.Payload;
                request.Selected = Item(request.InUse, "Layers", "WALLS");
                request.Action = CadDialogAction.SelectObjects;
                return true;
            });

            Run(session, "PURGE");

            Assert.Equal(
                new[]
                {
                    "Layer WALLS is the current layer.",
                    "Used by 2 objects in model space and 0 in layouts.",
                    "Referenced by block GB.",
                },
                Item(request.InUse, "Layers", "WALLS").Reasons
            );
            Assert.Equal(
                new[]
                {
                    "Layer 0 cannot be purged: AutoCAD keeps it in every drawing.",
                    "Used by 2 objects in model space and 0 in layouts.",
                    "Referenced by block OUTER.",
                },
                Item(request.InUse, "Layers", "0").Reasons
            );
            Assert.Equal(
                new[] { "Used by 1 object in model space and 0 in layouts." },
                Item(request.InUse, "Blocks", "GB").Reasons
            );
            Assert.Equal(new[] { "Referenced by block OUTER." }, Item(request.InUse, "Blocks", "INNER").Reasons);
            Assert.DoesNotContain(request.InUse.Single(k => k.Name == "Layers").Items, i => i.Name == "TMP");
            Assert.Equal(2, document.Selection.Handles.Count);
            Assert.All(document.Selection.Handles, h => Assert.Equal("WALLS", document.FindEntity(h).Layer));
        }

        /// <summary>
        /// A drawing of four groups: EMPTY never given a member, GONE whose one line is erased, an unnamed *A1 emptied the
        /// same way, and FULL of one line.
        /// </summary>
        private static CadDocument Grouped()
        {
            var db = new DxfDatabase("AC1027");

            if (db.GroupDictionary == null)
            {
                db.NamedObjectsDictionary.SetAt("ACAD_GROUP", new DxfDBDictionary());
            }

            var lines = new List<DxfLine>();

            for (int i = 0; i < 3; i++)
            {
                var line = new DxfLine(new GeoPoint3(0, 10 * i, 0), new GeoPoint3(100, 10 * i, 0));
                db.ModelSpace.AppendEntity(line);
                lines.Add(line);
            }

            string[] names = { "GONE", "*", "FULL" };

            for (int i = 0; i < 3; i++)
            {
                var group = new DxfGroup(string.Empty, true);
                db.GroupDictionary.SetAt(names[i], group);
                group.Append(lines[i]);
            }

            db.GroupDictionary.SetAt("EMPTY", new DxfGroup(string.Empty, true));
            lines[0].Erase();
            lines[1].Erase();
            return CadDocument.From(db, "groups.dxf");
        }

        /// <summary>The names of the drawing's groups, in ordinal order.</summary>
        private static string[] GroupNames(CadDocument document) =>
            document.Database.GroupDictionary.Select(e => e.Key).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        /// <summary>
        /// AutoCAD 2021's -PURGE Groups of such a drawing: "Deleting group "GONE"." for each group that holds nothing, the
        /// unnamed "*A1" among them, "3 groups deleted.", and of a second run "No unreferenced groups found.".
        /// </summary>
        [Fact]
        public void MinusPurgeGroups_DeletesTheGroupsThatHoldNothing_NamedOrNot_AndKeepsTheOneWithAMember()
        {
            CadDocument document = Grouped();
            CadCommandSession session = Run(Session(document), "-PURGE", "G", Enter, "N");

            Assert.False(session.IsRunning);
            Assert.Contains("Deleting group \"*A1\".", session.History);
            Assert.Contains("Deleting group \"EMPTY\".", session.History);
            Assert.Contains("Deleting group \"GONE\".", session.History);
            Assert.Equal("3 groups deleted.", session.History.Last());
            Assert.Equal(new[] { "FULL" }, GroupNames(document));
            Assert.Equal(1, Group(document, "FULL").NumEntities);

            Run(session, "-PURGE", "G", Enter, "N");
            Assert.Equal("No unreferenced groups found.", session.History.Last());

            Assert.True(document.Undo());
            Assert.Equal(4, GroupNames(document).Length);
        }

        [Fact]
        public void MinusPurgeAll_TakesTheEmptyGroupsWithTheRest_AndVerifiedAsksOfEach()
        {
            CadDocument document = Grouped();
            CadCommandSession session = Run(Session(document), "-PURGE", "G", "E*", Enter);
            Assert.Equal("Purge group \"EMPTY\"? [Yes/No] <No>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal(4, GroupNames(document).Length);

            Run(session, "-PURGE", "A", Enter, "N");
            Assert.Contains("3 groups deleted.", session.History);
            Assert.Equal(new[] { "FULL" }, GroupNames(document));
        }

        [Fact]
        public void PurgeDialog_ListsTheEmptyGroupsUnderGroups_PurgesOne_AndSaysOfAGroupInUseThatItHoldsObjects()
        {
            CadDocument document = Grouped();
            CadPurgeRequest request = null;
            CadCommandSession session = Answering(document, "PURGE", e =>
            {
                request = (CadPurgeRequest)e.Payload;
                request.ConfirmEach = false;
                Assert.Equal(
                    new[] { "*A1", "EMPTY", "GONE" },
                    request.Purgeable.Single(k => k.Name == "Groups").Items.Select(i => i.Name)
                );
                Assert.Equal(new[] { "Group FULL holds objects." }, Item(request.InUse, "Groups", "FULL").Reasons);
                Assert.Equal(1, request.Purge(new[] { Item(request.Purgeable, "Groups", "GONE") }));
                Assert.Equal(
                    new[] { "*A1", "EMPTY" },
                    request.Purgeable.Single(k => k.Name == "Groups").Items.Select(i => i.Name)
                );
                return true;
            });

            Run(session, "PURGE");

            Assert.Equal(new[] { "*A1", "EMPTY", "FULL" }, GroupNames(document));
        }

        [Fact]
        public void PurgeWithNoWindow_AsksAsMinusPurge_AndMinusPurgeAllPurgesNestedItemsToo()
        {
            CadDocument document = Nested();
            CadCommandSession session = Run(Session(document), "PURGE");
            Assert.StartsWith(
                "Enter type of unused objects to purge [Blocks/",
                session.PromptText,
                StringComparison.Ordinal
            );
            Run(session, "A", Enter, "N");

            Assert.False(session.IsRunning);
            Assert.False(document.Database.BlockTable.Has("OUTER"));
            Assert.False(document.Database.BlockTable.Has("INNER"));
            Assert.False(document.Database.LayerTable.Has("DEEP"));
            Assert.False(document.Database.LayerTable.Has("TMP"));
            Assert.Contains("2 blocks deleted.", session.History);
            Assert.Contains("2 layers deleted.", session.History);
            Assert.True(document.Undo());
            Assert.True(document.Database.BlockTable.Has("INNER"));
        }

        [Fact]
        public void MinusPurgeVerified_AsksTheNestedBlockOnceTheOuterIsPurged_AndNotAgainWhatWasKept()
        {
            CadDocument document = Nested();
            CadCommandSession session = Run(Session(document), "-PURGE", "B", Enter, Enter);
            Assert.Equal("Purge block \"OUTER\"? [Yes/No] <No>:", session.PromptText);
            Run(session, "Y");
            Assert.Equal("Purge block \"INNER\"? [Yes/No] <No>:", session.PromptText);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.True(document.Database.BlockTable.Has("INNER"));
            Assert.Equal("1 block deleted.", session.History.Last());
        }

        /// <summary>A line A, a circle B and a line C, and a line D, in that order.</summary>
        private static CadDocument Four(out string[] handles)
        {
            var db = new DxfDatabase("AC1027");
            var made = new List<string>();

            foreach (
                DxfEntity entity in new DxfEntity[]
                {
                    new DxfLine(new GeoPoint3(-20, 0, 0), new GeoPoint3(20, 0, 0)),
                    new DxfCircle(new GeoPoint3(0, 0, 0), Up, 10),
                    new DxfLine(new GeoPoint3(0, -20, 0), new GeoPoint3(0, 20, 0)),
                    new DxfLine(new GeoPoint3(30, 0, 0), new GeoPoint3(40, 0, 0)),
                }
            )
            {
                db.ModelSpace.AppendEntity(entity);
                made.Add(entity.Handle);
            }

            handles = made.ToArray();
            return CadDocument.From(db, "four.dxf");
        }

        private static DxfGroup Group(CadDocument document, string name) =>
            (DxfGroup)document.Database.GroupDictionary.GetAt(name);

        [Fact]
        public void ClassicGroup_NewAddRemoveAskOnTheCommandLine_AndTheDialogComesBack_OkKeepingEveryChangeAsOneUndoStep()
        {
            CadDocument document = Four(out string[] h);
            string start = document.Database.ToText();
            int shown = 0;
            CadCommandSession session = Answering(document, "CLASSICGROUP", e =>
            {
                var request = (CadGroupManagerRequest)e.Payload;
                shown++;

                switch (shown)
                {
                    case 1:
                        Assert.Empty(request.Groups);
                        request.GroupName = "STAIR_CORE";
                        request.Description = "Stair core walls";
                        request.NewSelectable = false;
                        request.Pick = "New";
                        break;
                    case 2:
                        Assert.Equal("STAIR_CORE", request.Selected);
                        CadGroupRow row = Assert.Single(request.Groups);
                        Assert.False(row.Selectable);
                        Assert.Equal(2, row.Count);
                        request.Pick = "Add";
                        break;
                    case 3:
                        request.Pick = "Remove";
                        break;
                    default:
                        Assert.Equal(
                            new[] { h[1], h[2] },
                            Group(document, "STAIR_CORE").GetAllEntityIds().Select(m => m.Handle)
                        );
                        request.Action = CadDialogAction.Ok;
                        return true;
                }

                request.Action = CadDialogAction.SelectObjects;
                return true;
            });

            Run(session, "CLASSICGROUP");
            Assert.Equal("Select objects for grouping:", session.PromptText);
            Run(session, new[] { h[0], h[1] }, Enter);
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { h[2] }, Enter);
            Assert.Equal("Remove objects:", session.PromptText);
            Run(session, new[] { h[0] }, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(4, shown);
            Assert.Equal("Stair core walls", Group(document, "STAIR_CORE").Description);
            Assert.Equal("CLASSICGROUP", document.NextUndo.Name);
            AssertReopens(document);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void ClassicGroup_RenameDescriptionSelectableReorderAndExplode_ChangeAtOnce_AndCancelTakesThemBack()
        {
            CadDocument document = Four(out string[] h);
            Run(Session(document), "-GROUP", Enter, "PLATE", "Plate", h, Enter);
            string start = document.Database.ToText();
            bool reached = false;
            CadCommandSession session = Answering(document, "CLASSICGROUP", e =>
            {
                var request = (CadGroupManagerRequest)e.Payload;
                Assert.False(request.Rename());
                Assert.Equal("Choose a group in the list first.", request.Message);

                request.Selected = "PLATE";
                request.GroupName = "P1";
                Assert.True(request.Rename());
                Assert.Equal("P1", request.Groups.Single().Name);

                request.Description = "First plate";
                Assert.True(request.ChangeDescription());
                Assert.True(request.ToggleSelectable());
                Assert.False(request.Groups.Single().Selectable);

                Assert.True(request.Reorder(0, 3, 1));
                Assert.Equal(
                    new[] { h[1], h[2], h[3], h[0] },
                    Group(document, "P1").GetAllEntityIds().Select(m => m.Handle)
                );
                Assert.False(request.Reorder(3, 3, 2));
                Assert.StartsWith("Invalid position number", request.Message, StringComparison.Ordinal);
                Assert.True(request.Reverse());
                Assert.Equal(
                    new[] { h[0], h[3], h[2], h[1] },
                    Group(document, "P1").GetAllEntityIds().Select(m => m.Handle)
                );

                Assert.True(request.Explode());
                Assert.Empty(request.Groups);
                Assert.Null(request.Selected);
                reached = true;
                request.Action = CadDialogAction.Cancel;
                return true;
            });

            Run(session, "CLASSICGROUP");

            Assert.True(reached);
            Assert.False(session.IsRunning);
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void ClassicGroup_FindNameSaysTheGroupsOfTheObjectPicked_HighlightShowsTheMembers_AndUnnamedGroupsAreListedToo()
        {
            CadDocument document = Four(out string[] h);
            Run(Session(document), "-GROUP", Enter, "PLATE", "Plate", new[] { h[0], h[1] }, Enter);
            Run(Session(document), "GROUP", new[] { h[2] }, Enter);
            int shown = 0;
            CadCommandSession session = Answering(document, "CLASSICGROUP", e =>
            {
                var request = (CadGroupManagerRequest)e.Payload;
                shown++;
                Assert.Equal(new[] { "*A1", "PLATE" }, request.Groups.Select(g => g.Name));
                Assert.True(request.Groups[0].IsUnnamed);
                request.Pick = shown == 1 ? "Find Name" : "Highlight";
                request.Action = shown < 3 ? CadDialogAction.SelectObjects : CadDialogAction.Ok;
                return true;
            });

            Run(session, "CLASSICGROUP");
            Assert.Equal("Pick a member of a group:", session.PromptText);
            session.FeedSelection(new[] { h[1] });
            Assert.Contains("Group Member List: PLATE", session.History);
            Assert.Equal("Press ENTER to continue:", session.PromptText);
            Assert.Equal(new[] { h[0], h[1] }, document.Selection.Handles.OrderBy(x => x, StringComparer.Ordinal));
            Run(session, Enter);

            Assert.Equal(3, shown);
            Assert.False(session.IsRunning);
            Assert.Empty(document.Selection.Handles);
        }

        [Fact]
        public void OrderGroupsHighlight_StepsThroughTheMembersOneAtATime_NextAndPreviousGoRound_AndEnterBringsTheDialogBack()
        {
            CadDocument document = Four(out string[] h);
            Run(Session(document), "-GROUP", Enter, "ROW", "Row", new[] { h[0], h[1], h[2] }, Enter);
            int shown = 0;
            CadCommandSession session = Answering(document, "CLASSICGROUP", e =>
            {
                var request = (CadGroupManagerRequest)e.Payload;
                shown++;
                request.Selected = "ROW";
                request.Pick = "Order Highlight";
                request.Action = shown == 1 ? CadDialogAction.SelectObjects : CadDialogAction.Ok;
                return true;
            });

            Run(session, "CLASSICGROUP");

            Assert.Equal("Object 0 of 2 [Next/Previous/eXit]:", session.PromptText);
            Assert.Equal(new[] { h[0] }, document.Selection.Handles);

            Run(session, "N");

            Assert.Equal("Object 1 of 2 [Next/Previous/eXit]:", session.PromptText);
            Assert.Equal(new[] { h[1] }, document.Selection.Handles);

            // Previous from the first goes round to the last.
            Run(session, "P", "P");

            Assert.Equal("Object 2 of 2 [Next/Previous/eXit]:", session.PromptText);
            Assert.Equal(new[] { h[2] }, document.Selection.Handles);

            Run(session, Enter);

            Assert.Equal(2, shown);
            Assert.False(session.IsRunning);
            Assert.Empty(document.Selection.Handles);
        }

        [Fact]
        public void ClassicGroupWithNoWindow_AsksAsMinusGroup()
        {
            CadCommandSession session = Run(Session(Blank()), "CLASSICGROUP");
            Assert.Equal(
                "Enter a group option [?/Order/Add/Remove/Explode/REName/Selectable/Create] <Create>:",
                session.PromptText
            );
        }
    }
}

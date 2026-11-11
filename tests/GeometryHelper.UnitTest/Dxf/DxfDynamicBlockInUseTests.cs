using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Dynamic blocks in use: the custom grips an INSERT of a dynamic block shows where its values put them, AutoCAD's
    /// ResetBlock, the names of a flip's states and the visibility states of a block, each made from code as AutoCAD 2021 saves
    /// such blocks and checked against what AutoCAD gave of its own samples and of the library's (plugin GhDyn1).
    /// </summary>
    public class DxfDynamicBlockInUseTests
    {
        private static readonly Tolerance Exact = new Tolerance(1E-9, 1E-9);

        /// <summary>A new drawing of R2018 with an empty block of the name and an INSERT of it at the point given.</summary>
        private static DxfDatabase Drawing(string name, out DxfBlockTableRecord block, GeoPoint3 at)
        {
            var db = new DxfDatabase("AC1032");
            block = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            db.ModelSpace.AppendEntity(new DxfBlockReference(at, block));
            return db;
        }

        private static T Add<T>(DxfBlockTableRecord block, T entity)
            where T : DxfEntity
        {
            block.AppendEntity(entity);
            return entity;
        }

        /// <summary>The rectangle 100 by 50 whose Width from (0, 0) to (100, 0), a list of 100, 150 and 200, stretches its right edge.</summary>
        private static DxfDatabase Panel(GeoPoint3 at, out DxfBlockTableRecord block)
        {
            DxfDatabase db = Drawing("GH_PANEL", out block, at);
            DxfLine[] lines =
            {
                Add(block, new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0))),
                Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0))),
                Add(block, new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0))),
                Add(block, new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0))),
            };
            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                "Overall width",
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                DxfBlockParameterValueSet.List(new[] { 100.0, 150, 200 })
            );
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            return db;
        }

        /// <summary>A sign of a frame, a cross and a tick, of the visibility states Cross, Tick and Blank, its grip at (20, -10).</summary>
        private static DxfDatabase Sign(out DxfBlockTableRecord block, out DxfLine[] lines)
        {
            DxfDatabase db = Drawing("GH_SIGN", out block, new GeoPoint3(0, 0, 0));
            lines = new[]
            {
                Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0))),
                Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 40, 0))),
                Add(block, new DxfLine(new GeoPoint3(10, 20, 0), new GeoPoint3(20, 10, 0))),
            };
            block.AddVisibilityParameter(
                "Mark",
                "What the sign shows",
                new GeoPoint3(20, -10, 0),
                new[]
                {
                    new DxfBlockVisibilityState("Cross", new DxfEntity[] { lines[1] }),
                    new DxfBlockVisibilityState("Tick", new DxfEntity[] { lines[2] }),
                    new DxfBlockVisibilityState("Blank", new DxfEntity[0]),
                }
            );
            return db;
        }

        private static DxfDatabase Reopened(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        private static DxfBlockReference TheInsert(DxfDatabase db) => db.ModelSpace.OfType<DxfBlockReference>().First();

        private static DxfDynamicBlockReferenceProperty Property(DxfBlockReference insert, string name) =>
            insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == name);

        private static bool At(GeoPoint3 point, double x, double y) => point.IsEqualTo(new GeoPoint3(x, y, 0), Exact);

        private static bool Along(GeoVector3 vector, double x, double y) =>
            Math.Abs(vector.X - x) <= 1E-9 && Math.Abs(vector.Y - y) <= 1E-9 && Math.Abs(vector.Z) <= 1E-9;

        // ---- ResetBlock ----------------------------------------------------------------------------------------------------

        [Fact]
        public void AnInsertSetThenReset_PlacesTheDynamicBlockAgain_AndLosesItsRepresentation()
        {
            DxfDatabase db = Panel(new GeoPoint3(1000, 500, 0), out DxfBlockTableRecord block);
            DxfBlockReference insert = TheInsert(db);
            Property(insert, "Width").Value = 200.0;
            DxfBlockTableRecord anonymous = insert.BlockTableRecord;
            Assert.True(anonymous.IsAnonymous);
            Assert.True(insert.ExtensionDictionary.Contains("AcDbBlockRepresentation"));

            insert.ResetBlock();

            Assert.Same(block, insert.BlockTableRecord);
            Assert.Null(insert.AnonymousBlockTableRecord);
            Assert.NotNull(insert.ExtensionDictionary);
            Assert.False(insert.ExtensionDictionary.Contains("AcDbBlockRepresentation"));
            Assert.True(At(insert.Position, 1000, 500));

            DxfDatabase reopened = Reopened(db);
            DxfBlockReference back = TheInsert(reopened);
            Assert.Equal("GH_PANEL", back.Name);
            Assert.True(back.IsDynamicBlock);
            Assert.Equal(100.0, Assert.IsType<double>(Property(back, "Width").Value), 9);

            // The anonymous block it placed stays in the drawing, as AutoCAD left *U2 there.
            Assert.Contains(reopened.BlockTable, b => b.Name == anonymous.Name);
        }

        [Fact]
        public void AResetInsert_IsSetAgainAsAnyOther()
        {
            DxfDatabase db = Panel(new GeoPoint3(0, 0, 0), out _);
            DxfBlockReference insert = TheInsert(db);
            Property(insert, "Width").Value = 200.0;
            insert.ResetBlock();

            Property(insert, "Width").Value = 150.0;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.Equal(150.0, Assert.IsType<double>(Property(back, "Width").Value), 9);
            Assert.Contains(back.BlockTableRecord.OfType<DxfLine>(), l =>
                At(l.StartPoint, 150, 0) && At(l.EndPoint, 150, 50)
            );
        }

        [Fact]
        public void AnInsertOfTheDynamicBlockItself_OrOfAPlainBlock_IsLeftAsItIsByAReset()
        {
            DxfDatabase db = Panel(new GeoPoint3(0, 0, 0), out _);
            var plain = new DxfBlockTableRecord { Name = "PLAIN", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(plain);
            plain.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(50, 0, 0), plain));
            string before = db.ToText();

            foreach (DxfBlockReference insert in db.ModelSpace.OfType<DxfBlockReference>().ToList())
            {
                insert.ResetBlock();
            }

            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void AResetOfAnErasedInsert_IsRefused()
        {
            DxfDatabase db = Panel(new GeoPoint3(0, 0, 0), out _);
            DxfBlockReference insert = TheInsert(db);
            insert.Erase();

            Assert.Throws<InvalidOperationException>(() => insert.ResetBlock());
        }

        // ---- grips ---------------------------------------------------------------------------------------------------------

        [Fact]
        public void TheWidthGrip_StandsAtTheEndItsValuePutsIt_TurnedWithTheInsert()
        {
            DxfDatabase db = Panel(new GeoPoint3(1000, 500, 0), out _);
            DxfBlockReference insert = TheInsert(db);
            insert.Rotation = Math.PI / 2;

            DxfDynamicBlockGrip grip = Assert.Single(insert.GetDynamicGrips());
            Assert.Equal(DxfDynamicBlockGripType.Linear, grip.GripType);
            Assert.Equal(new[] { "Width" }, grip.PropertyNames);
            Assert.True(At(grip.Position, 1000, 600));
            Assert.True(At(grip.BasePoint, 1000, 500));
            Assert.True(Along(grip.Direction, 0, 1));

            Property(insert, "Width").Value = 200.0;

            grip = Assert.Single(TheInsert(Reopened(db)).GetDynamicGrips());
            Assert.True(At(grip.Position, 1000, 700));
            Assert.True(At(grip.BasePoint, 1000, 500));
        }

        [Fact]
        public void AnInsertScaledByTwo_HasItsGripTwiceAsFarOut()
        {
            DxfDatabase db = Panel(new GeoPoint3(0, 0, 0), out _);
            DxfBlockReference insert = TheInsert(db);
            insert.ScaleFactors = new GeoVector3(2, 2, 2);
            Property(insert, "Width").Value = 300.0;

            DxfDynamicBlockGrip grip = Assert.Single(insert.GetDynamicGrips());
            Assert.True(At(grip.Position, 300, 0));
            Assert.Equal(300.0, Assert.IsType<double>(Property(insert, "Width").Value), 9);
        }

        [Fact]
        public void TheRotationGrip_TurnsWithTheAngle_AboutItsBasePoint()
        {
            DxfDatabase db = Drawing("GH_ARROW", out DxfBlockTableRecord block, new GeoPoint3(0, 0, 0));
            DxfLine arrow = Add(block, new DxfLine(new GeoPoint3(50, 25, 0), new GeoPoint3(80, 25, 0)));
            DxfBlockRotationParameter angle = block.AddRotationParameter(
                "Angle",
                "Turn of the arrow",
                new GeoPoint3(50, 25, 0),
                30,
                0
            );
            block.AddRotateAction(angle, new[] { arrow });
            DxfBlockReference insert = TheInsert(db);

            DxfDynamicBlockGrip grip = Assert.Single(insert.GetDynamicGrips());
            Assert.Equal(DxfDynamicBlockGripType.Rotation, grip.GripType);
            Assert.True(At(grip.Position, 80, 25));
            Assert.True(At(grip.BasePoint, 50, 25));

            Property(insert, "Angle").Value = Math.PI / 2;

            grip = Assert.Single(TheInsert(Reopened(db)).GetDynamicGrips());
            Assert.True(At(grip.Position, 50, 55));
        }

        [Fact]
        public void TheFlipGrip_PointsAcrossTheLine_AndTheOtherWayFlipped_AndItsStatesAreNamed()
        {
            DxfDatabase db = Drawing("GH_DOOR", out DxfBlockTableRecord block, new GeoPoint3(0, 0, 0));
            DxfLine leaf = Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(90, 0, 0)));
            DxfBlockFlipParameter hand = block.AddFlipParameter(
                "Hand",
                "Which way the door opens",
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(0, 50, 0)
            );
            block.AddFlipAction(hand, new[] { leaf });
            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(new[] { "Not flipped", "Flipped" }, Property(insert, "Hand").StateNames);

            DxfDynamicBlockGrip grip = Assert.Single(insert.GetDynamicGrips());
            Assert.Equal(DxfDynamicBlockGripType.Flip, grip.GripType);
            Assert.True(At(grip.Position, 0, 50));
            Assert.True(At(grip.BasePoint, 0, 0));
            Assert.True(Along(grip.Direction, -1, 0));

            Property(insert, "Hand").Value = (short)1;

            grip = Assert.Single(TheInsert(Reopened(db)).GetDynamicGrips());
            Assert.True(At(grip.Position, 0, 50));
            Assert.True(Along(grip.Direction, 1, 0));
        }

        [Fact]
        public void AGripOffTheFlipLine_IsMirroredAboutIt_WhileFlipped()
        {
            DxfDatabase db = Drawing("GH_DOOR", out DxfBlockTableRecord block, new GeoPoint3(0, 0, 0));
            DxfLine leaf = Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(90, 0, 0)));
            DxfBlockFlipParameter hand = block.AddFlipParameter(
                "Hand",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(0, 50, 0)
            );
            block.AddFlipAction(hand, new[] { leaf });

            // AutoCAD's Block Editor lets the grip stand off the line, as its Door sample's Swing grip stands 100 below it.
            DxfDBObject graph = block.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");
            DxfItem gripNode = graph
                .Item.Groups.Where(g => g.TypeCode == 360)
                .Select(g => graph.Item.File.ObjectItem(g.Value))
                .Single(n => n.Type == "BLOCKFLIPGRIP");
            DxfDatabase off = DxfDatabase.Parse(MoveGrip(db.ToText(), gripNode.Handle, -30, 20));
            DxfBlockReference insert = TheInsert(off);
            Assert.True(At(Assert.Single(insert.GetDynamicGrips()).Position, -30, 20));

            Property(insert, "Hand").Value = (short)1;

            Assert.True(At(Assert.Single(insert.GetDynamicGrips()).Position, 30, 20));
        }

        /// <summary>The drawing's text with the position of the BLOCKFLIPGRIP of a handle (1010 and 1020 of AcDbBlockGrip) set to the point given.</summary>
        private static string MoveGrip(string text, string handle, double x, double y)
        {
            string[] lines = text.Split('\n');
            int at = Array.FindIndex(lines, l => l.Trim() == "BLOCKFLIPGRIP");

            while (lines[at + 1].Trim() != "5" || lines[at + 2].Trim() != handle)
            {
                at = Array.FindIndex(lines, at + 1, l => l.Trim() == "BLOCKFLIPGRIP");
            }

            int grip = Array.FindIndex(lines, at, l => l.Trim() == "AcDbBlockGrip");
            int x1 = Array.FindIndex(lines, grip, l => l.Trim() == "1010");
            string end = lines[x1 + 1].EndsWith("\r", StringComparison.Ordinal) ? "\r" : string.Empty;
            lines[x1 + 1] = x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + end;
            lines[x1 + 3] = y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + end;
            return string.Join("\n", lines);
        }

        [Fact]
        public void TheVisibilityGrip_StandsAtItsLocation_AndTheBlockListsItsStates()
        {
            DxfDatabase db = Sign(out DxfBlockTableRecord block, out DxfLine[] lines);
            DxfDynamicBlockGrip grip = Assert.Single(TheInsert(db).GetDynamicGrips());
            Assert.Equal(DxfDynamicBlockGripType.Visibility, grip.GripType);
            Assert.Equal(new[] { "Mark" }, grip.PropertyNames);
            Assert.True(At(grip.Position, 20, -10));
            Assert.Empty(Property(TheInsert(db), "Mark").StateNames);

            DxfBlockTableRecord back = Reopened(db).BlockTable["GH_SIGN"];
            IReadOnlyList<DxfBlockVisibilityState> states = back.GetVisibilityStates();
            Assert.Equal(new[] { "Cross", "Tick", "Blank" }, states.Select(s => s.Name));
            Assert.Equal(new[] { lines[1].Handle }, states[0].Entities.Select(e => e.Handle));
            Assert.Equal(new[] { lines[2].Handle }, states[1].Entities.Select(e => e.Handle));
            Assert.Empty(states[2].Entities);
            Assert.Empty(Reopened(db).ModelSpace.GetVisibilityStates());
        }

        /// <summary>The sign of <see cref="Sign"/> given a Width from (0, 0) to (40, 0) that stretches its bottom line, before or after its states.</summary>
        private static DxfDatabase SignWithWidth(bool widthFirst, out DxfBlockTableRecord block)
        {
            DxfDatabase db = Drawing("GH_SIGN", out block, new GeoPoint3(0, 0, 0));
            DxfLine[] lines =
            {
                Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0))),
                Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 40, 0))),
                Add(block, new DxfLine(new GeoPoint3(10, 20, 0), new GeoPoint3(20, 10, 0))),
            };
            DxfBlockLinearParameter width = widthFirst
                ? block.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0), -10, null)
                : null;
            block.AddVisibilityParameter(
                "Mark",
                "What the sign shows",
                new GeoPoint3(20, -10, 0),
                new[]
                {
                    new DxfBlockVisibilityState("Cross", new DxfEntity[] { lines[1] }),
                    new DxfBlockVisibilityState("Tick", new DxfEntity[] { lines[2] }),
                    new DxfBlockVisibilityState("Blank", new DxfEntity[0]),
                }
            );
            width = width
                ?? block.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0), -10, null);
            block.AddStretchAction(width, new GeoPoint2(30, -5), new GeoPoint2(50, 5), new[] { lines[0] });
            return db;
        }

        /// <summary>The nodes of the graph of a block, in its order.</summary>
        private static List<DxfItem> Nodes(DxfBlockTableRecord block)
        {
            DxfDBObject graph = block.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");
            return graph
                .Item.Groups.Where(g => g.TypeCode == 360)
                .Select(g => graph.Item.File.ObjectItem(g.Value))
                .ToList();
        }

        /// <summary>The handles each state of the block's visibility parameter lists after its 95, by the state's name.</summary>
        private static Dictionary<string, List<string>> Listed(DxfBlockTableRecord block)
        {
            DxfItem visibility = Nodes(block).Single(n => n.Type == "BLOCKVISIBILITYPARAMETER");
            var listed = new Dictionary<string, List<string>>();
            List<string> state = null;

            foreach (DxfTypedValue group in visibility.Groups)
            {
                if (group.TypeCode == 303)
                {
                    state = new List<string>();
                    listed.Add(group.Value, state);
                }
                else if (group.TypeCode == 333)
                {
                    state.Add(group.Value);
                }
            }

            return listed;
        }

        /// <summary>A drawing's text with the state of a name made to list nothing (95 of nought), as a state that hides every parameter.</summary>
        private static string Emptied(string text, string state)
        {
            string[] lines = text.Split('\n');
            int at = 0;

            while (!(lines[at].Trim() == "303" && lines[at + 1] == state))
            {
                at++;
            }

            at = Array.FindIndex(lines, at, l => l.Trim() == "95");
            int count = int.Parse(lines[at + 1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            IEnumerable<string> kept = lines.Take(at + 1).Concat(new[] { "0" }).Concat(lines.Skip(at + 2 + 2 * count));
            return string.Join("\n", kept);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AParameterAddedAfterOrBeforeTheStates_IsShownInEveryState_ItsGripWithIt(bool widthFirst)
        {
            DxfDatabase db = SignWithWidth(widthFirst, out DxfBlockTableRecord block);

            foreach (DxfDatabase drawing in new[] { db, Reopened(db) })
            {
                DxfBlockReference insert = TheInsert(drawing);

                foreach (string state in new[] { "Cross", "Tick", "Blank" })
                {
                    Property(insert, "Mark").Value = state;

                    Assert.True(Property(insert, "Width").VisibleInCurrentVisibilityState);
                    Assert.Contains(insert.GetDynamicGrips(), g =>
                        g.GripType == DxfDynamicBlockGripType.Linear && At(g.Position, 40, 0)
                    );
                }
            }
        }

        [Fact]
        public void EveryState_ListsTheOtherParametersTheirGripsAndTheActions_AsAutoCADsDoorSampleDoes()
        {
            DxfDatabase db = SignWithWidth(false, out DxfBlockTableRecord block);

            // The Door sample's states each list every parameter but the visibility parameter and the base point, every grip
            // but the visibility grip and every action, in the order of the nodes, and no grip location component.
            string[] expected = Nodes(block)
                .Where(n =>
                    n.Type == "BLOCKLINEARPARAMETER" || n.Type == "BLOCKLINEARGRIP" || n.Type == "BLOCKSTRETCHACTION"
                )
                .Select(n => n.Handle)
                .ToArray();
            Assert.Equal(4, expected.Length);

            foreach (DxfBlockTableRecord read in new[] { block, Reopened(db).BlockTable["GH_SIGN"] })
            {
                Dictionary<string, List<string>> listed = Listed(read);
                Assert.Equal(new[] { "Cross", "Tick", "Blank" }, listed.Keys);
                Assert.All(listed.Values, handles => Assert.Equal(expected, handles));
            }
        }

        [Fact]
        public void AParameterTheStateShownDoesNotList_IsHidden_ItsGripWithIt()
        {
            DxfDatabase db = SignWithWidth(false, out DxfBlockTableRecord block);

            // Cross and Blank made to list nothing, as AutoCAD's Block Editor hides a parameter in a state: Width and its grip
            // show in Tick alone, as AutoCAD 2021 showed no grip of the Angle of its Toilet sample in a state that did not list
            // it (plugin GhDyn1).
            DxfDatabase listed = DxfDatabase.Parse(Emptied(Emptied(db.ToText(), "Cross"), "Blank"));
            DxfBlockReference insert = TheInsert(listed);
            Assert.False(Property(insert, "Width").VisibleInCurrentVisibilityState);
            Assert.True(Property(insert, "Width").Show);
            Assert.True(Property(insert, "Mark").VisibleInCurrentVisibilityState);
            Assert.Equal(
                new[] { DxfDynamicBlockGripType.Visibility },
                insert.GetDynamicGrips().Select(g => g.GripType)
            );

            Property(insert, "Mark").Value = "Tick";

            Assert.True(Property(insert, "Width").VisibleInCurrentVisibilityState);
            Assert.Equal(
                new[] { DxfDynamicBlockGripType.Visibility, DxfDynamicBlockGripType.Linear },
                insert.GetDynamicGrips().Select(g => g.GripType)
            );

            // A parameter added afterwards is listed in every state, the emptied ones with the others, the lists kept.
            DxfBlockTableRecord sign = listed.BlockTable["GH_SIGN"];
            sign.AddPointParameter("Tag", null, new GeoPoint3(20, 20, 0));
            Dictionary<string, List<string>> lists = Listed(sign);
            Assert.Equal(2, lists["Cross"].Count);
            Assert.Equal(6, lists["Tick"].Count);
            Assert.Equal(lists["Cross"], lists["Tick"].Skip(4));
            Assert.Equal(lists["Cross"], lists["Blank"]);
        }

        [Fact]
        public void AnXYParameter_GivesItsEndAndItsTwoOtherCorners_AsAutoCADShowsThem()
        {
            DxfDatabase db = Drawing("GH_XY", out DxfBlockTableRecord block, new GeoPoint3(1000, 500, 0));
            DxfLine right = Add(block, new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)));
            DxfLine top = Add(block, new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)));
            DxfBlockXYParameter xy = block.AddXYParameter(
                "Width",
                null,
                "Height",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 50, 0)
            );
            block.AddStretchAction(xy, new GeoPoint2(90, 40), new GeoPoint2(110, 60), new DxfEntity[] { right, top });

            // AutoCAD 2021 gave the end, base x with end y and end x with base y, and no base grip (plugin GhDb1).
            IReadOnlyList<DxfDynamicBlockGrip> grips = TheInsert(db).GetDynamicGrips();
            Assert.All(grips, g => Assert.Equal(DxfDynamicBlockGripType.XY, g.GripType));
            Assert.All(grips, g => Assert.Equal(new[] { "Width", "Height" }, g.PropertyNames));
            Assert.All(grips, g => Assert.True(At(g.BasePoint, 1000, 500)));
            Assert.Equal(3, grips.Count);
            Assert.True(At(grips[0].Position, 1100, 550));
            Assert.True(At(grips[1].Position, 1000, 550));
            Assert.True(At(grips[2].Position, 1100, 500));

            DxfBlockReference insert = TheInsert(db);
            Property(insert, "Width").Value = 120.0;
            Property(insert, "Height").Value = 60.0;

            grips = TheInsert(Reopened(db)).GetDynamicGrips();
            Assert.True(At(grips[0].Position, 1120, 560));
            Assert.True(At(grips[1].Position, 1000, 560));
            Assert.True(At(grips[2].Position, 1120, 500));
        }

        [Fact]
        public void ABlockPropertiesTable_GivesItsGripAtItsLocation_AfterTheGripsOfItsColumns()
        {
            DxfDatabase db = Drawing("GH_PLATE", out DxfBlockTableRecord block, new GeoPoint3(1000, 500, 0));
            DxfLine right = Add(block, new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)));
            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -10,
                null
            );
            block.AddStretchAction(width, new GeoPoint2(90, -5), new GeoPoint2(110, 60), new[] { right });
            block.AddBlockPropertiesTable(
                new GeoPoint3(0, -20, 0),
                new[] { DxfBlockPropertiesTableColumn.Distance(width) },
                new[] { new DxfBlockPropertiesTableRow(100.0), new DxfBlockPropertiesTableRow(200.0) }
            );

            // AutoCAD 2021 gave the table's grip at (1 000, 480), after Width's (plugin GhDb1).
            IReadOnlyList<DxfDynamicBlockGrip> grips = TheInsert(Reopened(db)).GetDynamicGrips();
            Assert.Equal(
                new[] { DxfDynamicBlockGripType.Linear, DxfDynamicBlockGripType.BlockTable },
                grips.Select(g => g.GripType)
            );
            Assert.True(At(grips[1].Position, 1000, 480));
            Assert.Equal(new[] { "Block Table1" }, grips[1].PropertyNames);
        }

        [Fact]
        public void ThePointGrip_FollowsThePosition_AndAPlainInsertHasNoGrips()
        {
            DxfDatabase db = Drawing("GH_TAG", out DxfBlockTableRecord block, new GeoPoint3(0, 0, 0));
            DxfCircle circle = Add(block, new DxfCircle(new GeoPoint3(80, 20, 0), GeoVector3.ZAxis, 5));
            DxfBlockPointParameter tag = block.AddPointParameter("Tag", null, new GeoPoint3(80, 20, 0));
            block.AddMoveAction(tag, new[] { circle });
            DxfBlockReference insert = TheInsert(db);
            Property(insert, "Tag X").Value = 100.0;

            DxfDynamicBlockGrip grip = Assert.Single(TheInsert(Reopened(db)).GetDynamicGrips());
            Assert.Equal(DxfDynamicBlockGripType.Point, grip.GripType);
            Assert.Equal(new[] { "Tag X", "Tag Y" }, grip.PropertyNames);
            Assert.True(At(grip.Position, 100, 20));

            var plain = new DxfBlockTableRecord { Name = "PLAIN", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(plain);
            var reference = new DxfBlockReference(new GeoPoint3(0, 0, 0), plain);
            db.ModelSpace.AppendEntity(reference);
            Assert.Empty(reference.GetDynamicGrips());
        }

        [Fact]
        public void TheLookupGrip_StandsAtItsLocation_BesideTheGripsOfItsInputs()
        {
            DxfDatabase db = Panel(new GeoPoint3(0, 0, 0), out DxfBlockTableRecord block);
            DxfBlockLinearParameter depth = block.AddLinearParameter(
                "Depth",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(0, 50, 0),
                20,
                null
            );
            block.AddLookupParameter(
                "Size",
                "Panel size",
                new GeoPoint3(50, -20, 0),
                new[] { DxfBlockLookupInput.Distance(depth) },
                new[] { new DxfBlockLookupRow("Low", 50), new DxfBlockLookupRow("High", 80) }
            );

            IReadOnlyList<DxfDynamicBlockGrip> grips = TheInsert(db).GetDynamicGrips();
            Assert.Equal(
                new[]
                {
                    DxfDynamicBlockGripType.Linear,
                    DxfDynamicBlockGripType.Linear,
                    DxfDynamicBlockGripType.Lookup,
                },
                grips.Select(g => g.GripType)
            );
            Assert.True(At(grips[2].Position, 50, -20));
            Assert.Equal(new[] { "Size" }, grips[2].PropertyNames);
        }
    }
}

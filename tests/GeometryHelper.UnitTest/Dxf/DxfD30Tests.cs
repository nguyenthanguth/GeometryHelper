using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The faults the dynamic-block samples of D29 found, each as AutoCAD 2021 wrote, read or set it (plugin D30, rounds x1 and
    /// x2): the second point of a justified text at the origin, the order of an array's copies, and attribute definitions in
    /// a move, a scale, a stretch and a rotate.
    /// </summary>
    public class DxfD30Tests
    {
        private static readonly Tolerance Exact = new Tolerance(1E-9, 1E-9);

        private static T Add<T>(DxfBlockTableRecord block, T entity)
            where T : DxfEntity
        {
            block.AppendEntity(entity);
            return entity;
        }

        private static DxfDatabase Reopened(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        private static DxfBlockTableRecord NewBlock(DxfDatabase db, string name)
        {
            var block = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            return block;
        }

        /// <summary>An INSERT of the block with an ATTRIB of each of its attribute definitions, filled from it where it stands.</summary>
        private static DxfBlockReference Place(
            DxfDatabase db,
            DxfBlockTableRecord block,
            double x,
            double y,
            double rotation = 0,
            double scale = 1
        )
        {
            var insert = new DxfBlockReference(new GeoPoint3(x, y, 0), block)
            {
                Rotation = rotation,
                ScaleFactors = new GeoVector3(scale, scale, scale),
            };
            db.ModelSpace.AppendEntity(insert);

            foreach (DxfAttributeDefinition definition in block.OfType<DxfAttributeDefinition>().ToList())
            {
                var attribute = new DxfAttributeReference();
                attribute.SetAttributeFromBlock(definition, insert.BlockTransform);
                insert.AttributeCollection.AppendAttribute(attribute);
            }

            return insert;
        }

        private static List<DxfBlockReference> Inserts(DxfDatabase db) =>
            db.ModelSpace.OfType<DxfBlockReference>().ToList();

        private static void Set(DxfBlockReference insert, string name, object value) =>
            insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == name).Value = value;

        private static DxfAttributeReference Attribute(DxfBlockReference insert, string tag) =>
            insert.AttributeCollection.Single(a => a.Tag == tag);

        private static bool At(GeoPoint3 point, double x, double y) => point.IsEqualTo(new GeoPoint3(x, y, 0), Exact);

        /// <summary>The number a record of a text gives a code, read as the file holds it; null where it gives none.</summary>
        private static double? Group(DxfEntity entity, int code)
        {
            List<DxfTypedValue> found = entity.Item.Groups.Where(g => g.TypeCode == code).ToList();
            return found.Count == 0 ? (double?)null : double.Parse(found[0].Value, CultureInfo.InvariantCulture);
        }

        // ---- the second point of a justified text --------------------------------------------------------------------------

        [Fact]
        public void AJustifiedDefinitionAtTheOrigin_GivesItsAttributeTheAlignmentTheInsertPlacesItAt()
        {
            // AutoCAD 2021 wrote the ATTDEF centred on (0, 0) with 11 0 21 0 31 0, and the ATTRIB SetAttributeFromBlock gave an
            // INSERT at (6000, 1000) with 11 6000 21 1000 (plugin D30, x1); the library wrote neither, so AutoCAD put the
            // attribute at the world's origin.
            var db = new DxfDatabase("AC1032");
            DxfBlockTableRecord block = NewBlock(db, "D");
            DxfAttributeDefinition tag = Add(
                block,
                new DxfAttributeDefinition(new GeoPoint3(0, 0, 0), "A", "AXIS", "Axis", null) { Height = 100 }
            );
            tag.Justify = DxfAttachmentPoint.MiddleCenter;
            tag.AlignmentPoint = new GeoPoint3(0, 0, 0);
            Place(db, block, 6000, 1000);

            DxfDatabase back = Reopened(db);
            DxfAttributeDefinition definition = back.BlockTable["D"].OfType<DxfAttributeDefinition>().Single();
            Assert.Equal(
                new double?[] { 0, 0, 0 },
                new[] { Group(definition, 11), Group(definition, 21), Group(definition, 31) }
            );
            DxfAttributeReference attribute = Attribute(Inserts(back)[0], "AXIS");
            Assert.True(At(attribute.AlignmentPoint, 6000, 1000), attribute.AlignmentPoint.ToString());
            Assert.Equal(new double?[] { 6000, 1000 }, new[] { Group(attribute, 11), Group(attribute, 21) });
        }

        [Fact]
        public void AnAttributeOfAJustifiedDefinitionOfNoSecondPoint_IsHeldAtWhereTheInsertPlacesTheOrigin()
        {
            // A justified ATTDEF of another writer, of no 11, which AutoCAD reads as held by the origin: its ATTRIB at an INSERT
            // turned a quarter turn at (200, 300) is held by (200, 300), and at an INSERT at the origin writes 11 too.
            var db = new DxfDatabase("AC1032");
            DxfBlockTableRecord block = NewBlock(db, "D");
            DxfAttributeDefinition tag = Add(
                block,
                new DxfAttributeDefinition(new GeoPoint3(0, 0, 0), "A", "AXIS", "Axis", null) { Height = 100 }
            );
            tag.Justify = DxfAttachmentPoint.MiddleCenter;
            tag.Item.File.ReplaceTail(
                tag.Item,
                0,
                tag.Item.File.TailOf(tag.Item, 0)
                    .Where(g => g.TypeCode != 11 && g.TypeCode != 21 && g.TypeCode != 31)
                    .Select(g => new KeyValuePair<int, string>(g.TypeCode, g.Value))
                    .ToList()
            );
            Assert.Null(Group(tag, 11));

            DxfAttributeReference turned = Attribute(Place(db, block, 200, 300, Math.PI / 2), "AXIS");
            DxfAttributeReference still = Attribute(Place(db, block, 0, 0), "AXIS");

            Assert.True(At(turned.AlignmentPoint, 200, 300), turned.AlignmentPoint.ToString());
            Assert.Equal(new double?[] { 200, 300 }, new[] { Group(turned, 11), Group(turned, 21) });
            Assert.Equal(new double?[] { 0, 0, 0 }, new[] { Group(still, 11), Group(still, 21), Group(still, 31) });
        }

        [Fact]
        public void ATextMadeJustified_WritesItsSecondPoint_AtTheOriginWhereNoneIsGiven_AsAutoCadWritesIt()
        {
            // AutoCAD 2021 wrote 11 0 21 0 31 0 for a TEXT at (500, 500) made centred on its middle with no alignment point
            // given, and no 11 for a text left on its baseline (plugin D30, x1).
            var centred = new DxfDBText
            {
                Position = new GeoPoint3(500, 500, 0),
                Height = 100,
                TextString = "T2",
            };
            centred.Justify = DxfAttachmentPoint.MiddleCenter;
            var right = new DxfDBText
            {
                Position = new GeoPoint3(500, 500, 0),
                Height = 100,
                TextString = "T5",
            };
            right.HorizontalMode = DxfTextHorizontalMode.TextRight;
            var top = new DxfDBText
            {
                Position = new GeoPoint3(500, 500, 0),
                Height = 100,
                TextString = "T6",
            };
            top.VerticalMode = DxfTextVerticalMode.TextTop;
            var left = new DxfDBText { Height = 100, TextString = "T3" };
            left.AlignmentPoint = new GeoPoint3(0, 0, 0);

            foreach (DxfDBText text in new[] { centred, right, top })
            {
                Assert.Equal(new double?[] { 0, 0, 0 }, new[] { Group(text, 11), Group(text, 21), Group(text, 31) });
                Assert.True(At(text.AlignmentPoint, 0, 0));
                Assert.True(At(text.Position, 500, 500));
            }

            Assert.Null(Group(left, 11));
        }

        [Fact]
        public void AJustifiedTextOfNoSecondPoint_IsMovedFromTheOrigin()
        {
            // A TEXT centred on its middle whose record gives no 11 is held by the origin, as AutoCAD reads it: moved by (30, 40),
            // it is held by (30, 40).
            var db = new DxfDatabase("AC1032");
            var text = new DxfDBText
            {
                Position = new GeoPoint3(5, 5, 0),
                Height = 2.5,
                TextString = "M",
            };
            db.ModelSpace.AppendEntity(text);
            text.Justify = DxfAttachmentPoint.MiddleCenter;
            text.Item.File.ReplaceTail(
                text.Item,
                0,
                text.Item.File.TailOf(text.Item, 0)
                    .Where(g => g.TypeCode != 11 && g.TypeCode != 21 && g.TypeCode != 31)
                    .Select(g => new KeyValuePair<int, string>(g.TypeCode, g.Value))
                    .ToList()
            );

            text.TransformBy(GeoTransform3.Translation(new GeoVector3(30, 40, 0)));

            Assert.True(At(text.AlignmentPoint, 30, 40), text.AlignmentPoint.ToString());
            Assert.True(At(text.Position, 35, 45));
        }

        // ---- the order of an array's copies ----------------------------------------------------------------------------------

        [Fact]
        public void AnArraysCopies_FollowTheBlocksEntitiesCellByCell_InTheOrderOfItsSelection_AsAutoCadDrawsThem()
        {
            // A circle C, a line L and a circle D; Length 40 arrays L and C (given in that order) every 30, and D every 50. Set
            // to 160, AutoCAD 2021 drew C, L, D, then L and C at 30, 60, 90 and 120, then D at 50 and 100 (plugin D30, x1).
            var db = new DxfDatabase("AC1032");
            DxfBlockTableRecord block = NewBlock(db, "BARR1");
            DxfCircle c = Add(block, new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5));
            DxfLine l = Add(block, new DxfLine(new GeoPoint3(0, -5, 0), new GeoPoint3(0, 5, 0)));
            DxfCircle d = Add(block, new DxfCircle(new GeoPoint3(0, 20, 0), GeoVector3.ZAxis, 3));
            DxfBlockLinearParameter length = block.AddLinearParameter(
                "Length",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(40, 0, 0),
                -20,
                null
            );
            block.AddArrayAction(length, new DxfEntity[] { l, c }, 30);
            block.AddArrayAction(length, new DxfEntity[] { d }, 50);
            DxfBlockReference insert = Place(db, block, 0, 0);

            Set(insert, "Length", 160.0);

            string[] drawn = Reopened(db)
                .ModelSpace.OfType<DxfBlockReference>()
                .Single()
                .BlockTableRecord.Select(e =>
                    e is DxfCircle circle
                        ? "C"
                            + circle.Center.X.ToString(CultureInfo.InvariantCulture)
                            + (circle.Radius == 3 ? "d" : string.Empty)
                        : "L" + ((DxfLine)e).StartPoint.X.ToString(CultureInfo.InvariantCulture)
                )
                .ToArray();
            Assert.Equal(
                new[] { "C0", "L0", "C0d", "L30", "C30", "L60", "C60", "L90", "C90", "L120", "C120", "C50d", "C100d" },
                drawn
            );
        }

        [Fact]
        public void AnXYArraysCopies_RunRowByRow_EachCellInTheOrderOfItsSelection()
        {
            // A line and a circle, the circle given first, arrayed every 30 by 20 by an XY parameter to (-40, -30): Width 90
            // and Depth 60 drew the circle and the line of (-30, 0), (-60, 0), then of each column of the rows at -20 and -40
            // (plugin D30, x1).
            var db = new DxfDatabase("AC1032");
            DxfBlockTableRecord block = NewBlock(db, "BARR2");
            DxfLine l = Add(block, new DxfLine(new GeoPoint3(-5, 0, 0), new GeoPoint3(5, 0, 0)));
            DxfCircle c = Add(block, new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 4));
            DxfBlockXYParameter xy = block.AddXYParameter(
                "Width",
                null,
                "Depth",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(-40, -30, 0)
            );
            block.AddArrayAction(xy, new DxfEntity[] { c, l }, 30, 20);
            DxfBlockReference insert = Place(db, block, 0, 0);

            Set(insert, "Width", 90.0);
            Set(insert, "Depth", 60.0);

            string[] drawn = Reopened(db)
                .ModelSpace.OfType<DxfBlockReference>()
                .Single()
                .BlockTableRecord.Select(e =>
                    e is DxfCircle circle
                        ? "C"
                            + circle.Center.X.ToString(CultureInfo.InvariantCulture)
                            + ","
                            + circle.Center.Y.ToString(CultureInfo.InvariantCulture)
                        : "L"
                            + (((DxfLine)e).StartPoint.X + 5).ToString(CultureInfo.InvariantCulture)
                            + ","
                            + ((DxfLine)e).StartPoint.Y.ToString(CultureInfo.InvariantCulture)
                )
                .ToArray();
            Assert.Equal(
                new[]
                {
                    "L0,0",
                    "C0,0",
                    "C-30,0",
                    "L-30,0",
                    "C-60,0",
                    "L-60,0",
                    "C0,-20",
                    "L0,-20",
                    "C-30,-20",
                    "L-30,-20",
                    "C-60,-20",
                    "L-60,-20",
                    "C0,-40",
                    "L0,-40",
                    "C-30,-40",
                    "L-30,-40",
                    "C-60,-40",
                    "L-60,-40",
                },
                drawn
            );
        }

        // ---- attribute definitions in actions --------------------------------------------------------------------------------

        /// <summary>
        /// A line from the origin to (100, 0), a circle of the radius 5 at its end, the ATTDEF LEFT at (100, 10) on its baseline
        /// and the ATTDEF MID centred on (100, -10), both 5 high and locked as asked; a Width from (0, 0) to (100, 0), or an
        /// Angle about the origin for a rotate, whose action of the kind holds the circle, the two ATTDEFs and, but for a move,
        /// the line. INSERTs at the origin and, turned a quarter turn at the scale 2, at (500, 0).
        /// </summary>
        private static DxfDatabase Acted(
            string kind,
            bool locked,
            out DxfAttributeDefinition left,
            out DxfAttributeDefinition mid
        )
        {
            var db = new DxfDatabase("AC1032");
            DxfBlockTableRecord block = NewBlock(db, "B" + kind);
            DxfLine line = Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            DxfCircle circle = Add(block, new DxfCircle(new GeoPoint3(100, 0, 0), GeoVector3.ZAxis, 5));
            left = Add(
                block,
                new DxfAttributeDefinition(new GeoPoint3(100, 10, 0), "L1", "LEFT", "Left", null)
                {
                    Height = 5,
                    LockPositionInBlock = locked,
                }
            );
            mid = Add(
                block,
                new DxfAttributeDefinition(new GeoPoint3(100, -10, 0), "M1", "MID", "Mid", null)
                {
                    Height = 5,
                    LockPositionInBlock = locked,
                }
            );
            mid.Justify = DxfAttachmentPoint.MiddleCenter;
            mid.AlignmentPoint = new GeoPoint3(100, -10, 0);
            var held = new DxfEntity[] { line, circle, left, mid };

            switch (kind)
            {
                case "MOVE":
                    block.AddMoveAction(
                        block.AddLinearParameter(
                            "Width",
                            null,
                            new GeoPoint3(0, 0, 0),
                            new GeoPoint3(100, 0, 0),
                            -20,
                            null
                        ),
                        held.Skip(1)
                    );
                    break;
                case "SCALE":
                    block.AddScaleAction(
                        block.AddLinearParameter(
                            "Width",
                            null,
                            new GeoPoint3(0, 0, 0),
                            new GeoPoint3(100, 0, 0),
                            -20,
                            null
                        ),
                        held
                    );
                    break;
                case "STRETCH":
                    block.AddStretchAction(
                        block.AddLinearParameter(
                            "Width",
                            null,
                            new GeoPoint3(0, 0, 0),
                            new GeoPoint3(100, 0, 0),
                            -20,
                            null
                        ),
                        new GeoPoint2(90, -20),
                        new GeoPoint2(110, 20),
                        held
                    );
                    break;
                default:
                    block.AddRotateAction(
                        block.AddRotationParameter("Angle", null, new GeoPoint3(0, 0, 0), 100, 0),
                        held
                    );
                    break;
            }

            Place(db, block, 0, 0);
            Place(db, block, 500, 0, Math.PI / 2, 2);
            return Reopened(db);
        }

        /// <summary>The ATTRIBs LEFT and MID of an INSERT read back: LEFT's position, MID's alignment point, their height and rotation.</summary>
        private static void Shows(
            DxfBlockReference insert,
            double lx,
            double ly,
            double mx,
            double my,
            double height,
            double rotation
        )
        {
            DxfAttributeReference left = Attribute(insert, "LEFT");
            DxfAttributeReference mid = Attribute(insert, "MID");
            Assert.True(At(left.Position, lx, ly), "LEFT stands at " + left.Position);
            Assert.True(At(mid.AlignmentPoint, mx, my), "MID is held by " + mid.AlignmentPoint);
            Assert.Equal(height, left.Height, 9);
            Assert.Equal(height, mid.Height, 9);
            Assert.Equal(rotation, left.Rotation, 9);
            Assert.Equal(rotation, mid.Rotation, 9);
        }

        [Theory]
        [InlineData("MOVE", 150, 10, 150, -10, 5, 480, 150, 520, 150, 10)]
        [InlineData("SCALE", 150, 15, 150, -15, 7.5, 485, 150, 515, 150, 7.5)]
        [InlineData("STRETCH", 150, 10, 150, -10, 5, 480, 150, 520, 150, 10)]
        public void ALockedAttributeDefinitionAnActionHolds_TakesTheInsertsAttributeAlong_AsAutoCadDid(
            string kind,
            double lx,
            double ly,
            double mx,
            double my,
            double h,
            double tlx,
            double tly,
            double tmx,
            double tmy,
            double th
        )
        {
            // Width set to 150 on each INSERT, as AutoCAD 2021 set the library's blocks (plugin D30, x2): the INSERT at the
            // origin and the one turned a quarter turn at the scale 2 at (500, 0), whose Width reads in the world's units.
            DxfDatabase db = Acted(kind, true, out _, out _);
            List<DxfBlockReference> inserts = Inserts(db);

            Set(inserts[0], "Width", 150.0);
            Set(inserts[1], "Width", 150.0);

            List<DxfBlockReference> back = Inserts(Reopened(db));
            Shows(back[0], lx, ly, mx, my, h, 0);
            Shows(back[1], tlx, tly, tmx, tmy, th, Math.PI / 2);
        }

        [Fact]
        public void ALockedAttributeDefinitionAScaleHolds_FollowsEachSetFromWhereTheLastLeftIt()
        {
            // Width 200 then 120 on the INSERT turned at the scale 2: LEFT at (480, 200) and 10 high, then (488, 120) and 6
            // high, as AutoCAD 2021 drew it (plugin D30, x2).
            DxfDatabase db = Acted("SCALE", true, out _, out _);
            DxfBlockReference turned = Inserts(db)[1];

            Set(turned, "Width", 200.0);
            Set(turned, "Width", 120.0);

            Shows(Inserts(Reopened(db))[1], 488, 120, 512, 120, 6, Math.PI / 2);
        }

        [Fact]
        public void ALockedAttributeDefinitionARotateHolds_TurnsTheInsertsAttribute_AsAutoCadDid()
        {
            // Angle a quarter turn: LEFT goes from (100, 10) to (-10, 100), turned a quarter turn, and MID's alignment point to
            // (10, 100); on the INSERT turned at the scale 2, to (300, -20) and (300, 20), turned a half turn (plugin D30, x2).
            DxfDatabase db = Acted("ROTATE", true, out _, out _);
            List<DxfBlockReference> inserts = Inserts(db);

            Set(inserts[0], "Angle", Math.PI / 2);
            Set(inserts[1], "Angle", Math.PI / 2);

            List<DxfBlockReference> back = Inserts(Reopened(db));
            Shows(back[0], -10, 100, 10, 100, 5, Math.PI / 2);
            Shows(back[1], 300, -20, 300, 20, 10, Math.PI);
            Assert.True(
                At(
                    back[0].BlockTableRecord.OfType<DxfAttributeDefinition>().Single(a => a.Tag == "LEFT").Position,
                    -10,
                    100
                )
            );
        }

        [Theory]
        [InlineData("MOVE")]
        [InlineData("SCALE")]
        [InlineData("STRETCH")]
        [InlineData("ROTATE")]
        public void AnAttributeDefinitionWhosePositionIsNotLocked_IsRefusedInAnAction(string kind)
        {
            // AutoCAD 2021 left an unlocked ATTDEF of the library's move, scale and rotate where it was, and the ATTRIB of one a
            // stretch held (plugin D30, x1), as its Block Editor takes only an attribute locked in its block into an action.
            Assert.Throws<ArgumentException>(() => Acted(kind, false, out _, out _));
        }

        [Fact]
        public void AnUnlockedAttributeDefinitionOfAnotherWritersMove_StaysAndItsAttributeWithIt_AsAutoCadLeftThem()
        {
            // The move written of locked ATTDEFs whose lock another writer then took off: Width 150 moves the circle to (150, 0)
            // and leaves the ATTDEFs and the ATTRIBs where they were, as AutoCAD 2021 left the library's (plugin D30, x1).
            DxfDatabase db = Acted("MOVE", true, out _, out _);

            foreach (DxfAttributeDefinition definition in db.BlockTable["BMOVE"].OfType<DxfAttributeDefinition>())
            {
                definition.LockPositionInBlock = false;
            }

            Set(Inserts(db)[0], "Width", 150.0);

            DxfBlockReference back = Inserts(Reopened(db))[0];
            Shows(back, 100, 10, 100, -10, 5, 0);
            Assert.True(
                At(
                    back.BlockTableRecord.OfType<DxfAttributeDefinition>().Single(a => a.Tag == "LEFT").Position,
                    100,
                    10
                )
            );
            Assert.True(At(back.BlockTableRecord.OfType<DxfCircle>().Single().Center, 150, 0));
        }

        [Fact]
        public void AnUnlockedAttributeDefinitionOfAnotherWritersStretch_MovesInTheBlock_ButNotItsAttribute_AsAutoCadDid()
        {
            // Width 150: AutoCAD 2021 moved the unlocked ATTDEF of a stretch to (150, 10) in the anonymous block and left the
            // INSERT's ATTRIB at (100, 10) (plugin D30, x1).
            DxfDatabase db = Acted("STRETCH", true, out _, out _);

            foreach (DxfAttributeDefinition definition in db.BlockTable["BSTRETCH"].OfType<DxfAttributeDefinition>())
            {
                definition.LockPositionInBlock = false;
            }

            Set(Inserts(db)[0], "Width", 150.0);

            DxfBlockReference back = Inserts(Reopened(db))[0];
            Shows(back, 100, 10, 100, -10, 5, 0);
            Assert.True(
                At(
                    back.BlockTableRecord.OfType<DxfAttributeDefinition>().Single(a => a.Tag == "LEFT").Position,
                    150,
                    10
                )
            );
        }
    }
}

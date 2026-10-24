using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfDBTextTests;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The INSERT as a <see cref="DxfBlockReference"/>: read as ezdxf 1.4.5 reads it, set and read back to the bit, its ATTRIBs
    /// following it, made in code and appended, refused where its block would insert itself, erased with its ATTRIBs, moved as
    /// ezdxf moves one, and exploded one level down.
    /// </summary>
    public class DxfBlockReferenceTests
    {
        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        #region Read as ezdxf reads it

        [Fact]
        public void TheInsertsOfEzdxf_AreReadAsEzdxfReadsThem()
        {
            // As ezdxf 1.4.5 reads the INSERT 65 of R2000 (68 of R12): BOLT at (50, 60, 0), turned by 45 degrees, scaled by
            // (1.5, -1.5, 2), on steel in colour 3, with the ATTRIB MARK of B-12; its matrix44() moves by (46.81801948466054,
            // 61.06066017177982, 0), the block's origin being (1, 2, 0).
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfBlockReference bolt = Everything(db).OfType<DxfBlockReference>().Single(b => b.Owner == db.ModelSpace && b.Name == "BOLT" && !(b is DxfMInsertBlock));

                Assert.Equal("BOLT", bolt.Name);
                Assert.Same(db.ModelSpace, bolt.Owner);
                Assert.Same(db.BlockTable["BOLT"], bolt.BlockTableRecord);
                Assert.Equal(Bits(new GeoPoint3(50.0, 60.0, 0.0)), Bits(bolt.Position));
                Assert.Equal(Math.PI / 4.0, bolt.Rotation);
                Assert.Equal(Bits(new GeoVector3(1.5, -1.5, 2.0)), Bits(bolt.ScaleFactors));
                Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(bolt.Normal));
                Assert.Equal("steel", bolt.Layer);
                Assert.Equal(3, bolt.ColorIndex);

                GeoTransform3 m = bolt.BlockTransform;
                Near(1.0606601717798214, m[0, 0]);
                Near(1.0606601717798214, m[1, 0]);
                Near(1.0606601717798214, m[0, 1]);
                Near(-1.0606601717798214, m[1, 1]);
                Near(2.0, m[2, 2]);
                Near(46.81801948466054, m[0, 3]);
                Near(61.06066017177982, m[1, 3]);

                DxfAttributeReference mark = Assert.Single(bolt.AttributeCollection);
                Assert.Equal("MARK", mark.Tag);
                Assert.Equal("B-12", mark.TextString);
                Assert.Same(mark, bolt.AttributeCollection[0]);
            }
        }

        [Fact]
        public void AnInsertOnATiltedNormal_StandsInTheWorld_AndItsShapesAreTheReadersPlaced()
        {
            // The INSERT 68 of FRAME at (200, 0, 0) on the normal (0, 0.6, 0.8), turned by -90 degrees: the arbitrary axis
            // algorithm takes x to (-1, 0, 0), so it stands at (-200, 0, 0), as ezdxf's matrix44() moves it.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfBlockReference frame = ByHandle<DxfBlockReference>(db, "68");

            Near(new GeoPoint3(-200.0, 0.0, 0.0), frame.Position);
            Near(-Math.PI / 2.0, frame.Rotation);
            GeoTransform3 m = frame.BlockTransform;
            Near(0.8, m[1, 0]);
            Near(-0.6, m[2, 0]);
            Near(-1.0, m[0, 1]);
            Near(0.6, m[1, 2]);
            Near(-200.0, m[0, 3]);

            DxfDocument read = DxfReader.Parse(DxfFileEzdxfDrawings.R2000, new DxfReadOptions { Tolerance = Tolerance.Default });
            object[] theirs = read.Shapes.Where(s => s.InsertHandles.Count > 0 && s.InsertHandles[0] == "68").Select(s => s.Shape).ToArray();
            Assert.NotEmpty(theirs);
            Assert.Equal(theirs.Length, frame.GetGeometry().Count);
            Assert.Equal(theirs.Select(s => s.ToString()), frame.GetGeometry().Select(s => s.ToString()));
        }

        #endregion

        #region Nothing changed

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            string writer = DxfReaderRoundTripTests.OneOfEach().ToString();

            foreach (string text in Drawings.Concat(new[] { writer }))
            {
                DxfDatabase db = DxfDatabase.Parse(text);

                if (!Everything(db).OfType<DxfBlockReference>().Any())
                {
                    continue;
                }

                SettingWhatItHasChangesNothing<DxfBlockReference>(text, b =>
                {
                    if (b.BlockTableRecord != null)
                    {
                        b.BlockTableRecord = b.BlockTableRecord;
                    }

                    b.Position = b.Position;
                    b.Rotation = b.Rotation;
                    b.ScaleFactors = b.ScaleFactors;
                    b.Normal = b.Normal;
                    b.BlockTransform = b.BlockTransform;
                    b.Layer = b.Layer;
                    b.ColorIndex = b.ColorIndex;
                    b.XData = b.XData;
                    b.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        #endregion

        #region Changed

        [Fact]
        public void AnInsertMovedTurnedAndScaled_IsThatRecordWrittenAnew_ItsAttribCarried()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfBlockReference bolt = Everything(db).OfType<DxfBlockReference>().Single(b => b.Owner == db.ModelSpace && b.Name == "BOLT" && !(b is DxfMInsertBlock));
                DxfAttributeReference mark = bolt.AttributeCollection[0];
                string handle = bolt.Handle;
                string attribute = mark.Handle;

                // Moved by (10, -5, 1): the ATTRIB at (50, 59, 0) goes to (60, 54, 1).
                bolt.Position = new GeoPoint3(60.0, 55.0, 1.0);
                Near(new GeoPoint3(60.0, 54.0, 1.0), mark.Position);

                // Turned by 45 degrees more about the insertion point: the ATTRIB turns with it.
                bolt.Rotation = Math.PI / 2.0;
                Near(new GeoPoint3(60.0 + 1.0 * Math.Sin(Math.PI / 4.0), 55.0 - Math.Cos(Math.PI / 4.0), 1.0), mark.Position);
                Near(Math.PI / 4.0, mark.Rotation);

                bolt.ScaleFactors = new GeoVector3(3.0, 3.0, 3.0);
                bolt.Normal = new GeoVector3(0.0, 0.0, -1.0);

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, handle, attribute);

                DxfBlockReference again = ByHandle<DxfBlockReference>(DxfDatabase.Parse(saved), handle);
                Assert.Equal(Bits(new GeoPoint3(60.0, 55.0, 1.0)), Bits(again.Position));
                Assert.Equal(Bits(Math.PI / 2.0), Bits(again.Rotation));
                Assert.Equal(Bits(new GeoVector3(3.0, 3.0, 3.0)), Bits(again.ScaleFactors));
                Assert.Equal(Bits(new GeoVector3(0.0, 0.0, -1.0)), Bits(again.Normal));
                Assert.Equal("B-12", again.AttributeCollection[0].TextString);
            }
        }

        [Fact]
        public void AnInsertGivenAnotherBlock_PlacesIt_AndOneThatWouldInsertItselfIsRefused()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfBlockReference bolt = Everything(db).OfType<DxfBlockReference>().Single(b => b.Owner == db.ModelSpace && b.Name == "BOLT" && !(b is DxfMInsertBlock));
                DxfBlockTableRecord frame = db.BlockTable["FRAME"];
                DxfBlockReference inFrame = frame.OfType<DxfBlockReference>().Single();

                bolt.BlockTableRecord = frame;
                Assert.Equal("FRAME", bolt.Name);
                Assert.Same(frame, bolt.BlockTableRecord);
                Assert.Equal("FRAME", ByHandle<DxfBlockReference>(DxfDatabase.Parse(db.ToText()), bolt.Handle ?? "68").Name);

                Assert.Throws<ArgumentException>(() => inFrame.BlockTableRecord = frame);
                Assert.Throws<ArgumentException>(() => bolt.BlockTableRecord = db.ModelSpace);
                Assert.Throws<ArgumentException>(() => bolt.BlockTableRecord = new DxfBlockTableRecord { Name = "LOOSE" });
                Assert.Equal("BOLT", inFrame.Name);
            }
        }

        #endregion

        #region Moved

        [Fact]
        public void AnInsertTurnedOutOfItsPlane_IsMovedAsEzdxfMovesIt_ItsAttribWithIt()
        {
            // ezdxf 1.4.5, Insert.transform() of the INSERT 65 of R2000 by M1: insert (74.84974487098928, -4.639436940955621,
            // 6.5943981460456715) in the new system, rotation -2.121572440350411, scale (1.5, -1.5, 1.9999999999999998), extrusion
            // (0.3947397981737999, -0.07139249941787587, 0.9160150668873175); its ATTRIB at (74.11694572584005,
            // -5.319881951862876, 6.59439814604567), rotation -47.1215724403504, height 2.5.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfBlockReference bolt = ByHandle<DxfBlockReference>(db, "65");
            var normal = new GeoVector3(0.3947397981737999, -0.07139249941787587, 0.9160150668873175);

            bolt.TransformBy(Turn);

            DxfBlockReference again = ByHandle<DxfBlockReference>(DxfDatabase.Parse(db.ToText()), "65");
            Near(normal, again.Normal);
            Near(Ocs(normal, new GeoPoint3(74.84974487098928, -4.639436940955621, 6.5943981460456715)), again.Position);
            Near(-2.121572440350411 * (Math.PI / 180.0), again.Rotation);
            Near(new GeoVector3(1.5, -1.5, 1.9999999999999998), again.ScaleFactors);

            DxfAttributeReference mark = again.AttributeCollection[0];
            Near(Ocs(normal, new GeoPoint3(74.11694572584005, -5.319881951862876, 6.59439814604567)), mark.Position);
            Near(-47.1215724403504 * (Math.PI / 180.0), mark.Rotation);
            Near(2.5, mark.Height);

            var shear = new GeoTransform3(new double[,] { { 1, 1, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } });
            Assert.Throws<ArgumentException>(() => bolt.TransformBy(shear));
        }

        #endregion

        #region Exploded

        [Fact]
        public void AnInsertExploded_GivesItsBlocksEntitiesOneLevelDown_InNoFile()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfBlockReference frame = Everything(db).OfType<DxfBlockReference>().Single(b => b.Owner == db.ModelSpace && b.Name == "FRAME");
                DxfEntity[] inBlock = db.BlockTable["FRAME"].Where(e => !(e is DxfAttributeDefinition)).ToArray();

                var exploded = frame.Explode();

                Assert.Equal(inBlock.Length, exploded.Count);
                Assert.All(exploded, e => Assert.Null(e.Handle));
                DxfBlockReference bolt = exploded.OfType<DxfBlockReference>().Single();
                Assert.Equal("BOLT", bolt.Name);
                Assert.Equal("0", bolt.Layer);

                // The INSERT of BOLT in FRAME placed by FRAME's placing: its own placing after FRAME's.
                GeoTransform3 expected = frame.BlockTransform.Multiply(frame.BlockTableRecord.OfType<DxfBlockReference>().Single().BlockTransform);
                Near(expected.Transform(db.BlockTable["BOLT"].Origin), bolt.Position);

                db.ModelSpace.AppendEntity(bolt);
                Assert.NotNull(bolt.Owner);
            }
        }

        #endregion

        #region Made in code

        [Fact]
        public void AnInsertMadeInCode_IsAppendedToEachVersion_AndOneOfAMissingOrItsOwnBlockIsRefused()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var made = new DxfBlockReference(new GeoPoint3(11, 12, 0), db.BlockTable["BOLT"]) { Rotation = 0.2, ScaleFactors = new GeoVector3(2, 2, 2) };

                db.ModelSpace.AppendEntity(made);
                Assert.Same(db.ModelSpace, made.Owner);
                Assert.Empty(made.AttributeCollection);

                DxfBlockReference again = ByHandle<DxfBlockReference>(DxfDatabase.Parse(db.ToText()), made.Handle);
                Assert.Equal("BOLT", again.Name);
                Assert.Equal(Bits(new GeoPoint3(11, 12, 0)), Bits(again.Position));
                Assert.Equal(Bits(0.2), Bits(again.Rotation));
                Assert.Equal(Bits(new GeoVector3(2, 2, 2)), Bits(again.ScaleFactors));

                // A block of no drawing, a layout, and an INSERT of FRAME put in BOLT, which FRAME inserts, are refused.
                Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), new DxfBlockTableRecord { Name = "LOOSE" })));
                Assert.Throws<ArgumentException>(() => new DxfBlockReference(new GeoPoint3(0, 0, 0), db.ModelSpace));
                Assert.Throws<ArgumentException>(() => db.BlockTable["BOLT"].AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), db.BlockTable["FRAME"])));
                Assert.Throws<ArgumentException>(() => db.BlockTable["BOLT"].AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), db.BlockTable["BOLT"])));
                Assert.Throws<ArgumentException>(() => DxfDatabase.Parse(text).ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), db.BlockTable["BOLT"])));
            }
        }

        [Fact]
        public void AnInsertErased_TakesItsAttribsAndItsEndWithIt()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfBlockReference bolt = Everything(db).OfType<DxfBlockReference>().Single(b => b.Owner == db.ModelSpace && b.Name == "BOLT" && !(b is DxfMInsertBlock));
                DxfAttributeReference mark = bolt.AttributeCollection[0];
                int records = Records(text).Count;

                bolt.Erase();

                Assert.True(bolt.IsErased);
                Assert.True(mark.IsErased);
                Assert.Throws<InvalidOperationException>(() => mark.TextString = "x");
                Assert.Equal(records - 3, Records(db.ToText()).Count);
                Assert.DoesNotContain(Records(db.ToText()), r => r.Contains("\nB-12\n"));
            }
        }

        #endregion

        #region The image the reader places

        [Fact]
        public void AnInsertTurnedAQuarter_StretchedAlongTheWorldsX_StretchesItsBlocksY_AsTheReaderPlacesIt()
        {
            // BOLT turned by 90 degrees runs its own y along the world's -x: stretched twice along the world's x, it is its y
            // scale that doubles. Each LINE the reader placed lands where the stretch takes it.
            var stretch = GeoTransform3.Scaling(2.0, 1.0, 1.0);

            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfBlockReference bolt = Bolt(db);
                bolt.Rotation = Math.PI / 2.0;
                GeoLine3[] wanted = bolt.GetGeometry().OfType<GeoLine3>().Select(l => stretch.Transform(l)).ToArray();

                bolt.TransformBy(stretch);

                Assert.NotEmpty(wanted);
                SameLines(wanted, bolt.GetGeometry().OfType<GeoLine3>().ToArray());
                Near(1.5, bolt.ScaleFactors.X);
                Near(-3.0, bolt.ScaleFactors.Y);
            }
        }

        [Fact]
        public void AnInsertTurnedAnEighth_StretchedAlongTheWorldsX_IsRefused_ItsBlockWouldShear()
        {
            // BOLT turned by 45 degrees, stretched along the world's x: its own axes come out no longer square, which an INSERT
            // does not hold. Nothing is written.
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfBlockReference bolt = Bolt(db);

                Assert.Throws<ArgumentException>(() => bolt.TransformBy(GeoTransform3.Scaling(2.0, 1.0, 1.0)));
                Assert.Equal(new UTF8Encoding(false).GetBytes(text), Saved(db));
            }
        }

        [Fact]
        public void AnInsertExploded_WhoseTurnedInsertCannotHoldItsPlacing_GivesTheReadersImage()
        {
            // FRAME holds BOLT turned by 30 degrees: the INSERT of FRAME stretched along its own x shears that BOLT, which no
            // INSERT holds; it is exploded one level more, and the lines that come out are those the reader places.
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfBlockReference frame = Everything(db).OfType<DxfBlockReference>().Single(b => b.Owner == db.ModelSpace && b.Name == "FRAME");
                frame.ScaleFactors = new GeoVector3(2.0, 1.0, 1.0);
                GeoLine3[] wanted = frame.GetGeometry().OfType<GeoLine3>().ToArray();

                IReadOnlyList<DxfEntity> parts = frame.Explode();

                Assert.NotEmpty(wanted);
                Assert.DoesNotContain(parts, p => p is DxfBlockReference);
                SameLines(wanted, parts.OfType<DxfLine>().Select(l => new GeoLine3(l.StartPoint, l.EndPoint)).ToArray());
            }
        }

        [Fact]
        public void AnInsertExploded_GivesItsAttribsAsTexts_AndLeavesItsAttdefsOut()
        {
            // As ezdxf and AutoCAD's BURST: the ATTRIB MARK of B-12 comes out a TEXT of B-12 where it stood, on its layer.
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfBlockReference bolt = Bolt(db);
                DxfAttributeReference mark = bolt.AttributeCollection.Single();

                IReadOnlyList<DxfEntity> parts = bolt.Explode();

                DxfDBText value = Assert.Single(parts.OfType<DxfDBText>());
                Assert.IsType<DxfDBText>(value);
                Assert.Equal("B-12", value.TextString);
                Assert.Equal(Bits(mark.Position), Bits(value.Position));
                Assert.Equal(mark.Height, value.Height);
                Assert.Equal("steel", value.Layer);
                Assert.Same(value, parts[parts.Count - 1]);
                Assert.Equal(db.BlockTable["BOLT"].Count(e => !(e is DxfAttributeDefinition)) + 1, parts.Count);
            }
        }

        [Fact]
        public void AnInsertExplodedInADrawingWithNoHandles_GivesWhatItGivesWithThem()
        {
            // A POLYLINE of an arc in BOLT, stretched unlike along its axes, is placed by the reader as more shapes than it reads
            // alone; with no handles to tell them apart, each shape still comes out of its own entity, with that entity's look.
            string text = DxfFileEzdxfDrawings.R12.Replace("  2\nMARK\n 70\n0\n  0\nENDBLK", "  2\nMARK\n 70\n0\n" + Bulged + "  0\nENDBLK");
            string bare = System.Text.RegularExpressions.Regex.Replace(text, "(?m)^  5\n[0-9A-Fa-f]+\n", string.Empty);
            Assert.NotEqual(text, bare);

            string[] Exploded(string drawing)
            {
                DxfDatabase db = DxfDatabase.Parse(drawing);
                DxfBlockReference bolt = Bolt(db);
                bolt.Rotation = 0.0;
                bolt.ScaleFactors = new GeoVector3(2.0, 1.0, 1.0);
                return bolt.Explode().Select(p => p.GetType().Name + " " + p.Layer + " " + string.Join(";", p.GetGeometry())).ToArray();
            }

            string[] withHandles = Exploded(text);
            string[] without = Exploded(bare);

            Assert.Contains(withHandles, p => p.Contains(" poly "));
            Assert.Equal(withHandles, without);
        }

        [Fact]
        public void BlockTableRecordGivenTheOneItHas_ChangesNothing_InADrawingWhoseBlockInsertsItself_OrHasNoBlock()
        {
            // FRAME made to insert itself, and an INSERT of a block the drawing has not: given what they have, nothing is checked.
            foreach (string text in Drawings)
            {
                string cyclic = ReplaceInBlock(text, "FRAME", "  2\nBOLT\n", "  2\nFRAME\n");
                DxfDatabase db = DxfDatabase.Parse(cyclic);
                DxfBlockReference inFrame = db.BlockTable["FRAME"].OfType<DxfBlockReference>().Single();

                inFrame.BlockTableRecord = inFrame.BlockTableRecord;

                Assert.Equal(new UTF8Encoding(false).GetBytes(cyclic), Saved(db));

                // BOLT's BLOCK renamed NUT: the INSERTs of BOLT place a block the drawing has not.
                int blocks = text.IndexOf("\nBLOCKS\n", StringComparison.Ordinal);
                int named = text.IndexOf("\n  2\nBOLT\n", blocks, StringComparison.Ordinal);
                string orphaned = text.Substring(0, named) + "\n  2\nNUT\n" + text.Substring(named + "\n  2\nBOLT\n".Length);
                DxfDatabase other = DxfDatabase.Parse(orphaned);
                DxfBlockReference bolt = Bolt(other);

                Assert.Null(bolt.BlockTableRecord);
                bolt.BlockTableRecord = bolt.BlockTableRecord;
                Assert.Equal(new UTF8Encoding(false).GetBytes(orphaned), Saved(other));
            }
        }

        [Fact]
        public async Task AnInsertExploded_WhoseBlockInsertsItselfTurned_IsRefusedAtOnce_AndOneThatHoldsThePlacingComesOutAnInsert()
        {
            // X holds a line and two INSERTs of X turned by 30 and 45 degrees: stretched to twice its width, each of them has to
            // be exploded in its turn, into X again, without end, twice as many each time; refused before anything is made.
            DxfBlockReference turned = Stretched(Nest("X", Line(0) + Insert("X", 1, 30) + Insert("X", 2, 45)), "X");

            Task<IReadOnlyList<DxfEntity>> explode = Task.Run(() => turned.Explode());

            Assert.Same(explode, await Task.WhenAny(explode, Task.Delay(TimeSpan.FromSeconds(30))));
            ArgumentException refused = await Assert.ThrowsAsync<ArgumentException>(() => explode);
            Assert.Equal("The INSERT of X would make the block X insert itself.", refused.Message);

            // The INSERT of X not turned holds the placing, and comes out an INSERT, one level down.
            IReadOnlyList<DxfEntity> parts = Stretched(Nest("X", Line(0) + Insert("X", 1, 0)), "X").Explode();

            Assert.Equal(2, parts.Count);
            Assert.IsType<DxfLine>(parts[0]);
            Assert.Equal("X", Assert.IsType<DxfBlockReference>(parts[1]).Name);
        }

        [Fact]
        public void AnInsertExploded_ThatWouldMakeMoreEntitiesThanTheReaderPlaces_IsRefusedBeforeAnyIsMade()
        {
            // C1 holds 1 100 INSERTs, turned by 10 degrees, of C0's 2 000 lines: stretched to twice its width, each INSERT is
            // exploded in its turn, 2 200 000 lines, past the 2 000 000 of DxfReadOptions.MaxPlacedEntities.
            string lines = string.Concat(Enumerable.Range(0, 2000).Select(Line));
            string inserts = string.Concat(Enumerable.Range(0, 1100).Select(i => Insert("C0", i, 10)));
            DxfBlockReference c1 = Stretched(Nest("C1", inserts, "C0", lines), "C1");

            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => c1.Explode());

            Assert.Equal("Exploding the INSERT of C1 would make more than 2000000 entities, the bound of DxfReadOptions.MaxPlacedEntities.", refused.Message);
        }

        #endregion

        // A POLYLINE of R12 on the layer poly: a half circle and a line, with handles of its own.
        private const string Bulged = "  0\nPOLYLINE\n  5\nF1\n  8\npoly\n 66\n1\n 10\n0.0\n 20\n0.0\n 30\n0.0\n 70\n0\n"
            + "  0\nVERTEX\n  5\nF2\n  8\npoly\n 10\n0.0\n 20\n0.0\n 30\n0.0\n 42\n1.0\n"
            + "  0\nVERTEX\n  5\nF3\n  8\npoly\n 10\n4.0\n 20\n0.0\n 30\n0.0\n"
            + "  0\nVERTEX\n  5\nF4\n  8\npoly\n 10\n4.0\n 20\n3.0\n 30\n0.0\n"
            + "  0\nSEQEND\n  5\nF5\n  8\npoly\n";

        private static DxfBlockReference Bolt(DxfDatabase db)
            => Everything(db).OfType<DxfBlockReference>().Single(b => b.Owner == db.ModelSpace && b.Name == "BOLT" && !(b is DxfMInsertBlock));

        /// <summary>A text with a piece of the BLOCK of a name replaced, the first after its BLOCK record.</summary>
        internal static string ReplaceInBlock(string text, string block, string piece, string by)
        {
            int at = text.IndexOf("\n  2\n" + block + "\n", text.IndexOf("\nBLOCKS\n", StringComparison.Ordinal), StringComparison.Ordinal);
            int put = text.IndexOf(piece, at + 1, StringComparison.Ordinal);
            return text.Substring(0, put) + by + text.Substring(put + piece.Length);
        }

        /// <summary>A drawing of R12 of blocks given as their names and their entities, by turns.</summary>
        private static string Nest(params string[] blocks)
        {
            var text = new StringBuilder("  0\nSECTION\n  2\nHEADER\n  9\n$ACADVER\n  1\nAC1009\n  0\nENDSEC\n  0\nSECTION\n  2\nBLOCKS\n");

            for (int i = 0; i < blocks.Length; i += 2)
            {
                text.Append("  0\nBLOCK\n  8\n0\n  2\n").Append(blocks[i]).Append("\n 70\n0\n 10\n0.0\n 20\n0.0\n 30\n0.0\n");
                text.Append(blocks[i + 1]).Append("  0\nENDBLK\n  8\n0\n");
            }

            return text.Append("  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n  0\nENDSEC\n  0\nEOF\n").ToString();
        }

        /// <summary>A LINE 1 long up the y axis, at x of the number given.</summary>
        private static string Line(int x)
            => "  0\nLINE\n  8\n0\n 10\n" + x + ".0\n 20\n0.0\n 30\n0.0\n 11\n" + x + ".0\n 21\n1.0\n 31\n0.0\n";

        /// <summary>An INSERT of a block at x of the number given, turned by the degrees given.</summary>
        private static string Insert(string block, int x, int degrees)
            => "  0\nINSERT\n  8\n0\n  2\n" + block + "\n 10\n" + x + ".0\n 20\n0.0\n 30\n0.0\n 50\n" + degrees + ".0\n";

        /// <summary>An INSERT of a block of a drawing, added to its model space stretched to twice its width.</summary>
        private static DxfBlockReference Stretched(string drawing, string block)
        {
            DxfDatabase db = DxfDatabase.Parse(drawing);
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), db.BlockTable[block]) { ScaleFactors = new GeoVector3(2, 1, 1) };
            db.ModelSpace.AppendEntity(insert);
            return insert;
        }

        /// <summary>Each line wanted has one got within 1E-6 mm of it, end for end, and as many of them.</summary>
        internal static void SameLines(GeoLine3[] wanted, GeoLine3[] got)
        {
            Assert.Equal(wanted.Length, got.Length);

            foreach (GeoLine3 line in wanted)
            {
                double best = got.Min(g => Math.Max(g.StartPoint.DistanceTo(line.StartPoint), g.EndPoint.DistanceTo(line.EndPoint)));
                Assert.True(best <= 1E-6, "the line " + line + " is " + best.ToString("R") + " from the nearest");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What D23c left of copying between drawings (D23d): an anonymous block two INSERTs place is copied once, as AutoCAD's
    /// Insert keeps one *U for both, whether it is dynamic or not; a dynamic INSERT copied alone by WblockCloneObjects stays
    /// dynamic, bringing the dynamic block it stands for; and a new drawing names its code page, as AutoCAD's own empty
    /// drawings do, so that a reader takes its text of R2007 or later for the UTF-8 it is.
    /// </summary>
    public class DxfInsertD23dTests
    {
        private const double Close = 1E-9;

        /// <summary>
        /// A drawing of R2018 with the dynamic block GH_PANEL, a rectangle 100 by 50 of four lines given Width from (0, 0) to
        /// (100, 0), from 50 to 300 by 10, and the stretch of its right edge; an INSERT of it at the origin at its own width, one
        /// at (0, 200) set to 200, which places an anonymous block, and one at (0, 400) of that same anonymous block, as
        /// AutoCAD's own save 24a-d holds two INSERTs of one *U.
        /// </summary>
        private static DxfDatabase Source()
        {
            var db = new DxfDatabase("AC1032");
            DxfBlockTableRecord block = Panel(db, "GH_PANEL");
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), block));
            var set = new DxfBlockReference(new GeoPoint3(0, 200, 0), block);
            db.ModelSpace.AppendEntity(set);
            Width(set).Value = 200.0;
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 400, 0), set.AnonymousBlockTableRecord));
            return db;
        }

        /// <summary>The dynamic block of a name in a drawing, as <see cref="Source"/> says.</summary>
        private static DxfBlockTableRecord Panel(DxfDatabase db, string name)
        {
            var block = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var lines = new[]
            {
                new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0)),
            };

            foreach (DxfLine line in lines)
            {
                block.AppendEntity(line);
            }

            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                "Overall width of the panel",
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                DxfBlockParameterValueSet.Increments(50, 300, 10)
            );
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            return block;
        }

        private static DxfDynamicBlockReferenceProperty Width(DxfBlockReference insert) =>
            insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == "Width");

        private static double Real(DxfDynamicBlockReferenceProperty property) => Assert.IsType<double>(property.Value);

        private static DxfDatabase Reopened(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        /// <summary>The INSERTs of a space that place GH_PANEL or a block standing for it, in their order.</summary>
        private static List<DxfBlockReference> Panels(IEnumerable<DxfEntity> space) =>
            space
                .OfType<DxfBlockReference>()
                .Where(r => r.Name == "GH_PANEL" || r.Name.StartsWith("*U", StringComparison.Ordinal))
                .ToList();

        /// <summary>The names of the anonymous blocks of a drawing, in the order of its table.</summary>
        private static List<string> Anonymous(DxfDatabase db) =>
            db.BlockTable.Where(b => b.Name.StartsWith("*U", StringComparison.Ordinal)).Select(b => b.Name).ToList();

        /// <summary>The ends of the lines of the block an INSERT places, in their order.</summary>
        private static List<GeoPoint3> Drawn(DxfBlockReference insert) =>
            insert.BlockTableRecord.OfType<DxfLine>().SelectMany(l => new[] { l.StartPoint, l.EndPoint }).ToList();

        // A drawing of R12 built as text: a block of a name AutoCAD gives itself flagged anonymous (70 of 1).
        private const string Head12 =
            "  0\nSECTION\n  2\nHEADER\n  9\n$ACADVER\n  1\nAC1009\n  0\nENDSEC\n  0\nSECTION\n  2\nBLOCKS\n";
        private const string Line12 = "  0\nLINE\n  8\n0\n 10\n0\n 20\n0\n 30\n0\n 11\n1\n 21\n0\n 31\n0\n";

        private static string Insert12(string name, double x) =>
            "  0\nINSERT\n  8\n0\n  2\n"
            + name
            + "\n 10\n"
            + x.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "\n 20\n0\n 30\n0\n";

        private static string Block12(string name, string entities) =>
            "  0\nBLOCK\n  8\n0\n  2\n"
            + name
            + "\n 70\n"
            + (name.StartsWith("*", StringComparison.Ordinal) ? "1" : "0")
            + "\n 10\n0\n 20\n0\n 30\n0\n"
            + entities
            + "  0\nENDBLK\n  8\n0\n";

        private static string Drawing12(string blocks, string entities) =>
            Head12 + blocks + "  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n" + entities + "  0\nENDSEC\n  0\nEOF\n";

        // ---- An anonymous block two INSERTs place, copied once ----

        [Fact]
        public void AnAnonymousBlockTwoDynamicInsertsPlace_IsInsertedOnce_AndBothCopiesPlaceIt()
        {
            // As AutoCAD's Database.Insert of its own save 24a-d: one *U for the two INSERTs that share it there.
            DxfDatabase source = Source();
            var db = new DxfDatabase("AC1032");
            db.Insert(GeoTransform3.Identity, source, true);
            db = Reopened(db);

            List<DxfBlockReference> now = Panels(db.ModelSpace);
            Assert.Equal(3, now.Count);
            Assert.Single(Anonymous(db));
            Assert.Equal(now[1].Name, now[2].Name);
            Assert.All(now, r => Assert.True(r.IsDynamicBlock));
            Assert.Equal(new[] { now[1].Name }, db.BlockTable["GH_PANEL"].GetAnonymousBlocks().Select(b => b.Name));
            List<DxfBlockReference> was = Panels(source.ModelSpace);

            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(Real(Width(was[i])), Real(Width(now[i])), Close);
                Assert.Equal(Drawn(was[i]), Drawn(now[i]));
            }
        }

        [Fact]
        public void TheSharedCopy_SetThroughOneOfItsInserts_StretchesThatInsertAlone_AsTheSource()
        {
            DxfDatabase source = Source();
            var db = new DxfDatabase("AC1032");
            db.Insert(GeoTransform3.Identity, source, true);

            Width(Panels(source.ModelSpace)[2]).Value = 270.0;
            Width(Panels(db.ModelSpace)[2]).Value = 270.0;
            db = Reopened(db);
            source = Reopened(source);

            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(Real(Width(Panels(source.ModelSpace)[i])), Real(Width(Panels(db.ModelSpace)[i])), Close);
                Assert.Equal(Drawn(Panels(source.ModelSpace)[i]), Drawn(Panels(db.ModelSpace)[i]));
            }

            Assert.Equal(200.0, Real(Width(Panels(db.ModelSpace)[1])), Close);
            Assert.Equal(270.0, Real(Width(Panels(db.ModelSpace)[2])), Close);
        }

        [Fact]
        public void AStaticAnonymousBlockTwoInsertsPlace_IsInsertedOnce()
        {
            DxfDatabase source = DxfDatabase.Parse(
                Drawing12(Block12("*U1", Line12), Insert12("*U1", 0) + Insert12("*U1", 10))
            );
            DxfDatabase db = DxfDatabase.Parse(Drawing12(Block12("*U1", Line12), string.Empty));

            db.Insert(GeoTransform3.Identity, source, true);

            DxfDatabase read = Reopened(db);
            List<DxfBlockReference> placed = read.ModelSpace.OfType<DxfBlockReference>().ToList();
            Assert.Equal(new[] { "*U1", "*U2" }, Anonymous(read));
            Assert.Equal(new[] { "*U2", "*U2" }, placed.Select(r => r.Name));
        }

        [Fact]
        public void AnAnonymousBlockPlacedInABlockMergedAndInModelSpace_IsInsertedOnce()
        {
            DxfDatabase source = DxfDatabase.Parse(
                Drawing12(
                    Block12("*U1", Line12) + Block12("HOLDER", Insert12("*U1", 5)),
                    Insert12("*U1", 0) + Insert12("HOLDER", 20)
                )
            );
            DxfDatabase db = DxfDatabase.Parse(Drawing12(string.Empty, string.Empty));

            db.Insert(GeoTransform3.Identity, source, true);

            DxfDatabase read = Reopened(db);
            Assert.Single(Anonymous(read));
            Assert.Equal(Anonymous(read)[0], read.BlockTable["HOLDER"].OfType<DxfBlockReference>().Single().Name);
            Assert.Equal(Anonymous(read)[0], read.ModelSpace.OfType<DxfBlockReference>().First().Name);
        }

        [Fact]
        public void TwoInsertsOfOneAnonymousBlock_CopiedTogetherByWblockCloneObjects_PlaceOneCopy()
        {
            DxfDatabase source = DxfDatabase.Parse(
                Drawing12(Block12("*U1", Line12), Insert12("*U1", 0) + Insert12("*U1", 10))
            );
            DxfDatabase db = DxfDatabase.Parse(Drawing12(string.Empty, string.Empty));

            IReadOnlyList<DxfEntity> copies = source.WblockCloneObjects(source.ModelSpace.ToList(), db.ModelSpace);

            Assert.Equal(new[] { "*U1", "*U1" }, copies.Cast<DxfBlockReference>().Select(r => r.Name));
            Assert.Equal(new[] { "*U1" }, Anonymous(Reopened(db)));
        }

        [Fact]
        public void TwoInsertsOfOneAnonymousBlock_CopiedByTwoCalls_PlaceACopyEach_AsAutoCadsTwoCallsDo()
        {
            DxfDatabase source = DxfDatabase.Parse(
                Drawing12(Block12("*U1", Line12), Insert12("*U1", 0) + Insert12("*U1", 10))
            );
            DxfDatabase db = DxfDatabase.Parse(Drawing12(string.Empty, string.Empty));

            source.WblockCloneObjects(new[] { source.ModelSpace.First() }, db.ModelSpace);
            source.WblockCloneObjects(new[] { source.ModelSpace.Last() }, db.ModelSpace);

            Assert.Equal(
                new[] { "*U1", "*U2" },
                Reopened(db).ModelSpace.OfType<DxfBlockReference>().Select(r => r.Name)
            );
        }

        // ---- A dynamic INSERT copied alone by WblockCloneObjects ----

        [Fact]
        public void ADynamicInsertSetToAnotherWidth_CopiedAloneByWblockCloneObjects_StaysDynamic_BringingItsBlock()
        {
            DxfDatabase source = Source();
            var db = new DxfDatabase("AC1032");

            source.WblockCloneObjects(new[] { Panels(source.ModelSpace)[1] }, db.ModelSpace);
            db = Reopened(db);

            DxfBlockReference copy = Panels(db.ModelSpace).Single();
            Assert.True(db.BlockTable["GH_PANEL"].IsDynamicBlock);
            Assert.True(copy.IsDynamicBlock);
            Assert.Equal("GH_PANEL", copy.DynamicBlockTableRecord.Name);
            Assert.Equal(200.0, Real(Width(copy)), Close);
            Assert.Equal("Overall width of the panel", Width(copy).Description);
        }

        [Fact]
        public void ADynamicInsertCopiedAlone_IsSetAgainAsTheInsertItWasCopiedFrom()
        {
            DxfDatabase source = Source();
            var db = new DxfDatabase("AC1024");

            source.WblockCloneObjects(new[] { Panels(source.ModelSpace)[1] }, db.ModelSpace);
            Width(Panels(source.ModelSpace)[1]).Value = 270.0;
            Width(Panels(db.ModelSpace).Single()).Value = 270.0;
            db = Reopened(db);
            source = Reopened(source);

            DxfBlockReference copy = Panels(db.ModelSpace).Single();
            Assert.Equal(270.0, Real(Width(copy)), Close);
            Assert.Equal(Drawn(Panels(source.ModelSpace)[1]), Drawn(copy));
        }

        [Fact]
        public void ADynamicInsertAtItsOwnWidth_CopiedAlone_StaysDynamic()
        {
            DxfDatabase source = Source();
            var db = new DxfDatabase("AC1032");

            source.WblockCloneObjects(new[] { Panels(source.ModelSpace)[0] }, db.ModelSpace);
            db = Reopened(db);

            DxfBlockReference copy = Panels(db.ModelSpace).Single();
            Assert.True(copy.IsDynamicBlock);
            Assert.Equal("GH_PANEL", copy.Name);
            Assert.Equal(100.0, Real(Width(copy)), Close);
        }

        [Fact]
        public void TwoDynamicInsertsOfOneAnonymousBlock_CopiedTogether_PlaceOneCopy_BothDynamic()
        {
            DxfDatabase source = Source();
            var db = new DxfDatabase("AC1032");

            List<DxfBlockReference> was = Panels(source.ModelSpace).Skip(1).ToList();
            source.WblockCloneObjects(was.Cast<DxfEntity>().ToList(), db.ModelSpace);
            db = Reopened(db);

            List<DxfBlockReference> copies = Panels(db.ModelSpace);
            Assert.Equal(2, copies.Count);
            Assert.Single(Anonymous(db));
            Assert.All(copies, r => Assert.True(r.IsDynamicBlock));

            for (int i = 0; i < 2; i++)
            {
                Assert.Equal(Real(Width(was[i])), Real(Width(copies[i])), Close);
                Assert.Equal(Drawn(was[i]), Drawn(copies[i]));
            }
        }

        [Fact]
        public void ADynamicInsertCopiedAloneIntoADrawingWithTheDynamicBlock_StandsForThatDrawingsOwn()
        {
            var db = new DxfDatabase("AC1032");
            DxfBlockTableRecord own = Panel(db, "GH_PANEL");
            DxfDatabase source = Source();

            source.WblockCloneObjects(new[] { Panels(source.ModelSpace)[1] }, db.ModelSpace);
            db = Reopened(db);

            DxfBlockReference copy = Panels(db.ModelSpace).Single();
            Assert.True(copy.IsDynamicBlock);
            Assert.Equal(own.Handle, copy.DynamicBlockTableRecord.Handle);
            Assert.Equal(200.0, Real(Width(copy)), Close);
        }

        [Fact]
        public void ADynamicInsertCopiedAloneIntoADrawingWithAStaticBlockOfTheName_ComesStatic()
        {
            var db = new DxfDatabase("AC1032");
            var own = new DxfBlockTableRecord { Name = "GH_PANEL", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(own);
            own.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 5));
            DxfDatabase source = Source();

            source.WblockCloneObjects(new[] { Panels(source.ModelSpace)[1] }, db.ModelSpace);
            db = Reopened(db);

            Assert.False(db.BlockTable["GH_PANEL"].IsDynamicBlock);
            Assert.False(Panels(db.ModelSpace).Single().IsDynamicBlock);
        }

        [Fact]
        public void ADynamicInsertCopiedAloneIntoADrawingOfR2004_ComesStatic_WithoutItsDynamicBlock_AsBefore()
        {
            DxfDatabase source = Source();
            var db = new DxfDatabase("AC1018");

            source.WblockCloneObjects(new[] { Panels(source.ModelSpace)[1] }, db.ModelSpace);
            db = Reopened(db);

            DxfBlockReference copy = Panels(db.ModelSpace).Single();
            Assert.False(copy.IsDynamicBlock);
            Assert.False(db.BlockTable.Has("GH_PANEL"));
            Assert.Contains(Drawn(copy), p => p.IsEqualTo(new GeoPoint3(200, 0, 0), new Tolerance(1E-9, 1E-9)));
        }

        [Fact]
        public void AnInsertOfABlockHoldingADynamicInsert_CopiedAlone_BringsTheDynamicBlock_AndTheHeldInsertStaysDynamic()
        {
            var source = new DxfDatabase("AC1032");
            DxfBlockTableRecord panel = Panel(source, "GH_PANEL");
            var holder = new DxfBlockTableRecord { Name = "HOLDER", Origin = new GeoPoint3(0, 0, 0) };
            source.BlockTable.Add(holder);
            var held = new DxfBlockReference(new GeoPoint3(10, 0, 0), panel);
            holder.AppendEntity(held);
            Width(held).Value = 250.0;
            var placed = new DxfBlockReference(new GeoPoint3(0, 0, 0), holder);
            source.ModelSpace.AppendEntity(placed);
            var db = new DxfDatabase("AC1032");

            source.WblockCloneObjects(new[] { placed }, db.ModelSpace);
            db = Reopened(db);

            Assert.True(db.BlockTable["GH_PANEL"].IsDynamicBlock);
            DxfBlockReference copy = Panels(db.BlockTable["HOLDER"]).Single();
            Assert.True(copy.IsDynamicBlock);
            Assert.Equal("GH_PANEL", copy.DynamicBlockTableRecord.Name);
            Assert.Equal(250.0, Real(Width(copy)), Close);
        }

        [Fact]
        public void ADynamicInsertCopiedAloneIntoABlock_StaysDynamic()
        {
            DxfDatabase source = Source();
            var db = new DxfDatabase("AC1032");
            var sheet = new DxfBlockTableRecord { Name = "SHEET", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(sheet);

            source.WblockCloneObjects(new[] { Panels(source.ModelSpace)[1] }, sheet);
            db = Reopened(db);

            DxfBlockReference copy = Panels(db.BlockTable["SHEET"]).Single();
            Assert.True(copy.IsDynamicBlock);
            Assert.Equal(200.0, Real(Width(copy)), Close);
        }

        // ---- The code page of a new drawing ----

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        [InlineData("AC1027")]
        [InlineData("AC1032")]
        public void ANewDrawing_NamesItsCodePageAfterItsVersion_AsAutoCadsOwnEmptyDrawings(string version)
        {
            var db = new DxfDatabase(version);

            Assert.Contains(
                "  9\n$ACADVER\n  1\n" + version + "\n  9\n$DWGCODEPAGE\n  3\nANSI_1252\n",
                db.ToText().Replace("\r\n", "\n")
            );
            Assert.Equal("ANSI_1252", db.GetHeader("$DWGCODEPAGE").Single().Value.Trim());
        }

        [Fact]
        public void KoreanTextCopiedIntoANewDrawingOfR2018_IsWrittenAsUtf8_UnderTheCodePageItNames()
        {
            const string words = "철근 상부";
            var source = new DxfDatabase("AC1032");
            source.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    Position = new GeoPoint3(0, 0, 0),
                    TextString = words,
                    Height = 2.5,
                }
            );
            var db = new DxfDatabase("AC1032");

            db.Insert(GeoTransform3.Identity, source, true);

            byte[] bytes;

            using (var stream = new MemoryStream())
            {
                db.Save(stream);
                bytes = stream.ToArray();
            }

            string text = new UTF8Encoding(false, true).GetString(bytes);
            Assert.Contains("\n" + words + "\n", text.Replace("\r\n", "\n"));
            Assert.Contains("$DWGCODEPAGE", text);
            Assert.Equal(words, DxfDatabase.Parse(text).ModelSpace.OfType<DxfDBText>().Single().TextString);
        }

        [Fact]
        public void KoreanTextCopiedIntoANewDrawingOfR2004_IsWrittenAsItsEscapes()
        {
            const string words = "철근";
            var source = new DxfDatabase("AC1032");
            source.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    Position = new GeoPoint3(0, 0, 0),
                    TextString = words,
                    Height = 2.5,
                }
            );
            var db = new DxfDatabase("AC1018");

            db.Insert(GeoTransform3.Identity, source, true);

            string text = db.ToText();
            Assert.Contains("\\U+CCA0\\U+ADFC", text);
            Assert.Equal(words, DxfDatabase.Parse(text).ModelSpace.OfType<DxfDBText>().Single().TextString);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Blocks another drawing's copies bring that would come to insert themselves once written: this drawing's P holding an
    /// INSERT of M, a block it has not, and the other drawing's M an INSERT of P, so that the M brought would place P and P
    /// the M brought, without end. <see cref="DxfDatabase.WblockCloneObjects"/> and a copy appended alone wrote that round,
    /// which no reader can place; they refuse it now, as <see cref="DxfDatabase.Insert(GeoTransform3, DxfDatabase, bool)"/>
    /// refused it, and write nothing; so does <see cref="DxfBlockReference.ConvertToStaticBlock(string)"/>. Each drawing is
    /// a DXF of R12 built here as text, read as a drawing from a file is, but for the dynamic block's, of R2018.
    /// </summary>
    public class DxfWblockRoundTests
    {
        private const string Head = "  0\nSECTION\n  2\nHEADER\n  9\n$ACADVER\n  1\nAC1009\n  0\nENDSEC\n  0\nSECTION\n  2\nBLOCKS\n";
        private const string Line = "  0\nLINE\n  8\n0\n 10\n0\n 20\n0\n 30\n0\n 11\n1\n 21\n0\n 31\n0\n";

        private static string Insert(string name) => "  0\nINSERT\n  8\n0\n  2\n" + name + "\n 10\n0\n 20\n0\n 30\n0\n";

        // A block of a name AutoCAD gives itself, *U1, flagged anonymous (70 of 1).
        private static string Block(string name, string entities) => "  0\nBLOCK\n  8\n0\n  2\n" + name + "\n 70\n" + (name.StartsWith("*", StringComparison.Ordinal) ? "1" : "0") + "\n 10\n0\n 20\n0\n 30\n0\n" + entities + "  0\nENDBLK\n  8\n0\n";

        private static string Drawing(string blocks, string entities) => Head + blocks + "  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n" + entities + "  0\nENDSEC\n  0\nEOF\n";

        /// <summary>
        /// Runs within a minute on a thread of its own, so that a walk that never ends fails the test rather than holding the
        /// run; what it throws is thrown here.
        /// </summary>
        private static void WithinAMinute(Action body)
        {
            Exception thrown = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    body();
                }
                catch (Exception e)
                {
                    thrown = e;
                }
            });

            thread.IsBackground = true;
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromMinutes(1)), "The copy did not end in a minute.");

            if (thrown != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(thrown).Throw();
            }
        }

        [Fact]
        public void AnInsertOfABlockWhoseCopyThisDrawingsInsertOfABlockItHasNotWouldPlace_IsRefusedByWblockCloneObjects_AndNothingIsWritten() => WithinAMinute(() =>
        {
            // This drawing's P holds a line and an INSERT of M, which it has not; the other's M holds an INSERT of P. The M
            // brought would insert this P, and this P the M brought: P, M, P and on.
            DxfDatabase db = DxfDatabase.Parse(Drawing(Block("P", Line + Insert("M")), string.Empty));
            DxfDatabase source = DxfDatabase.Parse(Drawing(Block("P", Line) + Block("M", Insert("P")), Insert("M")));
            string before = db.ToText();

            ArgumentException refused = Assert.Throws<ArgumentException>(() => source.WblockCloneObjects(source.ModelSpace.ToList(), db.ModelSpace));

            Assert.Contains("M", refused.Message, StringComparison.Ordinal);
            Assert.Contains("insert itself", refused.Message, StringComparison.Ordinal);
            Assert.Equal(before, db.ToText());
            Assert.False(db.BlockTable.Has("M"));
        });

        [Fact]
        public void TheSameInsertCopiedAlone_IsRefusedWhenAppended_AndNothingIsWritten() => WithinAMinute(() =>
        {
            DxfDatabase db = DxfDatabase.Parse(Drawing(Block("P", Line + Insert("M")), string.Empty));
            DxfDatabase source = DxfDatabase.Parse(Drawing(Block("P", Line) + Block("M", Insert("P")), Insert("M")));
            DxfEntity placed = source.ModelSpace.Single();
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(placed.Clone()));
            Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(placed.GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(100, 0, 0)))));
            Assert.Equal(before, db.ToText());
        });

        [Fact]
        public void ARoundThroughSeveralBlocksBrought_IsRefused() => WithinAMinute(() =>
        {
            // This P inserts M, which it has not; the other's M inserts N, N inserts Q and Q inserts P. M, N and Q would all be
            // brought, and the round closes through this P, four blocks round.
            DxfDatabase db = DxfDatabase.Parse(Drawing(Block("P", Line + Insert("M")), string.Empty));
            DxfDatabase source = DxfDatabase.Parse(Drawing(Block("P", Line) + Block("Q", Insert("P")) + Block("N", Insert("Q")) + Block("M", Insert("N")), Insert("M")));
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => source.WblockCloneObjects(source.ModelSpace.ToList(), db.ModelSpace));
            Assert.Equal(before, db.ToText());
        });

        [Fact]
        public void AnInsertCopiedIntoABlockThatTheBlockItBringsInserts_IsRefused() => WithinAMinute(() =>
        {
            // Both drawings have W; the other's Q inserts W. An INSERT of Q copied into this W would bring Q, which would
            // insert this W: W, Q, W and on.
            DxfDatabase db = DxfDatabase.Parse(Drawing(Block("W", Line), string.Empty));
            DxfDatabase source = DxfDatabase.Parse(Drawing(Block("W", Line) + Block("Q", Insert("W")), Insert("Q")));
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => source.WblockCloneObjects(source.ModelSpace.ToList(), db.BlockTable["W"]));
            Assert.Throws<ArgumentException>(() => db.BlockTable["W"].AppendEntity(source.ModelSpace.Single().Clone()));
            Assert.Equal(before, db.ToText());

            // Into model space, the same INSERT comes with Q, which places this W.
            source.WblockCloneObjects(source.ModelSpace.ToList(), db.ModelSpace);
            Assert.Equal("W", db.BlockTable["Q"].OfType<DxfBlockReference>().Single().Name);
        });

        [Fact]
        public void ADrawingWhoseBlocksInsertThemselves_IsNotCopiedFrom() => WithinAMinute(() =>
        {
            // The other drawing, read from a file, has M holding an INSERT of N and N one of M, and S an INSERT of itself.
            DxfDatabase source = DxfDatabase.Parse(Drawing(Block("M", Insert("N")) + Block("N", Insert("M")) + Block("S", Line + Insert("S")), Insert("M") + Insert("S")));
            DxfDatabase db = DxfDatabase.Parse(Drawing(string.Empty, string.Empty));
            string before = db.ToText();
            List<DxfEntity> placed = source.ModelSpace.ToList();

            Assert.Throws<ArgumentException>(() => source.WblockCloneObjects(new[] { placed[0] }, db.ModelSpace));
            Assert.Throws<ArgumentException>(() => source.WblockCloneObjects(new[] { placed[1] }, db.ModelSpace));
            Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(placed[1].Clone()));
            Assert.Equal(before, db.ToText());
        });

        [Fact]
        public void AnAnonymousBlockBrought_IsNotNamedAsAnInsertOfABlockNeitherDrawingHas_SoThatItPlacesNeitherItselfNorTheBlockOfThatInsert() => WithinAMinute(() =>
        {
            // This P holds an INSERT of *U1, a block it has not; the other's *U1 inserts P. Brought as *U1, the first name of
            // its letter this drawing has no block of, this P would place it and it this P; it comes as *U2.
            DxfDatabase db = DxfDatabase.Parse(Drawing(Block("P", Line + Insert("*U1")), string.Empty));
            DxfDatabase source = DxfDatabase.Parse(Drawing(Block("P", Line) + Block("*U1", Insert("P")), Insert("*U1")));

            var copy = (DxfBlockReference)source.WblockCloneObjects(source.ModelSpace.ToList(), db.ModelSpace).Single();

            Assert.Equal("*U2", copy.Name);
            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.Equal("P", read.BlockTable["*U2"].OfType<DxfBlockReference>().Single().Name);
            Assert.Equal("*U1", read.BlockTable["P"].OfType<DxfBlockReference>().Single().Name);
            Assert.False(read.BlockTable.Has("*U1"));

            // The other's *U2 holds an INSERT of *U1, a block it has not either, and this drawing has a *U2: brought as *U1, it
            // would place itself; it comes as *U3, its INSERT of *U1 as it was.
            DxfDatabase other = DxfDatabase.Parse(Drawing(Block("*U2", Line), string.Empty));
            DxfDatabase from = DxfDatabase.Parse(Drawing(Block("*U2", Line + Insert("*U1")), Insert("*U2")));

            var placed = (DxfBlockReference)from.WblockCloneObjects(from.ModelSpace.ToList(), other.ModelSpace).Single();

            Assert.Equal("*U3", placed.Name);
            DxfDatabase back = DxfDatabase.Parse(other.ToText());
            Assert.Equal("*U1", back.BlockTable["*U3"].OfType<DxfBlockReference>().Single().Name);
            Assert.DoesNotContain(DxfReader.Parse(other.ToText(), new DxfReadOptions { Tolerance = Tolerance.Default }).Warnings, w => w.Contains("without end"));
        });

        [Fact]
        public void TwoInsertsCopiedTogetherWhoseBlocksMeetOnlyThroughEachOther_AreRefusedWhole() => WithinAMinute(() =>
        {
            // This T inserts B and this U inserts A, neither of which it has; the other's A inserts T and B inserts U. Either
            // INSERT copied alone brings no round; the two together would: A, T, B, U, A. The set is refused before the first
            // is written.
            DxfDatabase db = DxfDatabase.Parse(Drawing(Block("T", Line + Insert("B")) + Block("U", Line + Insert("A")), string.Empty));
            DxfDatabase source = DxfDatabase.Parse(Drawing(Block("T", Line) + Block("U", Line) + Block("A", Insert("T")) + Block("B", Insert("U")), Insert("A") + Insert("B")));
            string before = db.ToText();

            ArgumentException refused = Assert.Throws<ArgumentException>(() => source.WblockCloneObjects(source.ModelSpace.ToList(), db.ModelSpace));

            Assert.Contains("insert itself", refused.Message, StringComparison.Ordinal);
            Assert.Equal(before, db.ToText());
            Assert.False(db.BlockTable.Has("A"));
        });

        /// <summary>
        /// The R2018 drawing with BOLT a dynamic block and *D1 the anonymous block AutoCAD made of it, an INSERT of *D1 tied to
        /// it in model space; PPP a line and an INSERT of a block the drawing has not, of the name given; *D1 an INSERT of PPP
        /// where asked.
        /// </summary>
        private static DxfDatabase Dynamic(bool anonymousInsertsPpp, string missing, out DxfBlockReference insert)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfBlockTableRecord bolt = db.BlockTable["BOLT"];
            bolt.XData = new[] { new DxfTypedValue(1001, "AcDbDynamicBlockGUID"), new DxfTypedValue(1000, "{3C8E1F3B-7D44-4C93-9F53-5E1C3C2B1A10}") };
            DxfBlockTableRecord anonymous = db.BlockTable["*D1"];
            anonymous.XData = new[] { new DxfTypedValue(1001, "AcDbBlockRepBTag"), new DxfTypedValue(1070, "1"), new DxfTypedValue(1005, bolt.Handle) };
            var stand = new DxfBlockTableRecord { Name = "ZZQ1" };
            db.BlockTable.Add(stand);
            stand.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));
            var ppp = new DxfBlockTableRecord { Name = "PPP" };
            db.BlockTable.Add(ppp);
            ppp.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));
            ppp.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), stand));

            if (anonymousInsertsPpp)
            {
                anonymous.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), ppp));
            }

            var placed = new DxfBlockReference(new GeoPoint3(500, 0, 0), anonymous);
            db.ModelSpace.AppendEntity(placed);
            placed.CreateExtensionDictionary();
            placed.ExtensionDictionary.SetAt("AcDbBlockRepresentation", new DxfDBDictionary());

            // PPP's INSERT of ZZQ1 is made one of the missing block, as a drawing read from a file may hold it.
            DxfDatabase read = DxfDatabase.Parse(db.ToText().Replace("AcDbBlockReference\n  2\nZZQ1\n", "AcDbBlockReference\n  2\n" + missing + "\n"));
            insert = read.ModelSpace.OfType<DxfBlockReference>().Last(r => r.IsDynamicBlock && r.Name == "*D1");
            return read;
        }

        [Fact]
        public void AnInsertMadeStaticWithNoName_IsNotNamedAsAnInsertOfABlockTheDrawingHasNot() => WithinAMinute(() =>
        {
            // PPP inserts *U1, which the drawing has not: the static block comes as *U2, and PPP's INSERT places nothing still.
            DxfDatabase db = Dynamic(false, "*U1", out DxfBlockReference insert);
            Assert.Equal("*U1", db.BlockTable["PPP"].OfType<DxfBlockReference>().Single().Name);

            insert.ConvertToStaticBlock();

            Assert.Equal("*U2", insert.Name);
            Assert.False(db.BlockTable.Has("*U1"));

            // *D1 inserting PPP, a static *U1 would have placed PPP and PPP it; as *U2 it places PPP, and no round is written.
            DxfDatabase looped = Dynamic(true, "*U1", out DxfBlockReference other);
            other.ConvertToStaticBlock();
            Assert.Equal("*U2", other.Name);
            Assert.Contains("PPP", other.BlockTableRecord.OfType<DxfBlockReference>().Select(r => r.Name));
            Assert.DoesNotContain(DxfReader.Parse(looped.ToText(), new DxfReadOptions { Tolerance = Tolerance.Default }).Warnings, w => w.Contains("without end"));
        });

        [Fact]
        public void AnInsertMadeStaticUnderTheNameOfABlockAnInsertWouldPlace_ThatWouldInsertItself_IsRefused_AndNothingIsWritten() => WithinAMinute(() =>
        {
            // PPP inserts MM, which the drawing has not, and *D1 inserts PPP: a static MM would place PPP and PPP that MM.
            DxfDatabase db = Dynamic(true, "MM", out DxfBlockReference insert);
            string before = db.ToText();

            ArgumentException refused = Assert.Throws<ArgumentException>(() => insert.ConvertToStaticBlock("MM"));

            Assert.Contains("insert itself", refused.Message, StringComparison.Ordinal);
            Assert.Equal(before, db.ToText());
            Assert.False(db.BlockTable.Has("MM"));
            Assert.Equal("*D1", insert.Name);

            // Under a name no INSERT gives, the same INSERT is made static.
            insert.ConvertToStaticBlock("MM2");
            Assert.Equal("MM2", insert.Name);
        });

        [Fact]
        public void BlocksBroughtThatComeBackToNone_AreCopiedAsBefore_AnInsertOfABlockNeitherDrawingHasKeptAsItIs() => WithinAMinute(() =>
        {
            // This P inserts K, which neither drawing has; the other's M inserts P and Z, which neither has either. M comes,
            // placing this P; no round, and the INSERTs of K and Z stay as they were written.
            DxfDatabase db = DxfDatabase.Parse(Drawing(Block("P", Line + Insert("K")), string.Empty));
            DxfDatabase source = DxfDatabase.Parse(Drawing(Block("P", Line) + Block("M", Insert("P") + Insert("Z")), Insert("M")));

            IReadOnlyList<DxfEntity> copies = source.WblockCloneObjects(source.ModelSpace.ToList(), db.ModelSpace);

            Assert.Equal("M", ((DxfBlockReference)Assert.Single(copies)).Name);
            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.Equal(new[] { "P", "Z" }, read.BlockTable["M"].OfType<DxfBlockReference>().Select(r => r.Name).ToArray());
            Assert.Equal(new[] { "K" }, read.BlockTable["P"].OfType<DxfBlockReference>().Select(r => r.Name).ToArray());
            Assert.DoesNotContain(DxfReader.Parse(db.ToText(), new DxfReadOptions { Tolerance = Tolerance.Default }).Warnings, w => w.Contains("without end"));
        });
    }
}

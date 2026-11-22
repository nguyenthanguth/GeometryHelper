using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Remote.UnitTest
{
    /// <summary>Closing a drawing from outside, and reading the text styles, dimension styles and blocks of a drawing.</summary>
    public class PyLoadBStylesAndCloseTests
    {
        private static (Harness harness, TestHost host, CadDocument document, CadRemoteDocument remote) Start(
            CadDocument document
        )
        {
            var harness = new Harness();
            var host = new TestHost();
            host.Add(document);
            harness.Serve(host);
            CadRemoteDocument remote = harness.Connect().Documents.Single();
            return (harness, host, document, remote);
        }

        private static DxfLine Line(double x1, double y1, double x2, double y2) =>
            new DxfLine(new GeoPoint3(x1, y1, 0), new GeoPoint3(x2, y2, 0));

        // ---- Close ----------------------------------------------------------------------------------------------------

        [Fact]
        public void ADrawingNotChanged_IsClosed_AndLeavesTheList_AndALaterCallThrows()
        {
            var harness = new Harness();
            var host = new TestHost();
            host.Add(CadDocument.From(new DxfDatabase("AC1027"), "A-101.dxf"));
            host.Add(CadDocument.From(new DxfDatabase("AC1027"), "B-202.dxf"));
            harness.Serve(host);

            using (harness)
            {
                CadApplication app = harness.Connect();
                CadRemoteDocument a = app.Documents.Get("A-101.dxf");

                a.Close();

                Assert.Equal(new[] { "B-202.dxf" }, host.Documents.Select(d => d.Title).ToArray());
                Assert.Equal(new[] { "B-202.dxf" }, app.Documents.Select(d => d.Name).ToArray());
                CadRemoteException failure = Assert.Throws<CadRemoteException>(() => a.Activate());
                Assert.Equal("The drawing A-101.dxf is no longer open.", failure.Message);
            }
        }

        [Fact]
        public void ADrawingWithChanges_IsRefusedUnlessDiscarded_AndDiscardClosesItUnsaved()
        {
            var (harness, host, document, remote) = Start(CadDocument.From(new DxfDatabase("AC1027"), "A-101.dxf"));
            using (harness)
            {
                remote.Insert(Line(0, 0, 10, 0));
                remote.CommitChanges();
                Assert.True(document.IsModified);

                CadRemoteException refused = Assert.Throws<CadRemoteException>(() => remote.Close());
                Assert.Equal("A-101.dxf has changes not saved: save it, or close it with discard.", refused.Message);
                Assert.Single(host.Documents);

                remote.Close(true);

                Assert.Empty(host.Documents);
            }
        }

        [Fact]
        public void ADrawingWithACommandRunning_IsRefusedEvenWithDiscard()
        {
            var (harness, host, _, remote) = Start(CadDocument.From(new DxfDatabase("AC1027"), "A-101.dxf"));
            using (harness)
            {
                Assert.False(remote.SendCommand("LINE", TimeSpan.Zero).Completed);

                CadRemoteException refused = Assert.Throws<CadRemoteException>(() => remote.Close(true));
                Assert.Equal(
                    "The drawing A-101.dxf is busy with the command LINE: end it, then close the drawing again.",
                    refused.Message
                );
                Assert.Single(host.Documents);
            }
        }

        // ---- Text styles ----------------------------------------------------------------------------------------------

        [Fact]
        public void TheTextStyles_AreTheDrawingsOwn_SortedByName_AndChangingACopyChangesNothing()
        {
            var db = new DxfDatabase("AC1027");
            db.TextStyleTable["Standard"].TextSize = 5;
            db.TextStyleTable.Add(
                new DxfTextStyleTableRecord
                {
                    Name = "bold",
                    FileName = "arialbd.ttf",
                    XScale = 0.8,
                }
            );
            db.TextStyleTable.Add(
                new DxfTextStyleTableRecord
                {
                    Name = "Arial",
                    FileName = "arial.ttf",
                    ObliquingAngle = 0.2,
                }
            );
            var (harness, _, document, remote) = Start(CadDocument.From(db, "A-101.dxf"));
            using (harness)
            {
                IReadOnlyList<DxfTextStyleTableRecord> styles = remote.TextStyles;

                Assert.Equal(new[] { "Arial", "bold", "Standard" }, styles.Select(s => s.Name).ToArray());
                Assert.Equal("arial.ttf", styles[0].FileName);
                Assert.Equal(0.2, styles[0].ObliquingAngle, 6);
                Assert.Equal(0.8, styles[1].XScale, 6);
                Assert.Equal(5, styles[2].TextSize, 6);

                styles[2].TextSize = 9;

                Assert.Equal(5, document.Database.TextStyleTable["Standard"].TextSize, 6);
                Assert.Equal(5, remote.TextStyles[2].TextSize, 6);
            }
        }

        // ---- Dimension styles -----------------------------------------------------------------------------------------

        [Fact]
        public void TheDimensionStyles_AreTheDrawingsOwn_WithTheTextStyleTheyPointAt()
        {
            var db = new DxfDatabase("AC1027");
            db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "Notes", FileName = "arial.ttf" });
            db.DimStyleTable["Standard"].Dimscale = 50;
            db.DimStyleTable.Add(
                new DxfDimStyleTableRecord
                {
                    Name = "Plan",
                    Dimtxt = 3.5,
                    Dimasz = 2,
                    Dimtxsty = db.TextStyleTable["Notes"],
                }
            );
            var (harness, _, document, remote) = Start(CadDocument.From(db, "A-101.dxf"));
            using (harness)
            {
                IReadOnlyList<DxfDimStyleTableRecord> styles = remote.DimensionStyles;

                Assert.Equal(new[] { "ISO-25", "Plan", "Standard" }, styles.Select(s => s.Name).ToArray());
                Assert.Equal(50, styles[2].Dimscale, 6);
                Assert.Equal(3.5, styles[1].Dimtxt, 6);
                Assert.Equal(2, styles[1].Dimasz, 6);
                Assert.Equal("Notes", styles[1].Dimtxsty.Name);
                Assert.Equal("arial.ttf", styles[1].Dimtxsty.FileName);

                styles[1].Dimtxt = 9;

                Assert.Equal(3.5, document.Database.DimStyleTable["Plan"].Dimtxt, 6);
            }
        }

        // ---- Blocks ---------------------------------------------------------------------------------------------------

        [Fact]
        public void TheBlocks_AreTheNamedOnes_WithTheirObjects_SortedByName()
        {
            var db = new DxfDatabase("AC1027");
            var inner = new DxfBlockTableRecord { Name = "inner" };
            db.BlockTable.Add(inner);
            inner.AppendEntity(Line(0, 0, 5, 0));
            var outer = new DxfBlockTableRecord { Name = "Bolt", Origin = new GeoPoint3(1, 2, 0) };
            db.BlockTable.Add(outer);
            outer.AppendEntity(Line(0, 0, 0, 5));
            outer.AppendEntity(new DxfBlockReference(new GeoPoint3(10, 0, 0), inner));
            var (harness, _, _, remote) = Start(CadDocument.From(db, "A-101.dxf"));
            using (harness)
            {
                IReadOnlyList<DxfBlockTableRecord> blocks = remote.Blocks;

                Assert.Equal(new[] { "Bolt", "inner" }, blocks.Select(b => b.Name).ToArray());
                Assert.Equal(new GeoPoint3(1, 2, 0), blocks[0].Origin);
                Assert.Equal(2, blocks[0].Count);
                Assert.IsType<DxfLine>(blocks[0].First());
                Assert.Equal("inner", ((DxfBlockReference)blocks[0].Last()).Name);
                Assert.Single(blocks[1]);
            }
        }

        [Fact]
        public void TheBlocks_LeaveOutTheLayoutsAndTheAnonymousOnes_AndKeepAnExternalReference()
        {
            var db = new DxfDatabase("AC1027");
            var bolt = new DxfBlockTableRecord { Name = "Bolt" };
            db.BlockTable.Add(bolt);
            bolt.AppendEntity(Line(0, 0, 0, 5));
            var dimension = new DxfRotatedDimension(
                0,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                new GeoPoint3(0, 20, 0),
                string.Empty,
                db.DimStyleTable["Standard"]
            );
            var picture = new DxfBlockTableRecord { Name = "*D" };
            db.BlockTable.Add(picture);
            dimension.DimBlock = picture;
            db.ModelSpace.AppendEntity(dimension);
            db.AttachXref("column.dxf", "COLUMN");
            Assert.Contains(db.BlockTable, b => b.IsAnonymous);
            var (harness, _, _, remote) = Start(CadDocument.From(db, "A-101.dxf"));
            using (harness)
            {
                IReadOnlyList<DxfBlockTableRecord> blocks = remote.Blocks;

                Assert.Equal(new[] { "Bolt", "COLUMN" }, blocks.Select(b => b.Name).ToArray());
                Assert.True(blocks[1].IsFromExternalReference);
                Assert.Equal("column.dxf", blocks[1].PathName);
            }
        }

        [Fact]
        public void ADimensionStyleWithAnArrowheadBlock_BringsTheBlock_AndNamesTheCopy()
        {
            var db = new DxfDatabase("AC1027");
            var tick = new DxfBlockTableRecord { Name = "_ARCHTICK" };
            db.BlockTable.Add(tick);
            tick.AppendEntity(Line(-1, -1, 1, 1));
            db.DimStyleTable.Add(
                new DxfDimStyleTableRecord
                {
                    Name = "Arch",
                    Dimblk = tick,
                    Dimblk1 = tick,
                    Dimblk2 = tick,
                }
            );
            var (harness, _, _, remote) = Start(CadDocument.From(db, "A-101.dxf"));
            using (harness)
            {
                DxfDimStyleTableRecord arch = remote.DimensionStyles.Single(s => s.Name == "Arch");

                Assert.Equal("_ARCHTICK", arch.Dimblk.Name);
                Assert.Single(arch.Dimblk);
            }
        }

        [Fact]
        public void ARecordAnExternalReferenceBrought_IsReadWithTheStyles_AndLeftOutOfTheBlocks()
        {
            var db = new DxfDatabase("AC1027");
            db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GRIDTEXT", FileName = "arial.ttf" });
            db.DimStyleTable.Add(new DxfDimStyleTableRecord { Name = "GRIDDIM", Dimtxt = 4 });
            var tag = new DxfBlockTableRecord { Name = "GRIDTAG" };
            db.BlockTable.Add(tag);
            tag.AppendEntity(Line(0, 0, 0, 10));
            var bolt = new DxfBlockTableRecord { Name = "Bolt" };
            db.BlockTable.Add(bolt);
            bolt.AppendEntity(Line(0, 0, 0, 5));
            string text = db.ToText().Replace("GRID", "grid|");
            var (harness, _, _, remote) = Start(CadDocument.From(DxfDatabase.Parse(text), "A-101.dxf"));
            using (harness)
            {
                Assert.Equal(new[] { "Bolt" }, remote.Blocks.Select(b => b.Name).ToArray());
                Assert.Equal(new[] { "grid|TEXT", "Standard" }, remote.TextStyles.Select(s => s.Name).ToArray());
                Assert.Contains(remote.DimensionStyles, s => s.Name == "grid|DIM");
            }
        }
    }
}

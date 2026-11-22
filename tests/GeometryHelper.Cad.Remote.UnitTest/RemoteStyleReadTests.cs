using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Remote.UnitTest
{
    /// <summary>
    /// The text styles, dimension styles and blocks of a drawing read from outside (D1626): copies, sorted by name ignoring
    /// case and then ordinally, the layout blocks and the anonymous blocks left out, the objects of a block with it, and a
    /// copy changed in the caller leaving the app's drawing as it was.
    /// </summary>
    public class RemoteStyleReadTests
    {
        /// <summary>
        /// A drawing with the text styles Zeta (arial.ttf, 0.9 wide) and alpha (romans.shx, 0.75 wide), the dimension styles Wide
        /// (DIMTXT 5, DIMASZ 4, DIMSCALE 50) and dense (DIMTXT 2, DIMASZ 1.5, DIMSCALE 1), the block Nut with one line and the
        /// block bolt with two lines (0, 0)-(10, 0) and (0, 0)-(0, 10), and one dimension, which has the anonymous block <c>*D1</c>.
        /// The names are chosen so that an ordinal sort (Zeta before alpha, Wide before dense, Nut before bolt) is not the sort
        /// ignoring case (alpha, Zeta; dense, Wide; bolt, Nut).
        /// </summary>
        private static CadDocument Styled(string name)
        {
            var db = new DxfDatabase("AC1027");
            db.TextStyleTable.Add(
                new DxfTextStyleTableRecord
                {
                    Name = "Zeta",
                    FileName = "arial.ttf",
                    XScale = 0.9,
                }
            );
            db.TextStyleTable.Add(
                new DxfTextStyleTableRecord
                {
                    Name = "alpha",
                    FileName = "romans.shx",
                    XScale = 0.75,
                }
            );
            db.DimStyleTable.Add(
                new DxfDimStyleTableRecord
                {
                    Name = "Wide",
                    Dimtxt = 5,
                    Dimasz = 4,
                    Dimscale = 50,
                }
            );
            db.DimStyleTable.Add(
                new DxfDimStyleTableRecord
                {
                    Name = "dense",
                    Dimtxt = 2,
                    Dimasz = 1.5,
                    Dimscale = 1,
                }
            );

            var nut = new DxfBlockTableRecord { Name = "Nut", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(nut);
            nut.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(5, 0, 0)));
            var bolt = new DxfBlockTableRecord { Name = "bolt", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(bolt);
            bolt.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            bolt.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 10, 0)));

            var dimension = new DxfRotatedDimension(
                0,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                new GeoPoint3(50, 40, 0),
                string.Empty,
                null
            );
            var picture = new DxfBlockTableRecord { Name = "*D" };
            db.BlockTable.Add(picture);
            dimension.DimBlock = picture;
            db.ModelSpace.AppendEntity(dimension);
            return CadDocument.From(db, name);
        }

        private static (Harness harness, CadDocument document, CadRemoteDocument remote) Start()
        {
            var harness = new Harness();
            var host = new TestHost();
            CadDocument document = host.Add(Styled("A-101.dxf"));
            harness.Serve(host);
            CadRemoteDocument remote = harness.Connect().Documents.Single();
            return (harness, document, remote);
        }

        /// <summary>The names in the order the lists promise: ignoring case, then ordinally.</summary>
        private static List<string> Sorted(IEnumerable<string> names) =>
            names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ThenBy(n => n, StringComparer.Ordinal).ToList();

        [Fact]
        public void TheTextStyles_AreTheDrawingsOwn_SortedIgnoringCase_WithTheirFontsAndWidths()
        {
            var (harness, document, remote) = Start();
            using (harness)
            {
                IReadOnlyList<DxfTextStyleTableRecord> styles = remote.TextStyles;

                // Every style of the app's drawing, in the sort order: alpha comes before Zeta, which an ordinal sort reverses.
                Assert.Equal(Sorted(document.Database.TextStyleTable.Select(s => s.Name)), styles.Select(s => s.Name));
                DxfTextStyleTableRecord alpha = styles.Single(s => s.Name == "alpha");
                Assert.Equal("romans.shx", alpha.FileName);
                Assert.Equal(0.75, alpha.XScale, 9);
                DxfTextStyleTableRecord zeta = styles.Single(s => s.Name == "Zeta");
                Assert.Equal("arial.ttf", zeta.FileName);
                Assert.Equal(0.9, zeta.XScale, 9);
            }
        }

        [Fact]
        public void TheDimensionStyles_AreTheDrawingsOwn_SortedIgnoringCase_WithTheirTextArrowAndScale()
        {
            var (harness, document, remote) = Start();
            using (harness)
            {
                IReadOnlyList<DxfDimStyleTableRecord> styles = remote.DimensionStyles;

                // dense comes before Wide, which an ordinal sort reverses.
                Assert.Equal(Sorted(document.Database.DimStyleTable.Select(s => s.Name)), styles.Select(s => s.Name));
                DxfDimStyleTableRecord wide = styles.Single(s => s.Name == "Wide");
                Assert.Equal(5.0, wide.Dimtxt, 9);
                Assert.Equal(4.0, wide.Dimasz, 9);
                Assert.Equal(50.0, wide.Dimscale, 9);
                DxfDimStyleTableRecord dense = styles.Single(s => s.Name == "dense");
                Assert.Equal(2.0, dense.Dimtxt, 9);
                Assert.Equal(1.5, dense.Dimasz, 9);
                Assert.Equal(1.0, dense.Dimscale, 9);
            }
        }

        [Fact]
        public void TheBlocks_AreTheNamedOnesOnly_SortedIgnoringCase_AndTheLayoutAndAnonymousBlocksAreLeftOut()
        {
            var (harness, document, remote) = Start();
            using (harness)
            {
                // The drawing has the anonymous block of its dimension and the layout blocks, which the list must not hold.
                Assert.Contains(document.Database.BlockTable, b => b.Name.StartsWith("*D", StringComparison.Ordinal));
                Assert.Contains(document.Database.BlockTable, b => b.Name == DxfBlockTableRecord.ModelSpace);

                IReadOnlyList<DxfBlockTableRecord> blocks = remote.Blocks;

                Assert.Equal(new[] { "bolt", "Nut" }, blocks.Select(b => b.Name));
            }
        }

        [Fact]
        public void ABlock_ComesWithItsObjects_InTheOrderTheyWereAppended()
        {
            var (harness, _, remote) = Start();
            using (harness)
            {
                IReadOnlyList<DxfBlockTableRecord> blocks = remote.Blocks;

                DxfBlockTableRecord bolt = blocks.Single(b => b.Name == "bolt");
                List<DxfLine> lines = bolt.OfType<DxfLine>().ToList();
                Assert.Equal(2, bolt.Count());
                Assert.Equal(10.0, lines[0].EndPoint.X, 9);
                Assert.Equal(0.0, lines[0].EndPoint.Y, 9);
                Assert.Equal(0.0, lines[1].EndPoint.X, 9);
                Assert.Equal(10.0, lines[1].EndPoint.Y, 9);
                Assert.Single(blocks.Single(b => b.Name == "Nut"));
            }
        }

        [Fact]
        public void AChangedCopy_LeavesTheAppsDrawingAsItWas_AndTheNextReadIsTheOriginal()
        {
            var (harness, document, remote) = Start();
            using (harness)
            {
                DxfTextStyleTableRecord style = remote.TextStyles.Single(s => s.Name == "alpha");
                style.XScale = 0.5;
                DxfDimStyleTableRecord dimStyle = remote.DimensionStyles.Single(s => s.Name == "Wide");
                dimStyle.Dimtxt = 9;
                DxfBlockTableRecord block = remote.Blocks.Single(b => b.Name == "bolt");
                block.AppendEntity(new DxfLine(new GeoPoint3(1, 1, 0), new GeoPoint3(2, 2, 0)));

                Assert.Equal(0.75, document.Database.TextStyleTable["alpha"].XScale, 9);
                Assert.Equal(5.0, document.Database.DimStyleTable["Wide"].Dimtxt, 9);
                Assert.Equal(2, document.Database.BlockTable["bolt"].Count());
                Assert.False(document.IsModified);
                Assert.Equal(0.75, remote.TextStyles.Single(s => s.Name == "alpha").XScale, 9);
                Assert.Equal(5.0, remote.DimensionStyles.Single(s => s.Name == "Wide").Dimtxt, 9);
                Assert.Equal(2, remote.Blocks.Single(b => b.Name == "bolt").Count());
            }
        }

        [Fact]
        public void TheSameReadTwice_GivesTheSameNames()
        {
            var (harness, _, remote) = Start();
            using (harness)
            {
                Assert.Equal(remote.TextStyles.Select(s => s.Name), remote.TextStyles.Select(s => s.Name));
                Assert.Equal(remote.DimensionStyles.Select(s => s.Name), remote.DimensionStyles.Select(s => s.Name));
                Assert.Equal(remote.Blocks.Select(b => b.Name), remote.Blocks.Select(b => b.Name));
            }
        }

        [Fact]
        public void ADrawingClosed_RefusesTheReadsWithTheNoLongerOpenError()
        {
            var harness = new Harness();
            using (harness)
            {
                var host = new TestHost();
                CadDocument document = host.Add(Styled("A-101.dxf"));
                harness.Serve(host);
                CadRemoteDocument remote = harness.Connect().Documents.Single();
                host.Close(document);

                Assert.Equal(
                    "The drawing A-101.dxf is no longer open.",
                    Assert.Throws<CadRemoteException>(() => remote.TextStyles).Message
                );
                Assert.Equal(
                    "The drawing A-101.dxf is no longer open.",
                    Assert.Throws<CadRemoteException>(() => remote.DimensionStyles).Message
                );
                Assert.Equal(
                    "The drawing A-101.dxf is no longer open.",
                    Assert.Throws<CadRemoteException>(() => remote.Blocks).Message
                );
            }
        }
    }
}

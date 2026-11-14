using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// -XBIND Block with dimension styles, as AutoCAD 2021's accoreconsole binds them (probe): a block TAG of the drawing a
    /// reference shows, its dimension in the style MYDIM (text 5 high, arrows 3, scale 2, one decimal, text above, red text,
    /// text style MYSTYLE), comes as <c>grid$0$TAG</c> with the dimension in <c>grid$0$MYDIM</c> of the same contents, whose
    /// text style comes as <c>grid$0$MYSTYLE</c>; a dimension in Standard comes in <c>grid$0$Standard</c>, with
    /// <c>grid$0$Standard</c> for its text style, the drawing's own Standard left as it is.
    /// </summary>
    public class XbindDimensionStyleTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "xbinddim-" + Guid.NewGuid().ToString("N")
        );

        public XbindDimensionStyleTests()
        {
            Directory.CreateDirectory(Path.Combine(_folder, "xref"));
            Directory.CreateDirectory(Path.Combine(_folder, "plans"));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file a watcher still holds is left for the system to clear.
            }
        }

        private static DxfRotatedDimension Dimension(DxfDatabase database, DxfDimStyleTableRecord style)
        {
            var picture = new DxfBlockTableRecord { Name = "*D" };
            database.BlockTable.Add(picture);
            picture.AppendEntity(new DxfLine(new GeoPoint3(0, 20, 0), new GeoPoint3(100, 20, 0)));
            return new DxfRotatedDimension(
                0.0,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                new GeoPoint3(50, 20, 0),
                string.Empty,
                style
            )
            {
                DimBlock = picture,
            };
        }

        /// <summary>The reference's drawing of the probe: MYDIM and OTHER, the text style MYSTYLE, blocks TAG and PLAIN.</summary>
        private string Grid()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            var mystyle = new DxfTextStyleTableRecord { Name = "MYSTYLE", FileName = "arial.ttf" };
            database.TextStyleTable.Add(mystyle);
            var mydim = new DxfDimStyleTableRecord
            {
                Name = "MYDIM",
                Dimtxt = 5.0,
                Dimasz = 3.0,
                Dimscale = 2.0,
                Dimdec = 1,
                Dimtad = 1,
                Dimtxsty = mystyle,
                Dimclrt = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1),
            };
            database.DimStyleTable.Add(mydim);
            database.DimStyleTable.Add(
                new DxfDimStyleTableRecord
                {
                    Name = "OTHER",
                    Dimtxt = 7.0,
                    Dimasz = 4.0,
                }
            );
            var tag = new DxfBlockTableRecord { Name = "TAG" };
            database.BlockTable.Add(tag);
            tag.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            tag.AppendEntity(Dimension(database, mydim));
            var plain = new DxfBlockTableRecord { Name = "PLAIN" };
            database.BlockTable.Add(plain);
            plain.AppendEntity(Dimension(database, database.DimStyleTable["Standard"]));
            string path = Path.Combine(_folder, "xref", "grid.dxf");
            database.Save(path);
            return path;
        }

        private CadDocument Host(out CadCommandSession session)
        {
            string grid = Grid();
            string path = Path.Combine(_folder, "plans", "host.dxf");
            CadTemplate.CreateMetric().Save(path);
            CadDocument document = CadDocument.Open(path);
            session = Session(document);
            Run(session, "-XREF", "A", grid, P(0, 0), Enter, Enter, Enter);
            return document;
        }

        [Fact]
        public void MinusXbind_Block_BindsTheDimensionStyleItsDimensionUses_WithTheTextStyleThatStyleUses()
        {
            CadDocument document = Host(out CadCommandSession session);

            Run(session, "-XBIND", "B", "grid|TAG");

            Assert.Equal("1 Block(s) bound.", session.History.Last());
            DxfDatabase database = document.Database;
            DxfBlockTableRecord bound = database.BlockTable["grid$0$TAG"];
            DxfRotatedDimension dimension = Assert.Single(bound.Cast<DxfEntity>().OfType<DxfRotatedDimension>());
            Assert.Equal("grid$0$MYDIM", dimension.DimensionStyleName);

            DxfDimStyleTableRecord style = database.DimStyleTable["grid$0$MYDIM"];
            Assert.False(style.IsDependent);
            Assert.Equal(5.0, style.Dimtxt, 9);
            Assert.Equal(3.0, style.Dimasz, 9);
            Assert.Equal(2.0, style.Dimscale, 9);
            Assert.Equal(1, style.Dimdec);
            Assert.Equal(1, style.Dimtad);
            Assert.Equal(1, style.Dimclrt.ColorIndex);
            Assert.Equal("grid$0$MYSTYLE", style.Dimtxsty.Name);
            Assert.Equal("arial.ttf", database.TextStyleTable["grid$0$MYSTYLE"].FileName);
            Assert.False(database.TextStyleTable["grid$0$MYSTYLE"].IsDependent);

            // The styles the clone would have brought under their plain names are not left in the drawing, and the reference's
            // other styles are not brought.
            Assert.False(database.DimStyleTable.Has("MYDIM"));
            Assert.False(database.DimStyleTable.Has("OTHER"));
            Assert.False(database.DimStyleTable.Has("grid$0$OTHER"));
            Assert.False(database.TextStyleTable.Has("MYSTYLE"));
            AssertReopens(document);

            Run(session, Enter);
            Assert.True(document.Undo());
            Assert.False(document.Database.DimStyleTable.Has("grid$0$MYDIM"));
            Assert.False(document.Database.TextStyleTable.Has("grid$0$MYSTYLE"));
        }

        [Fact]
        public void MinusXbind_Block_OfADimensionInStandard_BindsTheReferencesStandardStyleAndItsTextStyle()
        {
            CadDocument document = Host(out CadCommandSession session);

            Run(session, "-XBIND", "B", "grid|PLAIN");

            Assert.Equal("1 Block(s) bound.", session.History.Last());
            DxfDatabase database = document.Database;
            DxfRotatedDimension dimension = Assert.Single(
                database.BlockTable["grid$0$PLAIN"].Cast<DxfEntity>().OfType<DxfRotatedDimension>()
            );
            Assert.Equal("grid$0$Standard", dimension.DimensionStyleName);
            Assert.Equal("grid$0$Standard", database.DimStyleTable["grid$0$Standard"].Dimtxsty.Name);
            Assert.False(database.DimStyleTable["grid$0$Standard"].IsDependent);
            Assert.False(database.TextStyleTable["grid$0$Standard"].IsDependent);

            // The drawing's own Standard is not touched.
            Assert.False(database.DimStyleTable["Standard"].IsDependent);
            Assert.Equal("Standard", database.DimStyleTable["Standard"].Dimtxsty.Name);
            AssertReopens(document);
        }

        [Fact]
        public void MinusXbind_Block_OfTwoBlocksWithOneDimensionStyle_NumbersTheSecondCopyFromOne()
        {
            CadDocument document = Host(out CadCommandSession session);

            Run(session, "-XBIND", "B", "grid|TAG");
            Run(session, "-XBIND", "B", "grid|TAG");

            // As the layers of a block bound twice: grid$0$WALL, then grid$1$WALL.
            DxfDatabase database = document.Database;
            Assert.True(database.DimStyleTable.Has("grid$0$MYDIM"));
            Assert.True(database.DimStyleTable.Has("grid$1$MYDIM"));
            DxfRotatedDimension second = Assert.Single(
                database.BlockTable["grid$1$TAG"].Cast<DxfEntity>().OfType<DxfRotatedDimension>()
            );
            Assert.Equal("grid$1$MYDIM", second.DimensionStyleName);
            Assert.Equal("grid$1$MYSTYLE", database.DimStyleTable["grid$1$MYDIM"].Dimtxsty.Name);
        }
    }
}

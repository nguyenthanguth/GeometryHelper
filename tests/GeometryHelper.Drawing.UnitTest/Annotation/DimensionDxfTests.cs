using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.Drawing.UnitTest.Annotation.AnnotationKit;
using static GeometryHelper.Drawing.UnitTest.Projection.ProjectionKit;

namespace GeometryHelper.Drawing.UnitTest.Annotation
{
    public class DimensionDxfTests
    {
        private readonly ITestOutputHelper _output;

        public DimensionDxfTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ADimensionMadeInCodeIsGivenNoBlockByTheLibrarySoItsOwnIsMade()
        {
            // The library refuses a DIMENSION made in code that names no block: it draws none itself.
            var database = new DxfDatabase("AC1027");
            var bare = new DxfRotatedDimension(0.0, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(50, 10, 0), "<>", null);
            Assert.Throws<ArgumentException>(() => database.ModelSpace.AppendEntity(bare));

            DrawingObject[] objects = { Part("plate", Box(0, 0, 0, 300, 200, 20), true) };
            DrawingDimension overall = Dimension(objects, Top).First(d => d.Direction.X > 0.5);
            overall.WriteTo(database, new ViewPlacement2("V-1", new GeoPoint2(0, 0), 1.0));

            DxfRotatedDimension written = database.ModelSpace.OfType<DxfRotatedDimension>().Single();
            DxfBlockTableRecord block = written.DimBlock;
            Assert.Equal("GH_D_V-1_D-1", block.Name);
            Assert.Equal(3, block.OfType<DxfLine>().Count());
            Assert.Equal(2, block.OfType<DxfSolid>().Count());
            Assert.Equal("300", block.OfType<DxfMText>().Single().Contents);
            Assert.All(block, e => Assert.Equal(0, e.ColorIndex));
        }

        [Fact]
        public void ADimensionWrittenAtItsScaleReadsTheModelLengthThroughDimlfac()
        {
            DrawingObject[] objects = { Bar(3000, 200, P(100, 100), P(2900, 100)) };
            IReadOnlyList<DrawingDimension> dimensions = Dimension(objects, Top, new DimensionSettings { Scale = 20, Cumulative = true });
            var database = new DxfDatabase("AC1027");
            var placement = new ViewPlacement2("V-1", new GeoPoint2(40, 30), 20.0);

            foreach (DrawingDimension dimension in dimensions)
            {
                dimension.WriteTo(database, placement);
            }

            DxfDatabase read = DxfDatabase.Parse(database.ToText());
            DxfDimension[] written = read.ModelSpace.OfType<DxfDimension>().ToArray();
            Assert.Equal(dimensions.Count, written.Length);

            DxfDimStyleTableRecord style = (DxfDimStyleTableRecord)read.DimStyleTable["GH_20"];
            Assert.Equal(20.0, style.Dimlfac);
            Assert.Equal(2.5, style.Dimtxt, 12);

            foreach (DxfDimension entity in written)
            {
                Assert.True(DrawingEntityTags.TryRead(entity, out string id, out string viewId));
                Assert.Equal("V-1", viewId);
                DrawingDimension made = dimensions.Single(d => d.Id == id);

                Assert.Equal(DrawingDimension.Layer, entity.Layer);
                Assert.Equal("GH_20", entity.DimensionStyleName);
                Assert.Equal(made.IsOrdinate() ? typeof(DxfOrdinateDimension) : typeof(DxfRotatedDimension), entity.GetType());
                Near(made.Value, entity.Measurement * 20.0);
                Assert.Equal(made.Text, entity.FormatMeasurement(entity.Measurement, entity.DimensionText));
                Assert.NotNull(entity.DimBlock);
                Assert.Equal(made.Text, entity.DimBlock.OfType<DxfMText>().Single().Contents);
            }

            Assert.Contains(written, d => d is DxfOrdinateDimension);
            Assert.Contains(written, d => d is DxfRotatedDimension r && r.FormatMeasurement(r.Measurement, r.DimensionText) == "2800");
        }

        [Fact]
        public void AMergedDimensionKeepsItsCountInItsTextAndWritesTheTotalItMeasures()
        {
            DrawingObject[] objects = { Bar(1000, 100, P(100, 50), P(200, 50), P(300, 50), P(400, 50)) };
            DrawingDimension four = Dimension(objects, Top).Single(d => d.Text == "4 x 100 = 400");
            var database = new DxfDatabase("AC1027");
            four.WriteTo(database, new ViewPlacement2("V-1", new GeoPoint2(0, 0), 5.0));

            DxfDimension written = DxfDatabase.Parse(database.ToText()).ModelSpace.OfType<DxfDimension>().Single();
            Assert.Equal("4 x 100 = <>", written.DimensionText);
            Assert.Equal("4 x 100 = 400", written.FormatMeasurement(written.Measurement, written.DimensionText));
        }

        [Fact]
        public void WritingADimensionAgainFillsItsBlockAgain()
        {
            DrawingObject[] objects = { Part("plate", Box(0, 0, 0, 300, 200, 20), true) };
            DrawingDimension overall = Dimension(objects, Top).First();
            var database = new DxfDatabase("AC1027");
            var placement = new ViewPlacement2("V-1", new GeoPoint2(0, 0), 1.0);

            overall.WriteTo(database, placement);
            int blocks = database.BlockTable.Count();
            DrawingEntityTags.RemoveAll(database);
            overall.WriteTo(database, placement);

            Assert.Equal(blocks, database.BlockTable.Count());
            Assert.Equal(6, database.BlockTable["GH_D_V-1_D-1"].Count(e => !e.IsErased));
            Assert.Single(DrawingEntityTags.Find(database));
        }

        [Fact]
        public void TheDimensionsOfTheTestBodiesAreDrawnToPicturesToLookAt()
        {
            var cases = new List<(string Name, DrawingObject[] Objects, GeoCoordinateSystem3 Frame, DimensionSettings Settings)>
            {
                ("tc1-top", Objects(TestBodies.TC1()), Top, new DimensionSettings()),
                ("tc1-front", Objects(TestBodies.TC1()), Front, new DimensionSettings()),
                ("bar-top-1to10", new[] { Bar(3000, 200, P(100, 100), P(2900, 100)) }, Top, new DimensionSettings { Scale = 10 }),
                ("bar-cumulative-1to10", new[] { Bar(3000, 200, P(100, 100), P(1000, 100), P(1100, 100), P(2900, 100)) }, Top, new DimensionSettings { Scale = 10, Cumulative = true, Chain = false }),
                ("bar-holes-1to5", new[] { Bar(1000, 100, P(100, 50), P(200, 50), P(300, 50), P(400, 50), P(430, 50)) }, Top, new DimensionSettings { Scale = 5 }),
            };

            TestBody tc2 = TestBodies.TC2();
            GeoCoordinateSystem3 tc2Frame = tc2.ToSource().Frame;
            cases.Add(("tc2-front-1to20", Objects(tc2), StandardViews.GetFrame(tc2Frame, ViewKind.Front).CoordinateSystem, new DimensionSettings { Scale = 20 }));
            cases.Add(("tc2-right-1to5", Objects(tc2), StandardViews.GetFrame(tc2Frame, ViewKind.Right).CoordinateSystem, new DimensionSettings { Scale = 5 }));
            TestBody tc5 = TestBodies.TC5();
            cases.Add(("tc5-top-1to10", Objects(tc5), Top, new DimensionSettings { Scale = 10 }));
            cases.Add(("tc5-front-1to10", Objects(tc5), Front, new DimensionSettings { Scale = 10 }));

            foreach ((string name, DrawingObject[] objects, GeoCoordinateSystem3 frame, DimensionSettings settings) in cases)
            {
                ProjectedView2 view = Project(objects, frame);
                IReadOnlyList<DrawingDimension> dimensions = Annotator.Dimension(view, objects, frame, settings, Tol);
                string path = SavePicture(view, dimensions, name);
                _output.WriteLine(path + "  " + dimensions.Count + " dimensions: " + string.Join(", ", dimensions.Select(d => d.Text)));
                Assert.True(new FileInfo(path).Length > 0);
                Assert.Empty(Annotator.FindOverlaps(dimensions, Tol));

                // The presenter draws each dimension through its block.
                CadDocument document = CadDocument.Open(Path.ChangeExtension(path, ".dxf"));
                Assert.Equal(dimensions.Count, document.Database.ModelSpace.OfType<DxfDimension>().Count(d => d.DimBlock != null));
            }
        }
    }

    internal static class DrawingDimensionExtensions
    {
        public static bool IsOrdinate(this DrawingDimension dimension) => dimension.Kind == DimensionKind.Cumulative || dimension.Kind == DimensionKind.Ordinate;
    }
}

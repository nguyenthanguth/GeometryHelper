using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.Drawing.UnitTest.Builder.BuilderKit;

namespace GeometryHelper.Drawing.UnitTest.Builder
{
    /// <summary>
    /// Times whole drawings, views, dimensions, marks, sheet and DXF: an assembly of 50 parts, and a frame of 1 000 members
    /// in a general arrangement; the limits are those of the overview, loose until they are measured and set.
    /// </summary>
    public class DrawingSpeedTests
    {
        private readonly ITestOutputHelper _output;

        public DrawingSpeedTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TheDrawingOfFiveColumnBasesOfFiftyPartsIsMadeInUnderASecond()
        {
            DrawingSource source = TestBodies.TC10(5, 1).ToSource();
            Assert.Equal(50, source.Objects.Count);
            DrawingBuilder.Build(TestBodies.TC5().ToSource(), DrawingSettings.Assembly, Tol);

            var watch = Stopwatch.StartNew();
            Drawing drawing = DrawingBuilder.Build(source, DrawingSettings.Assembly, Tol);
            watch.Stop();

            _output.WriteLine($"50 parts, assembly preset: {watch.Elapsed.TotalMilliseconds:0} ms, {drawing.Views.Count} views at 1:{drawing.Scale} on {drawing.Sheet.SizeName}, {DrawingEntityTags.Find(drawing.Database).Count} entities, {drawing.Views.Sum(v => v.Dimensions.Count)} dimensions");
            drawing.Save(Path.Combine(Folder(), "tc10-5x1.dxf"));
            _output.WriteLine(SavePicture(drawing.Database, DrawingDxfWriter.LayoutName, "tc10-5x1-layout"));
            Assert.True(watch.Elapsed.TotalSeconds < 1.0, watch.Elapsed.ToString());
        }

        [Fact]
        public void TheGeneralArrangementOfAFrameOfAThousandMembersIsMadeInUnderAMinute()
        {
            DrawingSource source = TestBodies.TC10(10, 10).ToSource();
            Assert.Equal(1000, source.Objects.Count);

            var watch = Stopwatch.StartNew();
            Drawing drawing = DrawingBuilder.Build(source, DrawingSettings.General, Tol);
            watch.Stop();

            _output.WriteLine($"1 000 members, general preset: {watch.Elapsed.TotalMilliseconds:0} ms, {drawing.Views.Count} views at 1:{drawing.Scale} on {drawing.Sheet.SizeName}, {DrawingEntityTags.Find(drawing.Database).Count} entities, {drawing.Views.Sum(v => v.Marks.Count)} marks");
            drawing.Save(Path.Combine(Folder(), "tc10-10x10.dxf"));
            _output.WriteLine(SavePicture(drawing.Database, DrawingDxfWriter.LayoutName, "tc10-10x10-layout"));
            Assert.True(watch.Elapsed.TotalSeconds < 60.0, watch.Elapsed.ToString());
        }
    }
}

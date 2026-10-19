using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class SceneWriterTests
    {
        public static IEnumerable<object[]> Kinds => SampleShapes.KindData;

        [Theory]
        [MemberData(nameof(Kinds))]
        public void AddOfAnObject_DispatchesToTheKindOfTheShape(string kind)
        {
            object shape = SampleShapes.Make(kind, 3);

            SceneItem item = new SceneWriter().Add(shape).Scene.Items.Single();

            Assert.Equal(kind, item.Kind);
            Assert.Equal(kind + " 1", item.Name);
        }

        [Fact]
        public void AddOfAnObject_ThrowsAnArgumentExceptionThatNamesTheType()
        {
            var writer = new SceneWriter();

            ArgumentException unknown = Assert.Throws<ArgumentException>(() => writer.Add(new object()));
            Assert.Contains("System.Object", unknown.Message, StringComparison.Ordinal);

            ArgumentException number = Assert.Throws<ArgumentException>(() => writer.Add(42));
            Assert.Contains("System.Int32", number.Message, StringComparison.Ordinal);

            ArgumentException text = Assert.Throws<ArgumentException>(() => writer.Add("a loop"));
            Assert.Contains("System.String", text.Message, StringComparison.Ordinal);

            ArgumentException frame = Assert.Throws<ArgumentException>(() => writer.Add((object)GeoVector3.XAxis));
            Assert.Contains("GeoVector3", frame.Message, StringComparison.Ordinal);

            Assert.Empty(writer.Scene.Items);
        }

        [Fact]
        public void AddOfASequence_NamesTheTypeOfAnElementItCannotDraw()
        {
            var writer = new SceneWriter();
            object[] mixed = { new GeoPoint2(0, 0), new GeoPoint2(1, 1), DateTime.Today };

            ArgumentException error = Assert.Throws<ArgumentException>(() => writer.Add(mixed));

            Assert.Contains("System.DateTime", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AddOfNull_ThrowsAnArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new SceneWriter().Add((object)null));
            Assert.Throws<ArgumentNullException>(() => new SceneWriter().Add((GeoPolygon2)null));
        }

        [Fact]
        public void AddOfASequence_AddsEachShapeAndNumbersTheNames()
        {
            var writer = new SceneWriter();
            GeoLine2[] lines = { new GeoLine2(0, 0, 1, 1), new GeoLine2(1, 1, 2, 0), new GeoLine2(2, 0, 3, 1) };

            writer.Add(lines, layer: "round", name: "edge", color: "#aabbcc");
            writer.Add(new List<GeoPoint2> { new GeoPoint2(0, 0) }, name: "only");

            Assert.Equal(new[] { "edge 1", "edge 2", "edge 3", "only" }, writer.Scene.Items.Select(i => i.Name));
            Assert.All(writer.Scene.Items.Take(3), item => Assert.Equal("round", item.Layer));
            Assert.All(writer.Scene.Items.Take(3), item => Assert.Equal("#AABBCC", item.Color));
        }

        [Fact]
        public void AColourThatIsNotRrggbb_IsRefused()
        {
            var writer = new SceneWriter();

            Assert.Throws<ArgumentException>(() => writer.Layer("a", "blue"));
            Assert.Throws<ArgumentException>(() => writer.Add(new GeoPoint2(0, 0), color: "#12345"));
        }

        [Fact]
        public void TheLayerOfAnExistingName_KeepsItsPlaceAndTakesTheNewColour()
        {
            var writer = new SceneWriter();
            writer.Layer("a").Layer("b").Layer("a", "#112233", visible: false);

            Assert.Equal(new[] { "a", "b" }, writer.Scene.Layers.Select(l => l.Name));
            Assert.Equal("#112233", writer.Scene.Layers[0].Color);
            Assert.False(writer.Scene.Layers[0].Visible);
            Assert.True(writer.Scene.Layers[1].Visible);
        }

        [Fact]
        public void TheNamesOfItemsOfOneKind_CountUpFromOne()
        {
            var writer = new SceneWriter();
            writer.Add(new GeoPoint2(0, 0)).Add(new GeoLine2(0, 0, 1, 1)).Add(new GeoPoint2(1, 1));

            Assert.Equal(new[] { "GeoPoint2 1", "GeoLine2 1", "GeoPoint2 2" }, writer.Scene.Items.Select(i => i.Name));
        }
    }
}

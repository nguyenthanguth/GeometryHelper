using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class ItemMeasuresTests
    {
        private static Dictionary<string, string> Measure(object shape)
        {
            Scene scene = new SceneWriter().Add(shape).Scene;
            Primitives primitives = Primitives.Build(scene);
            return ItemMeasures.Describe(scene.Items[0], scene, primitives.Items[0]).ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        [Fact]
        public void ABox30By20By10WithAHole10By10Through_HasTheVolumeAndAreaOfTheMaterial()
        {
            Dictionary<string, string> numbers = Measure(SampleShapes.Box(30, 20, 10, true));

            Assert.Equal("5000", numbers["Volume"]);
            Assert.Equal("2400", numbers["Surface area"]);
            Assert.Equal("6", numbers["Faces"]);
            Assert.Equal("1", numbers["Openings"]);
            Assert.Equal("yes", numbers["Closed"]);
            // The library gives the box of the faces; what is drawn goes out to the hole, cut from -1 to 11.
            Assert.Equal("0, 0, 0", numbers["Bounds min (library)"].Replace("(", string.Empty).Replace(")", string.Empty));
            Assert.Equal("30, 20, 10", numbers["Bounds max (library)"].Replace("(", string.Empty).Replace(")", string.Empty));
        }

        [Fact]
        public void APolygonArc_HasItsVerticesArcsLengthAndArea()
        {
            var loop = new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10) }, new[] { 1.0, 0.0, 0.0, 0.0 });

            Dictionary<string, string> numbers = Measure(loop);

            Assert.Equal("4", numbers["Vertices"]);
            Assert.Equal("1", numbers["Arcs"]);
            Assert.Equal((loop.Area).ToString("G10", System.Globalization.CultureInfo.InvariantCulture), numbers["Area"]);
            Assert.Equal((loop.Length).ToString("G10", System.Globalization.CultureInfo.InvariantCulture), numbers["Length"]);
        }

        [Fact]
        public void ALabel_ShowsItsText()
        {
            Scene scene = new SceneWriter().Text(new GeoPoint3(0, 0, 0), "hello").Scene;

            var numbers = ItemMeasures.Describe(scene.Items[0], scene, null);

            Assert.Equal("hello", Assert.Single(numbers).Value);
        }

        [Fact]
        public void EveryKind_GivesAtLeastOneNumberAndNeverThrows()
        {
            foreach (string kind in SampleShapes.Kinds)
            {
                Assert.NotEmpty(Measure(SampleShapes.Make(kind, 9)));
            }
        }
    }
}

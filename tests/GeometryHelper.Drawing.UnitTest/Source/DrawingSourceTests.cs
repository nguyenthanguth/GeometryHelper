using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Drawing.UnitTest
{
    public class DrawingSourceTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoSolid3 Box(double x, double y, double z)
            => GeoSolid3.Extrude(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(x, 0), new GeoPoint2(x, y), new GeoPoint2(0, y)), GeoCoordinateSystem3.Global, z, Tolerance);

        private static DrawingObject Part(string id, bool isMain = false, GeoCoordinateSystem3? frame = null)
            => new DrawingObject(id, DrawingObjectKind.Part, Box(30, 20, 10), frame ?? GeoCoordinateSystem3.Global, isMain: isMain);

        [Fact]
        public void AnObjectKeepsWhatItIsGivenAndTakesTextsLeftOutAsEmpty()
        {
            GeoSolid3 body = Box(300, 200, 20);
            var frame = new GeoCoordinateSystem3(new GeoPoint3(1, 2, 3), GeoVector3.YAxis, GeoVector3.ZAxis);

            var plate = new DrawingObject("P-1", DrawingObjectKind.Part, body, frame, name: "PLATE", mark: "P1", parentId: "A-1", isMain: true);

            Assert.Equal("P-1", plate.Id);
            Assert.Equal(DrawingObjectKind.Part, plate.Kind);
            Assert.Same(body, plate.Solid);
            Assert.True(plate.Frame.IsEqualTo(frame, Tolerance));
            Assert.Equal("PLATE", plate.Name);
            Assert.Equal("P1", plate.Mark);
            Assert.Equal(string.Empty, plate.Profile);
            Assert.Equal(string.Empty, plate.Material);
            Assert.Equal("A-1", plate.ParentId);
            Assert.True(plate.IsMain);
            Assert.Empty(plate.Attributes);
        }

        [Fact]
        public void AnObjectOfNoAssemblyHasNoParentWhetherItsParentIsLeftOutOrEmpty()
        {
            Assert.Null(Part("P-1").ParentId);
            Assert.Null(new DrawingObject("P-2", DrawingObjectKind.Bolt, Box(1, 1, 1), GeoCoordinateSystem3.Global, parentId: string.Empty).ParentId);
        }

        [Fact]
        public void AttributesComeOutInTheOrderOfTheirNamesCaseAndAll()
        {
            var attributes = new[]
            {
                new KeyValuePair<string, string>("phase", "2"),
                new KeyValuePair<string, string>("PROJECT", "Bridge"),
                new KeyValuePair<string, string>("Finish", "galvanised"),
            };

            var plate = new DrawingObject("P-1", DrawingObjectKind.Part, Box(1, 1, 1), GeoCoordinateSystem3.Global, attributes: attributes);

            Assert.Equal(new[] { "Finish", "PROJECT", "phase" }, plate.Attributes.Keys.ToArray());
            Assert.Equal("Bridge", plate.Attributes["PROJECT"]);
        }

        [Fact]
        public void AnAttributeGivenTwiceOrWithNoValueIsRefused()
        {
            var twice = new[] { new KeyValuePair<string, string>("a", "1"), new KeyValuePair<string, string>("a", "2") };
            var noValue = new[] { new KeyValuePair<string, string>("a", null) };

            Assert.Throws<ArgumentException>(() => new DrawingObject("P-1", DrawingObjectKind.Part, Box(1, 1, 1), GeoCoordinateSystem3.Global, attributes: twice));
            Assert.Throws<ArgumentException>(() => new DrawingObject("P-1", DrawingObjectKind.Part, Box(1, 1, 1), GeoCoordinateSystem3.Global, attributes: noValue));
        }

        [Fact]
        public void AnObjectWithNoIdNoSolidAKindNotListedOrNoFrameIsRefused()
        {
            GeoSolid3 body = Box(1, 1, 1);
            GeoCoordinateSystem3 world = GeoCoordinateSystem3.Global;

            Assert.Throws<ArgumentNullException>(() => new DrawingObject(null, DrawingObjectKind.Part, body, world));
            Assert.Throws<ArgumentException>(() => new DrawingObject(string.Empty, DrawingObjectKind.Part, body, world));
            Assert.Throws<ArgumentNullException>(() => new DrawingObject("P-1", DrawingObjectKind.Part, null, world));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DrawingObject("P-1", (DrawingObjectKind)99, body, world));
            Assert.Throws<ArgumentException>(() => new DrawingObject("P-1", DrawingObjectKind.Part, body, default(GeoCoordinateSystem3)));
        }

        [Fact]
        public void TheObjectsComeOutInTheOrderOfTheirIdsWhateverOrderTheyAreGivenIn()
        {
            DrawingObject[] objects = { Part("b"), Part("B-2"), Part("B-10"), Part("A") };

            var forward = new DrawingSource(objects);
            var backward = new DrawingSource(objects.Reverse());

            string[] expected = { "A", "B-10", "B-2", "b" };
            Assert.Equal(expected, forward.Objects.Select(o => o.Id).ToArray());
            Assert.Equal(expected, backward.Objects.Select(o => o.Id).ToArray());
        }

        [Fact]
        public void TwoObjectsWithOneIdOrANullObjectAreRefused()
        {
            Assert.Throws<ArgumentException>(() => new DrawingSource(new[] { Part("P-1"), Part("P-2"), Part("P-1") }));
            Assert.Throws<ArgumentException>(() => new DrawingSource(new[] { Part("P-1"), null }));
            Assert.Throws<ArgumentNullException>(() => new DrawingSource(null));
        }

        [Fact]
        public void AnObjectIsFoundByItsIdCaseAndAllAndAnIdOfNoneIsNotFound()
        {
            var source = new DrawingSource(new[] { Part("P-1"), Part("P-2") });

            Assert.True(source.TryGetObject("P-2", out DrawingObject found));
            Assert.Equal("P-2", found.Id);
            Assert.False(source.TryGetObject("p-2", out DrawingObject missing));
            Assert.Null(missing);
            Assert.Throws<ArgumentNullException>(() => source.TryGetObject(null, out _));
        }

        [Fact]
        public void TheFrameIsThatOfTheMainObjectWithTheLowestIdAndOneGivenTakesItsPlace()
        {
            var beam = new GeoCoordinateSystem3(new GeoPoint3(100, 0, 0), GeoVector3.YAxis, GeoVector3.ZAxis);
            var other = new GeoCoordinateSystem3(new GeoPoint3(0, 100, 0), GeoVector3.ZAxis, GeoVector3.XAxis);
            DrawingObject[] objects = { Part("A-plate", frame: other), Part("Z-beam", true, beam), Part("Z-other", true, other) };

            Assert.True(new DrawingSource(objects).Frame.IsEqualTo(beam, Tolerance));
            Assert.True(new DrawingSource(objects, other).Frame.IsEqualTo(other, Tolerance));
            Assert.Throws<ArgumentException>(() => new DrawingSource(objects, default(GeoCoordinateSystem3)));
        }

        [Fact]
        public void WithNoMainObjectTheFrameIsThatOfTheLowestIdAndWithNoObjectsTheWorlds()
        {
            var first = new GeoCoordinateSystem3(new GeoPoint3(5, 0, 0), GeoVector3.ZAxis, GeoVector3.XAxis);

            Assert.True(new DrawingSource(new[] { Part("B"), Part("A", frame: first) }).Frame.IsEqualTo(first, Tolerance));
            Assert.True(new DrawingSource(new DrawingObject[0]).Frame.IsEqualTo(GeoCoordinateSystem3.Global, Tolerance));
        }

        [Fact]
        public void TheSourceOfTC2TakesTheBeamsFrameAndKeepsItsObjectsInIdOrder()
        {
            DrawingSource source = TestBodies.TC2().ToSource();

            Assert.Equal(new[] { "TC2-beam", "TC2-endplate" }, source.Objects.Select(o => o.Id).ToArray());
            Assert.True(source.Frame.IsEqualTo(source.Objects[0].Frame, Tolerance));
            Assert.True(source.Frame.YAxis.IsEqualTo(GeoVector3.ZAxis, Tolerance));
        }
    }
}

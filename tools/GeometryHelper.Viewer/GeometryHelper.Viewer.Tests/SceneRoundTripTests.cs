using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class SceneRoundTripTests
    {
        public static IEnumerable<object[]> Kinds => SampleShapes.KindData;

        /// <summary>
        /// Gets every number in a piece of data, in document order.
        /// </summary>
        private static List<double> NumbersOf(JsonElement element)
        {
            var numbers = new List<double>();

            void Walk(JsonElement e)
            {
                switch (e.ValueKind)
                {
                    case JsonValueKind.Number:
                        numbers.Add(e.GetDouble());
                        break;
                    case JsonValueKind.Array:
                        foreach (JsonElement item in e.EnumerateArray())
                        {
                            Walk(item);
                        }

                        break;
                    case JsonValueKind.Object:
                        foreach (JsonProperty property in e.EnumerateObject())
                        {
                            Walk(property.Value);
                        }

                        break;
                    case JsonValueKind.String:
                        if (e.GetString() == "NaN")
                        {
                            numbers.Add(double.NaN);
                        }

                        break;
                }
            }

            Walk(element);
            return numbers;
        }

        private static List<double> NumbersOfAll(Scene scene) => scene.Items.SelectMany(item => NumbersOf(item.Data)).ToList();

        private static SceneItem Encode(object shape) => new SceneWriter().Add(shape).Scene.Items.Single();

        private static void AssertSameBits(IReadOnlyList<double> expected, IReadOnlyList<double> actual)
        {
            Assert.Equal(expected.Count, actual.Count);

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.True(
                    BitConverter.DoubleToInt64Bits(expected[i]) == BitConverter.DoubleToInt64Bits(actual[i]),
                    "number " + i + ": " + expected[i].ToString("G17") + " came back as " + actual[i].ToString("G17"));
            }
        }

        [Theory]
        [MemberData(nameof(Kinds))]
        public void TheDataOfEveryKind_ReadsBackBitForBit(string kind)
        {
            object shape = SampleShapes.Make(kind, 11);
            var writer = new SceneWriter("round trip");
            writer.Add(shape, layer: "a", name: "item", props: new { distance = 0.1, join = "Round", flag = true });

            string json = writer.ToJson();
            Scene read = SceneReader.Read(json);

            Assert.Equal(kind, read.Items.Single().Kind);
            AssertSameBits(NumbersOf(writer.Scene.Items.Single().Data), NumbersOf(read.Items.Single().Data));

            // Written again from what was read, the text is the same, so nothing was lost on the way through.
            Assert.Equal(json, new SceneWriter(read).ToJson());
        }

        [Theory]
        [MemberData(nameof(Kinds))]
        public void EveryKind_RebuildsTheLibraryShapeFromItsData(string kind)
        {
            double worst = 0.0;

            for (int seed = 0; seed < 100; seed++)
            {
                object shape = SampleShapes.Make(kind, seed);
                Scene read = SceneReader.Read(new SceneWriter().Add(shape).ToJson());

                object rebuilt = read.Items.Single().ToGeometry();

                if (kind == "GeoMesh2" || kind == "GeoMesh3")
                {
                    // The library has no constructor of a mesh, so the faces come back as polygons, with the very corners the mesh has.
                    object[] faces = kind == "GeoMesh2" ? ((Meshing.GeoMesh2)shape).GetFaces() : (object[])((Meshing.GeoMesh3)shape).GetFaces();
                    Assert.Equal(faces.Length, ((Array)rebuilt).Length);
                    worst = Math.Max(worst, Distance(NumbersOfAll(new SceneWriter().Add(faces).Scene), NumbersOfAll(new SceneWriter().Add(rebuilt).Scene)));
                    continue;
                }

                Assert.Equal(kind, rebuilt.GetType().Name);
                worst = Math.Max(worst, Distance(NumbersOf(Encode(shape).Data), NumbersOf(Encode(rebuilt).Data)));
            }

            // Every constructor that keeps what it is given leaves the numbers as they were. Those that scale a direction to
            // length one again (a ray, the normal or major axis of an arc, a circle, an ellipse or a plane, the axes of a box), or work
            // the sweep of an arc out again from its two angles, can move the last digit; over a hundred shapes of each, by two at most.
            Assert.True(worst <= (ConstructorScalesItsDirections.Contains(kind) ? 1E-13 : 0.0), kind + " came back " + worst + " away");
        }

        private static readonly HashSet<string> ConstructorScalesItsDirections = new HashSet<string>
        {
            "GeoRay2", "GeoRay3", "GeoArc2", "GeoArc3", "GeoCircle3", "GeoEllipse2", "GeoEllipse3", "GeoObb3", "GeoPlane3",
        };

        /// <summary>
        /// Gets how far apart two lists of numbers are: nought when every pair has the same bits, otherwise the largest
        /// difference, taken against the size of the number where it is above one.
        /// </summary>
        private static double Distance(IReadOnlyList<double> expected, IReadOnlyList<double> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            double worst = 0.0;

            for (int i = 0; i < expected.Count; i++)
            {
                if (BitConverter.DoubleToInt64Bits(expected[i]) != BitConverter.DoubleToInt64Bits(actual[i]))
                {
                    worst = Math.Max(worst, Math.Max(Math.Abs(expected[i] - actual[i]) / Math.Max(1.0, Math.Abs(expected[i])), double.Epsilon));
                }
            }

            return worst;
        }
        [Fact]
        public void AnOpeningOfASolid_SurvivesTheRoundTrip()
        {
            GeoSolid3 box = SampleShapes.Box(30.0, 20.0, 10.0, true);
            Assert.Single(box.Openings);

            var rebuilt = (GeoSolid3)SceneReader.Read(new SceneWriter().Add(box).ToJson()).Items.Single().ToGeometry();

            Assert.Equal(box.Faces.Count, rebuilt.Faces.Count);
            Assert.Single(rebuilt.Openings);
            Assert.Equal(box.GetVolume(), rebuilt.GetVolume(), 9);
        }

        [Fact]
        public void NumbersThatAreHardToWrite_ComeBackTheSame()
        {
            double[] hard = { 0.1, 1E-300, 5E-324, double.MaxValue, -double.MaxValue, 123456789.12345678, 1.0 / 3.0, Math.PI, 0.30000000000000004, 1E-15 };

            foreach (double x in hard)
            {
                Scene read = SceneReader.Read(new SceneWriter().Add(new GeoPoint3(x, -x, x * 0.5)).ToJson());
                AssertSameBits(new[] { x, -x, x * 0.5 }, NumbersOf(read.Items.Single().Data));
            }
        }

        [Fact]
        public void MinusZero_IsKeptByTheTextAndTheReader()
        {
            // 17 digits of -0.0 are written as "-0", and System.Text.Json reads that as minus zero again on .NET 8, so the sign
            // of a zero survives. A reader on .NET Framework may give plain zero for the same text, which no coordinate
            // distinguishes, and which is why nothing here compares coordinates with ==.
            var writer = new SceneWriter().Add(new GeoPoint3(-0.0, 0.0, 1.0));
            string json = writer.ToJson();

            Assert.Contains("[-0,0,1]", json, StringComparison.Ordinal);

            List<double> numbers = NumbersOf(SceneReader.Read(json).Items.Single().Data);
            Assert.True(double.IsNegative(numbers[0]));
            Assert.False(double.IsNegative(numbers[1]));
        }

        [Fact]
        public void NumbersJsonCannotCarry_AreWrittenAsTextAndReadBack()
        {
            var writer = new SceneWriter().Add(new GeoPoint3(double.NaN, double.PositiveInfinity, double.NegativeInfinity));
            Scene read = SceneReader.Read(writer.ToJson());
            var rebuilt = (GeoPoint3)read.Items.Single().ToGeometry();

            Assert.True(double.IsNaN(rebuilt.X));
            Assert.True(double.IsPositiveInfinity(rebuilt.Y));
            Assert.True(double.IsNegativeInfinity(rebuilt.Z));
        }

        [Fact]
        public void TheSceneText_HasOneLinePerLayerAndPerItem_AndLfLineEnds()
        {
            var writer = new SceneWriter("two layers");
            writer.Layer("a", "#102030").Layer("b", "#405060", visible: false);
            writer.Add(new GeoPoint2(1, 2), layer: "a");
            writer.Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(1, 1)), layer: "b");

            string json = writer.ToJson();
            string[] lines = json.Split('\n');

            Assert.DoesNotContain('\r', json);
            Assert.Single(lines, line => line.StartsWith("{\"name\":\"a\"", StringComparison.Ordinal));
            Assert.Single(lines, line => line.StartsWith("{\"name\":\"b\"", StringComparison.Ordinal));
            Assert.Single(lines, line => line.StartsWith("{\"name\":\"GeoPoint2 1\"", StringComparison.Ordinal));
            Assert.Single(lines, line => line.StartsWith("{\"name\":\"GeoLine2 1\"", StringComparison.Ordinal));
            Assert.EndsWith("}\n", json, StringComparison.Ordinal);
        }

        [Fact]
        public void Layers_KeepTheirColourAndVisibility()
        {
            var writer = new SceneWriter();
            writer.Layer("a", "#102030").Layer("b", "#405060", visible: false);
            writer.Add(new GeoPoint2(1, 2), layer: "a");

            Scene read = SceneReader.Read(writer.ToJson());

            Assert.Equal(new[] { "a", "b" }, read.Layers.Select(l => l.Name));
            Assert.Equal(new[] { "#102030", "#405060" }, read.Layers.Select(l => l.Color));
            Assert.Equal(new[] { true, false }, read.Layers.Select(l => l.Visible));
        }

        [Fact]
        public void AnItemOnALayerNeverMade_GetsTheLayerWithTheNextColourOfTheCycle()
        {
            var writer = new SceneWriter();
            writer.Add(new GeoPoint2(1, 2), layer: "first");
            writer.Add(new GeoPoint2(1, 2), layer: "second");

            Assert.Equal(new[] { SceneColor.FromCycle(0), SceneColor.FromCycle(1) }, writer.Scene.Layers.Select(l => l.Color));
            Assert.Equal("default", new SceneWriter().Add(new GeoPoint2(0, 0)).Scene.Items.Single().Layer);
        }

        [Fact]
        public void Properties_AreWrittenSortedAndWithTheInvariantCulture()
        {
            System.Globalization.CultureInfo before = System.Globalization.CultureInfo.CurrentCulture;

            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

                var writer = new SceneWriter().Add(
                    new GeoPoint2(0, 0),
                    props: new Dictionary<string, object> { ["zeta"] = 1.5, ["alpha"] = "x", ["mid"] = 3, ["flag"] = false });

                string json = writer.ToJson();

                Assert.True(json.IndexOf("\"alpha\"", StringComparison.Ordinal) < json.IndexOf("\"flag\"", StringComparison.Ordinal));
                Assert.True(json.IndexOf("\"flag\"", StringComparison.Ordinal) < json.IndexOf("\"zeta\"", StringComparison.Ordinal));
                Assert.Equal("1.5", SceneReader.Read(json).Items.Single().Properties["zeta"]);
                Assert.Equal("false", SceneReader.Read(json).Items.Single().Properties["flag"]);
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = before;
            }
        }

        [Fact]
        public void AnItemOfAKindTheViewerDoesNotKnow_IsKeptListedAndWrittenAgain()
        {
            const string json = "{\"format\":\"ghscene\",\"version\":1,\"items\":[{\"name\":\"x\",\"layer\":\"l\",\"kind\":\"GeoHologram9\",\"data\":{\"q\":[1,2]}}]}";

            Scene read = SceneReader.Read(json);

            Assert.Equal(new[] { "GeoHologram9" }, read.UnknownKinds);
            Assert.Equal("GeoHologram9", read.Items.Single().Kind);
            Assert.Contains("\"q\":[1,2]", new SceneWriter(read).ToJson(), StringComparison.Ordinal);
            Assert.Throws<NotSupportedException>(() => read.Items.Single().ToGeometry());
            Assert.Single(Primitives.Build(read).Warnings);
        }

        [Fact]
        public void ATextThatIsNotAScene_IsRefusedWithAMessage()
        {
            Assert.Throws<FormatException>(() => SceneReader.ReadJson("{ not json"));
            Assert.Throws<FormatException>(() => SceneReader.ReadJson("[1,2]"));
            Assert.Throws<FormatException>(() => SceneReader.ReadJson("{\"format\":\"other\"}"));
            Assert.Throws<FormatException>(() => SceneReader.ReadJson("{\"version\":99}"));
        }

        [Fact]
        public void ALabel_ReadsBackWithItsTextAndHeight()
        {
            var writer = new SceneWriter().Text(new GeoPoint3(1.5, 2.5, 3.5), "note \"quoted\"", 12.5, layer: "notes");
            SceneItem item = SceneReader.Read(writer.ToJson()).Items.Single();

            Assert.Equal(SceneKinds.Text, item.Kind);
            Assert.Equal("note \"quoted\"", item.Data.GetProperty("text").GetString());
            Assert.Equal(12.5, item.Data.GetProperty("height").GetDouble());
            Assert.Throws<NotSupportedException>(() => item.ToGeometry());
        }

        [Fact]
        public void Save_WritesTheSameTextToAFile()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ghscene-" + Guid.NewGuid().ToString("N") + ".ghscene.json");

            try
            {
                var writer = new SceneWriter("saved").Add(new GeoPoint2(1.25, 2.5));
                writer.Save(path);

                Assert.Equal(writer.ToJson(), System.IO.File.ReadAllText(path));
                Assert.Equal("saved", SceneReader.Read(path).Title);
                Assert.False(System.IO.File.ReadAllBytes(path).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}

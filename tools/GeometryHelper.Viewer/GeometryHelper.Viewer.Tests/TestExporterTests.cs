using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GeometryHelper.Geometry;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// What the Roslyn tests of the viewer compile against: the framework, the library, and the test framework.
    /// </summary>
    internal static class RoslynReferences
    {
        internal static IReadOnlyList<MetadataReference> All()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var references = new List<MetadataReference>();
            string trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
            var files = trusted.Split(Path.PathSeparator).Where(f => f.Length > 0).Where(f =>
            {
                string name = Path.GetFileNameWithoutExtension(f);
                return name.StartsWith("System.", StringComparison.Ordinal) || name == "System" || name == "netstandard" || name == "mscorlib" || name == "Microsoft.CSharp";
            }).ToList();

            files.Add(typeof(Tolerance).Assembly.Location);
            files.AddRange(Directory.GetFiles(AppContext.BaseDirectory, "xunit*.dll"));

            foreach (string file in files.Where(f => seen.Add(Path.GetFileName(f))))
            {
                references.Add(MetadataReference.CreateFromFile(file));
            }

            return references;
        }
    }

    public class TestExporterTests
    {
        public static IEnumerable<object[]> Kinds => SampleShapes.KindData;

        private static ScriptOptions Options()
            => ScriptOptions.Default.WithReferences(RoslynReferences.All()).WithImports("System", "System.Linq", "System.Collections.Generic", "GeometryHelper", "GeometryHelper.Geometry");

        private static bool Same(object a, object b)
        {
            if (a is Array arrayA && b is Array arrayB)
            {
                return arrayA.Length == arrayB.Length && arrayA.Cast<object>().Zip(arrayB.Cast<object>(), Same).All(x => x);
            }

            return GeometryEncoder.Encode(a).GetRawText() == GeometryEncoder.Encode(b).GetRawText();
        }

        [Theory]
        [MemberData(nameof(Kinds))]
        public async Task TheCodeWrittenForAShape_BuildsTheShapeTheItemHolds_ToTheBit(string kind)
        {
            SceneItem item = new SceneWriter().Add(SampleShapes.Make(kind, 11)).Scene.Items.Single();

            string code = TestExporter.Expression(item, 0);
            object built = await CSharpScript.EvaluateAsync(code, Options());
            object expected = item.ToGeometry();

            Assert.True(Same(expected, built), kind + ": " + code);
        }

        [Fact]
        public async Task ANumberThatIsNotRoundOrNegativeZeroOrAnInteger_IsWrittenSoItReadsBackAsTheSameBits()
        {
            double[] values = { 0.1, 1E-300, 1.0 / 3.0, 123456789.12345678, -0.0, 0.0, 5.0, -2.0, 1E21, double.Epsilon };

            foreach (double value in values)
            {
                double read = (double)await CSharpScript.EvaluateAsync(TestExporter.D(value), Options());
                Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(read));
            }

            Assert.Equal("double.NaN", TestExporter.D(double.NaN));
            Assert.Equal("5.0", TestExporter.D(5.0));
            Assert.Equal("-0.0", TestExporter.D(-0.0));
        }

        private static Scene MixedScene()
        {
            var writer = new SceneWriter("mixed");
            writer.Scene.SetTolerance(new Tolerance(0.002, 1E-5, 0.01, 0.02));
            writer.Add(new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 60), new GeoPoint2(0, 60) }, new[] { 0.0, 0.5, 0.0, 0.0 }), layer: "source", name: "star 2:116");
            writer.Add(SampleShapes.Box(30, 20, 10, true), name: "box with a hole");
            writer.Add(SampleShapes.Make("GeoFace2", 5), name: "plate");
            writer.Add(SampleShapes.Make("GeoMesh3", 5), name: "mesh");
            writer.Add(new GeoPoint3(1, 2, 3), name: "point");
            writer.Add(new GeoRay2(new GeoPoint2(0, 0), new GeoVector2(1, 0)), name: "ray");
            writer.Text(new GeoPoint3(0, 0, 0), "a label");
            return writer.Scene;
        }

        private static (byte[] Image, string Errors) Compile(string source)
        {
            CSharpCompilation compilation = CSharpCompilation.Create(
                "ExportedTests" + Guid.NewGuid().ToString("N"),
                new[] { CSharpSyntaxTree.ParseText(source) },
                RoslynReferences.All(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Disable));

            using (var stream = new MemoryStream())
            {
                var result = compilation.Emit(stream);
                string errors = string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));
                return (result.Success ? stream.ToArray() : null, errors);
            }
        }

        [Fact]
        public void TheExportedFile_CompilesAgainstTheLibrary()
        {
            Scene scene = MixedScene();
            string text = TestExporter.Export(scene, scene.Items);

            var (image, errors) = Compile(text);

            Assert.True(image != null, errors + "\n" + text);
        }

        [Fact]
        public void EveryExportedTest_IsRedUntilTheExpectedValuesAreWritten()
        {
            Scene scene = MixedScene();
            string text = TestExporter.Export(scene, scene.Items);
            var (image, errors) = Compile(text);
            Assert.True(image != null, errors);

            Type type = Assembly.Load(image).GetType("GeometryHelper.UnitTest.ViewerExportTests", true);
            object instance = Activator.CreateInstance(type);
            MethodInfo[] tests = type.GetMethods().Where(m => m.GetCustomAttribute<FactAttribute>() != null).OrderBy(m => m.Name, StringComparer.Ordinal).ToArray();

            Assert.Equal(6, tests.Length);

            foreach (MethodInfo test in tests)
            {
                TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() => test.Invoke(instance, null));
                Assert.IsAssignableFrom<Xunit.Sdk.XunitException>(thrown.InnerException);
            }
        }

        [Fact]
        public void ABodyWithTheOpeningOfItsOwn_GetsOneAssertForEachNumberTheLibraryGives()
        {
            Scene scene = MixedScene();
            string text = TestExporter.Export(scene, new[] { scene.Items[0] });

            Assert.Equal(3, CountOf(text, "Assert.Equal("));
            Assert.Contains("the viewer measured ", text, StringComparison.Ordinal);
            Assert.Contains("int expectedVertices = -1;", text, StringComparison.Ordinal);
            Assert.Contains("double expectedLength = double.NaN;", text, StringComparison.Ordinal);
            Assert.Contains("double expectedArea = double.NaN;", text, StringComparison.Ordinal);
            Assert.Contains("public void Star2116_HasTheExpectedNumbers()", text, StringComparison.Ordinal);
        }

        [Fact]
        public void TheValueTheViewerMeasured_IsWrittenAs17DigitsInTheComment()
        {
            Scene scene = MixedScene();
            string text = TestExporter.Export(scene, new[] { scene.Items[0] });
            double length = double.Parse(ItemMeasures.Numbers(scene.Items[0], scene).First(n => n.Key == "Length").Value, System.Globalization.CultureInfo.InvariantCulture);

            Assert.Contains("the viewer measured " + length.ToString("G17", System.Globalization.CultureInfo.InvariantCulture) + ".", text, StringComparison.Ordinal);
        }

        [Fact]
        public void TheToleranceOfTheScene_IsPassedExplicitly_AndNeverSetGlobally()
        {
            Scene scene = MixedScene();
            string text = TestExporter.Export(scene, new[] { scene.Items[0] });

            Assert.Contains("var tolerance = new Tolerance(0.002, 1.0000000000000001E-05, 0.01, 0.02);", text, StringComparison.Ordinal);
            Assert.Contains("using (Tolerance.Use(tolerance))", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Tolerance.Global", text, StringComparison.Ordinal);
        }

        [Fact]
        public void ALabelAndAnUnknownKind_AreLeftOutAndSaidSo()
        {
            Scene scene = MixedScene();
            var unknown = new SceneItem { Name = "odd", Layer = "default", Kind = "GeoFuture9" };
            string text = TestExporter.Export(scene, new[] { scene.Items[6], unknown });

            Assert.DoesNotContain("[Fact]", text, StringComparison.Ordinal);
            Assert.Contains("// Not written: Text 1 (Text)", text, StringComparison.Ordinal);
            Assert.Contains("// Not written: odd (GeoFuture9)", text, StringComparison.Ordinal);
        }

        [Fact]
        public void TwoItemsOfOneName_GetTwoTestsOfDifferentNames()
        {
            var writer = new SceneWriter();
            writer.Add(new GeoPoint2(1, 1), name: "same");
            writer.Add(new GeoPoint2(2, 2), name: "same");

            string text = TestExporter.Export(writer.Scene, writer.Scene.Items);

            Assert.Contains("public void Same_HasTheExpectedNumbers()", text, StringComparison.Ordinal);
            Assert.Contains("public void Same2_HasTheExpectedNumbers()", text, StringComparison.Ordinal);
            Assert.True(Compile(text).Image != null);
        }

        [Fact]
        public void AShapeWithNoNumberToAssert_IsAFailingTestThatSaysWhatIsMissing()
        {
            Scene scene = MixedScene();
            string text = TestExporter.Export(scene, new[] { scene.Items[5] });

            Assert.Contains("Assert.Fail(", text, StringComparison.Ordinal);
            Assert.Contains("TODO", text, StringComparison.Ordinal);
            Assert.True(Compile(text).Image != null);
        }

        [Fact]
        public void MoreItemsThanTheLimit_AreCutAndSaidSo()
        {
            var writer = new SceneWriter();

            for (int i = 0; i < TestExporter.MaxItems + 3; i++)
            {
                writer.Add(new GeoPoint2(i, i), name: "p" + i);
            }

            string text = TestExporter.Export(writer.Scene, writer.Scene.Items);

            Assert.Equal(TestExporter.MaxItems, CountOf(text, "[Fact]"));
            Assert.Equal(3, CountOf(text, "// Not written: "));
            Assert.True(Compile(text).Image != null);
        }

        [Fact]
        public void TheText_HasLfLineEndsOnly_AndNamesNoFileOfTheRepository()
        {
            Scene scene = MixedScene();
            string text = TestExporter.Export(scene, scene.Items);

            Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
            Assert.EndsWith("}\n", text, StringComparison.Ordinal);
            Assert.StartsWith("// Written by the GeometryHelper viewer", text, StringComparison.Ordinal);
        }

        [Fact]
        public void NullArguments_AreRefused()
        {
            Assert.Throws<ArgumentNullException>(() => TestExporter.Export(null, new SceneItem[0]));
            Assert.Throws<ArgumentNullException>(() => TestExporter.Export(new Scene(), null));
        }

        private static int CountOf(string text, string what)
        {
            int count = 0;

            for (int at = text.IndexOf(what, StringComparison.Ordinal); at >= 0; at = text.IndexOf(what, at + what.Length, StringComparison.Ordinal))
            {
                count++;
            }

            return count;
        }
    }
}

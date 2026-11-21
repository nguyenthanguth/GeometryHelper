using System;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace GeometryHelper.PyStubs.UnitTest
{
    /// <summary>The generator on the real assemblies: GeometryHelper, GeometryHelper.Cad and GeometryHelper.Cad.Remote.</summary>
    public class LibraryStubsTests
    {
        private static readonly Lazy<StubRun> Run = new Lazy<StubRun>(() => StubRun.OfTheLibrary());

        [Fact]
        public void TheThreeAssemblies_GiveOneModuleForEachOfTheirEighteenNamespaces()
        {
            StubReport report = Run.Value.Report;

            Assert.Equal(18, report.Modules.Count);
            Assert.Equal(
                new[] { "GeometryHelper", "GeometryHelper.Cad", "GeometryHelper.Cad.Remote" },
                report.Assemblies.Select(a => a.Name)
            );
            Assert.All(report.Assemblies, a => Assert.True(a.Types > 0 && a.Members > a.Types, a.Name));
            Assert.Equal(18, Run.Value.Files().Length);
        }

        [Fact]
        public void GeoPoint3DistanceTo_IsWrittenAsOverloads_OneForEachShape()
        {
            string geometry = Run.Value.Module("Geometry");
            string point = geometry.Substring(geometry.IndexOf("\nclass GeoPoint3(", StringComparison.Ordinal));
            point = point.Substring(0, point.IndexOf("\nclass ", 1, StringComparison.Ordinal));

            Assert.True(Regex.Matches(point, @"@overload\n    def DistanceTo\(self, ").Count >= 10);
            Assert.Contains("    def DistanceTo(self, other: GeoPoint3) -> float:", point);
            Assert.Contains("class GeoPoint3(System.ValueType):", point);
        }

        [Fact]
        public void CadApplication_IsAContextManager_AndTheKindOfAFilterTakesAPythonClass()
        {
            string remote = Run.Value.Module("Cad/Remote");
            string application = StubRun.ClassOf(remote, "CadApplication");
            string filter = StubRun.ClassOf(remote, "CadObjectFilter");

            Assert.Contains("    def __enter__(self) -> Self: ...\n", application);
            Assert.Contains("    def __exit__(self, *args: object) -> None: ...\n", application);
            Assert.Contains("    def Kind(self) -> System.Type:\n", filter);
            Assert.Contains("    def Kind(self, value: System.Type | type[Any]) -> None: ...\n", filter);
        }

        [Fact]
        public void TheTypesWrittenAsAny_AreTheTwoThatTheTableDoesNotKnow()
        {
            Assert.Equal(new[] { "System.Double[,]", "System.Xml.Linq.XDocument" }, Run.Value.Report.AnyMapped);
        }

        [Fact]
        public void TheMembersLeftOut_AreTheOnesNamedNoneTrueOrFalse()
        {
            Assert.NotEmpty(Run.Value.Report.LeftOut);
            Assert.All(Run.Value.Report.LeftOut, item =>
                Assert.Matches(@"^keyword name: GeometryHelper(\.[A-Za-z0-9]+)+\.(None|True|False)$", item)
            );
        }

        [Fact]
        public void ARerun_WritesTheSameBytes()
        {
            StubRun again = StubRun.OfTheLibrary("library-again");

            Assert.Equal(Run.Value.Files().Select(f => f.Relative), again.Files().Select(f => f.Relative));
            Assert.All(Run.Value.Files().Zip(again.Files(), (a, b) => (a, b)), pair =>
                Assert.Equal(pair.a.Bytes, pair.b.Bytes)
            );
        }

        [Fact]
        public void TheStubs_HaveLineFeedsOnlyAndNoByteOrderMark()
        {
            foreach ((string relative, byte[] bytes) in Run.Value.Files())
            {
                Assert.DoesNotContain((byte)'\r', bytes);
                Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, relative);
            }
        }
    }
}

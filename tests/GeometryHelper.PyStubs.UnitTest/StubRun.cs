using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace GeometryHelper.PyStubs.UnitTest
{
    /// <summary>One run of the generator on a folder of assemblies, with the stubs it wrote under the test output folder.</summary>
    public sealed class StubRun
    {
        private StubRun(string lib, string name)
        {
            Output = Path.Combine(AppContext.BaseDirectory, "stubs-out", name);
            Report = StubGenerator.Generate(
                Path.Combine(AppContext.BaseDirectory, lib),
                Output,
                Path.Combine(AppContext.BaseDirectory, "netstandard")
            );
        }

        /// <summary>Runs the generator on the hand-made assembly of the tests.</summary>
        public static StubRun OfTheFixture(string name = "fixture") => new StubRun("fixture", name);

        /// <summary>Runs the generator on the real assemblies (GeometryHelper, GeometryHelper.Cad and GeometryHelper.Cad.Remote).</summary>
        public static StubRun OfTheLibrary(string name = "library") => new StubRun("lib", name);

        /// <summary>The folder the stubs were written into, which holds <c>GeometryHelper-stubs</c>.</summary>
        public string Output { get; }

        /// <summary>What the generator said it wrote and left out.</summary>
        public StubReport Report { get; }

        /// <summary>The text of the stub of a namespace, <c>Geometry</c> for <c>GeometryHelper.Geometry</c> ("" for the root).</summary>
        public string Module(string relative) =>
            File.ReadAllText(
                Path.Combine(
                    Output,
                    "GeometryHelper-stubs",
                    relative.Replace('/', Path.DirectorySeparatorChar),
                    "__init__.pyi"
                ),
                new UTF8Encoding(false, true)
            );

        /// <summary>The text of one class of a module: from its <c>class</c> line to the next class, or to the end.</summary>
        public static string ClassOf(string module, string name)
        {
            int start = module.IndexOf("\nclass " + name + "(", StringComparison.Ordinal);
            Assert.NotEqual(-1, start);
            int end = module.IndexOf("\nclass ", start + 1, StringComparison.Ordinal);
            return end < 0 ? module.Substring(start) : module.Substring(start, end - start);
        }

        /// <summary>Every file the run wrote, as a path inside <c>GeometryHelper-stubs</c> and its bytes, in path order.</summary>
        public (string Relative, byte[] Bytes)[] Files() =>
            Directory
                .GetFiles(Path.Combine(Output, "GeometryHelper-stubs"), "*", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => (Path.GetRelativePath(Output, f), File.ReadAllBytes(f)))
                .ToArray();
    }
}

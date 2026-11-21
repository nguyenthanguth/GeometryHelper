using System;
using System.IO;
using System.Text;

namespace GeometryHelper.PyStubs
{
    /// <summary>
    /// <c>GeometryHelper.PyStubs --lib &lt;folder&gt; --out &lt;folder&gt;</c>: writes the Python stubs of the GeometryHelper
    /// assemblies in the <c>lib</c> folder (with their XML docs) to <c>GeometryHelper-stubs</c> in the <c>out</c> folder, and
    /// prints one line per assembly, the members left out and the types written as <c>typing.Any</c>. The exit code is 0
    /// when the stubs were written and 1 on any error, with the reason on standard error.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            Console.Out.NewLine = "\n";
            Console.Error.NewLine = "\n";

            string lib = null;
            string output = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--lib" && i + 1 < args.Length)
                {
                    lib = args[++i];
                }
                else if (args[i] == "--out" && i + 1 < args.Length)
                {
                    output = args[++i];
                }
                else
                {
                    return Fail("Unknown argument: " + args[i]);
                }
            }

            if (lib == null || output == null)
            {
                return Fail("Usage: GeometryHelper.PyStubs --lib <folder with the DLLs and XML docs> --out <folder>");
            }

            try
            {
                StubReport report = StubGenerator.Generate(
                    Path.GetFullPath(lib),
                    Path.GetFullPath(output),
                    Path.Combine(AppContext.BaseDirectory, "netstandard")
                );
                Console.Out.Write(report.Summary());
                return 0;
            }
            catch (Exception e)
            {
                return Fail(e.Message);
            }
        }

        private static int Fail(string message)
        {
            Console.Error.WriteLine("error: " + message);
            return 1;
        }
    }
}

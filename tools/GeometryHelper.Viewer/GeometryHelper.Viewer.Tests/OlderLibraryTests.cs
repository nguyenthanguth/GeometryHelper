using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeometryHelper.Viewer.Builds;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// A library of an older version than the one the viewer was built against has to load in the runner: the runtime refuses a lower version, and the
    /// runner puts its own resolution in front of that.
    /// </summary>
    public class OlderLibraryTests : IClassFixture<CompareFixture>
    {
        private readonly CompareFixture _fixture;

        public OlderLibraryTests(CompareFixture fixture)
        {
            _fixture = fixture;
        }

        private static string FindRepositoryRoot()
        {
            for (DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "src", "GeometryHelper", "GeometryHelper.csproj")))
                {
                    return directory.FullName;
                }
            }

            return null;
        }

        private static void CopySources(string from, string to)
        {
            Directory.CreateDirectory(to);

            foreach (string file in Directory.GetFiles(from))
            {
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
            }

            foreach (string directory in Directory.GetDirectories(from).Where(d => !new[] { "bin", "obj" }.Contains(Path.GetFileName(d))))
            {
                CopySources(directory, Path.Combine(to, Path.GetFileName(directory)));
            }
        }

        [Fact]
        public async Task ALibraryOfALowerVersion_LoadsInTheRunnerAndRunsAScript()
        {
            string root = FindRepositoryRoot();
            Assert.NotNull(root);

            using (var folder = new TempFolder("ghviewer-older"))
            {
                // The library as it stands in this work tree, copied and built under the version 12.1.0, which is lower than the viewer's own.
                string tree = folder.Combine("tree");
                Directory.CreateDirectory(Path.Combine(tree, "src"));
                CopySources(Path.Combine(root, "src", "GeometryHelper"), Path.Combine(tree, "src", "GeometryHelper"));

                foreach (string name in new[] { "Directory.Build.props", "Directory.Build.targets", "icon.png" })
                {
                    if (File.Exists(Path.Combine(root, name)))
                    {
                        File.Copy(Path.Combine(root, name), Path.Combine(tree, name));
                    }
                }

                var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = tree };

                foreach (string argument in new[] { "build", Path.Combine(tree, "src", "GeometryHelper", "GeometryHelper.csproj"), "-c", "Release", "-nologo", "-v:q", "-p:GeometryHelperVersion=12.1.0", "-p:GeneratePackageOnBuild=false" })
                {
                    info.ArgumentList.Add(argument);
                }

                using (Process build = Process.Start(info))
                {
                    string output = await build.StandardOutput.ReadToEndAsync();
                    string errors = await build.StandardError.ReadToEndAsync();
                    build.WaitForExit();
                    Assert.True(build.ExitCode == 0, output + errors);
                }

                string older = Path.Combine(tree, "src", "GeometryHelper", "bin", "Release", "netstandard2.0", "GeometryHelper.dll");
                Assert.Equal(new Version(12, 1, 0, 0), System.Reflection.AssemblyName.GetAssemblyName(older).Version);
                Assert.True(System.Reflection.AssemblyName.GetAssemblyName(typeof(Tolerance).Assembly.Location).Version > new Version(12, 1, 0, 0));

                string script = folder.Combine("run.csx");
                File.WriteAllText(script, "Show(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(0, 10)), name: \"tri\");");

                SideRunResult result = await SideRunner.RunAsync(_fixture.Environment, script, BuildSource.FromDll(older), "left", null, CancellationToken.None);

                Assert.True(result.Succeeded, string.Join("\n", result.Errors));
                Assert.EndsWith(" 12.1.0.0", result.LibraryLine, StringComparison.Ordinal);
                Assert.Equal(50.0, RunnerMeasures.Read(result.Scene.Items.Single()).Area);
            }
        }
    }
}

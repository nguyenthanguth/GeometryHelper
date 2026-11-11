using System;
using System.IO;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The folder of this run of the tests, %TEMP%\gk\drawing\viewer-tests-&lt;a new id&gt;: the pictures and drawings the tests write
    /// go under it, so that two runs at once, as two worktrees testing side by side, share no file. It is deleted when the
    /// run ends.
    /// </summary>
    internal static class TestRun
    {
        private static readonly string Root = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "drawing",
            "viewer-tests-" + Guid.NewGuid().ToString("N")
        );

        static TestRun()
        {
            AppDomain.CurrentDomain.DomainUnload += (s, e) => Delete();
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Delete();
        }

        /// <summary>A folder under the run's own, of the names given, made where missing.</summary>
        public static string Folder(params string[] names)
        {
            string folder = names.Length == 0 ? Root : Path.Combine(Root, Path.Combine(names));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static void Delete()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, true);
                }
            }
            catch (IOException)
            {
                // A file still held by the system is left for the next clean of %TEMP%.
            }
            catch (UnauthorizedAccessException) { }
        }
    }
}

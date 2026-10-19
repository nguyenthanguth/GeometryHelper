using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GeometryHelper.Viewer.Scripting;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// How a file was opened.
    /// </summary>
    internal sealed class LoadResult
    {
        internal bool Succeeded { get; set; }

        internal Scene Scene { get; set; }

        internal IReadOnlyList<string> Notes { get; set; } = new string[0];

        internal IReadOnlyList<string> Errors { get; set; } = new string[0];

        internal bool WasScript { get; set; }
    }

    /// <summary>
    /// Opens the files the viewer shows: a C# script, which is run, a scene file, which is read, and an OBJ, DXF, WKT or SVG file, which is imported.
    /// </summary>
    internal static class SceneLoader
    {
        internal static bool IsScript(string path) => string.Equals(Path.GetExtension(path), ".csx", StringComparison.OrdinalIgnoreCase);

        internal static bool IsSceneFile(string path) => path.EndsWith(".ghscene.json", StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Opens a file. Nothing in it makes this throw, but running out of memory: whatever goes wrong comes back in the errors.
        /// </summary>
        /// <param name="path">The script or scene file.</param>
        /// <param name="note">Told each line a script says as it says it.</param>
        /// <param name="cancel">Stops a script.</param>
        internal static async Task<LoadResult> LoadAsync(string path, Action<string> note, CancellationToken cancel)
        {
            try
            {
                if (IsScript(path))
                {
                    string code = File.ReadAllText(path, Encoding.UTF8);
                    ScriptRunResult run = await ScriptHost.RunAsync(code, path, note, cancel).ConfigureAwait(false);

                    return new LoadResult { Succeeded = run.Succeeded, Scene = run.Scene, Notes = run.Notes, Errors = run.Errors, WasScript = true };
                }

                if (IsSceneFile(path))
                {
                    Scene scene = await Task.Run(() => SceneReader.ReadFile(path), cancel).ConfigureAwait(false);

                    if (string.IsNullOrEmpty(scene.Title))
                    {
                        scene.Title = Path.GetFileName(path);
                    }

                    return new LoadResult { Succeeded = true, Scene = scene };
                }

                if (SceneImport.IsImportable(path))
                {
                    Scene scene = await Task.Run(() => SceneImport.ReadFile(path, cancel), cancel).ConfigureAwait(false);
                    return new LoadResult { Succeeded = true, Scene = scene };
                }

                return new LoadResult { Errors = new[] { path + ": a script ends in .csx, a scene in .ghscene.json, and the files of the library in .obj, .dxf, .wkt or .svg." } };
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                // Whatever is wrong with a file, the caller gets it back as a message and the scene before stays.
                return new LoadResult { Errors = new[] { path + ": " + e.GetType().Name + ": " + e.Message } };
            }
        }
    }
}

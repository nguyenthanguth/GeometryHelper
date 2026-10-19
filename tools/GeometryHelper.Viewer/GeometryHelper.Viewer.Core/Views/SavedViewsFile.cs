using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// The views a person saved in the window while looking at the scene a script made, kept beside the script so that they are still there when the script
    /// is run again: the scene a script makes is made new each time, and the views saved in it would go with it. The file is <c>box.csx.views.json</c> beside
    /// <c>box.csx</c>, and holds only the views made in the window; the views the script makes itself, with <c>Scene.View</c>, are in the script.
    /// </summary>
    public static class SavedViewsFile
    {
        /// <summary>
        /// The name of the format in the header of the file.
        /// </summary>
        public const string Format = "ghviews";

        /// <summary>
        /// Gets the file that holds the views saved in the window for a script.
        /// </summary>
        /// <param name="scriptPath">The path of the script.</param>
        /// <returns>The path with <c>.views.json</c> added, beside the script.</returns>
        /// <exception cref="ArgumentException">Thrown when the path is empty.</exception>
        public static string PathFor(string scriptPath)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                throw new ArgumentException("The path of the script must not be empty.", nameof(scriptPath));
            }

            return scriptPath + ".views.json";
        }

        /// <summary>
        /// Reads the views of a file; none where there is no file.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <param name="warnings">Gets a line for each view in the file that was not one, or for a file that could not be read; they are left out and the rest is read.</param>
        /// <returns>The views, in the order of the file.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path or the list of warnings is null.</exception>
        public static List<SavedView> Read(string path, List<string> warnings)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (warnings == null)
            {
                throw new ArgumentNullException(nameof(warnings));
            }

            return ReadCore(path, warnings, out _);
        }

        /// <summary>
        /// Reads a file, and says whether it could be: false for a file that is there and that could not be read or is not a file of views, which is not the same as one with no views in it.
        /// </summary>
        private static List<SavedView> ReadCore(string path, List<string> warnings, out bool readable)
        {
            var views = new List<SavedView>();
            readable = true;

            if (!File.Exists(path))
            {
                return views;
            }

            try
            {
                using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }))
                {
                    if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("views", out JsonElement list) && list.ValueKind == JsonValueKind.Array)
                    {
                        SceneFile.ReadViews(list, views, warnings, Path.GetFileName(path));
                    }
                    else
                    {
                        readable = false;
                        warnings.Add(Path.GetFileName(path) + " is not a file of views (it has no list called views), and its views are left out");
                    }
                }
            }
            catch (Exception e) when (e is JsonException || e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                readable = false;
                warnings.Add(Path.GetFileName(path) + " could not be read, and its views are left out: " + e.Message);
            }

            return views;
        }

        /// <summary>
        /// Writes the views to a file, replacing it: a line to a view, every number in the 17 digits that read back as the same double, as in a scene. The text goes to a file beside it first and
        /// takes the place of the old one in one step, so that a file is never left half written.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <param name="views">The views.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path or the views are null.</exception>
        public static void Write(string path, IEnumerable<SavedView> views)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (views == null)
            {
                throw new ArgumentNullException(nameof(views));
            }

            var text = new StringBuilder();
            text.Append("{\n\"format\":\"").Append(Format).Append("\",\n\"version\":1,\n\"views\":[\n");
            text.Append(string.Join(",\n", views.Select(SceneFile.ViewLine)));
            text.Append("\n]\n}\n");
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                File.WriteAllText(temporary, text.ToString(), new UTF8Encoding(false));

                if (File.Exists(path))
                {
                    File.Replace(temporary, path, null);
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            catch
            {
                try
                {
                    File.Delete(temporary);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    // The file beside it is left; the fault that matters is the one that is thrown.
                }

                throw;
            }
        }

        /// <summary>
        /// Puts one view into a file: in place of the view of the same name that it holds, else at the end. A file that is there and cannot be read, or is not a file of views, is left as it is:
        /// writing the one view over it would lose the others.
        /// </summary>
        /// <param name="path">The file; made if there is none.</param>
        /// <param name="view">The view.</param>
        /// <param name="warnings">Told what was wrong in the file that was read, and that it was left as it was.</param>
        /// <returns>true when the view was written; false when the file could not be read and was left alone.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path, the view or the list of warnings is null.</exception>
        public static bool Keep(string path, SavedView view, List<string> warnings)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (warnings == null)
            {
                throw new ArgumentNullException(nameof(warnings));
            }

            List<SavedView> views = ReadCore(path, warnings, out bool readable);

            if (!readable)
            {
                warnings.Add(Path.GetFileName(path) + " was left as it is, and the view '" + view.Name + "' was not kept in it: mend or remove the file to keep views beside the script");
                return false;
            }

            Put(views, view);
            Write(path, views);
            return true;
        }

        /// <summary>
        /// Puts the views of a file into the views of a scene: a view of a name the scene has takes its place, the others come after the scene's own, in the order of the file. The first
        /// view of the scene, which a scene opens at, stays the first.
        /// </summary>
        /// <param name="scene">The scene a script made.</param>
        /// <param name="kept">The views read from the file.</param>
        /// <exception cref="ArgumentNullException">Thrown when the scene or the views are null.</exception>
        public static void Merge(Scene scene, IEnumerable<SavedView> kept)
        {
            if (scene == null)
            {
                throw new ArgumentNullException(nameof(scene));
            }

            if (kept == null)
            {
                throw new ArgumentNullException(nameof(kept));
            }

            foreach (SavedView view in kept)
            {
                Put(scene.Views, view);
            }
        }

        private static void Put(List<SavedView> views, SavedView view)
        {
            int at = views.FindIndex(v => string.Equals(v.Name, view.Name, StringComparison.Ordinal));

            if (at >= 0)
            {
                views[at] = view;
            }
            else
            {
                views.Add(view);
            }
        }
    }
}

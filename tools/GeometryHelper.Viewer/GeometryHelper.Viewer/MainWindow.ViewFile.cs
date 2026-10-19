using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// The views saved in the window while looking at the scene of a script, kept in a file beside the script so that they are there when the script is run again.
    /// </summary>
    public partial class MainWindow
    {
        // The files of views that may be written inside a Git repository: the person was asked once for each, and said yes.
        private readonly HashSet<string> _viewFilesAllowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the script whose scene is shown, or null when the scene was read from a file or there is none.
        /// </summary>
        private string ViewScript => _currentPath != null && SceneLoader.IsScript(_currentPath) ? _currentPath : null;

        /// <summary>
        /// Keeps a view saved in the window in the file beside the script that made the scene, in place of the view of the same name. Inside a Git repository, as the samples
        /// are, the first one asks, as saving a scene there does; a run with no window, which cannot ask, writes nothing there.
        /// </summary>
        private void KeepViewFile(SavedView view)
        {
            string script = ViewScript;

            if (script == null)
            {
                return;
            }

            string path = SavedViewsFile.PathFor(script);

            if (InsideARepository(path) && !_viewFilesAllowed.Contains(path))
            {
                if (_command.Headless)
                {
                    Log("view '" + view.Name + "' is not kept beside the script: " + path + " would be inside a Git repository, which a run with no window does not write to.");
                    return;
                }

                MessageBoxResult answer = MessageBox.Show(
                    this,
                    "The views you save are kept in a file beside the script, and this folder is inside a Git repository. The file holds only views of the camera, but a view can point at a model that must never be committed.\n\nKeep the views in\n" + path + "\n?",
                    "Keep the views beside the script",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (answer != MessageBoxResult.Yes)
                {
                    Log("view '" + view.Name + "' is kept in this scene only; it is lost when the script is run again.");
                    return;
                }

                _viewFilesAllowed.Add(path);
            }

            try
            {
                var warnings = new List<string>();
                bool kept = SavedViewsFile.Keep(path, view, warnings);

                foreach (string warning in warnings)
                {
                    Log("warning: " + warning);
                }

                Log(kept
                    ? "view '" + view.Name + "' kept in " + path + "; it comes back when the script is run again."
                    : "view '" + view.Name + "' is kept in this scene only; it is lost when the script is run again.");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log("Cannot keep the view beside the script (" + path + "): " + e.Message);
            }
        }

        /// <summary>
        /// Puts the views kept beside a script into the scene it has made, after its own: a script run again has them, whatever it makes.
        /// </summary>
        private void MergeKeptViews(string scriptPath, Scene scene)
        {
            if (scriptPath == null || scene == null || !SceneLoader.IsScript(scriptPath))
            {
                return;
            }

            var warnings = new List<string>();
            List<SavedView> kept = SavedViewsFile.Read(SavedViewsFile.PathFor(scriptPath), warnings);

            foreach (string warning in warnings)
            {
                Log("warning: " + warning);
            }

            if (kept.Count > 0)
            {
                SavedViewsFile.Merge(scene, kept);
                Log("views saved in the window: " + kept.Count.ToString(CultureInfo.InvariantCulture) + " came back from " + Path.GetFileName(SavedViewsFile.PathFor(scriptPath)));
            }
        }
    }
}

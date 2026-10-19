using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// What the viewer remembers between runs, in <c>%APPDATA%\GeometryHelper.Viewer\settings.json</c>: where the window was,
    /// how wide its panes are, the last folder, the render mode and the files opened lately.
    /// </summary>
    public sealed class Settings
    {
        /// <summary>
        /// The number of recent files kept.
        /// </summary>
        public const int MaxRecent = 10;

        /// <summary>
        /// Gets or sets the left edge of the window.
        /// </summary>
        public double? Left { get; set; }

        /// <summary>
        /// Gets or sets the top edge of the window.
        /// </summary>
        public double? Top { get; set; }

        /// <summary>
        /// Gets or sets the width of the window.
        /// </summary>
        public double Width { get; set; } = 1400.0;

        /// <summary>
        /// Gets or sets the height of the window.
        /// </summary>
        public double Height { get; set; } = 850.0;

        /// <summary>
        /// Gets or sets whether the window was maximized.
        /// </summary>
        public bool Maximized { get; set; }

        /// <summary>
        /// Gets or sets the width of the scene tree.
        /// </summary>
        public double TreeWidth { get; set; } = 260.0;

        /// <summary>
        /// Gets or sets the width of the properties pane.
        /// </summary>
        public double PropertiesWidth { get; set; } = 300.0;

        /// <summary>
        /// Gets or sets the height of the output pane.
        /// </summary>
        public double OutputHeight { get; set; } = 150.0;

        /// <summary>
        /// Gets or sets whether the properties pane is shown.
        /// </summary>
        public bool ShowProperties { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the diagnostics pane is shown.
        /// </summary>
        public bool ShowDiagnostics { get; set; }

        /// <summary>
        /// Gets or sets the width of the diagnostics pane.
        /// </summary>
        public double DiagnosticsWidth { get; set; } = 440.0;

        /// <summary>
        /// Gets or sets the checks that are ticked in the diagnostics pane, as the number of the flags; the defaults of the options where it is not set.
        /// </summary>
        public int? DiagnosticsTicked { get; set; }

        /// <summary>
        /// Gets or sets the folder a file was last opened from.
        /// </summary>
        public string LastFolder { get; set; }

        /// <summary>
        /// Gets or sets the render mode: Wireframe, Shaded, ShadedEdges or Transparent.
        /// </summary>
        public string RenderMode { get; set; } = "ShadedEdges";

        /// <summary>
        /// Gets or sets whether a script runs again when its file is saved.
        /// </summary>
        public bool RerunOnSave { get; set; }

        /// <summary>
        /// Gets or sets whether the grid on the plane z = 0 is drawn.
        /// </summary>
        public bool Grid { get; set; }

        /// <summary>
        /// Gets or sets the files opened lately, newest first.
        /// </summary>
        public List<string> Recent { get; set; } = new List<string>();

        /// <summary>
        /// Gets the folder the viewer keeps its files in.
        /// </summary>
        public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GeometryHelper.Viewer");

        /// <summary>
        /// Gets the path of the settings file.
        /// </summary>
        public static string FilePath => Path.Combine(Folder, "settings.json");

        /// <summary>
        /// Reads the settings; the defaults where there are none or they do not read.
        /// </summary>
        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath, Encoding.UTF8));

                    if (loaded != null)
                    {
                        loaded.Recent = loaded.Recent ?? new List<string>();
                        return loaded;
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is JsonException || e is UnauthorizedAccessException)
            {
            }

            return new Settings();
        }

        /// <summary>
        /// Writes the settings; a folder that cannot be written to is not worth stopping for.
        /// </summary>
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// Puts a file first among the recent ones.
        /// </summary>
        public void AddRecent(string path)
        {
            Recent.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            Recent.Insert(0, path);

            if (Recent.Count > MaxRecent)
            {
                Recent.RemoveRange(MaxRecent, Recent.Count - MaxRecent);
            }
        }
    }
}

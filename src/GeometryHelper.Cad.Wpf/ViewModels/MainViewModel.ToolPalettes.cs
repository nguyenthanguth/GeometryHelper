using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The tool palettes (TOOLPALETTES, TOOLPALETTESCLOSE, and the key given TOOLPALETTES), shown beside the window and left
    /// open: the set of palettes, read from the file beside the settings the first time it is asked for and written back after
    /// every change; running a tool, which is the command, hatch or INSERT of the tool started on the drawing shown; and the
    /// tools made from objects of the drawing and blocks of DesignCenter. With no window function set, the commands say they
    /// have no window.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private CadToolPalettes _toolPalettes;
        private bool _isToolPalettesShown;

        /// <summary>Gets or sets how the workspace shows the tool palettes, beside the window and left open (the one open is brought forward).</summary>
        public Action<ToolPalettesViewModel> ShowToolPalettes { get; set; }

        /// <summary>Gets or sets how the workspace closes the tool palettes (TOOLPALETTESCLOSE).</summary>
        public Action CloseToolPalettes { get; set; }

        /// <summary>Raised when the tool palettes changed other than by the palette window: DesignCenter made a palette of blocks, a palette was imported.</summary>
        public event EventHandler ToolPalettesChanged;

        /// <summary>Gets whether the tool palettes are open, as their window says.</summary>
        public bool IsToolPalettesShown
        {
            get => _isToolPalettesShown;
            private set => Set(ref _isToolPalettesShown, value);
        }

        /// <summary>
        /// Gets the file the palettes are kept in: <c>ToolPalettes.json</c> in the folder of <see cref="SettingsPath"/>, so that
        /// the palettes of a window with settings of its own are its own.
        /// </summary>
        public string ToolPalettesPath =>
            Path.Combine(
                Path.GetDirectoryName(
                    Path.GetFullPath(string.IsNullOrEmpty(SettingsPath) ? GlobalSettings.DefaultPath : SettingsPath)
                ),
                "ToolPalettes.json"
            );

        /// <summary>Gets the tool palettes, read from <see cref="ToolPalettesPath"/> when first asked for; the starter set where there is no file.</summary>
        public CadToolPalettes ToolPalettes =>
            _toolPalettes ?? (_toolPalettes = CadToolPalettes.Load(ToolPalettesPath));

        /// <summary>Tells the model the tool palettes are open or closed; the window calls it as it is shown and closed.</summary>
        /// <param name="shown">Whether they are open.</param>
        public void NoteToolPalettesShown(bool shown) => IsToolPalettesShown = shown;

        /// <summary>
        /// Writes the palettes to their file where they changed since they were read or last written; a file that cannot be
        /// written is said on the command line and logged, and the palettes stay as they are in the window.
        /// </summary>
        /// <returns>Whether the file is as the palettes are now.</returns>
        public bool SaveToolPalettes()
        {
            if (_toolPalettes == null || !_toolPalettes.IsModified)
            {
                return true;
            }

            try
            {
                _toolPalettes.Save(ToolPalettesPath);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                GeometryHelperLog.Warn(
                    "MainViewModel: the tool palettes were not saved to " + ToolPalettesPath + ".",
                    exception
                );
                SayFromDesignCenter("Cannot save the tool palettes: " + exception.Message);
                return false;
            }
        }

        /// <summary>Shows or hides the tool palettes, as the key given TOOLPALETTES does (AutoCAD's Ctrl+3).</summary>
        /// <returns>Whether it did something: there is a window to show them in.</returns>
        public bool ToggleToolPalettes()
        {
            if (_isToolPalettesShown && CloseToolPalettes != null)
            {
                CloseToolPalettes();
                return true;
            }

            if (ShowToolPalettes == null || _current == null)
            {
                return false;
            }

            StartCommand("TOOLPALETTES");
            return true;
        }

        /// <summary>
        /// Runs a tool on the drawing shown, as a click on it does: a command tool starts its command, a hatch tool HATCH and a
        /// block tool INSERT, each with the layer, colour, linetype and lineweight of the tool current while it runs. With a
        /// point (a tool dropped on the canvas) a block is placed there at once and a hatch fills the area round it.
        /// </summary>
        /// <param name="tool">The tool.</param>
        /// <param name="at">Where it is dropped, in the drawing; null to ask on the command line.</param>
        /// <returns>Whether it started; the command line says why where it did not.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the tool is null.</exception>
        public bool RunTool(CadTool tool, GeoPoint2? at = null)
        {
            if (tool == null)
            {
                throw new ArgumentNullException(nameof(tool));
            }

            if (_current == null || RefusedWhileOpening(tool.Name))
            {
                return false;
            }

            AttachTiles(_current);
            bool started = _current.Session.StartTool(tool, at);
            SyncHistory();
            RunAfterCommand();
            return started;
        }

        /// <summary>
        /// What a drag of a tool out of the palette does where it is dropped on the canvas: a block tool places its block there,
        /// a hatch tool hatches the area there; a command tool or any other is not dropped, as in AutoCAD.
        /// </summary>
        /// <param name="text">What <see cref="ToolPaletteDrag.Write"/> wrote.</param>
        /// <param name="at">Where it is dropped, in the drawing.</param>
        /// <returns>Whether the text was one a drag wrote and a tool was run.</returns>
        public bool DropToolPaletteTool(string text, GeoPoint2? at)
        {
            if (
                _current == null
                || !ToolPaletteDrag.TryRead(text, out int palette, out int index)
                || palette < 0
                || palette >= ToolPalettes.Palettes.Count
                || index < 0
                || index >= ToolPalettes.Palettes[palette].Tools.Count
            )
            {
                return false;
            }

            CadTool tool = ToolPalettes.Palettes[palette].Tools[index];
            return (tool.Kind == CadToolKind.Block || tool.Kind == CadToolKind.Hatch) && RunTool(tool, at);
        }

        /// <summary>
        /// Adds the objects dragged from the drawing onto a palette as tools, as AutoCAD does: a block reference a block tool, a
        /// hatch a hatch tool, a line, circle, polyline, text or dimension a command tool of the layer, colour, linetype and
        /// lineweight it has. Those that no tool draws are left out, said on the command line.
        /// </summary>
        /// <param name="palette">The palette they are added to.</param>
        /// <param name="handles">The handles of the objects, in the order they are added.</param>
        /// <param name="index">The place of the first in the palette's list; past the end puts them last.</param>
        /// <returns>The tools made, in order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the palette or the handles are null.</exception>
        public IReadOnlyList<CadTool> AddObjectsToToolPalette(
            CadToolPalette palette,
            IEnumerable<string> handles,
            int index = int.MaxValue
        )
        {
            if (palette == null)
            {
                throw new ArgumentNullException(nameof(palette));
            }

            if (handles == null)
            {
                throw new ArgumentNullException(nameof(handles));
            }

            var made = new List<CadTool>();
            int left = 0;

            if (_current != null)
            {
                foreach (string handle in handles)
                {
                    DxfEntity entity = _current.Document.FindEntity(handle);
                    CadTool tool = entity == null ? null : CadTool.FromObject(_current.Document, entity);

                    if (tool == null)
                    {
                        left++;
                        continue;
                    }

                    palette.Insert(index == int.MaxValue ? palette.Tools.Count : index + made.Count, tool);
                    made.Add(tool);
                }
            }

            if (made.Count > 0)
            {
                ToolPalettes.Touch();
                SaveToolPalettes();
            }

            SayFromDesignCenter(
                made.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + (made.Count == 1 ? " tool" : " tools")
                    + " added to "
                    + palette.Name
                    + "."
                    + (
                        left > 0
                            ? " "
                                + left.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                + " not drawn by any tool."
                            : string.Empty
                    )
            );
            return made;
        }

        /// <summary>
        /// Makes a palette of blocks of a drawing, as DesignCenter's Create Tool Palette does: named for the drawing (or as given),
        /// a block tool for each name, the drawing as its source; the palette is shown if the palettes are open.
        /// </summary>
        /// <param name="file">The drawing's file.</param>
        /// <param name="names">The blocks' names; none for every block the drawing has, in ordinal order.</param>
        /// <param name="name">The palette's name; null for the drawing's name without its extension.</param>
        /// <returns>The palette; null where the drawing cannot be read or has no block.</returns>
        public CadToolPalette CreateToolPaletteOfBlocks(
            string file,
            IEnumerable<string> names = null,
            string name = null
        )
        {
            if (string.IsNullOrEmpty(file))
            {
                return null;
            }

            List<string> blocks = names?.ToList() ?? new List<string>();

            if (blocks.Count == 0)
            {
                try
                {
                    blocks = CadDesignCenterDrawing
                        .Open(file)
                        .Items(CadDesignCenterKind.Blocks)
                        .Select(i => i.Name)
                        .ToList();
                }
                catch (Exception exception)
                    when (exception is IOException
                        || exception is UnauthorizedAccessException
                        || exception is ArgumentException
                        || exception is NotSupportedException
                        || exception is FormatException
                    )
                {
                    GeometryHelperLog.Warn(
                        "MainViewModel: the blocks of " + file + " were not read for a tool palette.",
                        exception
                    );
                    SayFromDesignCenter("Cannot read " + file + ": " + exception.Message);
                    return null;
                }
            }

            if (blocks.Count == 0)
            {
                SayFromDesignCenter(Path.GetFileName(file) + " has no block to make a tool palette of.");
                return null;
            }

            CadToolPalette palette = ToolPalettes.AddBlocks(
                name ?? Path.GetFileNameWithoutExtension(file),
                file,
                blocks
            );
            SaveToolPalettes();
            ToolPalettesChanged?.Invoke(this, EventArgs.Empty);
            SayFromDesignCenter(
                "Tool palette "
                    + palette.Name
                    + " made of "
                    + blocks.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + (blocks.Count == 1 ? " block." : " blocks.")
            );
            return palette;
        }

        /// <summary>A request of TOOLPALETTES or TOOLPALETTESCLOSE: the palettes shown where the workspace shows them, or closed.</summary>
        private void OnToolPalettesRequested(CadUiRequestEventArgs e)
        {
            if (e.Name == "TOOLPALETTES" && ShowToolPalettes != null && _current != null)
            {
                ShowToolPalettes(new ToolPalettesViewModel(this));
                e.Handled = true;
            }
            else if (e.Name == "TOOLPALETTESCLOSE" && CloseToolPalettes != null)
            {
                CloseToolPalettes();
                e.Handled = true;
            }
        }
    }
}

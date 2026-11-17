using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The tool palettes of the window: the palettes in the order of their tabs, the one shown, and the tools cut or copied that
    /// Paste puts back. The set is kept in one JSON file beside the settings (<see cref="DefaultPath"/>, written by
    /// <see cref="Save"/>, read by <see cref="Load"/>); one palette, or all of them, can be written to a file of their own
    /// (<see cref="Export"/>) and read from one (<see cref="Import"/>) in the same format, which the guide of the presenter
    /// describes member by member. A set with no file starts as <see cref="CreateStarter"/> makes it.
    /// </summary>
    public sealed partial class CadToolPalettes
    {
        private readonly List<CadToolPalette> _palettes = new List<CadToolPalette>();
        private readonly List<CadTool> _clipboard = new List<CadTool>();
        private int _activeIndex;

        /// <summary>The name of the palette a new one is given where it is not named.</summary>
        public const string NewPaletteName = "New Palette";

        /// <summary>Gets the file the palettes are kept in: %AppData%\GeometryHelper\Drawing\ToolPalettes.json, beside the settings.</summary>
        public static string DefaultPath =>
            Path.Combine(Path.GetDirectoryName(GlobalSettings.DefaultPath), "ToolPalettes.json");

        /// <summary>Gets the palettes, in the order of their tabs.</summary>
        public IReadOnlyList<CadToolPalette> Palettes => _palettes;

        /// <summary>Gets or sets the place in <see cref="Palettes"/> of the palette shown; kept inside the list, 0 with none.</summary>
        public int ActiveIndex
        {
            get => _palettes.Count == 0 ? 0 : Math.Max(0, Math.Min(_activeIndex, _palettes.Count - 1));
            set => _activeIndex = value;
        }

        /// <summary>Gets the palette shown; null where the set has none.</summary>
        public CadToolPalette Active => _palettes.Count == 0 ? null : _palettes[ActiveIndex];

        /// <summary>Gets the tools cut or copied, which <see cref="Paste"/> puts into a palette.</summary>
        public IReadOnlyList<CadTool> Clipboard => _clipboard;

        /// <summary>Gets whether the set changed since it was made, read or last saved.</summary>
        public bool IsModified { get; private set; }

        /// <summary>Tells the set it changed, as the window does after it changes a tool or a palette in place.</summary>
        public void Touch() => IsModified = true;

        /// <summary>
        /// Gives a palette name that no palette of the set has, letters of either case the same: the name wanted, else with
        /// <c> (2)</c>, <c> (3)</c> and on after it.
        /// </summary>
        /// <param name="wanted">The name wanted; blank is <see cref="NewPaletteName"/>.</param>
        /// <param name="except">A palette whose own name is not counted, so that renaming it to its own name keeps it.</param>
        /// <returns>The name.</returns>
        public string UniqueName(string wanted, CadToolPalette except = null)
        {
            string name = string.IsNullOrWhiteSpace(wanted) ? NewPaletteName : wanted.Trim();
            string tried = name;

            for (int n = 2; _palettes.Any(p => !ReferenceEquals(p, except) && Same(p.Name, tried)); n++)
            {
                tried = name + " (" + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
            }

            return tried;
        }

        /// <summary>Makes an empty palette after the last, its name made unique, and shows it.</summary>
        /// <param name="name">The name wanted; blank is <see cref="NewPaletteName"/>.</param>
        /// <returns>The palette.</returns>
        public CadToolPalette Add(string name) => Add(new CadToolPalette(UniqueName(name)));

        /// <summary>Puts a palette after the last, its name made unique, and shows it.</summary>
        /// <param name="palette">The palette.</param>
        /// <returns>The palette.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the palette is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the set has the palette already.</exception>
        public CadToolPalette Add(CadToolPalette palette)
        {
            if (palette == null)
            {
                throw new ArgumentNullException(nameof(palette));
            }

            if (_palettes.Contains(palette))
            {
                throw new ArgumentException("The set has the palette already.", nameof(palette));
            }

            palette.Name = UniqueName(palette.Name);
            _palettes.Add(palette);
            _activeIndex = _palettes.Count - 1;
            IsModified = true;
            return palette;
        }

        /// <summary>Takes a palette out; the palette shown stays the one shown where it is another, else the one next to it is.</summary>
        /// <param name="palette">The palette.</param>
        /// <returns>Whether the set had it.</returns>
        public bool Remove(CadToolPalette palette)
        {
            int at = palette == null ? -1 : _palettes.IndexOf(palette);

            if (at < 0)
            {
                return false;
            }

            int shown = ActiveIndex;
            _palettes.RemoveAt(at);
            _activeIndex = at < shown ? shown - 1 : shown;
            IsModified = true;
            return true;
        }

        /// <summary>Renames a palette; a name another palette has gets <c> (2)</c> and on after it.</summary>
        /// <param name="palette">The palette.</param>
        /// <param name="name">The name wanted.</param>
        /// <returns>Whether the palette is of the set and the name was not blank.</returns>
        public bool Rename(CadToolPalette palette, string name)
        {
            if (palette == null || !_palettes.Contains(palette) || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            palette.Name = UniqueName(name, palette);
            IsModified = true;
            return true;
        }

        /// <summary>Moves a palette to another place among the tabs; the palette shown stays the one shown.</summary>
        /// <param name="palette">The palette.</param>
        /// <param name="index">The place it has afterwards, kept inside the list.</param>
        /// <returns>Whether the set had the palette.</returns>
        public bool Move(CadToolPalette palette, int index)
        {
            int at = palette == null ? -1 : _palettes.IndexOf(palette);

            if (at < 0)
            {
                return false;
            }

            CadToolPalette shown = Active;
            _palettes.RemoveAt(at);
            _palettes.Insert(Math.Max(0, Math.Min(index, _palettes.Count)), palette);
            _activeIndex = _palettes.IndexOf(shown);
            IsModified = true;
            return true;
        }

        /// <summary>Puts copies of tools on the clipboard, as Copy of a tool's menu does.</summary>
        /// <param name="tools">The tools.</param>
        /// <exception cref="ArgumentNullException">Thrown when the tools are null.</exception>
        public void Copy(IEnumerable<CadTool> tools)
        {
            if (tools == null)
            {
                throw new ArgumentNullException(nameof(tools));
            }

            _clipboard.Clear();
            _clipboard.AddRange(tools.Select(t => t.Clone()));
        }

        /// <summary>Puts the tools on the clipboard and takes them out of the palette, as Cut does.</summary>
        /// <param name="palette">The palette they are in.</param>
        /// <param name="tools">The tools.</param>
        /// <exception cref="ArgumentNullException">Thrown when the palette or the tools are null.</exception>
        public void Cut(CadToolPalette palette, IEnumerable<CadTool> tools)
        {
            if (palette == null)
            {
                throw new ArgumentNullException(nameof(palette));
            }

            if (tools == null)
            {
                throw new ArgumentNullException(nameof(tools));
            }

            List<CadTool> cut = tools.Where(t => palette.Tools.Contains(t)).ToList();
            Copy(cut);
            cut.ForEach(t => palette.Remove(t));
            IsModified = true;
        }

        /// <summary>Puts copies of the tools on the clipboard into a palette, at a place of its list; the clipboard stays full, so they can be pasted again.</summary>
        /// <param name="palette">The palette.</param>
        /// <param name="index">The place of the first, as <see cref="CadToolPalette.Insert"/> counts it; past the end puts them last.</param>
        /// <returns>The tools made, in order; none where the clipboard is empty.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the palette is null.</exception>
        public IReadOnlyList<CadTool> Paste(CadToolPalette palette, int index)
        {
            if (palette == null)
            {
                throw new ArgumentNullException(nameof(palette));
            }

            var made = new List<CadTool>();

            foreach (CadTool tool in _clipboard)
            {
                CadTool copy = tool.Clone();
                palette.Insert(index + made.Count, copy);
                made.Add(copy);
            }

            IsModified |= made.Count > 0;
            return made;
        }

        /// <summary>
        /// Makes a palette of a drawing's blocks: one palette named for the drawing, a block tool for each name, taking the
        /// drawing as its source, as DesignCenter's Create Tool Palette does.
        /// </summary>
        /// <param name="name">The palette's name, made unique.</param>
        /// <param name="file">The drawing's file.</param>
        /// <param name="blockNames">The blocks' names, in the order the tools are listed.</param>
        /// <returns>The palette, shown.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name, the file or the names are null.</exception>
        public CadToolPalette AddBlocks(string name, string file, IEnumerable<string> blockNames)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            if (blockNames == null)
            {
                throw new ArgumentNullException(nameof(blockNames));
            }

            CadToolPalette palette = Add(name ?? throw new ArgumentNullException(nameof(name)));

            foreach (string block in blockNames)
            {
                palette.Add(CadTool.ForBlock(block, file, block));
            }

            return palette;
        }

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Makes the set a window starts with where it has no file: <b>Draw</b> and <b>Modify</b> of command tools (LINE, PLINE,
        /// CIRCLE ... MOVE, COPY, TRIM ...), <b>Annotation</b> (text, dimensions, leaders, tables), <b>Hatches</b> (the ANSI
        /// patterns the presenter has, solid and gradient fills and some materials), <b>Architectural Hatches</b> (the AR-
        /// patterns, drawn at their size in millimetres) and <b>ISO Hatches</b> (ISO02W100 to ISO15W100). All are
        /// the presenter's own: its patterns and its icons.
        /// </summary>
        /// <returns>The set, unmodified.</returns>
        public static CadToolPalettes CreateStarter()
        {
            var set = new CadToolPalettes();
            CadToolPalette draw = set.Add("Draw");
            AddCommands(
                draw,
                new[]
                {
                    "Line|LINE|Line|Draws straight line segments",
                    "Polyline|PLINE|Polyline|Draws a two-dimensional polyline of lines and arcs",
                    "Circle|CIRCLE|Circle|Draws a circle",
                    "Arc|ARC|Arc|Draws an arc",
                    "Rectangle|RECTANG|Rectangle|Draws a rectangular polyline",
                    "Ellipse|ELLIPSE|Ellipse|Draws an ellipse or an elliptical arc",
                    "Spline|SPLINE|Spline|Draws a smooth curve through points",
                    "Point|POINT|Point|Draws a point",
                }
            );
            CadToolPalette modify = set.Add("Modify");
            AddCommands(
                modify,
                new[]
                {
                    "Move|MOVE|Move|Moves objects a distance in a direction",
                    "Copy|COPY|Copy|Copies objects a distance in a direction",
                    "Rotate|ROTATE|Rotate|Rotates objects about a base point",
                    "Scale|SCALE|Scale|Enlarges or reduces objects",
                    "Mirror|MIRROR|Mirror|Makes a mirror image of objects",
                    "Offset|OFFSET|Offset|Draws parallel lines, circles and curves",
                    "Trim|TRIM|Trim|Trims objects at the edge of others",
                    "Extend|EXTEND|Extend|Extends objects to the edge of others",
                    "Fillet|FILLET|Fillet|Rounds the corner of two objects",
                    "Chamfer|CHAMFER|Chamfer|Bevels the corner of two objects",
                    "Stretch|STRETCH|Stretch|Stretches objects crossed by a window",
                    "Explode|EXPLODE|Explode|Breaks a compound object into its parts",
                    "Erase|ERASE|Erase|Removes objects from the drawing",
                }
            );
            CadToolPalette annotation = set.Add("Annotation");
            annotation.Add(CadTool.ForText("Text"));
            AddCommands(
                annotation,
                new[]
                {
                    "Single-line Text|TEXT|Text|Draws a line of text",
                    "Multiline Text|MTEXT|MText|Draws a paragraph of text in a box",
                    "Table|TABLE|Table|Draws an empty table",
                }
            );
            annotation.Add(CadTool.ForSeparator());
            annotation.Add(CadTool.ForText("Dimensions"));
            AddCommands(
                annotation,
                new[]
                {
                    "Linear|DIMLINEAR|Dimension|Dimensions the horizontal or vertical distance between two points",
                    "Aligned|DIMALIGNED|Aligned|Dimensions the distance between two points along their line",
                    "Angular|DIMANGULAR|Angular|Dimensions the angle between lines",
                    "Radius|DIMRADIUS|Radius|Dimensions the radius of a circle or an arc",
                    "Diameter|DIMDIAMETER|Diameter|Dimensions the diameter of a circle or an arc",
                    "Baseline|DIMBASELINE|DimBaseline|Dimensions from the first extension line of the last",
                    "Continue|DIMCONTINUE|DimContinue|Dimensions from the second extension line of the last",
                    "Multileader|MLEADER|Leader|Draws a leader with a note",
                }
            );
            CadToolPalette hatches = set.Add("Hatches");
            hatches.Add(CadTool.ForText("ANSI"));
            AddHatches(
                hatches,
                new[] { "ANSI31", "ANSI32", "ANSI33", "ANSI34", "ANSI35", "ANSI36", "ANSI37", "ANSI38" }
            );
            hatches.Add(CadTool.ForSeparator());
            hatches.Add(CadTool.ForText("Fills"));
            hatches.Add(CadTool.ForHatch("Solid", "SOLID"));
            CadTool gradient = CadTool.ForHatch("Linear gradient", "SOLID");
            gradient.HatchType = CadHatchType.Gradient;
            gradient.Gradient = "GR_LINEAR";
            gradient.Description = "A gradient of two colours along a line";
            hatches.Add(gradient);
            hatches.Add(CadTool.ForSeparator());
            hatches.Add(CadTool.ForText("Materials"));
            AddHatches(
                hatches,
                new[] { "BRICK", "EARTH", "GRASS", "INSUL", "STEEL", "HEX", "DOTS", "CROSS", "NET", "HONEY" }
            );
            CadToolPalette architectural = set.Add("Architectural Hatches");
            AddHatches(
                architectural,
                new[] { "AR-B816", "AR-BRSTD", "AR-CONC", "AR-HBONE", "AR-PARQ1", "AR-RROOF", "AR-RSHKE", "AR-SAND" }
            );
            CadToolPalette iso = set.Add("ISO Hatches");
            AddHatches(
                iso,
                Enumerable
                    .Range(2, 14)
                    .Select(n => "ISO" + n.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + "W100")
            );
            set._activeIndex = 0;
            set.IsModified = false;
            return set;
        }

        /// <summary>Adds command tools written as "name|command|icon|description".</summary>
        private static void AddCommands(CadToolPalette palette, IEnumerable<string> rows)
        {
            foreach (string row in rows)
            {
                string[] part = row.Split('|');
                CadTool tool = CadTool.ForCommand(part[0], part[1], part[2]);
                tool.Description = part[3];
                palette.Add(tool);
            }
        }

        /// <summary>Adds a hatch tool for each of the presenter's patterns named, the pattern's description as the tooltip.</summary>
        private static void AddHatches(CadToolPalette palette, IEnumerable<string> patterns)
        {
            foreach (string name in patterns)
            {
                CadTool tool = CadTool.ForHatch(name, name);
                tool.Description = CadPatternFile.FindBuiltIn(name)?.Description ?? string.Empty;
                palette.Add(tool);
            }
        }
    }
}

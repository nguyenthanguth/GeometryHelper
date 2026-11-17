using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The JSON of the tool palettes. A file is an object with <c>format</c> (<c>GeometryHelper.ToolPalettes</c>),
    /// <c>version</c> (1), <c>active</c> (the place of the palette shown, from 0) and <c>palettes</c>, a list of objects with
    /// <c>name</c>, <c>view</c> (<c>icons</c>, <c>iconsAndText</c> or <c>list</c>), <c>iconSize</c> (16 to 96) and <c>tools</c>.
    /// A tool has <c>kind</c> (<c>block</c>, <c>hatch</c>, <c>command</c>, <c>text</c> or <c>separator</c>), <c>name</c> and
    /// <c>description</c>, and by kind: command <c>command</c> and <c>icon</c>; block <c>file</c>, <c>block</c>, <c>scale</c>,
    /// <c>angle</c> (degrees), <c>promptForScale</c>, <c>promptForRotation</c> and <c>explode</c>; hatch <c>hatchType</c>
    /// (<c>pattern</c>, <c>userDefined</c>, <c>solid</c> or <c>gradient</c>), <c>pattern</c>, <c>angle</c>, <c>scale</c>,
    /// <c>spacing</c>, <c>gradient</c> and <c>associative</c>; and for the three that draw, <c>layer</c>, <c>color</c>,
    /// <c>linetype</c> and <c>lineWeight</c> where set. A member missing keeps its default; a tool of another kind is skipped.
    /// </summary>
    public sealed partial class CadToolPalettes
    {
        /// <summary>The <c>format</c> a file of tool palettes has.</summary>
        public const string Format = "GeometryHelper.ToolPalettes";

        /// <summary>The <c>version</c> this writes; a file of a later version is read as far as it is understood.</summary>
        public const int Version = 1;

        /// <summary>Gives the set as the JSON text <see cref="Save"/> writes: two spaces of indent and LF line ends.</summary>
        /// <returns>The text.</returns>
        public string ToJson() => Write(_palettes, ActiveIndex);

        /// <summary>
        /// Reads a set from JSON text, as <see cref="ToJson"/> wrote it; a value that is not valid falls back to its default
        /// with a Warn naming it.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="set">Receives the set, unmodified; null where the text does not read.</param>
        /// <param name="problem">Receives what is wrong where it does not read; null where it does.</param>
        /// <returns>Whether the text read.</returns>
        public static bool TryParse(string text, out CadToolPalettes set, out string problem)
        {
            set = null;
            problem = null;
            XElement root;

            try
            {
                using (
                    XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(
                        Encoding.UTF8.GetBytes(text ?? string.Empty),
                        XmlDictionaryReaderQuotas.Max
                    )
                )
                {
                    root = XElement.Load(reader);
                }
            }
            catch (Exception exception) when (exception is XmlException || exception is ArgumentException)
            {
                problem = "It is not JSON: " + exception.Message;
                return false;
            }

            if (!string.Equals(Text(root, "format"), Format, StringComparison.Ordinal))
            {
                problem = "It is not a file of tool palettes: its format is not " + Format + ".";
                return false;
            }

            var read = new CadToolPalettes();

            foreach (XElement item in root.Element("palettes")?.Elements() ?? Enumerable.Empty<XElement>())
            {
                CadToolPalette palette = ReadPalette(item);

                if (palette != null)
                {
                    read.Add(palette);
                }
            }

            double? active = Number(root, "active");
            read._activeIndex = active.HasValue ? (int)Math.Max(0.0, Math.Min(active.Value, 1.0E6)) : 0;
            read.IsModified = false;
            set = read;
            return true;
        }

        /// <summary>
        /// Reads the set from its file, never throwing: no file gives <see cref="CreateStarter"/>; a file that does not read
        /// gives it too, said in the log once.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <returns>The set.</returns>
        public static CadToolPalettes Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return CreateStarter();
            }

            try
            {
                if (TryParse(File.ReadAllText(path, Encoding.UTF8), out CadToolPalettes set, out string problem))
                {
                    return set;
                }

                GeometryHelperLog.Warn(
                    "CadToolPalettes: " + path + " does not read; the starter palettes are used: " + problem
                );
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                GeometryHelperLog.Warn(
                    "CadToolPalettes: " + path + " does not read; the starter palettes are used.",
                    exception
                );
            }

            return CreateStarter();
        }

        /// <summary>Writes the set to a JSON file, the folder made where it is missing, as <see cref="ToJson"/> gives it; the set is no longer modified.</summary>
        /// <param name="path">The file.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the file may not be written.</exception>
        public void Save(string path)
        {
            Write(path, ToJson());
            IsModified = false;
        }

        /// <summary>Writes palettes to a file of their own, in the format of the set, for <see cref="Import"/> to read on another machine.</summary>
        /// <param name="path">The file.</param>
        /// <param name="palettes">The palettes.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path or the palettes are null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the file may not be written.</exception>
        public static void Export(string path, IEnumerable<CadToolPalette> palettes)
        {
            if (palettes == null)
            {
                throw new ArgumentNullException(nameof(palettes));
            }

            Write(path, Write(palettes.ToList(), 0));
        }

        /// <summary>Reads a file <see cref="Export"/> (or <see cref="Save"/>) wrote and adds its palettes after the last, their names made unique.</summary>
        /// <param name="path">The file.</param>
        /// <returns>The palettes added, in order; the first is shown.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the file may not be read.</exception>
        /// <exception cref="FormatException">Thrown when the file is not a file of tool palettes.</exception>
        public IReadOnlyList<CadToolPalette> Import(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (!TryParse(File.ReadAllText(path, Encoding.UTF8), out CadToolPalettes read, out string problem))
            {
                throw new FormatException(Path.GetFileName(path) + " is not read: " + problem);
            }

            List<CadToolPalette> added = read._palettes.ToList();
            int first = _palettes.Count;
            added.ForEach(p => Add(p));
            _activeIndex = added.Count > 0 ? first : _activeIndex;
            return added;
        }

        private static void Write(string path, string text)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        private static string Write(IReadOnlyList<CadToolPalette> palettes, int active)
        {
            var w = new JsonText();
            w.Open(null);
            w.Value("format", Format);
            w.Value("version", Version);
            w.Value("active", active);
            w.OpenList("palettes");

            foreach (CadToolPalette palette in palettes)
            {
                w.Open(null);
                w.Value("name", palette.Name);
                w.Value(
                    "view",
                    palette.ViewStyle == CadToolViewStyle.List ? "list"
                        : palette.ViewStyle == CadToolViewStyle.IconWithText ? "iconsAndText"
                        : "icons"
                );
                w.Value("iconSize", palette.IconSize);
                w.OpenList("tools");

                foreach (CadTool tool in palette.Tools)
                {
                    WriteTool(w, tool);
                }

                w.CloseList();
                w.Close();
            }

            w.CloseList();
            w.Close();
            return w.ToString();
        }

        private static void WriteTool(JsonText w, CadTool tool)
        {
            w.Open(null);
            w.Value(
                "kind",
                tool.Kind.ToString().Substring(0, 1).ToLowerInvariant() + tool.Kind.ToString().Substring(1)
            );
            w.Value("name", tool.Name);

            if (tool.Description.Length > 0)
            {
                w.Value("description", tool.Description);
            }

            switch (tool.Kind)
            {
                case CadToolKind.Command:
                    w.Value("command", tool.Command);

                    if (tool.Icon != null)
                    {
                        w.Value("icon", tool.Icon);
                    }

                    break;
                case CadToolKind.Block:
                    if (tool.File != null)
                    {
                        w.Value("file", tool.File);
                    }

                    w.Value("block", tool.BlockName);
                    w.Value("scale", tool.Scale);
                    w.Value("angle", tool.Angle);
                    w.Value("promptForScale", tool.PromptForScale);
                    w.Value("promptForRotation", tool.PromptForRotation);
                    w.Value("explode", tool.Explode);
                    break;
                case CadToolKind.Hatch:
                    w.Value(
                        "hatchType",
                        tool.HatchType == CadHatchType.Solid ? "solid"
                            : tool.HatchType == CadHatchType.Gradient ? "gradient"
                            : tool.HatchType == CadHatchType.UserDefined ? "userDefined"
                            : "pattern"
                    );
                    w.Value("pattern", tool.Pattern);
                    w.Value("angle", tool.Angle);
                    w.Value("scale", tool.Scale);
                    w.Value("spacing", tool.Spacing);
                    w.Value("gradient", tool.Gradient);
                    w.Value("associative", tool.Associative);
                    break;
            }

            if (tool.Kind == CadToolKind.Command || tool.Kind == CadToolKind.Block || tool.Kind == CadToolKind.Hatch)
            {
                if (tool.Layer != null)
                {
                    w.Value("layer", tool.Layer);
                }

                if (tool.Color != null)
                {
                    w.Value("color", tool.Color);
                }

                if (tool.Linetype != null)
                {
                    w.Value("linetype", tool.Linetype);
                }

                if (tool.LineWeight.HasValue)
                {
                    w.Value("lineWeight", tool.LineWeight.Value);
                }
            }

            w.Close();
        }

        private static CadToolPalette ReadPalette(XElement item)
        {
            string name = Text(item, "name");

            if (string.IsNullOrWhiteSpace(name))
            {
                GeometryHelperLog.Warn("CadToolPalettes: a palette with no name is left out.");
                return null;
            }

            var palette = new CadToolPalette(name);
            string view = Text(item, "view");
            palette.ViewStyle =
                view == "list" ? CadToolViewStyle.List
                : view == "iconsAndText" ? CadToolViewStyle.IconWithText
                : CadToolViewStyle.IconOnly;
            double? size = Number(item, "iconSize");

            if (size.HasValue && size.Value >= CadToolPalette.MinIconSize && size.Value <= CadToolPalette.MaxIconSize)
            {
                palette.IconSize = (int)Math.Round(size.Value);
            }
            else if (size.HasValue)
            {
                GeometryHelperLog.Warn("CadToolPalettes: the icon size of " + name + " is not valid; 32 is used.");
            }

            foreach (XElement element in item.Element("tools")?.Elements() ?? Enumerable.Empty<XElement>())
            {
                CadTool tool = ReadTool(element, name);

                if (tool != null)
                {
                    palette.Add(tool);
                }
            }

            return palette;
        }

        private static CadTool ReadTool(XElement e, string palette)
        {
            string kind = Text(e, "kind");
            string name = Text(e, "name") ?? string.Empty;
            CadTool tool;

            switch (kind)
            {
                case "command":
                    tool = CadTool.ForCommand(name, Text(e, "command") ?? string.Empty, Text(e, "icon"));
                    break;
                case "block":
                    tool = CadTool.ForBlock(name, Text(e, "file"), Text(e, "block"));
                    Set(e, "scale", palette, v => tool.Scale = v);
                    Set(e, "angle", palette, v => tool.Angle = v);
                    tool.PromptForScale = Bool(e, "promptForScale") ?? false;
                    tool.PromptForRotation = Bool(e, "promptForRotation") ?? false;
                    tool.Explode = Bool(e, "explode") ?? false;
                    break;
                case "hatch":
                    tool = CadTool.ForHatch(name, Text(e, "pattern") ?? "ANSI31");
                    string type = Text(e, "hatchType");
                    tool.HatchType =
                        type == "solid" ? CadHatchType.Solid
                        : type == "gradient" ? CadHatchType.Gradient
                        : type == "userDefined" ? CadHatchType.UserDefined
                        : CadHatchType.Pattern;
                    Set(e, "angle", palette, v => tool.Angle = v);
                    Set(e, "scale", palette, v => tool.Scale = v);
                    Set(e, "spacing", palette, v => tool.Spacing = v);
                    tool.Gradient = Text(e, "gradient");
                    tool.Associative = Bool(e, "associative") ?? true;
                    break;
                case "text":
                    tool = CadTool.ForText(name);
                    break;
                case "separator":
                    tool = CadTool.ForSeparator();
                    break;
                default:
                    GeometryHelperLog.Warn(
                        "CadToolPalettes: a tool of the kind " + (kind ?? "(none)") + " in " + palette + " is left out."
                    );
                    return null;
            }

            tool.Description = Text(e, "description");

            if (tool.Kind == CadToolKind.Command || tool.Kind == CadToolKind.Block || tool.Kind == CadToolKind.Hatch)
            {
                tool.Layer = Text(e, "layer");
                tool.Color = Text(e, "color");
                tool.Linetype = Text(e, "linetype");
                double? weight = Number(e, "lineWeight");
                tool.LineWeight = weight.HasValue ? (int)Math.Round(weight.Value) : (int?)null;
            }

            return tool;
        }

        /// <summary>Gives a number member to a setter; a value the setter refuses leaves the default, said in the log.</summary>
        private static void Set(XElement e, string member, string palette, Action<double> set)
        {
            double? value = Number(e, member);

            if (!value.HasValue)
            {
                return;
            }

            try
            {
                set(value.Value);
            }
            catch (ArgumentOutOfRangeException)
            {
                GeometryHelperLog.Warn(
                    "CadToolPalettes: "
                        + member
                        + " "
                        + value.Value.ToString(CultureInfo.InvariantCulture)
                        + " in "
                        + palette
                        + " is not valid; the default is used."
                );
            }
        }

        private static string Text(XElement parent, string name)
        {
            XElement e = parent?.Element(name);
            return e == null || (string)e.Attribute("type") == "null" ? null : e.Value;
        }

        private static double? Number(XElement parent, string name)
        {
            string text = Text(parent, name);
            return
                text != null
                && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                && !double.IsNaN(value)
                && !double.IsInfinity(value)
                ? value
                : (double?)null;
        }

        private static bool? Bool(XElement parent, string name)
        {
            string text = Text(parent, name);
            return text == "true" ? true
                : text == "false" ? false
                : (bool?)null;
        }

        /// <summary>Writes JSON by hand, its members in the order written, two spaces of indent and LF line ends.</summary>
        private sealed class JsonText
        {
            private readonly StringBuilder _text = new StringBuilder();
            private readonly Stack<bool> _first = new Stack<bool>();

            public void Open(string name) => Begin(name, "{");

            public void OpenList(string name) => Begin(name, "[");

            public void Close() => End("}");

            public void CloseList() => End("]");

            public void Value(string name, string value) => Member(name, Quote(value));

            public void Value(string name, double value) =>
                Member(name, value.ToString("R", CultureInfo.InvariantCulture));

            public void Value(string name, int value) => Member(name, value.ToString(CultureInfo.InvariantCulture));

            public void Value(string name, bool value) => Member(name, value ? "true" : "false");

            public override string ToString() => _text.ToString() + "\n";

            private void Begin(string name, string bracket)
            {
                Member(name, bracket);
                _first.Push(true);
            }

            private void End(string bracket)
            {
                bool empty = _first.Pop();
                _text.Append(empty ? string.Empty : "\n" + new string(' ', 2 * _first.Count)).Append(bracket);
            }

            private void Member(string name, string value)
            {
                if (_first.Count > 0)
                {
                    _text.Append(_first.Peek() ? "\n" : ",\n").Append(' ', 2 * _first.Count);
                    _first.Pop();
                    _first.Push(false);
                }

                if (name != null)
                {
                    _text.Append(Quote(name)).Append(": ");
                }

                _text.Append(value);
            }

            private static string Quote(string text)
            {
                var quoted = new StringBuilder("\"");

                foreach (char c in text ?? string.Empty)
                {
                    switch (c)
                    {
                        case '"':
                            quoted.Append("\\\"");
                            break;
                        case '\\':
                            quoted.Append("\\\\");
                            break;
                        default:
                            if (c < ' ')
                            {
                                quoted.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                quoted.Append(c);
                            }

                            break;
                    }
                }

                return quoted.Append('"').ToString();
            }
        }
    }
}

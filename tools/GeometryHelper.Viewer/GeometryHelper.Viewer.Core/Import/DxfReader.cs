using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Reads a DXF with the library's <see cref="Export.DxfReader"/>, which gives every entity it reads as the library's shape, and makes the scene of
    /// them: a shape lying on the floor as the shape of the plane it is (<see cref="DxfReadOptions.PlanAs2D"/>), each INSERT as the entities of its
    /// block placed where it puts them, on their layers, in their colours, an entity placed through blocks named by them. The 3DFACEs of a layer are
    /// welded into meshes, one for each body. The library leaves out TEXT and MTEXT, and gives neither the colours of the layer table nor which of its
    /// layers are hidden; a pass of its own over the file's groups reads those, and no other entity. What the library leaves out is counted and said.
    /// </summary>
    internal sealed class DxfReader
    {
        private static readonly Regex Escape = new Regex(@"\\U\+([0-9A-Fa-f]{4})", RegexOptions.CultureInvariant);

        private readonly ImportBuilder _b;
        private readonly CancellationToken _cancel;
        private readonly Dictionary<string, LayerInfo> _layers = new Dictionary<string, LayerInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Label> _labels = new List<Label>();
        private readonly List<FaceSet> _faceSets = new List<FaceSet>();
        private readonly Dictionary<string, FaceSet> _faceSetIndex = new Dictionary<string, FaceSet>(StringComparer.Ordinal);

        private DxfReader(ImportBuilder builder, CancellationToken cancel)
        {
            _b = builder;
            _cancel = cancel;
        }

        private sealed class LayerInfo
        {
            internal string Color;
            internal bool Visible = true;
        }

        /// <summary>
        /// A record of the layer table or a label of the drawing: the groups after its 0.
        /// </summary>
        private sealed class Record
        {
            internal string Type;
            internal readonly List<KeyValuePair<int, string>> Groups = new List<KeyValuePair<int, string>>();

            internal string Text(int code)
            {
                foreach (KeyValuePair<int, string> g in Groups)
                {
                    if (g.Key == code)
                    {
                        return g.Value;
                    }
                }

                return null;
            }

            /// <summary>
            /// The real of the first group of a code; false where there is none, or it is no number.
            /// </summary>
            internal bool TryReal(int code, out double value)
            {
                value = 0.0;
                string text = Text(code);
                return text != null && ImportNumber.TryParse(text.Trim(), out value);
            }

            internal bool TryInt(int code, out int value)
            {
                value = 0;
                string text = Text(code);
                return text != null && int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }

            internal int Int(int code, int fallback) => TryInt(code, out int v) ? v : fallback;
        }

        /// <summary>
        /// A TEXT or an MTEXT of model space, as the scene draws it.
        /// </summary>
        private sealed class Label
        {
            internal string Type;
            internal string Layer;
            internal string Color;
            internal string Handle;
            internal Vec3 At;
            internal string Text;
            internal double Height;
        }

        /// <summary>
        /// The 3DFACEs of one layer in one colour, to be welded into meshes once they are all in.
        /// </summary>
        private sealed class FaceSet
        {
            internal string Layer;
            internal string Color;
            internal readonly MeshWelder Weld = new MeshWelder();
        }

        internal static Scene Read(string text, string title, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            var b = new ImportBuilder(title);
            DxfDocument document = Export.DxfReader.Parse(text, new DxfReadOptions { PlanAs2D = true, ExpandBlocks = true, KeepGroups = false, Tolerance = b.Scene.Tolerance });
            var reader = new DxfReader(b, cancel);
            reader.ReadTableAndLabels(text, document);
            reader.Take(document);
            return b.Finish();
        }

        private void Take(DxfDocument document)
        {
            for (int i = 0; i < document.Entities.Count; i++)
            {
                if ((i & 1023) == 0)
                {
                    _cancel.ThrowIfCancellationRequested();
                }

                DxfEntity e = document.Entities[i];

                if (e.Type == "3DFACE")
                {
                    AddFace(e);
                    continue;
                }

                string kind = GeometryEncoder.KindOf(e.Shape);

                if (kind == null)
                {
                    _b.Warn("DXF: a " + e.Type + " the reader gave as a " + e.Shape.GetType().Name + ", which the viewer does not draw, was left out");
                    continue;
                }

                Emit(kind, GeometryEncoder.Encode(e.Shape), e.Type, e.Layer, ItemColour(e), e.Handle, e.BlockPath.Count > 0 ? string.Join(" > ", e.BlockPath) : null);
            }

            var drawn = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (Label label in _labels)
            {
                Emit(SceneKinds.Text, ImportData.Text(label.At, label.Text, label.Height), label.Type, label.Layer, label.Color, label.Handle, null);
                drawn.TryGetValue(label.Type, out int count);
                drawn[label.Type] = count + 1;
            }

            EmitFaceSets();

            foreach (string warning in document.Warnings)
            {
                _b.Warn("DXF: " + warning);
            }

            // Every type is counted, INSERT too: the library gives a warning of its own for some of what it leaves out, not for all.
            foreach (KeyValuePair<string, int> skipped in document.Skipped)
            {
                drawn.TryGetValue(skipped.Key, out int labels);
                int left = skipped.Value - labels;

                if (left > 0)
                {
                    _b.Warn("DXF: " + left.ToString(CultureInfo.InvariantCulture) + " " + skipped.Key + " entity(ies) are not read, as the library's DxfReader counts them; a warning above gives the reason where it has one");
                }
            }
        }

        #region The layer table and the labels

        /// <summary>
        /// Reads, from the pairs of lines of the file, a group code and its value, the two things the library's reader does not give: the layers of the
        /// layer table with their colours and whether they are shown, and the TEXT and MTEXT of model space. The library has read the file already, so
        /// its sections are whole; no other record is looked at.
        /// </summary>
        private void ReadTableAndLabels(string text, DxfDocument document)
        {
            var known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string layer in document.Layers)
            {
                known[layer] = layer;
            }

            // A byte order mark before the first group code is no part of it, as the library's Parse reads it.
            string[] lines = (text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text).Split('\n');
            Record current = null;
            string section = null;
            string currentSection = null;
            bool expectSection = false;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if ((i & 8191) == 0)
                {
                    _cancel.ThrowIfCancellationRequested();
                }

                if (!int.TryParse(lines[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
                {
                    break;
                }

                string value = lines[i + 1].TrimEnd('\r');

                if (code == 0)
                {
                    Done(current, currentSection, known);
                    current = null;
                    string word = value.Trim();

                    if (word == "EOF")
                    {
                        break;
                    }

                    if (word == "SECTION")
                    {
                        expectSection = true;
                    }
                    else if (word == "ENDSEC")
                    {
                        section = null;
                    }
                    else if ((section == "TABLES" && word == "LAYER") || (section == "ENTITIES" && (word == "TEXT" || word == "MTEXT")))
                    {
                        current = new Record { Type = word };
                        currentSection = section;
                    }
                }
                else if (expectSection)
                {
                    expectSection = false;
                    section = code == 2 ? value.Trim() : null;
                }
                else
                {
                    current?.Groups.Add(new KeyValuePair<int, string>(code, value));
                }
            }

            Done(current, currentSection, known);
        }

        private void Done(Record r, string section, Dictionary<string, string> known)
        {
            if (r == null)
            {
                return;
            }

            if (section == "TABLES")
            {
                ReadLayer(r);
            }
            else
            {
                ReadLabel(r, known);
            }
        }

        private static string DecodeName(string name)
            => Escape.Replace(name, m => ((char)int.Parse(m.Groups[1].Value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)).ToString());

        private void ReadLayer(Record r)
        {
            string name = DecodeName((r.Text(2) ?? string.Empty).Trim());

            if (name.Length == 0)
            {
                return;
            }

            int colour = r.Int(62, 7);
            bool frozen = (r.Int(70, 0) & 1) != 0;
            var info = new LayerInfo { Color = TrueColour(r) ?? AciColour(colour == int.MinValue ? 0 : Math.Abs(colour)), Visible = colour >= 0 && !frozen };

            if (!info.Visible)
            {
                _b.Warn("DXF: the layer '" + name + "' is " + (frozen ? "frozen" : "turned off") + " in the file and is hidden; tick it in the scene tree to see it");
            }

            _layers[name] = info;
        }

        /// <summary>
        /// Reads a TEXT or an MTEXT of model space at its insertion point as the file gives it, with its height; one with no insertion point that can be
        /// read, or a height that cannot, is left out, and counted with what the library left out.
        /// </summary>
        private void ReadLabel(Record r, Dictionary<string, string> known)
        {
            if (r.Int(67, 0) == 1 || !r.TryReal(10, out double x) || !r.TryReal(20, out double y))
            {
                return;
            }

            double z = 0.0;
            double height = 0.0;

            if ((r.Text(30) != null && !r.TryReal(30, out z)) || (r.Text(40) != null && !r.TryReal(40, out height)))
            {
                return;
            }

            string text;

            if (r.Type == "MTEXT")
            {
                // The text comes in pieces of 250 characters (3) and a last one (1), with formatting codes in it.
                text = string.Concat(r.Groups.Where(g => g.Key == 3).Select(g => g.Value)) + (r.Text(1) ?? string.Empty);
                text = PlainMText(text);
            }
            else
            {
                text = r.Text(1) ?? string.Empty;
            }

            string layer = DecodeName((r.Text(8) ?? "0").Trim());
            layer = layer.Length == 0 ? "0" : layer;

            _labels.Add(new Label
            {
                Type = r.Type,
                Layer = Spelled(known, layer),
                Color = TrueColour(r) ?? (r.TryInt(62, out int aci) ? AciColour(aci) : null),
                Handle = r.Text(5)?.Trim(),
                At = new Vec3(x, y, z),
                Text = DecodeName(text).Replace("%%d", "\u00B0").Replace("%%p", "\u00B1").Replace("%%c", "\u2300"),
                Height = height,
            });
        }

        /// <summary>
        /// The name a layer is known by: the first spelling met, the library's for a layer it read, two that differ only in case being one.
        /// </summary>
        private static string Spelled(Dictionary<string, string> known, string layer)
        {
            if (!known.TryGetValue(layer, out string spelled))
            {
                spelled = layer;
                known.Add(layer, layer);
            }

            return spelled;
        }

        /// <summary>
        /// The words of an MTEXT without its formatting: <c>\P</c> is a new line, the codes that end in a semicolon (font, height, colour, width, stacking,
        /// paragraph and the rest) go with their arguments, those that stand alone (<c>\L</c>, <c>\l</c>, <c>\O</c>, <c>\o</c>, <c>\K</c>, <c>\k</c>) go alone, <c>\~</c> is
        /// a space, <c>\\</c>, <c>\{</c> and <c>\}</c> are the character, a stack <c>\S1/2;</c> is read as 1/2, a <c>\U+</c> escape is left for the decoding that follows, and
        /// the braces that group are taken off. No word is eaten with a code.
        /// </summary>
        internal static string PlainMText(string text)
        {
            var plain = new System.Text.StringBuilder(text.Length);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '{' || c == '}')
                {
                    continue;
                }

                if (c != '\\' || i + 1 >= text.Length)
                {
                    plain.Append(c);
                    continue;
                }

                char code = text[++i];

                switch (code)
                {
                    case 'P':
                    case 'X':
                        plain.Append('\n');
                        break;

                    case '~':
                        plain.Append(' ');
                        break;

                    case '\\':
                    case '{':
                    case '}':
                        plain.Append(code);
                        break;

                    case 'L':
                    case 'l':
                    case 'O':
                    case 'o':
                    case 'K':
                    case 'k':
                        break;

                    case 'U':
                        // \U+00E9 is a character, decoded afterwards with the other escapes of a name.
                        plain.Append('\\').Append('U');
                        break;

                    case 'S':
                    {
                        int end = text.IndexOf(';', i);
                        string stack = end < 0 ? text.Substring(i + 1) : text.Substring(i + 1, end - i - 1);
                        plain.Append(stack.Replace('^', '/').Replace('#', '/'));
                        i = end < 0 ? text.Length : end;
                        break;
                    }

                    default:
                        if (char.IsLetter(code))
                        {
                            // A code with arguments: they run to the semicolon.
                            int end = text.IndexOf(';', i);
                            i = end < 0 ? text.Length : end;
                        }
                        else
                        {
                            plain.Append(code);
                        }

                        break;
                }
            }

            return plain.ToString();
        }

        #endregion

        #region Layers and colours

        /// <summary>
        /// The colour of a true-colour group (420), as #RRGGBB, or null.
        /// </summary>
        private static string TrueColour(Record r) => r.TryInt(420, out int rgb) ? Hex(rgb) : null;

        private static string Hex(int rgb)
            => "#" + ((rgb >> 16) & 255).ToString("X2", CultureInfo.InvariantCulture) + ((rgb >> 8) & 255).ToString("X2", CultureInfo.InvariantCulture) + (rgb & 255).ToString("X2", CultureInfo.InvariantCulture);

        /// <summary>
        /// The colour of an entity: its true colour, else that of its colour index, each its own or taken from the INSERT that places it.
        /// </summary>
        private static string ItemColour(DxfEntity e) => e.TrueColour.HasValue ? Hex(e.TrueColour.Value) : AciColour(e.Colour ?? 0);

        /// <summary>
        /// The colour of an AutoCAD colour index as #RRGGBB. Seven is white on a dark screen and black on a white page, a colour of nothing in
        /// particular, and 0 and 256 are by block and by layer: none of them is a colour, so null is given and the layer's cycle colour is kept.
        /// </summary>
        internal static string AciColour(int index)
        {
            if (index <= 0 || index == 7 || index > 255)
            {
                return null;
            }

            int r, g, b;

            if (index <= 9)
            {
                int[][] basic =
                {
                    new[] { 255, 0, 0 }, new[] { 255, 255, 0 }, new[] { 0, 255, 0 }, new[] { 0, 255, 255 }, new[] { 0, 0, 255 }, new[] { 255, 0, 255 },
                    new[] { 255, 255, 255 }, new[] { 65, 65, 65 }, new[] { 128, 128, 128 },
                };
                int[] c = basic[index - 1];
                r = c[0];
                g = c[1];
                b = c[2];
            }
            else if (index >= 250)
            {
                int[] greys = { 51, 91, 132, 173, 214, 255 };
                r = g = b = greys[index - 250];
            }
            else
            {
                // 24 hues of 15 degrees, each in ten shades: full and half saturation at five brightnesses.
                int n = index - 10;
                double hue = (n / 10) * 15.0;
                int shade = n % 10;
                double value = new[] { 1.0, 1.0, 0.8, 0.8, 0.6, 0.6, 0.5, 0.5, 0.3, 0.3 }[shade];
                double saturation = shade % 2 == 0 ? 1.0 : 0.5;
                Hsv(hue, saturation, value, out r, out g, out b);
            }

            return "#" + r.ToString("X2", CultureInfo.InvariantCulture) + g.ToString("X2", CultureInfo.InvariantCulture) + b.ToString("X2", CultureInfo.InvariantCulture);
        }

        private static void Hsv(double hue, double saturation, double value, out int r, out int g, out int b)
        {
            double c = value * saturation;
            double h = hue / 60.0;
            double x = c * (1.0 - Math.Abs(h % 2.0 - 1.0));
            double m = value - c;
            double rr, gg, bb;

            if (h < 1.0)
            {
                rr = c; gg = x; bb = 0.0;
            }
            else if (h < 2.0)
            {
                rr = x; gg = c; bb = 0.0;
            }
            else if (h < 3.0)
            {
                rr = 0.0; gg = c; bb = x;
            }
            else if (h < 4.0)
            {
                rr = 0.0; gg = x; bb = c;
            }
            else if (h < 5.0)
            {
                rr = x; gg = 0.0; bb = c;
            }
            else
            {
                rr = c; gg = 0.0; bb = x;
            }

            r = (int)Math.Floor((rr + m) * 255.0 + 1E-9);
            g = (int)Math.Floor((gg + m) * 255.0 + 1E-9);
            b = (int)Math.Floor((bb + m) * 255.0 + 1E-9);
        }

        /// <summary>
        /// Adds an item on a layer, making the layer with the colour and visibility the layer table gave it.
        /// </summary>
        private void Emit(string kind, System.Text.Json.JsonElement data, string type, string layer, string color, string handle, string name)
        {
            EnsureLayer(layer);
            SceneItem item = _b.Add(kind, data, layer, name, color, "DXF " + type);

            if (!string.IsNullOrWhiteSpace(handle))
            {
                item.Properties["handle"] = handle.Trim();
            }
        }

        private void EnsureLayer(string layer)
        {
            if (_b.Scene.FindLayer(layer) != null)
            {
                return;
            }

            if (_layers.TryGetValue(layer, out LayerInfo info))
            {
                _b.Layer(layer, info.Color, info.Visible);
            }
            else
            {
                _b.Layer(layer);
            }
        }

        #endregion

        #region 3DFACEs

        /// <summary>
        /// Takes the corners of a 3DFACE, as the library read it, into the meshes of its layer and colour: a triangle, a face of four, or each of the two
        /// triangles of one whose four corners are not in one plane.
        /// </summary>
        private void AddFace(DxfEntity e)
        {
            IReadOnlyList<GeoPoint3> corners = e.Shape is GeoTriangle3 t ? (IReadOnlyList<GeoPoint3>)new[] { t.A, t.B, t.C } : ((GeoFace3)e.Shape).Boundary.Vertices;
            string color = ItemColour(e);
            string key = e.Layer + "|" + color;

            if (!_faceSetIndex.TryGetValue(key, out FaceSet set))
            {
                set = new FaceSet { Layer = e.Layer, Color = color };
                _faceSetIndex.Add(key, set);
                _faceSets.Add(set);
            }

            set.Weld.Faces.Add(corners.Select(p => set.Weld.Vertex(new Vec3(p.X, p.Y, p.Z))).ToArray());
        }

        /// <summary>
        /// Makes meshes of the 3DFACEs, one for each body or loose piece of surface of a layer, told apart by their edges (see <see cref="MeshShells"/>): two bodies on
        /// one layer are two items however many corners they share, a body with a void in it is one, and a corner a hair off still closes a body.
        /// </summary>
        private void EmitFaceSets()
        {
            double tolerance = _b.Scene.EqualPoint;

            foreach (FaceSet set in _faceSets)
            {
                _cancel.ThrowIfCancellationRequested();
                List<ShellPiece> pieces = MeshShells.Split(set.Weld.Points, set.Weld.Faces, tolerance, _cancel);
                EnsureLayer(set.Layer);

                for (int p = 0; p < pieces.Count; p++)
                {
                    MeshShells.Extract(set.Weld.Points, set.Weld.Faces, pieces[p].Faces, false, out List<Vec3> points, out List<int[]> faces);
                    string name = set.Layer + " mesh" + (pieces.Count > 1 ? " " + (p + 1).ToString(CultureInfo.InvariantCulture) : string.Empty);
                    _b.Add("GeoMesh3", ImportData.Mesh(points, faces, 3), set.Layer, name, set.Color, "DXF 3DFACE");
                }
            }
        }

        #endregion
    }
}

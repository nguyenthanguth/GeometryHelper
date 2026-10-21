using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using GeometryHelper.Geometry;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Reads the DXF that <c>DxfWriter</c> writes, and what a CAD program writes of the same kinds: POINT, LINE, RAY, XLINE, LWPOLYLINE with bulges,
    /// POLYLINE (a 3D chain, a 2D chain with bulges, a polygon mesh, a polyface mesh), CIRCLE, ARC, ELLIPSE, SPLINE (by its control points, knots
    /// and weights), 3DFACE, TEXT and MTEXT, on their layers, in their colours. Anything else is counted and said once. The edges a 3DFACE marks invisible, which DxfWriter does between the triangles it cuts a side into, are drawn
    /// like the others, so the cut shows. The numbers are read as the file prints them, to the bit; the
    /// angles of an ARC are degrees in the file and come back as radians, so they are right to the last place or two, as are the major
    /// axis and the minor radius of an ELLIPSE, which the file keeps as an end point and a ratio.
    /// </summary>
    internal sealed class DxfReader
    {
        private static readonly Regex Escape = new Regex(@"\\U\+([0-9A-Fa-f]{4})", RegexOptions.CultureInvariant);

        private readonly ImportBuilder _b;
        private readonly CancellationToken _cancel;
        private readonly Dictionary<string, LayerInfo> _layers = new Dictionary<string, LayerInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _unknown = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> _unknownOrder = new List<string>();
        private readonly List<FaceSet> _faceSets = new List<FaceSet>();
        private readonly Dictionary<string, FaceSet> _faceSetIndex = new Dictionary<string, FaceSet>(StringComparer.Ordinal);
        private int _paperSpace;

        private DxfReader(ImportBuilder builder, CancellationToken cancel)
        {
            _b = builder;
            _cancel = cancel;
        }

        private sealed class LayerInfo
        {
            internal string Name;
            internal string Color;
            internal bool Visible = true;
        }

        private sealed class Group
        {
            internal int Code;
            internal string Value;
        }

        /// <summary>
        /// One record of the file: an entity, a table entry, a section's own header; the groups after its 0.
        /// </summary>
        private sealed class Record
        {
            internal string Type;
            internal string Section;
            internal readonly List<Group> Groups = new List<Group>();

            internal bool Has(int code) => Groups.Any(g => g.Code == code);

            internal string Text(int code)
            {
                foreach (Group g in Groups)
                {
                    if (g.Code == code)
                    {
                        return g.Value;
                    }
                }

                return null;
            }

            internal bool TryReal(int code, out double value)
            {
                value = 0.0;
                string text = Text(code);
                return text != null && ImportNumber.TryParse(text.Trim(), out value);
            }

            internal double Real(int code, double fallback) => TryReal(code, out double v) ? v : fallback;

            internal bool TryInt(int code, out int value)
            {
                value = 0;
                string text = Text(code);
                return text != null && int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }

            internal int Int(int code, int fallback) => TryInt(code, out int v) ? v : fallback;
        }

        /// <summary>
        /// The 3DFACEs of one layer, to be welded into meshes once they are all in.
        /// </summary>
        private sealed class FaceSet
        {
            internal string Layer;
            internal string Color;
            internal readonly MeshWelder Weld = new MeshWelder();
        }

        internal static Scene Read(string text, string title, CancellationToken cancel)
        {
            var b = new ImportBuilder(title);
            var reader = new DxfReader(b, cancel);
            reader.ReadAll(text);
            return b.Finish();
        }

        private void ReadAll(string text)
        {
            List<Record> records = Records(text);

            foreach (Record r in records)
            {
                if (r.Section == "TABLES" && r.Type == "LAYER")
                {
                    ReadLayer(r);
                }
            }

            int blockEntities = records.Count(r => r.Section == "BLOCKS" && r.Type != "BLOCK" && r.Type != "ENDBLK");

            if (blockEntities > 0)
            {
                _b.Warn("DXF: " + blockEntities.ToString(CultureInfo.InvariantCulture) + " entity(ies) in the BLOCKS section are not read; the viewer reads model space");
            }

            List<Record> entities = records.Where(r => r.Section == "ENTITIES").ToList();

            for (int i = 0; i < entities.Count; i++)
            {
                if ((i & 1023) == 0)
                {
                    _cancel.ThrowIfCancellationRequested();
                }

                Record r = entities[i];

                if (r.Type == "POLYLINE")
                {
                    var vertices = new List<Record>();

                    while (i + 1 < entities.Count && entities[i + 1].Type == "VERTEX")
                    {
                        vertices.Add(entities[++i]);
                    }

                    if (i + 1 < entities.Count && entities[i + 1].Type == "SEQEND")
                    {
                        i++;
                    }

                    if (PaperSpace(r) || Unreadable(r) || vertices.Any(Unreadable))
                    {
                        continue;
                    }

                    ReadPolyline(r, vertices);
                    continue;
                }

                if (r.Type == "VERTEX" || r.Type == "SEQEND" || r.Type == "ATTRIB")
                {
                    continue;
                }

                if (PaperSpace(r) || Unreadable(r))
                {
                    continue;
                }

                ReadEntity(r);
            }

            EmitFaceSets();

            foreach (string type in _unknownOrder)
            {
                _b.Warn("DXF: " + _unknown[type].ToString(CultureInfo.InvariantCulture) + " " + type + " entity(ies) are not read (the DXF reader takes what DxfWriter writes: POINT, LINE, RAY, XLINE, LWPOLYLINE, POLYLINE, CIRCLE, ARC, ELLIPSE, SPLINE, 3DFACE, TEXT, MTEXT)");
            }

            if (_paperSpace > 0)
            {
                _b.Warn("DXF: " + _paperSpace.ToString(CultureInfo.InvariantCulture) + " entity(ies) in paper space were left out; the viewer reads model space");
            }

        }

        #region The file

        /// <summary>
        /// Reads the pairs of lines, a group code and its value, into the records they make: a record starts at a group 0, and belongs to the section it
        /// is in. The code may be padded with spaces, as the writers pad it to three places, or not.
        /// </summary>
        private List<Record> Records(string text)
        {
            if (text.StartsWith("AutoCAD Binary DXF", StringComparison.Ordinal))
            {
                throw new FormatException("This is a binary DXF; the viewer reads the text form (save it as ASCII DXF).");
            }

            string[] lines = text.Split('\n');
            var records = new List<Record>();
            Record current = null;
            string section = null;
            bool expectSection = false;
            bool any = false;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if ((i & 8191) == 0)
                {
                    _cancel.ThrowIfCancellationRequested();
                }

                if (!int.TryParse(lines[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
                {
                    string said = "line " + (i + 1).ToString(CultureInfo.InvariantCulture) + " should be a group code, and is '" + Clip(lines[i].Trim()) + "'";

                    if (!any)
                    {
                        throw new FormatException("This is not a DXF: " + said + ".");
                    }

                    _b.Warn("DXF: reading stopped, " + said);
                    break;
                }

                any = true;
                string value = lines[i + 1].TrimEnd('\r');

                if (code == 0)
                {
                    string word = value.Trim();

                    if (word == "EOF")
                    {
                        break;
                    }

                    if (word == "SECTION")
                    {
                        expectSection = true;
                        current = null;
                    }
                    else if (word == "ENDSEC")
                    {
                        section = null;
                        current = null;
                    }
                    else
                    {
                        current = new Record { Type = word, Section = section };
                        records.Add(current);
                    }
                }
                else if (expectSection)
                {
                    // The group after a SECTION names it; anything else is a section without a name, whose records belong to none.
                    expectSection = false;
                    section = code == 2 ? value.Trim() : null;
                }
                else if (current != null)
                {
                    current.Groups.Add(new Group { Code = code, Value = value });
                }
            }

            return records;
        }

        private static string Clip(string text) => text.Length <= 24 ? text : text.Substring(0, 24) + "...";

        /// <summary>
        /// Whether a record has a number that cannot be read, which leaves the entity out, said: a number that reads as nothing is never taken as nought.
        /// The groups of 10 to 59, 110 to 149 and 210 to 239 are reals, and those of 60 to 99, 170 to 179, 270 to 289, 370 to 379 and 420 to 429 whole numbers.
        /// </summary>
        private bool Unreadable(Record r)
        {
            foreach (Group g in r.Groups)
            {
                int c = g.Code;
                bool real = (c >= 10 && c <= 59) || (c >= 110 && c <= 149) || (c >= 210 && c <= 239);
                bool whole = (c >= 60 && c <= 99) || (c >= 170 && c <= 179) || (c >= 270 && c <= 289) || (c >= 370 && c <= 379) || (c >= 420 && c <= 429);

                if ((real && !ImportNumber.TryParse(g.Value.Trim(), out _)) || (whole && !int.TryParse(g.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
                {
                    _b.Warn("DXF: a " + r.Type + " with a number that cannot be read ('" + Clip(g.Value.Trim()) + "' in group " + c.ToString(CultureInfo.InvariantCulture) + ") was left out");
                    return true;
                }
            }

            return false;
        }

        private bool PaperSpace(Record r)
        {
            if (r.Int(67, 0) == 1)
            {
                _paperSpace++;
                return true;
            }

            return false;
        }

        #endregion

        #region Layers and colours

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
            var info = new LayerInfo { Name = name, Color = TrueColour(r) ?? AciColour(colour == int.MinValue ? 0 : Math.Abs(colour)), Visible = colour >= 0 && !frozen };

            if (!info.Visible)
            {
                _b.Warn("DXF: the layer '" + name + "' is " + (frozen ? "frozen" : "turned off") + " in the file and is hidden; tick it in the scene tree to see it");
            }

            _layers[name] = info;
        }

        /// <summary>
        /// The colour of a true-colour group (420), as #RRGGBB, or null.
        /// </summary>
        private static string TrueColour(Record r)
        {
            if (!r.TryInt(420, out int rgb))
            {
                return null;
            }

            return "#" + ((rgb >> 16) & 255).ToString("X2", CultureInfo.InvariantCulture) + ((rgb >> 8) & 255).ToString("X2", CultureInfo.InvariantCulture) + (rgb & 255).ToString("X2", CultureInfo.InvariantCulture);
        }

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

        private string ItemColour(Record r)
        {
            string true24 = TrueColour(r);

            if (true24 != null)
            {
                return true24;
            }

            return r.TryInt(62, out int aci) ? AciColour(aci) : null;
        }

        private string LayerOf(Record r)
        {
            string name = DecodeName((r.Text(8) ?? "0").Trim());
            return name.Length == 0 ? "0" : name;
        }

        /// <summary>
        /// Adds an item on the layer of the record, making the layer with the colour and visibility the layer table gave it.
        /// </summary>
        private SceneItem Emit(string kind, System.Text.Json.JsonElement data, Record r, string layer = null, string color = null, string name = null)
        {
            layer = layer ?? LayerOf(r);
            EnsureLayer(layer);
            SceneItem item = _b.Add(kind, data, layer, name, color ?? ItemColour(r), "DXF " + r.Type);
            string handle = r.Text(5);

            if (!string.IsNullOrWhiteSpace(handle))
            {
                item.Properties["handle"] = handle.Trim();
            }

            return item;
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

        #region Entities

        private void ReadEntity(Record r)
        {
            switch (r.Type)
            {
                case "POINT":
                    if (TryPoint(r, 0, out Vec3 point))
                    {
                        Emit("GeoPoint3", ImportData.Point(point, 3), r);
                    }
                    else
                    {
                        Incomplete(r);
                    }

                    break;

                case "LINE":
                    if (TryPoint(r, 0, out Vec3 from) && TryPoint(r, 1, out Vec3 to))
                    {
                        Emit("GeoLine3", ImportData.Line(from, to, 3), r);
                    }
                    else
                    {
                        Incomplete(r);
                    }

                    break;

                case "RAY":
                case "XLINE":
                    if (TryPoint(r, 0, out Vec3 origin) && TryPoint(r, 1, out Vec3 direction))
                    {
                        Emit(r.Type == "RAY" ? "GeoRay3" : "GeoXLine3", ImportData.Line(origin, direction, 3), r);
                    }
                    else
                    {
                        Incomplete(r);
                    }

                    break;

                case "LWPOLYLINE":
                    ReadLightweight(r);
                    break;

                case "CIRCLE":
                    ReadCircle(r);
                    break;

                case "ARC":
                    ReadArc(r);
                    break;

                case "ELLIPSE":
                    ReadEllipse(r);
                    break;

                case "SPLINE":
                    ReadSpline(r);
                    break;

                case "3DFACE":
                    ReadFace(r);
                    break;

                case "TEXT":
                case "MTEXT":
                    ReadText(r);
                    break;

                default:
                    if (!_unknown.ContainsKey(r.Type))
                    {
                        _unknown.Add(r.Type, 0);
                        _unknownOrder.Add(r.Type);
                    }

                    _unknown[r.Type]++;
                    break;
            }
        }

        private void Incomplete(Record r) => _b.Warn("DXF: a " + r.Type + " without its coordinates was left out");

        /// <summary>
        /// Reads the point of a record held in the groups 10, 20, 30 (n = 0), 11, 21, 31 (n = 1) and so on; the z is nought where there is none.
        /// </summary>
        private static bool TryPoint(Record r, int n, out Vec3 p)
        {
            p = default(Vec3);

            if (!r.TryReal(10 + n, out double x) || !r.TryReal(20 + n, out double y))
            {
                return false;
            }

            p = new Vec3(x, y, r.Real(30 + n, 0.0));
            return true;
        }

        /// <summary>
        /// The extrusion direction of a record: the normal of the plane its 2D shape lies in. A unit vector is kept as the file has it, not divided by
        /// its length, which would move its last digit; anything else is made unit.
        /// </summary>
        private static Vec3 Extrusion(Record r)
        {
            if (!r.TryReal(210, out double x) || !r.TryReal(220, out double y) || !r.TryReal(230, out double z))
            {
                return new Vec3(0.0, 0.0, 1.0);
            }

            var n = new Vec3(x, y, z);
            double length = n.Length;

            if (length == 0.0 || double.IsNaN(length) || double.IsInfinity(length))
            {
                return new Vec3(0.0, 0.0, 1.0);
            }

            return Math.Abs(length - 1.0) <= 4E-16 ? n : n.Normalized();
        }

        private static bool IsUp(Vec3 n) => Math.Abs(n.X) <= 1E-12 && Math.Abs(n.Y) <= 1E-12 && n.Z > 0.0;

        /// <summary>
        /// Takes a point in the object coordinate system of an extrusion to the world, by the arbitrary axis algorithm of the DXF reference.
        /// </summary>
        private static Vec3 FromOcs(Vec3 n, Vec3 p)
        {
            Vec3 ax = Math.Abs(n.X) < 1.0 / 64.0 && Math.Abs(n.Y) < 1.0 / 64.0 ? Vec3.Cross(new Vec3(0.0, 1.0, 0.0), n) : Vec3.Cross(new Vec3(0.0, 0.0, 1.0), n);
            ax = ax.Normalized();
            Vec3 ay = Vec3.Cross(n, ax).Normalized();
            return ax * p.X + ay * p.Y + n * p.Z;
        }

        private void ReadCircle(Record r)
        {
            if (!TryPoint(r, 0, out Vec3 centre) || !r.TryReal(40, out double radius))
            {
                Incomplete(r);
                return;
            }

            Vec3 n = Extrusion(r);

            if (IsUp(n) && centre.Z == 0.0)
            {
                Emit("GeoCircle2", ImportData.Circle(centre, null, radius, 2), r);
            }
            else
            {
                Emit("GeoCircle3", ImportData.Circle(FromOcs(n, centre), n, radius, 3), r);
            }
        }

        private void ReadArc(Record r)
        {
            if (!TryPoint(r, 0, out Vec3 centre) || !r.TryReal(40, out double radius) || !r.TryReal(50, out double startDegrees) || !r.TryReal(51, out double endDegrees))
            {
                Incomplete(r);
                return;
            }

            // The arc runs counter-clockwise from its start to its end; equal angles are no arc, and a whole turn is a CIRCLE.
            double sweepDegrees = (endDegrees - startDegrees) % 360.0;

            if (sweepDegrees <= 0.0)
            {
                sweepDegrees += 360.0;
            }

            double start = startDegrees * Math.PI / 180.0;
            double sweep = sweepDegrees * Math.PI / 180.0;
            Vec3 n = Extrusion(r);

            if (IsUp(n) && centre.Z == 0.0)
            {
                Emit("GeoArc2", ImportData.Arc2(centre, radius, start, sweep), r);
                return;
            }

            // In space the library names the angle from a frame of its own, so the arc is rebuilt from three of its points.
            Vec3 Place(double angle) => FromOcs(n, new Vec3(centre.X + radius * Math.Cos(angle), centre.Y + radius * Math.Sin(angle), centre.Z));

            try
            {
                Vec3 a = Place(start);
                Vec3 m = Place(start + sweep * 0.5);
                Vec3 z = Place(start + sweep);
                GeoArc3 arc = GeoArc3.FromThreePoints(new GeoPoint3(a.X, a.Y, a.Z), new GeoPoint3(m.X, m.Y, m.Z), new GeoPoint3(z.X, z.Y, z.Z));
                Emit("GeoArc3", GeometryEncoder.Encode(arc), r);
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                _b.Warn("DXF: an ARC in space the library could not place was left out: " + e.Message);
            }
        }

        private void ReadEllipse(Record r)
        {
            if (!TryPoint(r, 0, out Vec3 centre) || !TryPoint(r, 1, out Vec3 axisEnd) || !r.TryReal(40, out double ratio))
            {
                Incomplete(r);
                return;
            }

            double from = r.Real(41, 0.0);
            double to = r.Real(42, 2.0 * Math.PI);

            if (to - from < 2.0 * Math.PI - 1E-9)
            {
                _b.Warn("DXF: an ELLIPSE that is only an arc of an ellipse (" + from.ToString("G6", CultureInfo.InvariantCulture) + " to " + to.ToString("G6", CultureInfo.InvariantCulture) + " radians) was left out; the viewer has no elliptical arc");
                return;
            }

            double major = axisEnd.Length;

            if (!(major > 0.0) || double.IsInfinity(major))
            {
                _b.Warn("DXF: an ELLIPSE whose major axis has no length was left out");
                return;
            }

            Vec3 axis = new Vec3(axisEnd.X / major, axisEnd.Y / major, axisEnd.Z / major);
            Vec3 n = Extrusion(r);

            if (IsUp(n) && centre.Z == 0.0 && axisEnd.Z == 0.0)
            {
                Emit("GeoEllipse2", ImportData.Ellipse(centre, null, axis, major, ratio * major, 2), r);
            }
            else
            {
                Emit("GeoEllipse3", ImportData.Ellipse(centre, n, axis, major, ratio * major, 3), r);
            }
        }

        /// <summary>
        /// Reads a SPLINE by its degree (71), its knots (each 40), its weights (each 41, none where it is not rational) and its control points
        /// (each 10, 20, 30), into the library's spline, every number as the file prints it: a spline in the plane where its extrusion is up and
        /// every control point at z = 0, else in space. A SPLINE given by its fit points alone (11, 21, 31) is left out with a warning, as is one
        /// the library does not take, a periodic one among them, its knots not clamped.
        /// </summary>
        private void ReadSpline(Record r)
        {
            var points = new List<double[]>();
            var knots = new List<double>();
            var weights = new List<double>();
            int fitPoints = 0;

            foreach (Group g in r.Groups)
            {
                if (g.Code != 10 && g.Code != 20 && g.Code != 30 && g.Code != 40 && g.Code != 41 && g.Code != 11)
                {
                    continue;
                }

                if (!ImportNumber.TryParse(g.Value.Trim(), out double value))
                {
                    continue;
                }

                switch (g.Code)
                {
                    case 10: points.Add(new[] { value, double.NaN, 0.0 }); break;
                    case 20: if (points.Count > 0) { points[points.Count - 1][1] = value; } break;
                    case 30: if (points.Count > 0) { points[points.Count - 1][2] = value; } break;
                    case 40: knots.Add(value); break;
                    case 41: weights.Add(value); break;
                    default: fitPoints++; break;
                }
            }

            if (points.Count == 0)
            {
                if (fitPoints > 0)
                {
                    _b.Warn("DXF: a SPLINE given by its " + fitPoints.ToString(CultureInfo.InvariantCulture) + " fit point(s) alone was left out; the viewer reads a spline by its control points, which the file does not give");
                }
                else
                {
                    Incomplete(r);
                }

                return;
            }

            if (weights.Count > 0 && weights.Count != points.Count)
            {
                _b.Warn("DXF: a SPLINE of " + points.Count.ToString(CultureInfo.InvariantCulture) + " control points with " + weights.Count.ToString(CultureInfo.InvariantCulture) + " weights was left out");
                return;
            }

            int degree = r.Int(71, 0);
            bool flat = IsUp(Extrusion(r)) && points.TrueForAll(p => p[2] == 0.0);

            try
            {
                object spline = flat
                    ? (object)new GeoSpline2(degree, points.Select(p => new GeoPoint2(p[0], p[1])), knots, weights.Count > 0 ? weights : null)
                    : new GeoSpline3(degree, points.Select(p => new GeoPoint3(p[0], p[1], p[2])), knots, weights.Count > 0 ? weights : null);
                Emit(flat ? "GeoSpline2" : "GeoSpline3", GeometryEncoder.Encode(spline), r);
            }
            catch (ArgumentException e)
            {
                _b.Warn("DXF: a SPLINE the library does not take was left out" + ((r.Int(70, 0) & 2) != 0 ? " (it is periodic, and the library's splines are clamped)" : string.Empty) + ": " + e.Message);
            }
        }

        private void ReadText(Record r)
        {
            if (!TryPoint(r, 0, out Vec3 at))
            {
                Incomplete(r);
                return;
            }

            string text;

            if (r.Type == "MTEXT")
            {
                // The text comes in pieces of 250 characters (3) and a last one (1), with formatting codes in it.
                text = string.Concat(r.Groups.Where(g => g.Code == 3).Select(g => g.Value)) + (r.Text(1) ?? string.Empty);
                text = PlainMText(text);
            }
            else
            {
                text = r.Text(1) ?? string.Empty;
            }

            text = DecodeName(text).Replace("%%d", "\u00B0").Replace("%%p", "\u00B1").Replace("%%c", "\u2300");
            Emit(SceneKinds.Text, ImportData.Text(at, text, r.Real(40, 0.0)), r);
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

        private void ReadLightweight(Record r)
        {
            var points = new List<double[]>();
            double elevation = r.Real(38, 0.0);
            bool closed = (r.Int(70, 0) & 1) != 0;

            foreach (Group g in r.Groups)
            {
                if (g.Code == 10)
                {
                    points.Add(new[] { Number(g), 0.0, 0.0 });
                }
                else if (points.Count > 0 && g.Code == 20)
                {
                    points[points.Count - 1][1] = Number(g);
                }
                else if (points.Count > 0 && g.Code == 42)
                {
                    points[points.Count - 1][2] = Number(g);
                }
            }

            if (points.Count < 2)
            {
                _b.Warn("DXF: a LWPOLYLINE of fewer than two vertices was left out");
                return;
            }

            // The vertices are in the plane of the extrusion, at the elevation along it; where that plane is not the floor they are taken to the world.
            Vec3 normal = Extrusion(r);
            Vec3[] corners = points.Select(p => new Vec3(p[0], p[1], elevation)).ToArray();

            if (!IsUp(normal))
            {
                Chain(r, corners.Select(c => FromOcs(normal, c)).ToArray(), points.Select(p => p[2]).ToArray(), closed, 3, normal);
                return;
            }

            Chain(r, corners, points.Select(p => p[2]).ToArray(), closed, elevation == 0.0 ? 2 : 3, null);
        }

        private static double Number(Group g) => ImportNumber.TryParse(g.Value.Trim(), out double v) ? v : 0.0;

        /// <summary>
        /// Emits a chain or a loop of points, with the bulge of each edge where any is not nought: a bulge sits on the vertex the edge leaves, and on
        /// the last vertex of an open chain it leaves nothing.
        /// </summary>
        private void Chain(Record r, Vec3[] points, double[] bulges, bool closed, int dimension, Vec3? normal)
        {
            bool arcs = bulges != null && bulges.Take(closed ? bulges.Length : bulges.Length - 1).Any(x => x != 0.0);
            string number = dimension.ToString(CultureInfo.InvariantCulture);
            string shape = closed ? "GeoPolygon" : "GeoPolyline";

            if (!arcs)
            {
                Emit(shape + number, ImportData.Chain(points, dimension), r);
                return;
            }

            double[] used = (double[])bulges.Clone();

            if (!closed)
            {
                used[used.Length - 1] = 0.0;
            }

            if (dimension == 2)
            {
                Emit(shape + "Arc2", ImportData.Chain(points, 2, used), r);
            }
            else
            {
                // An arc in space is read about a normal: the extrusion of the chain, or up where it lies on the floor.
                Vec3 about = normal ?? new Vec3(0.0, 0.0, 1.0);
                Emit(shape + "Arc3", ImportData.Chain(points, 3, used, points.Select(_ => about).ToArray()), r);
            }
        }

        private void ReadPolyline(Record r, List<Record> vertices)
        {
            int flags = r.Int(70, 0);

            if ((flags & 64) != 0)
            {
                ReadPolyface(r, vertices);
                return;
            }

            if ((flags & 16) != 0)
            {
                ReadPolygonMesh(r, vertices, flags);
                return;
            }

            bool closed = (flags & 1) != 0;
            double elevation = r.Real(30, 0.0);
            var points = new List<Vec3>();
            var bulges = new List<double>();
            int fitPoints = 0;

            foreach (Record v in vertices)
            {
                // A vertex made by a curve fit or a spline frame is not a corner of the chain.
                if ((v.Int(70, 0) & (1 | 8 | 16)) != 0 && (flags & 8) == 0)
                {
                    fitPoints++;
                    continue;
                }

                if (!TryPoint(v, 0, out Vec3 p))
                {
                    continue;
                }

                points.Add(v.Has(30) ? p : new Vec3(p.X, p.Y, elevation));
                bulges.Add(v.Real(42, 0.0));
            }

            if (fitPoints > 0)
            {
                _b.Warn("DXF: " + fitPoints.ToString(CultureInfo.InvariantCulture) + " vertex(es) of a POLYLINE made by a curve fit or a spline frame were left out");
            }

            if (points.Count < 2)
            {
                _b.Warn("DXF: a POLYLINE of fewer than two vertices was left out");
                return;
            }

            bool threeD = (flags & 8) != 0;
            Vec3 normal = threeD ? new Vec3(0.0, 0.0, 1.0) : Extrusion(r);

            // The vertices of a 2D chain are in the plane of its extrusion; where that is not the floor they are taken to the world.
            if (!threeD && !IsUp(normal))
            {
                Chain(r, points.Select(c => FromOcs(normal, c)).ToArray(), bulges.ToArray(), closed, 3, normal);
                return;
            }

            bool space = threeD || points.Any(p => p.Z != 0.0);
            Chain(r, points.ToArray(), threeD ? null : bulges.ToArray(), closed, space ? 3 : 2, null);
        }

        private void ReadPolyface(Record r, List<Record> vertices)
        {
            var points = new List<Vec3>();
            var faces = new List<int[]>();
            int refused = 0;

            foreach (Record v in vertices)
            {
                int vflags = v.Int(70, 0);

                if ((vflags & 64) != 0 && TryPoint(v, 0, out Vec3 p))
                {
                    points.Add(p);
                }
            }

            foreach (Record v in vertices)
            {
                if ((v.Int(70, 0) & 128) == 0 || (v.Int(70, 0) & 64) != 0)
                {
                    continue;
                }

                var corners = new List<int>();

                foreach (int code in new[] { 71, 72, 73, 74 })
                {
                    // The sign says whether the edge is hidden; the number counts the vertices from one. Nought ends the list.
                    int n = v.Int(code, 0);
                    n = n == int.MinValue ? int.MaxValue : Math.Abs(n);

                    if (n == 0)
                    {
                        break;
                    }

                    corners.Add(n - 1);
                }

                if (corners.Count < 3 || corners.Any(i => i >= points.Count))
                {
                    refused++;
                    continue;
                }

                faces.Add(corners.ToArray());
            }

            if (refused > 0)
            {
                _b.Warn("DXF: " + refused.ToString(CultureInfo.InvariantCulture) + " face(s) of a polyface mesh with fewer than three corners, or naming a vertex that is not there, were left out");
            }

            if (faces.Count > 0)
            {
                Emit("GeoMesh3", ImportData.Mesh(points, faces, 3), r);
            }
        }

        private void ReadPolygonMesh(Record r, List<Record> vertices, int flags)
        {
            int m = r.Int(71, 0);
            int n = r.Int(72, 0);
            var points = new List<Vec3>();

            foreach (Record v in vertices)
            {
                if (TryPoint(v, 0, out Vec3 p))
                {
                    points.Add(p);
                }
            }

            if (m < 2 || n < 2 || points.Count < (long)m * n)
            {
                _b.Warn("DXF: a polygon mesh of " + m.ToString(CultureInfo.InvariantCulture) + " by " + n.ToString(CultureInfo.InvariantCulture) + " vertices with " + points.Count.ToString(CultureInfo.InvariantCulture) + " vertex records was left out");
                return;
            }

            bool closedM = (flags & 1) != 0;
            bool closedN = (flags & 32) != 0;
            var faces = new List<int[]>();

            for (int i = 0; i < (closedM ? m : m - 1); i++)
            {
                for (int j = 0; j < (closedN ? n : n - 1); j++)
                {
                    int i1 = (i + 1) % m;
                    int j1 = (j + 1) % n;
                    faces.Add(new[] { (i * n) + j, (i * n) + j1, (i1 * n) + j1, (i1 * n) + j });
                }
            }

            Emit("GeoMesh3", ImportData.Mesh(points.Take(m * n).ToList(), faces, 3), r);
        }

        #endregion

        #region 3DFACEs

        private void ReadFace(Record r)
        {
            if (!TryPoint(r, 0, out Vec3 a) || !TryPoint(r, 1, out Vec3 b) || !TryPoint(r, 2, out Vec3 c))
            {
                Incomplete(r);
                return;
            }

            // The fourth corner is the third again for a triangle, and may be missing altogether.
            Vec3 d = TryPoint(r, 3, out Vec3 fourth) ? fourth : c;
            var corners = new List<Vec3> { a, b, c, d };

            // Corners in a row that are one point are one corner, so that a quad folded to a triangle is read as one.
            for (int i = corners.Count - 1; i >= 1; i--)
            {
                if (new BitKey(corners[i]).Equals(new BitKey(corners[i - 1])))
                {
                    corners.RemoveAt(i);
                }
            }

            while (corners.Count > 1 && new BitKey(corners[0]).Equals(new BitKey(corners[corners.Count - 1])))
            {
                corners.RemoveAt(corners.Count - 1);
            }

            if (corners.Count < 3)
            {
                _b.Warn("DXF: a 3DFACE with fewer than three different corners was left out");
                return;
            }

            string layer = LayerOf(r);
            string color = ItemColour(r);
            string key = layer + "|" + color;

            if (!_faceSetIndex.TryGetValue(key, out FaceSet set))
            {
                set = new FaceSet { Layer = layer, Color = color };
                _faceSetIndex.Add(key, set);
                _faceSets.Add(set);
            }

            set.Weld.Faces.Add(corners.Select(set.Weld.Vertex).ToArray());
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

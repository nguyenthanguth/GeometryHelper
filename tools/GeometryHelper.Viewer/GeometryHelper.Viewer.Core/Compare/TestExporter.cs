using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Writes the source of an xUnit test for shapes seen in the viewer, in the style of the repository's tests: a method named as a sentence, the
    /// tolerance of the scene passed explicitly, each shape built in code from the 17-digit literals of its numbers, and one assertion for each
    /// number the library gives for it, holding the value the viewer measured in a comment and <c>double.NaN</c> as the expected value, so the test
    /// is red until someone writes down what is right.
    /// </summary>
    /// <remarks>
    /// The text is for the clipboard or a file the user picks; nothing here writes into the repository's tests folder.
    /// </remarks>
    public static class TestExporter
    {
        /// <summary>
        /// The most items one export writes; the rest are named in a comment.
        /// </summary>
        public const int MaxItems = 40;

        // What the library gives for each kind, as (label of ItemMeasures.Numbers, member to read it by, whether it is a count).
        private static readonly Dictionary<string, string[]> _asserts = BuildAsserts();

        /// <summary>
        /// Writes the test class.
        /// </summary>
        /// <param name="scene">The scene the items are in; the tolerance it was written at is the tolerance of the test.</param>
        /// <param name="items">The items to write a test for, in the order given; a label, and a kind the viewer does not know, are left out and said so in a comment.</param>
        /// <returns>The C# source of a file with one class, <c>ViewerExportTests</c>, in the namespace <c>GeometryHelper.UnitTest</c>, with LF line ends.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the scene or the items are null.</exception>
        public static string Export(Scene scene, IReadOnlyList<SceneItem> items)
        {
            if (scene == null)
            {
                throw new ArgumentNullException(nameof(scene));
            }

            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            return Export(items.Select(item => new KeyValuePair<Scene, SceneItem>(scene, item)).ToList());
        }

        /// <summary>
        /// Writes the test class for items that come from different scenes, each with the tolerance of its own scene: the two sides of a comparison, each
        /// written at the tolerance its script set and with the numbers its own build of the library gave.
        /// </summary>
        /// <param name="items">Each item with the scene it is in, in the order to write them; as for <see cref="Export(Scene, IReadOnlyList{SceneItem})"/>.</param>
        /// <returns>The C# source, as <see cref="Export(Scene, IReadOnlyList{SceneItem})"/> gives it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the items, or a scene in them, are null.</exception>
        public static string Export(IReadOnlyList<KeyValuePair<Scene, SceneItem>> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (items.Any(pair => pair.Key == null))
            {
                throw new ArgumentNullException(nameof(items), "An item needs its scene.");
            }

            var text = new StringBuilder();
            string title = string.Join("', '", items.Select(pair => pair.Key.Title ?? string.Empty).Distinct(StringComparer.Ordinal));
            text.Append("// Written by the GeometryHelper viewer from the scene '").Append(title).Append("'.\n");
            text.Append("// Every number is the value the library gave at the tolerance below; the expected values are yours to fill in, which is what makes the tests red.\n");
            text.Append("using System.Collections.Generic;\n");
            text.Append("using System.Linq;\n");
            text.Append("using GeometryHelper.Geometry;\n");
            text.Append("using Xunit;\n\n");
            text.Append("namespace GeometryHelper.UnitTest\n{\n");
            text.Append("    public class ViewerExportTests\n    {\n");

            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            int written = 0;
            var left = new List<string>();

            foreach (KeyValuePair<Scene, SceneItem> pair in items)
            {
                SceneItem item = pair.Value;

                if (item == null || item.Kind == SceneKinds.Text || !SceneKinds.IsKnown(item.Kind))
                {
                    left.Add((item?.Name ?? "?") + " (" + (item?.Kind ?? "null") + "): the viewer has no library shape for it");
                    continue;
                }

                if (written >= MaxItems)
                {
                    left.Add((item.Name ?? item.Kind) + ": more than " + MaxItems.ToString(CultureInfo.InvariantCulture) + " items");
                    continue;
                }

                if (written > 0)
                {
                    text.Append('\n');
                }

                WriteTest(text, pair.Key, item, usedNames);
                written++;
            }

            foreach (string line in left)
            {
                text.Append("\n        // Not written: ").Append(line.Replace("\r", " ").Replace("\n", " ")).Append('\n');
            }

            text.Append("    }\n}\n");
            return text.ToString();
        }

        private static void WriteTest(StringBuilder text, Scene scene, SceneItem item, HashSet<string> usedNames)
        {
            string sentence = Pascal(item.Name ?? item.Kind);
            string method = sentence + "_HasTheExpectedNumbers";

            for (int n = 2; !usedNames.Add(method); n++)
            {
                method = sentence + n.ToString(CultureInfo.InvariantCulture) + "_HasTheExpectedNumbers";
            }

            string variable = Camel(item.Name ?? item.Kind);
            Tolerance t = scene.Tolerance;

            text.Append("        [Fact]\n");
            text.Append("        public void ").Append(method).Append("()\n        {\n");
            text.Append("            // ").Append(item.Kind).Append(" '").Append((item.Name ?? string.Empty).Replace("\r", " ").Replace("\n", " ")).Append("' on layer '").Append((item.Layer ?? string.Empty).Replace("\r", " ").Replace("\n", " ")).Append("'.\n");
            text.Append("            var tolerance = new Tolerance(").Append(D(t.EqualPoint)).Append(", ").Append(D(t.EqualVector)).Append(", ").Append(D(t.EqualAngleRad)).Append(", ").Append(D(t.EqualPlanar)).Append(");\n\n");
            text.Append("            using (Tolerance.Use(tolerance))\n            {\n");
            text.Append("                var ").Append(variable).Append(" = ").Append(Expression(item, 16)).Append(";\n");

            IReadOnlyList<KeyValuePair<string, string>> numbers;
            string measuredBy = "the viewer";

            if (item.Properties.Keys.Any(k => k.StartsWith(RunnerMeasures.Prefix, StringComparison.Ordinal)))
            {
                // A scene a build of the library wrote carries the numbers that build gave: those are the ones to write down, not the viewer's own.
                measuredBy = "the build that made the scene";
                numbers = MeasuredNumbers(item);

                if (item.Properties.TryGetValue(RunnerMeasures.Prefix + "error", out string error))
                {
                    text.Append("\n                // That build could not measure the shape: ").Append(error.Replace("\r", " ").Replace("\n", " ")).Append('\n');
                }

                if (item.Properties.TryGetValue(RunnerMeasures.Prefix + "note", out string note))
                {
                    text.Append("\n                // ").Append(note.Replace("\r", " ").Replace("\n", " ")).Append('\n');
                }
            }
            else
            {
                try
                {
                    numbers = ItemMeasures.Numbers(item, scene);
                }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
                {
                    text.Append("\n                // The library refused the shape at this tolerance and at 1E-12 when it was measured: ").Append(e.Message.Replace("\r", " ").Replace("\n", " ")).Append('\n');
                    numbers = new KeyValuePair<string, string>[0];
                }
            }

            bool any = false;

            if (_asserts.TryGetValue(item.Kind, out string[] members))
            {
                foreach (string entry in members)
                {
                    // "Label=expression"; the expression is what follows the variable.
                    int cut = entry.IndexOf('=');
                    string label = entry.Substring(0, cut);
                    string member = entry.Substring(cut + 1);
                    KeyValuePair<string, string> number = numbers.FirstOrDefault(n => n.Key == label);

                    if (number.Key == null)
                    {
                        continue;
                    }

                    any = true;
                    string expected = "expected" + Pascal(label);
                    bool count = label == "Vertices";
                    string access = variable + "." + member;

                    text.Append('\n');
                    text.Append("                // TODO: the expected ").Append(label.ToLowerInvariant()).Append("; ").Append(measuredBy).Append(" measured ").Append(number.Value).Append(".\n");

                    if (count)
                    {
                        text.Append("                int ").Append(expected).Append(" = -1;\n");
                        text.Append("                Assert.Equal(").Append(expected).Append(", ").Append(access).Append(");\n");
                    }
                    else
                    {
                        text.Append("                double ").Append(expected).Append(" = double.NaN;\n");
                        text.Append("                Assert.Equal(").Append(expected).Append(", ").Append(access).Append(", tolerance.EqualPoint);\n");
                    }
                }
            }

            if (!any)
            {
                text.Append("\n                // TODO: this shape has no number to assert; call the member under test on it and assert what it gives.\n");
                text.Append("                Assert.Fail(\"TODO: write what ").Append(variable).Append(" should give.\");\n");
            }

            text.Append("            }\n        }\n");
        }

        /// <summary>
        /// Reads the numbers a build wrote into an item, under the names <see cref="ItemMeasures.Numbers"/> gives them.
        /// </summary>
        private static IReadOnlyList<KeyValuePair<string, string>> MeasuredNumbers(SceneItem item)
        {
            var numbers = new List<KeyValuePair<string, string>>();

            foreach (string[] pair in new[] { new[] { "vertices", "Vertices" }, new[] { "length", "Length" }, new[] { "area", "Area" }, new[] { "volume", "Volume" }, new[] { "surfaceArea", "Surface area" } })
            {
                if (item.Properties.TryGetValue(RunnerMeasures.Prefix + pair[0], out string value))
                {
                    numbers.Add(new KeyValuePair<string, string>(pair[1], value));
                }
            }

            return numbers;
        }

        private static Dictionary<string, string[]> BuildAsserts()
        {
            string[] chain = { "Vertices=VertexCount", "Length=Length" };
            string[] loop = { "Vertices=VertexCount", "Length=Length", "Area=Area" };

            return new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["GeoPoint2"] = new[] { "X=X", "Y=Y" },
                ["GeoPoint3"] = new[] { "X=X", "Y=Y", "Z=Z" },
                ["GeoLine2"] = new[] { "Length=Length" },
                ["GeoLine3"] = new[] { "Length=Length" },
                ["GeoPolyline2"] = chain,
                ["GeoPolyline3"] = chain,
                ["GeoPolylineArc2"] = chain,
                ["GeoPolylineArc3"] = chain,
                ["GeoPolygon2"] = loop,
                ["GeoPolygon3"] = loop,
                ["GeoPolygonArc2"] = loop,
                ["GeoPolygonArc3"] = loop,
                ["GeoArc2"] = new[] { "Radius=Radius", "Length=Length" },
                ["GeoArc3"] = new[] { "Radius=Radius", "Length=Length" },
                ["GeoCircle2"] = new[] { "Radius=Radius", "Length=Length", "Area=Area" },
                ["GeoCircle3"] = new[] { "Radius=Radius", "Length=Length", "Area=Area" },
                ["GeoEllipse2"] = new[] { "Major radius=MajorRadius", "Minor radius=MinorRadius", "Length=Length", "Area=Area" },
                ["GeoEllipse3"] = new[] { "Major radius=MajorRadius", "Minor radius=MinorRadius", "Length=Length", "Area=Area" },
                ["GeoRectangle2"] = new[] { "Width=Width", "Height=Height", "Area=Area" },
                ["GeoTriangle2"] = new[] { "Length=Length", "Area=Area" },
                ["GeoTriangle3"] = new[] { "Length=Perimeter", "Area=Area" },
                ["GeoFace2"] = new[] { "Length=Length", "Area=Area" },
                ["GeoFace3"] = new[] { "Area=Area" },
                ["GeoSolid3"] = new[] { "Volume=GetVolume()", "Surface area=GetSurfaceArea()" },
                ["GeoMesh2"] = new[] { "Area=Sum(f => f.Area)" },
                ["GeoMesh3"] = new[] { "Area=Sum(f => f.Area)" },
                ["GeoAabb3"] = new[] { "Volume=Volume" },
                ["GeoObb3"] = new[] { "Volume=Volume" },
            };
        }

        #region Names

        private static string Pascal(string text)
        {
            var name = new StringBuilder();
            bool upper = true;

            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c) && c < 128)
                {
                    name.Append(upper ? char.ToUpperInvariant(c) : c);
                    upper = false;
                }
                else
                {
                    upper = true;
                }
            }

            if (name.Length == 0 || char.IsDigit(name[0]))
            {
                name.Insert(0, "Item");
            }

            return name.ToString();
        }

        private static string Camel(string text)
        {
            string name = Pascal(text);
            string camel = char.ToLowerInvariant(name[0]) + name.Substring(1);

            // A name that is a keyword of C# or one of the names the test itself uses.
            switch (camel)
            {
                case "tolerance":
                case "double":
                case "int":
                case "class":
                case "object":
                case "string":
                case "var":
                case "new":
                case "this":
                case "base":
                case "default":
                case "params":
                case "ref":
                case "out":
                case "in":
                case "is":
                case "as":
                case "do":
                case "if":
                case "for":
                case "foreach":
                case "while":
                case "switch":
                case "case":
                case "true":
                case "false":
                case "null":
                    return camel + "Shape";
                default:
                    return camel;
            }
        }

        #endregion

        #region Literals and expressions

        /// <summary>
        /// Writes a number as a C# literal of type double that reads back as the same bits.
        /// </summary>
        internal static string D(double value)
        {
            if (double.IsNaN(value))
            {
                return "double.NaN";
            }

            if (double.IsPositiveInfinity(value))
            {
                return "double.PositiveInfinity";
            }

            if (double.IsNegativeInfinity(value))
            {
                return "double.NegativeInfinity";
            }

            if (value == 0.0)
            {
                return BitConverter.DoubleToInt64Bits(value) < 0L ? "-0.0" : "0.0";
            }

            string literal = value.ToString("G17", CultureInfo.InvariantCulture);
            return literal.IndexOf('.') < 0 && literal.IndexOf('E') < 0 ? literal + ".0" : literal;
        }

        private static string P2(Vec3 v) => "new GeoPoint2(" + D(v.X) + ", " + D(v.Y) + ")";

        private static string P3(Vec3 v) => "new GeoPoint3(" + D(v.X) + ", " + D(v.Y) + ", " + D(v.Z) + ")";

        private static string V2(Vec3 v) => "new GeoVector2(" + D(v.X) + ", " + D(v.Y) + ")";

        private static string V3(Vec3 v) => "new GeoVector3(" + D(v.X) + ", " + D(v.Y) + ", " + D(v.Z) + ")";

        /// <summary>
        /// Writes an array of the elements, on one line when it is short and one to a line when not.
        /// </summary>
        private static string Array(string type, IReadOnlyList<string> elements, int indent)
        {
            if (elements.Count == 0)
            {
                return "new " + type + "[0]";
            }

            string inline = "new " + type + "[] { " + string.Join(", ", elements) + " }";

            if (elements.Count <= 3 && inline.Length <= 96 && inline.IndexOf('\n') < 0)
            {
                return inline;
            }

            string pad = new string(' ', indent + 4);
            return "new " + type + "[]\n" + new string(' ', indent) + "{\n" + string.Join(string.Empty, elements.Select(e => pad + e + ",\n")) + new string(' ', indent) + "}";
        }

        /// <summary>
        /// Writes a list of the elements the way an array is written.
        /// </summary>
        private static string ListOf(string type, IReadOnlyList<string> elements, int indent)
        {
            if (elements.Count == 0)
            {
                return "new List<" + type + ">()";
            }

            string pad = new string(' ', indent + 4);
            return "new List<" + type + ">\n" + new string(' ', indent) + "{\n" + string.Join(string.Empty, elements.Select(e => pad + e + ",\n")) + new string(' ', indent) + "}";
        }

        private static string Numbers(double[] values) => "new[] { " + string.Join(", ", values.Select(D)) + " }";

        /// <summary>
        /// Writes the C# expression that builds the library shape of an item with the library's own constructors, from the numbers of its data,
        /// as <see cref="SceneItem.ToGeometry"/> would build it.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="indent">How many spaces the lines after the first are indented by.</param>
        /// <exception cref="NotSupportedException">Thrown for a label and for a kind the viewer does not know.</exception>
        internal static string Expression(SceneItem item, int indent)
        {
            JsonElement d = item.Data;

            switch (item.Kind)
            {
                case "GeoPoint2": return P2(DataJson.Point(d, 2));
                case "GeoPoint3": return P3(DataJson.Point(d, 3));
                case "GeoLine2": return "new GeoLine2(" + P2(DataJson.Point(d, "a", 2)) + ", " + P2(DataJson.Point(d, "b", 2)) + ")";
                case "GeoLine3": return "new GeoLine3(" + P3(DataJson.Point(d, "a", 3)) + ", " + P3(DataJson.Point(d, "b", 3)) + ")";
                case "GeoRay2": return "new GeoRay2(" + P2(DataJson.Point(d, "a", 2)) + ", " + V2(DataJson.Point(d, "b", 2)) + ")";
                case "GeoRay3": return "new GeoRay3(" + P3(DataJson.Point(d, "a", 3)) + ", " + V3(DataJson.Point(d, "b", 3)) + ")";
                case "GeoXLine2": return "new GeoXLine2(" + P2(DataJson.Point(d, "a", 2)) + ", " + V2(DataJson.Point(d, "b", 2)) + ")";
                case "GeoXLine3": return "new GeoXLine3(" + P3(DataJson.Point(d, "a", 3)) + ", " + V3(DataJson.Point(d, "b", 3)) + ")";
                case "GeoPolyline2": return "new GeoPolyline2(" + Array("GeoPoint2", DataJson.Points(d, "points", 2).Select(P2).ToList(), indent) + ")";
                case "GeoPolygon2": return "new GeoPolygon2(" + Array("GeoPoint2", DataJson.Points(d, "points", 2).Select(P2).ToList(), indent) + ")";
                case "GeoPolyline3": return "new GeoPolyline3(" + Array("GeoPoint3", DataJson.Points(d, "points", 3).Select(P3).ToList(), indent) + ")";
                case "GeoPolygon3": return "new GeoPolygon3(" + Array("GeoPoint3", DataJson.Points(d, "points", 3).Select(P3).ToList(), indent) + ")";

                case "GeoPolylineArc2":
                case "GeoPolygonArc2":
                {
                    Vec3[] points = DataJson.Points(d, "points", 2);
                    return "new " + item.Kind + "(" + Array("GeoPoint2", points.Select(P2).ToList(), indent) + ", " + Numbers(DataJson.Bulges(d, points.Length)) + ")";
                }

                case "GeoPolylineArc3":
                case "GeoPolygonArc3":
                {
                    Vec3[] points = DataJson.Points(d, "points", 3);
                    var normals = new List<string>();
                    Vec3[] given = DataJson.TryGet(d, "normals", out JsonElement list) ? DataJson.Points(list, 3) : new Vec3[0];

                    for (int i = 0; i < points.Length; i++)
                    {
                        normals.Add(i < given.Length ? V3(given[i]) : "default(GeoVector3)");
                    }

                    return "new " + item.Kind + "(" + Array("GeoPoint3", points.Select(P3).ToList(), indent) + ", " + Numbers(DataJson.Bulges(d, points.Length)) + ", " + Array("GeoVector3", normals, indent) + ")";
                }

                case "GeoArc2":
                {
                    double sweep = DataJson.Number(d, "sweep");
                    double start = DataJson.Number(d, "start");
                    return "new GeoArc2(" + P2(DataJson.Point(d, "center", 2)) + ", " + D(DataJson.Number(d, "radius")) + ", " + D(start) + ", " + D(start + sweep) + ", " + (sweep < 0.0 ? "true" : "false") + ")";
                }

                case "GeoArc3":
                {
                    double sweep = DataJson.Number(d, "sweep");
                    double start = DataJson.Number(d, "start");
                    return "new GeoArc3(" + P3(DataJson.Point(d, "center", 3)) + ", " + V3(DataJson.Point(d, "normal", 3)) + ", " + D(DataJson.Number(d, "radius")) + ", " + D(start) + ", " + D(start + sweep) + ", " + (sweep < 0.0 ? "true" : "false") + ")";
                }

                case "GeoCircle2": return "new GeoCircle2(" + P2(DataJson.Point(d, "center", 2)) + ", " + D(DataJson.Number(d, "radius")) + ")";
                case "GeoCircle3": return "new GeoCircle3(" + P3(DataJson.Point(d, "center", 3)) + ", " + V3(DataJson.Point(d, "normal", 3)) + ", " + D(DataJson.Number(d, "radius")) + ")";

                case "GeoEllipse2":
                    return "new GeoEllipse2(" + P2(DataJson.Point(d, "center", 2)) + ", " + V2(DataJson.Point(d, "majorAxis", 2)) + ", " + D(DataJson.Number(d, "majorRadius")) + ", " + D(DataJson.Number(d, "minorRadius")) + ")";

                case "GeoEllipse3":
                    return "new GeoEllipse3(" + P3(DataJson.Point(d, "center", 3)) + ", " + V3(DataJson.Point(d, "normal", 3)) + ", " + V3(DataJson.Point(d, "majorAxis", 3)) + ", " + D(DataJson.Number(d, "majorRadius")) + ", " + D(DataJson.Number(d, "minorRadius")) + ")";

                case "GeoRectangle2":
                    return "new GeoRectangle2(" + P2(DataJson.Point(d, "center", 2)) + ", " + D(DataJson.Number(d, "width")) + ", " + D(DataJson.Number(d, "height")) + ", " + D(DataJson.Number(d, "angle")) + ")";

                case "GeoTriangle2":
                {
                    Vec3[] c = DataJson.Points(d, "points", 2);
                    return "new GeoTriangle2(" + P2(c[0]) + ", " + P2(c[1]) + ", " + P2(c[2]) + ")";
                }

                case "GeoTriangle3":
                {
                    Vec3[] c = DataJson.Points(d, "points", 3);
                    return "new GeoTriangle3(" + P3(c[0]) + ", " + P3(c[1]) + ", " + P3(c[2]) + ")";
                }

                case "GeoFace2":
                case "GeoFace3":
                    return Face(d, item.Kind == "GeoFace3", indent);

                case "GeoSolid3":
                    return Solid(d, indent);

                case "GeoMesh2":
                {
                    Vec3[] points = DataJson.Points(d, "points", 2);
                    var faces = GeometryDecoder.Faces(d).Select(face => "new GeoPolygon2(" + Array("GeoPoint2", face.Select(i => P2(points[i])).ToList(), indent + 4) + ")").ToList();
                    return Array("GeoPolygon2", faces, indent);
                }

                case "GeoMesh3":
                {
                    Vec3[] points = DataJson.Points(d, "points", 3);
                    var faces = GeometryDecoder.Faces(d).Select(face => "new GeoPolygon3(" + Array("GeoPoint3", face.Select(i => P3(points[i])).ToList(), indent + 4) + ")").ToList();
                    return Array("GeoPolygon3", faces, indent);
                }

                case "GeoAabb3":
                    return DataJson.TryGet(d, "empty", out JsonElement empty) && empty.ValueKind == JsonValueKind.True
                        ? "GeoAabb3.Empty"
                        : "new GeoAabb3(" + P3(DataJson.Point(d, "min", 3)) + ", " + P3(DataJson.Point(d, "max", 3)) + ")";

                case "GeoObb3":
                    return "new GeoObb3(" + P3(DataJson.Point(d, "center", 3)) + ", " + D(DataJson.Number(d, "sizeX")) + ", " + D(DataJson.Number(d, "sizeY")) + ", " + D(DataJson.Number(d, "sizeZ")) + ", " + V3(DataJson.Point(d, "axisX", 3)) + ", " + V3(DataJson.Point(d, "axisY", 3)) + ")";

                case "GeoPlane3":
                    return "new GeoPlane3(" + P3(DataJson.Point(d, "origin", 3)) + ", " + V3(DataJson.Point(d, "normal", 3)) + ")";

                default:
                    throw new NotSupportedException("A " + item.Kind + " has no library shape to write in code.");
            }
        }

        private static string Face(JsonElement d, bool space, int indent)
        {
            string polygon = space ? "GeoPolygon3" : "GeoPolygon2";
            string point = space ? "GeoPoint3" : "GeoPoint2";
            int dimension = space ? 3 : 2;

            string Loop(JsonElement loop) => "new " + polygon + "(" + Array(point, DataJson.Points(loop, dimension).Select(space ? (Func<Vec3, string>)P3 : P2).ToList(), indent + 4) + ")";

            var holes = DataJson.List(d, "holes").Select(Loop).ToList();
            return "new " + (space ? "GeoFace3" : "GeoFace2") + "(" + Loop(DataJson.Get(d, "outer")) + ", " + ListOf(polygon, holes, indent + 4) + ")";
        }

        private static string Solid(JsonElement d, int indent)
        {
            var faces = DataJson.List(d, "faces").Select(face => Face(face, true, indent + 8)).ToList();
            var openings = DataJson.TryGet(d, "openings", out JsonElement list) ? list.EnumerateArray().Select(o => Solid(o, indent + 8)).ToList() : new List<string>();
            return "new GeoSolid3(" + ListOf("GeoFace3", faces, indent + 4) + ", " + ListOf("GeoSolid3", openings, indent + 4) + ")";
        }

        #endregion
    }
}

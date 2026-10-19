using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// A point that is the same as another only when every coordinate is the same to the bit, nought and minus nought alike: the key a
    /// file's corners are welded by, so that two triangles written with the same corner share it, and two corners a hair apart do not.
    /// </summary>
    internal readonly struct BitKey : IEquatable<BitKey>
    {
        private readonly long _x;
        private readonly long _y;
        private readonly long _z;

        internal BitKey(Vec3 p)
        {
            // Plus nought turns a minus nought into a plain one.
            _x = BitConverter.DoubleToInt64Bits(p.X + 0.0);
            _y = BitConverter.DoubleToInt64Bits(p.Y + 0.0);
            _z = BitConverter.DoubleToInt64Bits(p.Z + 0.0);
        }

        public bool Equals(BitKey other) => _x == other._x && _y == other._y && _z == other._z;

        public override bool Equals(object obj) => obj is BitKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                ulong h = Mix((ulong)_x);
                h = Mix(h * 0x9E3779B97F4A7C15UL + (ulong)_y);
                h = Mix(h * 0x9E3779B97F4A7C15UL + (ulong)_z);
                return (int)(h ^ (h >> 32));
            }
        }

        private static ulong Mix(ulong h)
        {
            unchecked
            {
                h ^= h >> 33;
                h *= 0xFF51AFD7ED558CCDUL;
                h ^= h >> 33;
                h *= 0xC4CEB9FE1A85EC53UL;
                h ^= h >> 33;
                return h;
            }
        }
    }

    /// <summary>
    /// The corners of a mesh read from a file, welded where they are the same to the bit, and the faces that name them.
    /// </summary>
    internal sealed class MeshWelder
    {
        private readonly Dictionary<BitKey, int> _index = new Dictionary<BitKey, int>();

        internal List<Vec3> Points { get; } = new List<Vec3>();

        internal List<int[]> Faces { get; } = new List<int[]>();

        /// <summary>
        /// Gets the index of a corner, making it on first sight; the first spelling of a nought is the one kept.
        /// </summary>
        internal int Vertex(Vec3 p)
        {
            var key = new BitKey(p);

            if (!_index.TryGetValue(key, out int at))
            {
                at = Points.Count;
                Points.Add(p);
                _index.Add(key, at);
            }

            return at;
        }
    }

    /// <summary>
    /// Reads the numbers of the text formats: the way a file writes a double, to the bit.
    /// </summary>
    internal static class ImportNumber
    {
        /// <summary>
        /// Reads a finite number written with a point and, where it has one, an exponent: <c>-0</c> is minus nought, and <c>0.1</c> is the
        /// double nearest a tenth. NaN and the infinities, which the .NET parser accepts as words, are not numbers of a file.
        /// </summary>
        internal static bool TryParse(string text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value))
            {
                return true;
            }

            value = 0.0;
            return false;
        }
    }

    /// <summary>
    /// The data of an item, written as the library's own shapes are in a scene, from numbers read out of a file. Nothing here asks the library
    /// to build a shape, so a number comes through as the file wrote it and a shape the library would refuse is kept and drawn all the same.
    /// </summary>
    internal static class ImportData
    {
        internal static JsonElement Point(Vec3 p, int dimension)
        {
            using (var w = new DataWriter())
            {
                Pt(w, p, dimension);
                return w.Finish();
            }
        }

        internal static JsonElement Line(Vec3 a, Vec3 b, int dimension)
        {
            using (var w = new DataWriter())
            {
                w.BeginObject();
                w.Name("a");
                Pt(w, a, dimension);
                w.Name("b");
                Pt(w, b, dimension);
                w.EndObject();
                return w.Finish();
            }
        }

        /// <summary>
        /// The data of a chain or a loop; bulges, and normals for a chain in space with arcs, are written when given.
        /// </summary>
        internal static JsonElement Chain(IReadOnlyList<Vec3> points, int dimension, IReadOnlyList<double> bulges = null, IReadOnlyList<Vec3> normals = null)
        {
            using (var w = new DataWriter())
            {
                w.BeginObject();
                w.Name("points");
                Loop(w, points, dimension);

                if (bulges != null)
                {
                    w.Numbers("bulges", bulges);
                }

                if (normals != null)
                {
                    w.Name("normals");
                    Loop(w, normals, 3);
                }

                w.EndObject();
                return w.Finish();
            }
        }

        internal static JsonElement Triangle(Vec3 a, Vec3 b, Vec3 c, int dimension) => Chain(new[] { a, b, c }, dimension);

        internal static JsonElement Circle(Vec3 centre, Vec3? normal, double radius, int dimension)
        {
            using (var w = new DataWriter())
            {
                w.BeginObject();
                w.Name("center");
                Pt(w, centre, dimension);

                if (dimension == 3)
                {
                    w.Name("normal");
                    Pt(w, normal ?? new Vec3(0.0, 0.0, 1.0), 3);
                }

                w.Field("radius", radius);
                w.EndObject();
                return w.Finish();
            }
        }

        internal static JsonElement Arc2(Vec3 centre, double radius, double start, double sweep)
        {
            using (var w = new DataWriter())
            {
                w.BeginObject();
                w.Name("center");
                Pt(w, centre, 2);
                w.Field("radius", radius);
                w.Field("start", start);
                w.Field("sweep", sweep);
                w.EndObject();
                return w.Finish();
            }
        }

        internal static JsonElement Ellipse(Vec3 centre, Vec3? normal, Vec3 majorAxis, double majorRadius, double minorRadius, int dimension)
        {
            using (var w = new DataWriter())
            {
                w.BeginObject();
                w.Name("center");
                Pt(w, centre, dimension);

                if (dimension == 3)
                {
                    w.Name("normal");
                    Pt(w, normal ?? new Vec3(0.0, 0.0, 1.0), 3);
                }

                w.Name("majorAxis");
                Pt(w, majorAxis, dimension);
                w.Field("majorRadius", majorRadius);
                w.Field("minorRadius", minorRadius);
                w.EndObject();
                return w.Finish();
            }
        }

        internal static JsonElement Face(IReadOnlyList<Vec3> outer, IReadOnlyList<Vec3[]> holes, int dimension)
        {
            using (var w = new DataWriter())
            {
                w.BeginObject();
                w.Name("outer");
                Loop(w, outer, dimension);
                w.Name("holes");
                w.BeginArray();

                foreach (Vec3[] hole in holes)
                {
                    Loop(w, hole, dimension);
                }

                w.EndArray();
                w.EndObject();
                return w.Finish();
            }
        }

        internal static JsonElement Mesh(IReadOnlyList<Vec3> points, IReadOnlyList<int[]> faces, int dimension)
        {
            using (var w = new DataWriter())
            {
                bool triangles = true;

                foreach (int[] face in faces)
                {
                    triangles &= face.Length == 3;
                }

                w.BeginObject();
                w.Name("kind");
                w.Text(triangles ? "Triangles" : "Polygons");
                w.Name("points");
                Loop(w, points, dimension);
                w.Name("faces");
                w.BeginArray();

                foreach (int[] face in faces)
                {
                    w.BeginArray();

                    foreach (int index in face)
                    {
                        w.Integer(index);
                    }

                    w.EndArray();
                }

                w.EndArray();
                w.EndObject();
                return w.Finish();
            }
        }

        internal static JsonElement Text(Vec3 at, string text, double height)
        {
            using (var w = new DataWriter())
            {
                w.BeginObject();
                w.Name("at");
                Pt(w, at, 3);
                w.Name("text");
                w.Text(text ?? string.Empty);
                w.Field("height", height);
                w.EndObject();
                return w.Finish();
            }
        }

        private static void Pt(DataWriter w, Vec3 p, int dimension)
        {
            if (dimension == 2)
            {
                w.Point(p.X, p.Y);
            }
            else
            {
                w.Point(p.X, p.Y, p.Z);
            }
        }

        private static void Loop(DataWriter w, IReadOnlyList<Vec3> points, int dimension)
        {
            w.BeginArray();

            for (int i = 0; i < points.Count; i++)
            {
                Pt(w, points[i], dimension);
            }

            w.EndArray();
        }
    }

    /// <summary>
    /// Builds the scene a reader hands back: the layers, the items named and counted the way a script's are, and the warnings, each said once
    /// with how many times it happened.
    /// </summary>
    internal sealed class ImportBuilder
    {
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _warned = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> _warnOrder = new List<string>();

        internal ImportBuilder(string title)
        {
            Scene = new Scene { Title = title };
        }

        internal Scene Scene { get; }

        /// <summary>
        /// Makes a layer, or sets the colour and visibility of one that exists; a layer made without a colour takes the next of the cycle.
        /// </summary>
        internal void Layer(string name, string color = null, bool visible = true)
        {
            Layer layer = Scene.FindLayer(name);

            if (layer == null)
            {
                layer = new Layer { Name = name, Color = color ?? SceneColor.FromCycle(Scene.Layers.Count) };
                Scene.Layers.Add(layer);
            }
            else if (color != null)
            {
                layer.Color = color;
            }

            layer.Visible = visible;
        }

        /// <summary>
        /// Adds an item to a layer, made if it is new. A name of null is made up from the kind and its number, and marked as made up, as a script's are.
        /// </summary>
        internal SceneItem Add(string kind, JsonElement data, string layer, string name, string color, string source)
        {
            if (Scene.FindLayer(layer) == null)
            {
                Layer(layer);
            }

            _counts.TryGetValue(kind, out int count);
            _counts[kind] = ++count;

            var item = new SceneItem
            {
                Name = name ?? kind + " " + count.ToString(CultureInfo.InvariantCulture),
                AutoName = name == null,
                Layer = layer,
                Color = color,
                Kind = kind,
                Data = data,
            };

            if (source != null)
            {
                item.Properties["source"] = source;
            }

            Scene.Items.Add(item);
            return item;
        }

        /// <summary>
        /// Says something the reader left out or could not take as it was. The same words said again are counted, not repeated.
        /// </summary>
        internal void Warn(string text)
        {
            if (_warned.TryGetValue(text, out int times))
            {
                _warned[text] = times + 1;
            }
            else
            {
                _warned.Add(text, 1);
                _warnOrder.Add(text);
            }
        }

        /// <summary>
        /// Puts the warnings into the scene, in the order they were first said, and gives the scene back.
        /// </summary>
        internal Scene Finish()
        {
            foreach (string text in _warnOrder)
            {
                int times = _warned[text];
                Scene.LoadWarnings.Add(times == 1 ? text : text + " (" + times.ToString(CultureInfo.InvariantCulture) + " times)");
            }

            _warnOrder.Clear();
            _warned.Clear();
            return Scene;
        }
    }
}

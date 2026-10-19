using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using GeometryHelper.Geometry;
using SceneLayer = GeometryHelper.Viewer.Layer;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Builds a <see cref="Scene"/>: the way a script, a test or an audit harness hands shapes to the viewer. Add the
    /// shapes, each on a layer, and save the scene or take its JSON.
    /// </summary>
    /// <example>
    /// <code>
    /// var s = new SceneWriter("item 6 star 2:116");
    /// s.Layer("source", "#202020").Layer("round", "#1f77b4");
    /// s.Add(loop, layer: "source", name: "star 2:116", props: new { distance = -0.3, join = "Round" });
    /// s.Add(offsets, layer: "round");
    /// s.Save(@"C:\scenes\star.ghscene.json");
    /// </code>
    /// </example>
    public sealed partial class SceneWriter
    {
        private const string DefaultLayer = "default";

        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// Initializes a writer with an empty scene.
        /// </summary>
        /// <param name="title">The title shown in the window; none when null.</param>
        /// <remarks>
        /// The scene records the tolerance in force on this thread, all four thresholds, so that what is built under one tolerance
        /// is rebuilt and measured under it.
        /// </remarks>
        public SceneWriter(string title = null)
        {
            Scene = new Scene { Title = title };
            Scene.SetTolerance(Tolerance.Global);
        }

        /// <summary>
        /// Initializes a writer over a scene that exists, such as one read from a file, to add to it or to write it again.
        /// </summary>
        /// <param name="scene">The scene; the writer adds to it and does not copy it.</param>
        /// <exception cref="ArgumentNullException">Thrown when the scene is null.</exception>
        public SceneWriter(Scene scene)
        {
            Scene = scene ?? throw new ArgumentNullException(nameof(scene));

            foreach (SceneItem item in scene.Items)
            {
                Count(item.Kind);
            }
        }

        /// <summary>
        /// Gets or sets the token that stops the writer: once it is cancelled every call that adds to the scene throws an
        /// <see cref="OperationCanceledException"/>, which is how a script is stopped in a loop that adds shapes.
        /// </summary>
        public CancellationToken Cancellation { get; set; }

        /// <summary>
        /// Gets the scene being built, which stays the writer's: items added later appear in it.
        /// </summary>
        public Scene Scene { get; }

        /// <summary>
        /// Makes a layer, or sets the colour and visibility of one that exists. A layer an item names that was never made is
        /// made with the next colour of the cycle.
        /// </summary>
        /// <param name="name">The name of the layer.</param>
        /// <param name="color">The colour of its items as #RRGGBB; the next colour of the cycle for a new layer, and the colour it has for one that exists, when null.</param>
        /// <param name="visible">Whether the layer is shown when the scene opens.</param>
        /// <returns>This writer, for the next call.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour is not #RRGGBB.</exception>
        public SceneWriter Layer(string name, string color = null, bool visible = true)
        {
            Cancellation.ThrowIfCancellationRequested();

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            string normalized = SceneColor.Normalize(color);
            SceneLayer layer = Scene.FindLayer(name);

            if (layer == null)
            {
                layer = new SceneLayer { Name = name, Color = normalized ?? SceneColor.FromCycle(Scene.Layers.Count) };
                Scene.Layers.Add(layer);
            }
            else if (normalized != null)
            {
                layer.Color = normalized;
            }

            layer.Visible = visible;
            return this;
        }

        /// <summary>
        /// Adds a shape of any kind the viewer draws, or every shape of a sequence of them.
        /// </summary>
        /// <param name="geometry">A library shape, or an array or list of them; a text is not a sequence.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given. A sequence is named the name and the number of each.</param>
        /// <param name="color">The colour of the items as #RRGGBB; their layer's when none is given.</param>
        /// <param name="props">Properties to show with the items: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the shape is of a type the viewer cannot draw; the message names the type.</exception>
        public SceneWriter Add(object geometry, string layer = null, string name = null, string color = null, object props = null)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            if (geometry is IEnumerable sequence && !(geometry is string))
            {
                List<object> shapes = sequence.Cast<object>().ToList();

                for (int i = 0; i < shapes.Count; i++)
                {
                    string each = name == null || shapes.Count == 1 ? name : name + " " + (i + 1).ToString(CultureInfo.InvariantCulture);
                    Add(shapes[i], layer, each, color, props);
                }

                return this;
            }

            return Put(geometry, layer, name, color, props);
        }

        /// <summary>
        /// Adds a label at a point in space.
        /// </summary>
        /// <param name="at">Where the label is.</param>
        /// <param name="text">What it says.</param>
        /// <param name="height">The height of the letters in millimetres; 0 draws them at a size that suits the screen.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="color">The colour as #RRGGBB; the layer's when none is given.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour is not #RRGGBB.</exception>
        public SceneWriter Text(GeoPoint3 at, string text, double height = 0.0, string layer = null, string color = null)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return Append(SceneKinds.Text, GeometryEncoder.EncodeText(at, text, height), layer, null, color, null);
        }

        /// <summary>
        /// Gets the scene as text, one line to a layer and one to an item, numbers as the 17 digits that read back as the
        /// same double.
        /// </summary>
        public string ToJson() => SceneFile.Write(Scene);

        /// <summary>
        /// Writes the scene to a file as UTF-8 with no byte order mark and LF line ends, replacing the file there.
        /// </summary>
        /// <param name="path">The path of the file; the folder must exist.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
        public void Save(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            File.WriteAllText(path, ToJson(), new UTF8Encoding(false));
        }

        private SceneWriter Put(object geometry, string layer, string name, string color, object props)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            string kind = GeometryEncoder.KindOf(geometry);

            if (kind == null)
            {
                throw new ArgumentException("The viewer cannot draw a " + geometry.GetType().FullName + ".", nameof(geometry));
            }

            return Append(kind, GeometryEncoder.Encode(geometry), layer, name, color, props);
        }

        private SceneWriter Append(string kind, System.Text.Json.JsonElement data, string layer, string name, string color, object props)
        {
            Cancellation.ThrowIfCancellationRequested();
            string normalized = SceneColor.Normalize(color);
            string layerName = layer ?? DefaultLayer;

            if (Scene.FindLayer(layerName) == null)
            {
                Scene.Layers.Add(new SceneLayer { Name = layerName, Color = SceneColor.FromCycle(Scene.Layers.Count) });
            }

            var item = new SceneItem
            {
                Name = name ?? kind + " " + Count(kind).ToString(CultureInfo.InvariantCulture),
                Layer = layerName,
                Color = normalized,
                Kind = kind,
                Data = data,
            };

            if (name != null)
            {
                Count(kind);
            }

            foreach (KeyValuePair<string, string> pair in Properties(props))
            {
                item.Properties[pair.Key] = pair.Value;
            }

            Scene.Items.Add(item);
            return this;
        }

        /// <summary>
        /// Counts an item of a kind and gives its number among the items of that kind, so that naming one is not a pass over
        /// all the items.
        /// </summary>
        private int Count(string kind)
        {
            _counts.TryGetValue(kind, out int count);
            _counts[kind] = ++count;
            return count;
        }

        private static IEnumerable<KeyValuePair<string, string>> Properties(object props)
        {
            if (props == null)
            {
                yield break;
            }

            if (props is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    yield return new KeyValuePair<string, string>(Convert.ToString(entry.Key, CultureInfo.InvariantCulture), AsText(entry.Value));
                }

                yield break;
            }

            foreach (PropertyInfo property in props.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length == 0 && property.CanRead)
                {
                    yield return new KeyValuePair<string, string>(property.Name, AsText(property.GetValue(props)));
                }
            }
        }

        private static string AsText(object value)
        {
            switch (value)
            {
                case null: return string.Empty;
                case double d: return d.ToString("G17", CultureInfo.InvariantCulture);
                case float f: return ((double)f).ToString("G17", CultureInfo.InvariantCulture);
                case bool b: return b ? "true" : "false";
                case IFormattable formattable: return formattable.ToString(null, CultureInfo.InvariantCulture);
                default: return value.ToString();
            }
        }
    }
}

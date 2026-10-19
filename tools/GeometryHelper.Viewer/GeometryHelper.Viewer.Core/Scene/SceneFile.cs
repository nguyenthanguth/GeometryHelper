using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// The text form of a scene: a header, then one line to a layer and one line to an item, so that two scenes differ in
    /// a text comparison only where they differ.
    /// </summary>
    internal static class SceneFile
    {
        internal const string Format = "ghscene";

        internal const int Version = 1;

        internal static string Write(Scene scene)
        {
            var text = new StringBuilder();
            text.Append("{\n");
            text.Append("\"format\":\"").Append(Format).Append("\",\n");
            text.Append("\"version\":").Append(Version.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            text.Append("\"title\":").Append(Compact(w => w.WriteStringValue(scene.Title ?? string.Empty))).Append(",\n");
            text.Append("\"equalPoint\":").Append(Compact(w => WriteDouble(w, scene.EqualPoint))).Append(",\n");
            text.Append("\"equalVector\":").Append(Compact(w => WriteDouble(w, scene.EqualVector))).Append(",\n");
            text.Append("\"equalAngle\":").Append(Compact(w => WriteDouble(w, scene.EqualAngleRad))).Append(",\n");
            text.Append("\"equalPlanar\":").Append(Compact(w => WriteDouble(w, scene.EqualPlanar))).Append(",\n");

            text.Append("\"layers\":[\n");
            text.Append(string.Join(",\n", scene.Layers.Select(layer => Compact(w =>
            {
                w.WriteStartObject();
                w.WriteString("name", layer.Name);
                w.WriteString("color", layer.Color);
                w.WriteBoolean("visible", layer.Visible);
                w.WriteEndObject();
            }))));
            text.Append("\n],\n");

            text.Append("\"items\":[\n");
            text.Append(string.Join(",\n", scene.Items.Select(item => Compact(w =>
            {
                w.WriteStartObject();
                w.WriteString("name", item.Name);
                w.WriteString("layer", item.Layer);

                if (item.Color != null)
                {
                    w.WriteString("color", item.Color);
                }

                w.WriteString("kind", item.Kind);
                w.WritePropertyName("data");
                item.Data.WriteTo(w);

                if (item.Properties.Count > 0)
                {
                    w.WriteStartObject("properties");

                    foreach (KeyValuePair<string, string> pair in item.Properties.OrderBy(p => p.Key, StringComparer.Ordinal))
                    {
                        w.WriteString(pair.Key, pair.Value);
                    }

                    w.WriteEndObject();
                }

                w.WriteEndObject();
            }))));
            text.Append("\n]\n}\n");

            return text.ToString();
        }

        internal static Scene Read(string json)
        {
            var options = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 256 };

            JsonDocument document;

            try
            {
                document = JsonDocument.Parse(json, options);
            }
            catch (JsonException e)
            {
                throw new FormatException("The scene is not valid JSON: " + e.Message, e);
            }

            using (document)
            {
                try
                {
                    return ReadScene(document.RootElement);
                }
                catch (Exception e) when (e is InvalidOperationException || e is InvalidCastException || e is OverflowException || e is KeyNotFoundException)
                {
                    throw new FormatException("The scene is malformed: " + e.Message, e);
                }
            }
        }

        private static Scene ReadScene(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("A scene is a JSON object.");
            }

            if (root.TryGetProperty("format", out JsonElement format) && format.ValueKind == JsonValueKind.String && format.GetString() != Format)
            {
                throw new FormatException("This is not a " + Format + " scene; its format is '" + format.GetString() + "'.");
            }

            if (root.TryGetProperty("version", out JsonElement version) && version.ValueKind == JsonValueKind.Number && version.GetInt32() > Version)
            {
                throw new FormatException("The scene is version " + version.GetInt32() + "; this viewer reads up to " + Version + ".");
            }

            var scene = new Scene();

            if (root.TryGetProperty("title", out JsonElement title) && title.ValueKind == JsonValueKind.String)
            {
                scene.Title = title.GetString();
            }

            if (root.TryGetProperty("equalPoint", out JsonElement equalPoint))
            {
                scene.EqualPoint = DataJson.Number(equalPoint);
            }

            // A scene written before the other thresholds were kept has the planar one follow the point one.
            scene.EqualPlanar = scene.EqualPoint;

            if (root.TryGetProperty("equalVector", out JsonElement equalVector))
            {
                scene.EqualVector = DataJson.Number(equalVector);
            }

            if (root.TryGetProperty("equalAngle", out JsonElement equalAngle))
            {
                scene.EqualAngleRad = DataJson.Number(equalAngle);
            }

            if (root.TryGetProperty("equalPlanar", out JsonElement equalPlanar))
            {
                scene.EqualPlanar = DataJson.Number(equalPlanar);
            }

            scene.ValidateTolerance();

            if (root.TryGetProperty("layers", out JsonElement layers) && layers.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement element in layers.EnumerateArray())
                {
                    scene.Layers.Add(new Layer
                    {
                        Name = String(element, "name") ?? string.Empty,
                        Color = LayerColor(String(element, "color"), scene.Layers.Count),
                        Visible = !element.TryGetProperty("visible", out JsonElement visible) || visible.ValueKind != JsonValueKind.False,
                    });
                }
            }

            if (root.TryGetProperty("items", out JsonElement items) && items.ValueKind == JsonValueKind.Array)
            {
                int index = 0;

                foreach (JsonElement element in items.EnumerateArray())
                {
                    string kind = String(element, "kind");

                    if (string.IsNullOrEmpty(kind))
                    {
                        throw new FormatException("Item " + index + " of the scene has no kind.");
                    }

                    var item = new SceneItem
                    {
                        Name = String(element, "name"),
                        Layer = String(element, "layer") ?? string.Empty,
                        Color = ItemColor(String(element, "color")),
                        Kind = kind,
                        Data = element.TryGetProperty("data", out JsonElement data) ? data.Clone() : NullData(),
                    };

                    if (element.TryGetProperty("properties", out JsonElement properties) && properties.ValueKind == JsonValueKind.Object)
                    {
                        foreach (JsonProperty property in properties.EnumerateObject())
                        {
                            item.Properties[property.Name] = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.GetRawText();
                        }
                    }

                    if (scene.FindLayer(item.Layer) == null)
                    {
                        scene.Layers.Add(new Layer { Name = item.Layer, Color = SceneColor.FromCycle(scene.Layers.Count) });
                    }

                    scene.Items.Add(item);
                    index++;
                }
            }

            return scene;
        }

        private static string String(JsonElement element, string name)
            => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static JsonElement NullData()
        {
            using (JsonDocument document = JsonDocument.Parse("null"))
            {
                return document.RootElement.Clone();
            }
        }

        private static string ItemColor(string text)
            => SceneColor.TryParse(text, out _, out _, out _) ? SceneColor.Normalize(text) : null;

        private static string LayerColor(string text, int index)
            => ItemColor(text) ?? SceneColor.FromCycle(index);

        private static void WriteDouble(Utf8JsonWriter writer, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                writer.WriteRawValue(value.ToString("G17", CultureInfo.InvariantCulture), true);
            }
        }

        private static string Compact(Action<Utf8JsonWriter> write)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
                {
                    write(writer);
                }

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}

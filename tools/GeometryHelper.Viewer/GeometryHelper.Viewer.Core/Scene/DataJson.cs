using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Writes the data of an item as JSON text with every double as the 17 significant digits that read back as the same
    /// value. A number that JSON cannot carry, NaN or an infinity, is written as the text "NaN", "Infinity" or "-Infinity",
    /// which <see cref="DataJson"/> reads back, so a scene can show the very shape that broke a computation.
    /// </summary>
    internal sealed class DataWriter : IDisposable
    {
        private readonly MemoryStream _stream = new MemoryStream();
        private readonly Utf8JsonWriter _json;

        internal DataWriter()
        {
            _json = new Utf8JsonWriter(_stream, new JsonWriterOptions { Indented = false });
        }

        internal void Number(double value)
        {
            if (double.IsNaN(value))
            {
                _json.WriteStringValue("NaN");
            }
            else if (double.IsPositiveInfinity(value))
            {
                _json.WriteStringValue("Infinity");
            }
            else if (double.IsNegativeInfinity(value))
            {
                _json.WriteStringValue("-Infinity");
            }
            else
            {
                _json.WriteRawValue(value.ToString("G17", CultureInfo.InvariantCulture), true);
            }
        }

        internal void Integer(int value) => _json.WriteNumberValue(value);

        internal void Text(string value) => _json.WriteStringValue(value);

        internal void Flag(bool value) => _json.WriteBooleanValue(value);

        internal void Name(string name) => _json.WritePropertyName(name);

        internal void BeginObject() => _json.WriteStartObject();

        internal void EndObject() => _json.WriteEndObject();

        internal void BeginArray() => _json.WriteStartArray();

        internal void EndArray() => _json.WriteEndArray();

        internal void Point(double x, double y)
        {
            _json.WriteStartArray();
            Number(x);
            Number(y);
            _json.WriteEndArray();
        }

        internal void Point(double x, double y, double z)
        {
            _json.WriteStartArray();
            Number(x);
            Number(y);
            Number(z);
            _json.WriteEndArray();
        }

        internal void Field(string name, double value)
        {
            Name(name);
            Number(value);
        }

        internal void Field(string name, double x, double y)
        {
            Name(name);
            Point(x, y);
        }

        internal void Field(string name, double x, double y, double z)
        {
            Name(name);
            Point(x, y, z);
        }

        internal void Numbers(string name, IEnumerable<double> values)
        {
            Name(name);
            _json.WriteStartArray();

            foreach (double value in values)
            {
                Number(value);
            }

            _json.WriteEndArray();
        }

        /// <summary>
        /// Ends the writing and gives what was written as a standalone element.
        /// </summary>
        internal JsonElement Finish()
        {
            _json.Flush();

            using (JsonDocument document = JsonDocument.Parse(_stream.ToArray()))
            {
                return document.RootElement.Clone();
            }
        }

        public void Dispose()
        {
            _json.Dispose();
            _stream.Dispose();
        }
    }

    /// <summary>
    /// Reads the data of an item back as numbers.
    /// </summary>
    internal static class DataJson
    {
        internal static double Number(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.GetDouble();
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                switch (element.GetString())
                {
                    case "NaN": return double.NaN;
                    case "Infinity": return double.PositiveInfinity;
                    case "-Infinity": return double.NegativeInfinity;
                }
            }

            throw new FormatException("A number was expected where the scene has " + element.ValueKind + ".");
        }

        /// <summary>
        /// Reads a point of two or three numbers; a point of two sits at z = 0.
        /// </summary>
        internal static Vec3 Point(JsonElement element, int dimension)
        {
            if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != dimension)
            {
                throw new FormatException("A point of " + dimension + " numbers was expected.");
            }

            double x = Number(element[0]);
            double y = Number(element[1]);
            return new Vec3(x, y, dimension == 3 ? Number(element[2]) : 0.0);
        }

        internal static Vec3[] Points(JsonElement element, int dimension)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException("A list of points was expected.");
            }

            var points = new Vec3[element.GetArrayLength()];
            int index = 0;

            foreach (JsonElement item in element.EnumerateArray())
            {
                points[index++] = Point(item, dimension);
            }

            return points;
        }

        internal static double[] Numbers(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException("A list of numbers was expected.");
            }

            var numbers = new double[element.GetArrayLength()];
            int index = 0;

            foreach (JsonElement item in element.EnumerateArray())
            {
                numbers[index++] = Number(item);
            }

            return numbers;
        }

        internal static JsonElement Get(JsonElement element, string name)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value))
            {
                return value;
            }

            throw new FormatException("The data lacks \"" + name + "\".");
        }

        internal static bool TryGet(JsonElement element, string name, out JsonElement value)
        {
            value = default(JsonElement);
            return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value);
        }

        internal static double Number(JsonElement element, string name) => Number(Get(element, name));

        internal static Vec3 Point(JsonElement element, string name, int dimension) => Point(Get(element, name), dimension);

        internal static Vec3[] Points(JsonElement element, string name, int dimension) => Points(Get(element, name), dimension);

        /// <summary>
        /// Reads the bulges, or nought for each of <paramref name="count"/> edges where the data has none.
        /// </summary>
        internal static double[] Bulges(JsonElement element, int count)
        {
            if (!TryGet(element, "bulges", out JsonElement list))
            {
                return new double[count];
            }

            double[] bulges = Numbers(list);

            if (bulges.Length >= count)
            {
                return bulges;
            }

            var padded = new double[count];
            Array.Copy(bulges, padded, bulges.Length);
            return padded;
        }

        internal static IEnumerable<JsonElement> List(JsonElement element, string name)
        {
            JsonElement list = Get(element, name);

            if (list.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException("\"" + name + "\" should be a list.");
            }

            return list.EnumerateArray();
        }
    }
}

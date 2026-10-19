using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// The numbers one build of the library gives for an item: how many vertices it keeps, its length, its area and its volume, where the
    /// kind has them.
    /// </summary>
    public sealed class ItemNumbers
    {
        /// <summary>
        /// Gets or sets the vertex count the library gives, or null where the kind has none.
        /// </summary>
        public int? Vertices { get; set; }

        /// <summary>
        /// Gets or sets the length (the perimeter of a loop), or null.
        /// </summary>
        public double? Length { get; set; }

        /// <summary>
        /// Gets or sets the area, or null.
        /// </summary>
        public double? Area { get; set; }

        /// <summary>
        /// Gets or sets the volume, or null.
        /// </summary>
        public double? Volume { get; set; }

        /// <summary>
        /// Gets or sets why the build could not measure the item, or null.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Gets whether the build gave no number at all for the item.
        /// </summary>
        public bool IsEmpty => !Vertices.HasValue && !Length.HasValue && !Area.HasValue && !Volume.HasValue;
    }

    /// <summary>
    /// Puts into a scene the numbers its own build of the library gives for each item, as properties named <c>measure.vertices</c>,
    /// <c>measure.length</c>, <c>measure.area</c>, <c>measure.volume</c>, <c>measure.surfaceArea</c> and <c>measure.closed</c>, every
    /// number as 17 digits, and <c>measure.note</c> where the shape was rebuilt and measured at 1E-12. The runner does it when asked, so that the comparison of two builds sets each build's numbers side by side
    /// without running the other build's library in the viewer.
    /// </summary>
    public static class RunnerMeasures
    {
        /// <summary>
        /// The start of the name of every property this class writes.
        /// </summary>
        public const string Prefix = "measure.";

        /// <summary>
        /// Measures every item of a scene with the library loaded now, at the tolerance of the scene, and sets the properties. An item the
        /// library cannot measure gets <c>measure.error</c> with the reason instead. Properties already there are replaced.
        /// </summary>
        /// <param name="scene">The scene.</param>
        /// <exception cref="ArgumentNullException">Thrown when the scene is null.</exception>
        public static void Add(Scene scene)
        {
            if (scene == null)
            {
                throw new ArgumentNullException(nameof(scene));
            }

            foreach (SceneItem item in scene.Items)
            {
                try
                {
                    foreach (KeyValuePair<string, string> number in ItemMeasures.Numbers(item, scene))
                    {
                        string key = KeyOf(number.Key);

                        if (key != null)
                        {
                            item.Properties[Prefix + key] = number.Value;
                        }
                    }
                }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    // A build older than the viewer may lack a member the viewer measures with; the item is told so and the others carry on.
                    item.Properties[Prefix + "error"] = e.GetType().Name + ": " + e.Message;
                }
            }
        }

        /// <summary>
        /// Reads the numbers <see cref="Add"/> put on an item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>The numbers; empty where the item has none.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the item is null.</exception>
        public static ItemNumbers Read(SceneItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            var numbers = new ItemNumbers();

            if (item.Properties.TryGetValue(Prefix + "vertices", out string vertices) && int.TryParse(vertices, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
            {
                numbers.Vertices = count;
            }

            numbers.Length = Double(item, "length");
            numbers.Area = Double(item, "area");
            numbers.Volume = Double(item, "volume");
            numbers.Error = item.Properties.TryGetValue(Prefix + "error", out string error) ? error : null;
            return numbers;
        }

        private static double? Double(SceneItem item, string name)
            => item.Properties.TryGetValue(Prefix + name, out string text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : (double?)null;

        private static string KeyOf(string label)
        {
            switch (label)
            {
                case "Vertices": return "vertices";
                case "Length": return "length";
                case "Area": return "area";
                case "Volume": return "volume";
                case "Surface area": return "surfaceArea";
                case "Closed": return "closed";
                case "Library": return "note";
                default: return null;
            }
        }
    }
}

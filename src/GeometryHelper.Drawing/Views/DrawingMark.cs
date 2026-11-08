using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// The mark of one part in one view: its text, the point of the part its leader starts from, and where its text stands,
    /// in view millimetres at 1:1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The leader starts at the middle of the longest visible line of the part in the view, which lies on the part whatever
    /// its outline, and the text stands up and to the right of it by the offset of <see cref="MarkSettings"/>: the end
    /// plate P1 of a beam, in a view at 1:20 with an offset of 8, has its text 160 up and 160 to the right of the middle of
    /// its longest line. A mark moved by hand keeps the move as a hand edit, so that after an update it stands as far from
    /// its part as it stood before.
    /// </para>
    /// <para>
    /// This is the simple mark the drawing needs to be edited and updated; placing marks clear of lines and of one another
    /// through <c>Arranging</c> is G4's. A mark does not change once made.
    /// </para>
    /// </remarks>
    public sealed class DrawingMark
    {
        /// <summary>The name of the layer marks are written on.</summary>
        public const string Layer = "Mark";

        internal DrawingMark(string id, string viewId, string objectId, string text, GeoPoint2 anchor, GeoPoint2 textPosition, double textHeight, GeoPoint3 modelPoint)
        {
            Id = id;
            ViewId = viewId;
            ObjectId = objectId;
            Text = text;
            Anchor = anchor;
            TextPosition = textPosition;
            TextHeight = textHeight;
            ModelPoint = modelPoint;
            Links = new DrawingLinks(new[] { objectId }, new[] { new DrawingLinkPoint3(objectId, modelPoint) });
        }

        /// <summary>Gets the id of the mark, unique in its drawing: the view's id, /M- and the part's id, as V-FRONT/M-P-1.</summary>
        public string Id { get; }

        /// <summary>Gets the id of the view the mark is in.</summary>
        public string ViewId { get; }

        /// <summary>Gets the id of the part marked.</summary>
        public string ObjectId { get; }

        /// <summary>Gets the text: the part's position mark.</summary>
        public string Text { get; }

        /// <summary>Gets the point the leader starts from, on the part, in view millimetres.</summary>
        public GeoPoint2 Anchor { get; }

        /// <summary>Gets the lower left corner of the text as the drawing places it, before any move by hand, in view millimetres.</summary>
        public GeoPoint2 TextPosition { get; }

        /// <summary>Gets the height of the text in the view, in view millimetres: the height on paper times the view's scale.</summary>
        public double TextHeight { get; }

        /// <summary>Gets the point of the model the mark is tied to: the middle of the box of its part.</summary>
        public GeoPoint3 ModelPoint { get; }

        /// <summary>Gets what the mark is tied to in the model: its part, and the middle of the part's box.</summary>
        public DrawingLinks Links { get; }

        /// <summary>Gets the box the text is taken to fill, in view millimetres: each character 0.8 of its height wide.</summary>
        public GeoRectangle2 TextBox => new GeoRectangle2(TextPosition.X, TextPosition.Y, Math.Max(1, Text.Length) * 0.8 * TextHeight, TextHeight);

        /// <inheritdoc/>
        public override string ToString() => $"Mark {Id} {Text}";

        /// <summary>The mark as values of the drawing document.</summary>
        internal List<KeyValuePair<string, string>> ToValues() => new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Object", ObjectId),
            new KeyValuePair<string, string>("Text", Text),
            new KeyValuePair<string, string>("AnchorX", DrawingSettings.Write(Anchor.X)),
            new KeyValuePair<string, string>("AnchorY", DrawingSettings.Write(Anchor.Y)),
            new KeyValuePair<string, string>("TextX", DrawingSettings.Write(TextPosition.X)),
            new KeyValuePair<string, string>("TextY", DrawingSettings.Write(TextPosition.Y)),
            new KeyValuePair<string, string>("TextHeight", DrawingSettings.Write(TextHeight)),
        };

        /// <summary>The mark read back from an element of the drawing document; null where its values do not read.</summary>
        internal static DrawingMark FromElement(DrawingElement element)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, string> value in element.Values)
            {
                values[value.Key] = value.Value;
            }

            if (!values.TryGetValue("Object", out string objectId) || string.IsNullOrEmpty(objectId)
                || !values.TryGetValue("Text", out string text) || text == null
                || !Number(values, "AnchorX", out double ax) || !Number(values, "AnchorY", out double ay)
                || !Number(values, "TextX", out double tx) || !Number(values, "TextY", out double ty)
                || !Number(values, "TextHeight", out double height))
            {
                return null;
            }

            GeoPoint3 model = element.Links.Points.Count > 0 ? element.Links.Points[0].Point : new GeoPoint3(0.0, 0.0, 0.0);
            return new DrawingMark(element.Id, element.ViewId, objectId, text, new GeoPoint2(ax, ay), new GeoPoint2(tx, ty), height, model);
        }

        private static bool Number(Dictionary<string, string> values, string key, out double value)
        {
            value = 0.0;
            return values.TryGetValue(key, out string written) && written != null && double.TryParse(written, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}

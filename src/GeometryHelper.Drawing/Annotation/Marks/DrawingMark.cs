using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// The mark of an object in one view, placed by <see cref="Marker"/>: its text, the box the text stands in, and the
    /// leader from the text to a point on the object's visible outline, in view millimetres at 1:1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The end plate P1 of a beam, seen at 1:10 with a text 3.5 high on paper, gets a text 35 high in the view, its box
    /// as wide as that for each character, 70 by 35, standing clear of the view's dimensions and the other marks; its
    /// leader runs from the side of the box nearest the plate to the point of the plate's visible lines nearest the text.
    /// The four bolts of a base plate get one mark, "4 x M20 x 80", its leader ending at the centre of one of them.
    /// </para>
    /// <para>
    /// In a drawing (<see cref="DrawingBuilder"/>) a mark is an element of the drawing document known by its view's id, / and
    /// its own, V-FRONT/M-P-1, which its entities are tagged with. A mark dragged by hand keeps the move as a hand edit: its
    /// text and the start of its leader stand that far from where the drawing puts them, after an update too, and its arrow
    /// stays on the object.
    /// </para>
    /// <para>A mark does not change once made.</para>
    /// </remarks>
    public sealed class DrawingMark
    {
        /// <summary>The name of the layer marks are written on.</summary>
        public const string Layer = "Mark";

        private readonly GeoPoint2[] _leader;
        private readonly string[] _objectIds;

        /// <summary>
        /// Initializes a mark placed in its view.
        /// </summary>
        internal DrawingMark(
            string id,
            MarkKind kind,
            string objectId,
            string[] objectIds,
            string text,
            GeoRectangle2 textBox,
            GeoPoint2[] leader,
            double textHeight,
            double scale,
            GeoPoint3 modelPoint
        )
        {
            Id = id;
            Kind = kind;
            ObjectId = objectId;
            _objectIds = objectIds;
            Text = text;
            TextBox = textBox;
            _leader = leader;
            TextHeight = textHeight;
            Scale = scale;
            Links = new DrawingLinks(objectIds, new[] { new DrawingLinkPoint3(objectId, modelPoint) });
        }

        /// <summary>
        /// Gets the id of the mark, unique among the marks of its view: M- and the id of the object marked, M-P-1; for a bolt
        /// group, M- and the lowest id of its bolts.
        /// </summary>
        public string Id { get; }

        /// <summary>Gets what the mark marks: a part, an assembly or a group of bolts.</summary>
        public MarkKind Kind { get; }

        /// <summary>Gets the id of the object the leader ends on: the part, or the bolt of the group nearest the text.</summary>
        public string ObjectId { get; }

        /// <summary>Gets the ids of every object the mark stands for, in ordinal order: the part, or every bolt of the group.</summary>
        public IReadOnlyList<string> ObjectIds => _objectIds;

        /// <summary>Gets the text, made from the fields of <see cref="MarkContent"/>: "P1", or "4 x M20 x 80".</summary>
        public string Text { get; }

        /// <summary>
        /// Gets the box the text is taken to fill, square to the view's axes, in view millimetres: each character as wide as the
        /// text is high, as the presenter draws it.
        /// </summary>
        public GeoRectangle2 TextBox { get; }

        /// <summary>
        /// Gets the leader, in view millimetres: its first point on the outline of the text box, its last
        /// <see cref="Anchor"/>.
        /// </summary>
        public IReadOnlyList<GeoPoint2> Leader => _leader;

        /// <summary>
        /// Gets the point the leader ends at, on the object: on one of its visible lines, or for a bolt seen along its axis,
        /// its rim seen whole, at its centre.
        /// </summary>
        public GeoPoint2 Anchor => _leader[_leader.Length - 1];

        /// <summary>Gets the height of the text in the view, in view millimetres: the height on paper times <see cref="Scale"/>.</summary>
        public double TextHeight { get; }

        /// <summary>Gets the scale the mark was laid out for: 20 for 1:20.</summary>
        public double Scale { get; }

        /// <summary>
        /// Gets what the mark is tied to in the model: the objects it stands for, and one point, the middle of the box of the
        /// part's body, or the middle of the axis of the bolt its leader ends at.
        /// </summary>
        public DrawingLinks Links { get; }

        /// <summary>
        /// Writes the mark into the model space of a drawing: its leader as a LINE with a closed filled arrow at the object,
        /// and its text as an MTEXT, its middle on the middle of the text box.
        /// </summary>
        /// <param name="database">The drawing to write into; what it holds already is left as it is.</param>
        /// <param name="placement">Where the view is drawn on the sheet, and at what scale.</param>
        /// <remarks>
        /// The library makes no multileader style in a drawing it starts, so a mark is a LINE, a SOLID and an MTEXT rather
        /// than an MLEADER, as a dimension's block is drawn. Each is on the layer <see cref="Layer"/>, made in colour 2 where
        /// the drawing has none, and tagged with <see cref="DrawingEntityTags"/>: the mark's id and the placement's view id.
        /// The arrow is half as long as the text is high, and left out of a leader shorter than two arrows: a mark of text
        /// 3.5 high at 1:10, set 4 off its part on paper, has a leader of about 40 in the view and an arrow 17.5 long.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public void WriteTo(DxfDatabase database, ViewPlacement2 placement)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            Write(database, placement, Id);
        }

        /// <inheritdoc/>
        public override string ToString() => $"{Kind} mark {Id} {Text}";

        /// <summary>Writes the mark as <see cref="WriteTo"/> does, its entities tagged with an id the caller gives.</summary>
        internal void Write(DxfDatabase database, ViewPlacement2 placement, string id)
        {
            SymbolDxf.MarkLayer(database);
            GeoPoint2 start = _leader[0];
            GeoPoint2 end = Anchor;
            double dx = end.X - start.X,
                dy = end.Y - start.Y,
                length = Math.Sqrt(dx * dx + dy * dy);

            if (length > 0.0)
            {
                SymbolDxf.Line(database, placement, start, end, Layer, id);
                double arrow = TextHeight * 0.5;

                if (length >= 2.0 * arrow)
                {
                    SymbolDxf.Arrow(
                        database,
                        placement,
                        end,
                        new GeoVector2(dx / length, dy / length),
                        arrow,
                        Layer,
                        id
                    );
                }
            }

            SymbolDxf.Text(database, placement, TextBox.Center, TextHeight, Text, Layer, id);
        }

        /// <summary>
        /// The mark with its text moved by hand, in view millimetres: the text box and the leader but its last point moved, its
        /// last point left on the object, so that the arrow still points at what the mark marks.
        /// </summary>
        internal DrawingMark MovedBy(GeoVector2 move)
        {
            if (move.X == 0.0 && move.Y == 0.0)
            {
                return this;
            }

            var leader = new GeoPoint2[_leader.Length];

            for (int i = 0; i < leader.Length - 1; i++)
            {
                leader[i] = new GeoPoint2(_leader[i].X + move.X, _leader[i].Y + move.Y);
            }

            leader[leader.Length - 1] = Anchor;
            var box = new GeoRectangle2(
                new GeoPoint2(TextBox.Center.X + move.X, TextBox.Center.Y + move.Y),
                TextBox.Width,
                TextBox.Height
            );
            return new DrawingMark(
                Id,
                Kind,
                ObjectId,
                _objectIds,
                Text,
                box,
                leader,
                TextHeight,
                Scale,
                Links.Points[0].Point
            );
        }

        /// <summary>The mark as values of the drawing document; the objects it stands for and its point are its links.</summary>
        internal List<KeyValuePair<string, string>> ToValues()
        {
            var leader = new string[_leader.Length * 2];

            for (int i = 0; i < _leader.Length; i++)
            {
                leader[2 * i] = DrawingSettings.Write(_leader[i].X);
                leader[2 * i + 1] = DrawingSettings.Write(_leader[i].Y);
            }

            GeoRectangle2 box = TextBox;
            return new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Kind", Kind.ToString()),
                new KeyValuePair<string, string>("Text", Text),
                new KeyValuePair<string, string>(
                    "TextBox",
                    string.Join(
                        ",",
                        DrawingSettings.Write(box.Center.X),
                        DrawingSettings.Write(box.Center.Y),
                        DrawingSettings.Write(box.Width),
                        DrawingSettings.Write(box.Height)
                    )
                ),
                new KeyValuePair<string, string>("Leader", string.Join(",", leader)),
                new KeyValuePair<string, string>("TextHeight", DrawingSettings.Write(TextHeight)),
                new KeyValuePair<string, string>("Scale", DrawingSettings.Write(Scale)),
            };
        }

        /// <summary>
        /// The mark read back from an element of the drawing document, its id the element's less its view's id and /; null
        /// where its values or links do not read.
        /// </summary>
        internal static DrawingMark FromElement(DrawingElement element)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, string> value in element.Values)
            {
                values[value.Key] = value.Value;
            }

            if (
                element.Links.Points.Count == 0
                || element.Links.ObjectIds.Count == 0
                || !values.TryGetValue("Kind", out string kindName)
                || !Enum.TryParse(kindName, out MarkKind kind)
                || !Enum.IsDefined(typeof(MarkKind), kind)
                || !values.TryGetValue("Text", out string text)
                || text == null
                || !Numbers(values, "TextBox", out double[] box)
                || box.Length != 4
                || !Numbers(values, "Leader", out double[] leader)
                || leader.Length < 4
                || leader.Length % 2 != 0
                || !Numbers(values, "TextHeight", out double[] height)
                || height.Length != 1
                || !(height[0] > 0.0)
                || !Numbers(values, "Scale", out double[] scale)
                || scale.Length != 1
                || !(scale[0] > 0.0)
            )
            {
                return null;
            }

            var points = new GeoPoint2[leader.Length / 2];

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new GeoPoint2(leader[2 * i], leader[2 * i + 1]);
            }

            string prefix = (element.ViewId ?? string.Empty) + "/";
            string id = element.Id.StartsWith(prefix, StringComparison.Ordinal)
                ? element.Id.Substring(prefix.Length)
                : element.Id;
            DrawingLinkPoint3 point = element.Links.Points[0];
            var ids = new string[element.Links.ObjectIds.Count];

            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = element.Links.ObjectIds[i];
            }

            return new DrawingMark(
                id,
                kind,
                point.ObjectId,
                ids,
                text,
                new GeoRectangle2(new GeoPoint2(box[0], box[1]), box[2], box[3]),
                points,
                height[0],
                scale[0],
                point.Point
            );
        }

        private static bool Numbers(Dictionary<string, string> values, string key, out double[] numbers)
        {
            numbers = null;

            if (!values.TryGetValue(key, out string written) || string.IsNullOrEmpty(written))
            {
                return false;
            }

            string[] parts = written.Split(',');
            numbers = new double[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                if (
                    !double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i])
                    || double.IsNaN(numbers[i])
                    || double.IsInfinity(numbers[i])
                )
                {
                    return false;
                }
            }

            return true;
        }
    }
}

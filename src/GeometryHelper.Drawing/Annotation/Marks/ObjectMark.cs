using System;
using System.Collections.Generic;
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
    /// 0.8 of that wide for each character, 28 by 35, standing clear of the view's dimensions and the other marks; its
    /// leader runs from the side of the box nearest the plate to the point of the plate's visible lines nearest the text.
    /// The four bolts of a base plate get one mark, "4 x M20 x 80", its leader ending at the centre of one of them.
    /// </para>
    /// <para>A mark does not change once made.</para>
    /// </remarks>
    public sealed class ObjectMark
    {
        /// <summary>The name of the layer marks are written on.</summary>
        public const string Layer = "Mark";

        private readonly GeoPoint2[] _leader;
        private readonly string[] _objectIds;

        /// <summary>
        /// Initializes a mark placed in its view.
        /// </summary>
        internal ObjectMark(string id, MarkKind kind, string objectId, string[] objectIds, string text, GeoRectangle2 textBox, GeoPoint2[] leader, double textHeight, double scale, GeoPoint3 modelPoint)
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
        /// Gets the box the text is taken to fill, square to the view's axes, in view millimetres: each character 0.8 of the
        /// height wide.
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
        /// 3.5 high at 1:10, set 8 off its part on paper, has a leader of about 80 in the view and an arrow 17.5 long.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public void WriteTo(DxfDatabase database, ViewPlacement2 placement)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            SymbolDxf.MarkLayer(database);
            GeoPoint2 start = _leader[0];
            GeoPoint2 end = Anchor;
            double dx = end.X - start.X, dy = end.Y - start.Y, length = Math.Sqrt(dx * dx + dy * dy);

            if (length > 0.0)
            {
                SymbolDxf.Line(database, placement, start, end, Layer, Id);
                double arrow = TextHeight * 0.5;

                if (length >= 2.0 * arrow)
                {
                    SymbolDxf.Arrow(database, placement, end, new GeoVector2(dx / length, dy / length), arrow, Layer, Id);
                }
            }

            SymbolDxf.Text(database, placement, TextBox.Center, TextHeight, Text, Layer, Id);
        }

        /// <inheritdoc/>
        public override string ToString() => $"{Kind} mark {Id} {Text}";
    }
}

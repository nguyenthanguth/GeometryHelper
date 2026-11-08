using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// The mark of a section in the view it is cut from: the cut line, an arrow at each end pointing the way the section
    /// looks, and its letter by each arrow, in view millimetres at 1:1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The section A-A across a beam 3 000 long at x = 1 500, looking along the beam's X, has the cut line from (1500, -200)
    /// to (1500, 200) and the direction (1, 0). Drawn with a text 2.5 high at 1:20, each arrow is 100 long in the view,
    /// from an end of the cut line out the way the section looks, and the box of each letter starts 20 past its arrow's tip.
    /// </para>
    /// <para>A mark does not change once made.</para>
    /// </remarks>
    public sealed class SectionMark
    {
        /// <summary>
        /// Makes the mark of a section.
        /// </summary>
        /// <param name="id">The id of the mark, unique in its view.</param>
        /// <param name="letter">The letter of the section: "A".</param>
        /// <param name="cutLine">Where the section plane cuts the view, in view millimetres.</param>
        /// <param name="lookDirection">The way the section looks, in the view; only its part square to the cut line counts.</param>
        /// <param name="textHeight">The height of the letters in the view, in view millimetres: the height on paper times the
        /// view's scale.</param>
        /// <exception cref="ArgumentNullException">Thrown when the id or the letter is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the id or the letter is empty, the cut line has no length or is not
        /// of finite numbers, or the direction has no part square to the cut line.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height is not a finite number above nought.</exception>
        public SectionMark(string id, string letter, GeoLine2 cutLine, GeoVector2 lookDirection, double textHeight)
        {
            Id = SymbolChecks.Text(id, nameof(id));
            Letter = SymbolChecks.Text(letter, nameof(letter));
            TextHeight = SymbolChecks.Positive(textHeight, nameof(textHeight));
            double length = cutLine.Length;

            if (!(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("The cut line has to have a length.", nameof(cutLine));
            }

            double ux = (cutLine.EndPoint.X - cutLine.StartPoint.X) / length, uy = (cutLine.EndPoint.Y - cutLine.StartPoint.Y) / length;
            double across = lookDirection.X * -uy + lookDirection.Y * ux;

            if (!(Math.Abs(across) > 0.0) || double.IsNaN(across) || double.IsInfinity(across))
            {
                throw new ArgumentException("The direction has to have a part square to the cut line.", nameof(lookDirection));
            }

            CutLine = cutLine;
            Direction = across > 0.0 ? new GeoVector2(-uy, ux) : new GeoVector2(uy, -ux);
        }

        /// <summary>Gets the id of the mark.</summary>
        public string Id { get; }

        /// <summary>Gets the letter of the section.</summary>
        public string Letter { get; }

        /// <summary>Gets where the section plane cuts the view, in view millimetres.</summary>
        public GeoLine2 CutLine { get; }

        /// <summary>Gets the way the section looks: a unit vector square to the cut line.</summary>
        public GeoVector2 Direction { get; }

        /// <summary>Gets the height of the letters in the view, in view millimetres.</summary>
        public double TextHeight { get; }

        /// <summary>
        /// Gets the boxes of the two letters, at the start of the cut line first, square to the view's axes: each 0.8 of the
        /// height wide for each character.
        /// </summary>
        public IReadOnlyList<GeoRectangle2> LetterBoxes => new[] { LetterBox(CutLine.StartPoint), LetterBox(CutLine.EndPoint) };

        /// <summary>
        /// Writes the mark into the model space of a drawing: the cut line as a thin LINE with the linetype CENTER and a thick
        /// stroke at each end, an arrow at each end as a LINE and a closed filled SOLID, and each letter as an MTEXT.
        /// </summary>
        /// <param name="database">The drawing to write into; what it holds already is left as it is.</param>
        /// <param name="placement">Where the view is drawn on the sheet, and at what scale.</param>
        /// <remarks>
        /// As ISO 128-44 draws a cutting plane: a long dash and a dot, thick where it ends. The strokes are as long as the
        /// letters are high and 0.5 wide; each arrow runs twice the height from an end of the cut line the way the section
        /// looks, its head half the height. Everything is on the layer <see cref="SymbolSettings.Layer"/>, made in colour 6
        /// where the drawing has none, and tagged with <see cref="DrawingEntityTags"/>: the mark's id and the placement's view
        /// id.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public void WriteTo(DxfDatabase database, ViewPlacement2 placement)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            SymbolDxf.SymbolLayer(database);
            SymbolDxf.CentreLayer(database);
            string layer = SymbolSettings.Layer;
            GeoPoint2 a = CutLine.StartPoint, b = CutLine.EndPoint;
            double length = CutLine.Length;
            var along = new GeoVector2((b.X - a.X) / length, (b.Y - a.Y) / length);
            DxfLine chain = SymbolDxf.Line(database, placement, a, b, layer, Id);
            chain.Linetype = "CENTER";
            chain.LinetypeScale = SymbolChecks.PatternScale(TextHeight, placement);
            double stroke = Math.Min(TextHeight, 0.5 * length);

            foreach ((GeoPoint2 end, GeoVector2 inward) in new[] { (a, along), (b, new GeoVector2(-along.X, -along.Y)) })
            {
                DxfLine thick = SymbolDxf.Line(database, placement, end, new GeoPoint2(end.X + inward.X * stroke, end.Y + inward.Y * stroke), layer, Id);
                thick.LineWeight = DxfLineWeight.LineWeight050;
                var tip = new GeoPoint2(end.X + Direction.X * 2.0 * TextHeight, end.Y + Direction.Y * 2.0 * TextHeight);
                SymbolDxf.Line(database, placement, end, tip, layer, Id);
                SymbolDxf.Arrow(database, placement, tip, Direction, 0.5 * TextHeight, layer, Id);
                SymbolDxf.Text(database, placement, LetterBox(end).Center, TextHeight, Letter, layer, Id);
            }
        }

        /// <inheritdoc/>
        public override string ToString() => $"Section mark {Id} {Letter}-{Letter}";

        /// <summary>The box of the letter at one end, starting 0.4 of the height past the arrow's tip the way the section looks.</summary>
        private GeoRectangle2 LetterBox(GeoPoint2 end)
        {
            double width = SymbolDxf.TextWidth(Letter, TextHeight);
            double reach = 2.0 * TextHeight + 0.4 * TextHeight + 0.5 * Math.Max(width * Math.Abs(Direction.X), TextHeight * Math.Abs(Direction.Y));
            return new GeoRectangle2(new GeoPoint2(end.X + Direction.X * reach, end.Y + Direction.Y * reach), width, TextHeight);
        }
    }
}

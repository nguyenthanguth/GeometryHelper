using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The words of an MTEXT being edited in place, laid out as the canvas will draw them: the one layout of the display,
    /// wrapped at the width, its lines spaced by the MTEXT's rule, its stacks, tabs, indents, paragraph spacing, columns, mask
    /// and frame, in the drawing's fonts; with where each place of the editor (<see cref="CadMTextEditor"/>) stands, for its
    /// caret and selection. Everything is in the MTEXT's own frame and drawing units: its location at nought, x along its
    /// direction, y up.
    /// </summary>
    /// <remarks>
    /// "II II II" in an MTEXT 20 wide of letters 10 high is laid out as "II II" with its baseline 10 below the top and "II"
    /// 50 / 3 below that, as the canvas draws it; the caret after the last letter stands at the end of the second line, and
    /// the space at the break stays at the end of the first. An empty paragraph at the end of the words is a line here, as
    /// AutoCAD's editor shows a caret on it, though the drawn MTEXT has no such line.
    /// </remarks>
    public sealed class CadMTextEditorLayout
    {
        private readonly CadDocument _document;
        private readonly DxfMText _text;
        private CadMTextLayout _layout;
        private IReadOnlyList<DisplayItem2> _parts = Array.Empty<DisplayItem2>();
        private int[] _lineStart = Array.Empty<int>();
        private int[] _lineEnd = Array.Empty<int>();

        /// <summary>
        /// Makes the layout of an MTEXT edited in place: the MTEXT of the edit's handle, its spacing style, columns, mask and
        /// frame kept; for an MTEXT being made or a table cell, an MTEXT of the edit's height, width, style and attachment.
        /// </summary>
        /// <param name="document">The document the MTEXT is drawn in, whose fonts lay it out.</param>
        /// <param name="edit">The edit.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document or the edit is null.</exception>
        public CadMTextEditorLayout(CadDocument document, CadInPlaceEdit edit)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));

            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            if (edit.Handle != null && document.FindEntity(edit.Handle) is DxfMText mtext && !mtext.IsErased)
            {
                _text = (DxfMText)mtext.Clone();
            }
            else
            {
                _text = new DxfMText
                {
                    TextHeight = edit.Height > 0.0 ? edit.Height : 2.5,
                    Width = Math.Max(0.0, edit.Width),
                    Attachment = (DxfAttachmentPoint)edit.Attachment,
                };

                if (!string.IsNullOrEmpty(edit.StyleName))
                {
                    _text.TextStyleName = edit.StyleName;
                }
            }
        }

        /// <summary>Gets the parts the words are drawn as, as the canvas draws an MTEXT (<see cref="DisplayText2.Parts"/>): the mask and the frame first, then the runs and their lines.</summary>
        public IReadOnlyList<DisplayItem2> Parts => _parts;

        /// <summary>Gets how many lines the words make, an empty paragraph at the end one of them.</summary>
        public int LineCount => _layout?.Places.Lines.Count ?? 0;

        /// <summary>Gets the box the MTEXT is defined by, as its grips stand: left, bottom, right and top.</summary>
        public double[] Box => _layout == null ? new double[4] : (double[])_layout.Box.Clone();

        /// <summary>Gets the box of everything drawn: the defined box, the ink of the letters and the mask and frame; left, bottom, right and top.</summary>
        public double[] Bounds { get; private set; } = new double[4];

        /// <summary>
        /// Lays the words out again, as they are and with the box as the editor has it now. A width of nought wraps nothing;
        /// with columns, the width is the columns'.
        /// </summary>
        /// <param name="editor">The words.</param>
        /// <param name="width">The width of the box in drawing units.</param>
        /// <param name="attachment">The attachment point, 1 top left to 9 bottom right.</param>
        /// <param name="lineSpacing">The line spacing factor, 0.25 to 4.</param>
        /// <param name="rgb">The colour of letters of no colour of their own, 0xRRGGBB.</param>
        /// <param name="layerRgb">The colour of letters coded ByLayer, 0xRRGGBB.</param>
        /// <exception cref="ArgumentNullException">Thrown when the editor is null.</exception>
        public void Update(
            CadMTextEditor editor,
            double width,
            int attachment,
            double lineSpacing,
            int rgb,
            int layerRgb
        )
        {
            if (editor == null)
            {
                throw new ArgumentNullException(nameof(editor));
            }

            if (_text.ColumnType == DxfColumnType.NoColumns && width >= 0.0 && !double.IsInfinity(width))
            {
                _text.Width = width;
            }

            if (attachment >= 1 && attachment <= 9)
            {
                _text.Attachment = (DxfAttachmentPoint)attachment;
            }

            if (lineSpacing >= 0.25 && lineSpacing <= 4.0)
            {
                _text.LineSpacingFactor = lineSpacing;
            }

            _layout = _document.EditorLayoutOf(_text, editor.Content);
            _parts = _document.EditorParts(_text, _layout, rgb, layerRgb);
            CadMTextLayout.PlaceMap map = _layout.Places;
            int lines = map.Lines.Count;
            _lineStart = new int[lines];
            _lineEnd = new int[lines];

            for (int n = 0; n < lines; n++)
            {
                _lineStart[n] = -1;
            }

            for (int q = 0; q < map.Line.Length; q++)
            {
                int n = map.Line[q];

                if (n >= 0 && n < lines)
                {
                    _lineStart[n] = _lineStart[n] < 0 ? q : _lineStart[n];
                    _lineEnd[n] = q;
                }
            }

            double[] box = _layout.Box;
            double[] ink = _layout.Ink;
            double left = Math.Min(box[0], ink[0]);
            double bottom = Math.Min(box[1], ink[1]);
            double right = Math.Max(box[2], ink[2]);
            double top = Math.Max(box[3], ink[3]);

            foreach (DisplayItem2 part in _parts)
            {
                DisplayBox bounds = part.Bounds;

                if (bounds.MinX <= bounds.MaxX)
                {
                    left = Math.Min(left, bounds.MinX);
                    bottom = Math.Min(bottom, bounds.MinY);
                    right = Math.Max(right, bounds.MaxX);
                    top = Math.Max(top, bounds.MaxY);
                }
            }

            foreach (CadMTextLayout.LineBox line in map.Lines)
            {
                bottom = Math.Min(bottom, line.Bottom);
                top = Math.Max(top, line.Top);
            }

            Bounds = new[] { left, bottom, right, top };
        }

        /// <summary>Gets the baseline of a line.</summary>
        /// <param name="line">The line, from nought.</param>
        /// <returns>Its Y.</returns>
        public double BaselineOf(int line) => Line(line).Baseline;

        /// <summary>Gets the top of a line: of its tallest letters.</summary>
        /// <param name="line">The line, from nought.</param>
        /// <returns>Its Y.</returns>
        public double TopOf(int line) => Line(line).Top;

        /// <summary>Gets the foot of a line's cell: the ink below its baseline, or a third of the MTEXT's height.</summary>
        /// <param name="line">The line, from nought.</param>
        /// <returns>Its Y.</returns>
        public double BottomOf(int line) => Line(line).Bottom;

        /// <summary>Gets the column a line stands in, from nought.</summary>
        /// <param name="line">The line, from nought.</param>
        /// <returns>The column.</returns>
        public int ColumnOf(int line) => Line(line).Column;

        /// <summary>Gets the first place of a line, where Home goes.</summary>
        /// <param name="line">The line, from nought.</param>
        /// <returns>The place.</returns>
        public int LineStart(int line)
        {
            Line(line);
            return _lineStart[line] < 0 ? 0 : _lineStart[line];
        }

        /// <summary>
        /// Gets the place at the end of a line, where End goes: before the space it breaks at or the paragraph break that ends
        /// it, or after the last place of the words.
        /// </summary>
        /// <param name="line">The line, from nought.</param>
        /// <returns>The place.</returns>
        public int LineEnd(int line)
        {
            Line(line);
            int end = _lineEnd[line];
            int[] lines = _layout.Places.Line;

            // The last place on the line is the space or break that ends it, set after the letters with no width, or the end of
            // the words; a line that breaks after a letter with no space (as between two ideographs) ends after that letter.
            return end + 1 < lines.Length && _layout.Places.Right[end] > _layout.Places.X[end] ? end + 1 : end;
        }

        /// <summary>Gets the line a place stands on.</summary>
        /// <param name="place">The place, 0 to the number of places.</param>
        /// <returns>The line, from nought.</returns>
        public int LineOfPlace(int place) => _layout == null ? 0 : _layout.Places.Line[Clamp(place)];

        /// <summary>Gets where the caret stands before a place, along the line: the left of its letter.</summary>
        /// <param name="place">The place, 0 to the number of places.</param>
        /// <returns>The X.</returns>
        public double XOf(int place) => _layout == null ? 0.0 : _layout.Places.X[Clamp(place)];

        /// <summary>Gets where a place's letter ends along the line: the X of the caret after it on the same line.</summary>
        /// <param name="place">The place, 0 to the number of places.</param>
        /// <returns>The X.</returns>
        public double RightOf(int place) => _layout == null ? 0.0 : _layout.Places.Right[Clamp(place)];

        /// <summary>
        /// Gets the place nearest a point, for a click: on the line under it (of the column under it, or the nearest), before
        /// the first letter whose middle is right of it, else at the line's end.
        /// </summary>
        /// <param name="x">The X.</param>
        /// <param name="y">The Y.</param>
        /// <returns>The place.</returns>
        public int PlaceAt(double x, double y)
        {
            if (_layout == null || LineCount == 0)
            {
                return 0;
            }

            List<CadMTextLayout.LineBox> lines = _layout.Places.Lines;
            int best = 0;
            double bestAcross = double.MaxValue;
            double bestDown = double.MaxValue;

            for (int n = 0; n < lines.Count; n++)
            {
                CadMTextLayout.LineBox line = lines[n];
                double across =
                    x < line.ColumnLeft ? line.ColumnLeft - x
                    : x > line.ColumnRight ? x - line.ColumnRight
                    : 0.0;
                double down =
                    y > line.Top ? y - line.Top
                    : y < line.Bottom ? line.Bottom - y
                    : 0.0;

                if (across < bestAcross - 1E-9 || (Math.Abs(across - bestAcross) <= 1E-9 && down < bestDown))
                {
                    best = n;
                    bestAcross = across;
                    bestDown = down;
                }
            }

            return PlaceOnLine(best, x);
        }

        /// <summary>Gets the place of a line nearest an X, for the arrows up and down: before the first letter whose middle is right of it, else at the line's end.</summary>
        /// <param name="line">The line, from nought.</param>
        /// <param name="x">The X.</param>
        /// <returns>The place.</returns>
        public int PlaceOnLine(int line, double x)
        {
            int end = LineEnd(line);

            for (int q = LineStart(line); q < end; q++)
            {
                if (_layout.Places.Line[q] == line && x < (_layout.Places.X[q] + _layout.Places.Right[q]) / 2.0)
                {
                    return q;
                }
            }

            return end;
        }

        private CadMTextLayout.LineBox Line(int line)
        {
            if (_layout == null || line < 0 || line >= LineCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(line),
                    line,
                    "The words have " + LineCount + " lines, counted from nought."
                );
            }

            return _layout.Places.Lines[line];
        }

        private int Clamp(int place) => Math.Max(0, Math.Min(_layout.Places.Line.Length - 1, place));
    }
}

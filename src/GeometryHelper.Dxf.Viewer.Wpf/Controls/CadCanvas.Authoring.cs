using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The canvas's drawing of the parameters and actions of the block open in the Block Editor, as AutoCAD 2021 draws them: a
    /// parameter in blue (its dimension line with arrowheads, its grips as squares, its label by it), an action in yellow (a
    /// lightning bolt with its name), the ones selected in white and thicker. The glyphs are the editor's
    /// (<see cref="CadBlockEditor.Glyphs"/>); their sizes are in pixels, so that they read at every zoom.
    /// </summary>
    public sealed partial class CadCanvas
    {
        private static readonly Brush ParameterInk = Solid(0x19B5F0);
        private static readonly Brush ActionInk = Solid(0xFFC83D);
        private static readonly Brush ChosenInk = Solid(0xFFFFFF);
        private static readonly Typeface AuthoringFace = new Typeface("Segoe UI");

        // The corners of the bolt, in a box 24 across as the Move icon is drawn, about its middle.
        private static readonly Point[] Bolt =
        {
            new Point(1.0, -9.0),
            new Point(-7.0, 2.0),
            new Point(-1.0, 2.0),
            new Point(-2.0, 9.0),
            new Point(6.0, -2.0),
            new Point(0.0, -2.0),
        };

        /// <summary>Draws the glyphs of the block open's parameters and actions over its picture; nothing outside the Block Editor.</summary>
        private void DrawAuthoring(DrawingContext dc, CadView view, double scale)
        {
            CadBlockEditor editor = Document?.BlockEditor;

            if (editor == null || WorkSpace == null || !ReferenceEquals(editor.Space, WorkSpace))
            {
                return;
            }

            double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            foreach (CadAuthoringGlyph glyph in editor.Glyphs)
            {
                bool chosen = Document.Selection.Contains(glyph.Handle);
                Brush ink =
                    chosen ? ChosenInk
                    : glyph.IsParameter ? ParameterInk
                    : ActionInk;
                var pen = new Pen(ink, chosen ? 2.0 : 1.25);
                pen.Freeze();

                foreach (GeoPoint2[] line in glyph.Lines)
                {
                    var geometry = new StreamGeometry();

                    using (StreamGeometryContext context = geometry.Open())
                    {
                        context.BeginFigure(ToScreen(view, line[0], scale), false, false);

                        for (int i = 1; i < line.Length; i++)
                        {
                            context.LineTo(ToScreen(view, line[i], scale), true, false);
                        }
                    }

                    geometry.Freeze();
                    dc.DrawGeometry(null, pen, geometry);
                }

                foreach (KeyValuePair<GeoPoint2, GeoPoint2> arrow in glyph.Arrows)
                {
                    DrawArrowHead(dc, ink, ToScreen(view, arrow.Key, scale), ToScreen(view, arrow.Value, scale));
                }

                foreach (GeoPoint2 grip in glyph.Grips)
                {
                    Point at = ToScreen(view, grip, scale);
                    dc.DrawRectangle(ink, null, new Rect(at.X - 3.5, at.Y - 3.5, 7.0, 7.0));
                }

                if (glyph.Bolt.HasValue)
                {
                    Point at = ToScreen(view, glyph.Bolt.Value, scale);
                    var bolt = new StreamGeometry();

                    using (StreamGeometryContext context = bolt.Open())
                    {
                        context.BeginFigure(new Point(at.X + Bolt[0].X, at.Y + Bolt[0].Y), true, true);

                        for (int i = 1; i < Bolt.Length; i++)
                        {
                            context.LineTo(new Point(at.X + Bolt[i].X, at.Y + Bolt[i].Y), true, false);
                        }
                    }

                    bolt.Freeze();
                    dc.DrawGeometry(ink, null, bolt);
                }

                if (!string.IsNullOrEmpty(glyph.Label) && glyph.LabelAt.HasValue)
                {
                    Point at = ToScreen(view, glyph.LabelAt.Value, scale);
                    var text = new FormattedText(
                        glyph.Label,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        AuthoringFace,
                        11.0,
                        ink,
                        dip
                    );

                    // A parameter's label stands over its point, an action's name beside its bolt.
                    dc.DrawText(
                        text,
                        glyph.Bolt.HasValue
                            ? new Point(at.X + 12.0, at.Y - text.Height / 2.0)
                            : new Point(at.X - text.Width / 2.0, at.Y - text.Height - 4.0)
                    );
                }
            }
        }

        /// <summary>A solid arrowhead 9 pixels long at a point, pointing away from another.</summary>
        private static void DrawArrowHead(DrawingContext dc, Brush ink, Point tip, Point from)
        {
            Vector along = tip - from;

            if (along.LengthSquared < 1E-9)
            {
                return;
            }

            along.Normalize();
            var side = new Vector(-along.Y, along.X);
            var head = new StreamGeometry();

            using (StreamGeometryContext context = head.Open())
            {
                context.BeginFigure(tip, true, true);
                context.LineTo(tip - along * 9.0 + side * 3.5, true, false);
                context.LineTo(tip - along * 9.0 - side * 3.5, true, false);
            }

            head.Freeze();
            dc.DrawGeometry(ink, null, head);
        }
    }
}

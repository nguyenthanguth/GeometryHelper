using System;
using System.Collections.Generic;
using SkiaSharp;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Draws a plot stamp (<see cref="CadPlotStamp"/>) on a PDF page: its lines in the stamp's font, black, at the corner of
    /// the sheet the stamp names, turned for a vertical or upside-down stamp. The text is a PDF's own, which a reader finds.
    /// </summary>
    internal static class CadPlotStampPainter
    {
        /// <summary>The distance from one baseline to the next, in heights of the letters, as a text of the canvas is spaced.</summary>
        private const double LineStep = 1.25;

        /// <summary>The height of a capital letter as a share of the font's size.</summary>
        private const double CapShare = 0.72;

        /// <summary>
        /// Draws the lines of a stamp on a page.
        /// </summary>
        /// <param name="canvas">The page's canvas.</param>
        /// <param name="stamp">The stamp.</param>
        /// <param name="lines">The lines of the stamp, from <see cref="CadPlotStamp.Lines"/>.</param>
        /// <param name="page">The plot laid on its sheet.</param>
        /// <param name="sheetToCanvas">From the sheet's millimetres, Y upward, to the canvas.</param>
        public static void Draw(
            SKCanvas canvas,
            CadPlotStamp stamp,
            IReadOnlyList<string> lines,
            CadPlotPage page,
            Affine2 sheetToCanvas
        )
        {
            if (lines.Count == 0)
            {
                return;
            }

            // Everything in the canvas's units, a millimetre of the sheet being sheetToCanvas.Scale of them.
            double perMillimetre = sheetToCanvas.Scale;
            double height = stamp.ToMillimetres(stamp.TextHeight) * perMillimetre;
            double step = height * LineStep;

            using (SKTypeface made = SKTypeface.FromFamilyName(stamp.FontName))
            using (var font = new SKFont(made ?? SKTypeface.Default, (float)(height / CapShare)))
            using (var paint = new SKPaint { IsAntialias = true, Color = SKColors.Black })
            {
                double width = 0.0;

                foreach (string line in lines)
                {
                    width = Math.Max(width, font.MeasureText(line));
                }

                double block = height + (lines.Count - 1) * step;
                DisplayBox box = stamp.Footprint(page, width / perMillimetre, block / perMillimetre);
                double centreX = sheetToCanvas.X((box.MinX + box.MaxX) / 2.0, (box.MinY + box.MaxY) / 2.0);
                double centreY = sheetToCanvas.Y((box.MinX + box.MaxX) / 2.0, (box.MinY + box.MaxY) / 2.0);

                canvas.Save();
                canvas.Translate((float)centreX, (float)centreY);

                // A vertical stamp reads up the sheet, a quarter turned against the clock; upside down turns it half round more.
                canvas.RotateDegrees((float)((stamp.Vertical ? -90.0 : 0.0) + (stamp.UpsideDown ? 180.0 : 0.0)));

                for (int i = 0; i < lines.Count; i++)
                {
                    canvas.DrawText(
                        lines[i],
                        (float)(-width / 2.0),
                        (float)(-block / 2.0 + height + i * step),
                        SKTextAlign.Left,
                        font,
                        paint
                    );
                }

                canvas.Restore();
            }
        }
    }
}

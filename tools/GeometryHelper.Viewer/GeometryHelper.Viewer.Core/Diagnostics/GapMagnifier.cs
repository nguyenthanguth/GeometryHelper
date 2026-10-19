using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// The picture of a gap too small to see: the lines of the two items near the gap, drawn larger about the middle of it, so that the gap
    /// is about 40 pixels across, with its true size beside it.
    /// </summary>
    public sealed class GapLens
    {
        internal GapLens(double scale, double distance, double pixels, DiagnosticOverlay overlay, string text)
        {
            Scale = scale;
            Distance = distance;
            Pixels = pixels;
            Overlay = overlay;
            Text = text;
        }

        /// <summary>
        /// Gets how many times larger the lens draws: a power of ten from 10 to 1 000 000, or 1 where the gap is large enough to be seen as it is.
        /// </summary>
        public double Scale { get; }

        /// <summary>
        /// Gets the gap the library measured.
        /// </summary>
        public double Distance { get; }

        /// <summary>
        /// Gets how many pixels across the gap is as it shows at the zoom of the view, before it is magnified.
        /// </summary>
        public double Pixels { get; }

        /// <summary>
        /// Gets what is drawn: the lines of the two items near the gap, the gap, and a label with the scale and the true number.
        /// </summary>
        public DiagnosticOverlay Overlay { get; }

        /// <summary>
        /// Gets what the label says.
        /// </summary>
        public string Text { get; }
    }

    /// <summary>
    /// Chooses the scale of a gap magnifier and draws it.
    /// </summary>
    public static class GapMagnifier
    {
        /// <summary>
        /// The size in pixels the magnified gap is aimed at.
        /// </summary>
        public const double TargetPixels = 40.0;

        /// <summary>
        /// The least scale the magnifier uses; a gap that needs less is drawn as it is.
        /// </summary>
        public const double MinimumScale = 10.0;

        /// <summary>
        /// The most scale the magnifier uses.
        /// </summary>
        public const double MaximumScale = 1000000.0;

        /// <summary>
        /// The radius in pixels of the window of the items drawn round the middle of the gap, once magnified.
        /// </summary>
        public const double WindowPixels = 100.0;

        /// <summary>
        /// Chooses the scale: the power of ten that brings a gap nearest to 40 pixels across, on a logarithmic scale, from 10 to 1 000 000.
        /// </summary>
        /// <param name="distance">The gap in millimetres.</param>
        /// <param name="pixelSize">How many millimetres one pixel of the view covers at the gap.</param>
        /// <returns>
        /// The scale. 1 where the gap is already 12.7 pixels or more, where ten times would pass 40 pixels by more than the gap now falls
        /// short of it, and where the gap is nought or not a number, since there is nothing to magnify. A gap that even a million times does
        /// not bring up to 40 pixels gets a million.
        /// </returns>
        public static double ChooseScale(double distance, double pixelSize)
        {
            if (!(distance > 0.0) || double.IsInfinity(distance) || !(pixelSize > 0.0) || double.IsInfinity(pixelSize))
            {
                return 1.0;
            }

            double pixels = distance / pixelSize;
            double power = Math.Round(Math.Log10(TargetPixels / pixels));

            if (power < 1.0)
            {
                return 1.0;
            }

            return Math.Min(MaximumScale, Math.Pow(10.0, power));
        }

        /// <summary>
        /// Draws the magnifier of a gap.
        /// </summary>
        /// <param name="geometry">The exact curves of the scene, which give the lines and arcs of the two items.</param>
        /// <param name="gap">The gap, as the library measured it.</param>
        /// <param name="pixelSize">How many millimetres one pixel of the view covers at the gap.</param>
        /// <returns>
        /// The lens. The curves of the two items within 100 pixels of the middle of the gap, as drawn after the scaling, are drawn scaled about
        /// that middle, and the gap itself between the scaled ends; a gap of nought has nothing to magnify and gets a scale of 1. A segment is
        /// cut to the window and scaled; an arc or a circle is cut again from its own numbers into chords so fine that none lies more than a tenth
        /// of a pixel from the curve once scaled, and not scaled from the chords it is drawn with in the view.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the geometry or the gap are null.</exception>
        public static GapLens Build(ExactGeometry geometry, GapReading gap, double pixelSize)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            if (gap == null)
            {
                throw new ArgumentNullException(nameof(gap));
            }

            double scale = ChooseScale(gap.Distance, pixelSize);
            Vec3 middle = (gap.From + gap.To) * 0.5;
            double pixels = pixelSize > 0.0 ? gap.Distance / pixelSize : 0.0;
            var overlay = new DiagnosticOverlay();
            string text = "gap " + gap.Distance.ToString("G10", CultureInfo.InvariantCulture) + (scale > 1.0 ? "  drawn x" + scale.ToString("G6", CultureInfo.InvariantCulture) : string.Empty);

            if (scale > 1.0)
            {
                double reach = Math.Max(WindowPixels * pixelSize / scale, gap.Distance);

                foreach (SceneCurve curve in geometry.Curves)
                {
                    string color = curve.ItemIndex == gap.First ? "#7A3E9D" : curve.ItemIndex == gap.Second ? "#008080" : null;

                    if (color != null)
                    {
                        ClipCurve(overlay, curve, middle, reach, scale, pixelSize, color);
                    }
                }
            }

            Vec3 from = middle + (gap.From - middle) * scale;
            Vec3 to = middle + (gap.To - middle) * scale;
            overlay.Line(DiagnosticColors.Gap, 3.0, from, to);
            overlay.Dots.Add(new DiagnosticDot(from, DiagnosticColors.Gap, 8.0));
            overlay.Dots.Add(new DiagnosticDot(to, DiagnosticColors.Gap, 8.0));

            // The label sits to one side of the gap, a third of its drawn length off the line, so that it does not cover what it says the size of.
            Vec3 along = (to - from).Normalized();
            Vec3 side = Vec3.Cross(new Vec3(0.0, 0.0, 1.0), along);
            side = side.Length > 1E-9 ? side.Normalized() : new Vec3(0.0, 1.0, 0.0);
            overlay.Labels.Add(new DrawLabel(middle + side * ((to - from).Length * 0.35), text, 0.0));
            return new GapLens(scale, gap.Distance, pixels, overlay, text);
        }

        /// <summary>
        /// The most chords an arc is cut into inside the window.
        /// </summary>
        private const int MaxChords = 4096;

        /// <summary>
        /// The most a chord may lie from its arc once scaled, in pixels.
        /// </summary>
        private const double SagittaPixels = 0.1;

        /// <summary>
        /// Adds the part of a curve that is within a distance of a point, scaled about that point.
        /// </summary>
        private static void ClipCurve(DiagnosticOverlay overlay, SceneCurve curve, Vec3 centre, double reach, double scale, double pixelSize, string color)
        {
            if (curve.Shape == CurveShape.Segment)
            {
                if (ClipSegment(curve.Start, curve.End, centre, reach, out Vec3 from, out Vec3 to))
                {
                    overlay.Line(color, 2.2, centre + (from - centre) * scale, centre + (to - centre) * scale);
                }

                return;
            }

            double radius = Math.Max(curve.RadiusU, curve.RadiusV);

            double near = (curve.ClosestPointTo(centre) - centre).Length;

            if (!(near <= reach) || !(radius > 0.0))
            {
                return;
            }

            // The arc within the window is the angles about the nearest point that the window reaches: the length along the curve at which it
            // leaves the sphere of the window, as an angle, out to the half turn; cut into chords no further than the sagitta allows from the
            // arc once scaled.
            double t0 = curve.ParameterOf(centre);
            double half = Math.Min(Math.PI, Math.Asin(Math.Min(1.0, Math.Sqrt(reach * reach - near * near) / radius)) + 1E-12);
            double allowed = SagittaPixels * pixelSize / (scale * radius);
            double chord = allowed >= 2.0 ? Math.PI : 2.0 * Math.Acos(Math.Max(-1.0, 1.0 - allowed));
            int count = (int)Math.Min(MaxChords, Math.Max(2.0, Math.Ceiling(2.0 * half / Math.Max(chord, 1E-9))));
            var run = new List<Vec3>();

            for (int k = 0; k <= count; k++)
            {
                double t = t0 - half + 2.0 * half * k / count;
                Vec3 p = curve.PointAt(t);

                if (curve.ContainsParameter(t, 1E-9) && (p - centre).Length <= reach * (1.0 + 1E-6))
                {
                    run.Add(centre + (p - centre) * scale);
                }
                else
                {
                    Flush(overlay, run, color);
                }
            }

            Flush(overlay, run, color);
        }

        private static void Flush(DiagnosticOverlay overlay, List<Vec3> run, string color)
        {
            if (run.Count >= 2)
            {
                overlay.Strokes.Add(new DiagnosticStroke(new DrawPolyline(run.ToArray(), false), color, 2.2));
            }

            run.Clear();
        }

        /// <summary>
        /// Cuts a segment to the sphere of a radius round a point.
        /// </summary>
        private static bool ClipSegment(Vec3 a, Vec3 b, Vec3 centre, double radius, out Vec3 from, out Vec3 to)
        {
            from = a;
            to = b;
            Vec3 d = b - a;
            Vec3 w = a - centre;
            double qa = Vec3.Dot(d, d);
            double qb = 2.0 * Vec3.Dot(w, d);
            double qc = Vec3.Dot(w, w) - radius * radius;

            if (!(qa > 0.0))
            {
                return qc <= 0.0;
            }

            double disc = qb * qb - 4.0 * qa * qc;

            if (disc < 0.0)
            {
                return false;
            }

            double root = Math.Sqrt(disc);
            double t0 = Math.Max(0.0, (-qb - root) / (2.0 * qa));
            double t1 = Math.Min(1.0, (-qb + root) / (2.0 * qa));

            if (t0 >= t1)
            {
                return false;
            }

            from = a + d * t0;
            to = a + d * t1;
            return true;
        }
    }
}

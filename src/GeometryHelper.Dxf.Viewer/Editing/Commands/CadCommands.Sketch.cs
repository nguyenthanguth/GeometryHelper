using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// SKETCH: freehand lines. It writes "Type = Lines  Increment = 0.1000  Tolerance = 0.5000" and asks "Specify sketch or
    /// [Type/Increment/toLerance]:". Type asks "Enter sketch type [Lines/Polyline/Spline] &lt;Lines&gt;:" (SKPOLY 0, 1 or 2),
    /// Increment "Specify sketch increment &lt;0.1000&gt;:" (the least length of a piece, SKETCHINC, which a nought or less is
    /// refused with "Value must be positive."), toLerance "Specify spline fit tolerance &lt;0.5000&gt;:" (SKTOLERANCE, nought to
    /// one, others refused with "Requires a value between 0 and 1."). A point given puts the pen down there: the cursor then
    /// draws, a point recorded each time it has moved the increment from the last one, and Enter lifts the pen, making the
    /// stroke as lines (one for each piece), one polyline or one spline through the points; Enter with the pen up ends the
    /// command. AutoCAD lifts the pen with a click; the presenter's cursor feeds a point at each click as at each move, so
    /// Enter lifts it. Every stroke is part of the command's one undo step.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>The key SKPOLY is kept under in <see cref="CadCommandSession.Remembered"/>.</summary>
        private const string SketchTypeKey = "skpoly";

        /// <summary>The key SKTOLERANCE is kept under in <see cref="CadCommandSession.Remembered"/>.</summary>
        private const string SketchToleranceKey = "sktolerance";

        private static readonly string[] SketchTypes = { "Lines", "Polyline", "Spline" };

        /// <summary>The type of sketch: what SKPOLY was set to, else 1 (polylines) where the header's $SKPOLY is on, 0 (lines) where it is off.</summary>
        private static int SketchType(CadCommandSession session) =>
            Math.Max(0, Math.Min(2, (int)Kept(session, SketchTypeKey, session.Document.Database.Skpoly ? 1.0 : 0.0)));

        private static IEnumerable<CadPrompt> Sketch(CadCommandContext c)
        {
            c.Write(
                "Type = "
                    + SketchTypes[SketchType(c.Session)]
                    + "  Increment = "
                    + Length(c.Database.Sketchinc)
                    + "  Tolerance = "
                    + Length(Kept(c.Session, SketchToleranceKey, 0.5))
            );

            while (true)
            {
                yield return CadPrompt
                    .ForPoint("Specify sketch", null, new[] { "Type", "Increment", "toLerance" })
                    .Refusing("2D point or option keyword required.");

                if (c.Input.Is("Type"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Enter sketch type",
                        SketchTypes,
                        SketchTypes[SketchType(c.Session)]
                    );

                    for (int i = 0; i < SketchTypes.Length; i++)
                    {
                        if (c.Input.Is(SketchTypes[i]))
                        {
                            c.Remember(SketchTypeKey, i);
                        }
                    }

                    continue;
                }

                if (c.Input.Is("Increment"))
                {
                    double was = c.Database.Sketchinc;

                    while (true)
                    {
                        yield return CadPrompt.ForDistance("Specify sketch increment", null, null, Length(was));

                        if (c.Input.Kind == CadInputKind.Number && !(c.Input.Number > 0.0))
                        {
                            c.Write("Value must be positive.");
                            continue;
                        }

                        break;
                    }

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        double typed = c.Input.Number;
                        SetHeader(c, "$SKETCHINC", was, typed, d => d.Sketchinc = typed);
                    }

                    continue;
                }

                if (c.Input.Is("toLerance"))
                {
                    double was = Kept(c.Session, SketchToleranceKey, 0.5);

                    while (true)
                    {
                        yield return CadPrompt.ForDistance("Specify spline fit tolerance", null, null, Length(was));

                        if (c.Input.Kind == CadInputKind.Number && !(c.Input.Number >= 0.0 && c.Input.Number <= 1.0))
                        {
                            c.Write("Requires a value between 0 and 1.");
                            continue;
                        }

                        break;
                    }

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        c.Remember(SketchToleranceKey, c.Input.Number);
                    }

                    continue;
                }

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                // The pen is down at the point: the cursor records a point each time it has gone the increment from the last.
                double increment = c.Database.Sketchinc;
                var stroke = new List<GeoPoint2> { c.Input.Point };

                while (true)
                {
                    yield return CadPrompt
                        .ForPoint("Move the cursor to sketch, Enter to lift the pen", stroke[stroke.Count - 1])
                        .WithoutColon()
                        .Tracking()
                        .Previewing(CadPreview.MakingOne(c, input =>
                            {
                                var shown = new List<GeoPoint2>(stroke);

                                if ((input.Point - shown[shown.Count - 1]).Length >= increment)
                                {
                                    shown.Add(input.Point);
                                }

                                return shown.Count < 2 ? null : Current(c, SketchPolyline(shown));
                            }));

                    if (!c.Input.IsPoint)
                    {
                        break;
                    }

                    if ((c.Input.Point - stroke[stroke.Count - 1]).Length >= increment)
                    {
                        stroke.Add(c.Input.Point);
                    }
                }

                SketchStroke(c, stroke);
            }
        }

        /// <summary>An open polyline through the points of a sketch.</summary>
        private static DxfPolyline SketchPolyline(IReadOnlyList<GeoPoint2> points)
        {
            var polyline = new DxfPolyline();

            for (int i = 0; i < points.Count; i++)
            {
                polyline.AddVertexAt(i, points[i], 0.0, 0.0, 0.0);
            }

            return polyline;
        }

        /// <summary>Makes the stroke a pen drew as SKPOLY says: a line for each piece, one polyline, or one spline through the points.</summary>
        private static void SketchStroke(CadCommandContext c, IReadOnlyList<GeoPoint2> stroke)
        {
            if (stroke.Count < 2)
            {
                return;
            }

            int type = SketchType(c.Session);

            if (type == 0)
            {
                c.Apply(null, s =>
                {
                    for (int i = 1; i < stroke.Count; i++)
                    {
                        s.Append(c.Space, Current(c, new DxfLine(P3(stroke[i - 1]), P3(stroke[i]))));
                    }
                });
            }
            else if (type == 1)
            {
                c.Apply(null, s => s.Append(c.Space, Current(c, SketchPolyline(stroke))));
            }
            else
            {
                DxfSpline spline = FitSpline(c, stroke, 0, 0.0, null, null, false);

                if (spline != null)
                {
                    c.Apply(null, s => s.Append(c.Space, spline));
                }
            }
        }
    }
}

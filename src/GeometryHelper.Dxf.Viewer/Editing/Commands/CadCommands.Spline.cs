using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>SPLINE in AutoCAD 2021's words: Method Fit or CV, Knots, Degree, Object, start and end Tangency, toLerance, Undo and Close.</summary>
    internal static partial class CadCommands
    {
        private static readonly string[] SplineKnotNames = { "Chord", "Square root", "Uniform" };

        /// <summary>
        /// SPLINE: "Current settings: Method=Fit   Knots=Chord" and "Specify first point or [Method/Knots/Object]:", or for the
        /// control-vertex method "Method=CV   Degree=3" and "[Method/Degree/Object]". Method asks "Enter spline creation method
        /// [Fit/CV] &lt;Fit&gt;:", Knots "Enter knot parameterization [Chord/Square root/Uniform] &lt;Chord&gt;:" and Degree "Enter
        /// degree of spline &lt;3&gt;:", 1 to 10; each is kept as SPLMETHOD, SPLKNOTS and SPLDEGREE for the next SPLINE. Object asks
        /// "Select spline-fit polyline:" and turns each 2D polyline fitted with a spline into the spline on its frame, the
        /// polyline erased (AutoCAD's DELOBJ of 3), and ends the command; anything else picked is "Only spline fitted polylines
        /// can be converted to splines." and "Unable to convert the selected object.". The points go on with "Enter next point or
        /// [start Tangency/toLerance]:", then "[end Tangency/toLerance/Undo]:" and from the third point "[end Tangency/toLerance/Undo/Close]:"
        /// for the fit method (the cubic through the points, <see cref="DxfSpline.FitData"/>), "Enter next point:", "[Undo]:" and
        /// "[Close/Undo]:" for control vertices (the clamped uniform spline on them, of the degree set, lowered where there are
        /// fewer points). A start tangent is a point picked, the direction to it from the first point; an end tangent the direction
        /// to it from the last point, which ends the command. toLerance asks "Specify fit tolerance &lt;0.0000&gt;:" (written to
        /// the spline, which the library still fits through the points). Close makes the spline closed through its first point
        /// (fit) or the periodic spline on its points (CV). Enter ends the command, which makes nothing of a single point. The
        /// spline through the points and the cursor is drawn while the next point is asked.
        /// </summary>
        private static IEnumerable<CadPrompt> Spline(CadCommandContext c)
        {
            GeoPoint2 first;

            while (true)
            {
                bool vertices = c.Remembered("spline.method", 0.0) != 0.0;
                int knots = (int)c.Remembered("spline.knots", 0.0);
                int degree = (int)c.Remembered("spline.degree", 3.0);
                c.Write(
                    vertices
                        ? "Current settings: Method=CV   Degree=" + degree.ToString(CultureInfo.InvariantCulture)
                        : "Current settings: Method=Fit   Knots=" + SplineKnotNames[knots]
                );
                yield return CadPrompt.ForPoint(
                    "Specify first point",
                    null,
                    vertices ? new[] { "Method", "Degree", "Object" } : new[] { "Method", "Knots", "Object" }
                );

                if (c.Input.Is("Method"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Enter spline creation method",
                        new[] { "Fit", "CV" },
                        vertices ? "CV" : "Fit"
                    );

                    if (c.Input.Is("Fit") || c.Input.Is("CV"))
                    {
                        c.Remember("spline.method", c.Input.Is("CV") ? 1.0 : 0.0);
                    }
                }
                else if (c.Input.Is("Knots"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Enter knot parameterization",
                        SplineKnotNames,
                        SplineKnotNames[knots]
                    );

                    if (c.Input.Kind == CadInputKind.Keyword)
                    {
                        c.Remember("spline.knots", Array.IndexOf(SplineKnotNames, c.Input.Text));
                    }
                }
                else if (c.Input.Is("Degree"))
                {
                    while (true)
                    {
                        yield return CadPrompt
                            .ForInteger("Enter degree of spline", null, degree.ToString(CultureInfo.InvariantCulture))
                            .Refusing("Requires an integer between -32768 and 32767, or option keyword.");

                        if (c.Input.Kind != CadInputKind.Number)
                        {
                            break;
                        }

                        // AutoCAD 2021 asks again, with no words, for a degree outside 1 to 10.
                        if (c.Input.Number >= 1.0 && c.Input.Number <= 10.0)
                        {
                            c.Remember("spline.degree", c.Input.Number);
                            break;
                        }
                    }
                }
                else if (c.Input.Is("Object"))
                {
                    foreach (CadPrompt prompt in SplineObjects(c))
                    {
                        yield return prompt;
                    }

                    yield break;
                }
                else if (c.Input.IsPoint)
                {
                    first = c.Input.Point;
                    break;
                }
                else
                {
                    yield break;
                }
            }

            bool cv = c.Remembered("spline.method", 0.0) != 0.0;
            int parameters = (int)c.Remembered("spline.knots", 0.0);
            int order = (int)c.Remembered("spline.degree", 3.0);
            var points = new List<GeoPoint2> { first };
            GeoVector2? startTangent = null;
            GeoVector2? endTangent = null;
            double tolerance = 0.0;

            DxfSpline Made(IReadOnlyList<GeoPoint2> through, bool close) =>
                cv
                    ? ControlSpline(c, through, order, close)
                    : FitSpline(c, through, parameters, tolerance, startTangent, endTangent, close);

            while (true)
            {
                int count = points.Count;
                string[] words = cv
                    ? (
                        count == 1 ? null
                        : count == 2 ? new[] { "Undo" }
                        : new[] { "Close", "Undo" }
                    )
                    : (
                        count == 1 ? new[] { "start Tangency", "toLerance" }
                        : count == 2 ? new[] { "end Tangency", "toLerance", "Undo" }
                        : new[] { "end Tangency", "toLerance", "Undo", "Close" }
                    );
                yield return CadPrompt
                    .ForPoint("Enter next point", points[count - 1], words)
                    .Previewing(
                        CadPreview.MakingOne(c, input => Made(points.Concat(new[] { input.Point }).ToList(), false))
                    );

                if (c.Input.IsPoint)
                {
                    GeoPoint2 next = c.Input.Point;

                    if (!next.IsEqualTo(points[count - 1], c.Session.Tolerance))
                    {
                        points.Add(next);
                    }

                    continue;
                }

                if (c.Input.Is("Undo"))
                {
                    points.RemoveAt(count - 1);
                    continue;
                }

                if (c.Input.Is("toLerance"))
                {
                    while (true)
                    {
                        yield return CadPrompt.ForDistance("Specify fit tolerance", null, null, Length(tolerance));

                        if (c.Input.Kind != CadInputKind.Number)
                        {
                            break;
                        }

                        if (c.Input.Number < 0.0)
                        {
                            c.Write("Value must be positive.");
                            continue;
                        }

                        tolerance = c.Input.Number;
                        break;
                    }

                    continue;
                }

                if (c.Input.Is("start Tangency"))
                {
                    yield return CadPrompt.ForPoint("Specify start tangent", points[0]);

                    if (c.Input.IsPoint)
                    {
                        startTangent = c.Input.Point - points[0];
                    }

                    continue;
                }

                bool close = c.Input.Is("Close");

                if (c.Input.Is("end Tangency"))
                {
                    yield return CadPrompt.ForPoint("Specify end tangent", points[count - 1]);

                    if (c.Input.IsPoint && (c.Input.Point - points[count - 1]).Length > c.Session.Tolerance.EqualPoint)
                    {
                        GeoVector2 way = c.Input.Point - points[count - 1];
                        endTangent = way / way.Length;
                    }
                }

                DxfSpline spline;

                try
                {
                    spline = Made(points, close);
                }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
                {
                    c.Write("*Invalid* The spline cannot be made through those points.");
                    yield break;
                }

                if (spline != null)
                {
                    c.Apply(null, s => s.Append(c.Space, spline));
                }

                yield break;
            }
        }

        /// <summary>
        /// The cubic AutoCAD's SPLINE of the fit method makes through points: knots by the chord, the square root of it or one
        /// step each, the tolerance written, the start tangent as the vector it was given and the end tangent as given,
        /// closed through the first point where asked; null for fewer than two points, or three for a closed one.
        /// </summary>
        private static DxfSpline FitSpline(
            CadCommandContext c,
            IReadOnlyList<GeoPoint2> points,
            int knots,
            double tolerance,
            GeoVector2? start,
            GeoVector2? end,
            bool close
        )
        {
            if (points.Count < (close ? 3 : 2))
            {
                return null;
            }

            var spline = new DxfSpline();
            spline.FitData = new DxfFitData(points.Select(P3), 3, tolerance)
            {
                KnotParam =
                    knots == 1 ? DxfKnotParameterizationEnum.SqrtChord
                    : knots == 2 ? DxfKnotParameterizationEnum.Uniform
                    : DxfKnotParameterizationEnum.Chord,
                StartTangent = start.HasValue ? new GeoVector3(start.Value.X, start.Value.Y, 0.0) : default,
                EndTangent = end.HasValue ? new GeoVector3(end.Value.X, end.Value.Y, 0.0) : default,
                TangentsExist = start.HasValue || end.HasValue,
                Periodic = close,
            };
            return Current(c, spline);
        }

        /// <summary>
        /// The spline of control vertices AutoCAD's SPLINE of the CV method makes on points: of the degree, lowered to the
        /// points less one where they are fewer, clamped, its knots a step apart; closed, the periodic spline on the points as
        /// SPLINEDIT closes one, of the degree asked however few the points. Null for fewer than two points, or three for a
        /// closed one.
        /// </summary>
        private static DxfSpline ControlSpline(
            CadCommandContext c,
            IReadOnlyList<GeoPoint2> points,
            int degree,
            bool close
        )
        {
            if (points.Count < (close ? 3 : 2))
            {
                return null;
            }

            List<GeoPoint3> polygon = points.Select(P3).ToList();
            DxfSpline spline;

            if (close)
            {
                spline = new DxfSpline();
                spline.SetPeriodicControlPoints(degree, polygon);
                return Current(c, spline);
            }

            int p = Math.Min(degree, polygon.Count - 1);
            var knots = new List<double>(polygon.Count + p + 1);

            for (int i = 0; i <= p; i++)
            {
                knots.Add(0.0);
            }

            for (int i = 1; i < polygon.Count - p; i++)
            {
                knots.Add(i);
            }

            for (int i = 0; i <= p; i++)
            {
                knots.Add(polygon.Count - p);
            }

            spline = new DxfSpline(p, false, false, false, polygon, knots, null, 1E-10, 1E-10);
            return Current(c, spline);
        }

        /// <summary>
        /// The Object option: "Select spline-fit polyline:" until Enter, each 2D polyline fitted with a spline (PEDIT Spline)
        /// replaced by the spline on its frame, with the polyline's layer, colour and look, all in one undo step. What else was
        /// picked, a polyline that is not so fitted among it, is "Only spline fitted polylines can be converted to splines."
        /// and "Unable to convert the selected object." (or "Unable to convert 3 selected objects."); nothing picked is "None
        /// found.".
        /// </summary>
        private static IEnumerable<CadPrompt> SplineObjects(CadCommandContext c)
        {
            yield return CadPrompt.ForSelection("Select spline-fit polyline");
            IReadOnlyList<string> picked =
                c.Input.Kind == CadInputKind.Selection ? c.Input.Handles : Array.Empty<string>();

            if (picked.Count == 0)
            {
                c.Write("None found.");
                yield break;
            }

            var convert = new List<string>();
            int refused = 0;

            foreach (string handle in picked.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (
                    c.Document.FindEntity(handle) is DxfPolyline2d polyline
                    && (
                        polyline.PolyType == DxfPoly2dType.QuadSplinePoly
                        || polyline.PolyType == DxfPoly2dType.CubicSplinePoly
                    )
                )
                {
                    if (CanChange(c, handle, "converted"))
                    {
                        convert.Add(handle);
                    }
                }
                else
                {
                    refused++;
                }
            }

            if (convert.Count > 0)
            {
                c.Apply(convert.Select(h => new CadEditRecord(CadEditRecordKind.Erased, h)).ToList(), s =>
                {
                    foreach (string handle in convert)
                    {
                        var polyline = (DxfPolyline2d)s.Document.FindEntity(handle);
                        DxfSpline spline = polyline.Spline;
                        spline.SetPropertiesFrom(polyline);
                        CadSpace where = s.Document.FindSpace(handle);
                        polyline.Erase();
                        s.Append(where, spline);
                    }
                });
            }

            if (refused > 0)
            {
                c.Write("Only spline fitted polylines can be converted to splines.");
                c.Write(
                    refused == 1
                        ? "Unable to convert the selected object."
                        : "Unable to convert " + refused.ToString(CultureInfo.InvariantCulture) + " selected objects."
                );
            }
        }
    }
}

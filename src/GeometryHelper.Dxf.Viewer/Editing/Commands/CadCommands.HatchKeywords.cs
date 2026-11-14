using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The keywords of -HATCH that set what the hatch is made of and looks like, asked as AutoCAD 2021 asks them: draW boundary,
    /// remove Boundaries, Origin, ANnotative, hatch COlor, LAyer and Transparency; and Advanced's Boundary set and Gap
    /// tolerance. What a keyword sets that AutoCAD keeps in a variable (HPCOLOR, HPLAYER, HPTRANSPARENCY, HPORIGIN, HPGAPTOL...)
    /// is kept in the document's <see cref="CadHatchSettings"/> at once, as those variables are; the rest is the run's.
    /// </summary>
    internal static partial class CadCommands
    {
        private const string HatchDrawRetainKey = "hatch.draw.retain";
        private const string HatchOriginStoreKey = "hatch.origin.store";

        /// <summary>What one run of -HATCH holds that no variable keeps: the candidate set, an origin for this hatch alone, the boundaries drawn.</summary>
        private sealed class HatchRun
        {
            /// <summary>The handles Boundary set is limited to; null for every object of the space.</summary>
            public List<string> Candidates;

            /// <summary>The origin of "Set new origin" answered No: this run's hatches alone are laid from it.</summary>
            public GeoPoint2? OriginOnce;

            /// <summary>The loops draW boundary drew, in the order they were drawn, which no object makes.</summary>
            public readonly List<CadBoundaryLoop> Drawn = new List<CadBoundaryLoop>();

            /// <summary>The drawn loops the user left open: a loop not closed in the hatch, and a polyline kept of one open too.</summary>
            public readonly HashSet<CadBoundaryLoop> Open = new HashSet<CadBoundaryLoop>();

            /// <summary>The drawn loops of a shape left open on its first point: closed in the hatch, their polyline open and running back to that point.</summary>
            public readonly HashSet<CadBoundaryLoop> Reclosed = new HashSet<CadBoundaryLoop>();

            /// <summary>The drawn loops draW boundary was asked to retain: a polyline is kept of each.</summary>
            public readonly HashSet<CadBoundaryLoop> Retained = new HashSet<CadBoundaryLoop>();

            /// <summary>The drawn loops whose polyline has been made.</summary>
            public readonly HashSet<CadBoundaryLoop> Made = new HashSet<CadBoundaryLoop>();

            /// <summary>Whether a shape drawn closed no area and was left out: said once the hatch is asked for and there is none.</summary>
            public bool Unfilled;
        }

        private static readonly string[] HatchCorners =
        {
            "bottom Left",
            "bottom Right",
            "top rIght",
            "top lEft",
            "Center",
        };

        /// <summary>
        /// draW boundary: "Retain polyline boundary? [Yes/No] &lt;N&gt;:", "Specify start point:", then "Specify next point or
        /// [Arc/Length/Undo]:" (Close from the third corner) until Enter or Close, and "Specify start point for new boundary or
        /// &lt;Accept&gt;:" for another shape. The loops stay in the order the shapes were drawn, as AutoCAD keeps them, each
        /// as drawn (a shape drawn clockwise stays so); one wholly inside another is an island, and the loops are flagged by how
        /// deep they lie: External for none round them, Outermost for one, none for more. A shape closed, or left open on its
        /// first point, is a loop of the hatch; one left open is a loop that is not closed, kept as its own edges (a
        /// polyline of (60, 0), (80, 0) beside a square is one line edge, flagged NotClosed) and only beside a closed shape: open
        /// shapes alone make no hatch. Retained, each shape is a polyline made before the hatch, and where the settings are
        /// associative the hatch follows the polylines; not retained, associativity is turned off and AutoCAD leaves the setting
        /// off after it, as the presenter does. Length goes on along the way the last segment left its end (the tangent of an arc).
        /// Close with two points makes a hatch of the line and back, a loop of no area, as AutoCAD does.
        /// Arc goes on as AutoCAD's "Enter an arc boundary option [Angle/CEnter/CLose/Direction/Line/Radius/Second pt/Undo/Endpoint of
        /// arc] &lt;Endpoint&gt;:", with the options PLINE's arc mode has but widths: an arc leaves along the way the shape came (east
        /// at the first point), CLose closes it with an arc leaving that way, Line goes back to straight segments, and Undo takes
        /// the last segment off and goes on in line mode. A line from (0, 0) to (100, 0), an arc to (100, 50) and a line to (0, 50)
        /// closed is a loop of 5 981.75 square units, the half circle of radius 25 on the right side included.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchDrawBoundary(
            CadCommandContext c,
            CadHatchSettings settings,
            HatchRun run,
            List<IReadOnlyList<CadBoundaryLoop>> picks
        )
        {
            foreach (CadPrompt prompt in YesNoKept(c, "Retain polyline boundary?", HatchDrawRetainKey, 0.0))
            {
                yield return prompt;
            }

            bool retain = c.Remembered(HatchDrawRetainKey, 0.0) != 0.0;
            yield return CadPrompt.ForPoint("Specify start point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            var loops = new List<CadBoundaryLoop>();

            while (true)
            {
                var points = new List<GeoPoint2> { c.Input.Point };

                // The bulge of the segment leaving each point, nought for a line; the last stays nought unless Close drew an arc.
                var bulges = new List<double> { 0.0 };

                // The way the shape leaves each point, which an arc segment drawn next starts along: east at the first point, or the
                // way the line, arc or polyline drawn last left its end, as AutoCAD 2021 starts a first arc segment along it.
                var tangents = new List<GeoVector2>
                {
                    c.Remembered("draw.last", 0.0) == 0.0
                        ? new GeoVector2(1.0, 0.0)
                        : new GeoVector2(c.Remembered("draw.last.dx", 1.0), c.Remembered("draw.last.dy", 0.0)),
                };
                bool closed = false;
                bool arcs = false;

                while (true)
                {
                    GeoPoint2 last = points[points.Count - 1];
                    GeoVector2 along = tangents[tangents.Count - 1];
                    int segments = points.Count - 1;

                    if (arcs)
                    {
                        yield return CadPrompt
                            .ForPoint(
                                "Enter an arc boundary option",
                                last,
                                segments >= 1
                                    ? new[]
                                    {
                                        "Angle",
                                        "CEnter",
                                        "CLose",
                                        "Direction",
                                        "Line",
                                        "Radius",
                                        "Second pt",
                                        "Undo",
                                        "Endpoint of arc",
                                    }
                                    : new[]
                                    {
                                        "Angle",
                                        "CEnter",
                                        "Direction",
                                        "Line",
                                        "Radius",
                                        "Second pt",
                                        "Undo",
                                        "Endpoint of arc",
                                    },
                                "Endpoint"
                            )
                            .WithoutOr();
                    }
                    else
                    {
                        yield return CadPrompt.ForPoint(
                            "Specify next point",
                            last,
                            segments >= 1
                                ? new[] { "Arc", "Close", "Length", "Undo" }
                                : new[] { "Arc", "Length", "Undo" }
                        );
                    }

                    if (c.Input.Is("Arc"))
                    {
                        arcs = true;
                        continue;
                    }

                    if (c.Input.Is("Line"))
                    {
                        arcs = false;
                        continue;
                    }

                    if (c.Input.Is("Undo"))
                    {
                        if (segments == 0)
                        {
                            c.Write("All segments already undone.");
                            continue;
                        }

                        // AutoCAD takes the last segment off and goes on in line mode, whichever it was in.
                        points.RemoveAt(points.Count - 1);
                        tangents.RemoveAt(tangents.Count - 1);
                        bulges.RemoveAt(bulges.Count - 1);
                        bulges[bulges.Count - 1] = 0.0;
                        arcs = false;
                        continue;
                    }

                    // Enter at the arc prompt answers its default, which is no segment: the shape ends.
                    if (c.Input.Is("Endpoint of arc"))
                    {
                        break;
                    }

                    bool close = c.Input.Is("Close") || c.Input.Is("CLose");
                    GeoPoint2 next;
                    double sweep = 0.0;

                    if (close || c.Input.IsPoint)
                    {
                        next = close ? points[0] : c.Input.Point;

                        // The arc that leaves along the way the shape came and ends at the point; Ctrl held takes the other way.
                        if (arcs)
                        {
                            sweep = SweepLeaving(c.Input.Control ? along * -1.0 : along, next - last);
                        }
                    }
                    else if (!arcs && c.Input.Is("Length"))
                    {
                        yield return CadPrompt.ForDistance("Specify length of line", last);

                        if (c.Input.Kind != CadInputKind.Number || c.Input.Number <= 0.0)
                        {
                            continue;
                        }

                        next = last + along * c.Input.Number;
                    }
                    else if (arcs && !c.Input.IsNone)
                    {
                        var end = new PlineArcEnd();

                        foreach (CadPrompt prompt in PlineArc(c, c.Input.Text, last, along, end, new[] { 0.0, 0.0 }))
                        {
                            yield return prompt;
                        }

                        if (!end.Done)
                        {
                            continue;
                        }

                        next = end.End;
                        sweep = end.Sweep;
                    }
                    else
                    {
                        break;
                    }

                    GeoVector2 span = next - last;

                    // An arc segment ending where the shape leaves its last point, or at it, is no arc: AutoCAD says "*Invalid*". A
                    // line segment of no length is kept, as AutoCAD keeps it.
                    if (
                        span.Length <= c.Session.Tolerance.EqualPoint
                        || (arcs && Math.Abs(sweep) <= c.Session.Tolerance.EqualAngleRad)
                    )
                    {
                        if (arcs)
                        {
                            c.Write("*Invalid*");
                        }
                        else if (!close)
                        {
                            points.Add(next);
                            bulges.Add(0.0);
                            tangents.Add(along);
                        }
                        else if (points.Count == 2)
                        {
                            // Close after a line of no length ends the shape and makes no loop, as AutoCAD does.
                            closed = true;
                            break;
                        }

                        continue;
                    }

                    bulges[bulges.Count - 1] = Math.Tan(sweep / 4.0);

                    if (close)
                    {
                        closed = true;
                        break;
                    }

                    GeoVector2 leaving = span / span.Length;

                    if (arcs)
                    {
                        double outAngle = Math.Atan2(span.Y, span.X) + sweep / 2.0;
                        leaving = new GeoVector2(Math.Cos(outAngle), Math.Sin(outAngle));
                    }

                    points.Add(next);
                    bulges.Add(0.0);
                    tangents.Add(leaving);
                }

                // One point and Enter leaves draW boundary for the main prompt, keeping the shapes drawn, as AutoCAD does.
                if (points.Count == 1 && !closed)
                {
                    break;
                }

                // A shape left open on its first point is the closed loop of the corners before it, as AutoCAD 2021 reads it; the
                // polyline kept stays open and runs back to that point.
                bool reclosed = false;

                if (
                    !closed
                    && points.Count >= 4
                    && (points[points.Count - 1] - points[0]).Length <= c.Session.Tolerance.EqualPoint
                )
                {
                    points.RemoveAt(points.Count - 1);
                    bulges.RemoveAt(bulges.Count - 1);
                    closed = true;
                    reclosed = true;
                }

                // A shape of two points is a loop where its points are apart: closed, a line and back, a hatch of no area, as
                // AutoCAD makes it; open, a one-edge loop beside a closed shape.
                if (
                    points.Count >= 3
                    || (points.Count == 2 && (points[1] - points[0]).Length > c.Session.Tolerance.EqualPoint)
                )
                {
                    CadBoundaryLoop loop = CadBoundaryFinder.Corners(points, bulges, Array.Empty<string>());
                    loops.Add(loop);

                    if (!closed)
                    {
                        run.Open.Add(loop);
                    }
                    else if (reclosed)
                    {
                        run.Reclosed.Add(loop);
                    }
                }
                else
                {
                    run.Unfilled = true;
                }

                yield return CadPrompt.ForPoint("Specify start point for new boundary or <Accept>");

                if (!c.Input.IsPoint)
                {
                    break;
                }
            }

            if (loops.Count == 0)
            {
                yield break;
            }

            double tolerance = Math.Max(c.Session.Tolerance.EqualPoint, 1E-9);

            foreach (CadBoundaryLoop loop in loops)
            {
                loop.Depth = loops.Count(o => Nested(loop, o, tolerance));
            }

            run.Drawn.AddRange(loops);

            if (retain)
            {
                run.Retained.UnionWith(loops);
            }

            // Open shapes alone make no hatch: AutoCAD says so where the hatch is asked for, and still keeps their polylines.
            if (loops.All(run.Open.Contains))
            {
                run.Unfilled = true;
            }
            else
            {
                picks.Add(loops.AsReadOnly());
            }

            // A boundary that no object makes cannot be followed: AutoCAD turns associativity off and leaves it so; one retained
            // is followed through its polyline, and the setting is left as it was.
            if (!retain)
            {
                settings.Associative = false;
            }

            c.Document.HatchSettings = settings.Clone();
        }

        /// <summary>
        /// Whether a loop lies wholly inside another that closes more area, its edges touching the other's allowed, as AutoCAD
        /// reads a boundary drawn: a square of 60 holds one of 50 by 30 that shares its right edge, and does not hold a square
        /// of 50 that overlaps it with a corner inside. An open loop counts closed across.
        /// </summary>
        private static bool Nested(CadBoundaryLoop inner, CadBoundaryLoop outer, double tolerance)
        {
            if (inner == outer || outer.Area <= inner.Area + tolerance * tolerance)
            {
                return false;
            }

            IReadOnlyList<GeoPoint2> edge = inner.Outline;

            for (int i = 0; i < edge.Count; i++)
            {
                GeoPoint2 a = edge[i];
                GeoPoint2 b = edge[(i + 1) % edge.Count];

                if (
                    !InsideOrOn(outer.Outline, a, tolerance) || !InsideOrOn(outer.Outline, a + (b - a) / 2.0, tolerance)
                )
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Whether a point lies inside a closed run of points or on its edges, within a tolerance.</summary>
        private static bool InsideOrOn(IReadOnlyList<GeoPoint2> outline, GeoPoint2 point, double tolerance)
        {
            if (CadBoundaryFinder.Inside(outline, point))
            {
                return true;
            }

            for (int i = 0; i < outline.Count; i++)
            {
                GeoPoint2 a = outline[i];
                GeoVector2 along = outline[(i + 1) % outline.Count] - a;
                double length2 = along.X * along.X + along.Y * along.Y;
                GeoVector2 to = point - a;
                double t =
                    length2 <= 0.0 ? 0.0 : Math.Max(0.0, Math.Min(1.0, (to.X * along.X + to.Y * along.Y) / length2));
                GeoVector2 off = to - along * t;

                if (off.X * off.X + off.Y * off.Y <= tolerance * tolerance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Makes the polyline of every drawn loop that is to be retained and has none yet, in the current layer: closed, or open
        /// for a shape left open, or open and back on its first point for one left open there. Gives the polylines made, by loop.
        /// </summary>
        private static Dictionary<CadBoundaryLoop, DxfPolyline> AppendDrawnPolylines(
            CadCommandContext c,
            CadEditScope s,
            HatchRun run,
            IEnumerable<CadBoundaryLoop> loops
        )
        {
            var made = new Dictionary<CadBoundaryLoop, DxfPolyline>();

            foreach (CadBoundaryLoop loop in loops)
            {
                if (!run.Retained.Contains(loop) || !run.Made.Add(loop))
                {
                    continue;
                }

                DxfPolyline polyline = LoopPolyline(loop);

                if (run.Open.Contains(loop))
                {
                    polyline.Closed = false;
                }
                else if (run.Reclosed.Contains(loop))
                {
                    polyline.AddVertexAt(loop.Vertices.Count, loop.Vertices[0], 0.0, 0.0, 0.0);
                    polyline.Closed = false;
                }

                made[loop] = s.Append(c.Space, Current(c, polyline));
            }

            return made;
        }

        /// <summary>
        /// Keeps the polylines of the drawn loops that no hatch took, as one undo step: where the shapes drawn were open alone,
        /// or their hatch was undone. AutoCAD makes a polyline as the shape is drawn, hatch or none.
        /// </summary>
        private static void KeepStrayPolylines(CadCommandContext c, HatchRun run)
        {
            if (run.Retained.Any(l => !run.Made.Contains(l)))
            {
                CadEditResult result = c.Apply(null, s => AppendDrawnPolylines(c, s, run, run.Drawn));

                if (!result.Succeeded && result.Message != null)
                {
                    c.Write(result.Message);
                }
            }
        }

        /// <summary>
        /// remove Boundaries: "Select objects or [Add boundaries]:", the loops made of the objects selected taken out of the
        /// hatch (an area whose outer loop goes goes with it), "Select objects or [Add boundaries/Undo]:" again; Undo brings the
        /// last removal back, Add boundaries goes back to the point prompt, Enter ends.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchRemoveBoundaries(
            CadCommandContext c,
            List<IReadOnlyList<CadBoundaryLoop>> picks
        )
        {
            var taken = new Stack<List<IReadOnlyList<CadBoundaryLoop>>>();

            while (true)
            {
                yield return CadPrompt.ForSelection(
                    "Select objects",
                    taken.Count > 0 ? new[] { "Add boundaries", "Undo" } : new[] { "Add boundaries" }
                );

                if (c.Input.Is("Add boundaries"))
                {
                    yield break;
                }

                if (c.Input.Is("Undo"))
                {
                    if (taken.Count > 0)
                    {
                        picks.Clear();
                        picks.AddRange(taken.Pop());
                    }

                    continue;
                }

                if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
                {
                    yield break;
                }

                var wanted = new HashSet<string>(c.Input.Handles, StringComparer.OrdinalIgnoreCase);
                var after = new List<IReadOnlyList<CadBoundaryLoop>>();
                bool changed = false;

                foreach (IReadOnlyList<CadBoundaryLoop> pick in picks)
                {
                    List<CadBoundaryLoop> kept = pick.Where(l => !l.Sources.Any(wanted.Contains)).ToList();

                    if (kept.Count == pick.Count)
                    {
                        after.Add(pick);
                        continue;
                    }

                    changed = true;

                    if (kept.Any(l => l.Depth == 0))
                    {
                        after.Add(kept.AsReadOnly());
                    }
                }

                if (changed)
                {
                    taken.Push(new List<IReadOnlyList<CadBoundaryLoop>>(picks));
                    picks.Clear();
                    picks.AddRange(after);
                }
            }
        }

        /// <summary>
        /// Origin: "[Use current origin/Set new origin/Default to boundary extents] &lt;Use current origin&gt;:". Set new origin
        /// asks "Select point:" and "Store as default origin? [Yes/No] &lt;N&gt;:", the point kept for the next hatches where
        /// Yes and for this run's alone where No; Default to boundary extents asks the corner of the box round the hatch, or its
        /// middle, "[bottom Left/bottom Right/top rIght/top lEft/Center]", and lays each hatch's pattern from it.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchOrigin(CadCommandContext c, CadHatchSettings settings, HatchRun run)
        {
            string[] choices = { "Use current origin", "Set new origin", "Default to boundary extents" };
            yield return CadPrompt.ForString(
                "[" + string.Join("/", choices) + "]",
                settings.OriginMode == CadHatchOriginMode.Current ? choices[0] : choices[2]
            );

            string chosen = c.Input.Kind == CadInputKind.Text ? Matched(c.Input.Text, choices) : null;

            if (chosen == choices[0])
            {
                settings.OriginMode = CadHatchOriginMode.Current;
                run.OriginOnce = null;
                c.Document.HatchSettings = settings.Clone();
            }
            else if (chosen == choices[1])
            {
                yield return CadPrompt.ForPoint("Select point");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 point = c.Input.Point;

                foreach (CadPrompt prompt in YesNoKept(c, "Store as default origin?", HatchOriginStoreKey, 0.0))
                {
                    yield return prompt;
                }

                if (c.Remembered(HatchOriginStoreKey, 0.0) != 0.0)
                {
                    settings.OriginX = point.X;
                    settings.OriginY = point.Y;
                    settings.OriginMode = CadHatchOriginMode.Current;
                    run.OriginOnce = null;
                    c.Document.HatchSettings = settings.Clone();
                }
                else
                {
                    run.OriginOnce = point;
                }
            }
            else if (chosen == choices[2])
            {
                CadHatchOriginMode mode =
                    settings.OriginMode == CadHatchOriginMode.Current
                        ? CadHatchOriginMode.BottomLeft
                        : settings.OriginMode;
                yield return CadPrompt.ForString(
                    "[" + string.Join("/", HatchCorners) + "]",
                    HatchCorners[(int)mode - 1]
                );

                string corner = c.Input.Kind == CadInputKind.Text ? Matched(c.Input.Text, HatchCorners) : null;

                if (corner != null)
                {
                    mode = (CadHatchOriginMode)(Array.IndexOf(HatchCorners, corner) + 1);
                }

                foreach (CadPrompt prompt in YesNoKept(c, "Store as default origin?", HatchOriginStoreKey, 0.0))
                {
                    yield return prompt;
                }

                // AutoCAD keeps the corner whichever way the question is answered: HPORIGINMODE is 5 after "Center" and "No".
                settings.OriginMode = mode;
                run.OriginOnce = null;
                c.Document.HatchSettings = settings.Clone();
            }
        }

        /// <summary>The word of a list a text names as AutoCAD reads a keyword: by its capital letters, the whole word, or a longer start of it; null for none.</summary>
        private static string Matched(string typed, string[] words) =>
            CadPrompt.ForKeyword(string.Empty, words).MatchKeyword(typed);

        /// <summary>
        /// ANnotative: "Create annotative hatch [Yes/No] &lt;N&gt;:", kept in the settings the hatches are made with; Yes makes
        /// each hatch annotative at the annotation scale of its space, as AutoCAD 2021 does (its pattern lines multiplied by
        /// 1 over the scale, a context of that scale under its extension dictionary).
        /// </summary>
        private static IEnumerable<CadPrompt> HatchAnnotative(CadCommandContext c, CadHatchSettings settings)
        {
            yield return CadPrompt.ForKeyword(
                "Create annotative hatch",
                new[] { "Yes", "No" },
                settings.Annotative ? "Y" : "N"
            );

            if (c.Input.Is("Yes"))
            {
                settings.Annotative = true;
            }
            else if (c.Input.Is("No"))
            {
                settings.Annotative = false;
            }
        }

        /// <summary>
        /// hatch COlor: "New color [Truecolor/COlorbook/. (for use current)] &lt;use current&gt;:" and "New background color
        /// [Truecolor/COlorbook/. (for none)] &lt;None&gt;:", each a name from red to white, an index 1 to 255, Truecolor and its
        /// "Red,Green,Blue:", ByLayer or ByBlock, or a dot for none; a colour that is none of them is said so and asked again.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchColors(CadCommandContext c, CadHatchSettings settings)
        {
            foreach (
                CadPrompt prompt in ColorAsked(
                    c,
                    "New color [Truecolor/COlorbook/. (for use current)]",
                    settings.Color == null ? "use current" : HatchColorOffered(settings.Color),
                    true,
                    true,
                    color => settings.Color = color
                )
            )
            {
                yield return prompt;
            }

            foreach (
                CadPrompt prompt in ColorAsked(
                    c,
                    "New background color [Truecolor/COlorbook/. (for none)]",
                    settings.BackgroundColor == null ? "None" : HatchColorOffered(settings.BackgroundColor),
                    true,
                    true,
                    color => settings.BackgroundColor = color
                )
            )
            {
                yield return prompt;
            }

            c.Document.HatchSettings = settings.Clone();
        }

        /// <summary>The colour as AutoCAD offers it in angle brackets: "1 (red)" for the seven named, the index for the rest, red, green and blue for a true colour.</summary>
        private static string HatchColorOffered(DxfColor color) =>
            color.IsByLayer ? "BYLAYER"
            : color.IsByBlock ? "BYBLOCK"
            : color.IsByColor ? CadPropertyEditor.ColorName(color)
            : color.ColorIndex >= 1 && color.ColorIndex <= 7
                ? color.ColorIndex.ToString(CultureInfo.InvariantCulture)
                    + " ("
                    + CadPropertyEditor.ColorName(color).ToLowerInvariant()
                    + ")"
            : color.ColorIndex.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Asks a colour as -HATCH's colour prompts ask it: a text with its keywords inside the message, Enter keeping what is
        /// offered, a dot where <paramref name="dot"/> takes it off (null), Truecolor asking "Red,Green,Blue:", COlorbook said
        /// not to be here, and an answer that is no colour said so and asked again.
        /// </summary>
        private static IEnumerable<CadPrompt> ColorAsked(
            CadCommandContext c,
            string message,
            string offered,
            bool dot,
            bool byLayerOrBlock,
            Action<DxfColor> chosen
        )
        {
            while (true)
            {
                yield return CadPrompt.ForString(message, offered);

                if (c.Input.Kind != CadInputKind.Text)
                {
                    yield break;
                }

                string text = c.Input.Text.Trim();

                if (dot && text == ".")
                {
                    chosen(null);
                    yield break;
                }

                if (text.Length >= 2 && "COlorbook".StartsWith(text, StringComparison.OrdinalIgnoreCase))
                {
                    c.Write("Color books are not available here.");
                    continue;
                }

                if (text.Length >= 1 && "Truecolor".StartsWith(text, StringComparison.OrdinalIgnoreCase))
                {
                    yield return CadPrompt.ForString("Red,Green,Blue");
                    text = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

                    if (text.Length == 0)
                    {
                        yield break;
                    }
                }

                string error = CadPropertyEditor.ParseColor(text, out DxfColor color);

                if (error == null && (byLayerOrBlock || !(color.IsByLayer || color.IsByBlock)))
                {
                    chosen(color);
                    yield break;
                }

                c.Write("A color number or standard color name is required.");
            }
        }

        /// <summary>
        /// LAyer: "Specify layer or [. (for use current)] &lt;use current&gt;:", a layer of the drawing by its name in either case,
        /// or a dot for the current layer; a name the drawing has not is said so, "Cannot find layer "NOSUCH".", and asked again.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchLayer(CadCommandContext c, CadHatchSettings settings)
        {
            while (true)
            {
                yield return CadPrompt.ForString(
                    "Specify layer or [. (for use current)]",
                    settings.Layer ?? "use current"
                );

                if (c.Input.Kind != CadInputKind.Text)
                {
                    yield break;
                }

                string name = c.Input.Text.Trim();

                if (name == ".")
                {
                    settings.Layer = null;
                }
                else if (c.Database.LayerTable.Has(name))
                {
                    settings.Layer = c.Database.LayerTable[name].Name;
                }
                else
                {
                    c.Write("Cannot find layer \"" + name + "\".");
                    continue;
                }

                c.Document.HatchSettings = settings.Clone();
                yield break;
            }
        }

        /// <summary>
        /// Transparency: "Specify transparency value (0-90) or [. (for use current)] &lt;use current&gt;:", a whole number from 0
        /// to 90 per cent, ByLayer or ByBlock, or a dot for the current setting; the alpha is that of the layer's transparency,
        /// 255 times what is left, rounded down. Anything else is said so and asked again.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchTransparency(CadCommandContext c, CadHatchSettings settings)
        {
            DxfTransparency? now = settings.Transparency;
            string offered =
                !now.HasValue ? "use current"
                : now.Value.IsByLayer ? "ByLayer"
                : now.Value.IsByBlock ? "ByBlock"
                : ((int)Math.Round((255 - now.Value.Alpha) * 100.0 / 255.0)).ToString(CultureInfo.InvariantCulture);

            while (true)
            {
                yield return CadPrompt.ForString("Specify transparency value (0-90) or [. (for use current)]", offered);

                if (c.Input.Kind != CadInputKind.Text)
                {
                    yield break;
                }

                string text = c.Input.Text.Trim();

                if (text == ".")
                {
                    settings.Transparency = null;
                }
                else if (string.Equals(text, "ByLayer", StringComparison.OrdinalIgnoreCase))
                {
                    settings.Transparency = DxfTransparency.ByLayer;
                }
                else if (string.Equals(text, "ByBlock", StringComparison.OrdinalIgnoreCase))
                {
                    settings.Transparency = DxfTransparency.ByBlock;
                }
                else if (
                    int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int percent)
                    && percent >= 0
                    && percent <= 90
                )
                {
                    settings.Transparency = new DxfTransparency((byte)Math.Floor(255.0 * (100 - percent) / 100.0));
                }
                else
                {
                    c.Write("Requires an integer between 0 and 90 or ByLayer/ByBlock.");
                    continue;
                }

                c.Document.HatchSettings = settings.Clone();
                yield break;
            }
        }

        /// <summary>
        /// Boundary set, of Advanced: "Specify candidate set for boundary [New/Everything] &lt;Everything&gt;:"; New asks "Select
        /// objects:" and then looks for an area among those alone, Everything among every object of the space. A point whose area
        /// the set does not hold is told "Valid hatch boundary not found." and the search goes on among every object, as AutoCAD
        /// 2021's console does: with the set of one square, a point in another square hatches that one all the same.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchBoundarySet(CadCommandContext c, HatchRun run)
        {
            yield return CadPrompt.ForKeyword(
                "Specify candidate set for boundary",
                new[] { "New", "Everything" },
                "Everything"
            );

            if (c.Input.Is("Everything"))
            {
                run.Candidates = null;
            }
            else if (c.Input.Is("New"))
            {
                yield return CadPrompt.ForSelection();

                if (c.Input.Kind == CadInputKind.Selection && c.Input.Handles.Count > 0)
                {
                    run.Candidates = c.Input.Handles.ToList();
                    c.Write("Analyzing the selected data...");
                }
            }
        }

        /// <summary>
        /// Gap tolerance, of Advanced: "Specify a boundary gap tolerance value &lt;0&gt;:", from 0 to 5000, kept as HPGAPTOL is
        /// and said, "HPGAPTOL set to 3"; a value outside is refused, "Value must be between 0 and 5000.", and asked again.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchGapTolerance(CadCommandContext c, CadHatchSettings settings)
        {
            while (true)
            {
                yield return CadPrompt.ForString(
                    "Specify a boundary gap tolerance value",
                    settings.GapTolerance.ToString("0.####", CultureInfo.InvariantCulture)
                );

                if (c.Input.Kind != CadInputKind.Text)
                {
                    yield break;
                }

                if (
                    !double.TryParse(
                        c.Input.Text.Trim(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double gap
                    )
                    || double.IsNaN(gap)
                    || double.IsInfinity(gap)
                )
                {
                    c.Write("Requires numeric value.");
                    continue;
                }

                if (gap < 0.0 || gap > 5000.0)
                {
                    c.Write("Value must be between 0 and 5000.");
                    continue;
                }

                settings.GapTolerance = gap;
                c.Document.HatchSettings = settings.Clone();
                c.Write("HPGAPTOL set to " + gap.ToString("0.####", CultureInfo.InvariantCulture));
                yield break;
            }
        }

        /// <summary>
        /// The settings a hatch is laid from: the origin of this run where "Set new origin" was answered No, or the corner or
        /// middle of the box round the outer loops where the mode names one; the settings themselves where neither does.
        /// </summary>
        private static CadHatchSettings WithOrigin(
            CadHatchSettings settings,
            IReadOnlyList<CadBoundaryLoop> loops,
            GeoPoint2? once
        )
        {
            if (once.HasValue)
            {
                CadHatchSettings own = settings.Clone();
                own.OriginMode = CadHatchOriginMode.Current;
                own.OriginX = once.Value.X;
                own.OriginY = once.Value.Y;
                return own;
            }

            if (settings.OriginMode == CadHatchOriginMode.Current)
            {
                return settings;
            }

            IEnumerable<CadBoundaryLoop> outer = loops.Where(l => l.Depth == 0);
            IEnumerable<GeoPoint2> corners = (outer.Any() ? outer : loops).SelectMany(l => l.Outline);

            if (!corners.Any())
            {
                return settings;
            }

            double minX = corners.Min(p => p.X);
            double maxX = corners.Max(p => p.X);
            double minY = corners.Min(p => p.Y);
            double maxY = corners.Max(p => p.Y);
            CadHatchSettings boxed = settings.Clone();
            boxed.OriginMode = CadHatchOriginMode.Current;

            switch (settings.OriginMode)
            {
                case CadHatchOriginMode.BottomLeft:
                    boxed.OriginX = minX;
                    boxed.OriginY = minY;
                    break;
                case CadHatchOriginMode.BottomRight:
                    boxed.OriginX = maxX;
                    boxed.OriginY = minY;
                    break;
                case CadHatchOriginMode.TopRight:
                    boxed.OriginX = maxX;
                    boxed.OriginY = maxY;
                    break;
                case CadHatchOriginMode.TopLeft:
                    boxed.OriginX = minX;
                    boxed.OriginY = maxY;
                    break;
                default:
                    boxed.OriginX = (minX + maxX) / 2.0;
                    boxed.OriginY = (minY + maxY) / 2.0;
                    break;
            }

            return boxed;
        }

        /// <summary>
        /// Puts a hatch just made in the draw order <paramref name="order"/> names (an index of <see cref="HatchOrders"/>): to the
        /// back or the front, or behind or in front of the lowest or topmost object of its boundary; nowhere for do Not assign,
        /// and where its loops are made of no object (a boundary drawn) it stays on top.
        /// </summary>
        private static void PlaceHatch(
            CadCommandContext c,
            CadEditScope s,
            DxfHatch hatch,
            IEnumerable<CadBoundaryLoop> loops,
            int order
        )
        {
            if (order == 0)
            {
                return;
            }

            DxfDrawOrderTable table = c.Space.Block.CreateDrawOrderTable();
            var moving = new List<DxfEntity> { hatch };

            if (order == 1)
            {
                table.MoveToBottom(moving);
            }
            else if (order == 2)
            {
                table.MoveToTop(moving);
            }
            else
            {
                List<DxfEntity> boundary = loops
                    .SelectMany(l => l.Sources)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(h => s.Document.FindEntity(h))
                    .Where(e => e != null)
                    .ToList();
                DxfEntity target = Extreme(c.Space.Block, boundary, order == 4);

                if (target != null && order == 3)
                {
                    table.MoveBelow(moving, target);
                }
                else if (target != null)
                {
                    table.MoveAbove(moving, target);
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>SPLINEDIT in AutoCAD's words: Fit data, Close and Open, Join, Move vertex, Refine, rEverse, convert to Polyline, Undo and eXit.</summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// SPLINEDIT: "Select spline:" (a pick that is no spline asks again), then "Enter an option [Fit data/Close/Join/Move
        /// vertex/Refine/rEverse/convert to Polyline/Undo/eXit] &lt;eXit&gt;:" until Enter, Fit data offered for a spline of fit
        /// points, Open in place of Close for a closed one and Join only for an open one. Fit data asks "Enter a fit data option
        /// [Add/Close/Delete/Kink/Move/Purge/Tangents/toLerance/eXit]" (see <see cref="SplineFitData"/>). Close makes a spline of
        /// fit points the periodic cubic AutoCAD makes through its points (<see cref="DxfFitData.Periodic"/>) and one of control
        /// points the periodic one of those points with uniform knots; Open gives the open cubic back. Join asks "Select any
        /// open curves to join to source:", takes what meets the spline end to end and says "1 object joined to source". Move
        /// vertex moves a control vertex, a spline of fit points losing its fit data, as AutoCAD's does, and asks "Specify new
        /// location or [Next/Previous/Select point/eXit] &lt;N&gt;:". Refine asks "Enter a vertex editing option
        /// [Add/Delete/Elevate order/Move/Weight/eXit]" (see <see cref="SplineRefine"/>). rEverse turns the spline round,
        /// "Spline has been reversed.". convert to Polyline asks "Specify a precision &lt;10&gt;:", replaces the spline by the
        /// polyline of lines within it and ends the command. Undo takes back the last change. Each change is a step of undo
        /// within the command, the whole one step.
        /// </summary>
        private static IEnumerable<CadPrompt> SplineEdit(CadCommandContext c)
        {
            string handle = null;

            while (handle == null)
            {
                yield return CadPrompt.ForEntity("Select spline");
                string picked = c.Input.Handles.FirstOrDefault();

                if (picked == null)
                {
                    yield break;
                }

                if (!(c.Document.FindEntity(picked) is DxfSpline))
                {
                    continue;
                }

                if (CanChange(c, picked, "edited"))
                {
                    handle = picked;
                }
            }

            var marks = new List<CadGroupMark>();

            while (true)
            {
                if (!(c.Document.FindEntity(handle) is DxfSpline spline))
                {
                    yield break;
                }

                var options = new List<string>();

                if (spline.HasFitData)
                {
                    options.Add("Fit data");
                }

                options.Add(spline.Closed ? "Open" : "Close");

                if (!spline.Closed)
                {
                    options.Add("Join");
                }

                options.AddRange(new[] { "Move vertex", "Refine", "rEverse", "convert to Polyline", "Undo", "eXit" });
                yield return CadPrompt.ForKeyword("Enter an option", options, "eXit");

                if (c.Input.IsNone || c.Input.Is("eXit"))
                {
                    break;
                }

                if (c.Input.Is("Undo"))
                {
                    PeditUndo(c, marks);
                }
                else if (c.Input.Is("Fit data"))
                {
                    foreach (CadPrompt prompt in SplineFitData(c, handle, marks))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Close") || c.Input.Is("Open"))
                {
                    SplineCloseOrOpen(c, handle, marks, c.Input.Is("Close"));
                }
                else if (c.Input.Is("Join"))
                {
                    yield return CadPrompt.ForSelection("Select any open curves to join to source");

                    if (c.Input.Kind == CadInputKind.Selection)
                    {
                        SplineJoin(c, handle, marks, c.Input.Handles);
                    }
                }
                else if (c.Input.Is("Refine"))
                {
                    foreach (CadPrompt prompt in SplineRefine(c, handle, marks))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("rEverse"))
                {
                    if (PeditEach(c, new[] { handle }, "Reverse", marks, (s, curve) => curve.ReverseCurve()))
                    {
                        c.Write("Spline has been reversed.");
                    }
                }
                else if (c.Input.Is("Move vertex"))
                {
                    foreach (CadPrompt prompt in SplineVertices(c, handle, marks, SplineVertexKind.MoveControl))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("convert to Polyline"))
                {
                    yield return CadPrompt.ForInteger("Specify a precision", null, "10");
                    int precision = c.Input.Kind == CadInputKind.Number ? (int)c.Input.Number : 10;

                    if (precision < 0 || precision > 99)
                    {
                        c.Write("Requires an integer between 0 and 99.");
                        continue;
                    }

                    SplineToPolyline(c, handle, precision);
                    yield break;
                }
                else
                {
                    c.Write("Invalid option keyword.");
                }
            }
        }

        /// <summary>
        /// Close and Open of the top menu: a spline of fit points closed through its first point or opened again
        /// (<see cref="SplineClosed"/>), one of control points closed or opened as <see cref="DxfSpline"/> does it
        /// (uniform knots, AutoCAD's periodic form).
        /// </summary>
        private static void SplineCloseOrOpen(CadCommandContext c, string handle, List<CadGroupMark> marks, bool close)
        {
            PeditEach(c, new[] { handle }, close ? "Close" : "Open", marks, (s, curve) =>
            {
                var spline = (DxfSpline)curve;

                if (spline.HasFitData)
                {
                    SplineClosed(spline, close, c.Session.Tolerance);
                }
                else if (close)
                {
                    spline.ClosePeriodic();
                }
                else
                {
                    spline.OpenPeriodic();
                }
            });
        }

        /// <summary>
        /// A spline of fit points closed through its first point, the periodic cubic AutoCAD makes (its first point again as
        /// the last), or opened again: the cubic through the points without that last one, as AutoCAD gives back (the library
        /// keeps a closed spline closed, so the open one is fitted on a spline of its own and its data given to this one).
        /// </summary>
        private static void SplineClosed(DxfSpline spline, bool close, Tolerance tolerance)
        {
            DxfFitData data = spline.FitData;

            if (close)
            {
                data.Periodic = true;
                spline.FitData = data;
                return;
            }

            List<GeoPoint3> points = data.GetFitPoints().ToList();

            if (points.Count > 2 && points[0].DistanceTo(points[points.Count - 1]) <= tolerance.EqualPoint)
            {
                points.RemoveAt(points.Count - 1);
            }

            var open = new DxfSpline();
            open.FitData = new DxfFitData(points, data.Degree, data.FitTolerance) { KnotParam = data.KnotParam };
            spline.NurbsData = open.NurbsData;
            spline.FitData = open.FitData;
        }

        /// <summary>
        /// Join: what of the curves picked is open and meets the spline joins into it, as JOIN joins them into a spline picked
        /// first (the spline's fit data go, as it is made of NURBS); says "1 object joined to source", and where some were
        /// not taken ", 1 object discarded from the operation".
        /// </summary>
        private static void SplineJoin(
            CadCommandContext c,
            string handle,
            List<CadGroupMark> marks,
            IReadOnlyList<string> picked
        )
        {
            var others = new List<string>();
            int discarded = 0;

            foreach (string other in picked.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.Equals(other, handle, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (CanChange(c, other, "joined") && Joinable(c.Document.FindEntity(other)))
                {
                    others.Add(other);
                }
                else
                {
                    discarded++;
                }
            }

            int joined = 0;

            if (others.Count > 0)
            {
                PeditEach(c, new[] { handle }, "Join", marks, (s, curve) =>
                {
                    List<DxfEntity> found = others.Select(h => s.Document.FindEntity(h)).ToList();
                    IReadOnlyList<int> taken = curve.JoinEntities(found);
                    joined = taken.Count;
                    discarded += others.Count - joined;
                    var alive = new List<DxfCurve>();
                    var joinedAll = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (int i in taken)
                    {
                        Gone(s, (DxfCurve)found[i], alive, joinedAll);
                    }
                });
            }

            c.Write(
                joined.ToString(CultureInfo.InvariantCulture)
                    + (joined == 1 ? " object" : " objects")
                    + " joined to source"
                    + (
                        discarded > 0
                            ? ", "
                                + discarded.ToString(CultureInfo.InvariantCulture)
                                + (discarded == 1 ? " object" : " objects")
                                + " discarded from the operation"
                            : string.Empty
                    )
            );
        }

        /// <summary>
        /// Fit data: "Enter a fit data option [Add/Close/Delete/Kink/Move/Purge/Tangents/toLerance/eXit] &lt;eXit&gt;:" until
        /// eXit, with Open in place of Close for a closed spline. Add asks "Specify existing fit point on spline &lt;exit&gt;:"
        /// and then new points ("Specify new fit point to add &lt;exit&gt;:") each put in after the one before; at the first
        /// point it asks "Specify new point or [After/Before] &lt;exit&gt;:", a point typed going before it. Delete takes out
        /// the fit point picked, Kink puts a knot three times at the point of the spline nearest a point picked, so that the
        /// curve bends there with a corner, Move moves the marked fit point and keeps it marked, as Move vertex does, Purge takes
        /// the fit data out and goes back to the top menu, Tangents asks the start and end tangent by a point each ("System
        /// default" at the end taking both away) and toLerance the fit tolerance. A point that is no fit point is "Not a fit
        /// point.". Every change fits the spline through its points anew, as <see cref="DxfSpline.FitData"/> does.
        /// </summary>
        private static IEnumerable<CadPrompt> SplineFitData(
            CadCommandContext c,
            string handle,
            List<CadGroupMark> marks
        )
        {
            while (true)
            {
                if (!(c.Document.FindEntity(handle) is DxfSpline spline) || !spline.HasFitData)
                {
                    yield break;
                }

                yield return CadPrompt.ForKeyword(
                    "Enter a fit data option",
                    new[]
                    {
                        "Add",
                        spline.Closed ? "Open" : "Close",
                        "Delete",
                        "Kink",
                        "Move",
                        "Purge",
                        "Tangents",
                        "toLerance",
                        "eXit",
                    },
                    "eXit"
                );

                if (c.Input.IsNone || c.Input.Is("eXit"))
                {
                    yield break;
                }

                if (c.Input.Is("Add"))
                {
                    foreach (CadPrompt prompt in SplineFitAdd(c, handle, marks))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Close") || c.Input.Is("Open"))
                {
                    SplineCloseOrOpen(c, handle, marks, c.Input.Is("Close"));
                }
                else if (c.Input.Is("Delete"))
                {
                    while (true)
                    {
                        yield return CadPrompt.ForPoint("Specify existing fit point on spline <exit>");

                        if (!c.Input.IsPoint)
                        {
                            break;
                        }

                        spline = (DxfSpline)c.Document.FindEntity(handle);
                        int index = SplineNearestFit(c, spline, c.Input.Point);

                        if (index < 0)
                        {
                            c.Write("Not a fit point.");
                        }
                        else if (spline.NumFitPoints > 2)
                        {
                            PeditEach(c, new[] { handle }, "Delete fit point", marks, (s, curve) =>
                                ((DxfSpline)curve).RemoveFitPointAt(index)
                            );
                        }
                    }
                }
                else if (c.Input.Is("Kink"))
                {
                    while (true)
                    {
                        yield return CadPrompt.ForPoint("Specify a point on the spline <exit>");

                        if (!c.Input.IsPoint)
                        {
                            break;
                        }

                        GeoPoint2 at = c.Input.Point;
                        PeditEach(c, new[] { handle }, "Kink", marks, (s, curve) => SplineKink((DxfSpline)curve, at));
                    }
                }
                else if (c.Input.Is("Move"))
                {
                    foreach (CadPrompt prompt in SplineVertices(c, handle, marks, SplineVertexKind.MoveFit))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Purge"))
                {
                    PeditEach(c, new[] { handle }, "Purge", marks, (s, curve) => ((DxfSpline)curve).PurgeFitData());
                    yield break;
                }
                else if (c.Input.Is("Tangents"))
                {
                    foreach (CadPrompt prompt in SplineTangents(c, handle, marks))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("toLerance"))
                {
                    yield return CadPrompt.ForDistance(
                        "Enter fit tolerance",
                        null,
                        null,
                        SplineNumber(spline.FitTolerance)
                    );

                    if (c.Input.Kind == CadInputKind.Number && c.Input.Number >= 0.0)
                    {
                        double tolerance = c.Input.Number;
                        PeditEach(c, new[] { handle }, "Fit tolerance", marks, (s, curve) =>
                        {
                            var fit = (DxfSpline)curve;
                            DxfFitData data = fit.FitData;
                            data.FitTolerance = tolerance;
                            fit.FitData = data;
                        });
                    }
                }
            }
        }

        /// <summary>A number as the prompts of SPLINEDIT show a current value: four decimals, and a tiny one in powers of ten, 1.0000E-10.</summary>
        private static string SplineNumber(double value) =>
            value != 0.0 && Math.Abs(value) < 1E-4
                ? value.ToString("0.0000E-00", CultureInfo.InvariantCulture)
                : value.ToString("0.0000", CultureInfo.InvariantCulture);

        /// <summary>The index of the fit point nearest a point within the pick aperture; -1 where there is none.</summary>
        private static int SplineNearestFit(CadCommandContext c, DxfSpline spline, GeoPoint2 at)
        {
            int found = -1;
            double best = c.Session.PickAperture;

            for (int i = 0; i < spline.NumFitPoints; i++)
            {
                GeoPoint3 p = spline.GetFitPointAt(i);
                double d = new GeoPoint2(p.X, p.Y).DistanceTo(at);

                if (d <= best)
                {
                    best = d;
                    found = i;
                }
            }

            return found;
        }

        /// <summary>
        /// Fit data Add: the existing fit point picked, then the points put in after it, each after the one before. At the
        /// first point of an open spline the first answer is "Specify new point or [After/Before] &lt;exit&gt;:", a point
        /// typed going before it and each next before the one before that, After putting them after.
        /// </summary>
        private static IEnumerable<CadPrompt> SplineFitAdd(CadCommandContext c, string handle, List<CadGroupMark> marks)
        {
            while (true)
            {
                yield return CadPrompt.ForPoint("Specify existing fit point on spline <exit>");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                var spline = (DxfSpline)c.Document.FindEntity(handle);
                int index = SplineNearestFit(c, spline, c.Input.Point);

                if (index < 0)
                {
                    c.Write("Not a fit point.");
                    continue;
                }

                bool after = true;
                bool pending = false;
                GeoPoint2 first = default;

                if (index == 0 && !spline.Closed)
                {
                    yield return CadPrompt.ForPoint("Specify new point", null, new[] { "After", "Before" }, "exit");

                    if (c.Input.IsPoint)
                    {
                        after = false;
                        pending = true;
                        first = c.Input.Point;
                    }
                    else if (c.Input.Is("After"))
                    {
                        after = true;
                    }
                    else if (c.Input.Is("Before"))
                    {
                        after = false;
                    }
                    else
                    {
                        continue;
                    }
                }

                int at = after ? index + 1 : index;

                while (true)
                {
                    GeoPoint2 point;

                    if (pending)
                    {
                        point = first;
                        pending = false;
                    }
                    else
                    {
                        yield return CadPrompt.ForPoint("Specify new fit point to add <exit>");

                        if (!c.Input.IsPoint)
                        {
                            break;
                        }

                        point = c.Input.Point;
                    }

                    int where = at;
                    double z = spline.GetFitPointAt(index).Z;
                    PeditEach(c, new[] { handle }, "Add fit point", marks, (s, curve) =>
                        ((DxfSpline)curve).InsertFitPointAt(where, new GeoPoint3(point.X, point.Y, z))
                    );

                    if (after)
                    {
                        at++;
                    }
                }
            }
        }

        /// <summary>
        /// Kink: a knot put in at the point of the spline nearest a point picked until it stands as many times as the degree
        /// (a knot the spline has already counted in), the curve kept and a corner allowed there, as AutoCAD's: the cubic given
        /// the knot of its fit point 2 had knots 2, 2, 2 and 2 + 1E-8 and the control point there twice, and given a knot that
        /// was none, three of it; the fit data become the points at the knots.
        /// </summary>
        private static void SplineKink(DxfSpline spline, GeoPoint2 at)
        {
            const double Offset = 1E-8;
            GeoPoint3 near = spline.GetClosestPointTo(new GeoPoint3(at.X, at.Y, spline.GetControlPointAt(0).Z), false);
            double param = spline.GetParameterAtPoint(near);

            // A knot the spline has within the knot tolerance is that knot, as the nearest point of the curve is by rounding.
            foreach (double knot in spline.Knots)
            {
                if (Math.Abs(knot - param) <= 1E-9)
                {
                    param = knot;
                    break;
                }
            }

            if (!(param > spline.StartParam && param < spline.EndParam))
            {
                return;
            }

            int degree = spline.Degree;

            for (int i = 0; i < degree; i++)
            {
                int stands = spline.Knots.Count(k => k == param);
                spline.InsertKnot(stands < degree ? param : param + Offset);
            }
        }

        /// <summary>
        /// Tangents: "Specify start tangent or [System default]:" and "Specify end tangent or [System default]:", a point
        /// each, the start tangent running from the point to the first fit point and the end from the last fit point to the
        /// point, as the rubber band of AutoCAD's runs. System default at the start leaves the start free; at the end it takes
        /// both tangents away, as AutoCAD 2021 did with a start tangent given.
        /// </summary>
        private static IEnumerable<CadPrompt> SplineTangents(
            CadCommandContext c,
            string handle,
            List<CadGroupMark> marks
        )
        {
            var spline = (DxfSpline)c.Document.FindEntity(handle);
            GeoPoint3 startAt = spline.GetFitPointAt(0);
            GeoPoint3 endAt = spline.GetFitPointAt(spline.NumFitPoints - 1);
            GeoVector3 start = default;
            GeoVector3 end = default;
            yield return CadPrompt.ForPoint("Specify start tangent", null, new[] { "System default" });

            if (c.Input.IsPoint)
            {
                start = new GeoVector3(startAt.X - c.Input.Point.X, startAt.Y - c.Input.Point.Y, 0.0);
            }
            else if (!c.Input.Is("System default"))
            {
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify end tangent", null, new[] { "System default" });

            if (c.Input.IsPoint)
            {
                end = new GeoVector3(c.Input.Point.X - endAt.X, c.Input.Point.Y - endAt.Y, 0.0);
            }
            else if (c.Input.Is("System default"))
            {
                start = default;
            }
            else
            {
                yield break;
            }

            PeditEach(c, new[] { handle }, "Tangents", marks, (s, curve) =>
            {
                var tangent = (DxfSpline)curve;
                DxfFitData data = tangent.FitData;
                data.StartTangent = start.Length > 1E-9 ? start / start.Length : default;
                data.EndTangent = end.Length > 1E-9 ? end / end.Length : default;
                data.TangentsExist = start.Length > 1E-9 || end.Length > 1E-9;
                tangent.FitData = data;
            });
        }

        /// <summary>
        /// Refine: "Enter a vertex editing option [Add/Delete/Elevate order/Move/Weight/eXit] &lt;eXit&gt;:" until eXit. The
        /// fit data go with the first change (a spline asked and left alone keeps them). Add asks "Specify a point on the
        /// spline &lt;exit&gt;:" and puts a knot in at the nearest point of the curve, the curve kept and a control point
        /// more; Delete asks "Specify control vertex to delete:" and takes the vertex picked out ("Not a control vertex." for
        /// a pick that is none); Elevate order asks "Enter new order &lt;4&gt;:", the degree and one, up to 26 as in AutoCAD;
        /// Move is Move vertex; Weight says "Spline is not rational. Will make it so." for one that is not and
        /// asks "Enter new weight (current = 1.0000) or [Next/Previous/Select point/eXit] &lt;N&gt;:" of each vertex.
        /// </summary>
        private static IEnumerable<CadPrompt> SplineRefine(CadCommandContext c, string handle, List<CadGroupMark> marks)
        {
            while (true)
            {
                if (!(c.Document.FindEntity(handle) is DxfSpline spline))
                {
                    yield break;
                }

                yield return CadPrompt.ForKeyword(
                    "Enter a vertex editing option",
                    new[] { "Add", "Delete", "Elevate order", "Move", "Weight", "eXit" },
                    "eXit"
                );

                if (c.Input.IsNone || c.Input.Is("eXit"))
                {
                    yield break;
                }

                if (c.Input.Is("Add"))
                {
                    while (true)
                    {
                        yield return CadPrompt.ForPoint("Specify a point on the spline <exit>");

                        if (!c.Input.IsPoint)
                        {
                            break;
                        }

                        GeoPoint2 at = c.Input.Point;
                        PeditEach(c, new[] { handle }, "Add control vertex", marks, (s, curve) =>
                        {
                            var refined = (DxfSpline)curve;
                            refined.PurgeFitData();
                            GeoPoint3 near = refined.GetClosestPointTo(
                                new GeoPoint3(at.X, at.Y, refined.GetControlPointAt(0).Z),
                                false
                            );
                            double param = refined.GetParameterAtPoint(near);

                            if (param > refined.StartParam && param < refined.EndParam)
                            {
                                refined.InsertKnot(param);
                            }
                        });
                    }
                }
                else if (c.Input.Is("Delete"))
                {
                    while (true)
                    {
                        yield return CadPrompt.ForPoint("Specify control vertex to delete");

                        if (!c.Input.IsPoint)
                        {
                            break;
                        }

                        spline = (DxfSpline)c.Document.FindEntity(handle);
                        int index = SplineNearestVertex(c, spline, c.Input.Point);

                        if (index < 0)
                        {
                            c.Write("Not a control vertex.");
                        }
                        else if (spline.NumControlPoints > spline.Degree + 1)
                        {
                            PeditEach(c, new[] { handle }, "Delete control vertex", marks, (s, curve) =>
                            {
                                var refined = (DxfSpline)curve;
                                refined.PurgeFitData();
                                refined.RemoveControlPointAt(index);
                            });
                        }
                    }
                }
                else if (c.Input.Is("Elevate order"))
                {
                    int order = spline.Degree + 1;

                    while (true)
                    {
                        yield return CadPrompt.ForInteger(
                            "Enter new order",
                            null,
                            order.ToString(CultureInfo.InvariantCulture)
                        );

                        int wanted = c.Input.Kind == CadInputKind.Number ? (int)c.Input.Number : order;

                        if (wanted < order || wanted > MostSplineOrder)
                        {
                            c.Write(
                                "Requires an integer between "
                                    + order.ToString(CultureInfo.InvariantCulture)
                                    + " and "
                                    + MostSplineOrder.ToString(CultureInfo.InvariantCulture)
                                    + "."
                            );
                            continue;
                        }

                        if (wanted > order)
                        {
                            PeditEach(c, new[] { handle }, "Elevate order", marks, (s, curve) =>
                            {
                                var refined = (DxfSpline)curve;
                                refined.PurgeFitData();
                                refined.ElevateDegree(wanted - 1);
                            });
                        }

                        break;
                    }
                }
                else if (c.Input.Is("Move"))
                {
                    foreach (CadPrompt prompt in SplineVertices(c, handle, marks, SplineVertexKind.MoveControl))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Weight"))
                {
                    foreach (CadPrompt prompt in SplineVertices(c, handle, marks, SplineVertexKind.Weight))
                    {
                        yield return prompt;
                    }
                }
            }
        }

        // The most order (the degree and one) the presenter raises a spline to: AutoCAD's 26, as the library's DxfSpline.ElevateDegree
        // does, the control points to 1E-7 of AutoCAD's; a spline of degree 25 is a GeoSpline3 like any other, so it is drawn.
        private const int MostSplineOrder = 26;

        /// <summary>The index of the control vertex nearest a point within the pick aperture; -1 where there is none.</summary>
        private static int SplineNearestVertex(CadCommandContext c, DxfSpline spline, GeoPoint2 at)
        {
            int found = -1;
            double best = c.Session.PickAperture;

            for (int i = 0; i < spline.NumControlPoints; i++)
            {
                GeoPoint3 p = spline.GetControlPointAt(i);
                double d = new GeoPoint2(p.X, p.Y).DistanceTo(at);

                if (d <= best)
                {
                    best = d;
                    found = i;
                }
            }

            return found;
        }

        /// <summary>What <see cref="SplineVertices"/> works on: the control vertices to move, the fit points to move, or the weights of the control vertices.</summary>
        private enum SplineVertexKind
        {
            MoveControl,
            MoveFit,
            Weight,
        }

        /// <summary>
        /// The prompts that go from vertex to vertex of a spline, the first at first, the one marked: "Specify new location or
        /// [Next/Previous/Select point/eXit] &lt;N&gt;:" for the control vertices or the fit points, a point moving the vertex
        /// (a control vertex of a spline of fit points taking its fit data away), and "Enter new weight (current = 1.0000) or
        /// [Next/Previous/Select point/eXit] &lt;N&gt;:" for the weights, a number setting the weight (the fit data go, and
        /// "Spline is not rational. Will make it so." is said once for a spline that is not). Next and Previous go round the
        /// vertices, Select point takes the vertex nearest a point picked ("Not a control vertex." or "Not a fit point." where
        /// none is within the pick aperture); the vertex stays marked after a change.
        /// </summary>
        private static IEnumerable<CadPrompt> SplineVertices(
            CadCommandContext c,
            string handle,
            List<CadGroupMark> marks,
            SplineVertexKind kind
        )
        {
            int index = 0;
            bool told = false;

            while (true)
            {
                if (!(c.Document.FindEntity(handle) is DxfSpline spline))
                {
                    yield break;
                }

                bool fit = kind == SplineVertexKind.MoveFit;
                int count = fit ? spline.NumFitPoints : spline.NumControlPoints;

                if (count == 0)
                {
                    yield break;
                }

                if (index >= count)
                {
                    index = 0;
                }

                GeoPoint3 at = fit ? spline.GetFitPointAt(index) : spline.GetControlPointAt(index);
                GeoPoint2 mark = new GeoPoint2(at.X, at.Y);

                if (kind == SplineVertexKind.Weight)
                {
                    if (!told && !spline.IsRational)
                    {
                        c.Write("Spline is not rational. Will make it so.");
                        told = true;
                    }

                    yield return CadPrompt
                        .ForDistance(
                            "Enter new weight (current = "
                                + spline.WeightAt(index).ToString("0.0000", CultureInfo.InvariantCulture)
                                + ")",
                            null,
                            new[] { "Next", "Previous", "Select point", "eXit" },
                            "N"
                        )
                        .Marking(mark);
                }
                else
                {
                    yield return CadPrompt
                        .ForPoint(
                            "Specify new location",
                            mark,
                            new[] { "Next", "Previous", "Select point", "eXit" },
                            "N"
                        )
                        .Marking(mark);
                }

                if (kind != SplineVertexKind.Weight && c.Input.IsPoint)
                {
                    GeoPoint2 to = c.Input.Point;
                    int moving = index;
                    PeditEach(c, new[] { handle }, "Move vertex", marks, (s, curve) =>
                    {
                        var moved = (DxfSpline)curve;
                        var target = new GeoPoint3(to.X, to.Y, at.Z);

                        if (fit)
                        {
                            moved.SetFitPointAt(moving, target);
                        }
                        else
                        {
                            moved.SetControlPointAt(moving, target);
                        }
                    });
                }
                else if (kind == SplineVertexKind.Weight && c.Input.Kind == CadInputKind.Number)
                {
                    double weight = c.Input.Number;
                    int weighing = index;

                    if (weight > 0.0)
                    {
                        PeditEach(c, new[] { handle }, "Weight", marks, (s, curve) =>
                        {
                            var weighed = (DxfSpline)curve;
                            weighed.PurgeFitData();
                            weighed.SetWeightAt(weighing, weight);
                        });
                    }
                    else
                    {
                        c.Write("Requires numeric value or option keyword.");
                    }
                }
                else if (c.Input.IsNone || c.Input.Is("Next"))
                {
                    index = (index + 1) % count;
                }
                else if (c.Input.Is("Previous"))
                {
                    index = (index + count - 1) % count;
                }
                else if (c.Input.Is("Select point"))
                {
                    while (true)
                    {
                        yield return CadPrompt.ForPoint("Specify existing fit point on spline <exit>");

                        if (!c.Input.IsPoint)
                        {
                            break;
                        }

                        int found = fit
                            ? SplineNearestFit(c, spline, c.Input.Point)
                            : SplineNearestVertex(c, spline, c.Input.Point);

                        if (found >= 0)
                        {
                            index = found;
                            break;
                        }

                        c.Write(fit ? "Not a fit point." : "Not a control vertex.");
                    }
                }
                else
                {
                    yield break;
                }
            }
        }

        /// <summary>Replaces the spline by the polyline of lines that runs along it within a precision, with the spline's look, as one change.</summary>
        private static void SplineToPolyline(CadCommandContext c, string handle, int precision)
        {
            c.OpenUndoStep();
            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Erased, handle) }, s =>
            {
                var spline = (DxfSpline)s.Document.FindEntity(handle);
                DxfCurve polyline = spline.ToPolylineWithPrecision(precision, false, true);
                polyline.SetPropertiesFrom(spline);
                CadSpace where = s.Document.FindSpace(handle);
                spline.Erase();
                s.Append(where, polyline);
            });
        }
    }
}

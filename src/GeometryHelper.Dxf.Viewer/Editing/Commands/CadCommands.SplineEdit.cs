using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>SPLINEDIT in AutoCAD's words, for the options built: Close and Open, Move vertex, rEverse, convert to Polyline, Undo and eXit.</summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// SPLINEDIT: "Select spline:" (a pick that is no spline asks again), then "Enter an option [Close/Move vertex/rEverse/convert
        /// to Polyline/Undo/eXit] &lt;eXit&gt;:" until Enter. Close and Open are offered for a spline of fit points: Close
        /// makes it the periodic cubic AutoCAD makes through its points (<see cref="DxfFitData.Periodic"/>), Open the cubic
        /// through the same points again, open. Move vertex moves a control vertex, a spline of fit points losing its fit data,
        /// as AutoCAD's does, and asks "Specify new location or [Next/Previous/Select point/eXit] &lt;N&gt;:". rEverse turns the
        /// spline round, "Spline has been reversed.". convert to Polyline asks "Specify a precision &lt;10&gt;:", replaces the
        /// spline by the polyline of lines within it and ends the command. Undo takes back the last change. AutoCAD's Fit
        /// data, Join and Refine are not built. Each change is a step of undo within the command, the whole one step.
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
                    options.Add(spline.Closed ? "Open" : "Close");
                }

                options.AddRange(new[] { "Move vertex", "rEverse", "convert to Polyline", "Undo", "eXit" });
                yield return CadPrompt.ForKeyword("Enter an option", options, "eXit");

                if (c.Input.IsNone || c.Input.Is("eXit"))
                {
                    break;
                }

                if (c.Input.Is("Undo"))
                {
                    PeditUndo(c, marks);
                }
                else if (c.Input.Is("Close") || c.Input.Is("Open"))
                {
                    bool close = c.Input.Is("Close");
                    PeditEach(c, new[] { handle }, close ? "Close" : "Open", marks, (s, curve) =>
                        SplineClosed((DxfSpline)curve, close, c.Session.Tolerance)
                    );
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
                    foreach (CadPrompt prompt in SplineVertices(c, handle, marks))
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
        /// The prompts of Move vertex: "Specify new location or [Next/Previous/Select point/eXit] &lt;N&gt;:" for the control
        /// vertex now marked (the first at first), a point moving it, Next and Previous going round the vertices, Select
        /// point taking the vertex nearest a point picked ("Not a control vertex." where none is within the pick aperture).
        /// </summary>
        private static IEnumerable<CadPrompt> SplineVertices(
            CadCommandContext c,
            string handle,
            List<CadGroupMark> marks
        )
        {
            int index = 0;

            while (true)
            {
                if (!(c.Document.FindEntity(handle) is DxfSpline spline) || spline.NumControlPoints == 0)
                {
                    yield break;
                }

                GeoPoint3 at = spline.GetControlPointAt(index);
                yield return CadPrompt
                    .ForPoint(
                        "Specify new location",
                        new GeoPoint2(at.X, at.Y),
                        new[] { "Next", "Previous", "Select point", "eXit" },
                        "N"
                    )
                    .Marking(new GeoPoint2(at.X, at.Y));

                if (c.Input.IsPoint)
                {
                    GeoPoint2 to = c.Input.Point;
                    int moving = index;
                    PeditEach(c, new[] { handle }, "Move vertex", marks, (s, curve) =>
                        ((DxfSpline)curve).SetControlPointAt(moving, new GeoPoint3(to.X, to.Y, at.Z))
                    );
                }
                else if (c.Input.IsNone || c.Input.Is("Next"))
                {
                    index = (index + 1) % spline.NumControlPoints;
                }
                else if (c.Input.Is("Previous"))
                {
                    index = (index + spline.NumControlPoints - 1) % spline.NumControlPoints;
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

                        int found = -1;
                        double best = c.Session.PickAperture;

                        for (int i = 0; i < spline.NumControlPoints; i++)
                        {
                            GeoPoint3 p = spline.GetControlPointAt(i);
                            double d = new GeoPoint2(p.X, p.Y).DistanceTo(c.Input.Point);

                            if (d <= best)
                            {
                                best = d;
                                found = i;
                            }
                        }

                        if (found >= 0)
                        {
                            index = found;
                            break;
                        }

                        c.Write("Not a control vertex.");
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

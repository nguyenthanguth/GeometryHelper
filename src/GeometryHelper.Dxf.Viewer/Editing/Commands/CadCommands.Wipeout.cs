using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// WIPEOUT as AutoCAD 2021 asks it: the points of a closed outline, Polyline (a closed polyline picked, which it can
    /// erase) and Frames (ON, OFF, Display but not plot), the variable WIPEOUTFRAME the same.
    /// </summary>
    /// <remarks>
    /// The wipeout is written as AutoCAD's command writes it: the frame the square on the box of the points at their lowest
    /// corner, the points in it from -0.5 to 0.5, the variables of wipeouts (WIPEOUTVARIABLES) made with the first one. The
    /// frames are kept in those variables as shown or not; "Display but not plot" is shown here and written as shown, as
    /// AutoCAD 2021 writes it, and the session remembers it for WIPEOUTFRAME.
    /// </remarks>
    internal static partial class CadCommands
    {
        private const string WipeoutFrameKey = "wipeoutframe";

        private static void AddWipeoutTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("WIPEOUT", Wipeout));
            registry.AddVariable(
                CadSystemVariable
                    .Whole(
                        "WIPEOUTFRAME",
                        true,
                        0,
                        2,
                        s => WipeoutFrameOf(s),
                        (c, v) => SetWipeoutFrame(c, v),
                        "WIPEOUTFRAME",
                        (c, v) =>
                            c.Database.NamedObjectsDictionary == null
                                ? "WIPEOUTFRAME is kept in the drawing's objects, which a drawing of R12 has none of."
                                : null
                    )
                    .Recorded(CadEditRecordKind.TableChanged)
            );
        }

        /// <summary>WIPEOUTFRAME as the drawing keeps it: 0 for frames off, 2 where they are shown and the session has them not plotted, 1 otherwise, and where the drawing keeps no variables of wipeouts.</summary>
        internal static int WipeoutFrameOf(CadCommandSession session)
        {
            DxfDBDictionary root = session.Document.Database.NamedObjectsDictionary;

            if (
                root?.Contains("ACAD_WIPEOUT_VARS") == true
                && root.GetAt("ACAD_WIPEOUT_VARS") is DxfWipeoutVariables variables
                && !variables.ShowFrames
            )
            {
                return 0;
            }

            return session.Remembered.TryGetValue(WipeoutFrameKey, out double kept) && kept == 2.0 ? 2 : 1;
        }

        private static void SetWipeoutFrame(CadCommandContext c, int value)
        {
            DxfWipeoutVariables.GetWipeoutVariables(c.Database).ShowFrames = value != 0;
            c.Remember(WipeoutFrameKey, value);
        }

        private static IEnumerable<CadPrompt> Wipeout(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify first point", null, new[] { "Frames", "Polyline" }, "Polyline");

            if (c.Input.Is("Frames"))
            {
                foreach (CadPrompt prompt in WipeoutFrames(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (c.Input.IsNone || c.Input.Is("Polyline"))
            {
                foreach (CadPrompt prompt in WipeoutOfPolyline(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            var points = new List<GeoPoint2> { c.Input.Point };

            while (true)
            {
                string[] options =
                    points.Count == 1 ? null
                    : points.Count == 2 ? new[] { "Undo" }
                    : new[] { "Close", "Undo" };
                yield return CadPrompt.ForPoint("Specify next point", points[points.Count - 1], options);

                if (c.Input.IsPoint)
                {
                    points.Add(c.Input.Point);
                }
                else if (c.Input.Is("Undo"))
                {
                    points.RemoveAt(points.Count - 1);
                }
                else
                {
                    // Close, or Enter on the third point and after, makes the wipeout; Enter before that makes none.
                    if (c.Input.Is("Close") || (c.Input.IsNone && points.Count >= 3))
                    {
                        MakeWipeout(c, points, null);
                    }

                    yield break;
                }
            }
        }

        /// <summary>Frames: "Enter mode [ON/OFF/Display but not plot] &lt;ON&gt;:", the answer set as WIPEOUTFRAME 1, 0 or 2.</summary>
        private static IEnumerable<CadPrompt> WipeoutFrames(CadCommandContext c)
        {
            int old = WipeoutFrameOf(c.Session);
            yield return CadPrompt.ForKeyword(
                "Enter mode",
                new[] { "ON", "OFF", "Display but not plot" },
                old == 0 ? "OFF"
                    : old == 2 ? "Display but not plot"
                    : "ON"
            );

            int value =
                c.Input.Is("OFF") ? 0
                : c.Input.Is("Display but not plot") ? 2
                : c.Input.Is("ON") ? 1
                : c.Input.IsNone ? old
                : -1;

            if (value < 0)
            {
                yield break;
            }

            c.Apply(
                new[]
                {
                    new CadEditRecord(CadEditRecordKind.TableChanged, null, "WIPEOUTFRAME", Values(old), Values(value)),
                },
                s => SetWipeoutFrame(c, value)
            );
        }

        /// <summary>
        /// Polyline: "Select a closed polyline:" asked again where the pick is no polyline, a polyline not closed or of arcs
        /// refused ("The polyline must be closed and made up of only line segments.") and the command over, then "Erase
        /// polyline? [Yes/No] &lt;No&gt;:". The wipeout has the polyline's corners, in their order.
        /// </summary>
        private static IEnumerable<CadPrompt> WipeoutOfPolyline(CadCommandContext c)
        {
            DxfPolyline polyline;

            while (true)
            {
                yield return CadPrompt.ForEntity("Select a closed polyline");

                if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
                {
                    yield break;
                }

                polyline = c.Document.FindEntity(c.Input.Handles[0]) as DxfPolyline;

                if (polyline != null)
                {
                    break;
                }
            }

            var points = new List<GeoPoint2>();
            bool lines = polyline.Closed && polyline.NumberOfVertices >= 3;

            for (int i = 0; lines && i < polyline.NumberOfVertices; i++)
            {
                lines = polyline.GetBulgeAt(i) == 0.0;
                GeoPoint3 corner = polyline.GetPoint3dAt(i);
                points.Add(new GeoPoint2(corner.X, corner.Y));
            }

            if (!lines)
            {
                c.Write("The polyline must be closed and made up of only line segments.");
                yield break;
            }

            yield return CadPrompt.ForKeyword("Erase polyline?", new[] { "Yes", "No" }, "No");
            MakeWipeout(c, points, c.Input.Is("Yes") ? polyline.Handle : null);
        }

        /// <summary>
        /// Makes the wipeout of corners in the world, closed on the first, in the current layer; with the polyline of a handle
        /// erased in the same step. The frame is the square on the box of the corners at their lowest corner, as AutoCAD's
        /// WIPEOUT writes it, where <see cref="DxfWipeout.SetFrom"/> lays it on the box and the origin both: the corners are
        /// framed from the origin, and the wipeout moved to where they stand.
        /// </summary>
        private static void MakeWipeout(CadCommandContext c, IReadOnlyList<GeoPoint2> corners, string erased)
        {
            IReadOnlyList<GeoPoint2> world = corners;

            double minX = double.MaxValue;
            double minY = double.MaxValue;

            foreach (GeoPoint2 point in world)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
            }

            var framed = new List<GeoPoint2>(world.Count + 1);

            foreach (GeoPoint2 point in world)
            {
                framed.Add(new GeoPoint2(point.X - minX, point.Y - minY));
            }

            framed.Add(framed[0]);
            var wipeout = new DxfWipeout();

            try
            {
                wipeout.SetFrom(framed, new GeoVector3(0.0, 0.0, 1.0));
            }
            catch (ArgumentException)
            {
                c.Write("*Invalid* The points do not close an area.");
                return;
            }

            wipeout.TransformBy(GeoTransform3.Translation(new GeoVector3(minX, minY, 0.0)));
            Current(c, wipeout);

            c.Apply(erased == null ? null : new[] { new CadEditRecord(CadEditRecordKind.Erased, erased) }, s =>
            {
                s.Append(c.Space, wipeout);

                if (erased != null)
                {
                    s.Document.FindEntity(erased).Erase();
                }
            });
        }
    }
}

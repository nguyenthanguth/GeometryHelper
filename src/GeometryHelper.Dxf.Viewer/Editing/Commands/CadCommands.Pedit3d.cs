using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>PEDIT of a 3D polyline, as AutoCAD 2021 asks it and stores what it makes (checked in AutoCAD's own console).</summary>
    internal static partial class CadCommands
    {
        private static readonly string[] Pedit3dOpenOptions =
        {
            "Close",
            "Join",
            "Edit vertex",
            "Spline curve",
            "Decurve",
            "Reverse",
            "Undo",
        };
        private static readonly string[] Pedit3dClosedOptions =
        {
            "Open",
            "Join",
            "Edit vertex",
            "Spline curve",
            "Decurve",
            "Reverse",
            "Undo",
        };
        private static readonly string[] Pedit3dVertexOptions =
        {
            "Next",
            "Previous",
            "Break",
            "Insert",
            "Move",
            "Regen",
            "Straighten",
            "eXit",
        };

        /// <summary>
        /// PEDIT of a 3D polyline: Close or Open, Join, Edit vertex, Spline curve, Decurve, Reverse and Undo, until eXit or Enter.
        /// Spline curve fits it as SPLINETYPE and SPLINESEGS ask, Decurve straightens it, Reverse turns its vertices round, the
        /// first of a closed one too; a fitted polyline closed, opened or edited at a vertex is fitted again on its frame. Join
        /// asks the objects and adds none, as AutoCAD 2021's console did for lines and 3D polylines meeting an end. Each option
        /// is a step of the run that Undo takes back.
        /// </summary>
        private static IEnumerable<CadPrompt> Pedit3d(CadCommandContext c, string handle)
        {
            var marks = new List<CadGroupMark>();

            while (c.Document.FindEntity(handle) is DxfPolyline3d polyline)
            {
                yield return CadPrompt.ForKeywordOrText(
                    "Enter an option",
                    polyline.Closed ? Pedit3dClosedOptions : Pedit3dOpenOptions,
                    null
                );

                if (c.Input.IsNone || PeditExit(c.Input))
                {
                    yield break;
                }

                if (c.Input.Kind != CadInputKind.Keyword)
                {
                    c.Write("Invalid option keyword.");
                    continue;
                }

                if (c.Input.Is("Undo"))
                {
                    PeditUndo(c, marks);
                }
                else if (c.Input.Is("Join"))
                {
                    yield return CadPrompt.ForSelection();
                    c.Write("0 segments added to polyline");
                }
                else if (c.Input.Is("Edit vertex"))
                {
                    foreach (CadPrompt prompt in Pedit3dVertex(c, handle, marks))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Close") || c.Input.Is("Open"))
                {
                    bool closed = c.Input.Is("Close");
                    PeditEach(c, new[] { handle }, c.Input.Text, marks, (s, p) =>
                    {
                        var three = (DxfPolyline3d)p;
                        Pedit3dWrite(three, Pedit3dFrame(three), closed, three.PolyType);
                    });
                }
                else if (c.Input.Is("Spline curve"))
                {
                    PeditEach(c, new[] { handle }, "Spline curve", marks, (s, p) =>
                    {
                        var three = (DxfPolyline3d)p;

                        if (Pedit3dFrame(three).Count >= (three.Closed ? 3 : 2))
                        {
                            three.SplineFit();
                        }
                    });
                }
                else if (c.Input.Is("Decurve"))
                {
                    PeditEach(c, new[] { handle }, "Decurve", marks, (s, p) => ((DxfPolyline3d)p).Straighten());
                }
                else if (c.Input.Is("Reverse"))
                {
                    // The frame turned round, the first vertex of a closed one too, and fitted again as it was.
                    PeditEach(c, new[] { handle }, "Reverse", marks, (s, p) =>
                    {
                        var three = (DxfPolyline3d)p;
                        List<GeoPoint3> frame = Pedit3dFrame(three);
                        frame.Reverse();
                        Pedit3dWrite(three, frame, three.Closed, three.PolyType);
                    });
                }
            }
        }

        /// <summary>
        /// Edit vertex of a 3D polyline: the vertex marked with a cross, the first the view shows, moved along by Next and
        /// Previous, which stop at the ends; Break and Straighten mark a second and Go breaks the polyline between the two or
        /// takes out the vertices between them; Insert puts a vertex after the marked one, Move moves it, at the point picked.
        /// A fitted polyline is edited on its frame and fitted again. The whole of it is one step of the run that Undo takes back.
        /// </summary>
        private static IEnumerable<CadPrompt> Pedit3dVertex(
            CadCommandContext c,
            string handle,
            List<CadGroupMark> marks
        )
        {
            var polyline = (DxfPolyline3d)c.Document.FindEntity(handle);
            List<GeoPoint3> frame = Pedit3dFrame(polyline);
            int at = 0;

            if (c.Session.View != null)
            {
                DisplayBox box = c.Session.View.VisibleBox;
                at = frame.FindIndex(v => box.Contains(v.X, v.Y));

                if (at < 0)
                {
                    c.Write("No visible vertices");
                    yield break;
                }
            }

            string way = "N";
            var state = new Ref<CadGroupMark>();

            while (c.Document.FindEntity(handle) is DxfPolyline3d own)
            {
                polyline = own;
                frame = Pedit3dFrame(polyline);
                at = Math.Max(0, Math.Min(at, frame.Count - 1));
                yield return CadPrompt
                    .ForKeyword("Enter a vertex editing option", Pedit3dVertexOptions, way)
                    .Marking(Flat(frame[at]));

                if (c.Input.Is("eXit") || c.Input.Kind != CadInputKind.Keyword)
                {
                    break;
                }

                if (c.Input.Is("Next") || c.Input.Is("Previous"))
                {
                    bool forward = c.Input.Is("Next");
                    at = Math.Max(0, Math.Min(frame.Count - 1, at + (forward ? 1 : -1)));
                    way = forward ? "N" : "P";
                }
                else if (c.Input.Is("Move") || c.Input.Is("Insert"))
                {
                    bool insert = c.Input.Is("Insert");
                    yield return CadPrompt.ForPoint(
                        insert ? "Specify location for new vertex" : "Specify new location for marked vertex",
                        Flat(frame[at])
                    );

                    if (!c.Input.IsPoint)
                    {
                        continue;
                    }

                    // A point picked lies at nought, as AutoCAD's picked in the plan of the world lies at its elevation.
                    var to = new GeoPoint3(c.Input.Point.X, c.Input.Point.Y, 0.0);

                    if (insert)
                    {
                        frame.Insert(at + 1, to);
                        at++;
                    }
                    else
                    {
                        frame[at] = to;
                    }

                    bool closed = polyline.Closed;
                    PeditVertexApply(c, handle, state, s =>
                    {
                        var three = (DxfPolyline3d)s.Document.FindEntity(handle);
                        Pedit3dWrite(three, frame, closed, three.PolyType);
                    });
                }
                else if (c.Input.Is("Break") || c.Input.Is("Straighten"))
                {
                    bool breaking = c.Input.Is("Break");
                    int other = at;
                    string go = "N";
                    bool gone = false;

                    while (true)
                    {
                        yield return CadPrompt
                            .ForKeyword("Enter an option", PeditGoOptions, go)
                            .Marking(Flat(frame[other]));

                        if (c.Input.Is("Next") || c.Input.Is("Previous"))
                        {
                            bool forward = c.Input.Is("Next");
                            other = Math.Max(0, Math.Min(frame.Count - 1, other + (forward ? 1 : -1)));
                            go = forward ? "N" : "P";
                            continue;
                        }

                        gone = c.Input.Is("Go");
                        break;
                    }

                    if (!gone)
                    {
                        continue;
                    }

                    int a = Math.Min(at, other);
                    int b = Math.Max(at, other);

                    if (breaking && (a == b || (!polyline.Closed && a == 0 && b == frame.Count - 1)))
                    {
                        c.Write("*Invalid*");
                        continue;
                    }

                    if (breaking)
                    {
                        Pedit3dBreak(c, handle, state, a, b);
                    }
                    else if (b > a + 1)
                    {
                        frame.RemoveRange(a + 1, b - a - 1);
                        bool closed = polyline.Closed;
                        PeditVertexApply(c, handle, state, s =>
                        {
                            var three = (DxfPolyline3d)s.Document.FindEntity(handle);
                            Pedit3dWrite(three, frame, closed, three.PolyType);
                        });
                    }

                    at = a;
                }
            }

            if (state.Value != null)
            {
                marks.Add(state.Value);
            }
        }

        /// <summary>
        /// Break between the vertices a and b (a below b) of a 3D polyline's frame: an open one keeps the part up to a and a new
        /// one holds the part from b; a closed one is opened into the part up to a and a new one from b round to the first
        /// vertex, as AutoCAD breaks it. A piece of a spline is fitted again where three vertices of its frame are left.
        /// </summary>
        private static void Pedit3dBreak(CadCommandContext c, string handle, Ref<CadGroupMark> state, int a, int b)
        {
            PeditVertexApply(c, handle, state, s =>
            {
                var polyline = (DxfPolyline3d)s.Document.FindEntity(handle);
                DxfPoly3dType type = polyline.PolyType;
                List<GeoPoint3> frame = Pedit3dFrame(polyline);
                List<GeoPoint3> head = frame.GetRange(0, a + 1);
                List<GeoPoint3> tail = frame.GetRange(b, frame.Count - b);

                if (polyline.Closed)
                {
                    tail.Add(frame[0]);
                }

                List<GeoPoint3> own = head.Count >= 2 ? head : tail;
                List<GeoPoint3> made = head.Count >= 2 && tail.Count >= 2 ? tail : null;
                Pedit3dWrite(polyline, own, false, own.Count >= 3 ? type : DxfPoly3dType.SimplePoly);

                if (made != null)
                {
                    var piece = new DxfPolyline3d(DxfPoly3dType.SimplePoly, made, false);
                    piece.SetPropertiesFrom(polyline);

                    if (
                        s.Append(s.Document.FindSpace(handle), piece) is DxfPolyline3d added
                        && type != DxfPoly3dType.SimplePoly
                        && made.Count >= 3
                    )
                    {
                        added.ConvertToPolyType(type);
                    }
                }
            });
        }

        /// <summary>The frame of a 3D polyline: its vertices, those of a fitted one it is fitted on, in the world.</summary>
        private static List<GeoPoint3> Pedit3dFrame(DxfPolyline3d polyline) =>
            polyline.Where(v => v.VertexType != DxfVertex3dType.FitVertex).Select(v => v.Position).ToList();

        /// <summary>
        /// Writes a frame into a 3D polyline, closed or not, and fits it as given: straightened, its vertices set, taken out or
        /// added, every handle kept that can be, and fitted again where it has vertices enough.
        /// </summary>
        private static void Pedit3dWrite(DxfPolyline3d polyline, List<GeoPoint3> frame, bool closed, DxfPoly3dType type)
        {
            polyline.Straighten();
            List<DxfPolylineVertex3d> own = polyline.ToList();

            for (int i = 0; i < frame.Count; i++)
            {
                if (i < own.Count)
                {
                    own[i].Position = frame[i];
                }
                else
                {
                    polyline.AppendVertex(new DxfPolylineVertex3d(frame[i]));
                }
            }

            for (int i = own.Count - 1; i >= frame.Count; i--)
            {
                own[i].Erase();
            }

            polyline.Closed = closed;

            if (type != DxfPoly3dType.SimplePoly && frame.Count >= (closed ? 3 : 2))
            {
                polyline.ConvertToPolyType(type);
            }
        }
    }
}

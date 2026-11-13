using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>PEDIT, as AutoCAD 2021 asks it and stores what it makes (checked in AutoCAD's own console).</summary>
    internal static partial class CadCommands
    {
        private static readonly string[] PeditOpenOptions =
        {
            "Close",
            "Join",
            "Width",
            "Edit vertex",
            "Fit",
            "Spline",
            "Decurve",
            "Ltype gen",
            "Reverse",
            "Undo",
        };
        private static readonly string[] PeditClosedOptions =
        {
            "Open",
            "Join",
            "Width",
            "Edit vertex",
            "Fit",
            "Spline",
            "Decurve",
            "Ltype gen",
            "Reverse",
            "Undo",
        };
        private static readonly string[] PeditManyOptions =
        {
            "Close",
            "Open",
            "Join",
            "Width",
            "Fit",
            "Spline",
            "Decurve",
            "Ltype gen",
            "Reverse",
            "Undo",
        };
        private static readonly string[] PeditVertexOptions =
        {
            "Next",
            "Previous",
            "Break",
            "Insert",
            "Move",
            "Regen",
            "Straighten",
            "Tangent",
            "Width",
            "eXit",
        };
        private static readonly string[] PeditGoOptions = { "Next", "Previous", "Go", "eXit" };
        private static readonly string[] PeditJoinTypes = { "Extend", "Add", "Both" };

        /// <summary>
        /// PEDIT: a polyline picked is edited by the options until eXit or Enter, a line or an arc picked being made one first
        /// ("Do you want to turn it into one? &lt;Y&gt;"); Multiple edits several at once, lines and arcs among them made polylines,
        /// and joins them within a fuzz distance. Fit and Spline make a LWPOLYLINE the POLYLINE AutoCAD makes of it, with the
        /// vertices of the curve flagged 1 or 8 and those of the frame 16; a POLYLINE left straight when one is edited alone is
        /// made a LWPOLYLINE again, as AutoCAD's PLINETYPE 2 makes it. Each run is one undo step; Undo takes back the last option.
        /// </summary>
        private static IEnumerable<CadPrompt> Pedit(CadCommandContext c)
        {
            string handle = null;

            while (handle == null)
            {
                yield return CadPrompt.ForEntity("Select polyline", new[] { "Multiple" });

                if (c.Input.Is("Multiple"))
                {
                    foreach (CadPrompt prompt in PeditMany(c))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                // Enter asks again, as AutoCAD asks; Esc ends the command.
                if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
                {
                    continue;
                }

                string picked = c.Input.Handles[0];
                DxfEntity entity = c.Document.FindEntity(picked);

                if (
                    !(
                        entity is DxfPolyline
                        || entity is DxfPolyline2d
                        || entity is DxfPolyline3d
                        || entity is DxfLine
                        || entity is DxfArc
                    )
                )
                {
                    c.Write(
                        entity is DxfPolygonMesh || entity is DxfPolyFaceMesh
                            ? "PEDIT edits polylines and 3D polylines here; meshes are not edited yet."
                            : "Object selected is not a polyline"
                    );
                    continue;
                }

                if (!CanChange(c, picked, "edited"))
                {
                    continue;
                }

                if (entity is DxfPolyline3d)
                {
                    foreach (CadPrompt prompt in Pedit3d(c, picked))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                if (entity is DxfPolyline || entity is DxfPolyline2d)
                {
                    handle = picked;
                    continue;
                }

                // PEDITACCEPT 1 turns it into one without a word, as AutoCAD 2021 does.
                var yes = new Ref<bool> { Value = PeditAccepts(c) };

                if (!yes.Value)
                {
                    c.Write("Object selected is not a polyline");

                    foreach (CadPrompt prompt in PeditYesNo(c, "Do you want to turn it into one?", yes))
                    {
                        yield return prompt;
                    }
                }

                if (yes.Value)
                {
                    handle = PeditMakePolylines(c, new[] { picked }).FirstOrDefault();
                }
            }

            var marks = new List<CadGroupMark>();

            while (true)
            {
                if (!(c.Document.FindEntity(handle) is DxfCurve polyline))
                {
                    yield break;
                }

                yield return CadPrompt.ForKeywordOrText(
                    "Enter an option",
                    polyline.Closed ? PeditClosedOptions : PeditOpenOptions,
                    null
                );

                if (c.Input.IsNone || PeditExit(c.Input))
                {
                    break;
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
                    if (polyline.Closed)
                    {
                        c.Write("Can't join to a closed polyline");
                        continue;
                    }

                    yield return CadPrompt.ForSelection();

                    if (c.Input.Kind == CadInputKind.Selection)
                    {
                        PeditJoin(c, handle, c.Input.Handles, marks);
                    }
                }
                else if (c.Input.Is("Width"))
                {
                    var width = new Ref<double?>();

                    foreach (CadPrompt prompt in PeditWidth(c, width))
                    {
                        yield return prompt;
                    }

                    if (width.Value.HasValue)
                    {
                        PeditEach(c, new[] { handle }, "Width", marks, (s, p) => PeditSetWidth(p, width.Value.Value));
                    }
                }
                else if (c.Input.Is("Edit vertex"))
                {
                    foreach (CadPrompt prompt in PeditVertex(c, handle, marks))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Ltype gen"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Enter polyline linetype generation option",
                        new[] { "ON", "OFF" },
                        PeditLinetypeGeneration(polyline) ? "On" : "Off"
                    );

                    if (c.Input.Is("ON") || c.Input.Is("OFF"))
                    {
                        bool on = c.Input.Is("ON");
                        PeditEach(c, new[] { handle }, "Ltype gen", marks, (s, p) => PeditSetLinetypeGeneration(p, on));
                    }
                }
                else
                {
                    PeditPlain(c, new[] { handle }, c.Input.Text, marks);
                }
            }

            PeditToLightweight(c, handle);
        }

        /// <summary>PEDIT Multiple: the objects selected, lines and arcs among them made polylines where asked, edited together.</summary>
        private static IEnumerable<CadPrompt> PeditMany(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            var handles = new List<string>();
            var others = new List<string>();

            foreach (string picked in c.Selected)
            {
                DxfEntity entity = c.Document.FindEntity(picked);

                if (entity is DxfPolyline || entity is DxfPolyline2d || entity is DxfPolyline3d)
                {
                    handles.Add(picked);
                }
                else if (entity is DxfLine || entity is DxfArc || entity is DxfSpline)
                {
                    others.Add(picked);
                }
            }

            if (others.Count > 0)
            {
                var yes = new Ref<bool> { Value = PeditAccepts(c) };

                if (!yes.Value)
                {
                    foreach (
                        CadPrompt prompt in PeditYesNo(c, "Convert Lines, Arcs and Splines to polylines [Yes/No]?", yes)
                    )
                    {
                        yield return prompt;
                    }
                }

                // Splines are left as they are: their conversion by a precision is not built.
                if (yes.Value)
                {
                    handles.AddRange(
                        PeditMakePolylines(c, others.Where(h => !(c.Document.FindEntity(h) is DxfSpline)).ToList())
                    );
                }
            }

            if (handles.Count == 0)
            {
                yield break;
            }

            // In the order the handles were given out, a shorter one first, as AutoCAD joins into the oldest.
            handles.Sort(
                (a, b) =>
                    a.Length != b.Length
                        ? a.Length.CompareTo(b.Length)
                        : string.Compare(a, b, StringComparison.OrdinalIgnoreCase)
            );
            var marks = new List<CadGroupMark>();

            while (true)
            {
                handles.RemoveAll(h => c.Document.FindEntity(h) == null);

                if (handles.Count == 0)
                {
                    yield break;
                }

                yield return CadPrompt.ForKeywordOrText("Enter an option", PeditManyOptions, null);

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
                    int type = (int)c.Remembered("pedit.jointype", 0.0);
                    double fuzz = c.Remembered("pedit.fuzz", 0.0);
                    c.Write("Join Type = " + PeditJoinTypeName(type));

                    while (true)
                    {
                        yield return CadPrompt.ForDistance(
                            "Enter fuzz distance",
                            null,
                            new[] { "Jointype" },
                            Length(fuzz)
                        );

                        if (c.Input.Is("Jointype"))
                        {
                            yield return CadPrompt.ForKeyword("Enter join type", PeditJoinTypes, PeditJoinTypes[type]);
                            int chosen = Array.FindIndex(PeditJoinTypes, k => c.Input.Is(k));
                            type = chosen >= 0 ? chosen : type;
                            c.Write("Join Type = " + PeditJoinTypeName(type));
                            continue;
                        }

                        if (c.Input.Kind == CadInputKind.Number && c.Input.Number < 0.0)
                        {
                            c.Write("Value must be positive.");
                            continue;
                        }

                        break;
                    }

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        fuzz = c.Input.Number;
                    }

                    c.Remember("pedit.jointype", type);
                    c.Remember("pedit.fuzz", fuzz);
                    PeditFuzzJoin(c, handles, fuzz, type, marks);
                }
                else if (c.Input.Is("Width"))
                {
                    var width = new Ref<double?>();

                    foreach (CadPrompt prompt in PeditWidth(c, width))
                    {
                        yield return prompt;
                    }

                    if (width.Value.HasValue)
                    {
                        PeditEach(c, handles, "Width", marks, (s, p) => PeditSetWidth(p, width.Value.Value));
                    }
                }
                else if (c.Input.Is("Ltype gen"))
                {
                    yield return CadPrompt
                        .ForKeyword("Enter polyline linetype generation option", new[] { "ON", "OFF" })
                        .WithoutColon();

                    if (c.Input.Is("ON") || c.Input.Is("OFF"))
                    {
                        bool on = c.Input.Is("ON");
                        PeditEach(c, handles, "Ltype gen", marks, (s, p) => PeditSetLinetypeGeneration(p, on));
                    }
                }
                else
                {
                    PeditPlain(c, handles, c.Input.Text, marks);
                }
            }
        }

        /// <summary>Asks a question taken by Y or N, Enter being Yes, as "Do you want to turn it into one? &lt;Y&gt;" asks.</summary>
        private static IEnumerable<CadPrompt> PeditYesNo(CadCommandContext c, string question, Ref<bool> yes)
        {
            while (true)
            {
                yield return CadPrompt.ForKeywordOrText(question, Array.Empty<string>(), "Y").WithoutColon();

                if (c.Input.IsNone)
                {
                    yes.Value = true;
                    yield break;
                }

                string typed = (c.Input.Text ?? string.Empty).Trim();

                if (typed.Length > 0 && "YES".StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                {
                    yes.Value = true;
                    yield break;
                }

                if (typed.Length > 0 && "NO".StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                {
                    yes.Value = false;
                    yield break;
                }

                c.Write("Yes or No, please.");
            }
        }

        /// <summary>Asks "Specify new width for all segments:" until a width of nought or more, or Enter for none.</summary>
        private static IEnumerable<CadPrompt> PeditWidth(CadCommandContext c, Ref<double?> width)
        {
            while (true)
            {
                yield return CadPrompt.ForDistance("Specify new width for all segments");

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                if (c.Input.Number < 0.0)
                {
                    c.Write("Value must be positive.");
                    continue;
                }

                width.Value = c.Input.Number;
                yield break;
            }
        }

        /// <summary>Whether an answer typed is AutoCAD's hidden eXit of PEDIT's option prompt: X, EX, EXI or EXIT.</summary>
        private static bool PeditExit(CadInput input)
        {
            string typed = input.Kind == CadInputKind.Text ? (input.Text ?? string.Empty).Trim() : string.Empty;
            return string.Equals(typed, "X", StringComparison.OrdinalIgnoreCase)
                || (typed.Length >= 2 && "EXIT".StartsWith(typed, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Takes back the last option of the run, or says that nothing is left to take back.</summary>
        private static void PeditUndo(CadCommandContext c, List<CadGroupMark> marks)
        {
            if (marks.Count == 0)
            {
                c.Write("Command has been completely undone.");
                return;
            }

            c.Document.BackTo(marks[marks.Count - 1]);
            marks.RemoveAt(marks.Count - 1);
        }

        private static string PeditJoinTypeName(int type) =>
            type == 1 ? "Add segment"
            : type == 2 ? "Both (Extend or Add)"
            : "Extend";

        /// <summary>
        /// Makes lines and arcs polylines of one segment, each a new LWPOLYLINE of the look of the object it replaces, an arc's
        /// running counter-clockwise from its start as AutoCAD's does; one lying out of the plane is left. Gives the new handles.
        /// </summary>
        private static List<string> PeditMakePolylines(CadCommandContext c, IReadOnlyList<string> handles)
        {
            var made = new List<string>();
            var flat = handles.Where(h => PolylineOf((DxfCurve)c.Document.FindEntity(h)) != null).ToList();

            if (flat.Count == 0)
            {
                return made;
            }

            c.Apply(flat.Select(h => new CadEditRecord(CadEditRecordKind.Erased, h)).ToList(), s =>
            {
                foreach (string handle in flat)
                {
                    var curve = (DxfCurve)s.Document.FindEntity(handle);
                    DxfPolyline polyline = PolylineOf(curve);
                    polyline.SetPropertiesFrom(curve);
                    CadSpace space = s.Document.FindSpace(handle);
                    curve.Erase();
                    made.Add(s.Append(space, OfPlineType(c, polyline)).Handle);
                }
            });

            return made;
        }

        /// <summary>Makes one change to each of the polylines, one step of the run that Undo takes back.</summary>
        private static bool PeditEach(
            CadCommandContext c,
            IReadOnlyList<string> handles,
            string how,
            List<CadGroupMark> marks,
            Action<CadEditScope, DxfCurve> change
        )
        {
            c.OpenUndoStep();
            CadGroupMark mark = c.Document.MarkGroup();
            var spaces = handles.Select(h => c.Document.FindSpace(h)?.Name).ToList();
            CadEditResult result = c.Apply(
                handles.Select(h => new CadEditRecord(CadEditRecordKind.Reshaped, h, how)).ToList(),
                s =>
                {
                    for (int i = 0; i < handles.Count; i++)
                    {
                        DxfEntity before = s.Document.FindEntity(handles[i]);
                        change(s, (DxfCurve)before);

                        // A polyline put in its own place (a LWPOLYLINE made a POLYLINE and back) is another object of the handle,
                        // which the index of handles is made again for.
                        if (before.IsErased)
                        {
                            s.Record(new CadEditRecord(CadEditRecordKind.Added, handles[i]) { Space = spaces[i] });
                        }
                    }
                }
            );

            if (result.Succeeded)
            {
                marks.Add(mark);
            }

            return result.Succeeded;
        }

        /// <summary>Close, Open, Fit, Spline, Decurve and Reverse, each on every polyline given.</summary>
        private static void PeditPlain(
            CadCommandContext c,
            IReadOnlyList<string> handles,
            string option,
            List<CadGroupMark> marks
        )
        {
            switch (option)
            {
                case "Close":
                case "Open":
                    bool closed = option == "Close";
                    PeditEach(c, handles, option, marks, (s, p) =>
                    {
                        if (p is DxfPolyline3d three)
                        {
                            Pedit3dWrite(three, Pedit3dFrame(three), closed, three.PolyType);
                            return;
                        }

                        // A fitted polyline is fitted again closed or open, as AutoCAD fits it.
                        if (PeditType(p) == DxfPoly2dType.SimplePoly)
                        {
                            PeditSetClosed(p, closed);
                        }
                        else
                        {
                            PeditWrite(p, PeditFrame(p), closed, PeditType(p));
                        }
                    });
                    break;

                case "Fit":
                    // A 3D polyline has no curve fit: AutoCAD leaves it as it is.
                    PeditEach(c, handles, option, marks, (s, p) =>
                    {
                        if (!(p is DxfPolyline3d))
                        {
                            PeditHeavy(p).CurveFit();
                        }
                    });
                    break;

                case "Spline":
                    PeditEach(c, handles, option, marks, (s, p) =>
                    {
                        if (p is DxfPolyline3d three)
                        {
                            if (Pedit3dFrame(three).Count >= (three.Closed ? 3 : 2))
                            {
                                three.SplineFit();
                            }

                            return;
                        }

                        if (PeditFrame(p).Count >= (p.Closed ? 3 : 2))
                        {
                            PeditHeavy(p).SplineFit();
                        }
                    });
                    break;

                case "Decurve":
                    // Every segment made straight, the vertices of a fitting taken out, as AutoCAD's Decurve does to arcs too.
                    PeditEach(c, handles, option, marks, (s, p) =>
                    {
                        if (p is DxfPolyline3d three)
                        {
                            three.Straighten();
                        }
                        else if (PeditType(p) != DxfPoly2dType.SimplePoly)
                        {
                            List<PeditPoint> frame = PeditFrame(p);
                            frame.ForEach(v => v.Bulge = 0.0);
                            PeditWrite(p, frame, p.Closed, DxfPoly2dType.SimplePoly);
                        }
                        else if (p is DxfPolyline light)
                        {
                            for (int i = 0; i < light.NumberOfVertices; i++)
                            {
                                light.SetBulgeAt(i, 0.0);
                            }
                        }
                        else
                        {
                            foreach (DxfVertex2d vertex in (DxfPolyline2d)p)
                            {
                                vertex.Bulge = 0.0;
                            }
                        }
                    });
                    break;

                case "Reverse":
                    PeditEach(c, handles, option, marks, (s, p) =>
                    {
                        // The frame of a 3D polyline turned round, the first vertex of a closed one too, and fitted again as it was.
                        if (p is DxfPolyline3d three)
                        {
                            List<GeoPoint3> frame = Pedit3dFrame(three);
                            frame.Reverse();
                            Pedit3dWrite(three, frame, three.Closed, three.PolyType);
                            return;
                        }

                        // A spline is fitted again on its frame turned round, its curve's vertices written anew as AutoCAD writes them.
                        if (
                            p is DxfPolyline2d heavy
                            && (
                                heavy.PolyType == DxfPoly2dType.QuadSplinePoly
                                || heavy.PolyType == DxfPoly2dType.CubicSplinePoly
                            )
                        )
                        {
                            DxfPoly2dType type = heavy.PolyType;
                            heavy.Straighten();
                            heavy.ReverseCurve();
                            heavy.ConvertToPolyType(type);
                            return;
                        }

                        p.ReverseCurve();
                    });
                    break;
            }
        }

        /// <summary>The polyline as a POLYLINE: a LWPOLYLINE put in its own place as one, as AutoCAD's Fit and Spline make it.</summary>
        private static DxfPolyline2d PeditHeavy(DxfCurve polyline) =>
            polyline is DxfPolyline light ? light.ConvertTo(true) : (DxfPolyline2d)polyline;

        private static DxfPoly2dType PeditType(DxfCurve polyline) =>
            polyline is DxfPolyline2d heavy ? heavy.PolyType : DxfPoly2dType.SimplePoly;

        private static bool PeditLinetypeGeneration(DxfCurve polyline) =>
            polyline is DxfPolyline light ? light.Plinegen : ((DxfPolyline2d)polyline).LinetypeGenerationOn;

        private static void PeditSetLinetypeGeneration(DxfCurve polyline, bool on)
        {
            if (polyline is DxfPolyline3d)
            {
                // A 3D polyline has no linetype generation to set: AutoCAD asks and leaves it.
                return;
            }

            if (polyline is DxfPolyline light)
            {
                light.Plinegen = on;
            }
            else
            {
                ((DxfPolyline2d)polyline).LinetypeGenerationOn = on;
            }
        }

        private static void PeditSetWidth(DxfCurve polyline, double width)
        {
            if (polyline is DxfPolyline3d)
            {
                // A 3D polyline has no width: AutoCAD asks and leaves it.
                return;
            }

            if (polyline is DxfPolyline light)
            {
                light.ConstantWidth = width;
                return;
            }

            // The frame given the width and fitted again, as AutoCAD widens a fitted polyline.
            var heavy = (DxfPolyline2d)polyline;
            DxfPoly2dType type = heavy.PolyType;
            heavy.Straighten();
            heavy.ConstantWidth = width;

            if (type != DxfPoly2dType.SimplePoly)
            {
                heavy.ConvertToPolyType(type);
            }
        }

        /// <summary>
        /// The single Join: the lines, arcs and polylines that meet the polyline's ends joined into it end to end, as AutoCAD's
        /// JoinEntities joins them, and "3 segments added to polyline" said.
        /// </summary>
        private static void PeditJoin(
            CadCommandContext c,
            string handle,
            IReadOnlyList<string> picked,
            List<CadGroupMark> marks
        )
        {
            var others = picked
                .Where(h => !string.Equals(h, handle, StringComparison.OrdinalIgnoreCase) && PeditJoinable(c, h))
                .ToList();
            var polyline = (DxfCurve)c.Document.FindEntity(handle);

            if (others.Count == 0 || (polyline is DxfPolyline2d fitted && fitted.PolyType != DxfPoly2dType.SimplePoly))
            {
                c.Write("0 segments added to polyline");
                return;
            }

            int before = PeditSegments(polyline);
            int added = 0;
            c.OpenUndoStep();
            CadGroupMark mark = c.Document.MarkGroup();
            CadEditResult result = c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, handle, "Join") }, s =>
            {
                var seed = (DxfCurve)s.Document.FindEntity(handle);
                var curves = others.Select(h => s.Document.FindEntity(h)).ToList();

                foreach (int i in seed.JoinEntities(curves))
                {
                    s.Record(new CadEditRecord(CadEditRecordKind.Erased, curves[i].Handle));
                    curves[i].Erase();
                }

                added = PeditSegments(seed) - before;
            });

            if (result.Succeeded)
            {
                marks.Add(mark);
                c.Write(added.ToString(CultureInfo.InvariantCulture) + " segments added to polyline");
            }
        }

        /// <summary>Whether Join takes an object: a line, an arc, or a polyline of the plane not fitted, on no locked layer.</summary>
        private static bool PeditJoinable(CadCommandContext c, string handle)
        {
            DxfEntity entity = c.Document.FindEntity(handle);

            if (
                entity == null
                || CadLayerEditor.IsLocked(c.Database, entity.Layer)
                || c.Document.Owners.GetEditRule(entity) != CadEditRule.Free
            )
            {
                return false;
            }

            return entity is DxfLine
                || entity is DxfArc
                || entity is DxfPolyline
                || (entity is DxfPolyline2d heavy && heavy.PolyType == DxfPoly2dType.SimplePoly);
        }

        /// <summary>The count of segments a polyline draws through its frame: its vertices, one less where it is open.</summary>
        private static int PeditSegments(DxfCurve polyline)
        {
            int count = PeditFrame(polyline).Count;
            return polyline.Closed ? count : count - 1;
        }

        /// <summary>
        /// Multiple's Join: the open polylines given joined end to end where their ends lie within the fuzz distance, nearest
        /// first, in the order of their handles: Extend runs the two end segments on or back to where they meet, an arc round its circle,
        /// Add puts a straight segment between the ends, Both extends where it can and adds where it cannot.
        /// </summary>
        private static void PeditFuzzJoin(
            CadCommandContext c,
            List<string> handles,
            double fuzz,
            int type,
            List<CadGroupMark> marks
        )
        {
            double slack = Slack(c);
            var chains = new List<PeditChain>();

            foreach (string handle in handles)
            {
                var polyline = (DxfCurve)c.Document.FindEntity(handle);

                if (!polyline.Closed && !(polyline is DxfPolyline3d) && PeditType(polyline) == DxfPoly2dType.SimplePoly)
                {
                    // The bulge PLINE leaves on the last vertex of one ended in arc mode draws nothing and is not joined, as
                    // AutoCAD 2021 joins two such arcs with a last bulge of nought.
                    List<PeditPoint> frame = PeditFrame(polyline);

                    if (frame.Count > 0)
                    {
                        frame[frame.Count - 1].Bulge = 0.0;
                    }

                    chains.Add(new PeditChain(handle, polyline, frame));
                }
            }

            int added = 0;
            var gone = new List<string>();
            var changed = new List<PeditChain>();

            foreach (PeditChain seed in chains)
            {
                if (seed.Gone)
                {
                    continue;
                }

                var refused = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                while (true)
                {
                    PeditChain best = null;
                    bool bestAtEnd = false,
                        bestFromStart = false;
                    double bestGap = double.MaxValue;

                    foreach (PeditChain other in chains)
                    {
                        if (
                            other == seed
                            || other.Gone
                            || refused.Contains(other.Handle)
                            || !seed.SamePlane(other, slack)
                        )
                        {
                            continue;
                        }

                        foreach (bool atEnd in new[] { true, false })
                        {
                            foreach (bool fromStart in new[] { true, false })
                            {
                                GeoPoint2 mine = atEnd ? seed.Points[seed.Points.Count - 1].At : seed.Points[0].At;
                                GeoPoint2 theirs = fromStart
                                    ? other.Points[0].At
                                    : other.Points[other.Points.Count - 1].At;
                                double gap = (theirs - mine).Length;

                                // The nearest that joins within the fuzz; at one distance the lower handle, the seed's end before
                                // its start.
                                if (
                                    gap < bestGap - slack
                                    && PeditAttach(seed, other, atEnd, fromStart, type, fuzz, slack, false) >= 0
                                )
                                {
                                    best = other;
                                    bestGap = gap;
                                    bestAtEnd = atEnd;
                                    bestFromStart = fromStart;
                                }
                            }
                        }
                    }

                    if (best == null)
                    {
                        break;
                    }

                    int segments = PeditAttach(seed, best, bestAtEnd, bestFromStart, type, fuzz, slack, true);

                    if (segments < 0)
                    {
                        refused.Add(best.Handle);
                        continue;
                    }

                    added += segments;
                    best.Gone = true;
                    gone.Add(best.Handle);

                    if (!changed.Contains(seed))
                    {
                        changed.Add(seed);
                    }
                }

                // Ends brought together close the polyline, as AutoCAD's join closes one.
                if (
                    changed.Contains(seed)
                    && seed.Points.Count > 2
                    && (seed.Points[0].At - seed.Points[seed.Points.Count - 1].At).Length <= slack
                )
                {
                    seed.Points.RemoveAt(seed.Points.Count - 1);
                    seed.Closed = true;
                }
            }

            if (changed.Count == 0)
            {
                c.Write("0 segments added to polyline");
                return;
            }

            c.OpenUndoStep();
            CadGroupMark mark = c.Document.MarkGroup();
            var planned = changed
                .Select(k => new CadEditRecord(CadEditRecordKind.Reshaped, k.Handle, "Join"))
                .Concat(gone.Select(h => new CadEditRecord(CadEditRecordKind.Erased, h)))
                .ToList();
            CadEditResult result = c.Apply(planned, s =>
            {
                foreach (PeditChain chain in changed)
                {
                    PeditWrite(
                        (DxfCurve)s.Document.FindEntity(chain.Handle),
                        chain.Points,
                        chain.Closed,
                        DxfPoly2dType.SimplePoly
                    );
                }

                foreach (string handle in gone)
                {
                    s.Document.FindEntity(handle).Erase();
                }
            });

            if (result.Succeeded)
            {
                marks.Add(mark);
                c.Write(added.ToString(CultureInfo.InvariantCulture) + " segments added to polyline");
            }
        }

        /// <summary>
        /// Puts another polyline's vertices on at an end of the seed (or, not applied, says whether it would): joined where
        /// their ends meet, else by the join type (1 Add, 2 Both, otherwise Extend). Extend runs the two end segments on or back
        /// to where they meet, a line along its line and an arc round its circle, where that point lies within the fuzz of both
        /// ends, however far apart the ends are, as AutoCAD 2021 joins; Add puts a segment between ends within the fuzz of each
        /// other; Both extends where it can and adds where it cannot. Gives the segments added, or -1 where they do not join
        /// and nothing is changed.
        /// </summary>
        private static int PeditAttach(
            PeditChain seed,
            PeditChain other,
            bool atEnd,
            bool fromStart,
            int type,
            double fuzz,
            double slack,
            bool apply
        )
        {
            // The two run one way: the seed then the other where they meet at the seed's end, the other then the seed at its start.
            List<PeditPoint> first;
            List<PeditPoint> second;

            if (atEnd)
            {
                first = PeditCopy(seed.Points);
                second = fromStart ? PeditCopy(other.Points) : PeditReversed(other.Points);
            }
            else
            {
                first = fromStart ? PeditReversed(other.Points) : PeditCopy(other.Points);
                second = PeditCopy(seed.Points);
            }

            int segments = other.Points.Count - 1;
            GeoPoint2 a = first[first.Count - 1].At;
            GeoPoint2 b = second[0].At;
            bool touch = (b - a).Length <= slack;

            if (!touch && type != 1)
            {
                GeoPoint2? meet =
                    first.Count >= 2 && second.Count >= 2
                        ? PeditMeet(first[first.Count - 2], a, b, second[0].Bulge, second[1].At)
                        : null;

                // An arc at an end is run on, or cut back, along its circle to where the two meet, its bulge made anew.
                double before = meet.HasValue
                    ? PeditBulgeTo(first[first.Count - 2].At, a, first[first.Count - 2].Bulge, meet.Value, true)
                    : 0.0;
                double after = meet.HasValue ? PeditBulgeTo(b, second[1].At, second[0].Bulge, meet.Value, false) : 0.0;

                if (
                    meet.HasValue
                    && !double.IsNaN(before)
                    && !double.IsNaN(after)
                    && (meet.Value - a).Length <= fuzz + slack
                    && (meet.Value - b).Length <= fuzz + slack
                )
                {
                    first[first.Count - 2].Bulge = before;
                    first[first.Count - 1].At = meet.Value;
                    second[0].At = meet.Value;
                    second[0].Bulge = after;
                    touch = true;
                }
                else if (type != 2)
                {
                    return -1;
                }
            }

            if (!touch && (b - a).Length > fuzz + slack)
            {
                return -1;
            }

            if (!apply)
            {
                return touch ? segments : segments + 1;
            }

            var joined = new List<PeditPoint>(first);

            if (touch)
            {
                PeditPoint end = joined[joined.Count - 1];
                end.Bulge = second[0].Bulge;
                end.Start = second[0].Start;
                end.End = second[0].End;
                joined.AddRange(second.Skip(1));
            }
            else
            {
                // A straight segment between the ends, as wide as the seed's last segment ends.
                PeditPoint end = joined[joined.Count - 1];
                double width = first.Count >= 2 ? first[first.Count - 2].End : 0.0;
                end.Bulge = 0.0;
                end.Start = width;
                end.End = width;
                joined.AddRange(second);
                segments++;
            }

            seed.Points = joined;
            return segments;
        }

        /// <summary>
        /// Where the last segment of one polyline, from a vertex to its end a, meets the first of another, from its start b on
        /// to a point, each run on as a line or round its circle: of two crossings, the one nearer the two ends together, as
        /// AutoCAD 2021's Extend chose in its console; null where they do not meet.
        /// </summary>
        private static GeoPoint2? PeditMeet(PeditPoint last, GeoPoint2 a, GeoPoint2 b, double bulge, GeoPoint2 next)
        {
            bool round = PeditCircle(last.At, a, last.Bulge, out GeoPoint2 c0, out double r0);
            bool round2 = PeditCircle(b, next, bulge, out GeoPoint2 c1, out double r1);
            var found = new List<GeoPoint2>();

            if (!round && !round2)
            {
                GeoPoint2? cross = PeditCross(last.At, a, b, next);

                if (cross.HasValue)
                {
                    found.Add(cross.Value);
                }
            }
            else if (round && round2)
            {
                PeditCircles(c0, r0, c1, r1, found);
            }
            else if (round)
            {
                PeditLineCircle(b, next, c0, r0, found);
            }
            else
            {
                PeditLineCircle(last.At, a, c1, r1, found);
            }

            if (found.Count == 0)
            {
                return null;
            }

            return found.OrderBy(p => (p - a).Length + (p - b).Length).ThenBy(p => p.X).ThenBy(p => p.Y).First();
        }

        /// <summary>The circle of a segment of a bulge, its centre and radius; false where the segment is straight.</summary>
        private static bool PeditCircle(
            GeoPoint2 from,
            GeoPoint2 to,
            double bulge,
            out GeoPoint2 center,
            out double radius
        )
        {
            GeoVector2 chord = to - from;

            if (bulge == 0.0 || chord.Length <= 0.0)
            {
                center = from;
                radius = 0.0;
                return false;
            }

            // The centre lies off the chord's middle, to its left where the arc runs counter-clockwise.
            double off = (1.0 / bulge - bulge) / 4.0;
            center = from + chord / 2.0 + new GeoVector2(-chord.Y, chord.X) * off;
            radius = (to - center).Length;
            return true;
        }

        /// <summary>Adds where the line through two points crosses a circle.</summary>
        private static void PeditLineCircle(
            GeoPoint2 p0,
            GeoPoint2 p1,
            GeoPoint2 center,
            double radius,
            List<GeoPoint2> found
        )
        {
            GeoVector2 u = p1 - p0;
            double length = u.Length;

            if (length <= 0.0)
            {
                return;
            }

            u = u / length;
            GeoVector2 w = center - p0;
            double along = w.X * u.X + w.Y * u.Y;
            double off = w.X * u.Y - w.Y * u.X;
            double left = radius * radius - off * off;

            if (left < 0.0)
            {
                return;
            }

            double half = Math.Sqrt(left);
            found.Add(p0 + u * (along - half));
            found.Add(p0 + u * (along + half));
        }

        /// <summary>Adds where two circles cross.</summary>
        private static void PeditCircles(GeoPoint2 c0, double r0, GeoPoint2 c1, double r1, List<GeoPoint2> found)
        {
            GeoVector2 between = c1 - c0;
            double d = between.Length;

            if (d <= 0.0 || d > r0 + r1 || d < Math.Abs(r0 - r1))
            {
                return;
            }

            double along = (d * d + r0 * r0 - r1 * r1) / (2.0 * d);
            double half = Math.Sqrt(Math.Max(0.0, r0 * r0 - along * along));
            GeoVector2 u = between / d;
            GeoPoint2 foot = c0 + u * along;
            found.Add(foot + new GeoVector2(-u.Y, u.X) * half);
            found.Add(foot - new GeoVector2(-u.Y, u.X) * half);
        }

        /// <summary>
        /// The bulge of a segment from one point to another run on or cut back to a point of its circle, at its end (or, with
        /// atEnd false, at its start), turning the way it turned; nought for a straight one, NaN where nothing of it is left.
        /// </summary>
        private static double PeditBulgeTo(GeoPoint2 from, GeoPoint2 to, double bulge, GeoPoint2 meet, bool atEnd)
        {
            if (!PeditCircle(from, to, bulge, out GeoPoint2 center, out double _))
            {
                return 0.0;
            }

            GeoPoint2 start = atEnd ? from : meet;
            GeoPoint2 end = atEnd ? meet : to;
            double sweep =
                Math.Atan2(end.Y - center.Y, end.X - center.X) - Math.Atan2(start.Y - center.Y, start.X - center.X);
            double turn = 2.0 * Math.PI;
            sweep = bulge > 0.0 ? ((sweep % turn) + turn) % turn : -((((-sweep) % turn) + turn) % turn);
            return Math.Abs(sweep) <= Tolerance.Default.EqualVector ? double.NaN : Math.Tan(sweep / 4.0);
        }

        /// <summary>Where the line through two points meets that through two others; null where they run parallel.</summary>
        private static GeoPoint2? PeditCross(GeoPoint2 a0, GeoPoint2 a1, GeoPoint2 b0, GeoPoint2 b1)
        {
            GeoVector2 u = a1 - a0;
            GeoVector2 v = b1 - b0;
            double cross = u.X * v.Y - u.Y * v.X;

            if (Math.Abs(cross) <= Tolerance.Default.EqualVector * u.Length * v.Length)
            {
                return null;
            }

            GeoVector2 w = b0 - a0;
            double t = (w.X * v.Y - w.Y * v.X) / cross;
            return a0 + u * t;
        }

        /// <summary>Whether PEDIT turns a line, an arc or a spline into a polyline without asking: PEDITACCEPT 1.</summary>
        private static bool PeditAccepts(CadCommandContext c) => c.Remembered("peditaccept", 0.0) != 0.0;

        /// <summary>
        /// A polyline made by PLINE, POLYGON, DONUT or PEDIT as PLINETYPE asks it: at 0 the old-style POLYLINE of its vertices and
        /// look, as AutoCAD 2021 makes one (RECTANG and REVCLOUD make LWPOLYLINEs whatever it is); a LWPOLYLINE otherwise, or
        /// in a drawing of R12.
        /// </summary>
        private static DxfCurve OfPlineType(CadCommandContext c, DxfPolyline made) =>
            made != null
            && c.Remembered("plinetype", 2.0) == 0.0
            && string.CompareOrdinal(c.Database.Version, "AC1014") >= 0
                ? made.ConvertTo(false)
                : (DxfCurve)made;

        /// <summary>
        /// Makes a POLYLINE a LWPOLYLINE in its own place at the end of the single PEDIT, as AutoCAD 2021's PLINETYPE 1 and 2 do
        /// to one edited and left straight; one fitted with a curve or a spline stays a POLYLINE, as one in a drawing of R12
        /// does, and PLINETYPE 0 leaves every POLYLINE one.
        /// </summary>
        private static void PeditToLightweight(CadCommandContext c, string handle)
        {
            if (
                c.Remembered("plinetype", 2.0) == 0.0
                || !(c.Document.FindEntity(handle) is DxfPolyline2d heavy)
                || heavy.PolyType != DxfPoly2dType.SimplePoly
                || string.CompareOrdinal(c.Database.Version, "AC1014") < 0
                || heavy.Count() < 2
            )
            {
                return;
            }

            string space = c.Document.FindSpace(handle)?.Name;
            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, handle, "Lightweight") }, s =>
            {
                new DxfPolyline().ConvertFrom(s.Document.FindEntity(handle), true);
                s.Record(new CadEditRecord(CadEditRecordKind.Added, handle) { Space = space });
            });
        }

        /// <summary>
        /// Edit vertex: the vertex marked with a cross on the canvas, the first the view shows, is moved along by Next and
        /// Previous, which stop at the ends; Break and Straighten mark a second and Go breaks the polyline between the two or
        /// straightens it; Insert, Move, Tangent and Width change at the marked vertex. A fitted polyline is edited on its frame
        /// and fitted again. The whole of it is one step of the run that Undo takes back.
        /// </summary>
        private static IEnumerable<CadPrompt> PeditVertex(CadCommandContext c, string handle, List<CadGroupMark> marks)
        {
            var polyline = (DxfCurve)c.Document.FindEntity(handle);
            List<PeditPoint> frame = PeditFrame(polyline);
            int at = 0;
            CadView view = c.Session.View;

            if (view != null)
            {
                DisplayBox box = view.VisibleBox;
                at = frame.FindIndex(v => box.Contains(PeditWorld(polyline, v.At).X, PeditWorld(polyline, v.At).Y));

                if (at < 0)
                {
                    c.Write("No visible vertices");
                    yield break;
                }
            }

            string way = "N";
            var state = new Ref<CadGroupMark>();

            while (true)
            {
                polyline = c.Document.FindEntity(handle) as DxfCurve;

                if (polyline == null)
                {
                    break;
                }

                frame = PeditFrame(polyline);
                at = Math.Max(0, Math.Min(at, frame.Count - 1));
                GeoPoint2 here = PeditWorld(polyline, frame[at].At);
                yield return CadPrompt
                    .ForKeyword("Enter a vertex editing option", PeditVertexOptions, way)
                    .Marking(here);

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
                else if (c.Input.Is("Width"))
                {
                    yield return CadPrompt.ForDistance(
                        "Specify starting width for next segment",
                        null,
                        null,
                        Length(frame[at].Start)
                    );
                    double start = c.Input.Kind == CadInputKind.Number ? c.Input.Number : frame[at].Start;
                    yield return CadPrompt.ForDistance(
                        "Specify ending width for next segment",
                        null,
                        null,
                        Length(start)
                    );
                    double end = c.Input.Kind == CadInputKind.Number ? c.Input.Number : start;

                    if (start >= 0.0 && end >= 0.0)
                    {
                        frame[at].Start = start;
                        frame[at].End = end;
                        PeditVertexChange(c, handle, state, frame, polyline.Closed);
                    }
                    else
                    {
                        c.Write("Value must be positive.");
                    }
                }
                else if (c.Input.Is("Tangent"))
                {
                    yield return CadPrompt.ForAngle("Specify direction of vertex tangent", here);

                    // A LWPOLYLINE keeps no tangent, as AutoCAD's keeps none; a POLYLINE's vertex keeps it for the next Fit.
                    if (c.Input.Kind == CadInputKind.Number && polyline is DxfPolyline2d && frame[at].Source != null)
                    {
                        double angle = c.Input.Number;
                        string vertex = frame[at].Source.Handle;
                        PeditVertexApply(c, handle, state, s =>
                        {
                            DxfVertex2d own = ((DxfPolyline2d)s.Document.FindEntity(handle)).First(v =>
                                string.Equals(v.Handle, vertex, StringComparison.OrdinalIgnoreCase)
                            );
                            own.Tangent = angle;
                            own.TangentUsed = true;
                        });
                    }
                }
                else if (c.Input.Is("Move") || c.Input.Is("Insert"))
                {
                    bool insert = c.Input.Is("Insert");
                    yield return CadPrompt.ForPoint(
                        insert ? "Specify location for new vertex" : "Specify new location for marked vertex",
                        here
                    );

                    if (!c.Input.IsPoint)
                    {
                        continue;
                    }

                    GeoPoint2 to = PeditPlane(polyline, c.Input.Point);

                    if (insert)
                    {
                        // The new vertex follows the marked one, as wide as that one's segment ends, and is marked next.
                        frame.Insert(
                            at + 1,
                            new PeditPoint
                            {
                                At = to,
                                Start = frame[at].End,
                                End = frame[at].End,
                            }
                        );
                        at++;
                    }
                    else
                    {
                        frame[at].At = to;
                    }

                    PeditVertexChange(c, handle, state, frame, polyline.Closed);
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
                            .Marking(PeditWorld(polyline, frame[other].At));

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

                    if (breaking)
                    {
                        if (a == b || (!polyline.Closed && a == 0 && b == frame.Count - 1))
                        {
                            c.Write("*Invalid*");
                            continue;
                        }

                        PeditBreak(c, handle, state, a, b);
                        at = a;
                    }
                    else
                    {
                        if (a == b)
                        {
                            // Go at the vertex marked first straightens the segment that leaves it.
                            frame[a].Bulge = 0.0;
                        }
                        else
                        {
                            frame.RemoveRange(a + 1, b - a - 1);
                            frame[a].Bulge = 0.0;
                        }

                        PeditVertexChange(c, handle, state, frame, polyline.Closed);
                        at = a;
                    }
                }
            }

            if (state.Value != null)
            {
                marks.Add(state.Value);
            }
        }

        /// <summary>Writes the frame of a polyline changed by Edit vertex, fitted again as it was.</summary>
        private static void PeditVertexChange(
            CadCommandContext c,
            string handle,
            Ref<CadGroupMark> state,
            List<PeditPoint> frame,
            bool closed
        ) => PeditVertexApply(c, handle, state, s =>
            {
                var polyline = (DxfCurve)s.Document.FindEntity(handle);
                PeditWrite(polyline, frame, closed, PeditType(polyline));
            });

        /// <summary>Makes a change of Edit vertex, the savepoint of the first being that which Undo goes back to.</summary>
        private static void PeditVertexApply(
            CadCommandContext c,
            string handle,
            Ref<CadGroupMark> state,
            Action<CadEditScope> change
        )
        {
            c.OpenUndoStep();

            if (state.Value == null)
            {
                state.Value = c.Document.MarkGroup();
            }

            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, handle, "Edit vertex") }, change);
        }

        /// <summary>
        /// Break between the vertices a and b (a below b) of the frame: an open polyline keeps the part up to a and a new one
        /// holds the part from b, a piece of one vertex left out; a closed one is opened into the part up to a and a new one
        /// from b round to the first vertex, as AutoCAD breaks it. The part of a curve-fitted polyline keeps the vertices it is
        /// drawn through, straight; that of a spline is fitted again where three vertices of its frame are left.
        /// </summary>
        private static void PeditBreak(CadCommandContext c, string handle, Ref<CadGroupMark> state, int a, int b)
        {
            PeditVertexApply(c, handle, state, s =>
            {
                var polyline = (DxfCurve)s.Document.FindEntity(handle);
                DxfPoly2dType type = PeditType(polyline);
                bool curve = type == DxfPoly2dType.FitCurvePoly;
                List<PeditPoint> points = curve ? PeditDrawn(polyline) : PeditFrame(polyline);
                List<int> frameAt = curve ? PeditFrameIndices(polyline) : Enumerable.Range(0, points.Count).ToList();
                int ia = frameAt[a];
                int ib = frameAt[b];
                List<PeditPoint> head = points.GetRange(0, ia + 1);
                List<PeditPoint> tail = points.GetRange(ib, points.Count - ib);

                if (polyline.Closed)
                {
                    tail.Add(points[0].Copy());
                }

                head[head.Count - 1].Bulge = 0.0;
                tail[tail.Count - 1].Bulge = 0.0;
                List<PeditPoint> own = head.Count >= 2 ? head : tail;
                List<PeditPoint> made = head.Count >= 2 && tail.Count >= 2 ? tail : null;
                PeditWrite(polyline, own, false, PeditPieceType(type, own.Count));

                if (made != null)
                {
                    DxfPolyline2d piece =
                        s.Append(s.Document.FindSpace(handle), PeditNew(polyline, made)) as DxfPolyline2d;
                    DxfPoly2dType fit = PeditPieceType(type, made.Count);

                    if (piece != null && fit != DxfPoly2dType.SimplePoly)
                    {
                        piece.ConvertToPolyType(fit);
                    }
                }
            });
        }

        /// <summary>How a piece Break leaves is fitted: a spline's again on three vertices of its frame or more, any other straight.</summary>
        private static DxfPoly2dType PeditPieceType(DxfPoly2dType type, int vertices) =>
            (type == DxfPoly2dType.QuadSplinePoly || type == DxfPoly2dType.CubicSplinePoly) && vertices >= 3
                ? type
                : DxfPoly2dType.SimplePoly;

        /// <summary>A new polyline of a piece, of the kind, look and plane of the one it came from, straight; it is fitted once in the drawing.</summary>
        private static DxfCurve PeditNew(DxfCurve from, List<PeditPoint> points)
        {
            if (from is DxfPolyline light)
            {
                var made = new DxfPolyline();

                for (int i = 0; i < points.Count; i++)
                {
                    made.AddVertexAt(i, points[i].At, points[i].Bulge, points[i].Start, points[i].End);
                }

                made.SetPropertiesFrom(light);
                made.Normal = light.Normal;
                made.Elevation = light.Elevation;
                made.Thickness = light.Thickness;
                made.Plinegen = light.Plinegen;
                return made;
            }

            var heavy = (DxfPolyline2d)from;
            var piece = new DxfPolyline2d(
                DxfPoly2dType.SimplePoly,
                points.Select(p => new GeoPoint3(p.At.X, p.At.Y, 0.0)),
                heavy.Elevation,
                false,
                0.0,
                0.0,
                points.Select(p => p.Bulge)
            );
            List<DxfVertex2d> vertices = piece.ToList();

            for (int i = 0; i < points.Count; i++)
            {
                vertices[i].StartWidth = points[i].Start;
                vertices[i].EndWidth = points[i].End;
            }

            piece.SetPropertiesFrom(heavy);
            piece.Normal = heavy.Normal;
            piece.Thickness = heavy.Thickness;
            piece.LinetypeGenerationOn = heavy.LinetypeGenerationOn;
            return piece;
        }

        private static void PeditSetClosed(DxfCurve polyline, bool closed)
        {
            if (polyline is DxfPolyline light)
            {
                light.Closed = closed;
            }
            else
            {
                ((DxfPolyline2d)polyline).Closed = closed;
            }
        }

        /// <summary>A vertex of a polyline as PEDIT edits it, in the system of the polyline's normal.</summary>
        private sealed class PeditPoint
        {
            public GeoPoint2 At { get; set; }

            public double Bulge { get; set; }

            public double Start { get; set; }

            public double End { get; set; }

            public double? Tangent { get; set; }

            public DxfVertex2d Source { get; set; }

            public PeditPoint Copy() => (PeditPoint)MemberwiseClone();
        }

        /// <summary>A polyline being joined by Multiple's Join: its vertices, closed or not.</summary>
        private sealed class PeditChain
        {
            private readonly GeoVector3 _normal;
            private readonly double _elevation;

            public PeditChain(string handle, DxfCurve polyline, List<PeditPoint> points)
            {
                Handle = handle;
                Points = points;
                _normal = polyline is DxfPolyline light ? light.Normal : ((DxfPolyline2d)polyline).Normal;
                _elevation = polyline is DxfPolyline flat ? flat.Elevation : ((DxfPolyline2d)polyline).Elevation;
            }

            public string Handle { get; }

            public List<PeditPoint> Points { get; set; }

            public bool Closed { get; set; }

            public bool Gone { get; set; }

            public bool SamePlane(PeditChain other, double slack) =>
                (_normal - other._normal).Length <= Tolerance.Default.EqualVector
                && Math.Abs(_elevation - other._elevation) <= slack;
        }

        /// <summary>
        /// The frame of a polyline: every vertex of a LWPOLYLINE or a straight POLYLINE; the vertices of a fitted one it is fitted
        /// on, straight, each with the widths its segment runs between.
        /// </summary>
        private static List<PeditPoint> PeditFrame(DxfCurve polyline)
        {
            var frame = new List<PeditPoint>();

            if (polyline is DxfPolyline light)
            {
                for (int i = 0; i < light.NumberOfVertices; i++)
                {
                    frame.Add(
                        new PeditPoint
                        {
                            At = light.GetPoint2dAt(i),
                            Bulge = light.GetBulgeAt(i),
                            Start = light.GetStartWidthAt(i),
                            End = light.GetEndWidthAt(i),
                        }
                    );
                }

                return frame;
            }

            var heavy = (DxfPolyline2d)polyline;
            DxfPoly2dType type = heavy.PolyType;
            bool spline = type == DxfPoly2dType.QuadSplinePoly || type == DxfPoly2dType.CubicSplinePoly;
            List<DxfVertex2d> vertices = heavy.ToList();

            for (int i = 0; i < vertices.Count; i++)
            {
                DxfVertex2d vertex = vertices[i];
                DxfVertex2dType kind = vertex.VertexType;

                if (
                    spline
                        ? kind != DxfVertex2dType.SplineControlVertex
                        : kind == DxfVertex2dType.CurveFitVertex || kind == DxfVertex2dType.SplineFitVertex
                )
                {
                    continue;
                }

                // A curve fit moved the end width of a segment onto the vertex it put in its middle.
                double end =
                    type == DxfPoly2dType.FitCurvePoly
                    && i + 1 < vertices.Count
                    && vertices[i + 1].VertexType == DxfVertex2dType.CurveFitVertex
                        ? vertices[i + 1].EndWidth
                        : vertex.EndWidth;
                frame.Add(
                    new PeditPoint
                    {
                        At = new GeoPoint2(vertex.Position.X, vertex.Position.Y),
                        Bulge = type == DxfPoly2dType.SimplePoly ? vertex.Bulge : 0.0,
                        Start = vertex.StartWidth,
                        End = end,
                        Tangent = vertex.TangentUsed ? vertex.Tangent : (double?)null,
                        Source = vertex,
                    }
                );
            }

            return frame;
        }

        /// <summary>The vertices a POLYLINE is drawn through, those of its frame and those its curve fitting made, as plain vertices.</summary>
        private static List<PeditPoint> PeditDrawn(DxfCurve polyline) =>
            ((DxfPolyline2d)polyline)
                .Where(v => v.VertexType != DxfVertex2dType.SplineControlVertex)
                .Select(v => new PeditPoint
                {
                    At = new GeoPoint2(v.Position.X, v.Position.Y),
                    Bulge = v.Bulge,
                    Start = v.StartWidth,
                    End = v.EndWidth,
                })
                .ToList();

        /// <summary>Where each vertex of the frame of a curve-fitted POLYLINE is among the vertices it is drawn through.</summary>
        private static List<int> PeditFrameIndices(DxfCurve polyline)
        {
            var found = new List<int>();
            int i = 0;

            foreach (DxfVertex2d vertex in (DxfPolyline2d)polyline)
            {
                if (vertex.VertexType == DxfVertex2dType.SplineControlVertex)
                {
                    continue;
                }

                if (vertex.VertexType != DxfVertex2dType.CurveFitVertex)
                {
                    found.Add(i);
                }

                i++;
            }

            return found;
        }

        /// <summary>
        /// Writes vertices into a polyline, closed or not, and fits it as given: a LWPOLYLINE's own vertices set, a POLYLINE
        /// straightened, its vertices set, taken out or added, and fitted again.
        /// </summary>
        private static void PeditWrite(DxfCurve polyline, List<PeditPoint> points, bool closed, DxfPoly2dType type)
        {
            if (polyline is DxfPolyline light)
            {
                int had = light.NumberOfVertices;

                for (int i = 0; i < points.Count; i++)
                {
                    PeditPoint p = points[i];

                    if (i < had)
                    {
                        light.SetPointAt(i, p.At);
                        light.SetBulgeAt(i, p.Bulge);
                        light.SetStartWidthAt(i, p.Start);
                        light.SetEndWidthAt(i, p.End);
                    }
                    else
                    {
                        light.AddVertexAt(i, p.At, p.Bulge, p.Start, p.End);
                    }
                }

                if (points.Count < had)
                {
                    light.Reset(true, points.Count);
                }

                light.Closed = closed;
                return;
            }

            var heavy = (DxfPolyline2d)polyline;
            heavy.Straighten();
            double z = heavy.Elevation;
            List<DxfVertex2d> own = heavy.ToList();

            for (int i = 0; i < points.Count; i++)
            {
                PeditPoint p = points[i];
                DxfVertex2d vertex;

                if (i < own.Count)
                {
                    vertex = own[i];
                    vertex.Position = new GeoPoint3(p.At.X, p.At.Y, z);
                    vertex.Bulge = p.Bulge;
                    vertex.StartWidth = p.Start;
                    vertex.EndWidth = p.End;
                }
                else
                {
                    vertex = new DxfVertex2d(new GeoPoint3(p.At.X, p.At.Y, z), p.Bulge, p.Start, p.End, 0.0);
                    heavy.AppendVertex(vertex);
                }

                vertex.TangentUsed = p.Tangent.HasValue;

                if (p.Tangent.HasValue)
                {
                    vertex.Tangent = p.Tangent.Value;
                }
            }

            for (int i = own.Count - 1; i >= points.Count; i--)
            {
                own[i].Erase();
            }

            heavy.Closed = closed;
            bool spline = type == DxfPoly2dType.QuadSplinePoly || type == DxfPoly2dType.CubicSplinePoly;

            if (type != DxfPoly2dType.SimplePoly && points.Count >= (closed && spline ? 3 : 2))
            {
                heavy.ConvertToPolyType(type);
            }
        }

        /// <summary>The polyline's vertices turned to run the other way: each segment's bulge turned over and its widths swapped.</summary>
        private static List<PeditPoint> PeditReversed(List<PeditPoint> points)
        {
            int n = points.Count;
            var turned = new List<PeditPoint>(n);

            for (int j = 0; j < n; j++)
            {
                PeditPoint point = points[n - 1 - j].Copy();
                PeditPoint leaves = j < n - 1 ? points[n - 2 - j] : null;
                point.Bulge = leaves == null ? 0.0 : -leaves.Bulge;
                point.Start = leaves == null ? 0.0 : leaves.End;
                point.End = leaves == null ? 0.0 : leaves.Start;
                turned.Add(point);
            }

            return turned;
        }

        private static List<PeditPoint> PeditCopy(List<PeditPoint> points) => points.Select(p => p.Copy()).ToList();

        /// <summary>A point of a polyline's plane where it is in the world, at the polyline's elevation.</summary>
        private static GeoPoint2 PeditWorld(DxfCurve polyline, GeoPoint2 point)
        {
            double z = polyline is DxfPolyline light ? light.Elevation : ((DxfPolyline2d)polyline).Elevation;
            GeoPoint3 world = polyline.Ecs.Transform(new GeoPoint3(point.X, point.Y, z));
            return new GeoPoint2(world.X, world.Y);
        }

        /// <summary>A point picked in the world in the system of the polyline's normal.</summary>
        private static GeoPoint2 PeditPlane(DxfCurve polyline, GeoPoint2 point)
        {
            GeoPoint3 own = polyline.Ecs.Inverse(Tolerance.Default).Transform(new GeoPoint3(point.X, point.Y, 0.0));
            return new GeoPoint2(own.X, own.Y);
        }
    }
}

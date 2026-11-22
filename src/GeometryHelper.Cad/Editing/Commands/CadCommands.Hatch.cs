using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The hatch commands, in AutoCAD's words: HATCH (points picked or objects selected, the settings by the window's dialog or
    /// typed), HATCHEDIT and BOUNDARY.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// HATCH: "Pick internal point or [Select objects/Undo/seTtings]:" until Enter, each point finding the area round it
        /// with its islands (<see cref="CadBoundaryFinder"/>); Select objects takes closed objects instead; seTtings shows the
        /// window's dialog, or asks the pattern typed. Enter makes one HATCH of every area, on the current layer unless the
        /// settings give another, in the document's <see cref="CadDocument.HatchSettings"/>, following its objects where the
        /// settings are associative (AutoCAD makes the loops anew when such an object moves; the presenter keeps the association
        /// and does not), one undo step. The hatch is drawn behind its boundary, or where -HATCH's DRaw order says, as AutoCAD's
        /// HPDRAWORDER of 3 does: it lies under the lowest object its loops are made of. A gap in the boundary is closed over
        /// where the settings' <see cref="CadHatchSettings.GapTolerance"/> reaches it. Select objects goes back to the point
        /// prompt, as AutoCAD's does; remove Boundaries and Origin, taken but not shown, are -HATCH's own questions, which the
        /// Hatch Creation tab starts; island detection, separate hatches, retained boundaries and the draw order are the ones
        /// -HATCH keeps. With HPDLGMODE 1 the window's Hatch dialog is shown first.
        /// </summary>
        private static IEnumerable<CadPrompt> Hatch(CadCommandContext c)
        {
            var picks = new List<IReadOnlyList<CadBoundaryLoop>>();
            var run = new HatchRun();

            // The area last found under the cursor, kept while the cursor stays in it and the drawing is as it was, which the
            // pick index (made anew on every change) tells.
            IReadOnlyList<CadBoundaryLoop> region = null;
            PickIndex regionIndex = null;

            IEnumerable<DxfEntity> Previewed(CadInput input)
            {
                PickIndex index = c.Document.GetPickIndex(c.Space);

                if (region == null || !ReferenceEquals(index, regionIndex) || !Within(region, input.Point))
                {
                    regionIndex = index;
                    region = CadBoundaryFinder.Find(
                        c.Document,
                        c.Space,
                        input.Point,
                        c.Session.Tolerance,
                        c.Session.WorkView?.VisibleBox,
                        null,
                        c.Document.HatchSettings.GapTolerance,
                        GapPixel(c)
                    );
                }

                if (region == null)
                {
                    return null;
                }

                CadHatchSettings settings = c.Document.HatchSettings;
                DxfHatch hatch = Current(c, new DxfHatch());
                string searched = c.Database.PatternSearchPath;

                try
                {
                    settings.ApplyTo(hatch, c.Database, c.Document);
                }
                finally
                {
                    // The search path the settings set is the made hatch's to set, inside its undo step.
                    if (!string.Equals(c.Database.PatternSearchPath, searched, StringComparison.Ordinal))
                    {
                        c.Database.PatternSearchPath = searched;
                    }
                }

                AppendLoops(hatch, KeptLoops(new List<IReadOnlyList<CadBoundaryLoop>>(picks) { region }));
                return new[] { hatch };
            }

            // HPDLGMODE 1 is the Hatch dialog first, as AutoCAD 2021 shows it; with no window to show it, none.
            if (c.Remembered(HatchDialogModeKey, 2.0) == 1.0)
            {
                foreach (CadPrompt prompt in HatchSettingsAsked(c, false))
                {
                    yield return prompt;
                }
            }

            while (true)
            {
                yield return CadPrompt
                    .ForPoint("Pick internal point", null, new[] { "Select objects", "Undo", "seTtings" })
                    .Hiding("remove Boundaries", "Origin")
                    .Previewing(CadPreview.Making(c, Previewed));

                if (c.Input.IsPoint)
                {
                    c.Write("Analyzing the selected data...");
                    c.Write("Analyzing internal islands...");
                    IReadOnlyList<CadBoundaryLoop> loops = CadBoundaryFinder.Find(
                        c.Document,
                        c.Space,
                        c.Input.Point,
                        c.Session.Tolerance,
                        c.Session.WorkView?.VisibleBox,
                        null,
                        c.Document.HatchSettings.GapTolerance,
                        GapPixel(c)
                    );

                    if (loops == null)
                    {
                        c.Write("Valid hatch boundary not found.");
                    }
                    else
                    {
                        // Island detection off (the Options panel's No island detection) keeps the outer loop alone.
                        picks.Add(
                            c.Remembered(HatchIslandsKey, 1.0) != 0.0 ? loops : loops.Where(l => l.Depth == 0).ToList()
                        );
                    }
                }
                else if (c.Input.Is("remove Boundaries"))
                {
                    foreach (CadPrompt prompt in HatchRemoveBoundaries(c, picks))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Origin"))
                {
                    // The Origin panel of the Hatch Creation tab: -HATCH's Origin, kept in the settings the hatch is made with.
                    foreach (CadPrompt prompt in HatchOrigin(c, c.Document.HatchSettings, run))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Undo"))
                {
                    if (picks.Count > 0)
                    {
                        picks.RemoveAt(picks.Count - 1);
                    }
                    else
                    {
                        c.Write("Nothing to undo.");
                    }
                }
                else if (c.Input.Is("seTtings"))
                {
                    foreach (CadPrompt prompt in HatchSettingsAsked(c))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Select objects"))
                {
                    yield return CadPrompt.ForSelection();
                    IReadOnlyList<CadBoundaryLoop> loops = LoopsOfSelected(c, out int passed);

                    if (passed > 0)
                    {
                        c.Write(
                            passed.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                + (passed == 1 ? " object is" : " objects are")
                                + " not closed and cannot be a hatch boundary."
                        );
                    }

                    if (loops.Count > 0)
                    {
                        picks.Add(loops);
                    }
                }
                else
                {
                    break;
                }
            }

            if (picks.Count > 0)
            {
                // The Options panel's separate hatches, retained boundaries and draw order are the ones -HATCH keeps.
                MakeHatches(
                    c,
                    picks,
                    c.Document.HatchSettings,
                    c.Remembered(HatchSeparateKey, 0.0) != 0.0,
                    c.Remembered(HatchRetainKey, 0.0) != 0.0,
                    (int)c.Remembered(HatchOrderKey, 3.0),
                    run
                );
            }
        }

        /// <summary>
        /// The answer of the window's Hatch dialog closed by Click to set new origin; asked again, the point picked is its
        /// payload, or these words where none was.
        /// </summary>
        private const string HatchOriginAnswer = "Click to set new origin";

        /// <summary>
        /// The settings of HATCH: the window's dialog, closed and shown again around "Specify origin point:" for its Click to
        /// set new origin; with no window, the pattern and its scale and angle asked, unless <paramref name="typedWithNoWindow"/>
        /// is false (HPDLGMODE 1 shows the dialog at the start, and asks nothing where there is none).
        /// </summary>
        private static IEnumerable<CadPrompt> HatchSettingsAsked(CadCommandContext c, bool typedWithNoWindow = true)
        {
            CadUiRequestEventArgs asked = c.Session.Ask("HATCHSETTINGS", c.Document, null);

            // Click to set new origin closes the dialog: the point is asked and the dialog shown again with it, until it closes
            // otherwise. Esc at the point shows it again as it was.
            while (asked.Handled && asked.Answer == HatchOriginAnswer)
            {
                yield return CadPrompt.ForPoint("Specify origin point");
                object picked = c.Input.IsPoint ? (object)c.Input.Point : null;
                asked = c.Session.Ask("HATCHSETTINGS", c.Document, picked ?? HatchOriginAnswer);
            }

            if (asked.Handled || !typedWithNoWindow)
            {
                yield break;
            }

            CadHatchSettings settings = c.Document.HatchSettings.Clone();

            foreach (CadPrompt prompt in HatchPropertiesTyped(c, settings))
            {
                yield return prompt;
            }

            c.Document.HatchSettings = settings;
        }

        /// <summary>
        /// The properties of a hatch asked as -HATCH asks them: "Enter a pattern name or [?/Solid/User defined/Gradient]", then
        /// for a pattern its scale and angle, for lines of the user's their angle, spacing and whether double, for a gradient its
        /// name; ? lists the patterns of the presenter's own set.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchPropertiesTyped(CadCommandContext c, CadHatchSettings settings)
        {
            string offered =
                settings.Type == CadHatchType.Solid ? "Solid"
                : settings.Type == CadHatchType.UserDefined ? "_USER"
                : settings.Type == CadHatchType.Gradient ? settings.GradientName
                : settings.PatternName;

            while (true)
            {
                yield return CadPrompt.ForKeywordOrText(
                    "Enter a pattern name or",
                    new[] { "?", "Solid", "User defined", "Gradient" },
                    offered
                );

                if (c.Input.Is("?"))
                {
                    c.Write(string.Join(", ", CadPatternFile.BuiltIn.Select(p => p.Name)) + ".");
                    continue;
                }

                break;
            }

            if (c.Input.Is("Solid"))
            {
                settings.Type = CadHatchType.Solid;
                yield break;
            }

            if (c.Input.Is("Gradient"))
            {
                settings.Type = CadHatchType.Gradient;

                while (true)
                {
                    yield return CadPrompt.ForKeywordOrText(
                        "Enter a gradient name or",
                        new[] { "?", "Single color", "Two color" },
                        settings.GradientName
                    );

                    if (c.Input.Is("?"))
                    {
                        c.Write(string.Join(", ", CadHatchSettings.GradientNames) + ".");
                        continue;
                    }

                    break;
                }

                if (c.Input.Is("Single color") || c.Input.Is("Two color"))
                {
                    // Either goes back to the first prompt with the gradient as it was: its name and angle are not asked again.
                    bool one = c.Input.Is("Single color");
                    settings.GradientOneColor = one;

                    foreach (CadPrompt prompt in GradientColorsTyped(c, settings, one))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                if (c.Input.Kind == CadInputKind.Text)
                {
                    string name = CadHatchSettings.GradientNames.FirstOrDefault(n =>
                        string.Equals(n, c.Input.Text.Trim(), StringComparison.OrdinalIgnoreCase)
                    );

                    if (name == null)
                    {
                        c.Write("Unknown gradient: " + string.Join(", ", CadHatchSettings.GradientNames) + ".");
                    }
                    else
                    {
                        settings.GradientName = name;
                    }
                }

                yield return CadPrompt.ForAngle(
                    "Specify an angle for the gradient",
                    null,
                    null,
                    Counted(c, settings.Angle * Math.PI / 180.0)
                );

                if (c.Input.Kind == CadInputKind.Number)
                {
                    settings.Angle = c.Input.Number * 180.0 / Math.PI;
                }

                yield return CadPrompt.ForKeyword(
                    "Center the gradient",
                    new[] { "Yes", "No" },
                    settings.GradientCentered ? "Yes" : "No"
                );

                if (c.Input.Is("Yes") || c.Input.Is("No"))
                {
                    settings.GradientCentered = c.Input.Is("Yes");
                }

                yield break;
            }

            bool user = c.Input.Is("User defined");

            if (!user && c.Input.Kind == CadInputKind.Text)
            {
                settings.Type = CadHatchType.Pattern;
                settings.PatternName = c.Input.Text.Trim().ToUpperInvariant();
                settings.PatternFile = null;
            }
            else if (user)
            {
                settings.Type = CadHatchType.UserDefined;
            }

            if (settings.Type == CadHatchType.Pattern)
            {
                yield return CadPrompt.ForDistance(
                    "Specify a scale for the pattern",
                    null,
                    null,
                    Length(settings.Scale)
                );

                if (c.Input.Kind == CadInputKind.Number && c.Input.Number > 0.0)
                {
                    settings.Scale = c.Input.Number;
                }
            }

            yield return CadPrompt.ForAngle(
                "Specify an angle for the pattern",
                null,
                null,
                Counted(c, settings.Angle * Math.PI / 180.0)
            );

            if (c.Input.Kind == CadInputKind.Number)
            {
                settings.Angle = c.Input.Number * 180.0 / Math.PI;
            }

            if (settings.Type != CadHatchType.UserDefined)
            {
                yield break;
            }

            yield return CadPrompt.ForDistance(
                "Specify spacing between the lines",
                null,
                null,
                Length(settings.Spacing)
            );

            if (c.Input.Kind == CadInputKind.Number && c.Input.Number > 0.0)
            {
                settings.Spacing = c.Input.Number;
            }

            yield return CadPrompt.ForKeyword(
                "Double hatch area?",
                new[] { "Yes", "No" },
                settings.Double ? "Yes" : "No"
            );

            if (c.Input.Is("Yes") || c.Input.Is("No"))
            {
                settings.Double = c.Input.Is("Yes");
            }
        }

        /// <summary>
        /// Appends one hatch of the loops to the current space inside an edit: given the settings, the loops, and the objects
        /// of the loops where the settings are associative. Gives the fault of the pattern where it is in no file.
        /// </summary>
        private static DxfHatch AppendHatch(
            CadCommandContext c,
            CadEditScope s,
            List<CadBoundaryLoop> loops,
            CadHatchSettings settings,
            GeoPoint2? onceOrigin,
            HatchRun run,
            IReadOnlyDictionary<CadBoundaryLoop, DxfPolyline> polylines,
            out string fault
        )
        {
            DxfHatch hatch = s.Append(c.Space, Current(c, new DxfHatch()));
            fault = WithOrigin(settings, loops, onceOrigin).ApplyTo(hatch, s.Database, s.Document);
            AppendLoops(hatch, loops, run);
            AppendSeeds(hatch, loops);

            if (settings.Associative)
            {
                var objects = new List<IReadOnlyList<DxfEntity>>();

                foreach (CadBoundaryLoop loop in loops)
                {
                    // A loop drawn follows the polyline kept of it, where there is one.
                    objects.Add(
                        polylines != null && polylines.TryGetValue(loop, out DxfPolyline kept)
                            ? new List<DxfEntity> { kept }
                            : loop.Sources.Select(h => s.Document.FindEntity(h)).Where(e => e != null).ToList()
                    );
                }

                hatch.SetAssociatedObjects(objects);
            }

            // An annotative hatch is made once its loops are in, as its context holds them.
            if (settings.Annotative)
            {
                string why = MakeAnnotative(s.Database, hatch);
                fault = fault ?? why;
            }

            return hatch;
        }

        /// <summary>
        /// The size of a pixel of the view in drawing units, which the gap tolerance is widened by as AutoCAD 2021 does; nought
        /// where there is no view, as in a test.
        /// </summary>
        private static double GapPixel(CadCommandContext c) =>
            c.Session.WorkView is CadView view && view.PixelsPerUnit > 0.0 ? 1.0 / view.PixelsPerUnit : 0.0;

        /// <summary>The closed polyline of a loop, its corners and the bulges of its edges, as BOUNDARY makes it.</summary>
        private static DxfPolyline LoopPolyline(CadBoundaryLoop loop)
        {
            var polyline = new DxfPolyline();

            for (int i = 0; i < loop.Vertices.Count; i++)
            {
                polyline.AddVertexAt(i, loop.Vertices[i], loop.Bulges[i], 0.0, 0.0);
            }

            polyline.Closed = true;
            return polyline;
        }

        /// <summary>
        /// The loops of the picks a hatch is made of, all of each pick's: AutoCAD 2021 stores every loop whatever the island style
        /// is (three squares one in another, 91 3 at HPISLANDDETECTION 0, 1 and 2) and the style in group 75 decides which areas
        /// are filled.
        /// </summary>
        private static List<CadBoundaryLoop> KeptLoops(List<IReadOnlyList<CadBoundaryLoop>> picks)
        {
            var loops = new List<CadBoundaryLoop>();

            foreach (IReadOnlyList<CadBoundaryLoop> pick in picks)
            {
                loops.AddRange(pick);
            }

            return loops;
        }

        /// <summary>
        /// Adds the loops to a hatch, flagged as AutoCAD 2021 flags them: External for the outer loop of each pick, Outermost for
        /// one inside it, none for one inside that (a loop flagged Derived, as a loop picked is in AutoCAD, makes it expect the
        /// pixel size, group 47, which the library does not write, and refuse the file: "expected group code 47"). A closed
        /// object selected is stored as its edges, as AutoCAD stores it: lines for the straight sides, arcs for the bulged ones,
        /// from the first vertex and the way the polyline runs, a circle as one arc round; a loop found round a point keeps its
        /// polyline. A loop left open by draW boundary (those of <paramref name="run"/>) is a loop of edges flagged NotClosed, not
        /// closed across.
        /// </summary>
        private static void AppendLoops(DxfHatch hatch, List<CadBoundaryLoop> loops, HatchRun run = null)
        {
            foreach (CadBoundaryLoop loop in loops)
            {
                DxfHatchLoopTypes type =
                    loop.Depth == 0 ? DxfHatchLoopTypes.External
                    : loop.Depth == 1 ? DxfHatchLoopTypes.Outermost
                    : DxfHatchLoopTypes.Default;

                if (run != null && run.Drawn.Contains(loop))
                {
                    if (run.Open.Contains(loop))
                    {
                        var corners = loop.Vertices.Select(v => new GeoPoint3(v.X, v.Y, 0.0)).ToList();
                        var up = new GeoVector3(0.0, 0.0, 1.0);
                        var chain = new GeoPolylineArc3(corners, loop.Bulges, loop.Bulges.Select(b => up));
                        hatch.AppendLoop(new DxfHatchLoop(type | DxfHatchLoopTypes.NotClosed, new object[] { chain }));
                        continue;
                    }
                }

                if (loop.Curve != null)
                {
                    hatch.AppendLoop(new DxfHatchLoop(type, new[] { loop.Curve }));
                }
                else if (!loop.Selected || !AppendEdges(hatch, type, loop))
                {
                    hatch.AppendLoop(type, loop.Vertices, loop.Bulges);
                }
            }
        }

        /// <summary>
        /// Adds a loop of corners and bulges as its edges, in the world on the floor: one circle for the two half circles a circle
        /// selected is, else the loop's own sides in its order. False, and nothing added, where the corners make no sides a hatch
        /// takes (the loop is then kept as a polyline).
        /// </summary>
        private static bool AppendEdges(DxfHatch hatch, DxfHatchLoopTypes type, CadBoundaryLoop loop)
        {
            var up = new GeoVector3(0.0, 0.0, 1.0);
            var corners = loop.Vertices.Select(v => new GeoPoint3(v.X, v.Y, 0.0)).ToList();

            try
            {
                object edge =
                    corners.Count == 2 && Math.Abs(loop.Bulges[0] - 1.0) < 1E-9 && Math.Abs(loop.Bulges[1] - 1.0) < 1E-9
                        ? new GeoCircle3(
                            new GeoPoint3(
                                (corners[0].X + corners[1].X) / 2.0,
                                (corners[0].Y + corners[1].Y) / 2.0,
                                0.0
                            ),
                            up,
                            corners[0].DistanceTo(corners[1]) / 2.0
                        )
                        : (object)new GeoPolygonArc3(corners, loop.Bulges, loop.Bulges.Select(b => up));
                hatch.AppendLoop(new DxfHatchLoop(type, new[] { edge }));
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Gives a hatch its seed points as AutoCAD 2021 does: the point picked once for each loop it found, and for objects
        /// selected one point at the origin; none for the loops draW boundary drew.
        /// </summary>
        private static void AppendSeeds(DxfHatch hatch, List<CadBoundaryLoop> loops)
        {
            foreach (CadBoundaryLoop loop in loops)
            {
                if (loop.Seed is GeoPoint2 seed)
                {
                    hatch.AppendSeedPoint(seed);
                }
            }

            if (hatch.NumSeedPoints == 0 && loops.Exists(l => l.Selected))
            {
                hatch.AppendSeedPoint(new GeoPoint2(0.0, 0.0));
            }
        }

        /// <summary>
        /// Whether a point lies in the area of loops HATCH found: inside the outer loop and in none of its islands, so that the
        /// same loops are found round it.
        /// </summary>
        private static bool Within(IReadOnlyList<CadBoundaryLoop> loops, GeoPoint2 point)
        {
            foreach (CadBoundaryLoop loop in loops)
            {
                if (loop.Depth == 0 ? !loop.Contains(point) : loop.Contains(point))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// HATCHEDIT: "Select hatch object:", then the window's dialog on its settings, or its pattern typed, applied as one undo
        /// step. A hatch whose owner allows it no change of its own (a section hatch of a model drawing) is refused before
        /// anything is asked, in the owner's words.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchEdit(CadCommandContext c)
        {
            foreach (
                CadPrompt first in c.SelectOneFirst(
                    CadPrompt
                        .ForEntity("Select hatch object")
                        .Expecting("Window/Last/Crossing/BOX/ALL/Fence/WPolygon/CPolygon"),
                    e => e is DxfHatch
                )
            )
            {
                yield return first;
            }

            string handle = c.Input.Handles.FirstOrDefault();

            if (handle == null)
            {
                yield break;
            }

            if (!(c.Document.FindEntity(handle) is DxfHatch hatch))
            {
                c.Write("Selected object is not a hatch.");
                yield break;
            }

            if (CadLayerEditor.IsLocked(c.Database, hatch.Layer))
            {
                c.Write("1 was on a locked layer.");
                yield break;
            }

            var planned = new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "Hatch") };

            if (!c.Document.EditGate.Allows(c.Document, hatch, planned[0], out string reason))
            {
                c.Write(reason ?? "The hatch " + handle + " may not be changed.");
                yield break;
            }

            CadHatchSettings settings = CadHatchSettings.Of(hatch);
            c.Document.HatchBeingEdited = settings;

            CadUiRequestEventArgs asked = c.Session.Ask("HATCHEDIT", c.Document);

            // Click to set new origin closes the dialog here as it does in HATCH: the point is asked and the dialog shown again.
            while (asked.Handled && asked.Answer == HatchOriginAnswer)
            {
                yield return CadPrompt.ForPoint("Specify origin point");
                object picked = c.Input.IsPoint ? (object)c.Input.Point : null;
                asked = c.Session.Ask("HATCHEDIT", c.Document, picked ?? HatchOriginAnswer);
            }

            if (asked.Handled)
            {
                settings = c.Document.HatchBeingEdited;
            }
            else
            {
                foreach (CadPrompt prompt in HatchPropertiesTyped(c, settings))
                {
                    yield return prompt;
                }
            }

            c.Document.HatchBeingEdited = null;

            if (settings == null)
            {
                yield break;
            }

            string fault = null;
            CadEditResult result = c.Apply(planned, s => fault = ApplyEditedSettings(s, handle, settings));

            if (fault != null)
            {
                c.Write(fault);
            }

            if (!result.Succeeded && result.Message != null)
            {
                c.Write(result.Message);
            }
        }

        /// <summary>
        /// Gives a hatch of the drawing the settings an edit of it ends with, inside the edit: an annotative hatch whose pattern is
        /// laid again is made plain first and annotative again after, at the scale of its space, its other scales given back, as
        /// the lines of its contexts are those of the pattern it had; a hatch that no longer follows its objects is let go of
        /// them. Gives the fault of the pattern where it is in no file.
        /// </summary>
        internal static string ApplyEditedSettings(CadEditScope s, string handle, CadHatchSettings settings)
        {
            var edited = (DxfHatch)s.Document.FindEntity(handle);
            bool annotative = edited.Annotative == DxfAnnotativeStates.True;
            bool patternChanged = !SamePattern(CadHatchSettings.Of(edited), settings);

            List<DxfAnnotationScale> held =
                annotative && patternChanged && settings.Annotative
                    ? edited.GetContexts().OfType<DxfAnnotationScale>().ToList()
                    : new List<DxfAnnotationScale>();

            if (annotative && (patternChanged || !settings.Annotative))
            {
                edited.Annotative = DxfAnnotativeStates.False;
            }

            string fault = settings.ApplyTo(edited, s.Database, s.Document);

            if (settings.Annotative && edited.Annotative != DxfAnnotativeStates.True)
            {
                fault = fault ?? MakeAnnotative(s.Database, edited);

                foreach (DxfAnnotationScale scale in held)
                {
                    if (edited.Annotative == DxfAnnotativeStates.True && !edited.HasContext(scale))
                    {
                        edited.AddContext(scale);
                    }
                }
            }

            if (!settings.Associative && edited.Associative)
            {
                edited.Associative = false;
            }

            return fault;
        }

        /// <summary>Whether two sets of settings lay the same pattern: the type, the pattern and its angle, scale, spacing, double, and the gradient.</summary>
        private static bool SamePattern(CadHatchSettings a, CadHatchSettings b) =>
            a.Type == b.Type
            && a.PatternName == b.PatternName
            && a.Angle == b.Angle
            && a.Scale == b.Scale
            && a.Spacing == b.Spacing
            && a.Double == b.Double
            && a.IslandStyle == b.IslandStyle
            && a.GradientName == b.GradientName
            && a.GradientColor1.Equals(b.GradientColor1)
            && a.GradientColor2.Equals(b.GradientColor2)
            && a.GradientOneColor == b.GradientOneColor
            && a.GradientTint == b.GradientTint
            && a.GradientCentered == b.GradientCentered;

        /// <summary>
        /// BOUNDARY: the Boundary Creation dialog (CadCommands.BoundaryDialog.cs), then "Pick internal point:" until Enter,
        /// each point's area and its islands made closed polylines on the current layer, "BOUNDARY created 2 polylines"; an area
        /// with an ellipse or a spline in it is refused, as AutoCAD refuses it (see <see cref="BoundaryPoints"/>). One undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> Boundary(CadCommandContext c) => BoundaryDialog(c);
    }
}

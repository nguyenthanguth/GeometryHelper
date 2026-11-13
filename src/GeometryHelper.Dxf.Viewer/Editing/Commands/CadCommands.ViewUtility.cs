using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Batch 4 of acad.pgp, the view and utility commands with a window of their own: VPORTS and -VPORTS (the tiled
    /// viewports of model space, <see cref="CadTiledViewports"/>), FILTER (<see cref="CadSelectionFilter"/>), QUICKCALC and
    /// CAL (<see cref="CadCalculator"/>) and QUICKPROPERTIES, in the words of AutoCAD 2021 (its prompts read from
    /// accoreconsole probes).
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] VportsOptions =
        {
            "Save",
            "Restore",
            "Delete",
            "Join",
            "SIngle",
            "?",
            "2",
            "3",
            "4",
            "Toggle",
            "MOde",
        };

        internal static void AddViewUtilityTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("VPORTS", Vports));
            registry.Add(new CadCommand("-VPORTS", VportsTyped));
            registry.Add(new CadCommand("FILTER", Filter));
            registry.Add(new CadCommand("QUICKCALC", QuickCalc));
            registry.Add(new CadCommand("CAL", Calculate));
            registry.Add(new CadCommand("QUICKPROPERTIES", QuickProperties));
        }

        /// <summary>
        /// VPORTS: the Viewports dialog over the tiled viewports of model space, its changes one undo step; in a layout the
        /// prompts of MVIEW, as AutoCAD's. With no window, the command says so.
        /// </summary>
        private static IEnumerable<CadPrompt> Vports(CadCommandContext c)
        {
            if (!c.Space.IsModel)
            {
                foreach (CadPrompt prompt in MView(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            CadTiledViewports tiles = c.Document.TiledViewports;
            string before = tiles.State();
            c.OpenUndoStep();

            if (!c.Session.Request("VPORTS", c.Document, tiles))
            {
                c.Write("Viewports is shown by the window.");
                yield break;
            }

            if (tiles.State() != before)
            {
                c.Write("Regenerating model.");
            }
        }

        /// <summary>
        /// -VPORTS: "Enter an option [Save/Restore/Delete/Join/SIngle/?/2/3/4/Toggle/MOde] &lt;3&gt;:", as AutoCAD 2021 asks in
        /// model space; in a layout the prompts of MVIEW. 2, 3 and 4 split the current viewport, each change one undo step.
        /// MOde asks "Apply viewport configuration to [Display/Current viewport] &lt;Display&gt;:", which the Viewports dialog
        /// starts with, and asks the options again; 2, 3 and 4 split the current viewport in either mode, as AutoCAD 2021's do.
        /// </summary>
        private static IEnumerable<CadPrompt> VportsTyped(CadCommandContext c)
        {
            if (!c.Space.IsModel)
            {
                foreach (CadPrompt prompt in MView(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            CadTiledViewports tiles = c.Document.TiledViewports;
            c.OpenUndoStep();
            string option;

            while (true)
            {
                yield return CadPrompt.ForKeyword("Enter an option", VportsOptions, "3");
                option =
                    c.Input.IsNone ? "3"
                    : c.Input.Kind == CadInputKind.Keyword ? c.Input.Text
                    : null;

                if (option != "MOde")
                {
                    break;
                }

                // MOde asks where the configurations of the dialog apply, then the options again, as AutoCAD does.
                yield return CadPrompt.ForKeyword(
                    "Apply viewport configuration to",
                    new[] { "Display", "Current viewport" },
                    "Display"
                );
                tiles.AppliesToCurrentViewport = c.Input.Is("Current viewport");
            }

            switch (option)
            {
                case "Save":
                    foreach (CadPrompt prompt in VportsSave(c, tiles))
                    {
                        yield return prompt;
                    }

                    break;
                case "Restore":
                    while (true)
                    {
                        yield return CadPrompt.ForKeywordOrText(
                            "Enter name of viewport configuration to restore or",
                            new[] { "?" },
                            null
                        );

                        if (c.Input.Is("?"))
                        {
                            foreach (CadPrompt prompt in VportsList(c, tiles))
                            {
                                yield return prompt;
                            }

                            continue;
                        }

                        string name = Typed(c);

                        if (name == null)
                        {
                            break;
                        }

                        if (tiles.Restore(name))
                        {
                            c.Write("Regenerating model.");
                            break;
                        }

                        c.Write("Cannot find viewport configuration \"" + name + "\".");
                    }

                    break;
                case "Delete":
                    yield return CadPrompt.ForString("Enter name(s) of viewport configurations to delete", "none");
                    string names = Typed(c);

                    if (names == null)
                    {
                        break;
                    }

                    int deleted = tiles.Delete(names);
                    c.Write(
                        deleted == 0 ? "No matching viewport configurations found."
                        : deleted == 1 ? "Deleted one viewport configuration."
                        : "Deleted " + deleted.ToString(CultureInfo.InvariantCulture) + " viewport configurations."
                    );
                    break;
                case "Join":
                    foreach (CadPrompt prompt in VportsJoin(c, tiles))
                    {
                        yield return prompt;
                    }

                    break;
                case "SIngle":
                    if (tiles.Single())
                    {
                        c.Write("Regenerating model.");
                    }

                    break;
                case "?":
                    foreach (CadPrompt prompt in VportsList(c, tiles))
                    {
                        yield return prompt;
                    }

                    break;
                case "2":
                case "3":
                case "4":
                    string arrangement = null;

                    if (option == "2")
                    {
                        yield return CadPrompt.ForKeyword(
                            "Enter a configuration option",
                            new[] { "Horizontal", "Vertical" },
                            "Vertical"
                        );
                        arrangement = c.Input.IsNone ? "Vertical" : c.Input.Text;
                    }
                    else if (option == "3")
                    {
                        yield return CadPrompt.ForKeyword(
                            "Enter a configuration option",
                            new[] { "Horizontal", "Vertical", "Above", "Below", "Left", "Right" },
                            "Right"
                        );
                        arrangement = c.Input.IsNone ? "Right" : c.Input.Text;
                    }

                    if (c.Input.Kind == CadInputKind.Keyword || c.Input.IsNone || option == "4")
                    {
                        string refused = tiles.Split(
                            int.Parse(option, CultureInfo.InvariantCulture),
                            arrangement,
                            false
                        );
                        c.Write(refused ?? "Regenerating model.");
                    }

                    break;
                case "Toggle":
                    if (tiles.Toggle())
                    {
                        c.Write("Regenerating model.");
                    }

                    break;
            }
        }

        /// <summary>Save of -VPORTS: "Enter name for new viewport configuration or [?]:", a name in use asked about first.</summary>
        private static IEnumerable<CadPrompt> VportsSave(CadCommandContext c, CadTiledViewports tiles)
        {
            while (true)
            {
                yield return CadPrompt.ForKeywordOrText(
                    "Enter name for new viewport configuration or",
                    new[] { "?" },
                    null
                );

                if (c.Input.Is("?"))
                {
                    foreach (CadPrompt prompt in VportsList(c, tiles))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                string name = Typed(c);

                if (name == null)
                {
                    yield break;
                }

                if (tiles.Saved(name) != null)
                {
                    yield return CadPrompt
                        .ForKeyword(
                            "Viewport configuration \"" + name + "\" already exists.  Replace it?",
                            Array.Empty<string>(),
                            "N"
                        )
                        .Hiding("Yes", "No")
                        .WithoutColon();

                    if (!c.Input.Is("Yes"))
                    {
                        continue;
                    }
                }

                tiles.Save(name);
                yield break;
            }
        }

        /// <summary>? of -VPORTS: "Enter name(s) of viewport configuration(s) to list &lt;*&gt;:", then the current configuration and those named.</summary>
        private static IEnumerable<CadPrompt> VportsList(CadCommandContext c, CadTiledViewports tiles)
        {
            yield return CadPrompt.ForString("Enter name(s) of viewport configuration(s) to list", "*");
            string pattern = Typed(c) ?? "*";

            foreach (string line in tiles.List(pattern))
            {
                c.Write(line);
            }
        }

        /// <summary>
        /// Join of -VPORTS: "Select dominant viewport &lt;current viewport&gt;:" and "Select viewport to join:". A viewport is
        /// picked by a click in it, which makes it the current one first, as in AutoCAD; so the viewport a point answers with
        /// is the current one when the point comes.
        /// </summary>
        private static IEnumerable<CadPrompt> VportsJoin(CadCommandContext c, CadTiledViewports tiles)
        {
            if (!tiles.IsTiled)
            {
                c.Write("Only one viewport is active.");
                yield break;
            }

            yield return CadPrompt.ForPoint("Select dominant viewport", null, null, "current viewport");

            if (!c.Input.IsPoint && !c.Input.IsNone)
            {
                yield break;
            }

            int dominant = tiles.Current.Id;
            yield return CadPrompt.ForPoint("Select viewport to join");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            string refused = tiles.Join(dominant, tiles.Current.Id);
            c.Write(refused ?? "Regenerating model.");
        }

        /// <summary>The text typed at a prompt that takes one, trimmed; null for Enter or nothing.</summary>
        private static string Typed(CadCommandContext c)
        {
            string text =
                c.Input.Kind == CadInputKind.Text || c.Input.Kind == CadInputKind.Keyword ? c.Input.Text : null;
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        /// <summary>
        /// FILTER: the Object Selection Filters dialog. Apply asks "Select objects:" after "Applying filter to selection."; what
        /// passes the filter's lines is kept as the Previous set, as AutoCAD's FILTER leaves it for the next command, then
        /// "Exiting filtered selection.". Add Selected Object &lt; hides the dialog while one object is picked, and adds its
        /// lines. With no window, the command says so.
        /// </summary>
        private static IEnumerable<CadPrompt> Filter(CadCommandContext c)
        {
            CadSelectionFilter filter = c.Session.SelectionFilter;
            string unread = filter.Reload();

            if (unread != null)
            {
                c.Write(unread);
            }

            while (true)
            {
                CadUiRequestEventArgs asked = c.Session.Ask("FILTER", c.Document, filter);

                if (!asked.Handled)
                {
                    c.Write("Object Selection Filters is shown by the window.");
                    yield break;
                }

                if (asked.Answer == "Add Selected Object")
                {
                    yield return CadPrompt.ForEntity("Select object");

                    DxfEntity picked =
                        c.Input.Kind == CadInputKind.Selection && c.Input.Handles.Count > 0
                            ? c.Document.FindEntity(c.Input.Handles[0])
                            : null;

                    if (picked != null)
                    {
                        foreach (CadFilterLine line in CadSelectionFilter.LinesOf(picked))
                        {
                            filter.Lines.Add(line);
                        }
                    }

                    continue;
                }

                if (asked.Answer != "Apply")
                {
                    yield break;
                }

                string wrong = filter.Validate();

                if (wrong != null)
                {
                    c.Write(wrong);
                    yield break;
                }

                c.Write("Applying filter to selection.");
                yield return CadPrompt.ForSelection();

                if (c.Input.Kind == CadInputKind.Selection)
                {
                    IReadOnlyList<string> passed = filter.Apply(c.Document, c.Input.Handles);
                    int filteredOut = c.Input.Handles.Count - passed.Count;

                    if (filteredOut > 0)
                    {
                        c.Write(
                            filteredOut == 1
                                ? "1 was filtered out"
                                : filteredOut.ToString(CultureInfo.InvariantCulture) + " were filtered out"
                        );
                    }

                    c.Session.RememberFiltered(passed);
                }

                c.Write("Exiting filtered selection.");
                yield break;
            }
        }

        /// <summary>QUICKCALC: the QuickCalc palette, which stays open while commands run. With no window, the command says so.</summary>
        private static IEnumerable<CadPrompt> QuickCalc(CadCommandContext c)
        {
            if (!c.Session.Request("QUICKCALC", c.Document))
            {
                c.Write("QuickCalc is shown by the window.");
            }

            yield break;
        }

        /// <summary>
        /// CAL: "&gt;&gt; Expression:", evaluated by the geometry calculator, which asks for what the expression picks in the
        /// drawing, in the order it meets it: <c>cur</c> "&gt;&gt; Enter a point:", a snap function such as <c>end</c> "&gt;&gt;
        /// Select entity for END snap:" (an object that has no such point is "Improper type of object picked.", a pick on
        /// nothing "No object picked.", and the prompt comes again), <c>rad</c> "&gt;&gt; Select circle, arc or polyline segment
        /// for RAD function:". The value is written as AutoCAD 2021's CAL writes it (<c>120,80,0</c>), and given to the window
        /// (QuickCalc takes it where it asked); a wrong expression writes "Error:" and why, and asks again. Typed as 'CAL at a
        /// prompt, the prompt is "&gt;&gt;&gt;&gt; Expression:", nothing is written for the value, and the value is typed at
        /// the prompt the command was waiting at once it resumes.
        /// </summary>
        private static IEnumerable<CadPrompt> Calculate(CadCommandContext c)
        {
            while (true)
            {
                yield return CadPrompt.ForString(">> Expression");

                string expression = Typed(c);

                if (expression == null)
                {
                    yield break;
                }

                var answers = new List<CadCalcValue>();

                foreach (CadCalcInput input in CadCalculator.Inputs(expression))
                {
                    bool answered = false;

                    while (!answered)
                    {
                        if (input.Kind == CadCalcInputKind.Point)
                        {
                            yield return CadPrompt.ForPoint(input.Prompt);

                            if (!c.Input.IsPoint)
                            {
                                yield break;
                            }

                            GeoPoint3 point = P3(c.Input.Point);
                            answers.Add(CadCalcValue.Vector(point.X, point.Y, point.Z));
                            answered = true;
                            continue;
                        }

                        yield return CadPrompt.ForEntity(input.Prompt).MissingWith("No object picked.");

                        if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
                        {
                            yield break;
                        }

                        if (input.Kind == CadCalcInputKind.Radius)
                        {
                            double? radius = CadCalcSnap.Radius(c.Session, c.Input.Handles[0], c.Input.Point);

                            if (radius == null)
                            {
                                c.Write("Improper type of object picked.");
                                continue;
                            }

                            answers.Add(CadCalcValue.Real(radius.Value));
                            answered = true;
                            continue;
                        }

                        GeoPoint2? snapped = CadCalcSnap.Snap(c.Session, input.Mode, c.Input.Handles[0], c.Input.Point);

                        if (snapped == null)
                        {
                            c.Write("Improper type of object picked.");
                            continue;
                        }

                        c.Session.LastPoint = snapped.Value;
                        answers.Add(CadCalcValue.Vector(snapped.Value.X, snapped.Value.Y, 0.0));
                        answered = true;
                    }
                }

                CadCalcValue value;

                try
                {
                    value = CadCalculator.Evaluate(expression, answers);
                }
                catch (CadCalcException e)
                {
                    c.Write("Error:");
                    c.Write(e.Message);
                    continue;
                }

                if (c.Session.IsTransparent)
                {
                    c.TransparentResult = value.ToTyped();
                }
                else
                {
                    c.Write(value.ToString());
                    c.Session.Request("CAL", c.Document, value);
                }

                yield break;
            }
        }

        /// <summary>
        /// QUICKPROPERTIES: the Quick Properties panel over the objects selected, "Select objects:" asked first where none are.
        /// With no window, the command says so.
        /// </summary>
        private static IEnumerable<CadPrompt> QuickProperties(CadCommandContext c)
        {
            var handles = c.Document.Selection.Handles.Where(h => c.Document.FindEntity(h) != null).ToList();

            if (handles.Count == 0)
            {
                yield return CadPrompt.ForSelection();

                if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
                {
                    yield break;
                }

                handles = c.Input.Handles.ToList();
                c.Document.Selection.Set(handles);
            }

            handles.Sort(StringComparer.OrdinalIgnoreCase);

            if (!c.Session.Request("QUICKPROPERTIES", c.Document, handles))
            {
                c.Write("Quick Properties is shown by the window.");
            }
        }
    }
}

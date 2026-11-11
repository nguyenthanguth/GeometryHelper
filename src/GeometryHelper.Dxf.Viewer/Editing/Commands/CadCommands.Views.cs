using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Named views, as AutoCAD 2021 keeps and asks them: VIEW, the View Manager, where the window has one, and -VIEW, its ?,
    /// Delete, Orthographic, Restore, Save, sEttings and Window in AutoCAD's words. A view is a record of the VIEW table
    /// (<see cref="DxfViewTableRecord"/>), its layer snapshot the hidden layer state ACAD_VIEWS_ and its name; each run that
    /// changes the drawing is one undo step, restoring a view on screen is not, as ZOOM is not.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] ViewOptions =
        {
            "?",
            "Delete",
            "Orthographic",
            "Restore",
            "Save",
            "sEttings",
            "Window",
        };

        /// <summary>VIEW: the View Manager, shown again after each pick, until OK or Cancel; with no window, -VIEW's prompts.</summary>
        private static IEnumerable<CadPrompt> ViewManager(CadCommandContext c)
        {
            var request = new CadViewManagerRequest(c.Document);
            CadGroupMark mark = null;

            while (true)
            {
                ReadViews(c, request);
                request.Action = CadDialogAction.Cancel;
                request.Pick = null;
                request.Changing = (what, texts) =>
                {
                    if (mark == null && what != "Apply")
                    {
                        c.OpenUndoStep();
                        mark = c.Document.MarkGroup();
                    }

                    string said = ChangeViewInDialog(c, request, what, texts);
                    ReadViews(c, request);
                    return said;
                };

                bool asked = c.Session.Request("VIEW", c.Document, request);
                request.Changing = null;

                if (!asked)
                {
                    foreach (CadPrompt prompt in ViewTyped(c))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                if (request.Action == CadDialogAction.Ok)
                {
                    if (request.ToRestore != null)
                    {
                        RestoreView(c, request.ToRestore);
                    }

                    yield break;
                }

                if (request.Action != CadDialogAction.SelectObjects)
                {
                    if (mark != null)
                    {
                        c.Document.BackTo(mark);
                    }

                    yield break;
                }

                // Edit Boundaries… and Define window: two corners on the command line, the dialog hidden.
                yield return CadPrompt.ForPoint("Specify first corner");

                if (!c.Input.IsPoint)
                {
                    continue;
                }

                GeoPoint2 first = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify opposite corner", first);

                if (!c.Input.IsPoint)
                {
                    continue;
                }

                DisplayBox box = Box(first, c.Input.Point);

                if (box.Width <= 0.0 || box.Height <= 0.0)
                {
                    c.Write("The window has no size.");
                    continue;
                }

                if (request.Pick == "NewWindow")
                {
                    request.NewWindow = box;
                }
                else if (FindView(c, request.Selected) != null)
                {
                    if (mark == null)
                    {
                        c.OpenUndoStep();
                        mark = c.Document.MarkGroup();
                    }

                    string name = request.Selected;
                    c.Apply(
                        new[]
                        {
                            new CadEditRecord(
                                CadEditRecordKind.TableChanged,
                                null,
                                "View boundaries",
                                name,
                                Values(box.MinX, box.MinY, box.MaxX, box.MaxY)
                            ),
                        },
                        s => Bound(FindView(c, name), box)
                    );
                }
            }
        }

        /// <summary>The views read into the request, model views then layout views; the view on screen as Current.</summary>
        private static void ReadViews(CadCommandContext c, CadViewManagerRequest request)
        {
            var rows = new List<CadViewRow>();

            foreach (DxfViewTableRecord view in c.Database.ViewTable)
            {
                rows.Add(
                    new CadViewRow(
                        view.Name,
                        view.IsPaperspaceView,
                        view.LayerState.Length > 0,
                        view.CenterPoint.X,
                        view.CenterPoint.Y,
                        view.Height,
                        view.Width,
                        view.ViewTwist * 180.0 / Math.PI
                    )
                );
            }

            rows.Sort(
                (a, b) =>
                {
                    int order = a.IsPaperspace.CompareTo(b.IsPaperspace);
                    order = order != 0 ? order : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
                    return order != 0 ? order : string.CompareOrdinal(a.Name, b.Name);
                }
            );

            request.Views = rows;
            CadView shown = c.Session.View;
            request.CurrentView =
                shown == null
                    ? null
                    : new CadViewRow(
                        "Current",
                        !c.Document.CurrentSpace.IsModel,
                        false,
                        shown.CenterX,
                        shown.CenterY,
                        shown.VisibleBox.Height,
                        shown.VisibleBox.Width,
                        0.0
                    );

            if (request.Selected != null && FindView(c, request.Selected) == null)
            {
                request.Selected = null;
            }

            if (request.ToRestore != null && FindView(c, request.ToRestore) == null)
            {
                request.ToRestore = null;
            }
        }

        /// <summary>A change of the dialog's made at once, an edit of the command's step; what it says where it is refused, else null.</summary>
        private static string ChangeViewInDialog(
            CadCommandContext c,
            CadViewManagerRequest request,
            string what,
            string[] texts
        )
        {
            if (what == "New")
            {
                string name = (request.NewName ?? string.Empty).Trim();

                if (!IsViewName(name))
                {
                    return "Invalid view name.";
                }

                if (FindView(c, name) != null && texts[0] != "1")
                {
                    return "A view named " + name + " already exists.";
                }

                DisplayBox? box = request.NewWindow ?? Shown(c);

                if (box == null)
                {
                    return "There is no view to save.";
                }

                SaveView(c, name, box.Value, request.NewLayerSnapshot);
                request.Selected = FindView(c, name).Name;
                request.NewWindow = null;
                request.IsNewViewOpen = false;
                return null;
            }

            if (what == "Apply")
            {
                if (request.ToRestore == null)
                {
                    return "Choose a view and Set Current first.";
                }

                RestoreView(c, request.ToRestore);
                return null;
            }

            DxfViewTableRecord view = FindView(c, request.Selected);

            if (view == null)
            {
                return "Choose a named view in the list first.";
            }

            string chosen = view.Name;

            switch (what)
            {
                case "Delete":
                    DeleteViews(c, new[] { chosen });
                    request.Selected = null;
                    return null;
                case "Layers":
                    SnapshotView(c, chosen, true);
                    return null;
                case "Rename":
                    string typed = (texts[0] ?? string.Empty).Trim();

                    if (!IsViewName(typed))
                    {
                        return "Invalid view name.";
                    }

                    DxfViewTableRecord other = FindView(c, typed);

                    if (other != null && !string.Equals(other.Name, chosen, StringComparison.Ordinal))
                    {
                        return "A view named " + typed + " already exists.";
                    }

                    c.Apply(
                        new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Rename view", chosen, typed) },
                        s => RenameView(c, chosen, typed)
                    );
                    request.Selected = typed;
                    return null;
                default:
                    if (
                        !double.TryParse(texts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double height)
                        || !(height > 0.0)
                        || double.IsInfinity(height)
                        || !double.TryParse(
                            texts[1],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out double width
                        )
                        || !(width > 0.0)
                        || double.IsInfinity(width)
                    )
                    {
                        return "A view's height and width are numbers above nought.";
                    }

                    c.Apply(
                        new[]
                        {
                            new CadEditRecord(
                                CadEditRecordKind.TableChanged,
                                null,
                                "View size",
                                chosen,
                                Values(height, width)
                            ),
                        },
                        s =>
                        {
                            DxfViewTableRecord record = FindView(c, chosen);
                            record.Height = height;
                            record.Width = width;
                        }
                    );
                    return null;
            }
        }

        /// <summary>-VIEW: AutoCAD 2021's options, one a run, as its -VIEW asks them.</summary>
        private static IEnumerable<CadPrompt> ViewTyped(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword("Enter an option", ViewOptions);

            if (c.Input.Is("?"))
            {
                yield return CadPrompt.ForString("Enter view name(s) to list", "*");
                ListViews(c, c.Input.Kind == CadInputKind.Text ? c.Input.Text : "*");
                yield break;
            }

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            switch (c.Input.Text)
            {
                case "Delete":
                    yield return CadPrompt.ForString("Enter view name(s) to delete");

                    if (c.Input.Kind == CadInputKind.Text)
                    {
                        List<string> names = ViewsMatching(c, c.Input.Text);

                        if (names.Count == 0)
                        {
                            c.Write("No matching view names found.");
                        }
                        else
                        {
                            c.OpenUndoStep();
                            DeleteViews(c, names);
                        }
                    }

                    break;
                case "Orthographic":
                    c.Write("Orthographic views look at a model in 3D: a drawing here is 2D and has none.");
                    break;
                case "Restore":
                    yield return CadPrompt.ForString("Enter view name to restore");

                    if (c.Input.Kind == CadInputKind.Text)
                    {
                        DxfViewTableRecord view = FindView(c, c.Input.Text.Trim());

                        if (view == null)
                        {
                            c.Write("Cannot find view \"" + c.Input.Text.Trim() + "\".");
                        }
                        else
                        {
                            RestoreView(c, view.Name);
                        }
                    }

                    break;
                case "Save":
                case "Window":
                    bool window = c.Input.Is("Window");
                    yield return CadPrompt.ForString("Enter view name to save");
                    string name = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

                    if (!IsViewName(name))
                    {
                        if (name.Length > 0)
                        {
                            c.Write("Invalid view name.");
                        }

                        break;
                    }

                    DisplayBox? box = Shown(c);

                    if (window)
                    {
                        yield return CadPrompt.ForPoint("Specify first corner");

                        if (!c.Input.IsPoint)
                        {
                            break;
                        }

                        GeoPoint2 first = c.Input.Point;
                        yield return CadPrompt.ForPoint("Specify opposite corner", first);

                        if (!c.Input.IsPoint)
                        {
                            break;
                        }

                        box = Box(first, c.Input.Point);
                    }

                    if (box == null || box.Value.Width <= 0.0 || box.Value.Height <= 0.0)
                    {
                        c.Write("There is no view to save.");
                        break;
                    }

                    if (FindView(c, name) != null)
                    {
                        yield return CadPrompt.ForKeyword(
                            "View \"" + FindView(c, name).Name + "\" already exists. Do you want to redefine it?",
                            new[] { "Yes", "No" },
                            "No"
                        );

                        if (!c.Input.Is("Yes"))
                        {
                            break;
                        }
                    }

                    c.OpenUndoStep();
                    SaveView(c, name, box.Value, false);
                    break;
                default:
                    yield return CadPrompt.ForKeyword(
                        "Enter an option",
                        new[] { "Background", "Categorize", "Layer snapshot", "live Section", "Ucs", "Visual style" }
                    );

                    if (!c.Input.Is("Layer snapshot"))
                    {
                        if (c.Input.Kind == CadInputKind.Keyword)
                        {
                            c.Write("A drawing here is 2D: of a view's settings only its layer snapshot is kept.");
                        }

                        break;
                    }

                    while (true)
                    {
                        yield return CadPrompt.ForKeywordOrText(
                            "Enter view name to edit Layer snapshot or",
                            new[] { "?" },
                            null
                        );

                        if (c.Input.Is("?"))
                        {
                            yield return CadPrompt.ForString("Enter view name(s) to list", "*");
                            ListViews(c, c.Input.Kind == CadInputKind.Text ? c.Input.Text : "*");
                            continue;
                        }

                        if (c.Input.Kind != CadInputKind.Text)
                        {
                            break;
                        }

                        DxfViewTableRecord view = FindView(c, c.Input.Text.Trim());

                        if (view == null)
                        {
                            c.Write("No matching view names found.");
                            continue;
                        }

                        string chosen = view.Name;
                        yield return CadPrompt.ForKeyword("Enter an option", new[] { "Save", "Delete" }, "Cancel");

                        if (c.Input.Is("Save") || c.Input.Is("Delete"))
                        {
                            c.OpenUndoStep();
                            SnapshotView(c, chosen, c.Input.Is("Save"));
                        }

                        break;
                    }

                    break;
            }
        }

        /// <summary>-VIEW ?: the views whose names match, each with M for model space or P for a layout, as AutoCAD 2021 lists them.</summary>
        private static void ListViews(CadCommandContext c, string pattern)
        {
            List<string> names = ViewsMatching(c, pattern);
            c.Write("Saved views:");

            if (names.Count == 0)
            {
                c.Write("  No matching views found.");
                return;
            }

            c.Write("View name                        Space");

            foreach (string name in names)
            {
                c.Write(("\"" + name + "\"").PadRight(35) + (FindView(c, name).IsPaperspaceView ? "P" : "M"));
            }
        }

        /// <summary>The names of the views that match a list of patterns, each with its wildcards, in ordinal order, letters of either case alike.</summary>
        private static List<string> ViewsMatching(CadCommandContext c, string patterns)
        {
            string[] wanted = (string.IsNullOrWhiteSpace(patterns) ? "*" : patterns)
                .Split(',')
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToArray();
            var names = new List<string>();

            foreach (DxfViewTableRecord view in c.Database.ViewTable)
            {
                if (wanted.Any(p => CadWildcard.IsMatch(view.Name, p, true)))
                {
                    names.Add(view.Name);
                }
            }

            names.Sort(
                (a, b) =>
                {
                    int order = StringComparer.OrdinalIgnoreCase.Compare(a, b);
                    return order != 0 ? order : string.CompareOrdinal(a, b);
                }
            );

            return names;
        }

        /// <summary>A view's name AutoCAD takes: not empty, 255 letters at most, no space at either end and none of &lt;&gt;/\":;?*|=`.</summary>
        private static bool IsViewName(string name) =>
            !string.IsNullOrEmpty(name)
            && name.Length <= 255
            && name.Trim().Length == name.Length
            && name.IndexOfAny("<>/\\\":;?*|=`".ToCharArray()) < 0
            && !name.Any(ch => ch < ' ');

        private static DxfViewTableRecord FindView(CadCommandContext c, string name) =>
            string.IsNullOrEmpty(name) || !c.Database.ViewTable.Has(name) ? null : c.Database.ViewTable[name];

        /// <summary>What the canvas shows of the space on screen; null where it shows none.</summary>
        private static DisplayBox? Shown(CadCommandContext c) =>
            c.Session.View == null ? (DisplayBox?)null : c.Session.View.VisibleBox;

        private static DisplayBox Box(GeoPoint2 a, GeoPoint2 b) =>
            new DisplayBox(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));

        /// <summary>A view of a box saved in the space on screen, an edit of the command's step: made, or redefined where the name has one; its layer snapshot saved where asked.</summary>
        private static void SaveView(CadCommandContext c, string name, DisplayBox box, bool snapshot)
        {
            bool paper = !c.Document.CurrentSpace.IsModel;
            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "View", null, name) }, s =>
            {
                DxfViewTableRecord view = FindView(c, name);

                if (view == null)
                {
                    view = new DxfViewTableRecord
                    {
                        Name = name,
                        IsPaperspaceView = paper,
                        CenterPoint = new GeoPoint2((box.MinX + box.MaxX) / 2.0, (box.MinY + box.MaxY) / 2.0),
                        Height = box.Height,
                        Width = box.Width,
                    };
                    s.Database.ViewTable.Add(view);
                }
                else
                {
                    view.IsPaperspaceView = paper;
                    Bound(view, box);
                }
            });

            if (snapshot)
            {
                SnapshotView(c, FindView(c, name).Name, true);
            }
        }

        /// <summary>A view's centre, height and width set from a box.</summary>
        private static void Bound(DxfViewTableRecord view, DisplayBox box)
        {
            view.CenterPoint = new GeoPoint2((box.MinX + box.MaxX) / 2.0, (box.MinY + box.MaxY) / 2.0);
            view.Height = box.Height;
            view.Width = box.Width;
        }

        /// <summary>
        /// A view's layer snapshot saved from the layers as they stand, as the hidden state ACAD_VIEWS_ and its name with
        /// AutoCAD 2021's mask 0x8200, or deleted with the state; an edit of the command's step.
        /// </summary>
        private static void SnapshotView(CadCommandContext c, string name, bool save)
        {
            string state = CadLayerStates.ViewPrefix + name;
            c.Apply(
                new[]
                {
                    new CadEditRecord(
                        CadEditRecordKind.TableChanged,
                        null,
                        "View layer snapshot",
                        name,
                        save ? state : null
                    ),
                },
                s =>
                {
                    DxfViewTableRecord view = FindView(c, name);
                    string had = view.LayerState;

                    if (had.Length > 0 && !string.Equals(had, state, StringComparison.OrdinalIgnoreCase))
                    {
                        CadLayerStates.Delete(s.Database, had);
                    }

                    if (save)
                    {
                        CadLayerStates.Save(
                            s.Database,
                            state,
                            string.Empty,
                            CadLayerStates.Hidden | CadLayerStates.CurrentViewport
                        );
                        view.LayerState = state;
                    }
                    else
                    {
                        CadLayerStates.Delete(s.Database, state);
                        view.LayerState = string.Empty;
                    }
                }
            );
        }

        /// <summary>A view renamed, its layer snapshot's state given the name that goes with the new one.</summary>
        private static void RenameView(CadCommandContext c, string name, string newName)
        {
            DxfViewTableRecord view = FindView(c, name);
            string state = view.LayerState;
            view.Name = newName;

            if (state.Length > 0 && CadLayerStates.Find(c.Database, state) != null)
            {
                string renamed = CadLayerStates.ViewPrefix + newName;
                CadLayerStates.Rename(c.Database, state, renamed);
                view.LayerState = renamed;
            }
        }

        /// <summary>Views erased with their layer snapshots, an edit of the command's step.</summary>
        private static void DeleteViews(CadCommandContext c, IReadOnlyList<string> names)
        {
            c.Apply(
                names
                    .Select(n => new CadEditRecord(CadEditRecordKind.TableChanged, null, "Delete view", n, null))
                    .ToArray(),
                s =>
                {
                    foreach (string name in names)
                    {
                        DxfViewTableRecord view = FindView(c, name);
                        string state = view.LayerState;
                        view.Erase();

                        if (state.Length > 0)
                        {
                            CadLayerStates.Delete(s.Database, state);
                        }
                    }
                }
            );
        }

        /// <summary>
        /// A view shown: its space made current where the canvas shows another (a layout view in the layout on screen, or the
        /// first layout), the canvas zoomed to it, and its layer snapshot restored, the one edit of the step.
        /// </summary>
        private static void RestoreView(CadCommandContext c, string name)
        {
            DxfViewTableRecord view = FindView(c, name);

            if (view == null)
            {
                return;
            }

            GeoPoint2 center = view.CenterPoint;
            var box = new DisplayBox(
                center.X - view.Width / 2.0,
                center.Y - view.Height / 2.0,
                center.X + view.Width / 2.0,
                center.Y + view.Height / 2.0
            );
            CadSpace space = c.Document.CurrentSpace;
            CadSpace wanted = view.IsPaperspaceView
                ? (space.IsModel ? c.Document.Spaces.FirstOrDefault(s => !s.IsModel) : space)
                : c.Document.ModelSpace;

            if (wanted == null)
            {
                c.Write("The drawing has no layout to show the view \"" + view.Name + "\" in.");
                return;
            }

            if (wanted == space && c.Session.View != null)
            {
                c.Session.View.ZoomTo(box, 0.0);
            }
            else
            {
                wanted.PendingView = box;
                c.Document.CurrentSpace = wanted;
                c.Document.Selection.Clear();
            }

            string state = view.LayerState;

            if (state.Length > 0 && CadLayerStates.Find(c.Database, state) != null)
            {
                RestoreLayerState(c, state, false);
            }
        }
    }
}

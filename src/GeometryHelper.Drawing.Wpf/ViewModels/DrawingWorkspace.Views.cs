using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// The views of the drawing shown: dimensions, marks and centre lines of one view made again, a view along the axes
    /// added, and a section or a detail drawn by two points picked on the canvas (VIEWSECTION, VIEWDETAIL).
    /// </summary>
    public sealed partial class DrawingWorkspace
    {
        /// <summary>
        /// Gets or sets how work is put off until the command running has ended, as the window's dispatcher does; null to run
        /// it at once. A command of the command line cannot update the drawing while it runs, the document being in its step.
        /// </summary>
        public Action<Action> Deferrer { get; set; }

        /// <summary>
        /// Gets the id of the view the selection is in: a view's frame or title selected, or a dimension, a mark or a line of a
        /// view; null where nothing of a view is selected.
        /// </summary>
        /// <returns>The view's id, as V-FRONT, or null.</returns>
        public string SelectedViewId()
        {
            if (_current == null)
            {
                return null;
            }

            var views = new HashSet<string>(_current.Drawing.Elements.Where(e => e.Kind == "View").Select(e => e.Id), StringComparer.Ordinal);

            foreach (string handle in _current.Document.Selection.Handles.OrderBy(h => h, StringComparer.OrdinalIgnoreCase))
            {
                DxfEntity entity = _current.Document.FindEntity(handle);

                if (entity != null && DrawingEntityTags.TryRead(entity, out string id, out string viewId))
                {
                    if (views.Contains(id))
                    {
                        return id;
                    }

                    if (viewId != null && views.Contains(viewId))
                    {
                        return viewId;
                    }
                }
            }

            return null;
        }

        /// <summary>Dimensions the selected view again by the rules of the settings, and keeps it dimensioned (AUTODIM).</summary>
        /// <returns>What changed; null, said on the command line, with no view selected or where it failed.</returns>
        public UpdateReport ReDimensionSelectedView() => OnSelectedView("Dimensions", (s, v) => AddOnce(s.Views.DimensionedViews, v));

        /// <summary>Marks the selected view again, and keeps it marked (AUTOMARK).</summary>
        /// <returns>What changed; null, said on the command line, with no view selected or where it failed.</returns>
        public UpdateReport ReMarkSelectedView() => OnSelectedView("Marks", (s, v) => AddOnce(s.Views.MarkedViews, v));

        /// <summary>Draws the centre lines of the selected view again.</summary>
        /// <returns>What changed; null, said on the command line, with no view selected or where it failed.</returns>
        public UpdateReport ReCentreLinesSelectedView() => OnSelectedView("Centre lines", (s, v) => { });

        /// <summary>
        /// Adds a section cut along two points picked in a view along the axes, in the space shown: the section looks to the
        /// right of the line from the first point to the second, and is named by the next letter free.
        /// </summary>
        /// <param name="first">The first point, in the space shown.</param>
        /// <param name="second">The second point, in the space shown.</param>
        /// <param name="depth">How far behind the cut it sees, in model millimetres.</param>
        /// <returns>What changed; null, said on the command line, where the points lie in no view or the core refused them.</returns>
        public UpdateReport AddSection(GeoPoint2 first, GeoPoint2 second, double depth)
        {
            if (!TryViewAt(first, out DrawingView view, out GeoPoint2 a) || !TryViewAt(second, view, out GeoPoint2 b))
            {
                Say("The cut line has to lie in one view of the drawing.");
                return null;
            }

            string name = NextLetter();
            return Run("Section " + name + "-" + name, (e, provider) => e.Drawing.AddSection(name, view.Id, new GeoLine2(a, b), depth));
        }

        /// <summary>Adds a detail of the rectangle of two corners picked in a view, in the space shown, drawn at a scale of its own.</summary>
        /// <param name="first">A corner, in the space shown.</param>
        /// <param name="second">The opposite corner, in the space shown.</param>
        /// <param name="scale">The scale it is drawn at, 5 for 1:5: no smaller than the view's.</param>
        /// <returns>What changed; null, said on the command line, where the corners lie in no view or the core refused them.</returns>
        public UpdateReport AddDetail(GeoPoint2 first, GeoPoint2 second, double scale)
        {
            if (!TryViewAt(first, out DrawingView view, out GeoPoint2 a) || !TryViewAt(second, view, out GeoPoint2 b))
            {
                Say("The rectangle of the detail has to lie in one view of the drawing.");
                return null;
            }

            string name = NextLetter();
            var area = new GeoRectangle2(new GeoPoint2((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
            return Run("Detail " + name, (e, provider) => e.Drawing.AddDetail(name, view.Id, area, scale));
        }

        /// <summary>
        /// Finds the view a point of the space shown lies in, and the point in the view's millimetres at 1:1: on the sheet a
        /// view stands at its sheet origin at its scale; in model space of a layout drawing at its model origin at 1:1.
        /// </summary>
        /// <param name="point">The point, in the space shown.</param>
        /// <param name="view">The view; null where none is there.</param>
        /// <param name="inView">The point in the view's millimetres.</param>
        /// <returns>Whether a view is there.</returns>
        public bool TryViewAt(GeoPoint2 point, out DrawingView view, out GeoPoint2 inView)
        {
            view = null;
            inView = point;

            if (_current == null)
            {
                return false;
            }

            foreach (DrawingView candidate in _current.Drawing.Views)
            {
                if (TryViewAt(point, candidate, out inView))
                {
                    view = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Puts work off until the command running has ended, through <see cref="Deferrer"/>.</summary>
        internal void Later(Action work)
        {
            if (Deferrer == null)
            {
                work();
            }
            else
            {
                Deferrer(work);
            }
        }

        /// <summary>VIEWSECTION: the two points of the cut line, then the depth; the section added once the command has ended.</summary>
        private IEnumerable<CadPrompt> SectionPrompts(CadCommandContext c)
        {
            if (Find(c.Document) == null)
            {
                c.Write("VIEWSECTION works on a model drawing; " + c.Document.Title + " is a DXF.");
                yield break;
            }

            if (MakeFirst(c))
            {
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify first point of the cut line in a view");

            if (c.Input.Kind != CadInputKind.Point)
            {
                yield break;
            }

            GeoPoint2 first = c.Input.Point;

            if (!TryViewAt(first, out DrawingView view, out _))
            {
                c.Write("No view of the drawing is there.");
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify second point; the section looks to its right", first);

            if (c.Input.Kind != CadInputKind.Point)
            {
                yield break;
            }

            GeoPoint2 second = c.Input.Point;
            double depth = _current.Drawing.Settings.Views.SectionDepth;
            yield return CadPrompt.ForDistance("Specify depth behind the cut", null, null, Number(double.IsInfinity(depth) ? 100.0 : depth));

            if (c.Input.Kind == CadInputKind.Number)
            {
                depth = c.Input.Number;
            }
            else if (double.IsInfinity(depth))
            {
                depth = 100.0;
            }

            c.Write("Section of " + view.Name + " " + Number(depth) + " deep: the drawing is made again.");
            Later(() => AddSection(first, second, depth));
        }

        /// <summary>VIEWDETAIL: two corners of the rectangle, then the scale; the detail added once the command has ended.</summary>
        private IEnumerable<CadPrompt> DetailPrompts(CadCommandContext c)
        {
            if (Find(c.Document) == null)
            {
                c.Write("VIEWDETAIL works on a model drawing; " + c.Document.Title + " is a DXF.");
                yield break;
            }

            if (MakeFirst(c))
            {
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify first corner of the detail in a view");

            if (c.Input.Kind != CadInputKind.Point)
            {
                yield break;
            }

            GeoPoint2 first = c.Input.Point;

            if (!TryViewAt(first, out DrawingView view, out _))
            {
                c.Write("No view of the drawing is there.");
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify opposite corner", first);

            if (c.Input.Kind != CadInputKind.Point)
            {
                yield break;
            }

            GeoPoint2 second = c.Input.Point;
            double scale = Math.Max(1.0, Math.Floor(view.Scale / 2.0));
            yield return CadPrompt.ForDistance("Specify scale of the detail, 1:", null, null, Number(scale));

            if (c.Input.Kind == CadInputKind.Number)
            {
                scale = c.Input.Number;
            }

            c.Write("Detail of " + view.Name + " at 1:" + Number(scale) + ": the drawing is made again.");
            Later(() => AddDetail(first, second, scale));
        }

        /// <summary>
        /// VIEWSECTION and VIEWDETAIL on a drawing opened from its file and not made again, which has no views to pick in: the
        /// drawing made again from the source <see cref="SourceToMakeFrom"/> finds once the command has ended, one undo step
        /// as the other commands make it, and the command started again to ask for its points; where no source is at hand,
        /// said in plain words. False for a drawing that has its views.
        /// </summary>
        private bool MakeFirst(CadCommandContext c)
        {
            ModelDrawing entry = Find(c.Document);

            if (entry.Drawing.Source != null)
            {
                return false;
            }

            IDrawingSourceProvider source;

            try
            {
                source = SourceToMakeFrom(entry);
            }
            catch (InvalidOperationException ex)
            {
                c.Write(ex.Message);
                return true;
            }

            string command = c.Command.Name;
            c.Write(entry.Number + " was opened from its file: it is made again from " + source.Name + " first, then the points are picked.");
            Later(() =>
            {
                if (_current == entry && Run("Views of " + entry.Number, (e, provider) => e.Drawing.Apply(e.Drawing.Prepare(ObjectsToMakeFrom(e, provider), null)), entry.PresetName) != null)
                {
                    Model.StartCommand(command);
                }
            });
            return true;
        }

        /// <summary>AUTODIM, AUTOMARK: the selected view's dimensions or marks made again once the command has ended.</summary>
        private IEnumerable<CadPrompt> OnViewCommand(CadCommandContext c, Func<UpdateReport> run)
        {
            if (Find(c.Document) == null)
            {
                c.Write(c.Command.Name + " works on a model drawing; " + c.Document.Title + " is a DXF.");
            }
            else if (SelectedViewId() == null)
            {
                c.Write("Select a view first: its frame, or anything in it.");
            }
            else
            {
                Later(() => run());
            }

            return Array.Empty<CadPrompt>();
        }

        /// <summary>
        /// A change of the settings for the selected view, the drawing made again with it as the core's call for one view makes
        /// it; a drawing opened from its file and not updated is made from the source <see cref="SourceToMakeFrom"/> finds.
        /// </summary>
        private UpdateReport OnSelectedView(string what, Action<DrawingSettings, string> change)
        {
            string viewId = SelectedViewId();

            if (viewId == null)
            {
                Say(_current == null ? "No model drawing is shown." : "Select a view first: its frame, or anything in it.");
                return null;
            }

            return Run(what + " of " + viewId, (e, provider) =>
            {
                DrawingSettings settings = e.Drawing.Settings;
                change(settings, viewId);
                return e.Drawing.Apply(e.Drawing.Prepare(ObjectsToMakeFrom(e, provider), settings));
            });
        }

        /// <summary>Runs a change of the drawing shown, one undo step, and says what came of it; its preset is known as "Changed" then, or by the name given.</summary>
        private UpdateReport Run(string what, Func<ModelDrawing, IDrawingSourceProvider, UpdateReport> change, string presetName = "Changed")
        {
            ModelDrawing entry = _current;

            if (entry == null)
            {
                Say("No model drawing is shown.");
                return null;
            }

            UpdateReport report;

            try
            {
                IDrawingSourceProvider provider = SourceToMakeFrom(entry);
                report = WriteChange(entry, provider, presetName, () => change(entry, provider));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                Say(what + " not made: " + ex.Message.Split('\n')[0].Trim());
                return null;
            }

            Changed(entry);
            Say(what + ": " + Count(report.AddedElements.Count, "element") + " added, " + report.ChangedElements.Count.ToString(CultureInfo.InvariantCulture) + " changed, "
                + report.RemovedElements.Count.ToString(CultureInfo.InvariantCulture) + " taken away; " + Count(report.EditsKept.Count, "hand edit") + " kept.");
            SayDropped(report);
            return report;
        }

        /// <summary>The next letter no section or detail of the drawing has.</summary>
        private string NextLetter()
        {
            DrawingSettings settings = _current.Drawing.Settings;
            var used = new HashSet<string>(settings.Views.Sections.Select(s => s.Name).Concat(settings.Views.Details.Select(d => d.Name)), StringComparer.OrdinalIgnoreCase);

            for (char letter = 'A'; letter <= 'Z'; letter++)
            {
                if (!used.Contains(letter.ToString()))
                {
                    return letter.ToString();
                }
            }

            return "S" + (used.Count + 1).ToString(CultureInfo.InvariantCulture);
        }

        private bool TryViewAt(GeoPoint2 point, DrawingView view, out GeoPoint2 inView)
        {
            bool layout = _current.Drawing.Settings.ExportMode == DrawingExportMode.LayoutAndViewports;
            bool inModel = _current.Document.CurrentSpace == null || _current.Document.CurrentSpace.IsModel;
            Tolerance tol = Tolerance;

            if (layout && inModel)
            {
                inView = new GeoPoint2(point.X - view.ModelOrigin.X, point.Y - view.ModelOrigin.Y);
            }
            else
            {
                inView = new GeoPoint2((point.X - view.SheetOrigin.X) * view.Scale, (point.Y - view.SheetOrigin.Y) * view.Scale);
            }

            GeoRectangle2 box = view.Box;
            return Math.Abs(inView.X - box.Center.X) <= box.Width / 2.0 + tol.EqualPoint && Math.Abs(inView.Y - box.Center.Y) <= box.Height / 2.0 + tol.EqualPoint;
        }

        private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static void AddOnce(IList<string> list, string item)
        {
            if (!list.Contains(item))
            {
                list.Add(item);
            }
        }
    }
}

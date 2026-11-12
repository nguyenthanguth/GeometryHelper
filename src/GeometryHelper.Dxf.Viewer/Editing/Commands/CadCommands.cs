using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The commands of C2, each a body of a few lines that asks AutoCAD's prompts in AutoCAD's words: the view commands here,
    /// the edit commands in CadCommands.Edit.cs, the draw commands in CadCommands.Draw.cs and the dimensions in
    /// CadCommands.Dimension.cs.
    /// </summary>
    internal static partial class CadCommands
    {
        internal static void AddTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("ZOOM", Zoom));
            registry.Add(new CadCommand("PAN", Pan));
            registry.Add(new CadCommand("REGEN", Regen));
            registry.Add(new CadCommand("PROPERTIES", c => Ask(c, "PROPERTIES")));
            registry.Add(new CadCommand("OPTIONS", c => Ask(c, "OPTIONS")));
            registry.Add(new CadCommand("QSAVE", QuickSave));

            registry.Add(new CadCommand("MOVE", Move, true));
            registry.Add(new CadCommand("COPY", Copy, true));
            registry.Add(new CadCommand("ERASE", Erase, true));
            registry.Add(new CadCommand("ROTATE", Rotate, true));
            registry.Add(new CadCommand("SCALE", Scale, true));
            registry.Add(new CadCommand("MIRROR", Mirror, true));
            registry.Add(new CadCommand("EXPLODE", Explode, true));
            registry.Add(new CadCommand("MATCHPROP", MatchProperties));
            registry.Add(new CadCommand("LAYER", Layer));
            registry.Add(new CadCommand("-LAYER", LayerTyped));
            registry.Add(new CadCommand("LAYERSTATE", LayerStateManager));
            registry.Add(new CadCommand("VIEW", ViewManager));
            registry.Add(new CadCommand("-VIEW", ViewTyped));
            registry.Add(new CadCommand("UNDO", Undo));
            registry.Add(new CadCommand("U", UndoOne));
            registry.Add(new CadCommand("REDO", Redo));

            registry.Add(new CadCommand("LINE", Line));
            registry.Add(new CadCommand("PLINE", Polyline));
            registry.Add(new CadCommand("CIRCLE", Circle));
            registry.Add(new CadCommand("ARC", Arc));
            registry.Add(new CadCommand("RECTANG", Rectangle));
            registry.Add(new CadCommand("TEXT", Text));
            registry.Add(new CadCommand("MTEXT", MText));
            registry.Add(new CadCommand("MLEADER", MLeader));
            registry.Add(new CadCommand("MLEADERSTYLE", MLeaderStyleDialog));
            registry.Add(new CadCommand("LEADER", Leader));
            registry.Add(new CadCommand("QLEADER", QuickLeader));
            registry.Add(new CadCommand("TOLERANCE", FeatureControlFrame));
            registry.Add(new CadCommand("MVIEW", MView));
            registry.Add(new CadCommand("MSPACE", ModelSpaceThrough));
            registry.Add(new CadCommand("PSPACE", PaperSpace));

            registry.Add(new CadCommand("DIMLINEAR", DimLinear));
            registry.Add(new CadCommand("DIMALIGNED", DimAligned));
            registry.Add(new CadCommand("DIMRADIUS", c => DimRadial(c, false)));
            registry.Add(new CadCommand("DIMDIAMETER", c => DimRadial(c, true)));
            registry.Add(new CadCommand("DIMANGULAR", DimAngular));
            registry.Add(new CadCommand("DIMARC", DimArc));
            registry.Add(new CadCommand("DIMCONTINUE", DimContinue));
            registry.Add(new CadCommand("DIMBASELINE", DimBaseline));
            registry.Add(new CadCommand("QDIM", QuickDimension));
            registry.Add(new CadCommand("DIMEDIT", DimEdit));
            registry.Add(new CadCommand("DIMTEDIT", DimTextEdit));
            registry.Add(new CadCommand("DIMSPACE", DimSpace));
            registry.Add(new CadCommand("DIMBREAK", DimBreak));
            registry.Add(new CadCommand("DIMORDINATE", DimOrdinate));
            registry.Add(new CadCommand("DIMJOGLINE", DimJogLine));
            registry.Add(new CadCommand("DIMJOGGED", DimJogged));
            registry.Add(new CadCommand("DIM", Dim));
            AddDimensionAssociationTo(registry);

            registry.Add(new CadCommand("ELLIPSE", Ellipse));
            registry.Add(new CadCommand("POINT", Point));
            registry.Add(new CadCommand("LAYOUT", Layout));
            registry.Add(new CadCommand("DIST", Dist));
            registry.Add(new CadCommand("AREA", Area));
            registry.Add(new CadCommand("ID", Id));
            registry.Add(new CadCommand("LIST", ListObjects, true));

            registry.Add(new CadCommand("XLINE", XLine));
            registry.Add(new CadCommand("RAY", Ray));
            registry.Add(new CadCommand("POLYGON", Polygon));
            registry.Add(new CadCommand("DONUT", Donut));
            registry.Add(new CadCommand("SELECTALL", SelectAll));
            registry.Add(new CadCommand("LAYOFF", LayerOff));
            registry.Add(new CadCommand("LAYON", c => LayersAll(c, false)));
            registry.Add(new CadCommand("LAYFRZ", LayerFreeze));
            registry.Add(new CadCommand("LAYTHW", c => LayersAll(c, true)));
            registry.Add(new CadCommand("LAYLCK", c => LayerLock(c, true)));
            registry.Add(new CadCommand("LAYULK", c => LayerLock(c, false)));
            registry.Add(new CadCommand("LAYISO", LayerIsolate));
            registry.Add(new CadCommand("LAYUNISO", LayerUnisolate));
            registry.Add(new CadCommand("LAYERP", LayerPrevious));
            registry.Add(new CadCommand("LAYERPMODE", LayerPreviousMode));
            registry.Add(new CadCommand("VPLAYER", ViewportLayer));
            registry.Add(new CadCommand("LAYVPI", LayerViewportIsolate));
            registry.Add(new CadCommand("VPSCALE", ViewportScale));
            registry.Add(new CadCommand("VPCLIP", ViewportClip));
            registry.Add(new CadCommand("XCLIP", XClip, true));
            registry.Add(new CadCommand("-SCALELISTEDIT", ScaleListEdit));
            registry.Add(new CadCommand("SCALELISTEDIT", ScaleListDialog));
            registry.Add(new CadCommand("MEASUREGEOM", MeasureGeom));
            registry.Add(new CadCommand("UNITS", Units));
            registry.Add(new CadCommand("-UNITS", UnitsTyped));
            registry.Add(new CadCommand("SCRIPT", Script));
            registry.Add(new CadCommand("RECOVER", Recover));
            registry.Add(new CadCommand("AUDIT", Audit));
            registry.Add(new CadCommand("UCS", Ucs));
            registry.Add(new CadCommand("UCSICON", UcsIcon));
            registry.Add(new CadCommand("PLAN", Plan));
            registry.Add(new CadCommand("UCSMAN", UcsManager));
            registry.Add(new CadCommand("LINETYPE", Linetype));
            registry.Add(new CadCommand("-LINETYPE", LinetypeTyped));
            registry.Add(new CadCommand("HATCH", Hatch));
            registry.Add(new CadCommand("HATCHEDIT", HatchEdit));
            registry.Add(new CadCommand("BOUNDARY", Boundary));
            registry.Add(new CadCommand("STYLE", StyleDialog));
            registry.Add(new CadCommand("-STYLE", StyleTyped));
            registry.Add(new CadCommand("DIMSTYLE", DimStyleDialog));
            AddTablesTo(registry);
            registry.Add(new CadCommand("PTYPE", PointStyle));
            registry.Add(new CadCommand("OFFSET", Offset));
            registry.Add(new CadCommand("FILLET", Fillet));
            registry.Add(new CadCommand("CHAMFER", Chamfer));
            registry.Add(new CadCommand("STRETCH", Stretch));
            registry.Add(new CadCommand("ARRAY", ArrayAny, true));
            registry.Add(new CadCommand("ARRAYRECT", ArrayRectangular, true));
            registry.Add(new CadCommand("ARRAYPOLAR", ArrayPolar, true));
            registry.Add(new CadCommand("-ARRAY", ArrayTyped, true));
            registry.Add(new CadCommand("ALIGN", Align, true));
            registry.Add(new CadCommand("TRIM", Trim));
            registry.Add(new CadCommand("EXTEND", ExtendCommand));
            registry.Add(new CadCommand("BREAK", Break));
            registry.Add(new CadCommand("BREAKATPOINT", BreakAtPoint));
            registry.Add(new CadCommand("JOIN", Join));
            registry.Add(new CadCommand("LENGTHEN", Lengthen));
            registry.Add(new CadCommand("PEDIT", Pedit));
            registry.Add(new CadCommand("REVCLOUD", RevCloud));
            AddBlocksTo(registry);
            AddBlockEditorTo(registry);
            registry.Add(new CadCommand("COPYCLIP", CopyClip, true));
            registry.Add(new CadCommand("CUTCLIP", CutClip, true));
            registry.Add(new CadCommand("COPYBASE", CopyBase, true));
            registry.Add(new CadCommand("PASTECLIP", PasteClip));
            registry.Add(new CadCommand("PASTEBLOCK", PasteBlock));
            registry.Add(new CadCommand("PASTEORIG", PasteOrig));
            registry.Add(new CadCommand("PASTESPEC", PasteSpecial));
            registry.Add(new CadCommand("TEXTEDIT", TextEdit));
            registry.Add(new CadCommand("MTEDIT", MTextEdit));
            AddPlotTo(registry);
            AddImageOutTo(registry);
            AddExportTo(registry);
            AddSaveAsTo(registry);
            registry.Add(new CadCommand("QSELECT", QuickSelect));
            registry.Add(new CadCommand("FIND", Find, true));
            registry.Add(new CadCommand("DRAWORDER", DrawOrder, true));
            registry.Add(new CadCommand("TEXTTOFRONT", TextToFront));
            AddAnnotation2To(registry);
            registry.Add(new CadCommand("GROUP", Group, true));
            registry.Add(new CadCommand("-GROUP", GroupTyped));
            registry.Add(new CadCommand("UNGROUP", Ungroup, true));
            registry.Add(new CadCommand("CLASSICGROUP", GroupManager));
            registry.Add(new CadCommand("PURGE", Purge));
            registry.Add(new CadCommand("-PURGE", PurgeTyped));
            AddXrefsTo(registry);
            AddShapesBatchTo(registry);
            AddBatchFourBTo(registry);
            AddVariablesTo(registry);
            AddPgpBasicsTo(registry);
            AddOutputBatchTo(registry);
        }

        /// <summary>A point of the plan in space, at nought height.</summary>
        internal static GeoPoint3 P3(GeoPoint2 point) => new GeoPoint3(point.X, point.Y, 0.0);

        internal static string Length(double value) => CadCommandContext.Length(value);

        /// <summary>Numbers as the owner contract writes them (<see cref="CadEditKind"/>): invariant, to the bit, separated by commas.</summary>
        internal static string Values(params double[] numbers)
        {
            var texts = new string[numbers.Length];

            for (int i = 0; i < numbers.Length; i++)
            {
                texts[i] = numbers[i].ToString("R", CultureInfo.InvariantCulture);
            }

            return string.Join(",", texts);
        }

        /// <summary>The handle of the first entity an edit added, or null.</summary>
        internal static string AddedHandle(CadEditResult result)
        {
            foreach (CadEditRecord record in result.Records)
            {
                if (record.Kind == CadEditRecordKind.Added)
                {
                    return record.Handle;
                }
            }

            return null;
        }

        /// <summary>Gives a new entity the current layer, colour, linetype and lineweight of the drawing, as AutoCAD draws one.</summary>
        internal static T Current<T>(CadCommandContext c, T entity)
            where T : DxfEntity
        {
            entity.SetDatabaseDefaults(c.Database);
            return entity;
        }

        private static IEnumerable<CadPrompt> Ask(CadCommandContext c, string name)
        {
            if (!c.Session.Request(name))
            {
                c.Write(name + " is shown by the window.");
            }

            yield break;
        }

        /// <summary>
        /// QSAVE (Ctrl+S): the drawing saved to its file, a DWG back as a DWG. One with no file yet asks the window for its Save
        /// As dialog; with FILEDIA 0, SAVEAS's questions on the command line instead (<see cref="SaveAsTyped"/>), and the file
        /// handed to the window to save, or saved here with no window.
        /// </summary>
        private static IEnumerable<CadPrompt> QuickSave(CadCommandContext c)
        {
            if (c.Document.HasFile)
            {
                c.Document.Save();
                c.Write("Saved " + c.Document.FileName + ".");
                SayLeftOut(c);
                yield break;
            }

            if (c.Session.FileDialogs)
            {
                if (!c.Session.Request("QSAVE"))
                {
                    c.Write("The drawing has no file yet: save it with Save As.");
                }

                yield break;
            }

            foreach (CadPrompt prompt in SaveAsTyped(c, c.Document.Title))
            {
                yield return prompt;
            }
        }

        /// <summary>"Left out of the DWG, not written yet: IMAGE 1." where the drawing was just saved as a DWG that left something out.</summary>
        internal static void SayLeftOut(CadCommandContext c)
        {
            if (c.Document.LeftOutOfDwg.Length > 0)
            {
                c.Write("Left out of the DWG, not written yet: " + c.Document.LeftOutOfDwg + ".");
            }

            foreach (string sentence in c.Document.LeftOutOfDxf)
            {
                c.Write(sentence);
            }
        }

        private static IEnumerable<CadPrompt> Regen(CadCommandContext c)
        {
            c.Write("Regenerating model.");

            // Points of a relative size take the size of the view regenerated in, as AutoCAD's.
            if (c.Session.View != null)
            {
                c.Document.PointViewHeight = c.Session.WorkView.VisibleBox.Height;
            }

            c.Document.Regen();
            yield break;
        }

        private static IEnumerable<CadPrompt> Zoom(CadCommandContext c)
        {
            // Through a viewport the viewport's view is zoomed; through one whose display is locked, the paper, as AutoCAD's.
            DxfViewport through = c.Document.ActiveViewport;

            if (through != null && !through.Locked)
            {
                foreach (CadPrompt prompt in ZoomThrough(c, through.Handle))
                {
                    yield return prompt;
                }

                yield break;
            }

            CadView view = c.Session.View;

            if (view == null)
            {
                c.Write("There is no view to zoom.");
                yield break;
            }

            yield return CadPrompt.ForPoint(
                "Specify corner of window, enter a scale factor (nX or nXP)",
                null,
                new[] { "All", "Extents", "Previous", "Window" },
                "real time",
                true
            );
            DisplayBox before = view.VisibleBox;
            CadSpace shown = c.Document.CurrentSpace;

            // What the space draws and, in a layout, its sheet, as a double click of the middle button shows them.
            DisplayBox extents = c.Document.GetDisplay(shown).Bounds.Union(shown.PaperBounds);

            if (c.Input.IsNone)
            {
                c.Write("Real-time zoom is the mouse wheel's: the view is as it was.");
                yield break;
            }

            if (c.Input.Is("All") || c.Input.Is("Extents"))
            {
                view.ZoomTo(extents);
            }
            else if (c.Input.Kind == CadInputKind.Text)
            {
                string typed = c.Input.Text.Trim();
                bool relative =
                    typed.EndsWith("X", StringComparison.OrdinalIgnoreCase)
                    && !typed.EndsWith("XP", StringComparison.OrdinalIgnoreCase);
                string number = relative ? typed.Substring(0, typed.Length - 1) : typed;

                if (typed.EndsWith("XP", StringComparison.OrdinalIgnoreCase))
                {
                    c.Write(
                        "nXP scales the view of a viewport to paper space, which needs a viewport's model space (MSPACE): the view is as it was."
                    );
                    yield break;
                }

                if (
                    !double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double factor)
                    || !(factor > 0.0)
                    || double.IsInfinity(factor)
                )
                {
                    c.Write("Requires a point, a scale factor above nought (nX or nXP), or an option keyword.");
                    yield break;
                }

                // nX scales the view as it is about its centre; n alone scales the view of the extents, as AutoCAD's n scales that of the limits.
                if (!relative)
                {
                    view.ZoomTo(extents);
                }

                view.ZoomAt(view.Width / 2.0, view.Height / 2.0, factor);
            }
            else if (c.Input.Is("Previous"))
            {
                if (!c.Session.Views.TryTake(shown, out DisplayBox previous))
                {
                    c.Write("No previous view saved.");
                    yield break;
                }

                view.ZoomTo(previous, 0.0);
                c.Document.RecordSpaceView(shown, view, before, "ZOOM", false, previous);
                yield break;
            }
            else if (c.Input.IsPoint || c.Input.Is("Window"))
            {
                GeoPoint2 first;

                if (c.Input.IsPoint)
                {
                    first = c.Input.Point;
                }
                else
                {
                    yield return CadPrompt.ForPoint("Specify first corner");

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    first = c.Input.Point;
                }

                yield return CadPrompt.ForPoint("Specify opposite corner", first);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 second = c.Input.Point;

                // Corners picked through a locked viewport are points of model space, shown on the paper where it zooms.
                if (through != null)
                {
                    first = CadViewports.ToPaper(through, first);
                    second = CadViewports.ToPaper(through, second);
                }

                var box = new DisplayBox(
                    Math.Min(first.X, second.X),
                    Math.Min(first.Y, second.Y),
                    Math.Max(first.X, second.X),
                    Math.Max(first.Y, second.Y)
                );

                if (box.Width <= 0.0 || box.Height <= 0.0)
                {
                    c.Write("*Invalid window*");
                    yield break;
                }

                view.ZoomTo(box, 0.0);
            }
            else
            {
                yield break;
            }

            c.Document.RecordSpaceView(shown, view, before, "ZOOM", false);
        }

        /// <summary>
        /// ZOOM through a viewport: its view zoomed, the paper as it is, one undo step. All and Extents fit model space's
        /// extents to the frame; nX scales the view as it is, n that of the extents; nXP sets the viewport's scale, n paper
        /// units to the model unit, so 1/50XP shows model space at 1:50; Window fits the window picked; Previous goes back.
        /// </summary>
        private static IEnumerable<CadPrompt> ZoomThrough(CadCommandContext c, string handle)
        {
            yield return CadPrompt.ForPoint(
                "Specify corner of window, enter a scale factor (nX or nXP)",
                null,
                new[] { "All", "Extents", "Previous", "Window" },
                "real time",
                true
            );
            var viewport = (DxfViewport)c.Document.FindEntity(handle);

            if (viewport == null)
            {
                yield break;
            }

            GeoPoint2 wasCenter = viewport.ViewCenter;
            double wasHeight = viewport.ViewHeight;
            DisplayBox extents = c.Document.GetDisplay(c.Document.ModelSpace).Bounds;
            GeoPoint2 center = viewport.ViewCenter;
            double height = viewport.ViewHeight;

            if (c.Input.IsNone)
            {
                c.Write("Real-time zoom is the mouse wheel's: the view is as it was.");
                yield break;
            }

            if (c.Input.Is("All") || c.Input.Is("Extents"))
            {
                if (!extents.IsEmpty)
                {
                    CadViewports.Fit(viewport, extents, out center, out height);
                }
            }
            else if (c.Input.Kind == CadInputKind.Text)
            {
                string typed = c.Input.Text.Trim();
                bool paper = typed.EndsWith("XP", StringComparison.OrdinalIgnoreCase);
                bool relative = !paper && typed.EndsWith("X", StringComparison.OrdinalIgnoreCase);
                string number = typed.Substring(
                    0,
                    typed.Length
                        - (
                            paper ? 2
                            : relative ? 1
                            : 0
                        )
                );

                if (!TryFactor(number, out double factor))
                {
                    c.Write("Requires a point, a scale factor above nought (nX or nXP), or an option keyword.");
                    yield break;
                }

                if (paper)
                {
                    height = viewport.Height / factor;
                }
                else
                {
                    if (!relative && !extents.IsEmpty)
                    {
                        CadViewports.Fit(viewport, extents, out center, out height);
                    }

                    height /= factor;
                }
            }
            else if (c.Input.Is("Previous"))
            {
                // Each viewport goes back through views of its own, as AutoCAD's.
                if (!c.Session.Views.TryTake(handle, out center, out height))
                {
                    c.Write("No previous view saved.");
                    yield break;
                }

                CadEditResult taken = c.Apply(
                    c.Document.ViewRecord(handle, center, height),
                    CadDocument.ViewWriter(handle, center, height)
                );

                if (taken.Succeeded)
                {
                    c.Document.TookViewportView(taken, handle, center, height);
                }

                yield break;
            }
            else if (c.Input.IsPoint || c.Input.Is("Window"))
            {
                GeoPoint2 first;

                if (c.Input.IsPoint)
                {
                    first = c.Input.Point;
                }
                else
                {
                    yield return CadPrompt.ForPoint("Specify first corner");

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    first = c.Input.Point;
                }

                yield return CadPrompt.ForPoint("Specify opposite corner", first);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 second = c.Input.Point;
                var box = new DisplayBox(
                    Math.Min(first.X, second.X),
                    Math.Min(first.Y, second.Y),
                    Math.Max(first.X, second.X),
                    Math.Max(first.Y, second.Y)
                );

                if (box.Width <= 0.0 || box.Height <= 0.0)
                {
                    c.Write("*Invalid window*");
                    yield break;
                }

                CadViewports.Fit(viewport, box, out center, out height);
            }
            else
            {
                yield break;
            }

            CadEditResult moved = c.Apply(
                c.Document.ViewRecord(handle, center, height),
                CadDocument.ViewWriter(handle, center, height)
            );

            if (moved.Succeeded)
            {
                c.Document.KeepViewportView(moved, handle, wasCenter, wasHeight);
            }
        }

        /// <summary>A scale factor typed, as 2, 0.5 or 1/50: a finite number above nought.</summary>
        private static bool TryFactor(string text, out double factor)
        {
            factor = 0.0;
            int slash = text.IndexOf('/');

            if (slash > 0)
            {
                if (
                    !double.TryParse(
                        text.Substring(0, slash),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double over
                    )
                    || !double.TryParse(
                        text.Substring(slash + 1),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double under
                    )
                    || under == 0.0
                )
                {
                    return false;
                }

                factor = over / under;
            }
            else if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out factor))
            {
                return false;
            }

            return factor > 0.0 && !double.IsInfinity(factor);
        }

        private static IEnumerable<CadPrompt> Pan(CadCommandContext c) => PanBody(c, false);

        /// <summary>
        /// PAN and -PAN: the base point or displacement, then the second point. Typed as -PAN, Enter at the first prompt is
        /// refused, "Requires two points, or a displacement followed by ENTER.", and the prompt asked again, as AutoCAD 2021 does.
        /// </summary>
        private static IEnumerable<CadPrompt> PanBody(CadCommandContext c, bool typed)
        {
            DxfViewport through = c.Document.ActiveViewport;

            if (through != null && !through.Locked)
            {
                // Through a viewport its view is panned, one undo step; the paper stays.
                string handle = through.Handle;

                foreach (CadPrompt prompt in PanBase(c, typed))
                {
                    yield return prompt;
                }

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 start = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify second point", start);
                GeoVector2 shift = c.Input.IsPoint ? c.Input.Point - start : new GeoVector2(start.X, start.Y);
                var viewport = (DxfViewport)c.Document.FindEntity(handle);
                GeoPoint2 wasCenter = viewport.ViewCenter;
                double wasHeight = viewport.ViewHeight;

                // The view's centre is kept turned by its twist: the shift of model space turned with it.
                if (viewport.TwistAngle != 0.0)
                {
                    double cos = Math.Cos(viewport.TwistAngle);
                    double sin = Math.Sin(viewport.TwistAngle);
                    shift = new GeoVector2(cos * shift.X - sin * shift.Y, sin * shift.X + cos * shift.Y);
                }

                GeoPoint2 center = viewport.ViewCenter - shift;

                CadEditResult panned = c.Apply(
                    c.Document.ViewRecord(handle, center, viewport.ViewHeight),
                    CadDocument.ViewWriter(handle, center, viewport.ViewHeight)
                );

                if (panned.Succeeded)
                {
                    c.Document.KeepViewportView(panned, handle, wasCenter, wasHeight);
                }

                yield break;
            }

            CadView view = c.Session.View;

            if (view == null)
            {
                c.Write("There is no view to pan.");
                yield break;
            }

            foreach (CadPrompt prompt in PanBase(c, typed))
            {
                yield return prompt;
            }

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 from = c.Input.Point;
            yield return CadPrompt.ForPoint("Specify second point", from);
            GeoVector2 move = c.Input.IsPoint ? c.Input.Point - from : new GeoVector2(from.X, from.Y);

            // A displacement through a locked viewport is of model space, the paper moving by it at the viewport's scale.
            if (through != null)
            {
                move = move * (through.Height / through.ViewHeight);
            }

            DisplayBox was = view.VisibleBox;
            view.Pan(move.X * view.PixelsPerUnit, -move.Y * view.PixelsPerUnit);
            c.Document.RecordSpaceView(c.Document.CurrentSpace, view, was, "PAN", false);
        }

        /// <summary>
        /// LAYOUT: New adds a layout after the last tab, a sheet of A3 with its viewport, named Layout2 where Layout1 is taken;
        /// Rename renames one, Set shows one; ? lists the layouts. One undo step takes a new layout or a new name back.
        /// </summary>
        private static IEnumerable<CadPrompt> Layout(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword("Enter layout option", new[] { "New", "Rename", "Set", "?" }, "New");

            if (c.Input.Is("Rename") || c.Input.Is("Set"))
            {
                foreach (CadPrompt prompt in c.Input.Is("Set") ? LayoutSet(c) : LayoutRename(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (c.Input.Is("?"))
            {
                foreach (CadSpace space in c.Document.Spaces)
                {
                    c.Write(space.IsModel ? "Model" : space.Name);
                }

                yield break;
            }

            if (!c.Input.Is("New"))
            {
                yield break;
            }

            int next = c.Document.Spaces.Count;

            while (
                c.Document.Spaces.Any(s =>
                    string.Equals(
                        s.Name,
                        "Layout" + next.ToString(CultureInfo.InvariantCulture),
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            {
                next++;
            }

            string offered = "Layout" + next.ToString(CultureInfo.InvariantCulture);
            yield return CadPrompt.ForString("Enter name of new layout", offered);
            string name = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : offered;

            if (name.Length == 0 || name.Length > 255 || name.IndexOfAny("<>/\\\":;?*|,=`".ToCharArray()) >= 0)
            {
                c.Write("Invalid layout name.");
                yield break;
            }

            if (c.Document.Spaces.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                c.Write("A layout named \"" + name + "\" already exists.");
                yield break;
            }

            CadEditResult result = c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "New layout", null, name) },
                s =>
                {
                    var layout = new DxfLayout { LayoutName = name };
                    layout.AddToLayoutDictionary(s.Database, null);
                    layout.Initialize();
                }
            );

            if (result.Succeeded)
            {
                c.Write("Layout \"" + name + "\" created.");
            }
        }

        private static string Number(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>
        /// A direction as the default of a prompt for one, in degrees counted as the drawing counts angles: from ANGBASE,
        /// clockwise with ANGDIR 1, so that a text along east is offered as 90 where angles are counted from north; to the
        /// places of AUPREC, as AutoCAD 2021 offers 40.587 degrees as 41 under its default of none, which it takes too where
        /// the header gives no AUPREC.
        /// </summary>
        private static string Counted(CadCommandContext c, double direction)
        {
            int places = Math.Max(0, Math.Min(8, c.Database.Auprec));
            double degrees = Math.Round(c.Session.CountAngle(direction) * 180.0 / Math.PI, places);
            return (degrees == 0.0 ? 0.0 : degrees).ToString("F" + places, CultureInfo.InvariantCulture);
        }
    }
}

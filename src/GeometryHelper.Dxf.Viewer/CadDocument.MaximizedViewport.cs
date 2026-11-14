using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What a layout remembers of the viewport VPMAX maximized, so that VPMIN can put it and its neighbours back: the frame and
    /// view the viewport had, the frame it was given, and the viewports that were on and were turned off.
    /// </summary>
    internal sealed class CadMaximizedViewport
    {
        internal CadMaximizedViewport(string handle)
        {
            Handle = handle;
        }

        /// <summary>The handle of the viewport maximized.</summary>
        internal string Handle { get; }

        /// <summary>The centre of the viewport's frame on the sheet before it was maximized.</summary>
        internal GeoPoint3 Center { get; set; }

        /// <summary>The width of the frame before.</summary>
        internal double Width { get; set; }

        /// <summary>The height of the frame before.</summary>
        internal double Height { get; set; }

        /// <summary>The view's centre, as the viewport keeps it, before.</summary>
        internal GeoPoint2 ViewCenter { get; set; }

        /// <summary>The height of model space the viewport showed before.</summary>
        internal double ViewHeight { get; set; }

        /// <summary>The centre of the frame the viewport was given.</summary>
        internal GeoPoint3 MaximizedCenter { get; set; }

        /// <summary>The width of the frame the viewport was given.</summary>
        internal double MaximizedWidth { get; set; }

        /// <summary>The height of the frame the viewport was given.</summary>
        internal double MaximizedHeight { get; set; }

        /// <summary>The handles of the other viewports of the layout that were on and were turned off, in the order of the layout.</summary>
        internal List<string> TurnedOff { get; } = new List<string>();
    }

    /// <summary>
    /// A viewport of a layout maximized into a temporary model view, as AutoCAD 2021's VPMAX does it and VPMIN takes it back.
    /// </summary>
    public sealed partial class CadDocument
    {
        /// <summary>
        /// Gets the handle of the viewport of the layout shown that VPMAX has maximized and VPMIN has not yet taken back; null
        /// where none is, and where an undo has taken the maximizing back.
        /// </summary>
        public string MaximizedViewportHandle => MaximizedOf(CurrentSpace)?.Handle;

        /// <summary>Gets whether a viewport of the layout shown is maximized (<see cref="MaximizedViewportHandle"/>).</summary>
        public bool IsViewportMaximized => MaximizedViewportHandle != null;

        /// <summary>What a space remembers of its maximized viewport, null where there is none or the viewport no longer stands as it was left.</summary>
        private CadMaximizedViewport MaximizedOf(CadSpace space)
        {
            CadMaximizedViewport state = space?.Maximized;

            if (state == null || !(FindEntity(state.Handle) is DxfViewport viewport) || viewport.IsErased)
            {
                return null;
            }

            Tolerance tolerance = Tolerance.Default;
            GeoPoint3 centre = viewport.CenterPoint;
            return
                Math.Abs(centre.X - state.MaximizedCenter.X) <= tolerance.EqualPoint
                && Math.Abs(centre.Y - state.MaximizedCenter.Y) <= tolerance.EqualPoint
                && Math.Abs(viewport.Width - state.MaximizedWidth) <= tolerance.EqualPoint
                && Math.Abs(viewport.Height - state.MaximizedHeight) <= tolerance.EqualPoint
                ? state
                : null;
        }

        /// <summary>
        /// Maximizes a viewport of the layout shown, as VPMAX: its frame made the box given and its view moved so that it shows
        /// the part of model space it showed under the middle of that box at the scale it had, the other viewports of the layout
        /// that are on turned off, and the viewport worked through. A box 1 000 wide and 600 high over a viewport 100 by 60 at
        /// 1:20 gives a frame of 1 000 by 600 showing model space at 1:20, 20 000 by 12 000 of it. One undo step.
        /// </summary>
        /// <param name="handle">The handle of the viewport.</param>
        /// <param name="window">The box of the sheet the viewport is given, the sheet's own area the canvas shows less a margin.</param>
        /// <returns>What came of it; refused for a viewport that cannot be worked through or a box of no size.</returns>
        internal CadEditResult MaximizeViewport(string handle, DisplayBox window)
        {
            CadSpace space = CurrentSpace;
            DxfViewport viewport = WorkedThrough(space, handle);

            if (viewport == null)
            {
                return CadEditResult.Refuse("The layout shown has no viewport " + handle + " to maximize.");
            }

            if (window.IsEmpty || !(window.Width > 0.0) || !(window.Height > 0.0))
            {
                return CadEditResult.Refuse("There is no area to maximize the viewport into.");
            }

            var state = new CadMaximizedViewport(viewport.Handle)
            {
                Center = viewport.CenterPoint,
                Width = viewport.Width,
                Height = viewport.Height,
                ViewCenter = viewport.ViewCenter,
                ViewHeight = viewport.ViewHeight,
                MaximizedCenter = new GeoPoint3(
                    (window.MinX + window.MaxX) / 2.0,
                    (window.MinY + window.MaxY) / 2.0,
                    viewport.CenterPoint.Z
                ),
                MaximizedWidth = window.Width,
                MaximizedHeight = window.Height,
            };
            double scale = viewport.Height / viewport.ViewHeight;
            GeoPoint2 underMiddle = CadViewports.ToModel(
                viewport,
                new GeoPoint2(state.MaximizedCenter.X, state.MaximizedCenter.Y)
            );
            GeoPoint2 viewCenter = CadViewports.ToView(viewport, underMiddle);
            double viewHeight = window.Height / scale;
            var records = new List<CadEditRecord>
            {
                new CadEditRecord(
                    CadEditRecordKind.PropertyChanged,
                    viewport.Handle,
                    "Maximized",
                    CadCommands.Values(state.Center.X, state.Center.Y, state.Width, state.Height),
                    CadCommands.Values(
                        state.MaximizedCenter.X,
                        state.MaximizedCenter.Y,
                        state.MaximizedWidth,
                        state.MaximizedHeight
                    )
                ),
            };

            foreach (DxfEntity entity in space.Block)
            {
                if (
                    entity is DxfViewport other
                    && !other.IsErased
                    && other.Number > 1
                    && !ReferenceEquals(other, viewport)
                )
                {
                    state.TurnedOff.Add(other.Handle);
                    records.Add(new CadEditRecord(CadEditRecordKind.PropertyChanged, other.Handle, "On", "Yes", "No"));
                }
            }

            CadEditResult result = ApplyEdit("VPMAX", records, s =>
            {
                var target = (DxfViewport)s.Document.FindEntity(state.Handle);
                target.CenterPoint = state.MaximizedCenter;
                target.Width = state.MaximizedWidth;
                target.Height = state.MaximizedHeight;
                target.ViewCenter = viewCenter;
                target.ViewHeight = viewHeight;

                foreach (string off in state.TurnedOff)
                {
                    ((DxfViewport)s.Document.FindEntity(off)).On = false;
                }
            });

            if (result.Succeeded)
            {
                space.Maximized = state;
            }

            return result;
        }

        /// <summary>
        /// Takes the maximizing of the layout shown back, as VPMIN: the viewport's frame and view as they were before VPMAX, what
        /// was done to the view while it was maximized (a zoom, a pan) given up, the viewports VPMAX turned off turned on again,
        /// and the paper worked on. One undo step.
        /// </summary>
        /// <returns>What came of it; refused where no viewport is maximized.</returns>
        internal CadEditResult MinimizeViewport()
        {
            CadSpace space = CurrentSpace;
            CadMaximizedViewport state = MaximizedOf(space);

            if (state == null)
            {
                return CadEditResult.Refuse("No viewport is currently maximized.");
            }

            var records = new List<CadEditRecord>
            {
                new CadEditRecord(
                    CadEditRecordKind.PropertyChanged,
                    state.Handle,
                    "Maximized",
                    CadCommands.Values(
                        state.MaximizedCenter.X,
                        state.MaximizedCenter.Y,
                        state.MaximizedWidth,
                        state.MaximizedHeight
                    ),
                    CadCommands.Values(state.Center.X, state.Center.Y, state.Width, state.Height)
                ),
            };

            foreach (string handle in state.TurnedOff)
            {
                if (FindEntity(handle) is DxfViewport other && !other.IsErased)
                {
                    records.Add(new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "On", "No", "Yes"));
                }
            }

            CadEditResult result = ApplyEdit("VPMIN", records, s =>
            {
                var target = (DxfViewport)s.Document.FindEntity(state.Handle);
                target.CenterPoint = state.Center;
                target.Width = state.Width;
                target.Height = state.Height;
                target.ViewCenter = state.ViewCenter;
                target.ViewHeight = state.ViewHeight;

                foreach (string handle in state.TurnedOff)
                {
                    if (s.Document.FindEntity(handle) is DxfViewport other && !other.IsErased)
                    {
                        other.On = true;
                    }
                }
            });

            if (result.Succeeded)
            {
                space.Maximized = null;
            }

            return result;
        }
    }
}

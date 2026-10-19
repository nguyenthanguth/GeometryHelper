using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Viewer.Views
{
    /// <summary>
    /// The overlays of all the views as one: what is told to it, the dimensions, the findings, the rotation centre, the snap under the cursor, the
    /// work plane and the clip planes, is told to each view's overlay, so that what is drawn over the scene shows in every view. A view that comes
    /// later is told all of it when it is added.
    /// </summary>
    public sealed class OverlayGroup
    {
        private readonly List<OverlayView> _overlays = new List<OverlayView>();
        private List<DimensionDrawing> _drawings = new List<DimensionDrawing>();
        private DiagnosticOverlay _diagnostics;
        private DiagnosticOverlay _lens;
        private Vec3? _marker;
        private Vec3? _centre;
        private Vec3? _snapPoint;
        private SnapKinds _snapKind;
        private WorkPlane _workPlane = WorkPlane.Global;
        private ClipSet _clips = ClipSet.Empty;
        private int _clipSelected = -1;

        /// <summary>
        /// Adds the overlay of a view, which is told everything that is drawn now.
        /// </summary>
        /// <param name="overlay">The overlay.</param>
        public void Add(OverlayView overlay)
        {
            _overlays.Add(overlay);
            overlay.SetDrawings(_drawings);
            overlay.SetDiagnostics(_diagnostics);
            overlay.SetLens(_lens);
            overlay.SetMarker(_marker);
            overlay.SetRotationCentre(_centre);
            overlay.SetWorkPlane(_workPlane);
            overlay.SetClipPlanes(_clips, _clipSelected);
            overlay.SetSnap(_snapPoint, _snapKind);
        }

        /// <summary>
        /// Stops telling the overlay of a view, which is not shown, what is drawn.
        /// </summary>
        /// <param name="overlay">The overlay.</param>
        public void Remove(OverlayView overlay) => _overlays.Remove(overlay);

        /// <summary>
        /// Shows the dimensions in every view, replacing the ones before; none clears them.
        /// </summary>
        public void SetDrawings(IEnumerable<DimensionDrawing> drawings)
        {
            _drawings = drawings.ToList();

            foreach (OverlayView overlay in _overlays)
            {
                overlay.SetDrawings(_drawings);
            }
        }

        /// <summary>
        /// Shows what the diagnostics found in every view, or takes it away.
        /// </summary>
        public void SetDiagnostics(DiagnosticOverlay overlay)
        {
            _diagnostics = overlay;

            foreach (OverlayView view in _overlays)
            {
                view.SetDiagnostics(overlay);
            }
        }

        /// <summary>
        /// Shows the gap magnifier in every view, or takes it away.
        /// </summary>
        public void SetLens(DiagnosticOverlay lens)
        {
            _lens = lens;

            foreach (OverlayView view in _overlays)
            {
                view.SetLens(lens);
            }
        }

        /// <summary>
        /// Marks the finding that was clicked in every view, or takes the mark away.
        /// </summary>
        public void SetMarker(Vec3? at)
        {
            _marker = at;

            foreach (OverlayView view in _overlays)
            {
                view.SetMarker(at);
            }
        }

        /// <summary>
        /// Shows the rotation centre in every view, or takes it away.
        /// </summary>
        public void SetRotationCentre(Vec3? centre)
        {
            _centre = centre;

            foreach (OverlayView view in _overlays)
            {
                view.SetRotationCentre(centre);
            }
        }

        /// <summary>
        /// Shows the marker of the snap under the cursor in every view, or takes it away.
        /// </summary>
        public void SetSnap(Vec3? point, SnapKinds kind)
        {
            _snapPoint = point;
            _snapKind = kind;

            foreach (OverlayView view in _overlays)
            {
                view.SetSnap(point, kind);
            }
        }

        /// <summary>
        /// Shows the axes of the work plane in every view.
        /// </summary>
        public void SetWorkPlane(WorkPlane plane)
        {
            _workPlane = plane ?? WorkPlane.Global;

            foreach (OverlayView view in _overlays)
            {
                view.SetWorkPlane(_workPlane);
            }
        }

        /// <summary>
        /// Shows the clip planes in every view.
        /// </summary>
        public void SetClipPlanes(ClipSet clips, int selected)
        {
            _clips = clips ?? ClipSet.Empty;
            _clipSelected = selected;

            foreach (OverlayView view in _overlays)
            {
                view.SetClipPlanes(_clips, _clipSelected);
            }
        }
    }
}

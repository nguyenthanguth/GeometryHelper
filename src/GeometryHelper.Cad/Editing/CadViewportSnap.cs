using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// Object snaps to the model space a viewport shows, as AutoCAD gives them: from the paper, a point of a LINE or a
    /// dimension snaps to an endpoint, a centre or a nearest point of the model geometry seen through a viewport, the point
    /// taken where the viewport shows it; through a viewport worked in, the layers frozen in it are left out of the snaps.
    /// </summary>
    internal static class CadViewportSnap
    {
        /// <summary>The layers frozen in a viewport, whose objects it does not show.</summary>
        internal static ISet<string> FrozenIn(DxfViewport viewport)
        {
            if (viewport == null)
            {
                return null;
            }

            HashSet<string> names = null;

            foreach (DxfLayerTableRecord layer in viewport.GetFrozenLayers())
            {
                names = names ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                names.Add(layer.Name);
            }

            return names;
        }

        /// <summary>
        /// The object snap of model space seen through the viewports of the layout shown, where it lies nearer the cursor than
        /// the snap of the paper's own objects; the paper's where it does not or none is seen. Model space is snapped in model
        /// units, the aperture in the viewport's scale, and the point taken to the paper by the viewport's view, twist and
        /// scale. The result names no object: the object is of model space, the point is the paper's.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="space">The layout shown.</param>
        /// <param name="snap">The snaps.</param>
        /// <param name="own">The snap of the paper's own objects, as the snap gave it.</param>
        /// <param name="cursor">The cursor, on the paper.</param>
        /// <param name="aperture">The aperture, in paper units.</param>
        /// <param name="basePoint">The point the rubber band runs from, on the paper; null for none.</param>
        /// <param name="drawnFrom">The point perpendicular and tangent are drawn from, on the paper; null for none.</param>
        /// <returns>The snap to take.</returns>
        internal static CadSnapResult Through(
            CadDocument document,
            CadSpace space,
            ObjectSnap snap,
            CadSnapResult own,
            GeoPoint2 cursor,
            double aperture,
            GeoPoint2? basePoint,
            GeoPoint2? drawnFrom
        )
        {
            if (space == null || space.IsModel || !(aperture > 0.0))
            {
                return own;
            }

            double best = own != null && IsObject(own.Kind) ? (own.Point - cursor).Length : double.MaxValue;
            CadSnapResult found = null;
            PickIndex model = null;

            foreach (DxfViewport viewport in document.EnterableViewports)
            {
                if (!viewport.On || !Reaches(viewport, cursor, aperture))
                {
                    continue;
                }

                model = model ?? document.GetPickIndex(document.ModelSpace);
                double scale = viewport.Height / viewport.ViewHeight;
                snap.HiddenLayers = FrozenIn(viewport);
                CadSnapResult seen;

                try
                {
                    seen = snap.SnapToObjects(
                        model,
                        CadViewports.ToModel(viewport, cursor),
                        aperture / scale,
                        basePoint.HasValue ? CadViewports.ToModel(viewport, basePoint.Value) : (GeoPoint2?)null,
                        drawnFrom.HasValue ? CadViewports.ToModel(viewport, drawnFrom.Value) : (GeoPoint2?)null
                    );
                }
                finally
                {
                    snap.HiddenLayers = null;
                }

                // An object offered as the first of an extended apparent intersection is one of model space: not taken here.
                if (seen == null || seen.Source != null || !IsObject(seen.Kind))
                {
                    continue;
                }

                GeoPoint2 at = CadViewports.ToPaper(viewport, seen.Point);
                double reach = (at - cursor).Length;

                if (reach < best && Reaches(viewport, at, Tolerance.Default.EqualPoint))
                {
                    best = reach;
                    found = new CadSnapResult(seen.Kind, at, null);
                }
            }

            return found ?? own;
        }

        private static bool IsObject(CadSnapKind kind) =>
            kind != CadSnapKind.None
            && kind != CadSnapKind.Grid
            && kind != CadSnapKind.Ortho
            && kind != CadSnapKind.Polar
            && kind != CadSnapKind.Tracking;

        /// <summary>Whether a point of the paper lies in the frame of a viewport, or in its clip outline, or within a reach of its edge.</summary>
        private static bool Reaches(DxfViewport viewport, GeoPoint2 point, double reach)
        {
            DisplayClip2 clip = CadViewportClips.OutlineOf(viewport);

            if (clip != null)
            {
                return clip.Contains(point.X, point.Y) || clip.DistanceToEdge(point.X, point.Y) <= reach;
            }

            GeoPoint3 middle = viewport.CenterPoint;
            return Math.Abs(point.X - middle.X) <= viewport.Width / 2.0 + reach
                && Math.Abs(point.Y - middle.Y) <= viewport.Height / 2.0 + reach;
        }
    }
}

using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The grips of a spline and the edits of their hover menu: stretch a point, add one, take one out.</summary>
    public static partial class CadGrips
    {
        /// <summary>How many points a spline's grips stand on: its fit points where it has them, else its control points.</summary>
        internal static int SplineGripCount(DxfSpline spline) =>
            spline.HasFitData ? spline.NumFitPoints : spline.NumControlPoints;

        private static GeoPoint3 SplinePoint(DxfSpline spline, int index) =>
            spline.HasFitData ? spline.GetFitPointAt(index) : spline.GetControlPointAt(index);

        /// <summary>Whether a spline has more points than it can do without: more than two fit points, or more control points than its degree.</summary>
        internal static bool SplineCanLoseAPoint(DxfSpline spline) =>
            spline.HasFitData ? spline.NumFitPoints > 2 : spline.NumControlPoints > spline.Degree + 1;

        /// <summary>
        /// The grips of a spline, as AutoCAD 2021's GetGripPoints gives them (probed in its console): one on each fit point
        /// where it has fit data, else one on each control point, in order, each a <see cref="CadGripKind.Vertex"/>. The small
        /// grip AutoCAD puts by the first point to switch between the two is not given.
        /// </summary>
        private static void SplineGrips(DxfSpline spline, Action<GeoPoint2> add)
        {
            int n = spline.IsNull ? 0 : SplineGripCount(spline);

            for (int i = 0; i < n; i++)
            {
                GeoPoint3 at = SplinePoint(spline, i);
                add(new GeoPoint2(at.X, at.Y));
            }
        }

        /// <summary>
        /// Takes a point of a spline to a point of the plan, the height it had kept: a fit point moved makes the spline fit
        /// through it, a control point moved bends the curve with it. Also what the Stretch of the menu does to a spline.
        /// </summary>
        private static bool SplineStretchAt(DxfEntity entity, int index, GeoPoint2 to)
        {
            if (!(entity is DxfSpline spline) || index < 0 || index >= SplineGripCount(spline) || !to.IsValid)
            {
                return false;
            }

            GeoPoint3 was = SplinePoint(spline, index);
            var moved = new GeoPoint3(to.X, to.Y, was.Z);

            try
            {
                if (spline.HasFitData)
                {
                    spline.SetFitPointAt(index, moved);
                }
                else
                {
                    spline.SetControlPointAt(index, moved);
                }

                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Puts a point into a spline at a point of the plan, as <see cref="AddVertex"/> says; also what its prompt previews on
        /// a copy. A fit point goes after the fit point of the grip (after the last it extends the end); a control point goes
        /// with the knot at the point of the curve nearest the point, which must be inside the curve.
        /// </summary>
        /// <returns>Whether it was put in.</returns>
        internal static bool SplineAddAt(DxfEntity entity, int gripIndex, GeoPoint2 point)
        {
            if (
                !(entity is DxfSpline spline)
                || gripIndex < 0
                || gripIndex >= SplineGripCount(spline)
                || !point.IsValid
            )
            {
                return false;
            }

            var at = new GeoPoint3(point.X, point.Y, SplinePoint(spline, gripIndex).Z);

            try
            {
                if (spline.HasFitData)
                {
                    spline.InsertFitPointAt(gripIndex + 1, at);
                    return true;
                }

                double knot = spline.GetParameterAtPoint(spline.GetClosestPointTo(at, false));

                if (!(knot > spline.StartParam && knot < spline.EndParam))
                {
                    return false;
                }

                spline.InsertControlPointAt(knot, at, 1.0);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static CadEditResult AddSplineVertex(
            CadDocument document,
            string handle,
            int gripIndex,
            GeoPoint2 point
        )
        {
            var spline = (DxfSpline)document.FindEntity(handle);

            if (CadLayerEditor.IsLocked(document.Database, spline.Layer))
            {
                return CadEditResult.Refuse("The object is on a locked layer.");
            }

            if (gripIndex < 0 || gripIndex >= SplineGripCount(spline))
            {
                return CadEditResult.Refuse("The spline has no grip " + gripIndex + ".");
            }

            if (!SplineAddAt(spline.Clone(), gripIndex, point))
            {
                return CadEditResult.Refuse(
                    spline.HasFitData
                        ? "The fit point cannot be put there."
                        : "The point is not inside the spline's curve."
                );
            }

            GeoPoint3 grip = SplinePoint(spline, gripIndex);
            return Reshape(
                document,
                handle,
                spline.HasFitData ? "Add fit point" : "Add vertex",
                CadCommands.Values(grip.X, grip.Y),
                CadCommands.Values(point.X, point.Y),
                () => SplineAddAt(document.FindEntity(handle), gripIndex, point)
            );
        }

        private static CadEditResult RemoveSplineVertex(CadDocument document, string handle, int gripIndex)
        {
            var spline = (DxfSpline)document.FindEntity(handle);

            if (CadLayerEditor.IsLocked(document.Database, spline.Layer))
            {
                return CadEditResult.Refuse("The object is on a locked layer.");
            }

            if (gripIndex < 0 || gripIndex >= SplineGripCount(spline))
            {
                return CadEditResult.Refuse("The spline has no grip " + gripIndex + ".");
            }

            if (!SplineCanLoseAPoint(spline))
            {
                return CadEditResult.Refuse(
                    spline.HasFitData
                        ? "A spline keeps two fit points at least."
                        : "A spline keeps as many control points as its degree and one."
                );
            }

            GeoPoint3 grip = SplinePoint(spline, gripIndex);
            return Reshape(
                document,
                handle,
                spline.HasFitData ? "Remove fit point" : "Remove vertex",
                CadCommands.Values(grip.X, grip.Y),
                null,
                () =>
                {
                    var s = (DxfSpline)document.FindEntity(handle);

                    if (s.HasFitData)
                    {
                        s.RemoveFitPointAt(gripIndex);
                    }
                    else
                    {
                        s.RemoveControlPointAt(gripIndex);
                    }

                    return true;
                }
            );
        }
    }
}

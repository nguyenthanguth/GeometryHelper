using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>The edits of the hover menus of a line's and an arc's grips: Lengthen at an end, Radius in the middle of an arc.</summary>
    public static partial class CadGrips
    {
        /// <summary>
        /// Lengthens or shortens a line or an arc at one end, as the Lengthen item of the menu of an end grip does, one undo
        /// step. A line's end goes to where the point falls along the line's own way, the other end and the way kept: a line
        /// from (0, 0) to (100, 0) lengthened at its end by (150, 40) runs to (150, 0). An arc's end goes round the arc to the
        /// angle the point has from the centre, the centre, the radius and the other end kept: an arc of radius 50 from 0 to 90
        /// degrees lengthened at its end by (-10, 30) ends at the angle of that point, 108.4 degrees.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The handle of the line or the arc.</param>
        /// <param name="gripIndex">The index of the grip at the start (0) or the end (1) of the line or the arc, as <see cref="Of"/> gives it.</param>
        /// <param name="point">The point the end is taken to, in drawing units.</param>
        /// <returns>What came of it: refused for an object on a locked layer, no line or arc, a grip that is not an end, or a point that would leave the line no length or the arc no sweep.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult Lengthen(CadDocument document, string handle, int gripIndex, GeoPoint2 point)
        {
            CadEditResult refused = EndGrip(document, handle, gripIndex, out DxfEntity entity, out CadGrip grip);

            if (refused != null)
            {
                return refused;
            }

            // Tried on a copy first, so that a point which leaves the line or the arc nothing is refused with the drawing as it was.
            if (!LengthenAt(CloneOf(entity), gripIndex, point))
            {
                return CadEditResult.Refuse(
                    entity is DxfLine
                        ? "The point would leave the line no length."
                        : "The point would leave the arc no sweep."
                );
            }

            return Reshape(
                document,
                handle,
                "Lengthen",
                CadCommands.Values(grip.Point.X, grip.Point.Y),
                CadCommands.Values(point.X, point.Y),
                () => LengthenAt(document.FindEntity(handle), gripIndex, point)
            );
        }

        /// <summary>
        /// Changes the radius of an arc, as the Radius item of the menu of its middle grip does, one undo step: the radius is
        /// the distance of the point from the centre, the centre and the two angles kept, so that the ends move along their
        /// rays. An arc of radius 50 about (0, 0) given the point (0, 30) has radius 30.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The handle of the arc.</param>
        /// <param name="gripIndex">The index of the grip in the middle of the arc, as <see cref="Of"/> gives it.</param>
        /// <param name="point">A point of the new arc, in drawing units.</param>
        /// <returns>What came of it: refused for an object on a locked layer, no arc, a grip that is not its middle, or a point on the centre.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult ChangeRadius(CadDocument document, string handle, int gripIndex, GeoPoint2 point)
        {
            CadEditResult refused = EndGrip(document, handle, gripIndex, out DxfEntity entity, out CadGrip grip);

            if (refused != null)
            {
                return refused;
            }

            if (!(entity is DxfArc) || grip.Kind != CadGripKind.Middle)
            {
                return CadEditResult.Refuse("Only the middle of an arc has a radius to change.");
            }

            if (!RadiusAt(CloneOf(entity), point))
            {
                return CadEditResult.Refuse("An arc needs a radius above nought.");
            }

            return Reshape(
                document,
                handle,
                "Radius",
                CadCommands.Values(grip.Point.X, grip.Point.Y),
                CadCommands.Values(point.X, point.Y),
                () => RadiusAt(document.FindEntity(handle), point)
            );
        }

        /// <summary>Finds the line or arc and the grip of an edit, refusing what the edits cannot be made on.</summary>
        /// <returns>Null where the edit may go on, else the refusal.</returns>
        private static CadEditResult EndGrip(
            CadDocument document,
            string handle,
            int gripIndex,
            out DxfEntity entity,
            out CadGrip grip
        )
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            entity = document.FindEntity(handle);
            grip = null;

            if (!(entity is DxfLine) && !(entity is DxfArc))
            {
                return CadEditResult.Refuse("The drawing has no line or arc " + handle + ".");
            }

            if (CadLayerEditor.IsLocked(document.Database, entity.Layer))
            {
                return CadEditResult.Refuse("The object is on a locked layer.");
            }

            IReadOnlyList<CadGrip> grips = Of(entity);

            if (gripIndex < 0 || gripIndex >= grips.Count)
            {
                return CadEditResult.Refuse("The " + CadDocument.KindName(entity) + " has no grip " + gripIndex + ".");
            }

            grip = grips[gripIndex];
            return null;
        }

        /// <summary>A copy of an entity, for trying an edit on before it is made.</summary>
        private static DxfEntity CloneOf(DxfEntity entity) => entity.Clone();

        /// <summary>
        /// Lengthens a line or an arc at an end to a point of the world, as <see cref="Lengthen"/> says; also what its prompt
        /// previews on a copy.
        /// </summary>
        /// <returns>Whether the end was moved: not for a grip that is not an end, or a point that leaves the line no length or the arc no sweep.</returns>
        internal static bool LengthenAt(DxfEntity entity, int gripIndex, GeoPoint2 point)
        {
            if ((gripIndex != 0 && gripIndex != 1) || !point.IsValid)
            {
                return false;
            }

            double least = Tolerance.Default.EqualPoint;

            if (entity is DxfLine line)
            {
                // The moving end and the one kept, in 3D, the way run in the plan: the point falls on the line's own way.
                GeoPoint3 kept = gripIndex == 0 ? line.EndPoint : line.StartPoint;
                GeoPoint3 moving = gripIndex == 0 ? line.StartPoint : line.EndPoint;
                var way = new GeoVector2(moving.X - kept.X, moving.Y - kept.Y);
                double length = way.Length;

                if (length <= least)
                {
                    return false;
                }

                double along = ((point.X - kept.X) * way.X + (point.Y - kept.Y) * way.Y) / length;

                if (along <= least)
                {
                    return false;
                }

                double k = along / length;
                var to = new GeoPoint2(kept.X + way.X * k, kept.Y + way.Y * k);
                line.StartPoint = gripIndex == 0 ? new GeoPoint3(to.X, to.Y, kept.Z + (moving.Z - kept.Z) * k) : kept;
                line.EndPoint = gripIndex == 0 ? kept : new GeoPoint3(to.X, to.Y, kept.Z + (moving.Z - kept.Z) * k);
                return true;
            }

            if (entity is DxfArc arc && IsPlan(arc.Normal))
            {
                bool below = arc.Normal.Z < 0.0;
                GeoPoint2 centre = Own(new GeoPoint2(arc.Center.X, arc.Center.Y), below);
                GeoVector2 off = Own(point, below) - centre;

                if (off.Length <= least)
                {
                    return false;
                }

                double angle = Normalize(Math.Atan2(off.Y, off.X));
                double other = gripIndex == 0 ? arc.EndAngle : arc.StartAngle;
                double apart = Math.Abs(Normalize(angle - other));

                if (apart <= 1E-9 || apart >= 2.0 * Math.PI - 1E-9)
                {
                    return false;
                }

                if (gripIndex == 0)
                {
                    arc.StartAngle = angle;
                }
                else
                {
                    arc.EndAngle = angle;
                }

                return true;
            }

            return false;
        }

        /// <summary>Sets the radius of an arc to the distance of a point of the world from its centre, as <see cref="ChangeRadius"/> says; also what its prompt previews on a copy.</summary>
        /// <returns>Whether the radius was set: not for a point on the centre.</returns>
        internal static bool RadiusAt(DxfEntity entity, GeoPoint2 point)
        {
            if (!(entity is DxfArc arc) || !IsPlan(arc.Normal) || !point.IsValid)
            {
                return false;
            }

            double radius = (point - new GeoPoint2(arc.Center.X, arc.Center.Y)).Length;

            if (radius <= Tolerance.Default.EqualPoint)
            {
                return false;
            }

            arc.Radius = radius;
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>The edits of the hover menu of a hatch's grip: its pattern's scale, angle and origin.</summary>
    public static partial class CadGrips
    {
        /// <summary>The middle of the box a hatch's loops fill, in the plan: where its grip stands; null for a hatch with no loop.</summary>
        private static GeoPoint2? HatchCentre(DxfHatch hatch)
        {
            if (hatch.NumberOfLoops == 0)
            {
                return null;
            }

            try
            {
                GeoAabb3 box = hatch.GeometricExtents;
                return box.IsEmpty ? (GeoPoint2?)null : new GeoPoint2(box.Center.X, box.Center.Y);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets what the pattern of a hatch lets its menu change: its scale (a pattern with lines of its own and a scale, not a
        /// solid fill, a gradient or a pattern made by the user, which has a spacing), its angle (not a solid fill) and its origin
        /// (a pattern whose lines are laid from one, see HATCHSETORIGIN).
        /// </summary>
        internal static void HatchMenuAllows(DxfHatch hatch, out bool scale, out bool angle, out bool origin)
        {
            bool filled = hatch.IsSolidFill || hatch.IsGradient;
            scale = !filled && hatch.PatternType != DxfHatchPatternType.UserDefined;
            angle = !filled;
            origin = CadHatchOrigin.HasLines(hatch);
        }

        /// <summary>
        /// Sets the scale of a hatch's pattern, as the Hatch Scale item of the menu of its grip does, one undo step: the pattern
        /// is laid from the same origin as before.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The handle of the hatch.</param>
        /// <param name="scale">The new scale, above nought.</param>
        /// <returns>What came of it: refused for an object on a locked layer, no hatch, a scale that is not above nought, or a hatch whose pattern has no scale (a solid fill, a gradient or a pattern made by the user).</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult ChangeHatchScale(CadDocument document, string handle, double scale)
        {
            CadEditResult refused = HatchGrip(document, handle, out DxfHatch hatch);

            if (refused != null)
            {
                return refused;
            }

            HatchMenuAllows(hatch, out bool allowed, out _, out _);

            if (!allowed)
            {
                return CadEditResult.Refuse("A solid fill and a pattern made by the user have no scale.");
            }

            if (!(scale > 0.0) || double.IsInfinity(scale))
            {
                return CadEditResult.Refuse("A pattern's scale has to be a number above nought.");
            }

            return Reshape(
                document,
                handle,
                "Hatch scale",
                CadCommands.Values(hatch.PatternScale),
                CadCommands.Values(scale),
                () => HatchScaleAt(document.FindEntity(handle), scale)
            );
        }

        /// <summary>
        /// Sets the angle of a hatch's pattern, as the Hatch Angle item of the menu of its grip does, one undo step: the pattern
        /// is laid from the same origin as before.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The handle of the hatch.</param>
        /// <param name="angle">The new angle, in radians counter-clockwise.</param>
        /// <returns>What came of it: refused for an object on a locked layer, no hatch, an angle that is not a number, or a solid fill.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult ChangeHatchAngle(CadDocument document, string handle, double angle)
        {
            CadEditResult refused = HatchGrip(document, handle, out DxfHatch hatch);

            if (refused != null)
            {
                return refused;
            }

            HatchMenuAllows(hatch, out _, out bool allowed, out _);

            if (!allowed)
            {
                return CadEditResult.Refuse("A solid fill has no angle.");
            }

            if (double.IsNaN(angle) || double.IsInfinity(angle))
            {
                return CadEditResult.Refuse("An angle has to be a number.");
            }

            return Reshape(
                document,
                handle,
                "Hatch angle",
                CadCommands.Values(hatch.PatternAngle),
                CadCommands.Values(angle),
                () => HatchAngleAt(document.FindEntity(handle), angle)
            );
        }

        /// <summary>
        /// Lays a hatch's pattern from a point, as the Origin Point item of the menu of its grip does, one undo step: each
        /// line of the pattern moves by what the origin moves, as HATCHSETORIGIN moves them.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The handle of the hatch.</param>
        /// <param name="origin">The new origin, in drawing units.</param>
        /// <returns>What came of it: refused for an object on a locked layer, no hatch, or a hatch with no pattern lines of its own (a solid fill, a gradient or a pattern made by the user).</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult SetHatchOrigin(CadDocument document, string handle, GeoPoint2 origin)
        {
            CadEditResult refused = HatchGrip(document, handle, out DxfHatch hatch);

            if (refused != null)
            {
                return refused;
            }

            HatchMenuAllows(hatch, out _, out _, out bool allowed);

            if (!allowed)
            {
                return CadEditResult.Refuse("This hatch has no pattern lines to lay from an origin.");
            }

            GeoPoint2 was = CadHatchOrigin.Of(hatch);
            return Reshape(
                document,
                handle,
                "Hatch origin",
                CadCommands.Values(was.X, was.Y),
                CadCommands.Values(origin.X, origin.Y),
                () => HatchOriginAt(document.FindEntity(handle), origin)
            );
        }

        /// <summary>Finds the hatch of an edit, refusing what the edits cannot be made on.</summary>
        /// <returns>Null where the edit may go on, else the refusal.</returns>
        private static CadEditResult HatchGrip(CadDocument document, string handle, out DxfHatch hatch)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            hatch = document.FindEntity(handle) as DxfHatch;

            if (hatch == null)
            {
                return CadEditResult.Refuse("The drawing has no hatch " + handle + ".");
            }

            return CadLayerEditor.IsLocked(document.Database, hatch.Layer)
                ? CadEditResult.Refuse("The object is on a locked layer.")
                : null;
        }

        /// <summary>Sets a hatch's pattern scale, its origin kept; also what the prompt of Hatch Scale previews on a copy.</summary>
        internal static bool HatchScaleAt(DxfEntity entity, double scale)
        {
            if (!(entity is DxfHatch hatch) || !(scale > 0.0) || double.IsInfinity(scale))
            {
                return false;
            }

            HatchMenuAllows(hatch, out bool allowed, out _, out _);

            if (!allowed)
            {
                return false;
            }

            CadPropertyEditor.KeepOrigin(hatch, () => hatch.PatternScale = scale);
            return true;
        }

        /// <summary>Sets a hatch's pattern angle, its origin kept; also what the prompt of Hatch Angle previews on a copy.</summary>
        internal static bool HatchAngleAt(DxfEntity entity, double angle)
        {
            if (!(entity is DxfHatch hatch) || double.IsNaN(angle) || double.IsInfinity(angle))
            {
                return false;
            }

            HatchMenuAllows(hatch, out _, out bool allowed, out _);

            if (!allowed)
            {
                return false;
            }

            CadPropertyEditor.KeepOrigin(hatch, () => hatch.PatternAngle = angle);
            return true;
        }

        /// <summary>Lays a hatch's pattern from a point of the world; also what the prompt of Origin Point previews on a copy.</summary>
        internal static bool HatchOriginAt(DxfEntity entity, GeoPoint2 point)
        {
            if (!(entity is DxfHatch hatch) || !point.IsValid || !IsPlan(hatch.Normal))
            {
                return false;
            }

            return CadHatchOrigin.MoveTo(hatch, Own(point, hatch.Normal.Z < 0.0));
        }
    }
}

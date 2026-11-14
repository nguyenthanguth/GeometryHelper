using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The origin a hatch's pattern is laid from, as HATCHSETORIGIN sets it. A hatch keeps no origin of its own: it is in the base
    /// points of its pattern lines, each one the base point its pattern gives, scaled and turned, moved by the origin. The origin
    /// is worked back from them where the pattern is one of the presenter's own, and taken as the origin of the plane where it
    /// is not. A solid fill and a gradient have no lines, and lines made by the user follow the hatch's spacing alone
    /// (<see cref="DxfHatch.SetPatternDefinitions"/> refuses them): neither takes an origin.
    /// </summary>
    internal static class CadHatchOrigin
    {
        /// <summary>Whether a hatch has pattern lines of its own to lay from an origin.</summary>
        internal static bool HasLines(DxfHatch hatch) =>
            !hatch.IsSolidFill
            && !hatch.IsGradient
            && hatch.PatternType != DxfHatchPatternType.UserDefined
            && hatch.NumberOfPatternDefinitions > 0;

        /// <summary>
        /// Gets the origin a hatch's pattern is laid from: the base point of its first line less the one the presenter's pattern
        /// of that name gives it at the hatch's angle and scale (its offsets over the pattern's, for an annotative hatch whose
        /// lines are scaled again); (0, 0) for a pattern the presenter does not have, and for a hatch with no lines of its own.
        /// ANSI32 at angle 30 and scale 2 with its lines moved by (10, 5) has its first line based at (10, 5): the origin is
        /// (10, 5).
        /// </summary>
        /// <param name="hatch">The hatch.</param>
        /// <returns>The origin, in the plane of the hatch.</returns>
        internal static GeoPoint2 Of(DxfHatch hatch)
        {
            if (!HasLines(hatch))
            {
                return new GeoPoint2(0.0, 0.0);
            }

            CadHatchPattern pattern = CadPatternFile.FindBuiltIn(hatch.PatternName);

            if (pattern == null)
            {
                return new GeoPoint2(0.0, 0.0);
            }

            IReadOnlyList<DxfPatternDefinition> laid = pattern.Place(
                hatch.PatternAngle * 180.0 / Math.PI,
                hatch.PatternScale,
                0.0,
                0.0
            );

            if (laid.Count != hatch.NumberOfPatternDefinitions)
            {
                return new GeoPoint2(0.0, 0.0);
            }

            // An annotative hatch has its lines scaled again by the annotation scale: the first line's step shows by how much.
            DxfPatternDefinition first = hatch.GetPatternDefinitionAt(0);
            double had = Math.Sqrt(first.OffsetX * first.OffsetX + first.OffsetY * first.OffsetY);
            double gives = Math.Sqrt(laid[0].OffsetX * laid[0].OffsetX + laid[0].OffsetY * laid[0].OffsetY);
            double factor = gives > 0.0 && had > 0.0 ? had / gives : 1.0;
            return new GeoPoint2(first.BaseX - laid[0].BaseX * factor, first.BaseY - laid[0].BaseY * factor);
        }

        /// <summary>Lays a hatch's pattern from another origin, each line's base point moved by what the origin moves.</summary>
        /// <param name="hatch">The hatch.</param>
        /// <param name="origin">The new origin, in the plane of the hatch.</param>
        /// <returns>False, nothing moved, for a hatch with no lines of its own (<see cref="HasLines"/>).</returns>
        internal static bool MoveTo(DxfHatch hatch, GeoPoint2 origin)
        {
            if (!HasLines(hatch))
            {
                return false;
            }

            GeoPoint2 was = Of(hatch);
            double dx = origin.X - was.X;
            double dy = origin.Y - was.Y;
            var lines = new List<DxfPatternDefinition>();

            for (int i = 0; i < hatch.NumberOfPatternDefinitions; i++)
            {
                DxfPatternDefinition line = hatch.GetPatternDefinitionAt(i);
                lines.Add(
                    new DxfPatternDefinition(
                        line.Angle,
                        line.BaseX + dx,
                        line.BaseY + dy,
                        line.OffsetX,
                        line.OffsetY,
                        line.GetDashes()
                    )
                );
            }

            hatch.SetPatternDefinitions(lines);
            return true;
        }
    }
}

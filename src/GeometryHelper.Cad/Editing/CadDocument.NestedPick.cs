using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The object inside a block reference that a point reaches, which LAYOFF and LAYFRZ take the layer of by their Block
    /// selection setting.
    /// </summary>
    public sealed partial class CadDocument
    {
        /// <summary>
        /// The object a pick on a block reference means at a nesting level, as AutoCAD 2021's LAYOFF and LAYFRZ take it: going
        /// down from the reference, at each block the object whose box, put where the references draw it, holds the point within
        /// the aperture, the smallest where several do and the first of the block on a tie (the way REFEDIT's nested pick takes
        /// one); a reference it finds is gone into, an object that is none ends the descent. Block nesting level Block takes the
        /// reference the object is in, the innermost; Entity the object itself; None the reference picked.
        /// </summary>
        /// <param name="top">The reference picked, in a space.</param>
        /// <param name="at">The point it was picked at, in the drawing.</param>
        /// <param name="aperture">How far from an object's box the point still reaches it.</param>
        /// <param name="entity">Whether the object itself is wanted rather than the reference it is in.</param>
        /// <returns>The object; <paramref name="top"/> where nothing inside it is reached, or the block is an external
        /// reference's or an array's, whose objects are not gone into.</returns>
        internal static DxfEntity FindNestedPick(DxfBlockReference top, GeoPoint2 at, double aperture, bool entity)
        {
            DxfBlockReference current = top;
            GeoTransform3 placing = top.BlockTransform;

            // The same eight levels as REFEDIT's list of nested references.
            for (int depth = 0; depth < 8; depth++)
            {
                DxfBlockTableRecord block = current.BlockTableRecord;

                if (block == null || block.IsFromExternalReference || current is DxfMInsertBlock)
                {
                    return current;
                }

                DxfEntity best = null;
                double least = double.MaxValue;

                foreach (DxfEntity candidate in Candidates(block, false))
                {
                    GeoAabb3 box;

                    try
                    {
                        box = candidate is DxfBlockReference reference
                            ? reference.GeometryExtentsBestFit(placing)
                            : candidate.GetTransformedCopy(placing).GeometricExtents;
                    }
                    catch (Exception exception)
                        when (exception is ArgumentException
                            || exception is InvalidOperationException
                            || exception is NotSupportedException
                        )
                    {
                        // An object that cannot be placed is not one the pick can reach.
                        continue;
                    }

                    if (
                        box.IsEmpty
                        || at.X < box.Min.X - aperture
                        || at.X > box.Max.X + aperture
                        || at.Y < box.Min.Y - aperture
                        || at.Y > box.Max.Y + aperture
                    )
                    {
                        continue;
                    }

                    double area = (box.Max.X - box.Min.X) * (box.Max.Y - box.Min.Y);

                    if (area < least)
                    {
                        least = area;
                        best = candidate;
                    }
                }

                if (best == null)
                {
                    return current;
                }

                if (!(best is DxfBlockReference inner))
                {
                    return entity ? best : current;
                }

                current = inner;
                placing = placing.Multiply(inner.BlockTransform);
            }

            return current;
        }
    }
}

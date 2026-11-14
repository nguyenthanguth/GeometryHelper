using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The clipping planes of a clipped block reference (XCLIP Clipdepth) laid on what its block holds.</summary>
    internal sealed partial class DisplayBuilder
    {
        /// <summary>
        /// Whether an entity of a block whose reference has front and back clipping planes is drawn, and what of it, as AutoCAD 2021
        /// shows it (PNGOUT of a block of lines at several heights): the entity is shown where it lies between the planes, planes
        /// included, and not at all where its box lies wholly above the front plane or below the back one. A line that crosses a
        /// plane is cut at it, and any other entity that does is drawn whole, since the display keeps no height to cut it by.
        /// </summary>
        /// <param name="slab">The planes, measured in the block's coordinates.</param>
        /// <param name="entity">The entity of the block.</param>
        /// <param name="shown">The entity to draw: itself, or a cut copy of a line.</param>
        /// <returns>false where nothing of the entity is shown.</returns>
        private static bool TrySlice(DxfSpatialFilter.Slab slab, DxfEntity entity, out DxfEntity shown)
        {
            shown = entity;
            GeoAabb3 box;

            try
            {
                box = entity.GeometricExtents;
            }
            catch (Exception)
            {
                // An entity that has no extents is shown as it is.
                return true;
            }

            if (box.IsEmpty)
            {
                return true;
            }

            double least = double.MaxValue;
            double most = double.MinValue;

            for (int corner = 0; corner < 8; corner++)
            {
                double h = slab.HeightAt(
                    (corner & 1) == 0 ? box.Min.X : box.Max.X,
                    (corner & 2) == 0 ? box.Min.Y : box.Max.Y,
                    (corner & 4) == 0 ? box.Min.Z : box.Max.Z
                );
                least = Math.Min(least, h);
                most = Math.Max(most, h);
            }

            double tolerance = Tolerance.Default.EqualPoint;
            double? front = slab.Front;
            double? back = slab.Back;

            if ((front.HasValue && least > front.Value + tolerance) || (back.HasValue && most < back.Value - tolerance))
            {
                return false;
            }

            bool crosses =
                (front.HasValue && most > front.Value + tolerance) || (back.HasValue && least < back.Value - tolerance);

            if (!crosses || !(entity is DxfLine line))
            {
                return true;
            }

            GeoPoint3 a = line.StartPoint;
            GeoPoint3 b = line.EndPoint;
            double ha = slab.HeightAt(a.X, a.Y, a.Z);
            double hb = slab.HeightAt(b.X, b.Y, b.Z);
            double rise = hb - ha;

            if (Math.Abs(rise) <= tolerance)
            {
                return true;
            }

            // The part of the line, from 0 at its start to 1 at its end, whose height lies between the planes.
            double t0 = 0.0;
            double t1 = 1.0;

            if (front.HasValue)
            {
                double t = (front.Value - ha) / rise;

                if (rise > 0.0)
                {
                    t1 = Math.Min(t1, t);
                }
                else
                {
                    t0 = Math.Max(t0, t);
                }
            }

            if (back.HasValue)
            {
                double t = (back.Value - ha) / rise;

                if (rise > 0.0)
                {
                    t0 = Math.Max(t0, t);
                }
                else
                {
                    t1 = Math.Min(t1, t);
                }
            }

            if (t0 > t1)
            {
                return false;
            }

            var cut = (DxfLine)line.Clone();
            cut.StartPoint = new GeoPoint3(a.X + t0 * (b.X - a.X), a.Y + t0 * (b.Y - a.Y), a.Z + t0 * (b.Z - a.Z));
            cut.EndPoint = new GeoPoint3(a.X + t1 * (b.X - a.X), a.Y + t1 * (b.Y - a.Y), a.Z + t1 * (b.Z - a.Z));
            shown = cut;
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The part of a block that a clip tilted out of the block's plane shows, at the height each entity stands at.</summary>
    internal sealed partial class DisplayBuilder
    {
        /// <summary>
        /// The rings a clip tilted out of the plan (XCLIP, made in a system AutoCAD tilted) cuts the planes of a block in, one for
        /// each height the block's entities stand at, as AutoCAD 2021 draws it (PNGOUT of a grid at height 50 under the boundary
        /// of a grid at height 0 that showed from y 73.09 to 130.83: the grid at 50 shows from y 44.23 to 101.96, where the lines
        /// along the boundary's normal meet that plane). An entity that lies flat is shown in the ring of its height; one that
        /// does not, in the ring of the block's own plane, as the display keeps no height to cut it by.
        /// </summary>
        private sealed class SectionLevels
        {
            private readonly DxfBlockReference _reference;
            private readonly DxfSpatialFilter _filter;
            private readonly Affine2 _placing;
            private readonly DisplayClip2 _outline;
            private readonly Dictionary<long, DisplayClip2> _rings = new Dictionary<long, DisplayClip2>();
            private long _key;
            private DisplayClipRegion _before;
            private bool _pushed;

            /// <param name="reference">The clipped reference.</param>
            /// <param name="filter">Its clip.</param>
            /// <param name="placing">The block's coordinates into drawing units.</param>
            /// <param name="outline">The boundary's ring in drawing units, shown where no section can be made.</param>
            public SectionLevels(
                DxfBlockReference reference,
                DxfSpatialFilter filter,
                Affine2 placing,
                DisplayClip2 outline
            )
            {
                _reference = reference;
                _filter = filter;
                _placing = placing;
                _outline = outline;
            }

            /// <summary>Puts the ring of an entity's height on the list, taking the one before off where the height is not the same.</summary>
            public void Enter(DxfEntity entity, DisplayList2 list)
            {
                double height = HeightOf(entity);
                long key = (long)Math.Round(height * 1E6);

                if (_pushed && key == _key)
                {
                    return;
                }

                Leave(list);

                if (!_rings.TryGetValue(key, out DisplayClip2 ring))
                {
                    IReadOnlyList<GeoPoint2> section = _reference.GetClipSectionAt(_filter, height);
                    ring = section == null ? _outline : WorldRing(section, _placing);
                    _rings.Add(key, ring);
                }

                _before = list.PushClip(ring);
                _pushed = true;
                _key = key;
            }

            /// <summary>Takes the ring off the list.</summary>
            public void Leave(DisplayList2 list)
            {
                if (_pushed)
                {
                    list.PopClip(_before);
                    _pushed = false;
                }
            }

            /// <summary>The height an entity lies flat at in the block's coordinates, 0 for one that does not.</summary>
            private static double HeightOf(DxfEntity entity)
            {
                try
                {
                    GeoAabb3 box = entity.GeometricExtents;

                    if (!box.IsEmpty && box.Max.Z - box.Min.Z <= Tolerance.Default.EqualPoint)
                    {
                        return 0.5 * (box.Min.Z + box.Max.Z);
                    }
                }
                catch (Exception)
                {
                    // An entity that has no extents is shown in the ring of the block's plane.
                }

                return 0.0;
            }
        }
    }
}

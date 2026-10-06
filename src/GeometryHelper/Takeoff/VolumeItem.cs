using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Takeoff
{
    /// <summary>
    /// A part to take off: its body, a name to know it by, and its priority, which says who keeps the material two parts
    /// share.
    /// </summary>
    /// <remarks>
    /// The lower priority keeps the overlap: give the columns 0, the walls 1, the beams 2 and the slabs 3, and a slab
    /// 200 thick on a beam 300 wide loses the 300 by 200 strip the beam runs through, where the beam loses nothing to
    /// the slab. Two parts of the same priority are decided by their order in the list: the earlier keeps it.
    /// <see cref="VolumeTakeoff.Run(IReadOnlyList{VolumeItem}, VolumeTakeoffOptions)"/> only reads the item, and its
    /// body is never cut.
    /// </remarks>
    public sealed class VolumeItem
    {
        /// <summary>
        /// Initializes the item.
        /// </summary>
        /// <param name="solid">The body of the part; its openings are honoured, so only its material is counted.</param>
        /// <param name="name">A name to know the part by, such as its kind or its id; null if there is none.</param>
        /// <param name="priority">
        /// Who keeps the material this part shares with another: the lower priority keeps it. Any number will do,
        /// negative ones too.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        public VolumeItem(GeoSolid3 solid, string name, int priority)
        {
            Solid = solid ?? throw new ArgumentNullException(nameof(solid));
            Name = name;
            Priority = priority;
        }

        /// <summary>
        /// Gets the body of the part.
        /// </summary>
        public GeoSolid3 Solid { get; }

        /// <summary>
        /// Gets the name the part is known by; null if it was given none.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets who keeps the material this part shares with another: the lower priority keeps it, and of two the same,
        /// the item earlier in the list.
        /// </summary>
        public int Priority { get; }
    }
}

using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Takeoff
{
    /// <summary>
    /// A part to take off: its body, a name to know it by, its priority, which says who keeps the material two parts
    /// share, and an id, which says it between two of the same priority.
    /// </summary>
    /// <remarks>
    /// The higher priority keeps the overlap and cuts it out of the lower: give the columns 3, the walls 2, the beams 1
    /// and the slabs 0, and a slab 200 thick on a beam 300 wide loses the 300 by 200 strip the beam runs through, where
    /// the beam loses nothing to the slab. Of two parts of the same priority, one with an id keeps it from one without,
    /// the larger id from the smaller, and otherwise the earlier in the list. A Tekla part's <c>Identifier.ID</c> given
    /// as the id makes such ties go as HDC WBS's WBSCalculator takes them, the larger id cutting: two walls of one
    /// priority crossing, ids 1 001 and 1 002, the second keeps what they share, wherever the two stand in the list.
    /// <see cref="VolumeTakeoff.Run(IReadOnlyList{VolumeItem}, VolumeTakeoffOptions)"/> only reads the item, and its
    /// body is never cut.
    /// </remarks>
    public sealed class VolumeItem
    {
        /// <summary>
        /// Initializes the item, with no id.
        /// </summary>
        /// <param name="solid">The body of the part; its openings are honoured, so only its material is counted.</param>
        /// <param name="name">A name to know the part by, such as its kind or its id; null if there is none.</param>
        /// <param name="priority">
        /// Who keeps the material this part shares with another: the higher priority keeps it. Any number will do,
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
        /// Initializes the item with an id.
        /// </summary>
        /// <param name="solid">The body of the part; its openings are honoured, so only its material is counted.</param>
        /// <param name="name">A name to know the part by, such as its kind; null if there is none.</param>
        /// <param name="priority">
        /// Who keeps the material this part shares with another: the higher priority keeps it. Any number will do,
        /// negative ones too.
        /// </param>
        /// <param name="id">
        /// Who keeps it of two parts of the same priority: the larger id, and an item with an id before one without. Any
        /// number will do; a Tekla part's <c>Identifier.ID</c> takes ties as HDC WBS's WBSCalculator does.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        public VolumeItem(GeoSolid3 solid, string name, int priority, int id)
            : this(solid, name, priority)
        {
            Id = id;
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
        /// Gets who keeps the material this part shares with another: the higher priority keeps it, and of two the same,
        /// as <see cref="Id"/> says.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Gets the id that says who keeps the material of two parts of the same priority; null if the item was given
        /// none.
        /// </summary>
        /// <remarks>
        /// Of two parts of the same priority, one with an id keeps the overlap from one without, the larger id from the
        /// smaller, and of two with the same id or with none, the item earlier in the list.
        /// </remarks>
        public int? Id { get; }
    }
}

using System;

namespace GeometryHelper.Export
{
    /// <summary>
    /// What a loop of a HATCH is, as AutoCAD's <c>HatchLoopTypes</c> flags it: group 92 of the loop. A loop of a polyline has
    /// <see cref="Polyline"/>; one of edges has not.
    /// </summary>
    [Flags]
    public enum DxfHatchLoopTypes
    {
        /// <summary>None of the flags, 0.</summary>
        Default = 0,

        /// <summary>A loop on the outside, 1.</summary>
        External = 1,

        /// <summary>A loop of a polyline, its vertices with bulges, 2.</summary>
        Polyline = 2,

        /// <summary>A loop AutoCAD found by picking a point inside it, 4.</summary>
        Derived = 4,

        /// <summary>A loop round a text, 8.</summary>
        Textbox = 8,

        /// <summary>The outermost loop, 16.</summary>
        Outermost = 16,

        /// <summary>A loop that is not closed, 32.</summary>
        NotClosed = 32,

        /// <summary>A loop that crosses itself, 64.</summary>
        SelfIntersecting = 64,

        /// <summary>A loop round a text inside an island, 128.</summary>
        TextIsland = 128,

        /// <summary>A loop that repeats another, 256.</summary>
        Duplicate = 256,
    }
}

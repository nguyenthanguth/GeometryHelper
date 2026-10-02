namespace GeometryHelper.Meshing
{
    /// <summary>
    /// Which way the axes of a grid, or of strips, run in space: over a flat shape, its first axis within the shape's plane;
    /// through a body, all three.
    /// </summary>
    /// <remarks>
    /// <see cref="MeshPlacement3"/> carries one, with the direction or the frame some of them need.
    /// </remarks>
    public enum MeshAxes
    {
        /// <summary>
        /// Along the world's axes. Over a flat shape the first axis runs level and the second up the slope, as an elevation
        /// is drawn: along X and Y on a level shape, along the wall and up it on an upright one. Through a body, along X, Y
        /// and Z.
        /// </summary>
        World,

        /// <summary>
        /// Along the shape's own sides. Over a flat shape the first axis runs along the long side of the smallest rectangle
        /// round it, so a rectangle's grid runs along its sides. Through a box, along its own axes; through a body, along
        /// the smallest box round it, the axis nearest upright as Z and the longer of the other two as X.
        /// </summary>
        Own,

        /// <summary>
        /// Standing up. Through a body, Z runs up the world's Z axis and X along the long side of the smallest rectangle round
        /// the body's plan, so a wall's cells run along it and a column's stand on its section. Over a flat shape the same
        /// as <see cref="World"/>.
        /// </summary>
        Upright,

        /// <summary>
        /// The first axis along a direction. Over a flat shape the direction is laid onto the shape's plane; through a body
        /// X runs along it, Y level across it, and Z as near up as is square to both.
        /// </summary>
        Along,

        /// <summary>
        /// Along the axes of a coordinate system. Over a flat shape its X axis is laid onto the shape's plane, or its Y axis
        /// where X stands square to the plane; through a body its three axes are the grid's.
        /// </summary>
        Frame,
    }
}

using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>The kind of parameter a custom grip of a dynamic INSERT drives, as AutoCAD's Block Editor names the parameters.</summary>
    public enum DxfDynamicBlockGripType
    {
        /// <summary>The end of a linear parameter: dragged along its line, it sets the distance. AutoCAD draws an arrow.</summary>
        Linear,

        /// <summary>The end of a polar parameter: dragged anywhere, it sets the distance and the angle. AutoCAD draws a square.</summary>
        Polar,

        /// <summary>The end of an XY parameter: dragged anywhere, it sets the distances along x and along y. AutoCAD draws a square.</summary>
        XY,

        /// <summary>A point parameter: dragged anywhere, it sets the position's x and y. AutoCAD draws a square.</summary>
        Point,

        /// <summary>The end of a rotation parameter: dragged round its base point, it sets the angle. AutoCAD draws a circle.</summary>
        Rotation,

        /// <summary>A flip parameter: clicked, it flips the state. AutoCAD draws a bent arrow across the reflection line.</summary>
        Flip,

        /// <summary>A visibility parameter: clicked, it lists the states. AutoCAD draws a triangle pointing down.</summary>
        Visibility,

        /// <summary>A lookup parameter: clicked, it lists the rows. AutoCAD draws a triangle pointing down.</summary>
        Lookup,
    }

    /// <summary>
    /// One custom grip of an INSERT of a dynamic block, as AutoCAD shows it on the INSERT selected: what it drives, where it is
    /// and the point it is dragged from, in the coordinates of the file, as <see cref="DxfBlockReference.GetDynamicGrips"/>
    /// gives them.
    /// </summary>
    /// <remarks>
    /// The door of a Width from (0, 0) to (900, 0), inserted at (1 000, 500) and turned a quarter turn, gives a
    /// <see cref="DxfDynamicBlockGripType.Linear"/> grip at (1 000, 1 400), of the base point (1 000, 500) and the direction
    /// (0, 1), driving Width; dragged to (1 000, 1 500) it asks a Width of 1 000.
    /// </remarks>
    public sealed class DxfDynamicBlockGrip
    {
        private readonly string[] _names;

        internal DxfDynamicBlockGrip(
            DxfDynamicBlockGripType gripType,
            GeoPoint3 position,
            GeoPoint3 basePoint,
            GeoVector3 direction,
            IEnumerable<string> names
        )
        {
            GripType = gripType;
            Position = position;
            BasePoint = basePoint;
            Direction = direction;
            _names = new List<string>(names).ToArray();
        }

        /// <summary>Gets the kind of parameter the grip drives.</summary>
        public DxfDynamicBlockGripType GripType { get; }

        /// <summary>Gets where the grip is, in the coordinates of the file: where the INSERT's values put it.</summary>
        public GeoPoint3 Position { get; }

        /// <summary>
        /// Gets the point the parameter measures from, in the coordinates of the file: the base point of a linear, a polar, an
        /// XY or a rotation parameter, the start of a flip parameter's reflection line, the grip's own position for a point, a
        /// visibility or a lookup parameter.
        /// </summary>
        public GeoPoint3 BasePoint { get; }

        /// <summary>
        /// Gets the way the grip points, a unit vector in the coordinates of the file: from the base point to the end of a
        /// linear parameter; across a flip parameter's reflection line, to its left looking along it and to its right while
        /// flipped; nought for the others.
        /// </summary>
        public GeoVector3 Direction { get; }

        /// <summary>
        /// Gets the names of the properties the grip sets, as <see cref="DxfDynamicBlockReferenceProperty.PropertyName"/> gives
        /// them: one for a linear, a rotation, a flip, a visibility or a lookup parameter; the distance and then the angle of a
        /// polar one; the x and then the y of an XY or a point one.
        /// </summary>
        public IReadOnlyList<string> PropertyNames => Array.AsReadOnly(_names);

        /// <summary>Gives the grip as a line for debugging: its kind, its properties and its position.</summary>
        /// <returns>The line, such as <c>Linear Width (1000, 1400, 0)</c>.</returns>
        public override string ToString() => GripType + " " + string.Join(", ", _names) + " " + Position;
    }
}

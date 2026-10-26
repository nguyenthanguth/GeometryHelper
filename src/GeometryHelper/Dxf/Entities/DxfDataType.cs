namespace GeometryHelper.Dxf
{
    /// <summary>
    /// What kind of value a cell of a table holds, as AutoCAD's <c>DataType</c> numbers it: group 90 of the cell's value
    /// (<c>301 CELL_VALUE</c>), from R2007 on. A cell of 12.5 is <see cref="Double"/>, one of Parts <see cref="String"/>.
    /// </summary>
    public enum DxfDataType
    {
        /// <summary>Unknown, 0.</summary>
        Unknown = 0,

        /// <summary>A whole number of 32 bits, 1: its number in group 91.</summary>
        Long = 1,

        /// <summary>A number, 2: in group 140.</summary>
        Double = 2,

        /// <summary>A text, 4: in group 1, in pieces of 2 where it is long.</summary>
        String = 4,

        /// <summary>A date, 8.</summary>
        Date = 8,

        /// <summary>A point of the plane, 16.</summary>
        Point = 16,

        /// <summary>A point in space, 32.</summary>
        Point3d = 32,

        /// <summary>An object of the drawing, 64.</summary>
        ObjectId = 64,

        /// <summary>Bytes, 128.</summary>
        Buffer = 128,

        /// <summary>A list of groups, 256.</summary>
        Resbuf = 256,

        /// <summary>Any of the others, 512.</summary>
        General = 512,
    }
}

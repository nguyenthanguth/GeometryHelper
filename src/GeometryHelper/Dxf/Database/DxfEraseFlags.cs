using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The empty objects <see cref="DxfDatabase.CountEmptyObjects"/> counts and <see cref="DxfDatabase.EraseEmptyObjects"/>
    /// erases, as AutoCAD's <c>EraseFlags</c> names and numbers them, given as a whole number:
    /// <c>db.EraseEmptyObjects((int)DxfEraseFlags.ZeroLengthCurve)</c>.
    /// </summary>
    [Flags]
    public enum DxfEraseFlags
    {
        /// <summary>A curve of no length, 1: a line whose ends are one point, a circle or an arc of no radius, and the like.</summary>
        ZeroLengthCurve = 1,

        /// <summary>A text of no words, 2: a TEXT or an MTEXT whose text is empty or spaces.</summary>
        EmptyText = 2,

        /// <summary>Every kind of empty object, -1.</summary>
        AllEmptyObj = -1,
    }
}

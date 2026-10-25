namespace GeometryHelper.Dxf
{
    /// <summary>
    /// One of the six views along an axis, as AutoCAD's <c>OrthographicView</c> numbers them: the views of a viewport and
    /// its user coordinate system (group 79); <see cref="NonOrthoView"/> for any other.
    /// </summary>
    public enum DxfOrthographicView
    {
        /// <summary>Along no axis, 0.</summary>
        NonOrthoView = 0,

        /// <summary>From above, looking down the z axis, 1.</summary>
        TopView = 1,

        /// <summary>From below, 2.</summary>
        BottomView = 2,

        /// <summary>From the front, looking along the y axis, 3.</summary>
        FrontView = 3,

        /// <summary>From the back, 4.</summary>
        BackView = 4,

        /// <summary>From the left, looking along the x axis, 5.</summary>
        LeftView = 5,

        /// <summary>From the right, 6.</summary>
        RightView = 6,
    }
}

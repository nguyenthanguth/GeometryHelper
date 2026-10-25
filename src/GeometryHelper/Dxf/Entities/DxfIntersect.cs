namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Which of two entities is taken as running on past its ends when they are intersected, numbered as AutoCAD's
    /// <c>Intersect</c>: a line runs on as its infinite line, an arc as its circle; any other entity, an elliptical arc and a
    /// polyline among them, is taken as it is.
    /// </summary>
    public enum DxfIntersect
    {
        /// <summary>Neither: the points both entities pass through as they are.</summary>
        OnBothOperands = 0,

        /// <summary>The entity whose <c>IntersectWith</c> is called.</summary>
        ExtendThis = 1,

        /// <summary>The entity it is given.</summary>
        ExtendArgument = 2,

        /// <summary>Both.</summary>
        ExtendBoth = 3,
    }
}

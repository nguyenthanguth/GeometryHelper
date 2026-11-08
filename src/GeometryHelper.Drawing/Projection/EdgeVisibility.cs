namespace GeometryHelper.Drawing.Projection
{
    /// <summary>
    /// Whether a line of a view is seen or lies behind something.
    /// </summary>
    public enum EdgeVisibility
    {
        /// <summary>Seen: nothing of any object stands in front of it. Drawn as a continuous line.</summary>
        Visible = 0,

        /// <summary>Behind a face of some object, its own among them. Drawn as a dashed line.</summary>
        Hidden = 1,
    }
}

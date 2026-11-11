namespace GeometryHelper.Drawing
{
    /// <summary>
    /// What a view of a drawing is, which says where it looks from. The six views along the axes of the drawing's frame,
    /// and the isometric one, have frames of their own (<see cref="StandardViews.GetFrame"/>); a section, a detail and a
    /// free view are set by the one who makes them.
    /// </summary>
    public enum ViewKind
    {
        /// <summary>The main view: from the front of the drawing's frame, its X to the right and its Y up.</summary>
        Front,

        /// <summary>From above, along the frame's Y downwards, its X to the right.</summary>
        Top,

        /// <summary>From below, along the frame's Y upwards, its X to the right.</summary>
        Bottom,

        /// <summary>An end view from the left, along the frame's X, its Y up.</summary>
        Left,

        /// <summary>An end view from the right, against the frame's X, its Y up.</summary>
        Right,

        /// <summary>From behind, opposite the front view, its Y up.</summary>
        Back,

        /// <summary>Across a cut plane, the side towards the viewer taken away.</summary>
        Section,

        /// <summary>A part of another view, cropped and drawn at a larger scale.</summary>
        Detail,

        /// <summary>From the front, the right and above at once, along a fixed oblique direction.</summary>
        Isometric,

        /// <summary>Along a direction the user sets, as an MVIEW looking from any side.</summary>
        Free,
    }
}

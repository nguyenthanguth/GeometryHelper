namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// Which side of the joint a weld is on, as the arrow of its symbol sees it. A weld object gives it in its attribute
    /// "WeldSide" as Arrow, Other or Both, letters of either case the same.
    /// </summary>
    public enum WeldSide
    {
        /// <summary>On the side the arrow points to: the symbol stands on the continuous reference line.</summary>
        Arrow = 0,

        /// <summary>On the other side: the symbol hangs from the dashed identification line.</summary>
        Other = 1,

        /// <summary>On both sides: a symbol each side of the continuous line, and no dashed line, as ISO 2553 draws symmetric welds.</summary>
        Both = 2,
    }
}

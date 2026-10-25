namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A layer of the layer table of a DXF file as <see cref="DxfReader"/> read it: its name, the look it gives the entities
    /// on it that take theirs from it, and whether it is shown, frozen, locked and plotted.
    /// </summary>
    /// <remarks>
    /// The layer steel in red, turned off, is <c>Color</c> 1 and <c>IsOff</c> true: the table writes it as the colour -1. A
    /// layer cannot be changed once read, and two layers are the same only where they are one object: it does not compare
    /// its names or numbers.
    /// </remarks>
    public sealed class DxfLayer
    {
        internal DxfLayer(string name, int? colour, int? trueColour, string linetype, int? lineweight, bool isOff, bool isFrozen, bool isLocked, bool isPlotted)
        {
            Name = name;
            Color = colour;
            TrueColor = trueColour;
            Linetype = linetype;
            LineWeight = lineweight;
            IsOff = isOff;
            IsFrozen = isFrozen;
            IsLocked = isLocked;
            IsPlotted = isPlotted;
        }

        /// <summary>
        /// Gets the name of the layer (group 2), as <see cref="DxfDocument.Layers"/> names it: the first spelling met of a name
        /// that others differ from only in case.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the colour of the layer (group 62), an AutoCAD colour index from 1 to 255, whether the layer is on or off; null
        /// where the table gives none, or one outside 1 to 255.
        /// </summary>
        public int? Color { get; }

        /// <summary>
        /// Gets the colour of the layer as red, green and blue (group 420), 0xRRGGBB, which a CAD program draws in place of
        /// <see cref="Color"/>; null where the table gives none.
        /// </summary>
        public int? TrueColor { get; }

        /// <summary>
        /// Gets the name of the linetype of the layer (group 6), such as <c>Continuous</c> or <c>DASHED</c>; null where the
        /// table gives none.
        /// </summary>
        public string Linetype { get; }

        /// <summary>
        /// Gets the lineweight of the layer (group 370) in hundredths of a millimetre, one of AutoCAD's 0 to 211; null where the
        /// table gives none, the default (-3), or a number not among AutoCAD's.
        /// </summary>
        public int? LineWeight { get; }

        /// <summary>
        /// Gets whether the layer is turned off, which the table writes as a colour below nought: its entities are not drawn.
        /// </summary>
        public bool IsOff { get; }

        /// <summary>
        /// Gets whether the layer is frozen (flag 1 of group 70): its entities are not drawn.
        /// </summary>
        public bool IsFrozen { get; }

        /// <summary>
        /// Gets whether the layer is locked (flag 4 of group 70): its entities are drawn and cannot be changed.
        /// </summary>
        public bool IsLocked { get; }

        /// <summary>
        /// Gets whether the layer is plotted (group 290); true where the table does not say.
        /// </summary>
        public bool IsPlotted { get; }
    }
}

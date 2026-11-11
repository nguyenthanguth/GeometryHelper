namespace GeometryHelper.Drawing
{
    /// <summary>
    /// What a <see cref="DrawingObject"/> is in the model, which decides how a drawing shows it: a part is drawn with its
    /// outline and marked, a bolt and a hole are drawn with their symbols and dimensioned by their centres, a weld gets its
    /// symbol, and a bar is drawn inside the concrete it lies in.
    /// </summary>
    public enum DrawingObjectKind
    {
        /// <summary>A part: a plate, a profile, a tube, a concrete member.</summary>
        Part,

        /// <summary>A bolt, with its nut and washers.</summary>
        Bolt,

        /// <summary>A hole cut through parts, drawn on its own where no bolt fills it.</summary>
        Hole,

        /// <summary>A weld joining parts.</summary>
        Weld,

        /// <summary>A reinforcing bar or a stirrup.</summary>
        Rebar,
    }
}

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The user coordinate systems of the drawing.</summary>
    public sealed partial class CadDocument
    {
        private CadUcsManager _ucs;

        /// <summary>
        /// Gets the user coordinate systems of the drawing, read from it when first asked for, as <see cref="CadUcsManager"/>
        /// says: the current one of each space, the earlier ones and the named ones. A change to them is not a step of undo, and
        /// is written into the drawing when it is saved.
        /// </summary>
        public CadUcsManager Ucs => _ucs ?? (_ucs = new CadUcsManager(this));
    }
}

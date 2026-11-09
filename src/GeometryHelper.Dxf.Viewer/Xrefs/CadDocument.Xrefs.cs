namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The external references and images of a document.</summary>
    public sealed partial class CadDocument
    {
        private CadXrefManager _xrefs;

        /// <summary>
        /// Gets the external references and images of the drawing as the presenter shows them: the files found, the drawings
        /// read and what was unloaded (<see cref="CadXrefManager"/>). It outlives an undo, which reads the drawing anew, so a
        /// drawing referenced is read once.
        /// </summary>
        public CadXrefManager Xrefs => _xrefs ?? (_xrefs = new CadXrefManager(this));
    }
}

using System;
using GeometryHelper.Geometry;

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

        /// <summary>
        /// The base point ($INSBASE) the drawing an external reference names has now, which stands at the reference's insertion
        /// point; null for any other reference, and for one whose drawing is unloaded, not found or cannot be read.
        /// </summary>
        internal GeoPoint3? XrefBaseOf(DxfBlockReference reference)
        {
            DxfBlockTableRecord block = reference.BlockTableRecord;

            if (block == null || !block.IsFromExternalReference)
            {
                return null;
            }

            try
            {
                CadXrefContent content = Xrefs.ContentOf(block, DrawingFolder, out _);
                return content?.Database?.Insbase;
            }
            catch (Exception)
            {
                // A drawing that cannot be read has no base point to place the clip by.
                return null;
            }
        }
    }
}

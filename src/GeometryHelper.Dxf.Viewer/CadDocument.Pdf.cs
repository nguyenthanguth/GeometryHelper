namespace GeometryHelper.Dxf.Viewer
{
    public sealed partial class CadDocument
    {
        /// <summary>
        /// Gets or sets whether the frames of PDF underlays are drawn and plotted, as AutoCAD's PDFFRAME: 0 for no frame, 1, the
        /// default, for frames shown and plotted, 2 for frames shown and left off a plot. AutoCAD saves it in the drawing; a DXF has
        /// no place for it, so it lasts as long as the document is open. A display list already made keeps its frames until
        /// <see cref="Regen"/>.
        /// </summary>
        public int PdfFrame { get; set; } = 1;

        /// <summary>
        /// Gets or sets whether object snaps find the geometry of PDF underlays, as AutoCAD's PDFOSNAP: true, the default, where the
        /// snaps End, Mid, Cen, Int and the others take the lines and curves a PDF draws, false where they pass over them. It lasts
        /// as long as the document is open.
        /// </summary>
        public bool PdfOsnap { get; set; } = true;
    }
}

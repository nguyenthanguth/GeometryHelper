namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The clocks of the drawing TIME shows (<see cref="CadDocumentTimes"/>).</summary>
    public sealed partial class CadDocument
    {
        private CadDocumentTimes _times;

        /// <summary>Gets the clocks of the drawing, started when first asked for (TIME, TDINDWG, TDUSRTIMER).</summary>
        internal CadDocumentTimes Times => _times ?? (_times = new CadDocumentTimes(this));
    }
}

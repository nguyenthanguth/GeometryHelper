namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What BOUNDARY gives its Boundary Creation dialog: the settings it shares with -BOUNDARY, island detection and the
    /// object type, and whether the dialog closed with Pick Points, BOUNDARY then asking "Pick internal point:" until Enter.
    /// </summary>
    public sealed class CadBoundaryRequest
    {
        /// <summary>Gets or sets whether the islands inside an area are bounded too: on as AutoCAD starts.</summary>
        public bool IslandDetection { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the boundaries are made regions rather than polylines; the presenter cannot write a region yet,
        /// and BOUNDARY says so at each point.
        /// </summary>
        public bool IsRegion { get; set; }

        /// <summary>Gets or sets whether the dialog closed with Pick Points; false where it was cancelled, nothing then kept.</summary>
        public bool IsPicking { get; set; }
    }
}

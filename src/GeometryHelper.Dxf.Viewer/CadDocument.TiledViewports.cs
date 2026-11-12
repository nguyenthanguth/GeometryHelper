namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The tiled viewports model space is shown through.</summary>
    public sealed partial class CadDocument
    {
        private CadTiledViewports _tiles;

        /// <summary>
        /// Gets the tiled viewports of model space, read from the drawing's VPORT table when first asked for, as
        /// <see cref="CadTiledViewports"/> says: one, the whole drawing area, until VPORTS splits it.
        /// </summary>
        public CadTiledViewports TiledViewports => _tiles ?? (_tiles = new CadTiledViewports(this));
    }
}

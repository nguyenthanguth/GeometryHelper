namespace GeometryHelper.Cad
{
    public sealed partial class GlobalSettings
    {
        private double _fileListWidth = 288.0;
        private double _propertiesWidth = 312.0;
        private double _sidePanelWidth = 320.0;

        /// <summary>Gets or sets the width of the DRAWINGS list, in pixels, 180 to 600: 288 by default, as the user last dragged it.</summary>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public double FileListWidth
        {
            get => _fileListWidth;
            set => _fileListWidth = Range(value, 180.0, 600.0, nameof(FileListWidth));
        }

        /// <summary>Gets or sets the width of the PROPERTIES panel, in pixels, 180 to 600: 312 by default, as the user last dragged it.</summary>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public double PropertiesWidth
        {
            get => _propertiesWidth;
            set => _propertiesWidth = Range(value, 180.0, 600.0, nameof(PropertiesWidth));
        }

        /// <summary>
        /// Gets or sets the width of the panel an extension adds beside the DRAWINGS list, in pixels, 160 to 1 200: 320 by
        /// default, as the user last dragged it.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public double SidePanelWidth
        {
            get => _sidePanelWidth;
            set => _sidePanelWidth = Range(value, 160.0, 1200.0, nameof(SidePanelWidth));
        }

        /// <summary>Gets or sets whether the DRAWINGS list, with an extension's panel beside it, stands right of the canvas: off by default, left.</summary>
        public bool FileListOnRight { get; set; }

        /// <summary>Gets or sets whether the PROPERTIES panel stands left of the canvas: off by default, right.</summary>
        public bool PropertiesOnLeft { get; set; }
    }
}

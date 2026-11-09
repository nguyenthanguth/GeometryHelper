namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>The user coordinate system the status bar and dynamic input show their numbers in.</summary>
    public sealed partial class MainViewModel
    {
        /// <summary>The UCS points are given in for the drawing shown, as its session has it; the world's with none shown.</summary>
        private CadUcs Ucs => _current?.Session?.Ucs ?? CadUcs.World;
    }
}

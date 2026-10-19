using HelixToolkit.SharpDX.Core;
using MeshGeometry3D = HelixToolkit.SharpDX.Core.MeshGeometry3D;

namespace GeometryHelper.Viewer.Views
{
    /// <summary>
    /// What the graphics card is given for an item, relative to a render origin. It holds no screen type, so it can be made on another thread while the old
    /// geometry is still being drawn, and the one object is the geometry of the elements of the views that share it.
    /// </summary>
    internal sealed class ItemGeometry
    {
        internal MeshGeometry3D Mesh;
        internal LineGeometry3D Lines;
        internal PointGeometry3D Points;
        internal LineGeometry3D Section;
        internal MeshGeometry3D Cap;
    }
}

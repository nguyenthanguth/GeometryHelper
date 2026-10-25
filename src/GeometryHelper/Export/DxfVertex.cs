using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A VERTEX of a POLYLINE, as AutoCAD's <c>Vertex</c> is one of a polyline: the record that follows the polyline in the
    /// file and gives one of its points, of the class its polyline has (<see cref="DxfVertex2d"/>,
    /// <see cref="DxfPolylineVertex3d"/>, <see cref="DxfPolyFaceMeshVertex"/>, <see cref="DxfFaceRecord"/> or
    /// <see cref="DxfPolygonMeshVertex"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A vertex is reached by walking its polyline, <c>foreach (DxfVertex2d v in polyline)</c>, as AutoCAD walks the vertices
    /// of one: the same object each time while its record is there. Its <see cref="DxfEntity.Owner"/> is the polyline; its
    /// point, look and extended data are read from its own record and written into it, its handle kept.
    /// </para>
    /// <para>
    /// A vertex made in code, such as <c>new DxfVertex2d(point, 0.5, 0, 0, 0)</c>, is in no polyline until it is appended to
    /// one, as AutoCAD's <c>AppendVertex</c> takes it; it is the vertex of that polyline from then on, every other vertex
    /// keeping its handle. <see cref="DxfEntity.Erase"/> takes a vertex out of its polyline the same way.
    /// </para>
    /// </remarks>
    public abstract class DxfVertex : DxfEntity
    {
        /// <summary>The object of a VERTEX record that follows its polyline.</summary>
        private protected DxfVertex(DxfItem item)
            : base(item)
        {
        }

        /// <summary>A vertex made in code from its groups, in no polyline.</summary>
        private protected DxfVertex(IEnumerable<DxfTypedValue> groups)
            : base("VERTEX", groups)
        {
        }

        /// <summary>The polyline the vertex follows; null for one made in code and not yet appended.</summary>
        internal DxfEntity Polyline => FollowedEntity;

        /// <summary>The index of the vertex's record among those of its polyline, 1 for the first.</summary>
        internal int Record => FollowerIndex;

        /// <summary>The flags (group 70) of the POLYLINE a VERTEX record follows; nought for one that follows none.</summary>
        internal static int ParentFlags(DxfItem item)
            => item.Parent == null ? 0 : DxfPolyline2d.Flags(item.Parent);

        /// <summary>
        /// Takes the vertex out of its polyline, as AutoCAD's <c>Erase</c> does, every other vertex keeping its handle. A
        /// <see cref="DxfPolyline2d"/> or a <see cref="DxfPolyline3d"/> keeps two vertices at least, as AutoCAD keeps one on save
        /// and audit: a vertex of one with two is not taken out. A point or face of a <see cref="DxfPolyFaceMesh"/> is taken out
        /// with the mesh's counts (groups 71 and 72) one less and the faces' numbers of the later points one less; a point a face
        /// names is kept.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the vertex is of a polyline of two vertices, is a point of a mesh
        /// a face names, is in no file, or is erased already.</exception>
        public override void Erase()
        {
            DxfItem parent = Item.Parent;

            if (parent != null && !IsErased && Polyline is DxfPolyFaceMesh mesh)
            {
                mesh.Remove(this);
                return;
            }

            if (parent != null && !IsErased && (Polyline is DxfPolyline2d || Polyline is DxfPolyline3d))
            {
                int vertices = 0;

                for (int record = 1; record < Item.File.RecordCount(parent); record++)
                {
                    vertices += Item.File.RecordType(parent, record) == "VERTEX" ? 1 : 0;
                }

                if (vertices <= 2)
                {
                    throw new InvalidOperationException("A polyline keeps two vertices at least, and this one has " + vertices.ToString(CultureInfo.InvariantCulture) + ": erase the polyline instead.");
                }
            }

            base.Erase();
        }

        /// <summary>Takes the vertex's record out of its polyline, with no check of its own.</summary>
        internal void EraseRecord() => base.Erase();

        /// <summary>The own groups of the vertex's record as they stand.</summary>
        private protected IReadOnlyList<DxfTypedValue> OwnGroups() => GetOwnGroups(0);
    }
}

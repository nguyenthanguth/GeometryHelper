using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.SolidGeometry.Geometry;
using Xbim.Ifc4.Interfaces;

namespace GeometryHelper.IfcConvert.Models
{
    /// <summary>
    /// Represents the complete converted geometry of an <see cref="IIfcProduct"/>.
    /// Holds both closed solid bodies and open surface meshes, along with placement and bounding box.
    /// </summary>
    public sealed class IfcProductGeometry
    {
        private readonly GeoSolid3[] _solids;
        private readonly GeoFace3[] _openSurfaces;

        /// <summary>
        /// Gets the source IFC product.
        /// </summary>
        public IIfcProduct Product { get; }

        /// <summary>
        /// Gets the GlobalId (GUID) of the IFC product.
        /// </summary>
        public string GlobalId => Product?.GlobalId.ToString() ?? string.Empty;

        /// <summary>
        /// Gets the name of the IFC product.
        /// </summary>
        public string Name => Product?.Name?.ToString() ?? string.Empty;

        /// <summary>
        /// Gets the sequence of closed solid bodies converted for this product.
        /// </summary>
        public IReadOnlyList<GeoSolid3> Solids => _solids;

        /// <summary>
        /// Gets any open surface faces that could not form a closed solid (e.g. thin shells, surface models).
        /// </summary>
        public IReadOnlyList<GeoFace3> OpenSurfaces => _openSurfaces;

        /// <summary>
        /// Gets the world transformation applied to this product.
        /// </summary>
        public GeoTransform3 Placement { get; }

        /// <summary>
        /// Gets the total bounding box enclosing all converted solids and open surfaces.
        /// </summary>
        public GeoAabb3 BoundingBox { get; }

        /// <summary>
        /// Gets the sum of volumes of all converted solid bodies.
        /// </summary>
        public double TotalVolume => _solids.Sum(s => s.Volume);

        /// <summary>
        /// Initializes a new instance of <see cref="IfcProductGeometry"/>.
        /// </summary>
        /// <param name="product">The IFC product.</param>
        /// <param name="solids">Converted solid bodies.</param>
        /// <param name="openSurfaces">Converted open surfaces.</param>
        /// <param name="placement">World transformation matrix.</param>
        public IfcProductGeometry(
            IIfcProduct product,
            IEnumerable<GeoSolid3> solids,
            IEnumerable<GeoFace3> openSurfaces = null,
            GeoTransform3 placement = null)
        {
            Product = product;
            _solids = (solids ?? Enumerable.Empty<GeoSolid3>()).ToArray();
            _openSurfaces = (openSurfaces ?? Enumerable.Empty<GeoFace3>()).ToArray();
            Placement = placement ?? GeoTransform3.Identity;

            GeoAabb3 box = GeoAabb3.Empty;
            foreach (GeoSolid3 solid in _solids)
            {
                box = box.Union(solid.GetAabb());
            }

            foreach (GeoFace3 face in _openSurfaces)
            {
                foreach (GeoPoint3 vertex in face.Boundary.Vertices)
                {
                    box = box.Union(vertex);
                }
            }

            BoundingBox = box;
        }

        /// <summary>
        /// Returns a formatted string summarizing the product geometry.
        /// </summary>
        public override string ToString()
        {
            return $"IfcProductGeometry(Name: '{Name}', GUID: {GlobalId}, Solids: {_solids.Length}, OpenSurfaces: {_openSurfaces.Length}, Volume: {TotalVolume:0.###})";
        }
    }
}

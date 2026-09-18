using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.SolidGeometry.Geometry;

namespace GeometryHelper.IfcConvert.Models
{
    /// <summary>
    /// Represents the complete converted geometry of an IFC product.
    /// Holds both closed solid bodies and open surface meshes, along with placement and bounding box.
    /// Does not expose any xBIM types in its public API.
    /// </summary>
    public sealed class IfcProductGeometry
    {
        private readonly GeoSolid3[] _solids;
        private readonly GeoFace3[] _openSurfaces;
        private readonly string[] _warnings;


        /// <summary>
        /// Gets the GlobalId (GUID) of the IFC product.
        /// </summary>
        public string GlobalId { get; }

        /// <summary>
        /// Gets the name of the IFC product.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the IFC entity type name (e.g. "IfcWall", "IfcBeam", "IfcColumn").
        /// </summary>
        public string IfcType { get; }

        /// <summary>
        /// Gets the user-defined tag of the IFC product, if available.
        /// </summary>
        public string Tag { get; }

        /// <summary>
        /// Gets the sequence of closed solid bodies converted for this product.
        /// </summary>
        public IReadOnlyList<GeoSolid3> Solids => _solids;

        /// <summary>
        /// Gets any open surface faces that could not form a closed solid (e.g. thin shells, surface models).
        /// </summary>
        public IReadOnlyList<GeoFace3> OpenSurfaces => _openSurfaces;

        /// <summary>
        /// Gets the transformation applied to this product.
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
        /// Gets whether this product has at least one closed solid body.
        /// </summary>
        public bool HasSolids => _solids.Length > 0;

        /// <summary>
        /// Gets whether this product has open surface meshes.
        /// </summary>
        public bool HasOpenSurfaces => _openSurfaces.Length > 0;

        /// <summary>
        /// Gets whether this product has no solid bodies and no surface meshes.
        /// </summary>
        public bool IsEmpty => !HasSolids && !HasOpenSurfaces;

        /// <summary>
        /// Gets the problems met while converting this product: representation items that failed or produced
        /// no solid, faces that had to be left out, solids that are not closed, openings that could not be cut.
        /// Messages about a representation item start with its STEP entity label (e.g. "#123 IfcExtrudedAreaSolid: ...")
        /// so it can be found in the IFC file. Empty when the conversion was clean.
        /// </summary>
        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary>
        /// Gets whether any problem was met while converting this product. See <see cref="Warnings"/>.
        /// </summary>
        public bool HasWarnings => _warnings.Length > 0;

        /// <summary>
        /// Initializes a new instance of <see cref="IfcProductGeometry"/> with explicit identifiers and geometry.
        /// </summary>
        public IfcProductGeometry(
            string globalId,
            string name,
            string ifcType,
            IEnumerable<GeoSolid3> solids,
            IEnumerable<GeoFace3> openSurfaces = null,
            GeoTransform3 placement = null,
            string tag = null,
            IEnumerable<string> warnings = null)
        {
            GlobalId = globalId ?? string.Empty;
            Name = name ?? string.Empty;
            IfcType = ifcType ?? string.Empty;
            Tag = tag ?? string.Empty;

            _solids = (solids ?? Enumerable.Empty<GeoSolid3>()).ToArray();
            _openSurfaces = (openSurfaces ?? Enumerable.Empty<GeoFace3>()).ToArray();
            _warnings = (warnings ?? Enumerable.Empty<string>()).Where(w => !string.IsNullOrEmpty(w)).ToArray();
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
            return $"IfcProductGeometry(Type: {IfcType}, Name: '{Name}', GUID: {GlobalId}, Solids: {_solids.Length}, OpenSurfaces: {_openSurfaces.Length}, Volume: {TotalVolume:0.###}, Warnings: {_warnings.Length})";
        }
    }
}

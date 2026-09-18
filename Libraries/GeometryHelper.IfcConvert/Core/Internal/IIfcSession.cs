using System;
using System.Collections.Generic;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.SolidGeometry.Geometry;

namespace GeometryHelper.IfcConvert.Core.Internal
{
    /// <summary>
    /// Pure GeometryHelper session interface separating high-level APIs from xBIM types.
    /// </summary>
    internal interface IIfcSession : IDisposable
    {
        int ProductCount { get; }
        string SchemaVersion { get; }
        string OriginalLengthUnit { get; }
        IReadOnlyList<string> DuplicateGlobalIds { get; }

        IfcConvertOptions ResolveUnitOptions(IfcConvertOptions options);

        GeoSolid3 GetSolid(string guid, IfcConvertOptions options);
        IReadOnlyList<GeoSolid3> GetSolids(string guid, IfcConvertOptions options);
        IfcProductGeometry GetGeometry(string guid, IfcConvertOptions options);

        IReadOnlyList<GeoSolid3> GetSolidsByType(string ifcTypeName, IfcConvertOptions options);
        IReadOnlyList<IfcProductGeometry> GetGeometriesByType(string ifcTypeName, IfcConvertOptions options);

        IReadOnlyList<GeoSolid3> GetAllSolids(IfcConvertOptions options);
        IEnumerable<IfcProductGeometry> EnumerateGeometries(IfcConvertOptions options);
        IEnumerable<GeoSolid3> EnumerateSolids(IfcConvertOptions options);

        IReadOnlyList<IfcProductMetadata> GetProductCatalog();

        IReadOnlyDictionary<string, IfcPropertySet> GetProperties(string guid);
    }
}

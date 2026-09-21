using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace GeometryHelper.IfcConvert.Core.Internal
{
    /// <summary>
    /// Testing extensions providing internal access to xBIM session entities for unit tests.
    /// </summary>
    internal static class IfcStoreCacheTestingExtensions
    {
        public static IfcStore GetStore(this IfcStoreCache cache) => (cache?.Session as XbimIfcSession)?.Store;
        public static IReadOnlyDictionary<string, IIfcProduct> GetProductsByGuid(this IfcStoreCache cache) => (cache?.Session as XbimIfcSession)?.ProductsByGuid;
        public static IIfcProduct GetProduct(this IfcStoreCache cache, string guid) => (cache?.Session as XbimIfcSession)?.GetProduct(guid);
        public static IEnumerable<T> GetProducts<T>(this IfcStoreCache cache) where T : IIfcProduct => (cache?.Session as XbimIfcSession)?.GetProducts<T>() ?? Enumerable.Empty<T>();
    }
}

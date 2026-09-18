using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.IfcConvert.Converters.Internal;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.SolidGeometry.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace GeometryHelper.IfcConvert.Core.Internal
{
    /// <summary>
    /// Encapsulated xBIM session implementation.
    /// Loaded dynamically by <see cref="IfcStoreCache"/> after module initializers have executed.
    /// </summary>
    internal sealed class XbimIfcSession : IIfcSession
    {
        private bool _disposed;
        private readonly IfcStore _store;
        private readonly Dictionary<string, IIfcProduct> _productsByGuid;

        /// <summary>
        /// GlobalIds shared by more than one product. Only the first product with such an id is indexed.
        /// </summary>
        public IReadOnlyList<string> DuplicateGlobalIds { get; }

        // Geometry cache: key = "guid|optionsCacheKey" -> converted geometry
        // Eliminates redundant xBIM engine calls for repeated access to the same product with same options.
        // Values are Lazy so that concurrent callers asking for the same key share one conversion:
        // ConcurrentDictionary.GetOrAdd may run its factory more than once under contention.
        private readonly ConcurrentDictionary<string, Lazy<IfcProductGeometry>> _geometryCache
            = new ConcurrentDictionary<string, Lazy<IfcProductGeometry>>(StringComparer.Ordinal);

        // Property cache: key = guid -> dict of pset name -> IfcPropertySet
        private readonly ConcurrentDictionary<string, Lazy<IReadOnlyDictionary<string, IfcPropertySet>>> _propertyCache
            = new ConcurrentDictionary<string, Lazy<IReadOnlyDictionary<string, IfcPropertySet>>>(StringComparer.Ordinal);

        // The project's unit assignment and the property reader built on it, created on first use.
        private readonly Lazy<IfcUnits> _units;
        private readonly Lazy<PropertyReader> _propertyReader;

        public IfcStore Store => _store;
        public IReadOnlyDictionary<string, IIfcProduct> ProductsByGuid => _productsByGuid;

        public int ProductCount => _productsByGuid.Count;

        public string SchemaVersion => _store?.SchemaVersion.ToString() ?? string.Empty;

        public string OriginalLengthUnit => _units.Value.LengthUnitName;

        public XbimIfcSession(string filePath)
        {
            _store = IfcStore.Open(filePath);
            _units = new Lazy<IfcUnits>(() => new IfcUnits(_store));
            _propertyReader = new Lazy<PropertyReader>(() => new PropertyReader(_units.Value));
            (_productsByGuid, DuplicateGlobalIds) = IndexProducts(_store);
        }

        public XbimIfcSession(IfcStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _units = new Lazy<IfcUnits>(() => new IfcUnits(_store));
            _propertyReader = new Lazy<PropertyReader>(() => new PropertyReader(_units.Value));
            (_productsByGuid, DuplicateGlobalIds) = IndexProducts(_store);
        }

        private static (Dictionary<string, IIfcProduct>, IReadOnlyList<string>) IndexProducts(IfcStore store)
        {
            Dictionary<string, IIfcProduct> byGuid = new Dictionary<string, IIfcProduct>(StringComparer.Ordinal);
            HashSet<string> duplicates = new HashSet<string>(StringComparer.Ordinal);

            foreach (IIfcProduct product in store.Instances.OfType<IIfcProduct>())
            {
                string guid = product.GlobalId.ToString();
                if (string.IsNullOrWhiteSpace(guid))
                {
                    continue;
                }

                if (byGuid.ContainsKey(guid))
                {
                    duplicates.Add(guid);
                }
                else
                {
                    byGuid[guid] = product;
                }
            }

            return (byGuid, duplicates.ToList());
        }

        public IfcConvertOptions ResolveUnitOptions(IfcConvertOptions opts)
        {
            if (opts != null && opts.TargetUnit != LengthUnit.Original && _store?.ModelFactors != null)
            {
                double factorToMetres = _store.ModelFactors.LengthToMetresConversionFactor;
                double unitScale = 1.0;
                if (opts.TargetUnit == LengthUnit.Millimeters)
                {
                    unitScale = factorToMetres * 1000.0;
                }
                else if (opts.TargetUnit == LengthUnit.Meters)
                {
                    unitScale = factorToMetres;
                }

                if (Math.Abs(unitScale - 1.0) > 1e-9)
                {
                    IfcConvertOptions resolved = opts.Clone();
                    resolved.ScaleFactor = opts.ScaleFactor * unitScale;
                    resolved.TargetUnit = LengthUnit.Original;
                    return resolved;
                }
            }

            return opts;
        }

        private IfcProductGeometry GetCachedGeometry(string guid, IIfcProduct product, IfcConvertOptions options)
        {
            string cacheKey = guid + "|" + (options?.GetCacheKey() ?? string.Empty);
            return _geometryCache.GetOrAdd(cacheKey, _ => new Lazy<IfcProductGeometry>(() => product.ToProductGeometry(options))).Value;
        }

        public GeoSolid3 GetSolid(string guid, IfcConvertOptions options)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return null;
            }

            if (!_productsByGuid.TryGetValue(guid, out IIfcProduct product))
            {
                return null;
            }

            IfcProductGeometry geometry = GetCachedGeometry(guid, product, options);
            if (geometry.Solids.Count == 0)
            {
                return null;
            }

            return geometry.Solids.OrderByDescending(s => s.Volume).FirstOrDefault();
        }

        public IReadOnlyList<GeoSolid3> GetSolids(string guid, IfcConvertOptions options)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return Array.Empty<GeoSolid3>();
            }

            if (!_productsByGuid.TryGetValue(guid, out IIfcProduct product))
            {
                return Array.Empty<GeoSolid3>();
            }

            IfcProductGeometry geometry = GetCachedGeometry(guid, product, options);
            return geometry.Solids;
        }

        public IfcProductGeometry GetGeometry(string guid, IfcConvertOptions options)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return null;
            }

            if (!_productsByGuid.TryGetValue(guid, out IIfcProduct product))
            {
                return null;
            }

            return GetCachedGeometry(guid, product, options);
        }

        public IReadOnlyList<GeoSolid3> GetSolidsByType(string ifcTypeName, IfcConvertOptions options)
        {
            if (string.IsNullOrWhiteSpace(ifcTypeName))
            {
                return Array.Empty<GeoSolid3>();
            }

            List<GeoSolid3> list = new List<GeoSolid3>();
            foreach (KeyValuePair<string, IIfcProduct> kv in _productsByGuid)
            {
                if (MatchesIfcType(kv.Value, ifcTypeName))
                {
                    list.AddRange(GetCachedGeometry(kv.Key, kv.Value, options).Solids);
                }
            }

            return list;
        }

        public IReadOnlyList<IfcProductGeometry> GetGeometriesByType(string ifcTypeName, IfcConvertOptions options)
        {
            if (string.IsNullOrWhiteSpace(ifcTypeName))
            {
                return Array.Empty<IfcProductGeometry>();
            }

            List<IfcProductGeometry> list = new List<IfcProductGeometry>();
            foreach (KeyValuePair<string, IIfcProduct> kv in _productsByGuid)
            {
                if (MatchesIfcType(kv.Value, ifcTypeName))
                {
                    list.Add(GetCachedGeometry(kv.Key, kv.Value, options));
                }
            }

            return list;
        }

        /// <summary>
        /// Products visited by model-wide queries: physical elements only, unless the options ask for everything.
        /// </summary>
        private IEnumerable<KeyValuePair<string, IIfcProduct>> ModelWideProducts(IfcConvertOptions options)
        {
            bool includeAll = options?.IncludeNonPhysicalProducts ?? false;
            return includeAll ? _productsByGuid : _productsByGuid.Where(kv => IfcTypeNames.IsPhysical(kv.Value));
        }

        public IReadOnlyList<GeoSolid3> GetAllSolids(IfcConvertOptions options)
        {
            List<GeoSolid3> list = new List<GeoSolid3>();
            foreach (KeyValuePair<string, IIfcProduct> kv in ModelWideProducts(options))
            {
                list.AddRange(GetCachedGeometry(kv.Key, kv.Value, options).Solids);
            }

            return list;
        }

        public IEnumerable<IfcProductGeometry> EnumerateGeometries(IfcConvertOptions options)
        {
            foreach (KeyValuePair<string, IIfcProduct> kv in ModelWideProducts(options))
            {
                yield return GetCachedGeometry(kv.Key, kv.Value, options);
            }
        }

        public IEnumerable<GeoSolid3> EnumerateSolids(IfcConvertOptions options)
        {
            foreach (KeyValuePair<string, IIfcProduct> kv in ModelWideProducts(options))
            {
                foreach (GeoSolid3 solid in GetCachedGeometry(kv.Key, kv.Value, options).Solids)
                {
                    yield return solid;
                }
            }
        }

        public IReadOnlyList<IfcProductMetadata> GetProductCatalog()
        {
            List<IfcProductMetadata> catalog = new List<IfcProductMetadata>(_productsByGuid.Count);
            foreach (IIfcProduct product in _productsByGuid.Values)
            {
                string typeName = IfcTypeNames.GetName(product);
                string tag = (product as IIfcElement)?.Tag?.ToString() ?? string.Empty;
                catalog.Add(new IfcProductMetadata(
                    product.GlobalId.ToString(),
                    product.Name?.ToString() ?? string.Empty,
                    typeName,
                    tag));
            }

            return catalog;
        }

        public IReadOnlyDictionary<string, IfcPropertySet> GetProperties(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return EmptyPropertySets;
            }

            if (!_productsByGuid.TryGetValue(guid, out IIfcProduct product))
            {
                return EmptyPropertySets;
            }

            return _propertyCache.GetOrAdd(guid, _ => new Lazy<IReadOnlyDictionary<string, IfcPropertySet>>(() => _propertyReader.Value.Read(product))).Value;
        }

        private static readonly IReadOnlyDictionary<string, IfcPropertySet> EmptyPropertySets
            = new Dictionary<string, IfcPropertySet>(StringComparer.OrdinalIgnoreCase);

        internal IIfcProduct GetProduct(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return null;
            }

            _productsByGuid.TryGetValue(guid, out IIfcProduct product);
            return product;
        }

        internal IEnumerable<T> GetProducts<T>() where T : IIfcProduct
        {
            return _productsByGuid.Values.OfType<T>();
        }

        private static bool MatchesIfcType(IIfcProduct product, string typeName)
        {
            return IfcTypeNames.IsOfType(product, typeName);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _store?.Dispose();
        }
    }
}

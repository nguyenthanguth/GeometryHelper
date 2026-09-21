using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using GeometryHelper.IfcConvert.Converters.Internal;
using GeometryHelper.IfcConvert.Core.Internal;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace GeometryHelper.IfcConvert.Core
{
    /// <summary>
    /// Thread-safe in-memory cache and high-level reader for opened IFC models.
    /// Encapsulates the xBIM engine and provides pure GeometryHelper geometry APIs.
    /// Exposes zero xBIM types in its public and member interface to allow seamless assembly resolution.
    /// </summary>
    public sealed class IfcStoreCache : IDisposable
    {
        private bool _disposed;
        private readonly IIfcSession _session;

        private static readonly Dictionary<string, IfcStoreCache> GlobalStores = new Dictionary<string, IfcStoreCache>(StringComparer.OrdinalIgnoreCase);
        private static readonly object CacheLock = new object();

        /// <summary>
        /// Gets the file path of the opened IFC model, if loaded from disk.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets or sets default conversion options for this cache instance.
        /// </summary>
        public IfcConvertOptions DefaultOptions { get; set; }

        /// <summary>
        /// Gets or sets whether this cache instance is managed in the global static cache.
        /// </summary>
        public bool IsGlobalCached { get; set; }

        /// <summary>
        /// Gets the number of products indexed in this store.
        /// </summary>
        public int Count => _session?.ProductCount ?? 0;

        /// <summary>
        /// Gets the number of products indexed in this store.
        /// </summary>
        public int ProductCount => _session?.ProductCount ?? 0;

        /// <summary>
        /// Gets the schema version of the opened IFC store (e.g. IFC2X3, IFC4).
        /// </summary>
        public string SchemaVersion => _session?.SchemaVersion ?? string.Empty;

        /// <summary>
        /// Gets the original length unit name reported by the IFC model (e.g. METRE, MILLIMETRE).
        /// </summary>
        public string OriginalLengthUnit => _session?.OriginalLengthUnit ?? string.Empty;

        /// <summary>
        /// Gets the GlobalIds that more than one product in the file uses. GlobalIds must be unique in IFC;
        /// when they are not, only the first product with the id can be reached by GUID-based queries.
        /// </summary>
        public IReadOnlyList<string> DuplicateGlobalIds => _session?.DuplicateGlobalIds ?? Array.Empty<string>();

        /// <summary>
        /// Internal accessor for the underlying session (for testing purposes).
        /// </summary>
        internal object Session => _session;

        /// <summary>
        /// Opens an IFC file and initializes a new <see cref="IfcStoreCache"/>.
        /// </summary>
        /// <param name="filePath">Path to the IFC file.</param>
        /// <param name="defaultOptions">Optional default conversion options.</param>
        /// <returns>A new <see cref="IfcStoreCache"/> instance.</returns>
        public static IfcStoreCache Open(string filePath, IfcConvertOptions defaultOptions = null)
        {
            return new IfcStoreCache(filePath) { DefaultOptions = defaultOptions };
        }

        /// <summary>
        /// Initializes a new cache by opening the specified IFC file.
        /// </summary>
        /// <param name="filePath">Path to the IFC file.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null or whitespace.</exception>
        public IfcStoreCache(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            FilePath = filePath;
            _session = CreateSession(filePath);
        }

        /// <summary>
        /// Initializes a new cache wrapping an existing <see cref="IfcStore"/> (internal).
        /// </summary>
        /// <param name="store">The opened or in-memory IFC store.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is null.</exception>
        internal IfcStoreCache(IfcStore store)
        {
            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            FilePath = store.FileName ?? string.Empty;
            _session = CreateSession(store);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IIfcSession CreateSession(string filePath)
        {
            return new XbimIfcSession(filePath);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IIfcSession CreateSession(IfcStore store)
        {
            return new XbimIfcSession(store);
        }

        /// <summary>
        /// Gets an existing cached store for the given file path, or creates and caches a new one.
        /// </summary>
        /// <param name="filePath">
        /// The full path to the IFC file, e.g. <c>C:\Models\Building.ifc</c> or <c>\\server\share\Building.ifc</c>.
        /// Different spellings of the same full path (<c>/</c> or <c>\</c>, <c>.</c> and <c>..</c> segments, letter case)
        /// share one cached model.
        /// </param>
        /// <returns>A cached <see cref="IfcStoreCache"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is not a full path.</exception>
        public static IfcStoreCache GetOrCreate(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            string key = ToCacheKey(filePath);

            lock (CacheLock)
            {
                if (GlobalStores.TryGetValue(key, out var cachedStore))
                {
                    return cachedStore;
                }

                var newStore = new IfcStoreCache(key)
                {
                    IsGlobalCached = true
                };
                GlobalStores[key] = newStore;
                return newStore;
            }
        }

        /// <summary>
        /// Returns the global cache key of a file: its full path in one canonical spelling.
        /// <para>
        /// Only full paths are accepted. A relative path would resolve against the current folder, which can change
        /// while the process runs, so the same text could name different files and one file could be cached twice.
        /// <see cref="Path.GetFullPath(string)"/> then unifies the spelling of the full path ('/' and '\', '.' and '..',
        /// repeated separators) without touching the disk; letter case is ignored by the cache dictionary.
        /// </para>
        /// </summary>
        private static string ToCacheKey(string filePath)
        {
            if (!IsFullPath(filePath))
            {
                throw new ArgumentException(
                    $"The global IFC cache needs the full path of the file, e.g. C:\\Models\\Building.ifc or \\\\server\\share\\Building.ifc, not '{filePath}'.",
                    nameof(filePath));
            }

            return Path.GetFullPath(filePath);
        }

        /// <summary>
        /// Checks that a path names a file without depending on the current drive or folder: a drive letter followed
        /// by a separator (<c>C:\...</c>) or a UNC path (<c>\\server\share\...</c>).
        /// <see cref="Path.IsPathRooted(string)"/> is not enough, since it also accepts <c>\Models\a.ifc</c> (current drive)
        /// and <c>C:a.ifc</c> (current folder of drive C:).
        /// </summary>
        private static bool IsFullPath(string path)
        {
            bool IsSeparator(char c) => c == '\\' || c == '/';

            if (path.Length >= 2 && IsSeparator(path[0]) && IsSeparator(path[1]))
            {
                return true;
            }

            return path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && IsSeparator(path[2]);
        }

        #region High-Level GeometryHelper APIs (Pure Geometry - No xBIM Types)

        /// <summary>
        /// Retrieves a single <see cref="GeoSolid3"/> by product GlobalId (GUID).
        /// If the product contains multiple solids, returns the largest body by volume.
        /// Returns null if not found or if the product has no closed solid representation.
        /// </summary>
        /// <param name="guid">The GlobalId (GUID) of the product.</param>
        /// <param name="options">Optional conversion options.</param>
        /// <returns>The converted <see cref="GeoSolid3"/>, or null.</returns>
        public GeoSolid3 GetSolid(string guid, IfcConvertOptions options = null)
        {
            return _session?.GetSolid(guid, ResolveOptions(options));
        }

        /// <summary>
        /// Retrieves all <see cref="GeoSolid3"/> bodies belonging to a product by its GlobalId (GUID).
        /// Returns an empty list if not found or if the product has no closed solid representation.
        /// </summary>
        /// <param name="guid">The GlobalId (GUID) of the product.</param>
        /// <param name="options">Optional conversion options.</param>
        /// <returns>A read-only list of converted <see cref="GeoSolid3"/> bodies.</returns>
        public IReadOnlyList<GeoSolid3> GetSolids(string guid, IfcConvertOptions options = null)
        {
            return _session?.GetSolids(guid, ResolveOptions(options)) ?? Array.Empty<GeoSolid3>();
        }

        /// <summary>
        /// Retrieves the complete <see cref="IfcProductGeometry"/> (including closed solids, open surfaces, and metadata) by GlobalId.
        /// </summary>
        /// <param name="guid">The GlobalId (GUID) of the product.</param>
        /// <param name="options">Optional conversion options.</param>
        /// <returns>The complete <see cref="IfcProductGeometry"/>, or null if not found.</returns>
        public IfcProductGeometry GetGeometry(string guid, IfcConvertOptions options = null)
        {
            return _session?.GetGeometry(guid, ResolveOptions(options));
        }

        /// <summary>
        /// Retrieves all <see cref="GeoSolid3"/> bodies for products of a specific IFC type (e.g. "IfcWall", "IfcBeam", "Wall").
        /// </summary>
        /// <param name="ifcTypeName">The IFC entity type name (with or without 'Ifc' prefix, case-insensitive).</param>
        /// <param name="options">Optional conversion options.</param>
        /// <returns>A list of all converted <see cref="GeoSolid3"/> bodies of that type.</returns>
        public IReadOnlyList<GeoSolid3> GetSolidsByType(string ifcTypeName, IfcConvertOptions options = null)
        {
            return _session?.GetSolidsByType(ifcTypeName, ResolveOptions(options)) ?? Array.Empty<GeoSolid3>();
        }

        /// <summary>
        /// Retrieves complete <see cref="IfcProductGeometry"/> representations for products of a specific IFC type.
        /// </summary>
        /// <param name="ifcTypeName">The IFC entity type name (with or without 'Ifc' prefix, case-insensitive).</param>
        /// <param name="options">Optional conversion options.</param>
        /// <returns>A list of <see cref="IfcProductGeometry"/> representations.</returns>
        public IReadOnlyList<IfcProductGeometry> GetGeometriesByType(string ifcTypeName, IfcConvertOptions options = null)
        {
            return _session?.GetGeometriesByType(ifcTypeName, ResolveOptions(options)) ?? Array.Empty<IfcProductGeometry>();
        }

        /// <summary>
        /// Converts and returns all <see cref="GeoSolid3"/> bodies present in the entire IFC model.
        /// </summary>
        /// <param name="options">Optional conversion options.</param>
        /// <returns>A list of all converted solids in the model.</returns>
        public IReadOnlyList<GeoSolid3> GetAllSolids(IfcConvertOptions options = null)
        {
            return _session?.GetAllSolids(ResolveOptions(options)) ?? Array.Empty<GeoSolid3>();
        }

        /// <summary>
        /// Lazily enumerates converted <see cref="IfcProductGeometry"/> instances across the model,
        /// minimizing memory usage for large files.
        /// </summary>
        /// <param name="options">Optional conversion options.</param>
        /// <returns>An enumerable sequence of product geometries.</returns>
        public IEnumerable<IfcProductGeometry> EnumerateGeometries(IfcConvertOptions options = null)
        {
            return _session?.EnumerateGeometries(ResolveOptions(options)) ?? Enumerable.Empty<IfcProductGeometry>();
        }

        /// <summary>
        /// Lazily enumerates converted <see cref="GeoSolid3"/> bodies across the model.
        /// </summary>
        /// <param name="options">Optional conversion options.</param>
        /// <returns>An enumerable sequence of solid bodies.</returns>
        public IEnumerable<GeoSolid3> EnumerateSolids(IfcConvertOptions options = null)
        {
            return _session?.EnumerateSolids(ResolveOptions(options)) ?? Enumerable.Empty<GeoSolid3>();
        }

        /// <summary>
        /// Retrieves a lightweight catalog of product metadata (GUID, Name, Type, Tag) without converting 3D geometry.
        /// </summary>
        /// <returns>A list of <see cref="IfcProductMetadata"/> records.</returns>
        public IReadOnlyList<IfcProductMetadata> GetProductCatalog()
        {
            return _session?.GetProductCatalog() ?? Array.Empty<IfcProductMetadata>();
        }

        #endregion

        #region Property Access APIs (No xBIM Types)

        /// <summary>
        /// Retrieves all IFC property sets and quantity sets for a product by its GlobalId.
        /// Returns an empty dictionary if the product is not found or has no property sets.
        /// </summary>
        /// <param name="guid">The GlobalId (GUID) of the product.</param>
        /// <returns>A dictionary of <see cref="IfcPropertySet"/> instances keyed by property set name (case-insensitive).</returns>
        public IReadOnlyDictionary<string, IfcPropertySet> GetProperties(string guid)
        {
            return _session?.GetProperties(guid) ?? EmptyPropertySets;
        }

        /// <summary>
        /// Retrieves a single property value from a named property set.
        /// Returns null if the product, property set, or property is not found.
        /// </summary>
        /// <param name="guid">The GlobalId (GUID) of the product.</param>
        /// <param name="psetName">The property set name (case-insensitive, e.g. "Pset_WallCommon").</param>
        /// <param name="propertyName">The property name (case-insensitive, e.g. "IsExternal").</param>
        /// <returns>The matching <see cref="IfcPropertyValue"/>, or null.</returns>
        public IfcPropertyValue GetProperty(string guid, string psetName, string propertyName)
        {
            var sets = GetProperties(guid);
            if (sets.TryGetValue(psetName ?? string.Empty, out IfcPropertySet pset))
            {
                pset.TryGetProperty(propertyName, out IfcPropertyValue value);
                return value;
            }

            return null;
        }

        private static readonly IReadOnlyDictionary<string, IfcPropertySet> EmptyPropertySets =
            new Dictionary<string, IfcPropertySet>(StringComparer.OrdinalIgnoreCase);

        #endregion

        private IfcConvertOptions ResolveOptions(IfcConvertOptions options)
        {
            IfcConvertOptions opts = options ?? DefaultOptions ?? new IfcConvertOptions();
            return _session?.ResolveUnitOptions(opts) ?? opts;
        }

        /// <summary>
        /// Releases resources and closes the underlying store if not globally cached.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (!IsGlobalCached)
            {
                _session?.Dispose();
            }
        }

        /// <summary>
        /// Clears all globally cached stores and disposes their underlying resources.
        /// </summary>
        public static void ClearGlobalCache()
        {
            lock (CacheLock)
            {
                foreach (var storeCache in GlobalStores.Values)
                {
                    storeCache._session?.Dispose();
                }

                GlobalStores.Clear();
            }
        }

        /// <summary>
        /// Clears and disposes a specific file from the global cache.
        /// </summary>
        /// <param name="filePath">The full path of the file to remove from cache, in any spelling accepted by <see cref="GetOrCreate"/>.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is not a full path.</exception>
        public static void ClearGlobalCache(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            string key = ToCacheKey(filePath);

            lock (CacheLock)
            {
                if (GlobalStores.TryGetValue(key, out var storeCache))
                {
                    storeCache._session?.Dispose();
                    GlobalStores.Remove(key);
                }
            }
        }

        /// <summary>
        /// Gets diagnostic information about all currently cached IFC stores.
        /// </summary>
        /// <returns>A list of file paths and their indexed product count.</returns>
        public static List<KeyValuePair<string, int>> GetGlobalCacheInfo()
        {
            lock (CacheLock)
            {
                return GlobalStores.Select(x => new KeyValuePair<string, int>(x.Key, x.Value.Count)).ToList();
            }
        }
    }
}

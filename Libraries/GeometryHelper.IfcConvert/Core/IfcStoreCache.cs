using System;
using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace GeometryHelper.IfcConvert.Core
{
    /// <summary>
    /// Thread-safe in-memory cache for opened IFC models, indexing products by GlobalId.
    /// </summary>
    public sealed class IfcStoreCache : IDisposable
    {
        private bool _disposed;

        private static readonly Dictionary<string, IfcStoreCache> GlobalStores = new Dictionary<string, IfcStoreCache>(StringComparer.OrdinalIgnoreCase);
        private static readonly object CacheLock = new object();

        /// <summary>
        /// Gets the underlying xBIM IFC store.
        /// </summary>
        public IfcStore Store { get; }

        /// <summary>
        /// Gets the dictionary of products indexed by their GlobalId.
        /// </summary>
        public IReadOnlyDictionary<string, IIfcProduct> ProductsByGuid { get; }

        /// <summary>
        /// Gets or sets whether this cache instance is managed in the global static cache.
        /// </summary>
        public bool IsGlobalCached { get; set; }

        /// <summary>
        /// Gets the number of products indexed in this store.
        /// </summary>
        public int Count => ProductsByGuid.Count;

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

            Store = IfcStore.Open(filePath);

            // GroupBy + First instead of direct ToDictionary: some IFC files contain duplicate GlobalIds
            // contrary to the specification. GroupBy safely takes the first record instead of throwing.
            ProductsByGuid = Store.Instances
                .OfType<IIfcProduct>()
                .Where(x => !string.IsNullOrWhiteSpace(x.GlobalId))
                .GroupBy(x => x.GlobalId.ToString())
                .ToDictionary(g => g.Key, g => g.First());
        }

        /// <summary>
        /// Gets an existing cached store for the given file path, or creates and caches a new one.
        /// </summary>
        /// <param name="filePath">The path to the IFC file.</param>
        /// <returns>A cached <see cref="IfcStoreCache"/> instance.</returns>
        public static IfcStoreCache GetOrCreate(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            lock (CacheLock)
            {
                if (GlobalStores.TryGetValue(filePath, out var cachedStore))
                {
                    return cachedStore;
                }

                var newStore = new IfcStoreCache(filePath)
                {
                    IsGlobalCached = true
                };
                GlobalStores[filePath] = newStore;
                return newStore;
            }
        }

        /// <summary>
        /// Retrieves an <see cref="IIfcProduct"/> by its GlobalId.
        /// </summary>
        /// <param name="guid">The GlobalId (GUID) of the product.</param>
        /// <returns>The product if found; otherwise, null.</returns>
        public IIfcProduct GetProduct(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return null;
            }

            ProductsByGuid.TryGetValue(guid, out IIfcProduct product);
            return product;
        }

        /// <summary>
        /// Retrieves all products of a specific IFC type (e.g. IfcWall, IfcBeam).
        /// </summary>
        /// <typeparam name="T">The IFC product interface type.</typeparam>
        /// <returns>Sequence of matching products.</returns>
        public IEnumerable<T> GetProducts<T>() where T : IIfcProduct
        {
            return ProductsByGuid.Values.OfType<T>();
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
                Store?.Dispose();
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
                    storeCache.Store?.Dispose();
                }
                GlobalStores.Clear();
            }
        }

        /// <summary>
        /// Clears and disposes a specific file from the global cache.
        /// </summary>
        /// <param name="filePath">The file path to remove from cache.</param>
        public static void ClearGlobalCache(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            lock (CacheLock)
            {
                if (GlobalStores.TryGetValue(filePath, out var storeCache))
                {
                    storeCache.Store?.Dispose();
                    GlobalStores.Remove(filePath);
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

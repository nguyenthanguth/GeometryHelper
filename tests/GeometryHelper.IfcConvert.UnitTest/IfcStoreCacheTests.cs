using System;
using System.Linq;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Core.Internal;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.IO;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    // Shares the IfcEngine collection with the other IFC tests: GlobalCache_ClearGlobalCache_Works empties the
    // process-wide cache, which must not happen while another test is holding a cached model.
    [Collection("IfcEngine")]
    public class IfcStoreCacheTests
    {
        private static XbimEditorCredentials CreateCredentials()
        {
            return new XbimEditorCredentials
            {
                ApplicationDevelopersName = "GeometryHelper",
                ApplicationFullName = "GeometryHelper.UnitTest",
                ApplicationIdentifier = "GH",
                ApplicationVersion = "3.2.0",
                EditorsFamilyName = "Unit",
                EditorsGivenName = "Test",
                EditorsOrganisationName = "GeometryHelper"
            };
        }

        [Fact]
        public void InMemoryStore_IndexesProductsByGuid()
        {
            var credentials = CreateCredentials();
            using (var store = IfcStore.Create(credentials, XbimSchemaVersion.Ifc4, XbimStoreType.InMemoryModel))
            {
                string wallGuid;
                using (var txn = store.BeginTransaction("Add wall"))
                {
                    var wall = store.Instances.New<Xbim.Ifc4.SharedBldgElements.IfcWall>();
                    wall.Name = "Wall_01";
                    wallGuid = wall.GlobalId;
                    txn.Commit();
                }

                using (var cache = new IfcStoreCache(store))
                {
                    Assert.NotNull(cache.GetProductsByGuid());
                    Assert.True(cache.GetProductsByGuid().ContainsKey(wallGuid));

                    var retrieved = cache.GetProduct(wallGuid);
                    Assert.NotNull(retrieved);
                    Assert.Equal("Wall_01", retrieved.Name?.ToString());
                }
            }
        }

        [Fact]
        public void GetProducts_GenericFilter_ReturnsCorrectType()
        {
            var credentials = CreateCredentials();
            using (var store = IfcStore.Create(credentials, XbimSchemaVersion.Ifc4, XbimStoreType.InMemoryModel))
            {
                using (var txn = store.BeginTransaction("Add elements"))
                {
                    var wall1 = store.Instances.New<Xbim.Ifc4.SharedBldgElements.IfcWall>();
                    wall1.Name = "Wall_1";

                    var wall2 = store.Instances.New<Xbim.Ifc4.SharedBldgElements.IfcWall>();
                    wall2.Name = "Wall_2";

                    var beam = store.Instances.New<Xbim.Ifc4.SharedBldgElements.IfcBeam>();
                    beam.Name = "Beam_1";

                    txn.Commit();
                }

                using (var cache = new IfcStoreCache(store))
                {
                    var walls = cache.GetProducts<IIfcWall>().ToList();
                    var beams = cache.GetProducts<IIfcBeam>().ToList();

                    Assert.Equal(2, walls.Count);
                    Assert.Single(beams);
                    Assert.Equal("Beam_1", beams[0].Name?.ToString());
                }
            }
        }

        [Fact]
        public void GlobalCache_ClearGlobalCache_Works()
        {
            IfcStoreCache.ClearGlobalCache();
        }

        [Fact]
        public void HighLevelApi_GetProductCatalog_ReturnsMetadata()
        {
            var credentials = CreateCredentials();
            using (var store = IfcStore.Create(credentials, XbimSchemaVersion.Ifc4, XbimStoreType.InMemoryModel))
            {
                using (var txn = store.BeginTransaction("Add elements"))
                {
                    var wall = store.Instances.New<Xbim.Ifc4.SharedBldgElements.IfcWall>();
                    wall.Name = "Concrete_Wall";

                    var beam = store.Instances.New<Xbim.Ifc4.SharedBldgElements.IfcBeam>();
                    beam.Name = "Steel_Beam";

                    txn.Commit();
                }

                using (var cache = new IfcStoreCache(store))
                {
                    var catalog = cache.GetProductCatalog();
                    Assert.Equal(2, catalog.Count);

                    var wallMeta = catalog.FirstOrDefault(x => x.Name == "Concrete_Wall");
                    Assert.NotNull(wallMeta);
                    Assert.Equal("IfcWall", wallMeta.IfcType);

                    var beamMeta = catalog.FirstOrDefault(x => x.Name == "Steel_Beam");
                    Assert.NotNull(beamMeta);
                    Assert.Equal("IfcBeam", beamMeta.IfcType);
                }
            }
        }
    }
}

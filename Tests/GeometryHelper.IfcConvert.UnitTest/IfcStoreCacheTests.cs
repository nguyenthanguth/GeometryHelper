using System;
using System.Linq;
using GeometryHelper.IfcConvert.Core;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.IO;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    public class IfcStoreCacheTests
    {
        private static XbimEditorCredentials CreateCredentials()
        {
            return new XbimEditorCredentials
            {
                ApplicationDevelopersName = "GeometryHelper",
                ApplicationFullName = "GeometryHelper.UnitTest",
                ApplicationIdentifier = "GH",
                ApplicationVersion = "3.1.0",
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
                    Assert.NotNull(cache.ProductsByGuid);
                    Assert.True(cache.ProductsByGuid.ContainsKey(wallGuid));

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
    }
}

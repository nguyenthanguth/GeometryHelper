using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Geometry;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Models;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    /// <summary>
    /// <see cref="IfcStoreCache.GetAllGeometries"/> converts a whole model on every core at once where the file is held
    /// in memory: it has to give what <see cref="IfcStoreCache.EnumerateGeometries"/> gives one product at a time,
    /// product for product, in the same order, coordinate for coordinate.
    /// </summary>
    [Collection("IfcEngine")]
    public class AllGeometriesTests
    {
        private static string PathOf(string fileName) =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", fileName);

        /// <summary>
        /// Every coordinate of a product's bodies, openings and open surfaces, in order, and its warnings.
        /// </summary>
        private static string Text(IfcProductGeometry product)
        {
            var text = new StringBuilder(product.GlobalId);

            void Point(GeoPoint3 p)
            {
                text.Append(p.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(p.Y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(p.Z.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            }

            void Face(GeoFace3 face)
            {
                foreach (GeoPoint3 p in face.Boundary.Vertices)
                {
                    Point(p);
                }

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    text.Append('h');

                    foreach (GeoPoint3 p in hole.Vertices)
                    {
                        Point(p);
                    }
                }

                text.Append('|');
            }

            void Solid(GeoSolid3 solid)
            {
                text.Append('S');

                foreach (GeoFace3 face in solid.Faces)
                {
                    Face(face);
                }

                foreach (GeoSolid3 opening in solid.Openings)
                {
                    text.Append('O');
                    Solid(opening);
                }
            }

            foreach (GeoSolid3 solid in product.Solids)
            {
                Solid(solid);
            }

            foreach (GeoFace3 face in product.OpenSurfaces)
            {
                text.Append('F');
                Face(face);
            }

            foreach (string warning in product.Warnings)
            {
                text.Append('W').Append(warning);
            }

            return text.ToString();
        }

        [Theory]
        [InlineData("House.ifc", false)]
        [InlineData("House.ifc", true)]
        [InlineData("4walls1floorSite.ifc", true)]
        [InlineData("IFC4TessellationComplex.ifc", false)]
        public void AllGeometries_AreWhatEnumeratingGives_InItsOrder(string fileName, bool applyVoids)
        {
            var options = new IfcConvertOptions { ApplyVoids = applyVoids };

            // Opened twice, so that neither answer comes out of the other's cache.
            using (IfcStoreCache atOnce = IfcStoreCache.Open(PathOf(fileName)))
            using (IfcStoreCache oneByOne = IfcStoreCache.Open(PathOf(fileName)))
            {
                Assert.True(atOnce.IsInMemory, "the file is converted in parallel only when it is held in memory");

                IReadOnlyList<IfcProductGeometry> all = atOnce.GetAllGeometries(options);
                List<IfcProductGeometry> enumerated = oneByOne.EnumerateGeometries(options).ToList();

                Assert.NotEmpty(all);
                Assert.Equal(enumerated.Select(product => product.GlobalId), all.Select(product => product.GlobalId));
                Assert.Equal(enumerated.Select(Text), all.Select(Text));
            }
        }

        /// <summary>
        /// The geometry engine hands the shells of one faceted brep back in an order that is not the same from one
        /// reading of a file to the next (House.ifc's railings are one brep each, cut into several shells). The bodies of
        /// each product come back in one order all the same, however many times the file is read.
        /// </summary>
        [Fact]
        public void TheSameFileReadAgain_GivesTheSameBodies_InTheSameOrder()
        {
            var options = new IfcConvertOptions { ApplyVoids = true };
            List<string> first = null;

            for (int reading = 0; reading < 4; reading++)
            {
                using (IfcStoreCache model = IfcStoreCache.Open(PathOf("House.ifc")))
                {
                    List<string> answers = model.EnumerateGeometries(options).Select(Text).ToList();

                    if (first == null)
                    {
                        first = answers;
                    }
                    else
                    {
                        Assert.Equal(first, answers);
                    }
                }
            }
        }

        [Fact]
        public void AllGeometries_InAToleranceScope_AreWhatEnumeratingGivesInIt()
        {
            using (Tolerance.Use(new Tolerance(1e-4, 1e-4)))
            using (IfcStoreCache atOnce = IfcStoreCache.Open(PathOf("House.ifc")))
            using (IfcStoreCache oneByOne = IfcStoreCache.Open(PathOf("House.ifc")))
            {
                var options = new IfcConvertOptions { ApplyVoids = true };

                Assert.Equal(oneByOne.EnumerateGeometries(options).Select(Text), atOnce.GetAllGeometries(options).Select(Text));
            }
        }

        [Fact]
        public void AllGeometries_TakeTheFiltersOfTheOptions_AsEnumeratingDoes()
        {
            using (IfcStoreCache atOnce = IfcStoreCache.Open(PathOf("House.ifc")))
            using (IfcStoreCache oneByOne = IfcStoreCache.Open(PathOf("House.ifc")))
            {
                // Everything, not only the physical products; and the same file read with no options at all.
                var everything = new IfcConvertOptions { IncludeNonPhysicalProducts = true };

                Assert.Equal(oneByOne.EnumerateGeometries(everything).Select(Text), atOnce.GetAllGeometries(everything).Select(Text));
                Assert.Equal(oneByOne.EnumerateGeometries().Select(Text), atOnce.GetAllGeometries().Select(Text));
                Assert.True(atOnce.GetAllGeometries(everything).Count > atOnce.GetAllGeometries().Count);
            }
        }

        [Fact]
        public void AllSolids_AreTheBodiesOfEveryProduct_InOrder()
        {
            using (IfcStoreCache atOnce = IfcStoreCache.Open(PathOf("House.ifc")))
            using (IfcStoreCache oneByOne = IfcStoreCache.Open(PathOf("House.ifc")))
            {
                var options = new IfcConvertOptions { ApplyVoids = true };

                IReadOnlyList<GeoSolid3> all = atOnce.GetAllSolids(options);
                List<GeoSolid3> enumerated = oneByOne.EnumerateSolids(options).ToList();

                Assert.NotEmpty(all);
                Assert.Equal(enumerated.Count, all.Count);
                Assert.Equal(enumerated.Select(solid => solid.GetVolume()), all.Select(solid => solid.GetVolume()));
                Assert.Equal(enumerated.Select(solid => solid.Faces.Count), all.Select(solid => solid.Faces.Count));
            }
        }
    }
}

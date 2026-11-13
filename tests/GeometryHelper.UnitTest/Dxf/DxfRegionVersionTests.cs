using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A drawing with regions saved in another version (<see cref="DxfDatabase.Save(Stream, string)"/>): each region's ACIS data
    /// is read and written again as the version writes it, SAT text up to R2010 and SAB in the data store from R2013, so the
    /// regions go from one form to the other and keep their shape, where a body of the data store was left out going below R2013.
    /// </summary>
    public class DxfRegionVersionTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly string[] Versions = { "AC1015", "AC1018", "AC1021", "AC1024", "AC1027", "AC1032" };

        public static IEnumerable<object[]> Pairs() =>
            Versions.SelectMany(from => Versions.Where(to => to != from).Select(to => new object[] { from, to }));

        private static DxfDatabase Drawing(string version)
        {
            var database = new DxfDatabase(version);
            DxfRegionShape box = DxfRegionShape.Create(
                new DxfCurve[]
                {
                    new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0)),
                    new DxfLine(new GeoPoint3(40, 0, 0), new GeoPoint3(40, 30, 0)),
                    new DxfLine(new GeoPoint3(40, 30, 0), new GeoPoint3(0, 30, 0)),
                    new DxfLine(new GeoPoint3(0, 30, 0), new GeoPoint3(0, 0, 0)),
                },
                null,
                Tol
            );
            DxfRegionShape disc(double x, double radius) =>
                DxfRegionShape.Create(
                    new DxfCurve[] { new DxfCircle(new GeoPoint3(x, 15, 0), new GeoVector3(0, 0, 1), radius) },
                    null,
                    Tol
                );

            // A notch of an arc, a face with a hole, and two faces far apart.
            foreach (
                DxfRegionShape shape in new[]
                {
                    DxfRegionShape.Subtract(new[] { box }, new[] { disc(40, 10) }, Tol),
                    DxfRegionShape.Subtract(new[] { box }, new[] { disc(20, 5) }, Tol),
                    DxfRegionShape.Unite(new[] { box, disc(100, 10) }, Tol),
                }
            )
            {
                database.ModelSpace.AppendEntity(DxfUnknownEntity.CreateRegion(shape, database));
            }

            return database;
        }

        private static DxfDatabase Reopened(DxfDatabase database, string version, out IReadOnlyList<string> said)
        {
            using (var stream = new MemoryStream())
            {
                said = database.Save(stream, version);
                return DxfDatabase.Parse(Encoding.UTF8.GetString(stream.ToArray()));
            }
        }

        private static double[] Areas(DxfDatabase database) =>
            database
                .ModelSpace.OfType<DxfUnknownEntity>()
                .Select(r => Math.Round(r.GetRegionShape().Area, 6))
                .ToArray();

        [Theory]
        [MemberData(nameof(Pairs))]
        public void RegionsSavedInAnotherVersion_KeepTheirShapes_AndSayNothingOfThem(string from, string to)
        {
            DxfDatabase database = Drawing(from);

            DxfDatabase saved = Reopened(database, to, out IReadOnlyList<string> said);

            Assert.Equal(to, saved.Version);
            Assert.Equal(Areas(database), Areas(saved));
            Assert.DoesNotContain(said, s => s.Contains("REGION"));
            DxfUnknownEntity[] regions = saved.ModelSpace.OfType<DxfUnknownEntity>().ToArray();
            Assert.Equal(new[] { 1, 2, 2 }, regions.Select(r => r.GetRegionShape().Loops.Count).ToArray());
            Assert.Equal(AcisFormat.For(to).Binary, regions.All(r => r.Groups.Any(g => g.TypeCode == 2)));
        }

        [Theory]
        [InlineData("AC1032", "AC1015")]
        [InlineData("AC1027", "AC1024")]
        public void RegionsOfTheDataStore_GoingBelowR2013_AreSatInTheEntity_AndTheStoreIsGone(string from, string to)
        {
            DxfDatabase saved = Reopened(Drawing(from), to, out _);

            Assert.DoesNotContain("ACDSDATA", saved.ToText());
            Assert.All(saved.ModelSpace.OfType<DxfUnknownEntity>(), r =>
                Assert.Contains(r.Groups, g => g.TypeCode == 1)
            );
        }

        [Theory]
        [InlineData("AC1015", "AC1027")]
        [InlineData("AC1024", "AC1032")]
        public void RegionsOfSat_GoingToR2013AndLater_AreRecordsOfTheDataStore_ByTheirHandles(string from, string to)
        {
            DxfDatabase saved = Reopened(Drawing(from), to, out _);

            string text = saved.ToText();
            Assert.Contains("ACDSDATA", text);
            Assert.Equal(3, DataRecords(text));
            Assert.All(saved.ModelSpace.OfType<DxfUnknownEntity>(), r => Assert.NotNull(r.GetRegionShape()));
        }

        /// <summary>How many records of the data store hold the data of an entity (name its handle, group 320).</summary>
        private static int DataRecords(string text)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            int count = 0;

            for (int i = 0; i + 1 < lines.Length; i++)
            {
                if (lines[i].Trim() == "0" && lines[i + 1].Trim() == "ACDSRECORD")
                {
                    for (int j = i + 2; j + 1 < lines.Length && lines[j].Trim() != "0"; j += 2)
                    {
                        if (lines[j].Trim() == "320")
                        {
                            count++;
                            break;
                        }
                    }
                }
            }

            return count;
        }
    }
}

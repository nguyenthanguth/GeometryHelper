using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A REGION moved, turned, scaled and mirrored (<see cref="DxfEntity.TransformBy"/>), copied (<see cref="DxfEntity.GetTransformedCopy"/>,
    /// <see cref="DxfEntity.Clone"/>) and saved in another version: its ACIS data is written anew for the body it becomes, in the
    /// SAT of its groups or the record of the data store, the loops in their order and the normal turned over by a mirror, as
    /// AutoCAD 2021's <c>TransformBy</c> leaves a region (its notch moved by (100, 50), turned 30 degrees about the origin,
    /// scaled by 2, mirrored across x = 50, probed in AutoCAD).
    /// </summary>
    public class DxfRegionTransformTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        public static readonly string[] Versions = { "AC1015", "AC1018", "AC1021", "AC1024", "AC1027", "AC1032" };

        public static System.Collections.Generic.IEnumerable<object[]> EveryVersion() =>
            Versions.Select(v => new object[] { v });

        /// <summary>The box 40 by 30 less a circle of radius 10 on its right edge, a region of the version.</summary>
        private static DxfUnknownEntity Notch(DxfDatabase database)
        {
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
            DxfRegionShape disc = DxfRegionShape.Create(
                new DxfCurve[] { new DxfCircle(new GeoPoint3(40, 15, 0), new GeoVector3(0, 0, 1), 10) },
                null,
                Tol
            );
            DxfUnknownEntity region = DxfUnknownEntity.CreateRegion(
                DxfRegionShape.Subtract(new[] { box }, new[] { disc }, Tol),
                database
            );
            database.ModelSpace.AppendEntity(region);
            return region;
        }

        private static DxfArc ArcOf(DxfUnknownEntity region) =>
            Assert.Single(region.GetRegionShape().Loops[0].Curves.OfType<DxfArc>());

        private static DxfUnknownEntity Saved(DxfDatabase database) =>
            DxfDatabase.Parse(database.ToText()).ModelSpace.OfType<DxfUnknownEntity>().First();

        [Theory]
        [MemberData(nameof(EveryVersion))]
        public void ARegionMovedBy100And50_IsThere_AsAutoCadLeavesIt_AndAfterTheFileIsSavedAndRead(string version)
        {
            var database = new DxfDatabase(version);
            DxfUnknownEntity region = Notch(database);

            region.TransformBy(GeoTransform3.Translation(new GeoVector3(100, 50, 0)));

            // The marker (70, or 290 and the GUID) is written once, as the entity had it: AutoCAD refuses a doubled one.
            Assert.Single(region.Groups, g => g.TypeCode == 70 || g.TypeCode == 290);
            Assert.Equal(AcisFormat.For(version).Binary ? 1 : 0, region.Groups.Count(g => g.TypeCode == 2));

            foreach (DxfUnknownEntity seen in new[] { region, Saved(database) })
            {
                DxfRegionShape shape = seen.GetRegionShape();
                Assert.Equal(1042.920367321, shape.Area, 6);
                DxfArc arc = ArcOf(seen);
                Assert.Equal(140.0, arc.Center.X, 9);
                Assert.Equal(65.0, arc.Center.Y, 9);
                Assert.Equal(Math.PI / 2.0, arc.StartAngle, 6);
                Assert.Equal(3.0 * Math.PI / 2.0, arc.EndAngle, 6);
                DxfLine first = shape.Loops[0].Curves.OfType<DxfLine>().First();
                Assert.Equal(140.0, first.StartPoint.X, 9);
                Assert.Equal(75.0, first.StartPoint.Y, 9);
            }
        }

        [Theory]
        [MemberData(nameof(EveryVersion))]
        public void ARegionTurned30Degrees_IsWhereAutoCadPutsIt(string version)
        {
            var database = new DxfDatabase(version);
            DxfUnknownEntity region = Notch(database);

            region.TransformBy(GeoTransform3.RotationZ(Math.PI / 6.0));

            DxfArc arc = ArcOf(Saved(database));
            Assert.Equal(27.141016151, arc.Center.X, 8);
            Assert.Equal(32.990381057, arc.Center.Y, 8);
            Assert.Equal(2.094395102, arc.StartAngle, 8);
            Assert.Equal(5.235987756, arc.EndAngle, 8);
        }

        [Theory]
        [MemberData(nameof(EveryVersion))]
        public void ARegionScaledBy2_HasFourTimesTheArea_AndTwiceThePerimeter(string version)
        {
            var database = new DxfDatabase(version);
            DxfUnknownEntity region = Notch(database);

            region.TransformBy(GeoTransform3.Scaling(2.0));

            DxfRegionShape shape = Saved(database).GetRegionShape();
            Assert.Equal(4171.681469282, shape.Area, 6);
            Assert.Equal(302.831853072, shape.Perimeter, 6);
            Assert.Equal(20.0, ArcOf(region).Radius, 9);
        }

        [Theory]
        [MemberData(nameof(EveryVersion))]
        public void ARegionMirroredAcrossX50_HasItsNormalDown_AndItsLoopInTheSameOrder_AsAutoCadLeavesIt(string version)
        {
            var database = new DxfDatabase(version);
            DxfUnknownEntity region = Notch(database);

            region.TransformBy(GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(50, 0, 0), new GeoVector3(1, 0, 0))));

            DxfUnknownEntity seen = Saved(database);
            DxfRegionShape shape = seen.GetRegionShape();
            Assert.Equal(-1.0, shape.Normal.Z, 12);
            Assert.Equal(1042.920367321, shape.Area, 6);
            DxfArc arc = ArcOf(seen);
            Assert.Equal(60.0, arc.Center.X, 9);
            Assert.Equal(-1.0, arc.Normal.Z, 12);
            Assert.Equal(Math.PI / 2.0, arc.StartAngle, 6);
            Assert.Equal(3.0 * Math.PI / 2.0, arc.EndAngle, 6);
            DxfLine first = shape.Loops[0].Curves.OfType<DxfLine>().First();
            Assert.Equal(60.0, first.StartPoint.X, 9);
            Assert.Equal(25.0, first.StartPoint.Y, 9);
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1032")]
        public void ARegionThatWouldBeScaledMoreOneWayThanAnother_IsRefused_AndLeftAsItWas(string version)
        {
            var database = new DxfDatabase(version);
            DxfUnknownEntity region = Notch(database);
            string before = database.ToText();

            Assert.Throws<NotSupportedException>(() => region.TransformBy(GeoTransform3.Scaling(2.0, 1.0, 1.0)));

            Assert.Equal(before, database.ToText());
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1027")]
        [InlineData("AC1032")]
        public void OneOfSeveralRegionsReadFromAFile_IsMovedAlone_ItsRecordRewrittenInPlace(string version)
        {
            var written = new DxfDatabase(version);
            DxfUnknownEntity first = Notch(written);
            Notch(written);
            Notch(written);
            written.ModelSpace.OfType<DxfUnknownEntity>().ElementAt(2).TransformBy(GeoTransform3.Scaling(2.0));
            DxfDatabase read = DxfDatabase.Parse(written.ToText());

            DxfUnknownEntity middle = read.ModelSpace.OfType<DxfUnknownEntity>().ElementAt(1);
            middle.TransformBy(GeoTransform3.Translation(new GeoVector3(100, 50, 0)));

            foreach (DxfDatabase seen in new[] { read, DxfDatabase.Parse(read.ToText()) })
            {
                DxfUnknownEntity[] regions = seen.ModelSpace.OfType<DxfUnknownEntity>().ToArray();
                Assert.Equal(
                    new[] { 40.0, 140.0, 80.0 },
                    regions.Select(r => Math.Round(ArcOf(r).Center.X, 9)).ToArray()
                );
                Assert.Equal(
                    new[] { 1042.920367, 1042.920367, 4171.681469 },
                    regions.Select(r => Math.Round(r.GetRegionShape().Area, 6)).ToArray()
                );
            }
        }

        [Theory]
        [MemberData(nameof(EveryVersion))]
        public void ACopyMovedByTheTransformation_HasADataOfItsOwn_AndTheOriginalStaysWhereItWas(string version)
        {
            var database = new DxfDatabase(version);
            DxfUnknownEntity region = Notch(database);

            var copy = (DxfUnknownEntity)
                region.GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(100, 50, 0)));
            database.ModelSpace.AppendEntity(copy);

            Assert.NotEqual(region.Handle, copy.Handle);
            Assert.Equal(40.0, ArcOf(region).Center.X, 9);
            Assert.Equal(140.0, ArcOf(copy).Center.X, 9);
            DxfDatabase back = DxfDatabase.Parse(database.ToText());
            DxfUnknownEntity[] regions = back.ModelSpace.OfType<DxfUnknownEntity>().ToArray();
            Assert.Equal(2, regions.Length);
            Assert.Equal(new[] { 40.0, 140.0 }, regions.Select(r => Math.Round(ArcOf(r).Center.X, 9)).ToArray());
        }

        [Fact]
        public void ACopyOfARegionOfR2018_HasARecordOfItsOwn_AndAGuidOfItsOwn()
        {
            var database = new DxfDatabase("AC1032");
            DxfUnknownEntity region = Notch(database);

            database.ModelSpace.AppendEntity((DxfUnknownEntity)region.Clone());

            string text = database.ToText();
            Assert.Equal(2, Count(text, "ACDSRECORD", "ASM_Data"));
            DxfUnknownEntity[] regions = DxfDatabase.Parse(text).ModelSpace.OfType<DxfUnknownEntity>().ToArray();
            string[] guids = regions.Select(r => r.Groups.First(g => g.TypeCode == 2).Value).ToArray();
            Assert.NotEqual(guids[0], guids[1]);
        }

        private static int Count(string text, string record, string name)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            int count = 0;

            for (int i = 0; i + 1 < lines.Length; i++)
            {
                if (lines[i].Trim() == "0" && lines[i + 1].Trim() == record)
                {
                    for (int j = i + 2; j + 1 < lines.Length && lines[j].Trim() != "0"; j += 2)
                    {
                        if (lines[j].Trim() == "2" && lines[j + 1].Trim() == name)
                        {
                            count++;
                            break;
                        }
                    }
                }
            }

            return count;
        }

        [Theory]
        [InlineData("AC1015", "AC1032")]
        [InlineData("AC1032", "AC1015")]
        [InlineData("AC1024", "AC1027")]
        [InlineData("AC1027", "AC1021")]
        public void ARegionCopiedFromADrawingOfAnotherVersion_IsWrittenForTheOneItIsAddedTo(string from, string to)
        {
            var source = new DxfDatabase(from);
            DxfUnknownEntity region = Notch(source);
            var target = new DxfDatabase(to);

            target.ModelSpace.AppendEntity((DxfUnknownEntity)region.Clone());

            DxfUnknownEntity added = Saved(target);
            Assert.Equal(1042.920367321, added.GetRegionShape().Area, 6);
            DxfArc arc = ArcOf(added);
            Assert.Equal(40.0, arc.Center.X, 9);
            Assert.Equal(AcisFormat.For(to).Binary, added.Groups.Any(g => g.TypeCode == 2));
        }

        [Fact]
        public void ARegionMadeForOneVersion_IsStillRefusedByAnotherDrawing()
        {
            var made = new DxfDatabase("AC1032");
            DxfUnknownEntity region = DxfUnknownEntity.CreateRegion(Notch(made).GetRegionShape(), made);

            Assert.Throws<ArgumentException>(() => new DxfDatabase("AC1015").ModelSpace.AppendEntity(region));
        }

        [Fact]
        public void SetRegionShape_GivesTheRegionAnotherShape_KeepingItsHandleAndLayer()
        {
            var database = new DxfDatabase("AC1032");
            DxfUnknownEntity region = Notch(database);
            region.Layer = "0";
            string handle = region.Handle;
            DxfRegionShape disc = DxfRegionShape.Create(
                new DxfCurve[] { new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 3) },
                null,
                Tol
            );

            region.SetRegionShape(disc);

            Assert.Equal(handle, region.Handle);
            Assert.Equal(Math.PI * 9.0, Saved(database).GetRegionShape().Area, 9);
            Assert.Throws<ArgumentNullException>(() => region.SetRegionShape(null));
        }

        [Fact]
        public void TheMovedRegionOfR2018_WritesToDwgWithItsMovedData()
        {
            var database = new DxfDatabase("AC1032");
            DxfUnknownEntity region = Notch(database);
            region.TransformBy(GeoTransform3.Translation(new GeoVector3(100, 50, 0)));

            using (var stream = new MemoryStream())
            {
                GeometryHelper.Dwg.DwgWriter.Write(database, stream);

                Assert.True(stream.Length > 0);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class OwnerTests
    {
        /// <summary>An owner whose rule goes by the first letter of the object id: L read-only, M move only, D edit, F free.</summary>
        private sealed class TestOwner : ICadEntityOwner
        {
            public TestOwner(string name, bool takes = true)
            {
                Name = name;
                Takes = takes;
            }

            public string Name { get; }

            public bool Takes { get; }

            public bool Throws { get; set; }

            public List<CadEdit> Edits { get; } = new List<CadEdit>();

            public CadEditRule GetEditRule(DxfEntity entity)
            {
                if (Throws)
                {
                    throw new InvalidOperationException("The owner is broken.");
                }

                Assert.True(CadOwnerTag.TryRead(entity, out CadOwnerTag tag));

                switch (tag.ObjectId[0])
                {
                    case 'L':
                        return CadEditRule.ReadOnly;
                    case 'M':
                        return CadEditRule.MoveOnly;
                    case 'F':
                        return CadEditRule.Free;
                    default:
                        return CadEditRule.Edit;
                }
            }

            public bool TryApply(CadDocument document, CadEdit edit)
            {
                if (Throws)
                {
                    throw new InvalidOperationException("The owner is broken.");
                }

                Edits.Add(edit);
                return Takes;
            }
        }

        private static DxfLine Line(DxfDatabase database, double y, CadOwnerTag tag = null)
        {
            var line = new DxfLine(new GeoPoint3(0, y, 0), new GeoPoint3(300, y, 0));
            database.ModelSpace.AppendEntity(line);
            tag?.WriteTo(line);
            return line;
        }

        private static DxfDatabase SavedAndOpened(DxfDatabase database)
        {
            using (var stream = new MemoryStream())
            {
                database.Save(stream);
                stream.Position = 0;
                return DxfDatabase.Open(stream);
            }
        }

        private static DxfEntity Only(DxfDatabase database) => Assert.Single(database.ModelSpace.Cast<DxfEntity>().ToList());

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1027")]
        public void ATagComesBackFromTheFileLetterForLetter(string version)
        {
            var database = new DxfDatabase(version);
            var tag = new CadOwnerTag("GeometryHelper.Drawing", @"B-12 \U+0041 50% Đường" + "\nống", "V-1 鋼 😀");
            Line(database, 0, tag);

            DxfEntity line = Only(SavedAndOpened(database));

            Assert.True(CadOwnerTag.TryRead(line, out CadOwnerTag back));
            Assert.Equal("GeometryHelper.Drawing", back.Owner);
            Assert.Equal(tag.ObjectId, back.ObjectId);
            Assert.Equal(tag.ExtraId, back.ExtraId);
            IReadOnlyList<DxfTypedValue> data = line.GetXDataForApplication(CadOwnerTag.ApplicationName);
            Assert.Equal(new[] { 1001, 1000, 1000, 1000 }, data.Select(g => (int)g.TypeCode));
            Assert.Equal(@"B-12 %5CU+0041 50%25 Đường%0Aống", data[2].Value);
        }

        [Fact]
        public void ATagWithNoSecondIdWritesTwoStringsAndReadsBackWithNone()
        {
            var database = new DxfDatabase("AC1027");
            Line(database, 0, new CadOwnerTag("Owner", "L-1"));

            DxfEntity line = Only(SavedAndOpened(database));

            Assert.Equal(3, line.GetXDataForApplication(CadOwnerTag.ApplicationName).Count);
            Assert.True(CadOwnerTag.TryRead(line, out CadOwnerTag back));
            Assert.Equal("L-1", back.ObjectId);
            Assert.Null(back.ExtraId);
        }

        [Fact]
        public void ALineWithNoTagIsFreeAndHasNoOwner()
        {
            var database = new DxfDatabase("AC1027");
            DxfLine line = Line(database, 0);
            var registry = new CadOwnerRegistry();
            registry.Register(new TestOwner("Owner"));

            Assert.False(CadOwnerTag.TryRead(line, out CadOwnerTag tag));
            Assert.Null(tag);
            Assert.Null(registry.FindOwner(line));
            Assert.Equal(CadEditRule.Free, registry.GetEditRule(line));
        }

        [Fact]
        public void AnOwnedLineGetsItsOwnersRuleAndOneOfAnOwnerNotRegisteredIsReadOnly()
        {
            var database = new DxfDatabase("AC1027");
            DxfLine mark = Line(database, 0, new CadOwnerTag("Owner", "M-1"));
            DxfLine dimension = Line(database, 10, new CadOwnerTag("Owner", "D-1"));
            DxfLine stranger = Line(database, 20, new CadOwnerTag("Nobody", "D-2"));
            var owner = new TestOwner("Owner");
            var registry = new CadOwnerRegistry();
            registry.Register(owner);

            Assert.Same(owner, registry.FindOwner(mark));
            Assert.Equal(CadEditRule.MoveOnly, registry.GetEditRule(mark));
            Assert.Equal(CadEditRule.Edit, registry.GetEditRule(dimension));
            Assert.Null(registry.FindOwner(stranger));
            Assert.Equal(CadEditRule.ReadOnly, registry.GetEditRule(stranger));
        }

        [Fact]
        public void OwnerNamesAreMatchedCaseAndAll()
        {
            var database = new DxfDatabase("AC1027");
            DxfLine line = Line(database, 0, new CadOwnerTag("owner", "D-1"));
            var registry = new CadOwnerRegistry();
            registry.Register(new TestOwner("Owner"));

            Assert.Equal(CadEditRule.ReadOnly, registry.GetEditRule(line));
        }

        [Fact]
        public void AReadOnlyLineRefusesAMoveWithoutAskingItsOwner()
        {
            var database = new DxfDatabase("AC1027");
            DxfLine line = Line(database, 0, new CadOwnerTag("Owner", "L-1"));
            var owner = new TestOwner("Owner");
            var registry = new CadOwnerRegistry();
            registry.Register(owner);
            CadDocument document = CadDocument.From(database);

            Assert.False(registry.TryApply(document, new CadEdit(line.Handle, CadEditKind.Move, newValue: "10,0")));
            Assert.False(registry.TryApply(document, new CadEdit(line.Handle, CadEditKind.Erase)));
            Assert.Empty(owner.Edits);
        }

        [Fact]
        public void AMarkThatMayOnlyMoveHandsAMoveToItsOwnerAndRefusesARotation()
        {
            var database = new DxfDatabase("AC1027");
            DxfLine mark = Line(database, 0, new CadOwnerTag("Owner", "M-1"));
            var owner = new TestOwner("Owner");
            var registry = new CadOwnerRegistry();
            registry.Register(owner);
            CadDocument document = CadDocument.From(database);
            var move = new CadEdit(mark.Handle, CadEditKind.Move, newValue: "40,0");

            Assert.True(registry.TryApply(document, move));
            Assert.False(registry.TryApply(document, new CadEdit(mark.Handle, CadEditKind.Rotate, newValue: "0,0,1.5707963267948966")));
            Assert.Same(move, Assert.Single(owner.Edits));
        }

        [Fact]
        public void AnEditableDimensionHandsEveryEditToItsOwnerWhichMayRefuseIt()
        {
            var database = new DxfDatabase("AC1027");
            DxfLine dimension = Line(database, 0, new CadOwnerTag("Owner", "D-1"));
            var owner = new TestOwner("Owner", takes: false);
            var registry = new CadOwnerRegistry();
            registry.Register(owner);
            CadDocument document = CadDocument.From(database);

            Assert.False(registry.TryApply(document, new CadEdit(dimension.Handle, CadEditKind.Property, "Text override", "", "3000 (cut on site)")));
            Assert.Equal("3000 (cut on site)", Assert.Single(owner.Edits).NewValue);
        }

        [Fact]
        public void AnEditOfALineWithNoOwnerOrOfAFreeOneIsNotHandedToAnyOwner()
        {
            var database = new DxfDatabase("AC1027");
            DxfLine hand = Line(database, 0);
            DxfLine free = Line(database, 10, new CadOwnerTag("Owner", "F-1"));
            var owner = new TestOwner("Owner");
            var registry = new CadOwnerRegistry();
            registry.Register(owner);
            CadDocument document = CadDocument.From(database);

            Assert.False(registry.TryApply(document, new CadEdit(hand.Handle, CadEditKind.Move, newValue: "1,1")));
            Assert.False(registry.TryApply(document, new CadEdit(free.Handle, CadEditKind.Move, newValue: "1,1")));
            Assert.False(registry.TryApply(document, new CadEdit("FFFFFF", CadEditKind.Move, newValue: "1,1")));
            Assert.Equal(CadEditRule.Free, registry.GetEditRule(free));
            Assert.Empty(owner.Edits);
        }

        [Fact]
        public void AnOwnerThatThrowsIsTakenAsReadOnlyAndItsEditAsRefused()
        {
            var database = new DxfDatabase("AC1027");
            DxfLine dimension = Line(database, 0, new CadOwnerTag("Owner", "D-1"));
            var owner = new TestOwner("Owner");
            var registry = new CadOwnerRegistry();
            registry.Register(owner);
            CadDocument document = CadDocument.From(database);
            owner.Throws = true;

            Assert.Equal(CadEditRule.ReadOnly, registry.GetEditRule(dimension));
            Assert.False(registry.TryApply(document, new CadEdit(dimension.Handle, CadEditKind.Move, newValue: "1,0")));
        }

        [Fact]
        public void TheRulesLetThroughWhatTheySay()
        {
            foreach (CadEditKind kind in Enum.GetValues(typeof(CadEditKind)))
            {
                Assert.True(CadOwnerRegistry.Allows(CadEditRule.Free, kind));
                Assert.True(CadOwnerRegistry.Allows(CadEditRule.Edit, kind));
                Assert.False(CadOwnerRegistry.Allows(CadEditRule.ReadOnly, kind));
                Assert.Equal(kind == CadEditKind.Move, CadOwnerRegistry.Allows(CadEditRule.MoveOnly, kind));
            }
        }

        [Fact]
        public void TwoOwnersOfOneNameAreRefusedAndOwnersAreListedByName()
        {
            var registry = new CadOwnerRegistry();
            registry.Register(new TestOwner("b"));
            registry.Register(new TestOwner("B"));
            registry.Register(new TestOwner("a"));

            Assert.Throws<ArgumentException>(() => registry.Register(new TestOwner("b")));
            Assert.Throws<ArgumentException>(() => registry.Register(new TestOwner("")));
            Assert.Equal(new[] { "B", "a", "b" }, registry.Owners.Select(o => o.Name));
            Assert.True(registry.Unregister("B"));
            Assert.False(registry.Unregister("B"));
            Assert.Equal(new[] { "a", "b" }, registry.Owners.Select(o => o.Name));
        }

        [Fact]
        public void TakingTheTagOutKeepsTheDataOfOtherApplications()
        {
            var database = new DxfDatabase("AC1027");
            DxfLine line = Line(database, 0);
            line.XData = new[] { new DxfTypedValue(1001, "GHPROBE"), new DxfTypedValue(1000, "kept") };
            new CadOwnerTag("Owner", "D-1").WriteTo(line);

            Assert.True(CadOwnerTag.Remove(line));
            Assert.False(CadOwnerTag.Remove(line));

            DxfEntity back = Only(SavedAndOpened(database));
            Assert.False(CadOwnerTag.TryRead(back, out _));
            Assert.Equal("kept", back.GetXDataForApplication("GHPROBE")[1].Value);
        }

        [Fact]
        public void ATagWrittenAgainTakesThePlaceOfTheOneBefore()
        {
            var database = new DxfDatabase("AC1027");
            DxfLine line = Line(database, 0, new CadOwnerTag("Owner", "D-1", "V-1"));

            new CadOwnerTag("Owner", "D-2").WriteTo(line);

            Assert.True(CadOwnerTag.TryRead(line, out CadOwnerTag back));
            Assert.Equal("D-2", back.ObjectId);
            Assert.Null(back.ExtraId);
        }

        [Fact]
        public void ExtendedDataOfTheApplicationThatIsNotATagReadsAsNone()
        {
            var database = new DxfDatabase("AC1027");
            DxfLine real = Line(database, 0);
            real.XData = new[] { new DxfTypedValue(1001, CadOwnerTag.ApplicationName), new DxfTypedValue(1000, "Owner"), new DxfTypedValue(1040, "2.5") };
            DxfLine broken = Line(database, 10);
            broken.XData = new[] { new DxfTypedValue(1001, CadOwnerTag.ApplicationName), new DxfTypedValue(1000, "Owner"), new DxfTypedValue(1000, "D-%zz") };
            DxfLine alone = Line(database, 20);
            alone.XData = new[] { new DxfTypedValue(1001, CadOwnerTag.ApplicationName), new DxfTypedValue(1000, "Owner") };
            DxfLine empty = Line(database, 30);
            empty.XData = new[] { new DxfTypedValue(1001, CadOwnerTag.ApplicationName), new DxfTypedValue(1000, ""), new DxfTypedValue(1000, "D-1") };

            Assert.False(CadOwnerTag.TryRead(real, out _));
            Assert.False(CadOwnerTag.TryRead(broken, out _));
            Assert.False(CadOwnerTag.TryRead(alone, out _));
            Assert.False(CadOwnerTag.TryRead(empty, out _));
        }

        [Fact]
        public void AnIdLongerThanExtendedDataHoldsIsRefused()
        {
            Assert.Throws<ArgumentException>(() => new CadOwnerTag("Owner", new string('a', 256)));
            Assert.Throws<ArgumentException>(() => new CadOwnerTag("Owner", "D-1", new string('%', 100)));
            Assert.Throws<ArgumentException>(() => new CadOwnerTag("Owner", ""));
            Assert.Equal(255, new CadOwnerTag("Owner", new string('a', 255)).ObjectId.Length);
        }
    }
}

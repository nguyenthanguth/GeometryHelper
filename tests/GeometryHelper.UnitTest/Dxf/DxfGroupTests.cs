using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// GROUPs read as ezdxf 1.4.5 reads them (name, description, selectable, unnamed, members), made in code, their members
    /// put in, taken out and reordered with the reactors of the members kept right, renamed, made anonymous, their members'
    /// look set at once, and erased; ezdxf read the probe's files back, its recover and audit finding nothing.
    /// </summary>
    public class DxfGroupTests
    {
        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void TheGroupsOfADrawing_AreReadAsEzdxfReadsThem(string version)
        {
            DxfDatabase db = DxfDatabase.Parse(version == "R2000" ? DxfObjectsEzdxfDrawings.R2000 : DxfObjectsEzdxfDrawings.R2018);
            DxfDBDictionary groups = db.GroupDictionary;

            // ezdxf: BEAMS 37 'Beams of grid A' selectable 1 unnamed 0 [33, 34]; *A1 38 '' selectable 0 unnamed 1 [35]
            Assert.Equal(new[] { "BEAMS", "*A1" }, groups.Select(e => e.Key));
            var beams = Assert.IsType<DxfGroup>(groups["BEAMS"]);
            var anonymous = Assert.IsType<DxfGroup>(groups["*A1"]);
            Assert.Equal("BEAMS", beams.Name);
            Assert.Equal("Beams of grid A", beams.Description);
            Assert.True(beams.Selectable);
            Assert.False(beams.IsAnonymous);
            Assert.False(beams.IsNotAccessible);
            Assert.Equal(2, beams.NumEntities);
            Assert.Equal(new[] { "33", "34" }, beams.GetAllEntityIds().Select(e => e.Handle));
            Assert.IsType<DxfLine>(beams.GetAllEntityIds()[0]);
            Assert.Equal(1, beams.GetIndex(ByHandle<DxfCircle>(db, "34")));
            Assert.True(beams.Has(ByHandle<DxfLine>(db, "33")));
            Assert.False(beams.Has(ByHandle<DxfLine>(db, "36")));
            Assert.Throws<ArgumentException>(() => beams.GetIndex(ByHandle<DxfLine>(db, "36")));
            Assert.Equal("*A1", anonymous.Name);
            Assert.Equal(string.Empty, anonymous.Description);
            Assert.False(anonymous.Selectable);
            Assert.True(anonymous.IsAnonymous);
            Assert.Equal(new[] { "35" }, anonymous.GetAllEntityIds().Select(e => e.Handle));
        }

        [Fact]
        public void AGroupMadeInCode_IsWrittenAsAutoCADWritesOne_AndItsMembersNameItAsTheirReactor()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            var group = new DxfGroup("Hidden lines", false);
            DxfLine hidden = ByHandle<DxfLine>(db, "36");

            Assert.Null(group.Name);
            Assert.Throws<InvalidOperationException>(() => group.Append(hidden));
            db.GroupDictionary.SetAt("HIDDEN", group);
            group.Append(hidden);

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "C", "36", group.Handle);
            Assert.Equal("  0\nGROUP\n  5\n" + group.Handle + "\n102\n{ACAD_REACTORS\n330\nC\n102\n}\n330\nC\n100\nAcDbGroup\n300\nHidden lines\n 70\n0\n 71\n0\n340\n36\n", Records(saved).Single(r => HandleOf(r) == group.Handle));
            Assert.StartsWith("  0\nLINE\n  5\n36\n102\n{ACAD_REACTORS\n330\n" + group.Handle + "\n102\n}\n330\n17\n", Records(saved).Single(r => HandleOf(r) == "36"), StringComparison.Ordinal);

            DxfDatabase again = DxfDatabase.Parse(saved);
            var read = (DxfGroup)again.GroupDictionary["HIDDEN"];
            Assert.Equal("Hidden lines", read.Description);
            Assert.False(read.Selectable);
            Assert.Equal(new[] { "36" }, read.GetAllEntityIds().Select(e => e.Handle));
        }

        [Fact]
        public void AGroupAddedUnderAStar_IsAnonymous_NamedTheFirstNameTheDictionaryHasNot()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            var group = new DxfGroup();
            var other = new DxfGroup();
            other.SetAnonymous();

            db.GroupDictionary.SetAt("*", group);
            db.GroupDictionary.SetAt("ANY", other);

            Assert.Equal("*A2", group.Name);
            Assert.True(group.IsAnonymous);
            Assert.Equal("*A3", other.Name);
            Assert.True(other.IsAnonymous);
            Assert.True(group.Selectable);
        }

        [Fact]
        public void MembersPutInTakenOutAndReordered_KeepTheGroupAndTheReactorsOfTheMembersRight()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            var beams = (DxfGroup)db.GroupDictionary["BEAMS"];
            DxfLine line = ByHandle<DxfLine>(db, "33");
            DxfCircle circle = ByHandle<DxfCircle>(db, "34");
            DxfDBText note = ByHandle<DxfDBText>(db, "35");
            DxfLine hidden = ByHandle<DxfLine>(db, "36");

            beams.Prepend(hidden);
            beams.InsertAt(2, note);
            Assert.Equal(new[] { "36", "33", "35", "34" }, Handles(beams));
            Assert.Throws<ArgumentException>(() => beams.Append(line));
            Assert.Throws<ArgumentException>(() => beams.Append(new[] { (DxfEntity)new DxfLine() }));
            Assert.Throws<ArgumentOutOfRangeException>(() => beams.InsertAt(5, new DxfLine()));

            beams.Reverse();
            Assert.Equal(new[] { "34", "35", "33", "36" }, Handles(beams));
            beams.Transfer(0, 2, 2);
            Assert.Equal(new[] { "33", "36", "34", "35" }, Handles(beams));
            Assert.Throws<ArgumentOutOfRangeException>(() => beams.Transfer(3, 0, 2));

            beams.Replace(hidden, hidden);
            beams.RemoveAt(1);
            beams.Remove(note);
            Assert.Equal(new[] { "33", "34" }, Handles(beams));
            Assert.Throws<ArgumentException>(() => beams.Remove(note));

            // Back where it began: the note keeps the reactor of its anonymous group, the hidden line none.
            Assert.Equal(text, db.ToText());

            beams.Replace(circle, hidden);
            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "37", "34", "36");
            Assert.DoesNotContain("ACAD_REACTORS", Records(saved).Single(r => HandleOf(r) == "34"), StringComparison.Ordinal);
            Assert.Contains("{ACAD_REACTORS\n330\n37\n102\n}\n", Records(saved).Single(r => HandleOf(r) == "36"), StringComparison.Ordinal);

            // A named group left empty is kept when the drawing is saved, as AutoCAD 2021 keeps it.
            beams.Clear();
            Assert.Equal(0, beams.NumEntities);
            Assert.False(beams.IsErased);
            saved = db.ToText();
            Assert.False(beams.IsErased);
            Assert.Contains(Records(saved), r => HandleOf(r) == "37");
            Assert.DoesNotContain("ACAD_REACTORS", Records(saved).Single(r => HandleOf(r) == "33"), StringComparison.Ordinal);
            Assert.Equal(0, ((DxfGroup)DxfDatabase.Parse(saved).GroupDictionary["BEAMS"]).NumEntities);
        }

        [Fact]
        public void AGroupLeftEmpty_IsKeptOnSaving_NamedOrAnonymous_AndOneReadEmptyIsKept()
        {
            // An empty group as the file has it is kept: the file is written back as it was read.
            string empty = DxfObjectsEzdxfDrawings.R2018.Replace("340\n35\n", string.Empty);
            DxfDatabase read = DxfDatabase.Parse(empty);
            Assert.Equal(0, ((DxfGroup)read.GroupDictionary["*A1"]).NumEntities);
            Assert.Equal(empty, read.ToText());

            // One made in code and never given a member is kept, and one anonymous emptied by RemoveAt, as AutoCAD 2021 keeps
            // both (checks 27 and 27x).
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            var made = new DxfGroup("never filled", true);
            db.GroupDictionary.SetAt("EMPTY", made);
            var anonymous = (DxfGroup)db.GroupDictionary["*A1"];
            anonymous.RemoveAt(0);

            string saved = db.ToText();
            Assert.False(made.IsErased);
            Assert.False(anonymous.IsErased);
            DxfDatabase reread = DxfDatabase.Parse(saved);
            Assert.Equal(new[] { "*A1", "BEAMS", "EMPTY" }, reread.GroupDictionary.Select(e => e.Key).OrderBy(k => k, StringComparer.Ordinal));
            Assert.Equal(0, ((DxfGroup)reread.GroupDictionary["*A1"]).NumEntities);
            Assert.True(((DxfGroup)reread.GroupDictionary["*A1"]).IsAnonymous);
            Assert.DoesNotContain("ACAD_REACTORS", Records(saved).Single(r => HandleOf(r) == "35"), StringComparison.Ordinal);

            // A group emptied and filled again before the drawing is saved is kept.
            DxfDatabase again = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            var refilled = (DxfGroup)again.GroupDictionary["*A1"];
            DxfEntity text = refilled.GetAllEntityIds()[0];
            refilled.Clear();
            refilled.Append(text);
            Assert.Equal(DxfObjectsEzdxfDrawings.R2018, again.ToText());
        }

        [Fact]
        public void MembersAppendedOneAtATime_CostLittleEachOnABigDrawing()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            var group = new DxfGroup("many", true);
            db.GroupDictionary.SetAt("MANY", group);
            var watch = System.Diagnostics.Stopwatch.StartNew();

            // Each line appended to the drawing, then to the group, as a loop that builds both does.
            for (int i = 0; i < 4000; i++)
            {
                var line = new DxfLine(new GeometryHelper.Geometry.GeoPoint3(i, 0, 0), new GeometryHelper.Geometry.GeoPoint3(i, 1, 0));
                db.ModelSpace.AppendEntity(line);
                group.Append(line);
            }

            Assert.Equal(4000, group.NumEntities);
            Assert.True(watch.ElapsedMilliseconds < 20000, "4 000 members took " + watch.ElapsedMilliseconds + " ms.");
        }

        [Fact]
        public void AMemberErased_LeavesTheGroup_AndTheLastErasedTakesTheGroupWithIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            var beams = (DxfGroup)db.GroupDictionary["BEAMS"];
            var anonymous = (DxfGroup)db.GroupDictionary["*A1"];

            ByHandle<DxfLine>(db, "33").Erase();
            Assert.Equal(new[] { "34" }, Handles(beams));
            Assert.Equal(1, beams.NumEntities);

            ByHandle<DxfDBText>(db, "35").Erase();
            Assert.Equal(0, anonymous.NumEntities);

            string saved = db.ToText();
            DxfDatabase again = DxfDatabase.Parse(saved);
            Assert.Equal(new[] { "34" }, Handles((DxfGroup)again.GroupDictionary["BEAMS"]));
            Assert.False(again.GroupDictionary.Contains("*A1"));
            Assert.DoesNotContain(Records(saved), r => HandleOf(r) == "38");
        }

        [Fact]
        public void AGroupRenamedOrMadeAnonymous_HasItsEntryRenamed()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            var beams = (DxfGroup)db.GroupDictionary["BEAMS"];
            var anonymous = (DxfGroup)db.GroupDictionary["*A1"];

            beams.Name = "BEAMS";
            beams.Description = beams.Description;
            beams.Selectable = beams.Selectable;
            anonymous.SetAnonymous();
            Assert.Equal(text, db.ToText());

            beams.Name = "GIRDERS";
            Assert.Equal("GIRDERS", beams.Name);
            Assert.Throws<ArgumentException>(() => beams.Name = "*a1");
            Assert.Throws<ArgumentException>(() => beams.Name = "A|B");

            beams.SetAnonymous();
            Assert.True(beams.IsAnonymous);
            Assert.Equal("*A2", beams.Name);

            anonymous.Name = "NOTES";
            Assert.False(anonymous.IsAnonymous);
            Assert.Equal(new[] { "*A2", "NOTES" }, db.GroupDictionary.Select(e => e.Key));

            beams.Description = "Girders";
            beams.Selectable = false;
            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "C", "37", "38");
            var read = (DxfGroup)DxfDatabase.Parse(saved).GroupDictionary["*A2"];
            Assert.Equal("Girders", read.Description);
            Assert.False(read.Selectable);
            Assert.True(read.IsAnonymous);
        }

        [Fact]
        public void TheLookSetOnTheGroup_IsSetOnEveryMember()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            var beams = (DxfGroup)db.GroupDictionary["BEAMS"];

            beams.SetColorIndex(256);
            beams.SetLayer("steel");
            beams.SetVisibility(true);
            Assert.Equal(text, db.ToText());

            beams.SetColorIndex(3);
            beams.SetLayer("frame");
            beams.SetLinetype("DASHED");
            beams.SetLinetypeScale(2.5);
            beams.SetVisibility(false);
            beams.SetColor(DxfColor.FromRgb(255, 128, 0));

            foreach (DxfEntity member in beams.GetAllEntityIds())
            {
                Assert.Equal("frame", member.Layer);
                Assert.Equal("DASHED", member.Linetype);
                Assert.Equal(2.5, member.LinetypeScale, 12);
                Assert.False(member.Visible);
                Assert.Equal(0xFF8000, member.TrueColor);
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => beams.SetColorIndex(257));
            Assert.Throws<ArgumentException>(() => beams.SetLinetype("NO_SUCH"));
            Assert.Throws<ArgumentOutOfRangeException>(() => beams.SetLinetypeScale(0.0));
            Assert.Throws<ArgumentException>(() => beams.SetLayer("a|b"));
            List<string> saved = Records(db.ToText());
            Assert.Contains(Records(text).Single(r => HandleOf(r) == "35"), saved);
            Assert.Contains(Records(text).Single(r => HandleOf(r) == "36"), saved);
            Assert.DoesNotContain(Records(text).Single(r => HandleOf(r) == "33"), saved);
        }

        [Fact]
        public void AGroupErased_GoesWithItsEntry_ItsMembersKeptAndTheirReactorsCleared()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            var beams = (DxfGroup)db.GroupDictionary["BEAMS"];

            beams.Erase();

            Assert.True(beams.IsErased);
            Assert.False(db.GroupDictionary.Contains("BEAMS"));
            Assert.Throws<InvalidOperationException>(() => beams.Append(ByHandle<DxfLine>(db, "36")));
            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "C", "37", "33", "34");
            Assert.DoesNotContain("ACAD_REACTORS", Records(saved).Single(r => HandleOf(r) == "33"), StringComparison.Ordinal);
            Assert.NotNull(ByHandle<DxfLine>(DxfDatabase.Parse(saved), "33"));
        }

        private static IEnumerable<string> Handles(DxfGroup group) => group.GetAllEntityIds().Select(e => e.Handle);
    }
}

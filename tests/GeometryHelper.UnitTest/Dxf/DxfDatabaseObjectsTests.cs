using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The objects a database reaches by their ids: the named object dictionary and those it names, a dictionary's flag of
    /// hard ownership and its entries copied, the properties of the drawing (DWGPROPS), and the overrides of a layer in a
    /// viewport, each read as ezdxf 1.4.5 reads them.
    /// </summary>
    public class DxfDatabaseObjectsTests
    {
        [Fact]
        public void TheDictionariesOfADrawing_AreThoseTheNamedObjectDictionaryNames()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            DxfDBDictionary root = db.NamedObjectsDictionary;

            Assert.Equal("A", root.Handle);
            Assert.Equal("C", db.GroupDictionary.Handle);
            Assert.Equal("D", db.LayoutDictionary.Handle);
            Assert.Equal("E", db.MaterialDictionary.Handle);
            Assert.Equal("F", db.MLeaderStyleDictionary.Handle);
            Assert.Equal("10", db.MLStyleDictionary.Handle);
            Assert.Equal("11", db.PlotSettingsDictionary.Handle);
            Assert.Equal("12", db.PlotStyleNameDictionary.Handle);
            Assert.Equal("15", db.TableStyleDictionary.Handle);
            Assert.Equal("16", db.VisualStyleDictionary.Handle);
            Assert.Equal("B", db.ColorDictionary.Handle);
            Assert.Null(db.DetailViewStyleDictionary);
            Assert.Null(db.SectionViewStyleDictionary);
            Assert.Null(db.DataLinkDictionary);
            Assert.IsType<DxfXrecord>(root["DWGPROPS"]);
            Assert.IsType<DxfDBDictionary>(root["ACAD_PLOTSTYLENAME"]);
        }

        [Fact]
        public void ADictionaryOwningItsEntriesHard_NamesThemBy360_AndItsEntriesAreCopied()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDBDictionary overrides = db.LayerTable["steel"].ExtensionDictionary;
            DxfDBDictionary groups = db.GroupDictionary;

            Assert.True(overrides.TreatElementsAsHard);
            Assert.False(groups.TreatElementsAsHard);
            overrides.TreatElementsAsHard = true;
            groups.TreatElementsAsHard = false;
            Assert.Equal(text, db.ToText());

            groups.TreatElementsAsHard = true;
            string record = Records(db.ToText()).Single(r => HandleOf(r) == "C");
            Assert.Contains("100\nAcDbDictionary\n280\n1\n281\n1\n  3\nBEAMS\n360\n37\n  3\n*A1\n360\n38\n", record, StringComparison.Ordinal);
            groups.TreatElementsAsHard = false;
            Assert.Contains("280\n0\n281\n1\n  3\nBEAMS\n350\n37\n", Records(db.ToText()).Single(r => HandleOf(r) == "C"), StringComparison.Ordinal);

            var made = new DxfDBDictionary { TreatElementsAsHard = true };
            db.NamedObjectsDictionary.SetAt("GH_HARD", made);
            Assert.True(made.TreatElementsAsHard);

            var copy = new DxfDBDictionaryEntry[5];
            groups.CopyTo(copy, 3);
            Assert.Equal("BEAMS", copy[3].Key);
            Assert.Same(groups["*A1"], copy[4].Value);
            Assert.Throws<ArgumentException>(() => groups.CopyTo(copy, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => groups.CopyTo(copy, -1));
            Assert.Throws<ArgumentNullException>(() => groups.CopyTo(null, 0));
        }

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void ThePropertiesOfTheDrawing_AreThoseOfItsDwgprops(string version)
        {
            DxfDatabase db = DxfDatabase.Parse(version == "R2000" ? DxfObjectsEzdxfDrawings.R2000 : DxfObjectsEzdxfDrawings.R2018);
            DxfDatabaseSummaryInfo info = db.SummaryInfo;

            Assert.Equal("Plate B-12", info.Title);
            Assert.Equal("Steel plates", info.Subject);
            Assert.Equal("J. Doe", info.Author);
            Assert.Equal("Checked", info.Comments);
            Assert.Equal("plate;steel", info.Keywords);
            Assert.Equal("jdoe", info.LastSavedBy);
            Assert.Equal("3", info.RevisionNumber);
            Assert.Equal("https://example.com/", info.HyperlinkBase);
            Assert.Equal(new[] { "Project=Grid A", "Phase=2" }, info.CustomProperties.Select(p => p.Key + "=" + p.Value));
            Assert.Equal(info, new DxfDatabaseSummaryInfoBuilder(info).ToDatabaseSummaryInfo());
            Assert.True(info == db.SummaryInfo);
        }

        [Fact]
        public void PropertiesSet_RewriteDwgprops_KeepingItsDatesAndWhatFollows()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);

            db.SummaryInfo = db.SummaryInfo;
            Assert.Equal(text, db.ToText());

            var builder = new DxfDatabaseSummaryInfoBuilder(db.SummaryInfo) { Title = "Plate B-13", Author = "Đỗ" };
            builder.CustomPropertyTable.RemoveAt(1);
            builder.CustomPropertyTable.Add(new KeyValuePair<string, string>("Grade", "S355"));
            db.SummaryInfo = builder.ToDatabaseSummaryInfo();

            // The record has the list after a group 90: the custom properties go there, the ten groups each "=".
            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "3A");
            string record = Records(saved).Single(r => HandleOf(r) == "3A");
            Assert.Contains("  2\nPlate B-13\n  3\nSteel plates\n  4\nĐỗ\n", record, StringComparison.Ordinal);
            Assert.Contains("300\n=\n301\n=\n302\n=\n", record, StringComparison.Ordinal);
            Assert.EndsWith(" 40\n0.125\n 41\n2461338.25\n 42\n2461339.5\n  1\nhttps://example.com/\n 90\n2\n  1\nProject\n  1\nGrid A\n  1\nGrade\n  1\nS355\n", record, StringComparison.Ordinal);
            DxfDatabaseSummaryInfo read = DxfDatabase.Parse(saved).SummaryInfo;
            Assert.Equal("Plate B-13", read.Title);
            Assert.Equal("Đỗ", read.Author);
            Assert.Equal(new[] { "Project", "Grade" }, read.CustomProperties.Select(p => p.Key));

            builder.CustomPropertyTable.Add(new KeyValuePair<string, string>("grade", "S275"));
            Assert.Throws<ArgumentException>(() => builder.ToDatabaseSummaryInfo());
            Assert.Throws<ArgumentException>(() => builder.Title = "two\nlines");
        }

        [Fact]
        public void PropertiesGivenADrawingWithNone_AreADwgpropsOfTheNamedObjectDictionary()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);

            Assert.Equal(string.Empty, db.SummaryInfo.Title);
            Assert.Empty(db.SummaryInfo.CustomProperties);
            db.SummaryInfo = new DxfDatabaseSummaryInfoBuilder().ToDatabaseSummaryInfo();
            Assert.Equal(text, db.ToText());

            db.SummaryInfo = new DxfDatabaseSummaryInfoBuilder { Title = "Plan", Author = "GH" }.ToDatabaseSummaryInfo();

            DxfDatabase again = DxfDatabase.Parse(db.ToText());
            Assert.Equal("Plan", again.SummaryInfo.Title);
            Assert.Equal("GH", again.SummaryInfo.Author);
            var props = (DxfXrecord)again.NamedObjectsDictionary["DWGPROPS"];
            Assert.Equal("1 DWGPROPS COOKIE", props.Data[0].ToString());
            Assert.Equal("90 0", props.Data[22].ToString());
            Assert.Equal(23, props.Data.Count);
            Assert.Throws<InvalidOperationException>(() => DxfDatabase.Parse(DxfFileEzdxfDrawings.R12).SummaryInfo = again.SummaryInfo);
        }

        [Fact]
        public void CustomPropertiesListedAfterGroup90_AreRead_AndWrittenInTheFormTheRecordHas()
        {
            // The list after 90: its count, then each name and value in two groups 1; read after those of 300 to 309.
            string listed = DxfObjectsEzdxfDrawings.R2018.Replace("https://example.com/\n 90\n0\n", "https://example.com/\n 90\n2\n  1\nClient\n  1\nACME\n  1\nproject\n  1\nignored\n");
            DxfDatabase db = DxfDatabase.Parse(listed);
            Assert.Equal(new[] { "Project=Grid A", "Phase=2", "Client=ACME" }, db.SummaryInfo.CustomProperties.Select(p => p.Key + "=" + p.Value));

            // Written back in the list, the ten groups emptied: one set of them in the record.
            db.SummaryInfo = new DxfDatabaseSummaryInfoBuilder(db.SummaryInfo) { Title = "T" }.ToDatabaseSummaryInfo();
            string record = Records(db.ToText()).Single(r => HandleOf(r) == "3A");
            Assert.Contains("300\n=\n301\n=\n", record, StringComparison.Ordinal);
            Assert.EndsWith(" 90\n3\n  1\nProject\n  1\nGrid A\n  1\nPhase\n  1\n2\n  1\nClient\n  1\nACME\n", record, StringComparison.Ordinal);

            // A record with no list keeps the ten groups, ten at most.
            string plain = DxfObjectsEzdxfDrawings.R2000.Replace("https://example.com/\n 90\n0\n", "https://example.com/\n");
            DxfDatabase old = DxfDatabase.Parse(plain);
            var builder = new DxfDatabaseSummaryInfoBuilder(old.SummaryInfo);
            builder.CustomPropertyTable.Add(new KeyValuePair<string, string>("Grade", "S355"));
            old.SummaryInfo = builder.ToDatabaseSummaryInfo();
            string kept = Records(old.ToText()).Single(r => HandleOf(r) == "3A");
            Assert.Contains("300\nProject=Grid A\n301\nPhase=2\n302\nGrade=S355\n303\n=\n", kept, StringComparison.Ordinal);
            Assert.DoesNotContain(" 90\n", kept, StringComparison.Ordinal);

            for (int i = 0; i < 8; i++)
            {
                builder.CustomPropertyTable.Add(new KeyValuePair<string, string>("P" + i, "v"));
            }

            Assert.Throws<ArgumentException>(() => old.SummaryInfo = builder.ToDatabaseSummaryInfo());
        }

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void TheOverridesOfALayerInAViewport_AreReadAsEzdxfReadsThem(string version)
        {
            DxfDatabase db = DxfDatabase.Parse(version == "R2000" ? DxfObjectsEzdxfDrawings.R2000 : DxfObjectsEzdxfDrawings.R2018);
            DxfViewport viewport = ByHandle<DxfViewport>(db, "40");
            DxfViewport other = ByHandle<DxfViewport>(db, "41");
            DxfLayerTableRecord steel = db.LayerTable["steel"];
            DxfLayerTableRecord notes = db.LayerTable["notes"];

            // ezdxf: steel in 40: aci 5, linetype DASHED, lineweight 50, transparency 0.50196 (alpha 127);
            // notes in 40: rgb (255, 128, 0).
            Assert.True(steel.HasOverrides);
            Assert.True(steel.HasViewportOverrides(viewport));
            Assert.False(steel.HasViewportOverrides(other));
            Assert.False(db.LayerTable["hidden"].HasOverrides);

            DxfLayerViewportProperties properties = steel.GetViewportOverrides(viewport);
            Assert.True(properties.IsColorOverridden);
            Assert.Equal(5, properties.Color.ColorIndex);
            Assert.True(properties.IsLinetypeOverridden);
            Assert.Equal("DASHED", properties.Linetype);
            Assert.True(properties.IsLineWeightOverridden);
            Assert.Equal(DxfLineWeight.LineWeight050, properties.LineWeight);
            Assert.True(properties.IsTransparencyOverridden);
            Assert.Equal(127, properties.Transparency.Alpha);
            Assert.Same(viewport, properties.Viewport);

            DxfLayerViewportProperties plain = steel.GetViewportOverrides(other);
            Assert.False(plain.IsColorOverridden);
            Assert.Equal(1, plain.Color.ColorIndex);
            Assert.Equal("Continuous", plain.Linetype);
            Assert.Equal(DxfLineWeight.ByLineWeightDefault, plain.LineWeight);

            DxfLayerViewportProperties orange = notes.GetViewportOverrides(viewport);
            Assert.Equal(0xFF8000, orange.Color.ColorValue);
            Assert.False(orange.IsLinetypeOverridden);
        }

        [Fact]
        public void TheOverridesOfALayerTakenOut_AreItsXrecordsAndTheirEntries()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLayerTableRecord steel = db.LayerTable["steel"];

            db.LayerTable["hidden"].RemoveAllOverrides();
            Assert.Equal(text, db.ToText());

            steel.RemoveAllOverrides();
            Assert.False(steel.HasOverrides);
            Assert.Equal(0, steel.ExtensionDictionary.Count);
            OnlyTheseChanged(text, db.ToText(), "42", "43", "44", "45", "46");
        }
    }
}

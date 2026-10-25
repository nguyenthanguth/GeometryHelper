using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// What every object has: an extension dictionary made and released, its entries, binary data kept under a key, extended
    /// data moved with a transformation, and the hyperlinks of an entity; ezdxf 1.4.5 read the probe's extension dictionaries,
    /// binary data, moved extended data and hyperlinks back, its recover and audit finding nothing.
    /// </summary>
    public class DxfDBObjectTests
    {
        [Fact]
        public void AnExtensionDictionaryMade_IsADictionaryOwnedHardByTheObject_WrittenAsAutoCADWritesIt()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfCircle line = ByHandle<DxfCircle>(db, "3F");

            Assert.Null(line.ExtensionDictionary);
            line.CreateExtensionDictionary();
            DxfDBDictionary dictionary = line.ExtensionDictionary;
            line.CreateExtensionDictionary();

            Assert.NotNull(dictionary);
            Assert.Same(dictionary, line.ExtensionDictionary);
            Assert.Equal("3F", dictionary.OwnerHandle);
            Assert.Equal(0, dictionary.Count);

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "3F", dictionary.Handle);
            Assert.StartsWith("  0\nCIRCLE\n  5\n3F\n102\n{ACAD_XDICTIONARY\n360\n" + dictionary.Handle + "\n102\n}\n330\n17\n", Records(saved).Single(r => HandleOf(r) == "3F"), StringComparison.Ordinal);
            Assert.Equal("  0\nDICTIONARY\n  5\n" + dictionary.Handle + "\n102\n{ACAD_REACTORS\n330\n3F\n102\n}\n330\n3F\n100\nAcDbDictionary\n280\n1\n281\n1\n", Records(saved).Single(r => HandleOf(r) == dictionary.Handle));

            line.ReleaseExtensionDictionary();
            Assert.Null(line.ExtensionDictionary);
            Assert.True(dictionary.IsErased);
            OnlyTheseChanged(text, db.ToText());
        }

        [Fact]
        public void TheEntriesOfADictionary_AreAddedRenamedAndTakenOut_EachObjectOwnedByIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfCircle line = ByHandle<DxfCircle>(db, "3F");
            line.CreateExtensionDictionary();
            DxfDBDictionary dictionary = line.ExtensionDictionary;
            var data = new DxfDBDictionary();

            Assert.Throws<InvalidOperationException>(() => data.SetAt("x", new DxfDBDictionary()));
            dictionary.SetAt("GH_DATA", data);
            dictionary["GH_MORE"] = new DxfDBDictionary();

            Assert.Equal(2, dictionary.Count);
            Assert.True(dictionary.Contains("gh_data"));
            Assert.True(dictionary.Contains(data));
            Assert.Same(data, dictionary.GetAt("GH_DATA"));
            Assert.Equal("GH_DATA", dictionary.NameAt(data));
            Assert.Equal(dictionary.Handle, data.OwnerHandle);
            Assert.Equal(new[] { "GH_DATA", "GH_MORE" }, dictionary.Select(e => e.Key));
            Assert.Contains("  3\nGH_DATA\n360\n" + data.Handle + "\n", Records(db.ToText()).Single(r => HandleOf(r) == dictionary.Handle), StringComparison.Ordinal);

            dictionary.SetName("GH_DATA", "GH_FIRST");
            Assert.Equal(new[] { "GH_FIRST", "GH_MORE" }, dictionary.Select(e => e.Key));
            Assert.Throws<ArgumentException>(() => dictionary.SetName("GH_FIRST", "gh_more"));
            Assert.Throws<KeyNotFoundException>(() => dictionary.GetAt("GH_DATA"));

            DxfDBObject removed = dictionary.Remove("GH_MORE");
            Assert.True(removed.IsErased);
            Assert.Equal(1, dictionary.Count);
            Assert.Throws<InvalidOperationException>(() => line.ReleaseExtensionDictionary());
            Assert.Throws<ArgumentException>(() => dictionary.SetAt("GH_LINE", ByHandle<DxfLine>(db, "3E")));

            DxfDatabase again = DxfDatabase.Parse(db.ToText());
            DxfDBDictionary read = ByHandle<DxfCircle>(again, "3F").ExtensionDictionary;
            Assert.Equal(new[] { "GH_FIRST" }, read.Select(e => e.Key));
            Assert.IsType<DxfDBDictionary>(read["GH_FIRST"]);

            // The line erased takes its dictionary and its entries with it on saving.
            string handle = read.Handle;
            ByHandle<DxfCircle>(again, "3F").Erase();
            Assert.DoesNotContain(Records(again.ToText()), r => HandleOf(r) == handle);
            Assert.True(read.IsErased);
            Assert.Equal(handle, read.Handle);
        }

        [Fact]
        public void BinaryDataKeptUnderAKey_IsAnXrecordOfTheExtensionDictionary_In127ByteLines()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            byte[] data = Enumerable.Range(0, 200).Select(i => (byte)(255 - i)).ToArray();

            Assert.Null(line.GetBinaryDataForKey("GH_BIN"));
            line.SetBinaryDataForKey("GH_BIN", data);

            Assert.Equal(data, line.GetBinaryDataForKey("gh_bin"));
            string saved = db.ToText();
            DxfDBObject xrecord = line.ExtensionDictionary.GetAt("GH_BIN");
            string record = Records(saved).Single(r => HandleOf(r) == xrecord.Handle);
            Assert.StartsWith("  0\nXRECORD\n", record, StringComparison.Ordinal);
            Assert.Contains("100\nAcDbXrecord\n280\n1\n310\nFFFEFD", record, StringComparison.Ordinal);
            Assert.Equal(2, record.Split('\n').Count(l => l == "310"));
            Assert.Equal(data, ByHandle<DxfLine>(DxfDatabase.Parse(saved), "3E").GetBinaryDataForKey("GH_BIN"));

            line.SetBinaryDataForKey("GH_BIN", data);
            Assert.Equal(saved, db.ToText());
            line.SetBinaryDataForKey("GH_BIN", new byte[] { 1, 2, 3 });
            Assert.Equal(new byte[] { 1, 2, 3 }, line.GetBinaryDataForKey("GH_BIN"));
            line.ExtensionDictionary.SetAt("GH_DICT", new DxfDBDictionary());
            Assert.Throws<ArgumentException>(() => line.SetBinaryDataForKey("GH_DICT", data));
            Assert.Throws<InvalidOperationException>(() => new DxfLine().SetBinaryDataForKey("x", data));
            Assert.Throws<InvalidOperationException>(() => DxfDatabase.Parse(DxfFileEzdxfDrawings.R12).ModelSpace.First().CreateExtensionDictionary());
        }

        [Fact]
        public void ExtendedDataMovedWithATransformation_IsMovedAsAutoCADMovesIt_GroupByGroup()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            line.XData = new[]
            {
                new DxfTypedValue(1001, "GHPROBE"), new DxfTypedValue(1000, "kept"),
                new DxfTypedValue(1010, "1"), new DxfTypedValue(1020, "2"), new DxfTypedValue(1030, "3"),
                new DxfTypedValue(1011, "1"), new DxfTypedValue(1021, "2"), new DxfTypedValue(1031, "3"),
                new DxfTypedValue(1012, "1"), new DxfTypedValue(1022, "0"), new DxfTypedValue(1032, "0"),
                new DxfTypedValue(1013, "1"), new DxfTypedValue(1023, "0"), new DxfTypedValue(1033, "0"),
                new DxfTypedValue(1041, "2.5"), new DxfTypedValue(1042, "1.5"),
            };
            string before = db.ToText();

            line.XDataTransformBy(GeoTransform3.Identity);
            Assert.Equal(before, db.ToText());

            // Moved by (10, 0, 0) and scaled by 2: the position moves and scales, the displacement scales, the direction keeps
            // its length, the distance and the scale factor double; the plain point and the text stay.
            line.XDataTransformBy(GeoTransform3.Translation(new GeoVector3(10, 0, 0)) * GeoTransform3.Scaling(2));
            Assert.Equal(
                new[] { "GHPROBE", "kept", "1", "2", "3", "12", "4", "6", "2", "0", "0", "1", "0", "0", "5", "3" },
                line.XData.Select(g => g.Value));
            OnlyTheseChanged(before, db.ToText(), "3E");

            line.XDataTransformBy(GeoTransform3.RotationZ(Math.PI / 2));
            Assert.Equal(-4.0, double.Parse(line.XData[5].Value, System.Globalization.CultureInfo.InvariantCulture), 12);
            Assert.Equal("1", line.XData[12].Value);
            Assert.Throws<ArgumentNullException>(() => line.XDataTransformBy(null));
        }

        [Fact]
        public void TheHyperlinksOfAnEntity_AreItsExtendedDataOfPE_URL_AsAutoCADWritesIt()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLine line = ByHandle<DxfLine>(db, "3E");

            Assert.Equal(0, line.Hyperlinks.Count);
            line.Hyperlinks.Add(new DxfHyperLink { Name = "https://example.com/B-12.pdf", Description = "Plate B-12", SubLocation = "Sheet 2" });
            line.Hyperlinks.Add(new DxfHyperLink { Name = "file:///C:/plans/a.dwg" });

            Assert.EndsWith("1001\nPE_URL\n1000\nhttps://example.com/B-12.pdf\n1002\n{\n1000\nPlate B-12\n1000\nSheet 2\n1002\n}\n1000\nfile:///C:/plans/a.dwg\n", Records(db.ToText()).Single(r => HandleOf(r) == "3E"), StringComparison.Ordinal);
            DxfHyperLinkCollection read = ByHandle<DxfLine>(DxfDatabase.Parse(db.ToText()), "3E").Hyperlinks;
            Assert.Equal(2, read.Count);
            Assert.Equal("Plate B-12", read[0].DisplayString);
            Assert.Equal("Sheet 2", read[0].SubLocation);
            Assert.Equal("file:///C:/plans/a.dwg", read[1].DisplayString);
            Assert.Equal(1, read.IndexOf(new DxfHyperLink { Name = "file:///C:/plans/a.dwg" }));

            string saved = db.ToText();
            line.Hyperlinks[0] = line.Hyperlinks[0];
            Assert.Equal(saved, db.ToText());

            line.Hyperlinks.RemoveAt(0);
            Assert.Equal(1, line.Hyperlinks.Count);
            line.Hyperlinks.Clear();
            Assert.Null(line.GetXDataForApplication("PE_URL"));
            Assert.Throws<ArgumentOutOfRangeException>(() => line.Hyperlinks[0]);
            Assert.Throws<ArgumentNullException>(() => line.Hyperlinks.Add(null));

            DxfBlockTableRecord bolt = db.BlockTable["BOLT"];
            bolt.Hyperlinks.Add(new DxfHyperLink { Name = "https://example.com/bolts" });
            Assert.Equal("https://example.com/bolts", DxfDatabase.Parse(db.ToText()).BlockTable["BOLT"].Hyperlinks[0].Name);
        }

        [Fact]
        public void ADictionaryTakenOut_TakesTheDictionariesItOwnsAlong_AtEveryDepth()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            var s = new DxfDBDictionary();
            var t = new DxfDBDictionary();
            var u = new DxfDBDictionary();
            line.ExtensionDictionary.SetAt("S", s);
            s.SetAt("T", t);
            t.SetAt("U", u);
            string[] handles = { s.Handle, t.Handle, u.Handle };

            line.ExtensionDictionary.Remove("S");

            Assert.True(t.IsErased);
            Assert.True(u.IsErased);
            Assert.DoesNotContain(Records(db.ToText()), r => handles.Contains(HandleOf(r)));
            OnlyTheseChanged(text, db.ToText());
        }

        [Fact]
        public void AnObjectErased_TakesItsExtensionDictionaryAlong_WithEverythingItOwns()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            line.SetBinaryDataForKey("GH_BIN", new byte[] { 1, 2, 3 });
            var s = new DxfDBDictionary();
            line.ExtensionDictionary.SetAt("S", s);
            s.SetAt("T", new DxfDBDictionary());
            var made = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0));
            db.ModelSpace.AppendEntity(made);
            made.CreateExtensionDictionary();
            var layer = new DxfLayerTableRecord { Name = "GH_Spare" };
            db.LayerTable.Add(layer);
            layer.SetBinaryDataForKey("GH_BIN", new byte[] { 4 });
            layer.ExtensionDictionary.SetAt("S", new DxfDBDictionary());

            string[] handles =
            {
                line.ExtensionDictionary.Handle, line.ExtensionDictionary.GetAt("GH_BIN").Handle, s.Handle, s.GetAt("T").Handle,
                made.ExtensionDictionary.Handle, layer.ExtensionDictionary.Handle, layer.ExtensionDictionary.GetAt("GH_BIN").Handle,
                layer.ExtensionDictionary.GetAt("S").Handle,
            };

            line.Erase();
            made.Erase();
            layer.Erase();

            string saved = db.ToText();
            Assert.DoesNotContain(Records(saved), r => handles.Contains(HandleOf(r)));
            Assert.Equal(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018).ModelSpace.Count - 1, DxfDatabase.Parse(saved).ModelSpace.Count);
        }

        [Fact]
        public void ADictionaryTakenOut_WhoseEntriesOwnEachOtherInARing_IsTakenOutWithThemOnce()
        {
            // A file not well formed but readable: the extension dictionary 92 of the LINE 3E lists S (80), S lists T (82), and
            // T lists S back, S owned by T and T by S.
            string text = DxfFileEzdxfDrawings.R2018.Replace(
                "  5\n92\n330\n3E\n100\nAcDbDictionary\n280\n1\n281\n1\n",
                "  5\n92\n330\n3E\n100\nAcDbDictionary\n280\n1\n281\n1\n  3\nS\n360\n80\n");
            int end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
            text = text.Substring(0, end)
                + "  0\nDICTIONARY\n  5\n80\n330\n82\n100\nAcDbDictionary\n280\n1\n281\n1\n  3\nT\n360\n82\n"
                + "  0\nDICTIONARY\n  5\n82\n330\n80\n100\nAcDbDictionary\n280\n1\n281\n1\n  3\nBACK\n360\n80\n"
                + text.Substring(end);
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDBDictionary dictionary = ByHandle<DxfLine>(db, "3E").ExtensionDictionary;

            DxfDBObject removed = dictionary.Remove("S");

            Assert.True(removed.IsErased);
            Assert.Empty(dictionary);
            string saved = db.ToText();
            Assert.DoesNotContain(Records(saved), r => HandleOf(r) == "80" || HandleOf(r) == "82");
            Assert.Empty(ByHandle<DxfLine>(DxfDatabase.Parse(saved), "3E").ExtensionDictionary);
        }

        [Fact]
        public void ADictionaryWritten_KeepsTheCodeEachEntryWasReadWith_AndAnEntryAddedIsOwnedAsTheDictionaryOwns()
        {
            // The extension dictionary 92 of the LINE 3E owns its entries hard (280 1), but lists one entry soft, by 350.
            string text = DxfFileEzdxfDrawings.R2018.Replace(
                "  5\n92\n330\n3E\n100\nAcDbDictionary\n280\n1\n281\n1\n",
                "  5\n92\n330\n3E\n100\nAcDbDictionary\n280\n1\n281\n1\n  3\nGH_SOFT\n350\n80\n");
            int end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
            text = text.Substring(0, end) + "  0\nXRECORD\n  5\n80\n330\n92\n100\nAcDbXrecord\n280\n1\n  1\nkept\n" + text.Substring(end);
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDBDictionary dictionary = ByHandle<DxfLine>(db, "3E").ExtensionDictionary;
            Assert.Equal(new[] { "GH_SOFT" }, dictionary.Select(e => e.Key));

            var added = new DxfDBDictionary();
            dictionary.SetAt("GH_NEW", added);

            Assert.EndsWith("281\n1\n  3\nGH_SOFT\n350\n80\n  3\nGH_NEW\n360\n" + added.Handle + "\n", Records(db.ToText()).Single(r => HandleOf(r) == "92"), StringComparison.Ordinal);
        }
    }
}

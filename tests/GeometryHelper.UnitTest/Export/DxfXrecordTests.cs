using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Export;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// XRECORDs read as ezdxf 1.4.5 reads their tags, made in code and added to a dictionary, their data set, appended and
    /// erased; ezdxf read the probe's files back, its recover and audit finding nothing.
    /// </summary>
    public class DxfXrecordTests
    {
        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void TheDataOfAnXrecord_IsItsGroupsAfterTheCloningFlag_AsEzdxfReadsItsTags(string version)
        {
            DxfDatabase db = DxfDatabase.Parse(Drawing(version));
            var xrecord = Assert.IsType<DxfXrecord>(db.NamedObjectsDictionary.GetAt("GH_DATA"));

            // ezdxf: [1 'B-12'] [40 12.5] [70 3] [10 (1.5, 2.5, 3.5)] [330 '33'] [102 '{GH_NOTE'] [1 'in a list'] [102 '}'] [300 ...]
            Assert.Equal(
                new[] { "1 B-12", "40 12.5", "70 3", "10 1.5", "20 2.5", "30 3.5", "330 33", "102 {GH_NOTE", "1 in a list", "102 }", "300 " + (version == "R2018" ? "Wand€Außen" : "WandAussen") },
                xrecord.Data.Select(g => g.ToString()));
            Assert.Equal(xrecord.Data.Select(g => g.ToString()), xrecord.Select(g => g.ToString()));
            Assert.Equal("39", xrecord.Handle);
            Assert.Equal("A", xrecord.OwnerHandle);
        }

        [Fact]
        public void AnXrecordMadeInCode_IsWrittenAsAutoCADWritesOne_AndReadBack()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            var xrecord = new DxfXrecord { Data = new[] { new DxfTypedValue(1, "B-12"), new DxfTypedValue(40, "12.5") } };
            xrecord.Append(new[] { new DxfTypedValue(1, "Đ") });

            Assert.Null(xrecord.Handle);
            db.NamedObjectsDictionary.SetAt("GH_PLATE", xrecord);

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "A", xrecord.Handle);
            Assert.Equal("  0\nXRECORD\n  5\n" + xrecord.Handle + "\n102\n{ACAD_REACTORS\n330\nA\n102\n}\n330\nA\n100\nAcDbXrecord\n280\n1\n  1\nB-12\n 40\n12.5\n  1\nĐ\n", Records(saved).Single(r => HandleOf(r) == xrecord.Handle));

            var read = (DxfXrecord)DxfDatabase.Parse(saved).NamedObjectsDictionary["GH_PLATE"];
            Assert.Equal(new[] { "1 B-12", "40 12.5", "1 Đ" }, read.Data.Select(g => g.ToString()));
        }

        [Fact]
        public void DataSet_KeepsTheBytesOfTheGroupsItKeeps_AndItsOwnDataChangesNothing()
        {
            string text = DxfObjectsEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            var xrecord = (DxfXrecord)db.NamedObjectsDictionary["GH_DATA"];

            xrecord.Data = xrecord.Data.ToList();
            xrecord.Append(new DxfTypedValue[0]);
            Assert.Equal(text, db.ToText());

            var data = xrecord.Data.ToList();
            data[1] = new DxfTypedValue(40, "13.75");
            data.RemoveAt(data.Count - 1);
            xrecord.Data = data;

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "39");
            string record = Records(saved).Single(r => HandleOf(r) == "39");
            Assert.Contains("280\n1\n  1\nB-12\n 40\n13.75\n 70\n3\n", record, StringComparison.Ordinal);
            Assert.EndsWith("102\n}\n", record, StringComparison.Ordinal);
            Assert.Equal(10, ((DxfXrecord)DxfDatabase.Parse(saved).NamedObjectsDictionary["GH_DATA"]).Data.Count);
        }

        [Fact]
        public void DataAnXrecordDoesNotHold_IsRefused_AndNothingIsWritten()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            var xrecord = (DxfXrecord)db.NamedObjectsDictionary["GH_DATA"];

            Assert.Throws<ArgumentNullException>(() => xrecord.Data = null);
            Assert.Throws<ArgumentException>(() => xrecord.Data = new[] { new DxfTypedValue(5, "1F") });
            Assert.Throws<ArgumentException>(() => xrecord.Data = new[] { new DxfTypedValue(100, "AcDbXrecord") });
            Assert.Throws<ArgumentException>(() => xrecord.Data = new[] { new DxfTypedValue(1000, "x") });
            Assert.Throws<ArgumentException>(() => xrecord.Data = new[] { new DxfTypedValue(102, "{OPEN") });
            Assert.Throws<ArgumentException>(() => xrecord.Data = new[] { new DxfTypedValue(102, "}") });
            Assert.Throws<ArgumentException>(() => new DxfXrecord().Data = new[] { new DxfTypedValue(0, "LINE") });
            Assert.Equal(text, db.ToText());
        }

        [Fact]
        public void AnXrecordErased_GoesWithTheEntryThatNamesIt()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDBDictionary root = db.NamedObjectsDictionary;
            var xrecord = (DxfXrecord)root["GH_DATA"];

            xrecord.Erase();

            Assert.True(xrecord.IsErased);
            Assert.False(root.Contains("GH_DATA"));
            Assert.Throws<InvalidOperationException>(() => xrecord.Erase());
            Assert.Throws<InvalidOperationException>(() => xrecord.Data = new[] { new DxfTypedValue(1, "x") });
            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "A", "39");
            Assert.DoesNotContain(Records(saved), r => HandleOf(r) == "39");
            Assert.Throws<InvalidOperationException>(() => new DxfXrecord().Erase());
        }

        private static string Drawing(string version) => version == "R2000" ? DxfObjectsEzdxfDrawings.R2000 : DxfObjectsEzdxfDrawings.R2018;
    }
}

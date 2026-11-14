using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The variables of CENTERMARK and CENTERLINE, kept as DICTIONARYVARs of AcDbVariableDictionary as AutoCAD 2021 kept them
    /// in its console: read with its defaults where a drawing gives none, and written the way it wrote them (a real as six
    /// places, an expression as typed).
    /// </summary>
    public class DxfCenterVariablesTests
    {
        [Fact]
        public void TheCenterVariablesOfADrawingThatGivesNone_AreAutoCadsDefaults()
        {
            var db = new DxfDatabase("AC1027");

            Assert.Equal(0.12, db.CenterExe);
            Assert.True(db.CenterMarkExe);
            Assert.Equal("Center2", db.CenterLtype);
            Assert.Equal(1.0, db.CenterLtScale);
            Assert.Equal("0.1x", db.CenterCrossSize);
            Assert.Equal("0.05x", db.CenterCrossGap);
            Assert.Equal(".", db.CenterLayer);
        }

        [Fact]
        public void TheCenterVariablesSet_AreDictionaryVarsAsAutoCadWroteThem_AndReadBack()
        {
            var db = new DxfDatabase("AC1027");

            db.CenterExe = 0.5;
            db.CenterMarkExe = false;
            db.CenterLtype = "DASHED";
            db.CenterLtScale = 2.0;
            db.CenterCrossSize = "0.2x";
            db.CenterCrossGap = "0.1x";
            db.CenterLayer = "0";

            string saved = db.ToText();
            DxfDatabase again = DxfDatabase.Parse(saved);
            var root = (DxfDBDictionary)DxfDBObject.OfObject(again.File.ObjectItem(again.File.RootDictionaryHandle()));
            var variables = (DxfDBDictionary)root["AcDbVariableDictionary"];

            Assert.Equal(
                new[]
                {
                    "CENTEREXE",
                    "CENTERMARKEXE",
                    "CENTERLTYPE",
                    "CENTERLTSCALE",
                    "CENTERCROSSSIZE",
                    "CENTERCROSSGAP",
                    "CENTERLAYER",
                },
                variables.Select(e => e.Key)
            );
            Assert.Contains(
                "\n  1\n0.500000\n",
                Record(saved, variables["CENTEREXE"].Handle),
                StringComparison.Ordinal
            );
            Assert.Contains(
                "\n  1\n2.000000\n",
                Record(saved, variables["CENTERLTSCALE"].Handle),
                StringComparison.Ordinal
            );
            Assert.Contains("\n  1\n0\n", Record(saved, variables["CENTERMARKEXE"].Handle), StringComparison.Ordinal);
            Assert.Contains(
                "\n  1\n0.2x\n",
                Record(saved, variables["CENTERCROSSSIZE"].Handle),
                StringComparison.Ordinal
            );

            Assert.Equal(0.5, again.CenterExe);
            Assert.False(again.CenterMarkExe);
            Assert.Equal("DASHED", again.CenterLtype);
            Assert.Equal(2.0, again.CenterLtScale);
            Assert.Equal("0.2x", again.CenterCrossSize);
            Assert.Equal("0.1x", again.CenterCrossGap);
            Assert.Equal("0", again.CenterLayer);
        }

        [Fact]
        public void ACenterVariableOutOfRange_IsRefused()
        {
            var db = new DxfDatabase("AC1027");

            Assert.Throws<ArgumentOutOfRangeException>(() => db.CenterLtScale = 0.0);
            Assert.Throws<ArgumentNullException>(() => db.CenterLtype = null);
            Assert.Throws<ArgumentException>(() => db.CenterCrossSize = "abc");
            Assert.Throws<ArgumentException>(() => db.CenterCrossGap = "x");
            Assert.Equal(1.0, db.CenterLtScale);
        }

        private static string Record(string text, string handle)
        {
            string[] records = text.Split(new[] { "  0\n" }, StringSplitOptions.None);
            return "  0\n" + records.Single(r => r.Contains("\n  5\n" + handle + "\n"));
        }
    }
}

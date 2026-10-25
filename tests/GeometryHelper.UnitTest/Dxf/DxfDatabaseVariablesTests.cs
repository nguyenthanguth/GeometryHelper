using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The system variables AutoCAD keeps out of the header, as DICTIONARYVAR objects of AcDbVariableDictionary: read with
    /// AutoCAD's defaults where a drawing gives none, and set, the dictionary of variables added where there is none; ezdxf
    /// 1.4.5 read the probe's CETRANSPARENCY, ANNOALLVISIBLE and the rest back, its recover and audit finding nothing.
    /// </summary>
    public class DxfDatabaseVariablesTests
    {
        [Fact]
        public void TheVariablesOfADrawingThatGivesNone_AreAutoCADsDefaults()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);

                Assert.Equal("1:1", db.Cannoscale);
                Assert.Equal(DxfTransparency.ByLayer, db.Cetransparency);
                Assert.Equal("Standard", db.Tablestyle);
                Assert.Equal("Standard", db.MLeaderstyle);
                Assert.Null(db.DetailViewStyle);
                Assert.Null(db.SectionViewStyle);
                Assert.Equal(1.0, db.MLeaderscale);
                Assert.True(db.AnnoAllVisible);
                Assert.True(db.MsLtScale);
                Assert.Equal(1.0, db.MsOleScale);
                Assert.Equal(0, db.LayerEval);
                Assert.Equal(2, db.LightingUnits);
                Assert.True(db.LightsInBlocks);
                Assert.Equal(1, db.TStackAlign);
                Assert.Equal(70, db.TstackSize);
                Assert.False(db.HpInherit);
                Assert.Equal(4, db.Isolines);
                Assert.Equal(0.5, db.Facetres);
                Assert.Equal(1, db.PdfFrame);
                Assert.False(db.PlineEllipse);
                Assert.Null(db.ThumbnailBitmap);
            }
        }

        [Fact]
        public void AVariableSet_IsADictionaryVarOfTheDictionaryOfVariables_AddedWhereTheDrawingHasNone()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);

            db.AnnoAllVisible = db.AnnoAllVisible;
            db.Cetransparency = db.Cetransparency;
            db.TstackSize = db.TstackSize;
            db.Facetres = db.Facetres;
            db.Tablestyle = db.Tablestyle;
            Assert.Equal(text, db.ToText());

            db.Cetransparency = new DxfTransparency(127);
            db.AnnoAllVisible = false;
            db.TstackSize = 55;
            db.Facetres = 2.5;
            db.PlineEllipse = true;

            string saved = db.ToText();
            DxfDatabase again = DxfDatabase.Parse(saved);
            var root = (DxfDBDictionary)DxfDBObject.OfObject(again.File.ObjectItem(again.File.RootDictionaryHandle()));
            var variables = (DxfDBDictionary)root["AcDbVariableDictionary"];
            Assert.Equal(new[] { "CETRANSPARENCY", "ANNOALLVISIBLE", "TSTACKSIZE", "FACETRES", "PELLIPSE" }, variables.Select(e => e.Key));
            Assert.Contains("  3\nAcDbVariableDictionary\n350\n" + variables.Handle + "\n", Records(saved).Single(r => HandleOf(r) == root.Handle), StringComparison.Ordinal);
            string size = variables["TSTACKSIZE"].Handle;
            Assert.Equal("  0\nDICTIONARYVAR\n  5\n" + size + "\n102\n{ACAD_REACTORS\n330\n" + variables.Handle + "\n102\n}\n330\n" + variables.Handle + "\n100\nDictionaryVariables\n280\n0\n  1\n55\n", Records(saved).Single(r => HandleOf(r) == size));
            Assert.Contains("\n  1\n50\n", Records(saved).Single(r => HandleOf(r) == variables["CETRANSPARENCY"].Handle), StringComparison.Ordinal);

            Assert.Equal(new DxfTransparency(127), again.Cetransparency);
            Assert.False(again.AnnoAllVisible);
            Assert.Equal(55, again.TstackSize);
            Assert.Equal(2.5, again.Facetres);
            Assert.True(again.PlineEllipse);

            // Set again: the value of the DICTIONARYVAR in place, the dictionaries as they are.
            again.TstackSize = 60;
            OnlyTheseChanged(saved, again.ToText(), size);
            Assert.Equal(60, DxfDatabase.Parse(again.ToText()).TstackSize);
        }

        [Fact]
        public void AVariableOutOfRange_OrNamingWhatTheDrawingHasNot_IsRefused()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            string text = db.ToText();

            Assert.Throws<ArgumentOutOfRangeException>(() => db.TstackSize = 10);
            Assert.Throws<ArgumentOutOfRangeException>(() => db.Facetres = 20);
            Assert.Throws<ArgumentException>(() => db.Cetransparency = new DxfTransparency(10));
            Assert.Throws<ArgumentException>(() => db.Cannoscale = "1:50");
            Assert.Throws<ArgumentException>(() => db.Tablestyle = "GH_NONE");
            Assert.Throws<ArgumentNullException>(() => db.MLeaderstyle = null);
            Assert.Equal(text, db.ToText());

            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            Assert.Throws<InvalidOperationException>(() => r12.AnnoAllVisible = false);
            Assert.Throws<InvalidOperationException>(() => r12.Cannoscale = "1:50");
        }

        [Fact]
        public void TheFittingVariablesOfTheHeader_AreReadAndSetInPlace()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);

            Assert.Equal(new[] { 8, 6, 6, 6, 6 }, new[] { db.Splinesegs, db.Splinetype, db.Surftype, db.Surfu, db.Surfv });
            db.Splinesegs = db.Splinesegs;
            db.Splinetype = db.Splinetype;
            db.Surftype = db.Surftype;
            db.Surfu = db.Surfu;
            db.Surfv = db.Surfv;
            Assert.Equal(text, db.ToText());

            db.Splinesegs = -12;
            db.Splinetype = 5;
            db.Surftype = 8;
            db.Surfu = 20;
            db.Surfv = 30;

            string saved = db.ToText();
            OnlyTheseChanged(text, saved);
            Assert.Contains("$SPLINETYPE\n 70\n5\n  9\n$SPLINESEGS\n 70\n-12\n", saved, StringComparison.Ordinal);
            DxfDatabase again = DxfDatabase.Parse(saved);
            Assert.Equal(new[] { -12, 5, 8, 20, 30 }, new[] { again.Splinesegs, again.Splinetype, again.Surftype, again.Surfu, again.Surfv });
            Assert.Throws<ArgumentOutOfRangeException>(() => db.Splinesegs = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => db.Splinetype = 7);
            Assert.Throws<ArgumentOutOfRangeException>(() => db.Surftype = 7);
            Assert.Throws<ArgumentOutOfRangeException>(() => db.Surfu = 201);
        }

        [Fact]
        public void TheAnnotationScale_IsOneOfTheDrawingsListOfScales()
        {
            const string ScaleList = "  0\nDICTIONARY\n  5\n14\n330\nA\n100\nAcDbDictionary\n281\n1\n";
            const string Scale = "  0\nSCALE\n  5\nF0\n330\n14\n100\nAcDbScale\n 70\n0\n300\n1:50\n140\n1.0\n141\n50.0\n290\n0\n";
            string text = DxfFileEzdxfDrawings.R2018;
            Assert.Contains(ScaleList, text, StringComparison.Ordinal);
            int end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
            string scaled = (text.Substring(0, end) + Scale + text.Substring(end)).Replace(ScaleList, ScaleList + "  3\nA0\n350\nF0\n");
            DxfDatabase db = DxfDatabase.Parse(scaled);

            db.Cannoscale = "1:50";

            Assert.Equal("1:50", DxfDatabase.Parse(db.ToText()).Cannoscale);
        }
    }
}

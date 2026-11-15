using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The extension dictionary <see cref="DxfDBObject.CreateExtensionDictionary"/> makes, as AutoCAD 2021's
    /// <c>CreateExtensionDictionary</c> writes it (probed in its console, mirror-4 GHM4A, a LINE, a layer, a block, the
    /// tables and a dictionary of a drawing written by DxfOut): an empty DICTIONARY owned by its object (330) and hard (280 of
    /// 1), with no <c>{ACAD_REACTORS</c> of its own, and the group <c>{ACAD_XDICTIONARY</c> in the object's record straight
    /// after its handle, before its reactors.
    /// </summary>
    public class DxfExtensionDictionaryFormTests
    {
        private static string RecordOf(string text, string handle) => Records(text).Single(r => HandleOf(r) == handle);

        private static string DictionaryRecord(string text, DxfDBObject owner) =>
            RecordOf(text, owner.ExtensionDictionary.Handle);

        private static void AssertOwnedAlone(string record, string owner)
        {
            Assert.DoesNotContain("ACAD_REACTORS", record);
            Assert.Contains("\n330\n" + owner + "\n100\nAcDbDictionary\n280\n1\n281\n1\n", record);
        }

        [Fact]
        public void ALineGivenAnExtensionDictionary_HasItOwnedByTheLineAndNamingNoReactor()
        {
            var db = new DxfDatabase("AC1027");
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            db.ModelSpace.AppendEntity(line);

            line.CreateExtensionDictionary();

            string text = db.ToText();
            AssertOwnedAlone(DictionaryRecord(text, line), line.Handle);
            Assert.Empty(line.ExtensionDictionary.GetPersistentReactorIds());
            Assert.StartsWith(
                "  0\nLINE\n  5\n"
                    + line.Handle
                    + "\n102\n{ACAD_XDICTIONARY\n360\n"
                    + line.ExtensionDictionary.Handle
                    + "\n102\n}\n330\n",
                RecordOf(text, line.Handle)
            );
        }

        [Fact]
        public void ALayerABlockAndTheTables_GivenAnExtensionDictionary_HaveItOwnedByThemAndNamingNoReactor()
        {
            var db = new DxfDatabase("AC1027");
            var layer = new DxfLayerTableRecord { Name = "GHL" };
            db.LayerTable.Add(layer);
            var block = new DxfBlockTableRecord { Name = "GHB" };
            db.BlockTable.Add(block);

            layer.CreateExtensionDictionary();
            block.CreateExtensionDictionary();
            db.LayerTable.CreateExtensionDictionary();
            db.BlockTable.CreateExtensionDictionary();

            string text = db.ToText();

            foreach (DxfDBObject owner in new DxfDBObject[] { layer, block, db.LayerTable, db.BlockTable })
            {
                AssertOwnedAlone(DictionaryRecord(text, owner), owner.Handle);
            }
        }

        [Fact]
        public void ADictionaryOfTheDrawingGivenOne_HasItBeforeItsReactors_AndItNamesNoReactor()
        {
            var db = new DxfDatabase("AC1027");
            var sub = new DxfDBDictionary();
            db.NamedObjectsDictionary.SetAt("GH_SUB", sub);

            sub.CreateExtensionDictionary();

            string text = db.ToText();
            AssertOwnedAlone(DictionaryRecord(text, sub), sub.Handle);
            Assert.StartsWith(
                "  0\nDICTIONARY\n  5\n"
                    + sub.Handle
                    + "\n102\n{ACAD_XDICTIONARY\n360\n"
                    + sub.ExtensionDictionary.Handle
                    + "\n102\n}\n102\n{ACAD_REACTORS\n",
                RecordOf(text, sub.Handle)
            );
        }
    }
}

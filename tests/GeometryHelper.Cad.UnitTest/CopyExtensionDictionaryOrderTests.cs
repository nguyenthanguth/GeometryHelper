using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The extension dictionary of a COPY, as AutoCAD 2021 writes it (probed in its console, mirror-5 GHM5SORT, GHM5PTR and GHM5,
    /// written by DxfOut): its entries stand ordered by name, the names compared as lower-case letters ("10" before "9", "_under"
    /// before "a", "AcDbX" before "GH_Y"); and an XRECORD of the copy whose ownership pointer (350, 360) names an XRECORD or a
    /// dictionary outside the copy names a clone of it, owned by the copied XRECORD, in no dictionary of the drawing.
    /// </summary>
    public class CopyExtensionDictionaryOrderTests
    {
        private static DxfTypedValue V(int code, string value) => new DxfTypedValue(code, value);

        private static string[] Keys(DxfDBDictionary dictionary) => dictionary.Select(e => e.Key).ToArray();

        private static DxfLine Copied(CadDocument document, DxfLine line)
        {
            var before = new HashSet<string>(All<DxfLine>(document.ModelSpace).Select(e => e.Handle));
            Run(Session(document), "COPY", new[] { line.Handle }, Enter, P(0, 0), P(0, -100), Enter);
            return All<DxfLine>(document.ModelSpace).Single(e => !before.Contains(e.Handle));
        }

        [Fact]
        public void ALineCopied_HasTheEntriesOfItsExtensionDictionaryOrderedByName()
        {
            CadDocument document = Blank();
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            document.Database.ModelSpace.AppendEntity(line);
            line.CreateExtensionDictionary();
            string[] names = { "zz_last", "GH_PLAIN", "AA_FIRST", "9", "AcDbX", "_under", "10", "GH_SUB" };

            foreach (string name in names)
            {
                line.ExtensionDictionary.SetAt(name, new DxfXrecord { Data = new[] { V(1, name) } });
            }

            var inner = new DxfDBDictionary();
            line.ExtensionDictionary.SetAt("Beta", inner);
            inner.SetAt("zeta", new DxfXrecord { Data = new[] { V(1, "z") } });
            inner.SetAt("Alpha", new DxfXrecord { Data = new[] { V(1, "a") } });

            DxfLine copy = Copied(document, line);

            // AutoCAD's order: the lower-case letters compared as code units, so "_" (95) falls before "a" (97).
            Assert.Equal(
                new[] { "10", "9", "_under", "AA_FIRST", "AcDbX", "Beta", "GH_PLAIN", "GH_SUB", "zz_last" },
                Keys(copy.ExtensionDictionary)
            );
            Assert.Equal(new[] { "Alpha", "zeta" }, Keys((DxfDBDictionary)copy.ExtensionDictionary.GetAt("Beta")));

            // The original's own order is left as it was.
            Assert.Equal(
                new[] { "zz_last", "GH_PLAIN", "AA_FIRST", "9", "AcDbX", "_under", "10", "GH_SUB", "Beta" },
                Keys(line.ExtensionDictionary)
            );
            AssertReopens(document);
        }

        [Fact]
        public void AnAnnotativeHatchCopied_HasItsContextsAmongItsOtherEntriesByName()
        {
            CadDocument document = Blank();
            document
                .Database.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES")
                .AddContext(
                    new DxfAnnotationScale
                    {
                        Name = "1:1",
                        PaperUnits = 1,
                        DrawingUnits = 1,
                    }
                );
            var hatch = new DxfHatch();
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(42, 50), new GeoPoint2(58, 50), new GeoPoint2(58, 58), new GeoPoint2(42, 58) },
                null
            );
            document.Database.ModelSpace.AppendEntity(hatch);
            hatch.Annotative = DxfAnnotativeStates.True;
            hatch.ExtensionDictionary.SetAt("GH_PLAIN", new DxfXrecord { Data = new[] { V(1, "kept") } });
            hatch.ExtensionDictionary.SetAt("AA_FIRST", new DxfXrecord { Data = new[] { V(1, "first") } });
            var before = new HashSet<string>(All<DxfHatch>(document.ModelSpace).Select(e => e.Handle));

            Run(Session(document), "COPY", new[] { hatch.Handle }, Enter, P(0, 0), P(100, 0), Enter);

            DxfHatch copy = All<DxfHatch>(document.ModelSpace).Single(e => !before.Contains(e.Handle));

            // AutoCAD 2021 (GHM5, x-cp1): AA_FIRST, AcDbContextDataManager, GH_PLAIN, as the names compare; and in the manager
            // the view contexts before the contexts of the scales.
            Assert.Equal(new[] { "AA_FIRST", "AcDbContextDataManager", "GH_PLAIN" }, Keys(copy.ExtensionDictionary));
            Assert.Equal(
                new[] { "ACDB_ANNOTATIONSCALE_VIEW_COLLECTION", "ACDB_ANNOTATIONSCALES" },
                Keys((DxfDBDictionary)copy.ExtensionDictionary.GetAt("AcDbContextDataManager"))
            );
            AssertReopens(document);
        }

        [Fact]
        public void AnOwnershipPointerAtAnObjectOutsideTheCopy_NamesAClone_OwnedByTheCopiedXrecord()
        {
            CadDocument document = Blank();
            DxfDBDictionary root = document.Database.NamedObjectsDictionary;
            root.SetAt("GH_OUT1", new DxfXrecord { Data = new[] { V(1, "one") } });
            root.SetAt("GH_OUT2", new DxfXrecord { Data = new[] { V(1, "two") } });
            var outer = new DxfDBDictionary();
            root.SetAt("GH_OUTD", outer);
            outer.SetAt("INNER", new DxfXrecord { Data = new[] { V(1, "inner") } });
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            document.Database.ModelSpace.AppendEntity(line);
            var circle = new DxfCircle(new GeoPoint3(50, 50, 0), new GeoVector3(0, 0, 1), 5);
            document.Database.ModelSpace.AppendEntity(circle);
            line.CreateExtensionDictionary();
            string one = root.GetAt("GH_OUT1").Handle;
            string two = root.GetAt("GH_OUT2").Handle;
            line.ExtensionDictionary.SetAt(
                "GH_PTRS",
                new DxfXrecord
                {
                    Data = new[]
                    {
                        V(1, "ptrs"),
                        V(330, line.Handle),
                        V(340, circle.Handle),
                        V(350, one),
                        V(360, two),
                        V(350, outer.Handle),
                        V(350, circle.Handle),
                        V(350, one),
                    },
                }
            );
            int entries = root.Count;

            DxfLine copy = Copied(document, line);

            IReadOnlyList<DxfTypedValue> data = ((DxfXrecord)copy.ExtensionDictionary.GetAt("GH_PTRS")).Data;
            string made = copy.ExtensionDictionary.GetAt("GH_PTRS").Handle;
            Assert.Equal(copy.Handle, data[1].Value.Trim());
            Assert.Equal(circle.Handle, data[2].Value.Trim());

            // AutoCAD 2021 (GHM5PTR): the XRECORDs and the dictionary a 350 or 360 names are cloned, each owned by the copied XRECORD.
            string cloneOne = data[3].Value.Trim();
            string cloneTwo = data[4].Value.Trim();
            string cloneDictionary = data[5].Value.Trim();
            Assert.NotEqual(one, cloneOne);
            Assert.NotEqual(two, cloneTwo);
            Assert.NotEqual(outer.Handle, cloneDictionary);
            Assert.Equal(made, document.Database.GetObject(cloneOne).OwnerHandle);
            Assert.Equal(made, document.Database.GetObject(cloneTwo).OwnerHandle);
            Assert.Equal(made, document.Database.GetObject(cloneDictionary).OwnerHandle);
            Assert.Equal("one", ((DxfXrecord)document.Database.GetObject(cloneOne)).Data[0].Value.Trim());
            Assert.Equal("two", ((DxfXrecord)document.Database.GetObject(cloneTwo)).Data[0].Value.Trim());

            // The dictionary is cloned with its entry, owned by the clone.
            var below = (DxfDBDictionary)document.Database.GetObject(cloneDictionary);
            Assert.Equal(new[] { "INNER" }, Keys(below));
            Assert.NotEqual(outer.GetAt("INNER").Handle, below.GetAt("INNER").Handle);
            Assert.Equal(below.Handle, below.GetAt("INNER").OwnerHandle);

            // An entity named by an ownership pointer is not cloned; the pointer stays. Two pointers at one object share its clone.
            Assert.Equal(circle.Handle, data[6].Value.Trim());
            Assert.Equal(cloneOne, data[7].Value.Trim());

            // The objects outside are as they were, the clones in no dictionary of the drawing.
            Assert.Equal(entries, root.Count);
            Assert.Equal(
                new[] { "GH_OUT1", "GH_OUT2", "GH_OUTD" },
                Keys(root).Where(k => k.StartsWith("GH_OUT")).ToArray()
            );
            Assert.Equal(one, ((DxfXrecord)line.ExtensionDictionary.GetAt("GH_PTRS")).Data[3].Value.Trim());
            AssertReopens(document);
        }

        [Fact]
        public void APointerOfAnotherKind_AtAnObjectOutsideTheCopy_Stays()
        {
            CadDocument document = Blank();
            DxfDBDictionary root = document.Database.NamedObjectsDictionary;
            root.SetAt("GH_OUT1", new DxfXrecord { Data = new[] { V(1, "one") } });
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            document.Database.ModelSpace.AppendEntity(line);
            line.CreateExtensionDictionary();
            string one = root.GetAt("GH_OUT1").Handle;
            line.ExtensionDictionary.SetAt(
                "GH_SOFT",
                new DxfXrecord { Data = new[] { V(330, one), V(340, one), V(1, "soft") } }
            );

            DxfLine copy = Copied(document, line);

            IReadOnlyList<DxfTypedValue> data = ((DxfXrecord)copy.ExtensionDictionary.GetAt("GH_SOFT")).Data;
            Assert.Equal(one, data[0].Value.Trim());
            Assert.Equal(one, data[1].Value.Trim());
        }
    }
}

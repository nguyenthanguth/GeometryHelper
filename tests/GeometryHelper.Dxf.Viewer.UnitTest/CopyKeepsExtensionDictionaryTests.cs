using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// COPY and MIRROR of an entity that keeps data in its extension dictionary: AutoCAD 2021 copies the dictionary with its
    /// entries (probed in its console, mirror-4 GHM4B and GHM4E, a LINE and a CIRCLE copied and mirrored, written by DxfOut): a
    /// dictionary of the copy's own, each XRECORD and each dictionary below it a new object under the same name, a pointer
    /// at an object copied together (the entity, a sibling entry) naming its copy and any other left as it was.
    /// </summary>
    public class CopyKeepsExtensionDictionaryTests
    {
        private static DxfTypedValue V(int code, string value) => new DxfTypedValue(code, value);

        /// <summary>A drawing of a line from (0, 0) to (10, 0) and a circle, the line's extension dictionary full of entries.</summary>
        private static CadDocument Drawing(out DxfLine line, out DxfCircle circle)
        {
            CadDocument document = Blank();
            line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            document.Database.ModelSpace.AppendEntity(line);
            circle = new DxfCircle(new GeoPoint3(50, 50, 0), new GeoVector3(0, 0, 1), 5);
            document.Database.ModelSpace.AppendEntity(circle);
            line.CreateExtensionDictionary();
            DxfDBDictionary extension = line.ExtensionDictionary;
            extension.SetAt("GH_PLAIN", new DxfXrecord { Data = new[] { V(1, "hello"), V(90, "7") } });

            // A pointer at the line itself, one at the circle (which is not copied with it) and one at the entry above.
            extension.SetAt(
                "GH_POINTERS",
                new DxfXrecord { Data = new[] { V(330, line.Handle), V(340, circle.Handle), V(1, "points") } }
            );
            var inner = new DxfDBDictionary();
            extension.SetAt("GH_SUB", inner);
            inner.SetAt("GH_INNER", new DxfXrecord { Data = new[] { V(1, "inner") } });
            inner.SetAt("GH_BACK", new DxfXrecord { Data = new[] { V(330, extension.GetAt("GH_PLAIN").Handle) } });
            return document;
        }

        private static IReadOnlyList<DxfTypedValue> Data(DxfDBObject entry) => ((DxfXrecord)entry).Data;

        private static void AssertCopiedWhole(DxfLine line, DxfCircle circle, DxfLine copy)
        {
            DxfDBDictionary original = line.ExtensionDictionary;
            DxfDBDictionary extension = copy.ExtensionDictionary;

            Assert.NotNull(extension);
            Assert.NotEqual(original.Handle, extension.Handle);
            Assert.Equal(copy.Handle, extension.OwnerHandle);
            Assert.Empty(extension.GetPersistentReactorIds());
            Assert.Equal(new[] { "GH_PLAIN", "GH_POINTERS", "GH_SUB" }, extension.Select(e => e.Key).ToArray());

            DxfDBObject plain = extension.GetAt("GH_PLAIN");
            Assert.NotEqual(original.GetAt("GH_PLAIN").Handle, plain.Handle);
            Assert.Equal(extension.Handle, plain.OwnerHandle);
            Assert.Equal(new[] { "hello", "7" }, Data(plain).Select(g => g.Value.Trim()).ToArray());

            // The pointer at the line names the copy; the one at the circle, which was not copied, stays.
            IReadOnlyList<DxfTypedValue> pointers = Data(extension.GetAt("GH_POINTERS"));
            Assert.Equal(copy.Handle, pointers[0].Value.Trim());
            Assert.Equal(circle.Handle, pointers[1].Value.Trim());
            Assert.Equal(line.Handle, Data(original.GetAt("GH_POINTERS"))[0].Value.Trim());

            var inner = (DxfDBDictionary)extension.GetAt("GH_SUB");
            Assert.Equal(extension.Handle, inner.OwnerHandle);
            Assert.Equal(new[] { "GH_BACK", "GH_INNER" }, inner.Select(e => e.Key).OrderBy(k => k).ToArray());
            Assert.Equal("inner", Data(inner.GetAt("GH_INNER"))[0].Value.Trim());

            // A pointer at a sibling entry names its copy.
            Assert.Equal(plain.Handle, Data(inner.GetAt("GH_BACK"))[0].Value.Trim());
        }

        [Fact]
        public void ALineCopied_TakesItsExtensionDictionaryWithItsEntries()
        {
            CadDocument document = Drawing(out DxfLine line, out DxfCircle circle);
            var before = new HashSet<string>(All<DxfLine>(document.ModelSpace).Select(e => e.Handle));

            Run(Session(document), "COPY", new[] { line.Handle }, Enter, P(0, 0), P(0, -100), Enter);

            DxfLine copy = All<DxfLine>(document.ModelSpace).Single(e => !before.Contains(e.Handle));
            AssertCopiedWhole(line, circle, copy);
            AssertReopens(document);
        }

        [Fact]
        public void ALineMirrored_TakesItsExtensionDictionaryWithItsEntries()
        {
            CadDocument document = Drawing(out DxfLine line, out DxfCircle circle);
            var before = new HashSet<string>(All<DxfLine>(document.ModelSpace).Select(e => e.Handle));

            Run(Session(document), "MIRROR", new[] { line.Handle }, Enter, P(0, 100), P(10, 100), Enter);

            DxfLine copy = All<DxfLine>(document.ModelSpace).Single(e => !before.Contains(e.Handle));
            AssertCopiedWhole(line, circle, copy);
            AssertReopens(document);
        }

        [Fact]
        public void AnEntityWithAnEmptyExtensionDictionary_IsCopiedWithAnEmptyOne()
        {
            CadDocument document = Blank();
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            document.Database.ModelSpace.AppendEntity(line);
            line.CreateExtensionDictionary();
            var before = new HashSet<string>(All<DxfLine>(document.ModelSpace).Select(e => e.Handle));

            Run(Session(document), "COPY", new[] { line.Handle }, Enter, P(0, 0), P(0, -100), Enter);

            DxfLine copy = All<DxfLine>(document.ModelSpace).Single(e => !before.Contains(e.Handle));
            Assert.NotNull(copy.ExtensionDictionary);
            Assert.Equal(0, copy.ExtensionDictionary.Count);
            Assert.Equal(copy.Handle, copy.ExtensionDictionary.OwnerHandle);
        }

        [Fact]
        public void AnEntityWithNoExtensionDictionary_IsCopiedWithNone()
        {
            CadDocument document = Blank();
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            document.Database.ModelSpace.AppendEntity(line);
            var before = new HashSet<string>(All<DxfLine>(document.ModelSpace).Select(e => e.Handle));

            Run(Session(document), "COPY", new[] { line.Handle }, Enter, P(0, 0), P(0, -100), Enter);

            Assert.Null(All<DxfLine>(document.ModelSpace).Single(e => !before.Contains(e.Handle)).ExtensionDictionary);
        }

        [Fact]
        public void AnAnnotativeTextCopied_KeepsItsContextsAndTheDataBesideThem()
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
            var text = new DxfDBText
            {
                Position = new GeoPoint3(10, 10, 0),
                TextString = "ABC",
                Height = 2.5,
            };
            document.Database.ModelSpace.AppendEntity(text);
            text.Annotative = DxfAnnotativeStates.True;
            text.ExtensionDictionary.SetAt("GH_PLAIN", new DxfXrecord { Data = new[] { V(1, "kept") } });
            var before = new HashSet<string>(All<DxfDBText>(document.ModelSpace).Select(e => e.Handle));

            Run(Session(document), "COPY", new[] { text.Handle }, Enter, P(0, 0), P(0, -100), Enter);

            DxfDBText copy = All<DxfDBText>(document.ModelSpace).Single(e => !before.Contains(e.Handle));
            DxfDBDictionary extension = copy.ExtensionDictionary;
            Assert.True(extension.Contains("AcDbContextDataManager"));
            Assert.Equal("kept", Data(extension.GetAt("GH_PLAIN"))[0].Value.Trim());
            Assert.Equal(copy.Handle, extension.OwnerHandle);
            AssertReopens(document);
        }
    }
}

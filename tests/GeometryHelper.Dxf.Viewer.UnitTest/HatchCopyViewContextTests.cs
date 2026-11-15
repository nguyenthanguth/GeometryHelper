using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The copy of an annotative HATCH made by COPY, MIRROR, ROTATE or SCALE holds a view context, as AutoCAD 2021 writes one
    /// (probed in its console, mirror-4 GHM4G, GHM4H and GHM4I, hatches of ANSI31, HONEY, SOLID and others, written by DxfOut):
    /// the dictionary ACDB_ANNOTATIONSCALE_VIEW_COLLECTION of the context manager, before ACDB_ANNOTATIONSCALES, with one
    /// ACDB_HATCHVIEWCONTEXTDATA_CLASS under the name of the default context.
    /// </summary>
    public class HatchCopyViewContextTests
    {
        private const string Collection = "ACDB_ANNOTATIONSCALE_VIEW_COLLECTION";

        private static DxfAnnotationScale Scale(string name, int drawing) =>
            new DxfAnnotationScale
            {
                Name = name,
                PaperUnits = 1,
                DrawingUnits = drawing,
            };

        /// <summary>An annotative hatch of a user pattern at 30 degrees, lines 2 apart, over a rectangle, at 1:1 and, where asked, 1:2.</summary>
        private static CadDocument Hatched(bool second, out DxfHatch hatch)
        {
            CadDocument document = Blank();
            DxfObjectContextCollection scales = document.Database.ObjectContextManager.GetContextCollection(
                "ACDB_ANNOTATIONSCALES"
            );
            scales.AddContext(Scale("1:1", 1));
            scales.AddContext(Scale("1:2", 2));
            hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternAngle = 30.0 * Math.PI / 180.0;
            hatch.PatternSpace = 2.0;
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(42, 50), new GeoPoint2(58, 50), new GeoPoint2(58, 58), new GeoPoint2(42, 58) },
                null
            );
            document.Database.ModelSpace.AppendEntity(hatch);
            hatch.Annotative = DxfAnnotativeStates.True;

            if (second)
            {
                hatch.AddContext((DxfAnnotationScale)scales.GetContext("1:2"));
            }

            return document;
        }

        /// <summary>The groups of the record of a handle as "code=value", from its group 0 to the next.</summary>
        private static List<string> Groups(CadDocument document, string handle)
        {
            string[] lines = document.Database.ToText().Replace("\r\n", "\n").Split('\n');
            var groups = new List<string>();
            bool found = false;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1].Trim();

                if (code == 0 && found)
                {
                    break;
                }

                if (code == 0)
                {
                    groups.Clear();
                }

                groups.Add(code + "=" + value);

                if (code == 5 && value == handle)
                {
                    found = true;
                }
            }

            return found ? groups : null;
        }

        private static DxfHatch Copied(CadDocument document, DxfHatch hatch, params object[] inputs)
        {
            var before = new HashSet<string>(All<DxfHatch>(document.ModelSpace).Select(e => e.Handle));
            Run(Session(document), inputs.Prepend("COPY").ToArray());
            return All<DxfHatch>(document.ModelSpace).Single(e => !before.Contains(e.Handle));
        }

        private static DxfDBDictionary Manager(DxfDBObject entity) =>
            (DxfDBDictionary)entity.ExtensionDictionary.GetAt("AcDbContextDataManager");

        [Fact]
        public void TheCopyOfAnAnnotativeHatch_HoldsAViewContextOfItsDefaultScale_BeforeTheContextsOfItsScales()
        {
            CadDocument document = Hatched(false, out DxfHatch hatch);
            Assert.False(Manager(hatch).Contains(Collection));

            DxfHatch copy = Copied(document, hatch, new[] { hatch.Handle }, Enter, P(0, 0), P(100, 0), Enter);

            DxfDBDictionary manager = Manager(copy);
            Assert.Equal(new[] { Collection, "ACDB_ANNOTATIONSCALES" }, manager.Select(e => e.Key).ToArray());
            Assert.False(Manager(hatch).Contains(Collection));

            var views = (DxfDBDictionary)manager.GetAt(Collection);
            Assert.Equal(manager.Handle, views.OwnerHandle);
            Assert.Equal(new[] { "*A1" }, views.Select(e => e.Key).ToArray());
            DxfDBObject view = views.GetAt("*A1");
            Assert.Equal(views.Handle, view.OwnerHandle);
            Assert.Equal("0=ACDB_HATCHVIEWCONTEXTDATA_CLASS", Groups(document, view.Handle)[0]);

            // AutoCAD 2021 (GHM4H): the groups of the default context as the original held it, the origin of the pattern line
            // (43, 44) and the direction of the pattern (10, 20, 30) nought, then the subclass of the view.
            string[] own = Groups(
                    document,
                    ((DxfDBDictionary)manager.GetAt("ACDB_ANNOTATIONSCALES")).GetAt("*A1").Handle
                )
                .SkipWhile(g => g != "100=AcDbObjectContextData")
                .ToArray();
            string[] groups = Groups(document, view.Handle).SkipWhile(g => g != "100=AcDbObjectContextData").ToArray();
            int tail = Array.IndexOf(groups, "100=AcDbHatchViewContextData");
            Assert.True(tail > 0);
            Assert.Equal(
                new[] { "100=AcDbHatchViewContextData", "330=0", "10=0", "20=0", "30=1", "51=0", "290=0" },
                groups.Skip(tail)
            );
            string[] head = groups.Take(tail).ToArray();
            Assert.Equal(own.Length, head.Length);
            Assert.Contains("78=1", head);
            Assert.Contains("43=0", head);
            Assert.Contains("44=0", head);
            int scale = Array.IndexOf(head, "40=1");
            Assert.Equal(new[] { "10=0", "20=0", "30=0" }, head.Skip(scale + 1).Take(3));
            Assert.Equal(
                own.Take(scale + 1).Where(g => !g.StartsWith("43=") && !g.StartsWith("44=")),
                head.Take(scale + 1).Where(g => !g.StartsWith("43=") && !g.StartsWith("44="))
            );
            Assert.Contains("ACDB_HATCHVIEWCONTEXTDATA_CLASS", document.Database.ToText());
            AssertReopens(document);
        }

        [Fact]
        public void TheCopyOfAnAnnotativeHatchOfTwoScales_HoldsTheViewContextOfTheDefaultScaleAlone()
        {
            CadDocument document = Hatched(true, out DxfHatch hatch);

            DxfHatch copy = Copied(document, hatch, new[] { hatch.Handle }, Enter, P(0, 0), P(100, 0), Enter);

            DxfDBDictionary manager = Manager(copy);
            Assert.Equal(2, ((DxfDBDictionary)manager.GetAt("ACDB_ANNOTATIONSCALES")).Count);
            var views = (DxfDBDictionary)manager.GetAt(Collection);
            Assert.Equal(new[] { "*A1" }, views.Select(e => e.Key).ToArray());
        }

        [Fact]
        public void TheMirrorOfAnAnnotativeHatch_HoldsTheSameViewContext()
        {
            CadDocument document = Hatched(false, out DxfHatch hatch);
            var before = new HashSet<string>(All<DxfHatch>(document.ModelSpace).Select(e => e.Handle));

            Run(Session(document), "MIRROR", new[] { hatch.Handle }, Enter, P(0, 100), P(10, 110), Enter);

            DxfHatch copy = All<DxfHatch>(document.ModelSpace).Single(e => !before.Contains(e.Handle));
            Assert.Equal(
                new[] { "*A1" },
                ((DxfDBDictionary)Manager(copy).GetAt(Collection)).Select(e => e.Key).ToArray()
            );
        }
    }
}

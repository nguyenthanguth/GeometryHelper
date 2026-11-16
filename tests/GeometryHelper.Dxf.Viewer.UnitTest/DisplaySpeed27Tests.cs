using System;
using System.Globalization;
using System.IO;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The work of speed18 as the presenter sees it: a DWG opened from its file is the drawing it was when the DXF it is made
    /// into was copied to a new array before it was read, and its display is the same, item for item.
    /// </summary>
    public class DisplaySpeed27Tests
    {
        private static string WrittenDwg(int count)
        {
            var writer = new DxfWriter();

            for (int i = 0; i < count; i++)
            {
                double x = (i % 30) * 30.0 + 0.125;
                double y = (i / 30) * 30.0 + 0.0625;
                string layer = i % 2 == 0 ? "A" : "B";

                switch (i % 6)
                {
                    case 0:
                        writer.Add(new GeoLine2(new GeoPoint2(x, y), new GeoPoint2(x + 20.3, y + 12.7)), layer);
                        break;
                    case 1:
                        writer.Add(new GeoCircle2(new GeoPoint2(x, y), 5.5), layer);
                        break;
                    case 2:
                        writer.Add(new GeoArc2(new GeoPoint2(x, y), 6.25, 0.2, 3.5), layer);
                        break;
                    case 3:
                        writer.Add(
                            new GeoPolygon2(
                                new GeoPoint2(x, y),
                                new GeoPoint2(x + 20.0, y),
                                new GeoPoint2(x + 20.0, y + 20.0),
                                new GeoPoint2(x, y + 20.0)
                            ),
                            layer
                        );
                        break;
                    case 4:
                        writer.Add(
                            new GeoPolyline2(
                                new GeoPoint2(x, y),
                                new GeoPoint2(x + 20.0, y + 5.0),
                                new GeoPoint2(x + 30.0, y)
                            ),
                            layer
                        );
                        break;
                    default:
                        writer.Add(
                            new DxfText(
                                new GeoPoint3(x, y, 0.0),
                                new GeoVector3(0.0, 0.0, 1.0),
                                2.5,
                                0.0,
                                "T" + i.ToString(CultureInfo.InvariantCulture)
                            ),
                            layer: layer
                        );
                        break;
                }
            }

            string path = Path.Combine(Path.GetTempPath(), "gh-speed27-" + Guid.NewGuid().ToString("N") + ".dwg");
            DwgWriter.Write(DxfDatabase.Parse(writer.ToString()), path);
            return path;
        }

        [Fact]
        public void ADwgOpenedFromItsFile_IsTheDocumentOfItsDxfAsOlderCodeMadeIt_ItemForItem()
        {
            string path = WrittenDwg(1_200);

            try
            {
                CadDocument opened = CadDocument.Open(path);

                // The older road: the DXF cut to its length and read from a stream of it.
                DxfDatabase older = DwgReader.Open(path);
                CadDocument viaOlder = CadDocument.From(older, "older.dxf");

                Assert.Equal(older.ToText(), opened.Database.ToText());
                Assert.Equal(1_200, opened.ModelSpace.Block.Count);
                Assert.Equal(
                    DisplayPatchTests.Describe(viaOlder.GetDisplay(viaOlder.ModelSpace)),
                    DisplayPatchTests.Describe(opened.GetDisplay(opened.ModelSpace))
                );
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ADwgOpenedTwice_GivesTwoDocumentsThatShareNothing_AnEditOfOneLeavesTheOtherAsItWas()
        {
            string path = WrittenDwg(120);

            try
            {
                CadDocument first = CadDocument.Open(path);
                CadDocument second = CadDocument.Open(path);
                string before = second.Database.ToText();

                // The first is changed and shown; the second is the drawing it was.
                foreach (DxfEntity entity in first.ModelSpace.Block)
                {
                    entity.Erase();
                }

                first.Regen();

                Assert.Equal(0, first.ModelSpace.Block.Count);
                Assert.Equal(120, second.ModelSpace.Block.Count);
                Assert.Equal(before, second.Database.ToText());
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}

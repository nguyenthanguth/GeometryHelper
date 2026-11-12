using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The SPATIAL_FILTER of a clipped block reference (XCLIP) in a DWG: read from AutoCAD 2021's own DWG of R2004 to R2018 to the
    /// DXF AutoCAD wrote of it, and written by the DWG writer so that the reader reads it back to the same records. The drawing
    /// (made by a probe in accoreconsole, three references of one block: a rectangle clip under a turned UCS, a triangle with a
    /// front and a back plane switched off, and a clip turned inside out, which AutoCAD keeps as a polygon of eleven points with a slit) holds
    /// three filters.
    /// </summary>
    public class DwgSpatialFilterTests
    {
        private static readonly string[] Versions = { "AC1018", "AC1021", "AC1024", "AC1032" };

        private static DxfBlockReference[] Inserts(DxfDatabase database) =>
            database.ModelSpace.OfType<DxfBlockReference>().OrderBy(b => b.Position.X).ToArray();

        [Fact]
        public void TheClipsOfAutoCadsDwg_ReadAsTheDxfAutoCadWroteOfThem()
        {
            foreach (string version in Versions)
            {
                string name = "xclip_" + version;

                DwgAnswerKey.Result result = DwgAnswerKey.Compare(
                    DwgAnswerKey.Key(name),
                    DwgReader.ToDxf(DwgAnswerKey.Dwg(name))
                );

                // What else differs is the header and the tables AutoCAD works out as it reads a drawing made from a bare DXF, not the clips.
                Assert.DoesNotContain(result.Differences, difference => difference.Contains("SPATIAL"));
                Assert.DoesNotContain(result.Differences, difference => difference.Contains("ACAD_FILTER"));
                Assert.DoesNotContain("SPATIAL_FILTER", result.Missing.Keys);
                Assert.DoesNotContain("CLASS SPATIAL_FILTER", result.Missing.Keys);
            }
        }

        [Fact]
        public void TheClipsOfAutoCadsDwg_AreTheOnesItDrew_InEveryVersion()
        {
            foreach (string version in Versions)
            {
                Drew(DwgReader.Open(new MemoryStream(DwgAnswerKey.Dwg("xclip_" + version), false)));
            }
        }

        private static void Drew(DxfDatabase database)
        {
            DxfBlockReference[] inserts = Inserts(database);
            Assert.Equal(3, inserts.Length);

            DxfSpatialFilter rectangle = inserts[0].SpatialFilter;
            Assert.Equal(2, rectangle.Points.Count);
            Assert.True(rectangle.IsEnabled);
            Assert.Equal(0.433013, rectangle.InverseBlockTransform[0], 5);
            Assert.Equal(26.4634, rectangle.Origin.X, 3);

            DxfSpatialFilter triangle = inserts[1].SpatialFilter;
            Assert.Equal(3, triangle.Points.Count);
            Assert.False(triangle.IsEnabled);
            Assert.Equal(5.0, triangle.FrontClip);
            Assert.Equal(-5.0, triangle.BackClip);

            Assert.Equal(11, inserts[2].SpatialFilter.Points.Count);
            Assert.Equal(11, inserts[2].SpatialFilter.GetRing().Count);
        }

        [Fact]
        public void TheClipsWrittenAsDwg_ReadBackToTheSameRecords()
        {
            foreach (string version in Versions)
            {
                string name = "xclip_" + version;
                DxfDatabase source = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key(name), false));

                byte[] dwg;

                using (var stream = new MemoryStream())
                {
                    DwgWriter.Write(source, stream);
                    dwg = stream.ToArray();
                }

                DxfDatabase back = DwgReader.Open(new MemoryStream(dwg, false));
                DxfBlockReference[] was = Inserts(source);
                DxfBlockReference[] now = Inserts(back);
                Assert.Equal(3, now.Length);

                for (int i = 0; i < 3; i++)
                {
                    DxfSpatialFilter a = was[i].SpatialFilter;
                    DxfSpatialFilter b = now[i].SpatialFilter;
                    Assert.NotNull(b);
                    Assert.Equal(a.Points.Count, b.Points.Count);
                    Assert.Equal(a.IsEnabled, b.IsEnabled);
                    Assert.Equal(a.FrontClip, b.FrontClip);
                    Assert.Equal(a.BackClip, b.BackClip);

                    for (int k = 0; k < a.Points.Count; k++)
                    {
                        Assert.Equal(a.Points[k].X, b.Points[k].X, 9);
                        Assert.Equal(a.Points[k].Y, b.Points[k].Y, 9);
                    }

                    for (int k = 0; k < 12; k++)
                    {
                        Assert.Equal(a.InverseBlockTransform[k], b.InverseBlockTransform[k], 9);
                        Assert.Equal(a.WorldToClip[k], b.WorldToClip[k], 9);
                    }
                }
            }
        }
    }
}

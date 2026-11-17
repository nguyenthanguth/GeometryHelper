using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.TestDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The work of small1 in the display: an open polyline with a bulge in a tilted plane, which is read as a chain of arcs in
    /// space (<see cref="GeoPolylineArc3"/>), is drawn with no array of normals asked for. It is checked against what the
    /// entity drew before, item for item, and held by the bytes allocated, which do not vary with the machine as a time does.
    /// </summary>
    public class DisplaySpeed24Tests
    {
        private static string Handle(int i) => (0x100 + i).ToString("X", CultureInfo.InvariantCulture);

        // The plane of the polyline stands on the x axis, tilted by 36.87 degrees: not flat, so it is read as a chain in space.
        private const string Tilted = "210\n0.0\n220\n-0.6\n230\n0.8\n";

        private static string OnePolyline(string handle, double x, double y, string extrusion) =>
            Polyline("A", handle, false, x, y, 0.0, x + 20, y, 0.5, x + 20, y + 20, 0.0, x, y + 20, 0.0) + extrusion;

        [Fact]
        public void AnOpenPolylineWithABulgeInATiltedPlane_IsAChainInSpace()
        {
            CadDocument document = CadDocument.From(
                Drawing(string.Empty, string.Empty, OnePolyline("A1", 0, 0, Tilted)),
                "tilted.dxf"
            );

            var chain = Assert.IsType<GeoPolylineArc3>(
                document.ModelSpace.Block.Cast<DxfEntity>().Single().GetGeometry().Single()
            );
            Assert.Equal(4, chain.VertexCount);
            Assert.Equal(0.5, chain.GetBulgeAt(1));
        }

        [Fact]
        public void AChainWhoseNormalsWereAskedForBeforeItIsDrawn_IsDrawnItemForItem_AsOneWhoseNormalsWereNot()
        {
            string text = OnePolyline("A1", 0, 0, Tilted) + OnePolyline("A2", 50, 0, Tilted);
            CadDocument asked = CadDocument.From(Drawing(string.Empty, string.Empty, text), "asked.dxf");
            CadDocument plain = CadDocument.From(Drawing(string.Empty, string.Empty, text), "plain.dxf");

            foreach (DxfEntity entity in asked.ModelSpace.Block.Cast<DxfEntity>())
            {
                _ = ((GeoPolylineArc3)entity.GetGeometry().Single()).Normals;
            }

            DisplayList2 list = plain.GetDisplay(plain.ModelSpace);

            Assert.True(list.Items.OfType<DisplayArc2>().Any(), "no arc was drawn");
            Assert.Equal(
                DisplayPatchTests.Describe(list),
                DisplayPatchTests.Describe(asked.GetDisplay(asked.ModelSpace))
            );
        }

        [Fact]
        public void AnArcOfAChainOnTheUnderSide_IsDrawnTurnedAsItsNormalSaysIt()
        {
            // The same polyline in a plane turned over (its normal facing down) is drawn with the arc the other way round.
            DisplayArc2 up = Assert.Single(ArcsOf("210\n0.0\n220\n-0.6\n230\n0.8\n"));
            DisplayArc2 down = Assert.Single(ArcsOf("210\n0.0\n220\n0.6\n230\n-0.8\n"));

            Assert.True(up.Sweep * down.Sweep < 0.0, "sweeps " + up.Sweep + " and " + down.Sweep);
        }

        private static IEnumerable<DisplayArc2> ArcsOf(string extrusion)
        {
            CadDocument document = CadDocument.From(
                Drawing(string.Empty, string.Empty, OnePolyline("A1", 0, 0, extrusion)),
                "side.dxf"
            );
            return document.GetDisplay(document.ModelSpace).Items.OfType<DisplayArc2>().ToList();
        }

        [Fact]
        public void AColdDisplayOfTwentyThousandOpenPolylinesWithABulgeInATiltedPlane_MakesFewerThanEightHundredAndSixtyBytesAPolyline()
        {
            // The chain keeps its vertices and bulges and no array of normals, and the display draws it through GetNormalAt:
            // 1 636 bytes a polyline under .NET Framework before, 812 after: the array of normals and the three lists the chain grew
            // its arrays in are not made.
            const int count = 20_000;
            var text = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                text.Append(OnePolyline(Handle(i), (i % 300) * 30.0, (i / 300) * 30.0, Tilted));
            }

            CadDocument document = CadDocument.From(
                Drawing(Layer("A", 1), string.Empty, text.ToString()),
                "garbage.dxf"
            );

            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            DisplayList2 list = document.GetDisplay(document.ModelSpace);
            long used = (GC.GetAllocatedBytesForCurrentThread() - before) / count;

            Assert.True(list.Count >= count, "items " + list.Count);
            Assert.True(used < 860, "allocated " + used + " bytes a polyline");
        }
    }
}

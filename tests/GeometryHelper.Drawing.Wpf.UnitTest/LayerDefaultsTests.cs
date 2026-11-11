using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using Xunit;

namespace GeometryHelper.Drawing.Wpf.UnitTest
{
    /// <summary>The Layers page of Global settings lists every layer a model drawing writes.</summary>
    public class LayerDefaultsTests
    {
        [Fact]
        public async Task EveryLayerAModelDrawingWritesIsInTheDefaultLayerTable()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();
            var written = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfLayerTableRecord layer in made.Document.Database.LayerTable)
            {
                if (layer.Name != "0" && !string.Equals(layer.Name, "Defpoints", StringComparison.OrdinalIgnoreCase))
                {
                    written.Add(layer.Name);
                }
            }

            Assert.Contains("Symbol", written);
            Assert.Contains("Viewport", written);
            Assert.Empty(
                written.Except(new GlobalSettings().Layers.Select(l => l.Name), StringComparer.OrdinalIgnoreCase)
            );
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The turned words of a dimension laid out on many threads at once, each in a drawing of its own, as the test classes of
    /// this assembly run side by side: the fonts, their measures and the kept layouts are shared by the process only where
    /// the drawings say they are, so every thread puts the words where one thread alone does (once, a full run of the suite
    /// gave other coordinates for <see cref="DimensionTurnedWordsTests"/> under DIMTMOVE 1; thirteen full runs since and this
    /// test, run in a process of its own twenty times, found no state they share).
    /// </summary>
    public class DimensionTurnedWordsParallelTests
    {
        /// <summary>The words of a line of 4 turned 30 under DIMTMOVE 1 in the metric template with Arial words 2.5 high, as in <see cref="DimensionTurnedWordsTests"/>.</summary>
        private static GeoPoint3 WordsOfOneDrawing()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            CadDocument document = CadDocument.From(database, "probe.dxf");
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "Probe", FileName = "arial.ttf" });
            string start = CadDimStyleEditor.Current(database);
            var values = new SortedDictionary<string, object>(CadDimStyleEditor.Read(database.DimStyleTable[start]))
            {
                ["Dimasz"] = 2.5,
                ["Dimtxt"] = 2.5,
                ["Dimgap"] = 0.625,
                ["Dimexo"] = 0.625,
                ["Dimexe"] = 1.25,
                ["Dimscale"] = 1.0,
                ["Dimdec"] = 0,
                ["Dimtad"] = 1,
                ["Dimtih"] = false,
                ["Dimtoh"] = false,
                ["Dimtofl"] = true,
                ["Dimatfit"] = 3,
                ["Dimcen"] = 2.5,
                ["Dimjust"] = 0,
                ["Dimtix"] = false,
                ["Dimsoxd"] = false,
                ["Dimtmove"] = 1,
                ["Dimtvp"] = 0.0,
                ["Dimadec"] = 0,
                ["Dimazin"] = 0,
                ["Dimtxsty"] = "Probe",
            };
            CadDimStyleEditor.Add(document, "Probe", start, values);
            CadDimStyleEditor.SetCurrent(document, "Probe");
            CadCommandSession session = Session(document);
            Run(session, "DLI", P(0, 0), P(4, 0), "Text", "100", P(2, 30));
            DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, "A", "30");
            return dimension.DimBlock.OfType<DxfMText>().Single().Location;
        }

        [Fact]
        public void TurnedWords_LaidOutOnSixteenThreadsAtOnce_StandWhereOneThreadPutsThem()
        {
            GeoPoint3[] places = Enumerable
                .Range(0, 16)
                .Select(i => Task.Run(() => Enumerable.Range(0, 3).Select(k => WordsOfOneDrawing()).ToArray()))
                .ToArray()
                .SelectMany(t => t.Result)
                .ToArray();

            Assert.Equal(48, places.Length);
            Assert.All(places, p =>
                Assert.True(
                    Math.Abs(p.X - 4.9112) < 1E-3 && Math.Abs(p.Y - 35.4353) < 1E-3,
                    $"expected (4.9112, 35.4353), got ({p.X}, {p.Y})"
                )
            );
        }
    }
}

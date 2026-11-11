using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The arrowhead blocks AutoCAD adds to a drawing the first time a style or a leader uses one: their names and entities as
    /// AutoCAD 2021 writes them (read from its own blocks with AutoLISP's entget), made once, named by the dimension style's
    /// handles, given to a multileader from Properties, and drawn filled where AutoCAD fills them.
    /// </summary>
    public class ArrowheadBlockTests
    {
        private static DxfBlockTableRecord Made(string name, out DxfDatabase database)
        {
            database = new DxfDatabase("AC1027");
            DxfBlockTableRecord block = CadArrowheads.Ensure(database, name);
            Assert.NotNull(block);
            return block;
        }

        private static void Line(DxfEntity entity, double x1, double y1, double x2, double y2)
        {
            DxfLine line = Assert.IsType<DxfLine>(entity);
            Near(new GeoPoint3(x1, y1, 0), line.StartPoint, 1E-12);
            Near(new GeoPoint3(x2, y2, 0), line.EndPoint, 1E-12);
        }

        /// <summary>Each part on layer 0, colour and linetype ByBlock; a line, circle or arc of lineweight ByBlock, a polyline or solid of none of its own, as AutoCAD's.</summary>
        private static void AutoCadLook(DxfBlockTableRecord block)
        {
            foreach (DxfEntity part in block)
            {
                Assert.Equal("0", part.Layer);
                Assert.True(part.Color.IsByBlock);
                Assert.Equal("ByBlock", part.Linetype);
                Assert.Equal(
                    part is DxfLine || part is DxfCircle || part is DxfArc
                        ? DxfLineWeight.ByBlock
                        : DxfLineWeight.ByLayer,
                    part.LineWeight
                );
            }
        }

        [Fact]
        public void ClosedIsTheThreeSidesOfATriangleAThirdAsWideAsLong_AndALineDownItsMiddle()
        {
            DxfBlockTableRecord block = Made("Closed", out _);

            Assert.Equal("_Closed", block.Name);
            List<DxfEntity> parts = block.ToList();
            Assert.Equal(4, parts.Count);
            Line(parts[0], -1, 1.0 / 6.0, 0, 0);
            Line(parts[1], 0, 0, -1, -1.0 / 6.0);
            Line(parts[2], -1, 1.0 / 6.0, -1, -1.0 / 6.0);
            Line(parts[3], 0, 0, -1, 0);
            AutoCadLook(block);
        }

        [Fact]
        public void DotIsAPolylineOfTwoHalfCirclesHalfAUnitWide_AndALineBehindIt()
        {
            DxfBlockTableRecord block = Made("dot", out _);

            Assert.Equal("_Dot", block.Name);
            List<DxfEntity> parts = block.ToList();
            Assert.Equal(2, parts.Count);
            DxfPolyline dot = Assert.IsType<DxfPolyline>(parts[0]);
            Assert.True(dot.Closed);
            Assert.Equal(2, dot.NumberOfVertices);
            Assert.Equal(new GeoPoint2(-0.25, 0), dot.GetPoint2dAt(0));
            Assert.Equal(new GeoPoint2(0.25, 0), dot.GetPoint2dAt(1));
            Assert.Equal(1.0, dot.GetBulgeAt(0));
            Assert.Equal(1.0, dot.GetBulgeAt(1));
            Assert.Equal(0.5, dot.ConstantWidth);
            Assert.Equal(0.5, dot.GetStartWidthAt(1));
            Assert.Equal(0.5, dot.GetEndWidthAt(1));
            Line(parts[1], -0.5, 0, -1, 0);
            AutoCadLook(block);
        }

        [Fact]
        public void ArchitecturalTickIsAPolylineFifteenHundredthsWide_AndDotSmallADonutOfTwoSixteenths()
        {
            DxfPolyline tick = Assert.IsType<DxfPolyline>(Assert.Single(Made("Architectural tick", out _)));
            DxfPolyline small = Assert.IsType<DxfPolyline>(Assert.Single(Made("Dot small", out _)));

            Assert.False(tick.Closed);
            Assert.Equal(new GeoPoint2(-0.5, -0.5), tick.GetPoint2dAt(0));
            Assert.Equal(new GeoPoint2(0.5, 0.5), tick.GetPoint2dAt(1));
            Assert.Equal(0.15, tick.ConstantWidth);
            Assert.False(tick.HasBulges);
            Assert.True(small.Closed);
            Assert.Equal(new GeoPoint2(-0.0625, 0), small.GetPoint2dAt(0));
            Assert.Equal(new GeoPoint2(0.0625, 0), small.GetPoint2dAt(1));
            Assert.Equal(0.5, small.ConstantWidth);
        }

        [Fact]
        public void Open30_TheDatumTriangles_AndIntegral_CarryAutoCadsOwnNumbers()
        {
            List<DxfEntity> open30 = Made("Open 30", out _).ToList();
            List<DxfEntity> datum = Made("Datum triangle", out _).ToList();
            List<DxfArc> integral = Made("Integral", out _).Cast<DxfArc>().ToList();
            DxfSolid filled = Assert.IsType<DxfSolid>(Assert.Single(Made("Datum triangle filled", out _)));

            Line(open30[0], -1, 0.26794919, 0, 0);
            Line(open30[1], 0, 0, -1, -0.26794919);
            Line(open30[2], 0, 0, -1, 0);
            Line(datum[0], 0, 0.57735027, -1, 0);
            Line(datum[1], -1, 0, 0, -0.57735027);
            Line(datum[2], 0, -0.57735027, 0, 0.57735027);
            Near(new GeoPoint3(0, 0.57735027, 0), filled.GetPointAt(0));
            Near(new GeoPoint3(-1, 0, 0), filled.GetPointAt(1));
            Near(new GeoPoint3(0, -0.57735027, 0), filled.GetPointAt(2));
            Near(new GeoPoint3(0, -0.57735027, 0), filled.GetPointAt(3));
            Assert.Equal(2, integral.Count);
            Near(new GeoPoint3(0.44488802, -0.09133463, 0), integral[0].Center, 1E-12);
            Assert.Equal(0.45416667, integral[0].Radius, 12);
            Assert.Equal(1.780235837, integral[0].StartAngle, 12);
            Assert.Equal(2.932153143, integral[0].EndAngle, 12);
            Near(new GeoPoint3(-0.44488802, 0.09133463, 0), integral[1].Center, 1E-12);
            Assert.Equal(4.921828491, integral[1].StartAngle, 12);
            Assert.Equal(6.073745797, integral[1].EndAngle, 12);
        }

        [Fact]
        public void TheListIsAutoCads_AndEachBlockIsMadeOnceAndKept_NoneOfNothing_ClosedFilledOfNoBlock()
        {
            var database = new DxfDatabase("AC1027");
            int before = database.BlockTable.Count();

            Assert.Equal(20, CadArrowheads.Names.Count);
            Assert.Equal(new[] { "Closed filled", "Closed blank", "Closed", "Dot" }, CadArrowheads.Names.Take(4));
            Assert.Equal("Origin indicator 2", CadArrowheads.Names[8]);
            Assert.Equal("None", CadArrowheads.Names.Last());
            Assert.Null(CadArrowheads.Ensure(database, CadArrowheads.ClosedFilled));

            foreach (string name in CadArrowheads.Names.Skip(1))
            {
                DxfBlockTableRecord first = CadArrowheads.Ensure(database, name);
                Assert.Same(first, CadArrowheads.Ensure(database, name));
                Assert.Equal(name, CadArrowheads.NameOf(first));
            }

            Assert.Equal(before + 19, database.BlockTable.Count());
            Assert.Empty(database.BlockTable["_None"]);
            Assert.Equal(3, database.BlockTable["_Origin2"].Count());
            Assert.Equal(5, database.BlockTable["_BoxBlank"].Count());
            DxfDatabase again = DxfDatabase.Parse(database.ToText());
            Assert.Equal(database.ToText(), again.ToText());
        }

        [Fact]
        public void AStyleOfTwoArrowsAndALeaderArrow_NamesTheirBlocksByHandle_EachMadeOnce()
        {
            CadDocument document = Blank();
            string style = CadDimStyleEditor.Current(document.Database);
            SortedDictionary<string, object> values = CadDimStyleEditor.Read(document.Database.DimStyleTable[style]);
            values["Dimsah"] = true;
            values["Dimblk1"] = "Dot";
            values["Dimblk2"] = "Dot";
            values["Dimldrblk"] = "Integral";

            CadEditResult result = CadDimStyleEditor.Apply(document, style, values);

            Assert.True(result.Succeeded, result.Message);
            DxfDatabase database = document.Database;
            DxfDimStyleTableRecord record = database.DimStyleTable[style];
            Assert.Single(database.BlockTable, b => string.Equals(b.Name, "_Dot", StringComparison.OrdinalIgnoreCase));
            string dot = database.BlockTable["_Dot"].Handle;
            string integral = database.BlockTable["_Integral"].Handle;
            Assert.Equal(dot, record.Dimblk1.Handle);
            Assert.Equal(dot, record.Dimblk2.Handle);
            Assert.Equal(integral, record.Dimldrblk.Handle);
            string text = database.ToText();
            Assert.Contains("343\n" + dot + "\n", text, StringComparison.Ordinal);
            Assert.Contains("344\n" + dot + "\n", text, StringComparison.Ordinal);
            Assert.Contains("341\n" + integral + "\n", text, StringComparison.Ordinal);
            AssertReopens(document);
        }

        [Fact]
        public void ADimensionWithDots_DrawsAutoCadsDotAtEachEnd_ItsWidthScaledByTheArrowSize()
        {
            CadDocument document = Blank();
            string style = CadDimStyleEditor.Current(document.Database);
            SortedDictionary<string, object> values = CadDimStyleEditor.Read(document.Database.DimStyleTable[style]);
            values["Dimblk"] = "Dot";
            values["Dimasz"] = 4.0;
            Assert.True(CadDimStyleEditor.Apply(document, style, values).Succeeded);

            Run(Session(document), "DLI", P(0, 0), P(300, 0), P(150, -30));

            DxfRotatedDimension dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            List<DxfPolyline> dots = dimension.DimBlock.OfType<DxfPolyline>().ToList();
            Assert.Equal(2, dots.Count);
            double scale = document.Database.DimStyleTable[style].Dimscale;
            Assert.All(dots, d => Assert.Equal(0.5 * 4.0 * (scale > 0.0 ? scale : 1.0), d.ConstantWidth, 12));
            AssertReopens(document);
        }

        [Fact]
        public void AMultileaderTakesAnArrowheadFromProperties_ItsBlockMadeOnFirstUse_OneUndoStep()
        {
            CadDocument document = BlankWithMLeaderStyle();
            Run(Session(document), "MLD", P(0, 0), P(30, 20), "B-12");
            DxfMLeader mleader = Assert.Single(All<DxfMLeader>(document.ModelSpace));
            string before = document.Database.ToText();
            Assert.Equal("Closed filled", Arrowhead(document, mleader.Handle));

            CadEditResult result = CadPropertyEditor.Set(
                document,
                new[] { mleader.Handle },
                "Arrowhead",
                "datum triangle filled"
            );

            Assert.True(result.Succeeded, result.Message);
            mleader = Entity<DxfMLeader>(document, mleader.Handle);
            Assert.Equal("_DatumFilled", mleader.ArrowSymbol.Name);
            Assert.Equal("Datum triangle filled", Arrowhead(document, mleader.Handle));
            Assert.False(
                CadPropertyEditor.Set(document, new[] { mleader.Handle }, "Arrowhead", "Arrow of nobody").Succeeded
            );
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(before, document.Database.ToText());
        }

        private static string Arrowhead(CadDocument document, string handle) =>
            CadProperties
                .Describe(document, null, new[] { handle })
                .Groups.Single(g => g.Title == "Leaders")
                .Rows.Single(r => r.Label == "Arrowhead")
                .Value;

        [Fact]
        public void TheDotOfABlockIsDrawnFilled_ADiscOfHalfItsSize_AndTheTickAsABand()
        {
            var database = new DxfDatabase("AC1027");
            DxfBlockTableRecord dot = CadArrowheads.Ensure(database, "Dot");
            DxfBlockTableRecord tick = CadArrowheads.Ensure(database, "Architectural tick");
            database.ModelSpace.AppendEntity(
                new DxfBlockReference(new GeoPoint3(10, 10, 0), dot) { ScaleFactors = new GeoVector3(2, 2, 2) }
            );
            database.ModelSpace.AppendEntity(
                new DxfBlockReference(new GeoPoint3(100, 0, 0), tick) { ScaleFactors = new GeoVector3(4, 4, 4) }
            );
            CadDocument document = CadDocument.From(database, "arrows.dxf");

            List<DisplayFill2> fills = document.GetDisplay(document.ModelSpace).Items.OfType<DisplayFill2>().ToList();

            // The dot of radius 0.5 at scale 2 fills a disc of radius 1 about (10, 10): two half rings.
            List<DisplayFill2> disc = fills.Where(f => f.Bounds.MaxX < 50.0).ToList();
            Assert.Equal(2, disc.Count);
            Assert.Equal(9.0, disc.Min(f => f.Bounds.MinX), 9);
            Assert.Equal(11.0, disc.Max(f => f.Bounds.MaxX), 9);
            Assert.Equal(9.0, disc.Min(f => f.Bounds.MinY), 9);
            Assert.Equal(11.0, disc.Max(f => f.Bounds.MaxY), 9);

            // The tick from (98, -2) to (102, 2), 0.6 wide: a band of four corners.
            DisplayFill2 band = Assert.Single(fills, f => f.Bounds.MinX > 50.0);
            double[] ring = Assert.Single(band.Rings);
            Assert.Equal(8, ring.Length);
            double half = 0.3 / Math.Sqrt(2.0);
            Assert.Equal(98.0 - half, band.Bounds.MinX, 9);
            Assert.Equal(102.0 + half, band.Bounds.MaxX, 9);
        }
    }
}

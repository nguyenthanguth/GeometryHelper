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
    /// A dynamic block saved from the Block Editor has the anonymous *U blocks its INSERTs place worked out anew from the new
    /// definition, as AutoCAD 2021's <c>UpdateAnonymousBlocks</c> did to the same panel redefined through its API (plugin
    /// GhAn1): a new *U block for each one an INSERT places, at that INSERT's values, the INSERTs that shared one sharing the
    /// new one, the old ones left placed by none; one undo takes it back with the save.
    /// </summary>
    public class AnonymousBlocksUpdateTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        /// <summary>
        /// GH_PANEL, a dynamic rectangle 100 by 50 whose Width (100, 150 or 200) stretches its right edge, a short line from
        /// (10, 10) to (20, 10), and a cross line from (40, 20) to (60, 30) its visibility state Cross shows and Blank hides;
        /// INSERTs at (1 000, 500) given Width 150, at (2 000, 500) given Width 200 and Blank, at (3 000, 500) left as it is,
        /// and a copy of the first at (4 000, 500), which shares its *U block.
        /// </summary>
        private static CadDocument Panel(out string[] inserts)
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "GH_PANEL", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            DxfLine[] lines =
            {
                new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0)),
                new DxfLine(new GeoPoint3(10, 10, 0), new GeoPoint3(20, 10, 0)),
            };

            foreach (DxfLine line in lines)
            {
                block.AppendEntity(line);
            }

            var cross = new DxfLine(new GeoPoint3(40, 20, 0), new GeoPoint3(60, 30, 0));
            block.AppendEntity(cross);
            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                "Overall width",
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                DxfBlockParameterValueSet.List(new[] { 100.0, 150, 200 })
            );
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            block.AddVisibilityParameter(
                "Mark",
                string.Empty,
                new GeoPoint3(50, -30, 0),
                new[]
                {
                    new DxfBlockVisibilityState("Cross", new DxfEntity[] { cross }),
                    new DxfBlockVisibilityState("Blank", new DxfEntity[0]),
                }
            );
            var handles = new List<string>();

            foreach (double x in new[] { 1000.0, 2000, 3000 })
            {
                var insert = new DxfBlockReference(new GeoPoint3(x, 500, 0), block);
                db.ModelSpace.AppendEntity(insert);
                handles.Add(insert.Handle);
            }

            CadDocument document = CadDocument.From(db, "panel.dxf");
            Assert.True(CadDynamicBlocks.SetProperty(document, new[] { handles[0] }, "Width", "150").Succeeded);
            Assert.True(CadDynamicBlocks.SetProperty(document, new[] { handles[1] }, "Width", "200").Succeeded);
            Assert.True(CadDynamicBlocks.SetProperty(document, new[] { handles[1] }, "Mark", "Blank").Succeeded);
            List<string> before = document.Database.ModelSpace.Select(e => e.Handle).ToList();
            Start(document, "COPY", new[] { handles[0] }, Enter, P(0, 0), P(3000, 0), Enter);
            handles.Add(document.Database.ModelSpace.Select(e => e.Handle).Single(h => !before.Contains(h)));
            Assert.Equal(
                Entity<DxfBlockReference>(document, handles[0]).Name,
                Entity<DxfBlockReference>(document, handles[3]).Name
            );
            inserts = handles.ToArray();
            return document;
        }

        /// <summary>The entities of a block, each as its kind, visibility and points, in the block's order.</summary>
        private static List<string> Content(DxfBlockTableRecord block)
        {
            var content = new List<string>();

            foreach (DxfEntity entity in block)
            {
                switch (entity)
                {
                    case DxfLine line:
                        content.Add(
                            "LINE "
                                + (line.Visible ? string.Empty : "hidden ")
                                + Round(line.StartPoint)
                                + "-"
                                + Round(line.EndPoint)
                        );
                        break;
                    case DxfCircle circle:
                        content.Add(
                            "CIRCLE "
                                + Round(circle.Center)
                                + " r"
                                + Math.Round(circle.Radius, 9)
                                    .ToString(System.Globalization.CultureInfo.InvariantCulture)
                        );
                        break;
                    default:
                        content.Add(entity.GetType().Name);
                        break;
                }
            }

            return content;
        }

        private static string Round(GeoPoint3 p) =>
            "("
            + Math.Round(p.X, 9).ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ","
            + Math.Round(p.Y, 9).ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ")";

        private static double Width(DxfBlockReference insert) =>
            (double)insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == "Width").Value;

        /// <summary>
        /// The panel given a circle of radius 10 at (50, 25) and its short line erased in the Block Editor, and saved: AutoCAD
        /// 2021 made *U3 for the two INSERTs of *U1 (Width 150) and *U4 for the one of *U2 (Width 200, Blank), each from the
        /// new definition at those values, its cross hidden in *U4, and left *U1 and *U2 placed by none; the INSERT of the
        /// block's own values still placed GH_PANEL.
        /// </summary>
        [Fact]
        public void ADynamicBlockSavedFromTheBlockEditor_HasItsAnonymousBlocksWorkedOutAnew_AsAutoCADUpdatesThem()
        {
            CadDocument document = Panel(out string[] inserts);
            string start = document.Database.ToText();
            string first = Entity<DxfBlockReference>(document, inserts[0]).Name;
            string second = Entity<DxfBlockReference>(document, inserts[1]).Name;

            Start(document, "-BEDIT", "GH_PANEL", "CIRCLE", P(50, 25), "10", "ERASE", P(15, 10), Enter, "BCLOSE", "S");

            Assert.Null(document.BlockEditor);
            var a = Entity<DxfBlockReference>(document, inserts[0]);
            var b = Entity<DxfBlockReference>(document, inserts[1]);
            var c = Entity<DxfBlockReference>(document, inserts[2]);
            var d = Entity<DxfBlockReference>(document, inserts[3]);
            Assert.StartsWith("*U", a.Name, StringComparison.Ordinal);
            Assert.NotEqual(first, a.Name);
            Assert.Equal(a.Name, d.Name);
            Assert.StartsWith("*U", b.Name, StringComparison.Ordinal);
            Assert.NotEqual(second, b.Name);
            Assert.NotEqual(a.Name, b.Name);
            Assert.Equal("GH_PANEL", c.Name);
            Assert.Equal(
                new[]
                {
                    "LINE (150,0)-(150,50)",
                    "LINE (0,0)-(150,0)",
                    "LINE (0,50)-(150,50)",
                    "LINE (0,50)-(0,0)",
                    "LINE (40,20)-(60,30)",
                    "CIRCLE (50,25) r10",
                },
                Content(a.BlockTableRecord)
            );
            Assert.Equal(
                new[]
                {
                    "LINE (200,0)-(200,50)",
                    "LINE (0,0)-(200,0)",
                    "LINE (0,50)-(200,50)",
                    "LINE (0,50)-(0,0)",
                    "LINE hidden (40,20)-(60,30)",
                    "CIRCLE (50,25) r10",
                },
                Content(b.BlockTableRecord)
            );
            Assert.Equal(150.0, Width(a), 9);
            Assert.Equal(150.0, Width(d), 9);
            Assert.Equal(200.0, Width(b), 9);
            Assert.Equal(
                "Blank",
                (string)b.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == "Mark").Value
            );

            DxfBlockTableRecord dynamic = document.Database.BlockTable["GH_PANEL"];
            Assert.Equal(
                new[] { first, second, a.Name, b.Name },
                dynamic.GetAnonymousBlocks().Select(r => r.Name).ToArray()
            );
            Assert.Empty(document.Database.BlockTable[first].GetBlockReferences(true));
            Assert.Empty(document.Database.BlockTable[second].GetBlockReferences(true));
            AssertReopens(document);

            Assert.Equal("BEDIT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        /// <summary>
        /// A second save makes anew only the two anonymous blocks the INSERTs place, not the two the first save left placed by
        /// none, as AutoCAD 2021 passed over *U1 and *U2 when it updated the panel again; the INSERTs then show both circles.
        /// </summary>
        [Fact]
        public void ASecondSave_MakesAnewOnlyTheAnonymousBlocksAnInsertPlaces()
        {
            CadDocument document = Panel(out string[] inserts);
            Start(document, "-BEDIT", "GH_PANEL", "CIRCLE", P(50, 25), "10", "BCLOSE", "S");
            int count = document.Database.BlockTable["GH_PANEL"].GetAnonymousBlocks().Count;

            Start(document, "-BEDIT", "GH_PANEL", "CIRCLE", P(80, 25), "3", "BCLOSE", "S");

            Assert.Equal(count + 2, document.Database.BlockTable["GH_PANEL"].GetAnonymousBlocks().Count);
            Assert.Equal(8, Entity<DxfBlockReference>(document, inserts[0]).BlockTableRecord.Count(e => !e.IsErased));
        }
    }
}

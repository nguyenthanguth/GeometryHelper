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
    /// A dynamic block saved from the Block Editor after an entity one of its actions holds was erased has its anonymous *U
    /// blocks worked out anew with the entity gone and every value kept, as AutoCAD 2021's <c>UpdateAnonymousBlocks</c> did to
    /// the same panels with the same lines erased through its API (plugin GhAs1); the library left the old *U blocks as they
    /// were, the erased line still in them.
    /// </summary>
    public class AnonymousBlocksErasedTests
    {
        /// <summary>
        /// A panel of a right edge (100, 0)-(100, 50), a bottom (0, 0)-(100, 0), a top (0, 50)-(100, 50), a left (0, 50)-(0, 0),
        /// a short line (10, 10)-(20, 10) and a circle of radius 5 at (60, 25), a Width of 50 to 300 by 10 that the action
        /// given follows, and INSERTs at (1 000, 500) set to a Width of 150 and at (2 000, 500) set to 200.
        /// </summary>
        private static CadDocument Panel(
            Action<DxfBlockTableRecord, DxfBlockLinearParameter, List<DxfEntity>> action,
            out string[] inserts
        )
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "GH_PANEL", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var entities = new List<DxfEntity>
            {
                new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0)),
                new DxfLine(new GeoPoint3(10, 10, 0), new GeoPoint3(20, 10, 0)),
                new DxfCircle(new GeoPoint3(60, 25, 0), GeoVector3.ZAxis, 5),
            };

            foreach (DxfEntity entity in entities)
            {
                block.AppendEntity(entity);
            }

            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                "Overall width",
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                DxfBlockParameterValueSet.Increments(50, 300, 10)
            );
            action(block, width, entities);
            var handles = new List<string>();

            foreach (double x in new[] { 1000.0, 2000 })
            {
                var insert = new DxfBlockReference(new GeoPoint3(x, 500, 0), block);
                db.ModelSpace.AppendEntity(insert);
                handles.Add(insert.Handle);
            }

            CadDocument document = CadDocument.From(db, "panel.dxf");
            Assert.True(CadDynamicBlocks.SetProperty(document, new[] { handles[0] }, "Width", "150").Succeeded);
            Assert.True(CadDynamicBlocks.SetProperty(document, new[] { handles[1] }, "Width", "200").Succeeded);
            inserts = handles.ToArray();
            return document;
        }

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static string Number(double value) =>
            Math.Round(value, 9).ToString("0.#########", CultureInfo.InvariantCulture);

        /// <summary>The entities of a block, each as its kind and points, in the block's order; those erased left out.</summary>
        private static List<string> Content(DxfBlockTableRecord block)
        {
            var content = new List<string>();

            foreach (DxfEntity entity in block.Where(e => !e.IsErased))
            {
                switch (entity)
                {
                    case DxfLine line:
                        content.Add(
                            "LINE ("
                                + Number(line.StartPoint.X)
                                + ","
                                + Number(line.StartPoint.Y)
                                + ")-("
                                + Number(line.EndPoint.X)
                                + ","
                                + Number(line.EndPoint.Y)
                                + ")"
                        );
                        break;
                    case DxfCircle circle:
                        content.Add(
                            "CIRCLE ("
                                + Number(circle.Center.X)
                                + ","
                                + Number(circle.Center.Y)
                                + ") r"
                                + Number(circle.Radius)
                        );
                        break;
                    default:
                        content.Add(entity.GetType().Name);
                        break;
                }
            }

            return content;
        }

        private static double Width(DxfBlockReference insert) =>
            (double)insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == "Width").Value;

        /// <summary>
        /// A panel whose Width moves the right edge and the bottom (a move action), its bottom erased: AutoCAD 2021 worked
        /// out *U3 and *U4 with the right edge moved by the Width of each INSERT, the Widths kept, and no bottom.
        /// </summary>
        [Fact]
        public void AMoveWhoseBottomIsErased_GoesOnWithTheRightEdge_AndTheAnonymousBlocksLoseTheBottom()
        {
            CadDocument document = Panel(
                (block, width, entities) => block.AddMoveAction(width, entities.Take(2)),
                out string[] inserts
            );

            Start(document, "-BEDIT", "GH_PANEL", "ERASE", P(50, 0), Enter, "BCLOSE", "S");

            var a = Entity<DxfBlockReference>(document, inserts[0]);
            var b = Entity<DxfBlockReference>(document, inserts[1]);
            Assert.Equal(
                new[]
                {
                    "LINE (150,0)-(150,50)",
                    "LINE (0,50)-(100,50)",
                    "LINE (0,50)-(0,0)",
                    "LINE (10,10)-(20,10)",
                    "CIRCLE (60,25) r5",
                },
                Content(a.BlockTableRecord)
            );
            Assert.Equal(
                new[]
                {
                    "LINE (200,0)-(200,50)",
                    "LINE (0,50)-(100,50)",
                    "LINE (0,50)-(0,0)",
                    "LINE (10,10)-(20,10)",
                    "CIRCLE (60,25) r5",
                },
                Content(b.BlockTableRecord)
            );
            Assert.Equal(150.0, Width(a), 9);
            Assert.Equal(200.0, Width(b), 9);
        }

        /// <summary>
        /// A panel whose Width scales everything, its short line erased: AutoCAD 2021's *U blocks held the lines scaled by
        /// 1.5 and 2 and the circle of radius 7.5 and 10, and no short line.
        /// </summary>
        [Fact]
        public void AScaleWhoseShortLineIsErased_ScalesTheRest_AndTheAnonymousBlocksLoseTheLine()
        {
            CadDocument document = Panel(
                (block, width, entities) => block.AddScaleAction(width, entities),
                out string[] inserts
            );

            Start(document, "-BEDIT", "GH_PANEL", "ERASE", P(15, 10), Enter, "BCLOSE", "S");

            var a = Entity<DxfBlockReference>(document, inserts[0]);
            var b = Entity<DxfBlockReference>(document, inserts[1]);
            Assert.Equal(
                new[]
                {
                    "LINE (150,0)-(150,75)",
                    "LINE (0,0)-(150,0)",
                    "LINE (0,75)-(150,75)",
                    "LINE (0,75)-(0,0)",
                    "CIRCLE (90,37.5) r7.5",
                },
                Content(a.BlockTableRecord)
            );
            Assert.Equal(
                new[]
                {
                    "LINE (200,0)-(200,100)",
                    "LINE (0,0)-(200,0)",
                    "LINE (0,100)-(200,100)",
                    "LINE (0,100)-(0,0)",
                    "CIRCLE (120,50) r10",
                },
                Content(b.BlockTableRecord)
            );
            Assert.Equal(150.0, Width(a), 9);
            Assert.Equal(200.0, Width(b), 9);
        }

        /// <summary>
        /// A panel whose Width stretches the right edge, bottom and top, all three erased (the action is left holding
        /// nothing): AutoCAD 2021's *U blocks held the left, the short line and the circle, the Widths kept.
        /// </summary>
        [Fact]
        public void AStretchWhoseEveryLineIsErased_DoesNothing_AndTheAnonymousBlocksKeepTheRest()
        {
            CadDocument document = Panel(
                (block, width, entities) =>
                    block.AddStretchAction(
                        width,
                        new GeoPoint2(90, -10),
                        new GeoPoint2(110, 60),
                        entities.Take(3).Cast<DxfLine>()
                    ),
                out string[] inserts
            );

            Start(document, "-BEDIT", "GH_PANEL", "ERASE", P(100, 25), P(50, 0), P(50, 50), Enter, "BCLOSE", "S");

            var a = Entity<DxfBlockReference>(document, inserts[0]);
            var b = Entity<DxfBlockReference>(document, inserts[1]);
            string[] rest = { "LINE (0,50)-(0,0)", "LINE (10,10)-(20,10)", "CIRCLE (60,25) r5" };
            Assert.Equal(rest, Content(a.BlockTableRecord));
            Assert.Equal(rest, Content(b.BlockTableRecord));
            Assert.Equal(150.0, Width(a), 9);
            Assert.Equal(200.0, Width(b), 9);
        }

        /// <summary>
        /// A panel whose Width copies the right edge and the bottom by an array, its bottom erased: the library keeps the
        /// anonymous blocks it had, where AutoCAD 2021 drew no copies of the rest and (this block having that one action)
        /// reset the Width to 100, an answer with no rule behind it the library follows.
        /// </summary>
        [Fact]
        public void AnArrayWhoseBottomIsErased_LeavesTheAnonymousBlocksAsTheyWere()
        {
            CadDocument document = Panel(
                (block, width, entities) => block.AddArrayAction(width, entities.Take(2), 30),
                out string[] inserts
            );
            string first = Entity<DxfBlockReference>(document, inserts[0]).Name;

            Start(document, "-BEDIT", "GH_PANEL", "ERASE", P(50, 0), Enter, "BCLOSE", "S");

            var a = Entity<DxfBlockReference>(document, inserts[0]);
            Assert.Equal(first, a.Name);
            Assert.Equal(150.0, Width(a), 9);
        }
    }
}

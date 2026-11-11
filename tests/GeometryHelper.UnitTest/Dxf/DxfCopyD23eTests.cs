using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What D23d left of copying dynamic blocks (D23e): a drawing of dynamic blocks copied from on eight threads at once,
    /// each into a drawing of its own, is only read, and each thread copies what a copy made alone gives.
    /// </summary>
    public class DxfCopyD23eTests
    {
        /// <summary>
        /// A drawing of R2018 with the dynamic blocks GH_PANEL0 to GH_PANEL3, each a rectangle 100 by 50 of four lines given
        /// Width from (0, 0) to (100, 0), from 50 to 300 by 10, and the stretch of its right edge; for each, an INSERT at its own
        /// width, one set to 150 + 10 i, which places an anonymous block, and one more of that anonymous block; and a line.
        /// </summary>
        private static string Source()
        {
            var db = new DxfDatabase("AC1032");

            for (int i = 0; i < 4; i++)
            {
                DxfBlockTableRecord block = Panel(db, "GH_PANEL" + i);
                db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 200 * i, 0), block));
                var set = new DxfBlockReference(new GeoPoint3(400, 200 * i, 0), block);
                db.ModelSpace.AppendEntity(set);
                set.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == "Width").Value =
                    150.0 + 10 * i;
                db.ModelSpace.AppendEntity(
                    new DxfBlockReference(new GeoPoint3(800, 200 * i, 0), set.AnonymousBlockTableRecord)
                );
            }

            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, -10, 0), new GeoPoint3(1000, -10, 0)));
            return db.ToText();
        }

        private static DxfBlockTableRecord Panel(DxfDatabase db, string name)
        {
            var block = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var lines = new[]
            {
                new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0)),
            };

            foreach (DxfLine line in lines)
            {
                block.AppendEntity(line);
            }

            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                "Overall width of the panel",
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                DxfBlockParameterValueSet.Increments(50, 300, 10)
            );
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            return block;
        }

        /// <summary>
        /// The drawing a copy of a source gives, as text: inserted as it is (0), inserted as a block (1), copied whole by
        /// WblockCloneObjects (2), or copied by WblockCloneObjects one entity at a time (3).
        /// </summary>
        private static string Copy(DxfDatabase source, int way)
        {
            var into = new DxfDatabase("AC1032");

            if (way == 0)
            {
                into.Insert(GeoTransform3.Translation(new GeoVector3(1, 0, 0)), source, true);
            }
            else if (way == 1)
            {
                into.Insert("OUTER", source, true);
            }
            else if (way == 2)
            {
                source.WblockCloneObjects(source.ModelSpace.ToList(), into.ModelSpace);
            }
            else
            {
                foreach (DxfEntity entity in source.ModelSpace.ToList())
                {
                    source.WblockCloneObjects(new[] { entity }, into.ModelSpace);
                }
            }

            return into.ToText();
        }

        [Fact]
        public void ADrawingOfDynamicBlocksCopiedFromOnEightThreadsAtOnce_IsCopiedByEachAsItIsCopiedAlone()
        {
            // Each round opens the drawing again, so that nothing of it is read before the eight threads read it together.
            string text = Source();
            string[] alone = Enumerable.Range(0, 4).Select(way => Copy(DxfDatabase.Parse(text), way)).ToArray();
            Assert.All(alone, copy => Assert.Contains("ACAD_ENHANCEDBLOCK", copy));
            int wrong = 0;
            int thrown = 0;
            string first = null;
            var panels = new DxfBlockTableRecord[8];

            for (int round = 0; round < 60; round++)
            {
                DxfDatabase shared = DxfDatabase.Parse(text);
                Array.Clear(panels, 0, panels.Length);

                using (var go = new System.Threading.ManualResetEventSlim(false))
                {
                    int r = round;
                    var threads = Enumerable
                        .Range(0, 8)
                        .Select(t => new System.Threading.Thread(() =>
                        {
                            int way = (t + r) % 4;
                            go.Wait();

                            try
                            {
                                if (Copy(shared, way) != alone[way])
                                {
                                    System.Threading.Interlocked.Increment(ref wrong);
                                }

                                panels[t] = shared.BlockTable["GH_PANEL1"];
                            }
                            catch (Exception e)
                            {
                                System.Threading.Interlocked.Increment(ref thrown);
                                System.Threading.Interlocked.CompareExchange(
                                    ref first,
                                    e.GetType().Name + ": " + e.Message + e.StackTrace,
                                    null
                                );
                            }
                        }))
                        .ToList();

                    threads.ForEach(thread => thread.Start());
                    go.Set();
                    Assert.All(threads, thread =>
                        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "A thread copying did not end.")
                    );
                }

                Assert.True(
                    wrong == 0 && thrown == 0,
                    "Round "
                        + round
                        + ": "
                        + wrong
                        + " copies came out otherwise and "
                        + thrown
                        + " threw, the first "
                        + first
                );
                Assert.Equal(text, shared.ToText());
                Assert.All(panels, panel => Assert.Same(panels[0], panel));
                Assert.Same(panels[0], shared.BlockTable.Single(b => b.Name == "GH_PANEL1"));
            }
        }
    }
}

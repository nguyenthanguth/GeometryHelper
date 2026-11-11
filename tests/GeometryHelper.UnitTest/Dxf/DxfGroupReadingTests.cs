using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The groups of a record are found once for the readings of it that follow one another, and found anew once the record
    /// is written: each property read after a change reads the change, whichever setter wrote it, and two records written to
    /// the same length are each read as their own. The drawing is built in code and opened from its saved bytes, so that its
    /// entities are read from the bytes of a file, as a drawing opened is.
    /// </summary>
    public class DxfGroupReadingTests
    {
        private static readonly Tolerance Near = new Tolerance(1E-9, 1E-9);

        /// <summary>
        /// A drawing of a block DOOR, a line 900 long and a circle of radius 25, inserted 12 times 1 000 apart and turned 30
        /// degrees, each INSERT with an ATTRIB; a HATCH of ANSI31 over a square of 30, a lightweight polyline of four vertices,
        /// a POLYLINE of the plane of three, a TEXT and a LINE; opened from its saved bytes.
        /// </summary>
        private static DxfDatabase Drawing()
        {
            var db = new DxfDatabase("AC1032");
            var door = new DxfBlockTableRecord { Name = "DOOR" };
            db.BlockTable.Add(door);
            door.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(900, 0, 0)));
            door.AppendEntity(new DxfCircle(new GeoPoint3(450, 0, 0), new GeoVector3(0, 0, 1), 25));

            for (int i = 0; i < 12; i++)
            {
                var insert = new DxfBlockReference(new GeoPoint3(1000 * i, 0, 0), door) { Rotation = Math.PI / 6.0 };
                db.ModelSpace.AppendEntity(insert);
                insert.AttributeCollection.AppendAttribute(
                    new DxfAttributeReference(new GeoPoint3(1000 * i, 100, 0), "D" + i, "MARK", null)
                );
            }

            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI31");
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(0, 500), new GeoPoint2(30, 500), new GeoPoint2(30, 530), new GeoPoint2(0, 530) },
                null
            );
            db.ModelSpace.AppendEntity(hatch);

            var polyline = new DxfPolyline();

            for (int i = 0; i < 4; i++)
            {
                polyline.AddVertexAt(i, new GeoPoint2(100 * i, 800 + 10 * (i % 2)), 0, 0, 0);
            }

            db.ModelSpace.AppendEntity(polyline);
            db.ModelSpace.AppendEntity(
                new DxfPolyline2d(
                    DxfPoly2dType.SimplePoly,
                    new[] { new GeoPoint3(0, 900, 0), new GeoPoint3(50, 900, 0), new GeoPoint3(50, 950, 0) },
                    0,
                    false,
                    0,
                    0,
                    new[] { 0.0, 0.0, 0.0 }
                )
            );
            db.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    Position = new GeoPoint3(0, 1000, 0),
                    Height = 2.5,
                    TextString = "PLAN",
                }
            );
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 1100, 0), new GeoPoint3(500, 1100, 0)));
            return Opened(Saved(db));
        }

        [Fact]
        public void TheCommonGroupsReadThenSet_AreReadAsSet()
        {
            DxfDatabase db = Drawing();
            DxfLine line = db.ModelSpace.OfType<DxfLine>().Single();

            Assert.True(line.Visible);
            Assert.Equal(1.0, line.LinetypeScale);

            line.LinetypeScale = 2.5;
            Assert.Equal(2.5, line.LinetypeScale);
            Assert.True(line.Visible);

            line.Visible = false;
            Assert.False(line.Visible);
            Assert.Equal(2.5, line.LinetypeScale);

            line.Visible = true;
            Assert.True(line.Visible);

            line.ColorIndex = 3;
            Assert.Equal(3, line.ColorIndex);
            Assert.Equal(2.5, line.LinetypeScale);

            DxfLine again = Opened(Saved(db)).ModelSpace.OfType<DxfLine>().Single();
            Assert.Equal(2.5, again.LinetypeScale);
            Assert.True(again.Visible);
            Assert.Equal(3, again.ColorIndex);
        }

        [Fact]
        public void AnInsertReadThenMovedAndTurned_IsPlacedAsSet()
        {
            DxfDatabase db = Drawing();
            DxfBlockReference insert = db.ModelSpace.OfType<DxfBlockReference>().ElementAt(3);
            GeoTransform3 before = insert.BlockTransform;

            Assert.True(
                before
                    .Transform(new GeoPoint3(900, 0, 0))
                    .IsEqualTo(
                        new GeoPoint3(3000 + 900 * Math.Cos(Math.PI / 6.0), 900 * Math.Sin(Math.PI / 6.0), 0),
                        Near
                    )
            );

            insert.Position = new GeoPoint3(3000, 2000, 0);
            Assert.True(
                insert.BlockTransform.Transform(new GeoPoint3(0, 0, 0)).IsEqualTo(new GeoPoint3(3000, 2000, 0), Near)
            );

            insert.Rotation = 0.0;
            Assert.True(
                insert.BlockTransform.Transform(new GeoPoint3(900, 0, 0)).IsEqualTo(new GeoPoint3(3900, 2000, 0), Near)
            );

            insert.ScaleFactors = new GeoVector3(2, 2, 2);
            Assert.True(
                insert.BlockTransform.Transform(new GeoPoint3(900, 0, 0)).IsEqualTo(new GeoPoint3(4800, 2000, 0), Near)
            );

            DxfBlockReference again = Opened(Saved(db)).ModelSpace.OfType<DxfBlockReference>().ElementAt(3);
            Assert.True(
                again.BlockTransform.Transform(new GeoPoint3(900, 0, 0)).IsEqualTo(new GeoPoint3(4800, 2000, 0), Near)
            );
        }

        [Fact]
        public void TwoHatchesWrittenToRecordsOfOneLength_AreEachReadAsTheirOwn()
        {
            // Two HATCHes over the same square, turned to 45 and to 90 degrees, are written anew to the same number of bytes in
            // the same order, each in bytes of its own: read one after the other, each gives its own angle.
            DxfDatabase db = Drawing();
            DxfHatch first = db.ModelSpace.OfType<DxfHatch>().Single();
            var second = new DxfHatch();
            second.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI31");
            second.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(0, 500), new GeoPoint2(30, 500), new GeoPoint2(30, 530), new GeoPoint2(0, 530) },
                null
            );
            db.ModelSpace.AppendEntity(second);
            first.PatternAngle = Math.PI / 4.0;
            second.PatternAngle = Math.PI / 2.0;

            List<string> records = Records(db.ToText());
            Assert.Equal(
                records.Single(r => HandleOf(r) == first.Handle).Length,
                records.Single(r => HandleOf(r) == second.Handle).Length
            );

            for (int round = 0; round < 3; round++)
            {
                Assert.Equal(Math.PI / 4.0, first.PatternAngle, 12);
                Assert.Equal(Math.PI / 2.0, second.PatternAngle, 12);
            }
        }

        [Fact]
        public void AHatchReadThenGivenAnotherAngleAndALoop_IsReadWithThem()
        {
            DxfDatabase db = Drawing();
            DxfHatch hatch = db.ModelSpace.OfType<DxfHatch>().Single();

            Assert.Equal(1, hatch.NumberOfLoops);
            Assert.Equal(0.0, hatch.PatternAngle);
            Assert.Equal(900.0, hatch.Area, 9);

            hatch.PatternAngle = Math.PI / 4.0;
            Assert.Equal(Math.PI / 4.0, hatch.PatternAngle, 12);
            Assert.Equal(1, hatch.NumberOfLoops);

            hatch.AppendLoop(
                DxfHatchLoopTypes.Default,
                new[]
                {
                    new GeoPoint2(10, 510),
                    new GeoPoint2(20, 510),
                    new GeoPoint2(20, 520),
                    new GeoPoint2(10, 520),
                },
                null
            );
            Assert.Equal(2, hatch.NumberOfLoops);
            Assert.Equal(800.0, hatch.Area, 9);
            Assert.Equal(Math.PI / 4.0, hatch.PatternAngle, 12);
        }

        [Fact]
        public void TheVerticesOfALightweightPolylineReadThenEdited_AreReadAsEdited()
        {
            DxfDatabase db = Drawing();
            DxfPolyline polyline = db.ModelSpace.OfType<DxfPolyline>().Single();

            Assert.True(polyline.GetPoint3dAt(1).IsEqualTo(new GeoPoint3(100, 810, 0), Near));

            polyline.SetPointAt(1, new GeoPoint2(150, 850));
            Assert.True(polyline.GetPoint3dAt(1).IsEqualTo(new GeoPoint3(150, 850, 0), Near));

            polyline.AddVertexAt(4, new GeoPoint2(400, 800), 0, 0, 0);
            Assert.Equal(5, polyline.NumberOfVertices);
            Assert.True(polyline.GetPoint3dAt(4).IsEqualTo(new GeoPoint3(400, 800, 0), Near));

            polyline.SetBulgeAt(0, 0.5);
            Assert.Equal(0.5, polyline.GetBulgeAt(0));

            polyline.RemoveVertexAt(0);
            Assert.Equal(4, polyline.NumberOfVertices);
            Assert.Equal(0.0, polyline.GetBulgeAt(0));
            Assert.True(polyline.GetPoint3dAt(0).IsEqualTo(new GeoPoint3(150, 850, 0), Near));
        }

        [Fact]
        public void TheRecordsThatFollowAnEntityReadThenSet_AreReadAsSet()
        {
            // A vertex of a POLYLINE and an ATTRIB of an INSERT are records that follow their entity's.
            DxfDatabase db = Drawing();
            DxfPolyline2d polyline = db.ModelSpace.OfType<DxfPolyline2d>().Single();
            DxfVertex2d vertex = polyline.Cast<DxfVertex2d>().ElementAt(1);

            Assert.Equal(0.0, vertex.Bulge);
            vertex.Bulge = 0.25;
            Assert.Equal(0.25, vertex.Bulge);
            Assert.Equal(0.25, polyline.Cast<DxfVertex2d>().ElementAt(1).Bulge);
            Assert.Equal(0.0, polyline.Cast<DxfVertex2d>().ElementAt(0).Bulge);

            DxfBlockReference insert = db.ModelSpace.OfType<DxfBlockReference>().ElementAt(5);
            DxfAttributeReference mark = insert.AttributeCollection.Cast<DxfAttributeReference>().Single();
            Assert.Equal("D5", mark.TextString);

            mark.TextString = "D55";
            Assert.Equal("D55", mark.TextString);
            Assert.Equal("D55", insert.AttributeCollection.Cast<DxfAttributeReference>().Single().TextString);
            Assert.Equal("MARK", mark.Tag);
        }

        [Fact]
        public void ATextReadThenMovedByATransformation_IsReadWhereItWasMoved()
        {
            DxfDatabase db = Drawing();
            DxfDBText text = db.ModelSpace.OfType<DxfDBText>().Single();

            Assert.Equal(2.5, text.Height);
            Assert.True(text.Position.IsEqualTo(new GeoPoint3(0, 1000, 0), Near));

            text.TransformBy(GeoTransform3.Translation(new GeoVector3(70, 30, 0)));
            Assert.True(text.Position.IsEqualTo(new GeoPoint3(70, 1030, 0), Near));

            text.Height = 3.5;
            Assert.Equal(3.5, text.Height);
            Assert.Equal("PLAN", text.TextString);
        }

        [Fact]
        public void ADrawingReadWhole_IsSavedByteForByte()
        {
            DxfDatabase db = Drawing();
            string before = db.ToText();

            foreach (DxfEntity entity in Everything(db))
            {
                Assert.True(entity.Visible);
                Assert.Equal(1.0, entity.LinetypeScale);
                entity.GetGeometry();

                if (entity is DxfBlockReference insert)
                {
                    Assert.NotNull(insert.BlockTransform);
                    Assert.Single(insert.AttributeCollection.Cast<DxfAttributeReference>());
                }
                else if (entity is DxfHatch hatch)
                {
                    Assert.Equal(1, hatch.NumberOfLoops);
                    Assert.False(hatch.IsSolidFill);
                }
            }

            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void ADrawingReadByManyThreadsAtOnce_ReadsAsByOne()
        {
            DxfDatabase db = Drawing();
            List<DxfEntity> entities = Everything(db).ToList();
            List<string> alone = entities.Select(Read).ToList();
            var together = new string[entities.Count * 50];

            Parallel.For(0, together.Length, i => together[i] = Read(entities[(i * 7) % entities.Count]));

            for (int i = 0; i < together.Length; i++)
            {
                Assert.Equal(alone[(i * 7) % entities.Count], together[i]);
            }
        }

        /// <summary>What a reading of an entity gives, as one line: its visibility, linetype scale and placing.</summary>
        private static string Read(DxfEntity entity)
        {
            string read = entity.Handle + " " + entity.Visible + " " + Bits(entity.LinetypeScale);

            if (entity is DxfBlockReference insert)
            {
                GeoTransform3 placing = insert.BlockTransform;
                read += " " + Bits(placing.Transform(new GeoPoint3(900, 0, 0)));
            }
            else if (entity is DxfHatch hatch)
            {
                read += " " + hatch.NumberOfLoops + " " + Bits(hatch.PatternAngle);
            }

            return read;
        }
    }
}

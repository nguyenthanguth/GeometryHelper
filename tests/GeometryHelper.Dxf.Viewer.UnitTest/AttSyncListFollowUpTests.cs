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
    /// The follow-ups of ATTSYNC and LIST, each LIST line checked against AutoCAD 2021 in accoreconsole: ATTSYNC of a dynamic
    /// block bringing in line the INSERTs of its anonymous *U blocks too, against the dynamic block's definitions, their
    /// dynamic properties kept; LIST of a jogged radius, of an ordinate, of rotated dimensions at 180 and 270 degrees, of
    /// dimensions in a turned UCS and of an associative one; and the kind of every object set two places before "Layer:".
    /// </summary>
    public class AttSyncListFollowUpTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static double Radians(double degrees) => degrees * Math.PI / 180.0;

        /// <summary>Adds an entity to model space, a dimension with an empty block of its own to draw it with.</summary>
        private static string Add(CadDocument document, DxfEntity entity)
        {
            if (entity is DxfDimension dimension)
            {
                var picture = new DxfBlockTableRecord { Name = "*D" };
                document.Database.BlockTable.Add(picture);
                dimension.DimBlock = picture;
            }

            document.Database.ModelSpace.AppendEntity(entity);
            document.Regen();
            return entity.Handle;
        }

        /// <summary>What LIST writes of one object after its handle, each line as written.</summary>
        private static List<string> Listed(CadDocument document, string handle)
        {
            List<string> history = Start(document, "LIST", new[] { handle }, Enter).History.ToList();
            int at = history.FindIndex(l => l.Trim().StartsWith("Handle = ", StringComparison.Ordinal));
            Assert.True(at >= 0);
            return history.Skip(at + 1).ToList();
        }

        /// <summary>A blank drawing written to whole degrees.</summary>
        private static CadDocument Whole()
        {
            CadDocument document = Blank();
            document.Database.Auprec = 0;
            return document;
        }

        private static DxfRotatedDimension Rotated(double degrees, GeoPoint3 first, GeoPoint3 second, GeoPoint3 line) =>
            new DxfRotatedDimension(Radians(degrees), first, second, line, string.Empty, null);

        /// <summary>
        /// AutoCAD 2021 lists the jogged radius its API made with center (0, 300), chord point (50, 340), override center
        /// (-20, 250) and jog point (10, 290) so, each label of the four points set to the 32nd place.
        /// </summary>
        [Fact]
        public void ListOfAJoggedRadius_WritesItsFourDefiningPoints_AsAutoCADDoes()
        {
            CadDocument document = Whole();
            string handle = Add(
                document,
                new DxfRadialDimensionLarge(
                    new GeoPoint3(0, 300, 0),
                    new GeoPoint3(50, 340, 0),
                    new GeoPoint3(-20, 250, 0),
                    new GeoPoint3(10, 290, 0),
                    0.0,
                    string.Empty,
                    null
                )
            );

            List<string> lines = Listed(document, handle);

            Assert.Equal(
                new[]
                {
                    "associative: no",
                    "type: jogged",
                    "center defining point:          X=   0.0000  Y= 300.0000  Z=   0.0000",
                    "chord point defining point:     X=  50.0000  Y= 340.0000  Z=   0.0000",
                    "override center defining point: X= -20.0000  Y= 250.0000  Z=   0.0000",
                    "jog point defining point:       X=  10.0000  Y= 290.0000  Z=   0.0000",
                },
                lines.Take(6)
            );
            Assert.StartsWith("default text position: X=", lines[6], StringComparison.Ordinal);
            Assert.Equal("default text", lines[7]);
            Assert.StartsWith("dimension style: \"", lines[8], StringComparison.Ordinal);
            Assert.Equal("Annotative: No", lines[9]);
            Assert.Equal(10, lines.Count);
            Assert.Contains(
                "                  DIMENSION  Layer: \"0\"",
                Start(document, "LIST", new[] { handle }, Enter).History
            );
        }

        [Fact]
        public void ListOfAYOrdinate_WritesItsTypeAndItsThreePoints()
        {
            CadDocument document = Whole();
            string handle = Add(
                document,
                new DxfOrdinateDimension(
                    false,
                    new GeoPoint3(900, 100, 0),
                    new GeoPoint3(950, 100, 0),
                    string.Empty,
                    null
                )
            );

            Assert.Equal(
                new[]
                {
                    "associative: no",
                    "type: Y ordinate",
                    "feature      defining point: X= 900.0000  Y= 100.0000  Z=   0.0000",
                    "leader       defining point: X= 950.0000  Y= 100.0000  Z=   0.0000",
                    "feature UCS  defining point: X=   0.0000  Y=   0.0000  Z=   0.0000",
                },
                Listed(document, handle).Take(5)
            );
        }

        /// <summary>
        /// AutoCAD 2021 lists a rotated dimension at 180 degrees as rotated by 180, one at 270 or -90 as rotated by 270 (90 with
        /// ANGDIR 1), and one at 360 as horizontal.
        /// </summary>
        [Theory]
        [InlineData(180.0, false, "type: rotated", " rotation:    180")]
        [InlineData(270.0, false, "type: rotated", " rotation:    270")]
        [InlineData(-90.0, false, "type: rotated", " rotation:    270")]
        [InlineData(270.0, true, "type: rotated", " rotation:     90")]
        [InlineData(180.0, true, "type: rotated", " rotation:    180")]
        [InlineData(360.0, false, "type: horizontal", null)]
        public void ListOfARotatedDimensionTurnedHalfOrThreeQuarters_WritesItsRotation_AsAutoCADDoes(
            double degrees,
            bool clockwise,
            string type,
            string rotation
        )
        {
            CadDocument document = Whole();
            document.Database.Angdir = clockwise;
            string handle = Add(
                document,
                Rotated(degrees, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(50, 20, 0))
            );

            List<string> lines = Listed(document, handle);

            Assert.Equal(type, lines[1]);
            Assert.Equal(rotation ?? "1st extension  defining point: X=   0.0000  Y=   0.0000  Z=   0.0000", lines[2]);
        }

        /// <summary>
        /// In a UCS turned 30 degrees, AutoCAD 2021 counts a rotated dimension's rotation and its obliquing angle from the UCS's
        /// X axis, so that one turned 30 is horizontal and one turned 120 vertical, and writes its points in the UCS; its text
        /// angle stays as it is (45 read 45); with ANGBASE 90 and ANGDIR 1 the rotation of 90 reads 300.
        /// </summary>
        [Theory]
        [InlineData(0.0, false, "rotation:     60", "30")]
        [InlineData(90.0, true, "rotation:    300", "60")]
        public void ListInATurnedUcs_CountsTheRotationAndTheObliquingAngleFromTheUcs_AsAutoCADDoes(
            double angbase,
            bool clockwise,
            string upright,
            string oblique
        )
        {
            CadDocument document = Whole();
            document.Database.Angbase = Radians(angbase);
            document.Database.Angdir = clockwise;
            var level = Rotated(30, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(50, 40, 0));
            string levelHandle = Add(document, level);
            level.Oblique = Radians(60);
            level.TextRotation = Radians(45);
            string turned = Add(
                document,
                Rotated(90, new GeoPoint3(200, 0, 0), new GeoPoint3(200, 100, 0), new GeoPoint3(220, 50, 0))
            );
            string upward = Add(
                document,
                Rotated(120, new GeoPoint3(300, 0, 0), new GeoPoint3(300, 100, 0), new GeoPoint3(320, 50, 0))
            );
            document.Ucs.SetCurrent(CadUcs.InPlane(P(0, 0), Math.PI / 6.0));

            List<string> lines = Listed(document, levelHandle);
            Assert.Equal("type: horizontal", lines[1]);
            Assert.Equal("1st extension  defining point: X=   0.0000  Y=   0.0000  Z=   0.0000", lines[2]);
            Assert.Equal("2nd extension  defining point: X=  86.6025  Y= -50.0000  Z=   0.0000", lines[3]);
            Assert.Equal("obliquing angle:" + oblique.PadLeft(7), lines[5]);
            Assert.Equal("text angle:     45", lines[6]);

            lines = Listed(document, turned);
            Assert.Equal("type: rotated", lines[1]);
            Assert.Equal(" " + upright, lines[2]);
            Assert.Equal("1st extension  defining point: X= 173.2051  Y=-100.0000  Z=   0.0000", lines[3]);

            Assert.Equal("type: vertical", Listed(document, upward)[1]);
        }

        [Fact]
        public void ListOfADimensionSnappedToALine_SaysItIsAssociative()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            var snaps = new ObjectSnap(Tight) { Modes = CadSnapModes.Endpoint };
            Run(session, "LINE", P(0, 0), P(100, 0), Enter, "DIMLINEAR");

            foreach (GeoPoint2 cursor in new[] { P(0.4, 0.3), P(99.6, -0.3) })
            {
                CadSnapResult result = snaps.Snap(document.GetPickIndex(document.ModelSpace), cursor, 2.0);
                Assert.True(session.Feed(result, false));
            }

            Run(session, P(50, 20));
            var dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            Assert.NotNull(dimension.DimAssoc);

            List<string> lines = Listed(document, dimension.Handle);

            Assert.Equal("associative: yes", lines[0]);
            Assert.Equal("type: horizontal", lines[1]);
        }

        /// <summary>
        /// AutoCAD 2021 sets each kind, as LIST names it, in eight places and two more before "Layer:": "LINE      Layer:",
        /// "MULTILEADER  Layer:"; a lightweight polyline is an LWPOLYLINE, a 2D one a POLYLINE, a MINSERT a BLOCK REFERENCE and
        /// a table an ACAD_TABLE.
        /// </summary>
        [Fact]
        public void ListOfEachKind_SetsItsNameTwoPlacesBeforeTheLayer_AsAutoCADNamesIt()
        {
            CadDocument document = BlankWithMLeaderStyle();
            var block = new DxfBlockTableRecord { Name = "GHLB", Origin = new GeoPoint3(0, 0, 0) };
            document.Database.BlockTable.Add(block);
            block.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 5.0));
            var light = new DxfPolyline(3);
            light.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            light.AddVertexAt(1, new GeoPoint2(100, 0), 0, 0, 0);
            light.AddVertexAt(2, new GeoPoint2(100, 50), 0, 0, 0);
            var kinds = new List<KeyValuePair<string, DxfEntity>>
            {
                new KeyValuePair<string, DxfEntity>(
                    "LINE      ",
                    new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0))
                ),
                new KeyValuePair<string, DxfEntity>("MULTILEADER  ", new DxfMLeader()),
                new KeyValuePair<string, DxfEntity>("LWPOLYLINE  ", light),
                new KeyValuePair<string, DxfEntity>("POLYLINE  ", new DxfPolyline2d()),
                new KeyValuePair<string, DxfEntity>("ACAD_TABLE  ", new DxfTable(new GeoPoint3(0, 0, 0), 2, 2, 10, 30)),
                new KeyValuePair<string, DxfEntity>("3D FACE   ", new DxfFace()),
                new KeyValuePair<string, DxfEntity>("TOLERANCE  ", new DxfTolerance()),
                new KeyValuePair<string, DxfEntity>(
                    "ATTRIBUTE DEFINITION  ",
                    new DxfAttributeDefinition(new GeoPoint3(0, 0, 0), "V", "TAGM", "Prompt", null)
                ),
                new KeyValuePair<string, DxfEntity>(
                    "BLOCK REFERENCE  ",
                    new DxfBlockReference(new GeoPoint3(0, 900, 0), block)
                ),
            };
            var minsert = new DxfMInsertBlock(new GeoPoint3(100, 900, 0), block, 2, 2, 20, 20);
            kinds.Add(new KeyValuePair<string, DxfEntity>("BLOCK REFERENCE  ", minsert));

            foreach (KeyValuePair<string, DxfEntity> kind in kinds)
            {
                string handle = Add(document, kind.Value);

                Assert.Contains(
                    new string(' ', 18) + kind.Key + "Layer: \"0\"",
                    Start(document, "LIST", new[] { handle }, Enter).History
                );
            }
        }

        /// <summary>
        /// GH_PANEL, a dynamic rectangle 100 by 50 whose Width stretches its right edge and the definition TAG locked at (95, 20),
        /// with NOTE at (50, 30) and FIX locked at (10, 10), as AutoCAD 2021 opened it from the library's file; its INSERT at
        /// (1 000, 500) given a Width of 150, which places an anonymous *U block with TAG at (145, 20), and its INSERT at (2 000,
        /// 500) left as it is. The block is then defined anew, as AutoCAD 2021's API left it: TAG 5 high, FIX moved to (10, 40)
        /// and NEW added at (60, 25), the *U block unchanged.
        /// </summary>
        private static CadDocument Panel(out string stretched, out string plain)
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
            };

            foreach (DxfLine line in lines)
            {
                block.AppendEntity(line);
            }

            var tag = new DxfAttributeDefinition(new GeoPoint3(95, 20, 0), "T1", "TAG", "Tag", null);
            block.AppendEntity(tag);
            tag.LockPositionInBlock = true;
            block.AppendEntity(new DxfAttributeDefinition(new GeoPoint3(50, 30, 0), "N1", "NOTE", "Note", null));
            var fix = new DxfAttributeDefinition(new GeoPoint3(10, 10, 0), "F1", "FIX", "Fix", null);
            block.AppendEntity(fix);
            fix.LockPositionInBlock = true;
            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                "Overall width",
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                DxfBlockParameterValueSet.List(new[] { 100.0, 150, 200 })
            );
            block.AddStretchAction(
                width,
                new GeoPoint2(90, -10),
                new GeoPoint2(110, 60),
                lines.Take(3).Cast<DxfEntity>().Concat(new DxfEntity[] { tag })
            );
            var handles = new List<string>();

            foreach (GeoPoint3 at in new[] { new GeoPoint3(1000, 500, 0), new GeoPoint3(2000, 500, 0) })
            {
                var insert = new DxfBlockReference(at, block);
                db.ModelSpace.AppendEntity(insert);
                handles.Add(insert.Handle);

                foreach (DxfAttributeDefinition definition in block.OfType<DxfAttributeDefinition>())
                {
                    var attribute = new DxfAttributeReference();
                    attribute.SetAttributeFromBlock(definition, insert.BlockTransform);
                    attribute.TextString = definition.Tag.ToLowerInvariant() + (at.X > 1500 ? "2" : "1");
                    insert.AttributeCollection.AppendAttribute(attribute);
                }
            }

            stretched = handles[0];
            plain = handles[1];
            CadDocument made = CadDocument.From(db, "panel.dxf");
            Assert.True(CadDynamicBlocks.SetProperty(made, new[] { stretched }, "Width", "150").Succeeded);
            Assert.StartsWith("*U", Entity<DxfBlockReference>(made, stretched).Name, StringComparison.Ordinal);
            Near(
                new GeoPoint3(1145, 520, 0),
                Entity<DxfBlockReference>(made, stretched).AttributeCollection[0].Position,
                1E-9
            );

            DxfBlockTableRecord dynamic = made.Database.BlockTable["GH_PANEL"];
            List<DxfAttributeDefinition> definitions = dynamic.OfType<DxfAttributeDefinition>().ToList();
            definitions[0].Height = 5.0;
            definitions[2].Position = new GeoPoint3(10, 40, 0);
            dynamic.AppendEntity(new DxfAttributeDefinition(new GeoPoint3(60, 25, 0), "W1", "NEW", "New", null));
            return CadDocument.From(DxfDatabase.Parse(made.Database.ToText()), "panel.dxf");
        }

        /// <summary>An attribute of an INSERT, its tag, value, place and height.</summary>
        private static void AssertAttribute(
            DxfAttributeReference attribute,
            string tag,
            string value,
            GeoPoint3 position,
            double height
        )
        {
            Assert.Equal(tag, attribute.Tag);
            Assert.Equal(value, attribute.TextString);
            Near(position, attribute.Position, 1E-9);
            Assert.Equal(height, attribute.Height, 12);
        }

        [Fact]
        public void AttSyncByName_OfADynamicBlock_BringsTheInsertsOfItsAnonymousBlocksInLine_KeepingTheirDynamicProperties()
        {
            CadDocument document = Panel(out string stretched, out string plain);
            string start = document.Database.ToText();
            string anonymous = Entity<DxfBlockReference>(document, stretched).Name;

            Start(document, "ATTSYNC", "N", "GH_PANEL");

            var insert = Entity<DxfBlockReference>(document, stretched);
            Assert.Equal(anonymous, insert.Name);
            Assert.Equal(
                150.0,
                (double)insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == "Width").Value,
                9
            );
            DxfAttributeReference[] synced = insert.AttributeCollection.ToArray();
            Assert.Equal(4, synced.Length);
            AssertAttribute(synced[0], "TAG", "tag1", new GeoPoint3(1145, 520, 0), 5.0);
            AssertAttribute(synced[1], "NOTE", "note1", new GeoPoint3(1050, 530, 0), 2.5);
            AssertAttribute(synced[2], "FIX", "fix1", new GeoPoint3(1010, 540, 0), 2.5);
            AssertAttribute(synced[3], "NEW", "W1", new GeoPoint3(1060, 525, 0), 2.5);

            DxfAttributeReference[] other = Entity<DxfBlockReference>(document, plain).AttributeCollection.ToArray();
            Assert.Equal(4, other.Length);
            AssertAttribute(other[0], "TAG", "tag2", new GeoPoint3(2095, 520, 0), 5.0);
            AssertAttribute(other[2], "FIX", "fix2", new GeoPoint3(2010, 540, 0), 2.5);
            AssertAttribute(other[3], "NEW", "W1", new GeoPoint3(2060, 525, 0), 2.5);
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void AttSyncBySelect_OfAnInsertOfAnAnonymousBlock_SyncsItAgainstTheDynamicBlock()
        {
            CadDocument document = Panel(out string stretched, out _);

            Start(document, "ATTSYNC", Enter, new[] { stretched }, Enter);

            DxfAttributeReference[] synced = Entity<DxfBlockReference>(document, stretched)
                .AttributeCollection.ToArray();
            Assert.Equal(new[] { "TAG", "NOTE", "FIX", "NEW" }, synced.Select(a => a.Tag).ToArray());
            AssertAttribute(synced[0], "TAG", "tag1", new GeoPoint3(1145, 520, 0), 5.0);
        }
    }
}

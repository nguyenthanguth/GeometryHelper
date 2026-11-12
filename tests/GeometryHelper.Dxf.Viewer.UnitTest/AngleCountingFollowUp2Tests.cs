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
    /// The second round of the angle follow-ups, each checked against AutoCAD 2021 in accoreconsole: LIST of an ellipse and of
    /// the dimensions with their angles counted by ANGBASE and ANGDIR, line for line as AutoCAD writes them; ATTSYNC keeping
    /// the extended data and extension dictionary of an attribute it makes anew, as SetAttributeFromBlock keeps those of one
    /// synced in place; and ATTSYNC by Select naming the dynamic block of an INSERT, not its anonymous block.
    /// </summary>
    public class AngleCountingFollowUp2Tests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static double Radians(double degrees) => degrees * Math.PI / 180.0;

        /// <summary>A blank drawing counting angles from a base, one way, written to whole degrees.</summary>
        private static CadDocument Counting(double angbase, bool clockwise)
        {
            CadDocument document = Blank();
            document.Database.Angbase = Radians(angbase);
            document.Database.Angdir = clockwise;
            document.Database.Auprec = 0;
            return document;
        }

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

        /// <summary>The elliptical arc AutoCAD's probe made: centre (600, -300), major axis 100 up, ratio 0.5, from 30 to 120.</summary>
        private static DxfEllipse Arc() =>
            new DxfEllipse(
                new GeoPoint3(600, -300, 0),
                new GeoVector3(0, 0, 1),
                new GeoVector3(0, 100, 0),
                0.5,
                Radians(30),
                Radians(120)
            );

        [Theory]
        [InlineData(0.0, false, "30", "120")]
        [InlineData(90.0, false, "300", "30")]
        [InlineData(0.0, true, "330", "240")]
        [InlineData(90.0, true, "60", "330")]
        public void ListOfAnEllipticalArc_WritesAutoCADsLines_ItsAnglesCountedByAngbaseAndAngdir(
            double angbase,
            bool clockwise,
            string start,
            string end
        )
        {
            CadDocument document = Counting(angbase, clockwise);
            string handle = Add(document, Arc());

            List<string> lines = Listed(document, handle);

            Assert.Equal(
                new[]
                {
                    "                           Length: 94.8641",
                    "                           Center: X = 600.0000 , Y = -300.0000, Z = 0.0000",
                    "                       Major Axis: X = 0.0000   , Y = 100.0000 , Z = 0.0000",
                    "                       Minor Axis: X = -50.0000 , Y = 0.0000   , Z = 0.0000",
                    "                      Start Point: X = 562.2036 , Y = -234.5346, Z = 0.0000",
                    "                        End Point: X = 551.9616 , Y = -327.7350, Z = 0.0000",
                    "                      Start Angle: " + start,
                    "                        End Angle: " + end,
                    "                     Radius Ratio: 0.5000",
                },
                lines
            );
        }

        [Fact]
        public void ListOfAWholeEllipse_WritesItsAreaAndCircumference_AndNoAngles()
        {
            CadDocument document = Counting(90.0, true);
            string handle = Add(
                document,
                new DxfEllipse(
                    new GeoPoint3(600, 0, 0),
                    new GeoVector3(0, 0, 1),
                    new GeoVector3(0, 100, 0),
                    0.5,
                    0.0,
                    2.0 * Math.PI
                )
            );

            Assert.Equal(
                new[]
                {
                    "                             Area: 15707.9633",
                    "                    Circumference: 484.4224",
                    "                           Center: X = 600.0000 , Y = 0.0000   , Z = 0.0000",
                    "                       Major Axis: X = 0.0000   , Y = 100.0000 , Z = 0.0000",
                    "                       Minor Axis: X = -50.0000 , Y = 0.0000   , Z = 0.0000",
                    "                     Radius Ratio: 0.5000",
                },
                Listed(document, handle)
            );
        }

        /// <summary>
        /// AutoCAD 2021 writes the rotation of a rotated dimension as an amount turned, by ANGDIR alone (30 reads 330 clockwise
        /// whatever ANGBASE is), and its obliquing and text angles as directions counted from ANGBASE (30 reads 60 from the north,
        /// clockwise).
        /// </summary>
        [Theory]
        [InlineData(0.0, false, "30", "30", "45")]
        [InlineData(90.0, false, "30", "300", "315")]
        [InlineData(0.0, true, "330", "330", "315")]
        [InlineData(90.0, true, "330", "60", "45")]
        public void ListOfARotatedDimension_WritesItsRotationTurned_AndItsObliquingAndTextAnglesCounted(
            double angbase,
            bool clockwise,
            string rotation,
            string oblique,
            string text
        )
        {
            CadDocument document = Counting(angbase, clockwise);
            var dimension = new DxfRotatedDimension(
                Radians(30),
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                new GeoPoint3(50, 40, 0),
                string.Empty,
                null
            );
            string handle = Add(document, dimension);
            dimension.Oblique = Radians(30);
            dimension.TextRotation = Radians(45);

            List<string> lines = Listed(document, handle);

            Assert.Equal("associative: no", lines[0]);
            Assert.Equal("type: rotated", lines[1]);
            Assert.Equal(" rotation:" + rotation.PadLeft(7), lines[2]);
            Assert.Equal("1st extension  defining point: X=   0.0000  Y=   0.0000  Z=   0.0000", lines[3]);
            Assert.Equal("2nd extension  defining point: X= 100.0000  Y=   0.0000  Z=   0.0000", lines[4]);
            Assert.StartsWith("dimension line defining point: X=", lines[5], StringComparison.Ordinal);
            Assert.Equal("obliquing angle:" + oblique.PadLeft(7), lines[6]);
            Assert.Equal("text angle:" + text.PadLeft(7), lines[7]);
            Assert.StartsWith("default text position: X=", lines[8], StringComparison.Ordinal);
            Assert.Equal("default text", lines[9]);
            Assert.StartsWith("dimension style: \"", lines[10], StringComparison.Ordinal);
            Assert.Equal("Annotative: No", lines[11]);
            Assert.Equal(12, lines.Count);

            Assert.Contains(
                "                  DIMENSION  Layer: \"0\"",
                Start(document, "LIST", new[] { handle }, Enter).History
            );
        }

        [Fact]
        public void ListOfARotatedDimensionLevelOrUpright_SaysHorizontalOrVertical_AndWritesNoRotation()
        {
            CadDocument document = Counting(90.0, true);
            string level = Add(
                document,
                new DxfRotatedDimension(
                    0.0,
                    new GeoPoint3(0, 0, 0),
                    new GeoPoint3(100, 0, 0),
                    new GeoPoint3(50, 40, 0),
                    string.Empty,
                    null
                )
            );
            string upright = Add(
                document,
                new DxfRotatedDimension(
                    Radians(90),
                    new GeoPoint3(200, 0, 0),
                    new GeoPoint3(200, 100, 0),
                    new GeoPoint3(250, 50, 0),
                    string.Empty,
                    null
                )
            );

            List<string> first = Listed(document, level);
            Assert.Equal("type: horizontal", first[1]);
            Assert.StartsWith("1st extension", first[2], StringComparison.Ordinal);

            List<string> second = Listed(document, upright);
            Assert.Equal("type: vertical", second[1]);
            Assert.StartsWith("1st extension", second[2], StringComparison.Ordinal);
        }

        [Fact]
        public void ListOfTheOtherDimensions_WritesTheirTypesAndDefiningPoints_AsAutoCADDoes()
        {
            CadDocument document = Counting(0.0, false);
            var aligned = new DxfAlignedDimension(
                new GeoPoint3(0, 100, 0),
                new GeoPoint3(100, 160, 0),
                new GeoPoint3(40, 180, 0),
                "<> mm",
                null
            );
            string alignedHandle = Add(document, aligned);
            aligned.TextRotation = Radians(45);
            string angular = Add(
                document,
                new DxfLineAngularDimension2(
                    new GeoPoint3(300, 0, 0),
                    new GeoPoint3(400, 0, 0),
                    new GeoPoint3(300, 0, 0),
                    new GeoPoint3(360, 80, 0),
                    new GeoPoint3(360, 30, 0),
                    string.Empty,
                    null
                )
            );
            string three = Add(
                document,
                new DxfPoint3AngularDimension(
                    new GeoPoint3(300, 200, 0),
                    new GeoPoint3(400, 200, 0),
                    new GeoPoint3(360, 280, 0),
                    new GeoPoint3(360, 230, 0),
                    string.Empty,
                    null
                )
            );
            string radial = Add(
                document,
                new DxfRadialDimension(new GeoPoint3(600, 200, 0), new GeoPoint3(650, 250, 0), 20, string.Empty, null)
            );
            string diameter = Add(
                document,
                new DxfDiametricDimension(
                    new GeoPoint3(650, 400, 0),
                    new GeoPoint3(550, 400, 0),
                    20,
                    string.Empty,
                    null
                )
            );
            string ordinate = Add(
                document,
                new DxfOrdinateDimension(
                    true,
                    new GeoPoint3(900, 100, 0),
                    new GeoPoint3(900, 150, 0),
                    string.Empty,
                    null
                )
            );

            List<string> lines = Listed(document, alignedHandle);
            Assert.Equal("type: aligned", lines[1]);
            Assert.Equal("1st extension  defining point: X=   0.0000  Y= 100.0000  Z=   0.0000", lines[2]);
            Assert.Equal("2nd extension  defining point: X= 100.0000  Y= 160.0000  Z=   0.0000", lines[3]);
            Assert.Equal("text angle:     45", lines[5]);
            Assert.Equal("dimension text modifier: <> mm", lines[7]);

            lines = Listed(document, angular);
            Assert.Equal(
                new[]
                {
                    "type: angular",
                    "first line     defining point: X= 300.0000  Y=   0.0000  Z=   0.0000",
                    "first line     defining point: X= 400.0000  Y=   0.0000  Z=   0.0000",
                    "second line    defining point: X= 300.0000  Y=   0.0000  Z=   0.0000",
                    "second line    defining point: X= 360.0000  Y=  80.0000  Z=   0.0000",
                },
                lines.Skip(1).Take(5)
            );
            Assert.StartsWith("dimension arc defining point: X=", lines[6], StringComparison.Ordinal);

            lines = Listed(document, three);
            Assert.Equal(
                new[]
                {
                    "type: arc",
                    "1st extension defining point: X= 400.0000  Y= 200.0000  Z=   0.0000",
                    "2nd extension defining point: X= 360.0000  Y= 280.0000  Z=   0.0000",
                    "arc center    defining point: X= 300.0000  Y= 200.0000  Z=   0.0000",
                },
                lines.Skip(1).Take(4)
            );

            lines = Listed(document, radial);
            Assert.Equal(
                new[]
                {
                    "type: radial",
                    "center defining point: X= 600.0000  Y= 200.0000  Z=   0.0000",
                    "defining point:        X= 650.0000  Y= 250.0000  Z=   0.0000",
                },
                lines.Skip(1).Take(3)
            );

            lines = Listed(document, diameter);
            Assert.Equal(
                new[]
                {
                    "type: diameter",
                    "       defining point: X= 550.0000  Y= 400.0000  Z=   0.0000",
                    "defining point:        X= 650.0000  Y= 400.0000  Z=   0.0000",
                },
                lines.Skip(1).Take(3)
            );

            lines = Listed(document, ordinate);
            Assert.Equal(
                new[]
                {
                    "type: X ordinate",
                    "feature      defining point: X= 900.0000  Y= 100.0000  Z=   0.0000",
                    "leader       defining point: X= 900.0000  Y= 150.0000  Z=   0.0000",
                    "feature UCS  defining point: X=   0.0000  Y=   0.0000  Z=   0.0000",
                },
                lines.Skip(1).Take(4)
            );
        }

        /// <summary>
        /// GB with a circle and the definition GRID, inserted at the origin with GRID "C" given extended data of GHAPP and an
        /// extension dictionary holding the XRECORD GHX; then GB defined again with LEVEL before GRID, so that GRID stands out of
        /// order on the INSERT.
        /// </summary>
        [Fact]
        public void AttSync_AnAttributeMadeAnewForItsTagOutOfOrder_KeepsItsExtendedDataAndExtensionDictionary()
        {
            CadDocument document = CadDocument.From(CadTemplate.CreateMetric(), "blocks.dxf");
            DxfDatabase db = document.Database;
            var block = new DxfBlockTableRecord { Name = "GB", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 10.0));
            var gridDefinition = new DxfAttributeDefinition(new GeoPoint3(0, 0, 0), "A", "GRID", "Grid letter", null);
            block.AppendEntity(gridDefinition);
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            db.ModelSpace.AppendEntity(insert);
            var grid = new DxfAttributeReference();
            grid.SetAttributeFromBlock(gridDefinition, insert.BlockTransform);
            grid.TextString = "C";
            insert.AttributeCollection.AppendAttribute(grid);
            grid.XData = new[] { new DxfTypedValue(1001, "GHAPP"), new DxfTypedValue(1000, "kept") };
            grid.CreateExtensionDictionary();
            string dictionary = grid.ExtensionDictionary.Handle;
            grid.ExtensionDictionary.SetAt("GHX", new DxfXrecord { Data = new[] { new DxfTypedValue(1, "entry") } });

            gridDefinition.Erase();
            block.AppendEntity(new DxfAttributeDefinition(new GeoPoint3(0, -20, 0), "L0", "LEVEL", "Level", null));
            block.AppendEntity(new DxfAttributeDefinition(new GeoPoint3(0, 15, 0), "A", "GRID", "Grid letter", null));
            document = CadDocument.From(DxfDatabase.Parse(db.ToText()), "blocks.dxf");
            string start = document.Database.ToText();

            Start(document, "ATTSYNC", "N", "GB");

            DxfBlockReference synced = Assert.Single(All<DxfBlockReference>(document.ModelSpace));
            Assert.Equal(new[] { "LEVEL", "GRID" }, synced.AttributeCollection.Select(a => a.Tag).ToArray());
            DxfAttributeReference made = synced.AttributeCollection[1];
            Assert.Equal("C", made.TextString);
            Near(new GeoPoint3(0, 15, 0), made.Position);
            Assert.Equal("kept", made.GetXDataForApplication("GHAPP")[1].Value);
            Assert.NotNull(made.ExtensionDictionary);
            Assert.Equal(dictionary, made.ExtensionDictionary.Handle);
            Assert.Equal(made.Handle, made.ExtensionDictionary.OwnerHandle);
            Assert.Equal(new[] { made.Handle }, made.ExtensionDictionary.GetPersistentReactorIds());
            Assert.True(made.ExtensionDictionary.Contains("GHX"));
            Assert.Null(synced.AttributeCollection[0].ExtensionDictionary);
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        /// <summary>
        /// GH_PANEL, a dynamic rectangle whose Width stretches its right edge, with the definition TAG, inserted at (1 000, 500)
        /// and given a Width of 150, so that the INSERT places an anonymous *U block standing for it.
        /// </summary>
        [Fact]
        public void AttSyncBySelect_OfAnInsertOfADynamicBlock_NamesTheDynamicBlock_NotItsAnonymousBlock()
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

            block.AppendEntity(new DxfAttributeDefinition(new GeoPoint3(10, 20, 0), "P1", "TAG", "Tag", null));
            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                "Overall width",
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                DxfBlockParameterValueSet.List(new[] { 100.0, 150, 200 })
            );
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            var insert = new DxfBlockReference(new GeoPoint3(1000, 500, 0), block);
            db.ModelSpace.AppendEntity(insert);
            string handle = insert.Handle;
            CadDocument document = CadDocument.From(db, "panel.dxf");
            Assert.True(CadDynamicBlocks.SetProperty(document, new[] { handle }, "Width", "150").Succeeded);
            Assert.StartsWith("*U", Entity<DxfBlockReference>(document, handle).Name, StringComparison.Ordinal);

            CadCommandSession session = Start(document, "ATTSYNC", Enter, new[] { handle });

            Assert.Equal("ATTSYNC block GH_PANEL? [Yes/No] <Yes>:", session.PromptText);
            Run(session, Esc);
            Assert.False(session.IsRunning);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.Drawing.UnitTest.Projection.ProjectionKit;

namespace GeometryHelper.Drawing.UnitTest.Projection
{
    public class ProjectionDxfTests
    {
        private readonly ITestOutputHelper _output;

        public ProjectionDxfTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void AViewIsWrittenAsLinesArcsAndCirclesOnTheVisibleAndHiddenLayers()
        {
            GeoSolid3 plate = PlateWithHoles(300, 200, 20, 22, 24, P(60, 100), P(240, 100));
            var database = new DxfDatabase("AC1027");

            Project(Top, plate).WriteTo(database);
            Project(Front, plate).WriteTo(database);

            DxfDatabase read = DxfDatabase.Parse(database.ToText());
            Assert.Equal("HIDDEN", read.LayerTable["Hidden"].LinetypeName);
            Assert.Equal("CENTER", read.LayerTable["Centre"].LinetypeName);
            Assert.Equal(
                new[] { 6.35, -3.175 },
                Enumerable
                    .Range(0, read.LinetypeTable["HIDDEN"].NumDashes)
                    .Select(read.LinetypeTable["HIDDEN"].DashLengthAt)
            );
            Assert.Equal(2, read.ModelSpace.OfType<DxfCircle>().Count(c => c.Layer == "Visible"));
            Assert.Equal(8, read.ModelSpace.OfType<DxfLine>().Count(l => l.Layer == "Visible"));
            Assert.Equal(4, read.ModelSpace.OfType<DxfLine>().Count(l => l.Layer == "Hidden"));
        }

        [Fact]
        public void EachSectionFaceIsWrittenAsAnAnsi31HatchOnTheHatchLayerWithItsHolesAsInnerLoops()
        {
            // The plate of TC1 cut half way up its thickness, seen from above: one face 300 by 200 with its four holes of 22;
            // and a tube 168.3 by 6 cut across, seen along it: one ring.
            GeoSolid3 plate = TestBodies.TC1().Parts[0].Object.Solid;
            ProjectedView2 top = ViewProjector.Project(
                new[] { new ProjectionInput("plate", plate) },
                Top,
                new ProjectionOptions(sectionPlane: new GeoPlane3(new GeoPoint3(0, 0, 10), GeoVector3.ZAxis)),
                Tol
            );
            ProjectedView2 end = ViewProjector.Project(
                new[] { new ProjectionInput("tube", Tube(168.3, 6, 1000, 32)) },
                Right,
                new ProjectionOptions(sectionPlane: new GeoPlane3(new GeoPoint3(500, 0, 0), GeoVector3.XAxis)),
                Tol
            );
            var database = new DxfDatabase("AC1027") { PatternSearchPath = PatternFolder() };

            top.WriteTo(database, 2.0);
            end.WriteTo(database, 2.0);

            DxfDatabase read = DxfDatabase.Parse(database.ToText());
            ProjectedFace2[] faces = top.SectionFaces.Concat(end.SectionFaces).ToArray();
            DxfHatch[] hatches = read.ModelSpace.OfType<DxfHatch>().ToArray();
            Assert.Equal(2, faces.Length);
            Assert.Equal(faces.Length, hatches.Length);
            Assert.Equal(8, read.LayerTable["Hatch"].ColorIndex);
            Assert.Equal(new[] { 5, 2 }, hatches.Select(h => h.NumberOfLoops));
            Assert.Equal(300 * 200 - 4 * TestBodies.PolygonArea(11, 24), faces[0].Face.Area, 6);

            for (int i = 0; i < faces.Length; i++)
            {
                GeoFace2 face = faces[i].Face;
                double perimeter = face.Boundary.Length + face.Holes.Sum(h => h.Length);
                Assert.Equal("Hatch", hatches[i].Layer);
                Assert.Equal("ANSI31", hatches[i].PatternName);
                Assert.Equal(2.0, hatches[i].PatternScale);
                Assert.Equal(1, hatches[i].NumberOfPatternDefinitions);
                Assert.True(
                    Math.Abs(hatches[i].Area - face.Area) <= Tol.EqualPoint * perimeter,
                    hatches[i].Area + " / " + face.Area
                );
            }

            // The lines are still on their layers, and the hatching is under them.
            Assert.Contains(read.ModelSpace.OfType<DxfCircle>(), c => c.Layer == "Visible");
            Assert.IsType<DxfHatch>(read.ModelSpace.First());
            Assert.Throws<ArgumentOutOfRangeException>(() => top.WriteTo(database, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => top.WriteTo(database, double.PositiveInfinity));
            _output.WriteLine(SavePicture(top, "plate-section-hatch", 2.0));
            _output.WriteLine(SavePicture(end, "tube-section-hatch", 2.0));
        }

        [Fact]
        public void TheThreeViewsOfTheTestBodiesAreDrawnToPicturesToLookAt()
        {
            var bodies = new (string Name, ProjectionInput[] Inputs)[]
            {
                ("box", new[] { new ProjectionInput("box", Box(0, 0, 0, 300, 200, 100)) }),
                (
                    "plate",
                    new[]
                    {
                        new ProjectionInput("plate", PlateWithHoles(300, 200, 20, 22, 24, P(60, 100), P(240, 100))),
                    }
                ),
                ("angle", new[] { new ProjectionInput("angle", Angle(100, 10, 1000)) }),
                ("tube", new[] { new ProjectionInput("tube", Tube(168.3, 6, 1000, 32)) }),
                ("beam", BeamWithEndPlates()),
                ("tc5", TestBodies.TC5().Parts.Select(p => new ProjectionInput(p.Object.Id, p.Object.Solid)).ToArray()),
                ("tc4", TestBodies.TC4().Parts.Select(p => new ProjectionInput(p.Object.Id, p.Object.Solid)).ToArray()),
                ("tc6", TestBodies.TC6().Parts.Select(p => new ProjectionInput(p.Object.Id, p.Object.Solid)).ToArray()),
            };

            foreach ((string name, ProjectionInput[] inputs) in bodies)
            {
                foreach (
                    (string view, GeoCoordinateSystem3 frame) in new[]
                    {
                        ("top", Top),
                        ("front", Front),
                        ("right", Right),
                        ("iso", Iso),
                    }
                )
                {
                    ProjectedView2 projected = ViewProjector.Project(inputs, frame, Tol);
                    string path = SavePicture(projected, name + "-" + view);
                    _output.WriteLine(path + "  " + projected);
                    Assert.True(new FileInfo(path).Length > 0);
                }
            }

            var section = new ProjectionOptions(
                sectionPlane: new GeoPlane3(new GeoPoint3(500, 0, 0), GeoVector3.XAxis)
            );
            _output.WriteLine(
                SavePicture(ViewProjector.Project(BeamWithEndPlates(), Right, section, Tol), "beam-section")
            );
        }
    }
}

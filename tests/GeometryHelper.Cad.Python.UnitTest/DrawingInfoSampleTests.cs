using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// The sample that ships in the Pythons folder, <c>GH-DrawingInfo.py</c>, as the build leaves it beside
    /// GeometryHelper.Cad.dll: loading it defines GHPYINFO and writes nothing, and GHPYINFO writes the drawing's name and
    /// folder, the current layer, the objects of model space by type, the layers and blocks, and the extents. It changes nothing.
    /// </summary>
    public sealed class DrawingInfoSampleTests : PythonHostTestBase
    {
        public DrawingInfoSampleTests(PythonHostFixture fixture)
            : base(fixture) { }

        /// <summary>A drawing with two circles and a line in model space, the layer Steel and a block of one line.</summary>
        private static CadDocument TwoCirclesAndALine()
        {
            CadDocument document = Blank("Plan.dxf");
            DxfDatabase database = document.Database;
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "Steel", ColorIndex = 1 });
            var block = new DxfBlockTableRecord { Name = "BOLT" };
            database.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 10));
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(50, 0, 0), GeoVector3.ZAxis, 10));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(120, 80, 0)));
            database.Extmin = new GeoPoint3(-10, -10, 0);
            database.Extmax = new GeoPoint3(120, 80, 0);
            return document;
        }

        /// <summary>Loads the shipped Pythons folder as the app does at its start.</summary>
        private void LoadShippedFolder(CadCommandSession session)
        {
            Owner.Session = session;
            IReadOnlyList<string> files = CadPythonStartup.FilesToLoad(
                new[] { Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pythons") },
                Array.Empty<string>()
            );
            Assert.Equal("GH-DrawingInfo.py", Path.GetFileName(Assert.Single(files)));
            Assert.True(new CadPythonStartup(Host).Run(session, files));
            Owner.Drain();
        }

        [Fact]
        public void LoadingTheSample_DefinesGhpyinfo_AndTheFileItselfWritesNothing()
        {
            CadCommandSession session = NewSession();

            LoadShippedFolder(session);

            Assert.NotNull(session.Registry.Find("GHPYINFO"));
            Assert.Empty(Errors);

            // The one line is the host's, written for every file of the start; the file itself prints nothing.
            Assert.Equal(new[] { "GH-DrawingInfo.py loaded: GHPYINFO." }, session.History);
            Assert.Equal(new[] { "GH-DrawingInfo.py loaded: GHPYINFO." }, Printed);
        }

        [Fact]
        public void Ghpyinfo_OnTwoCirclesAndALine_WritesTheNameTheLayerTheObjectsByTypeTheLayersTheBlocksAndTheExtents()
        {
            CadCommandSession session = new CadCommandSession(TwoCirclesAndALine());
            Host.AddCommandsTo(session.Registry);
            LoadShippedFolder(session);
            Owner.Session = session;

            int loaded = Printed.Count;
            Assert.True(session.Feed("GHPYINFO"));
            Owner.Drain();

            Assert.Empty(Errors);
            Assert.Equal(
                new[]
                {
                    "Drawing: Plan.dxf in not saved yet",
                    "Current layer: 0",
                    "Model space: 3 objects",
                    "  CIRCLE   2",
                    "  LINE     1",
                    "Layers: 2, blocks: 1",
                    "Extents: (-10.00,-10.00) to (120.00,80.00)",
                },
                Printed.Skip(loaded)
            );
            Assert.False(session.Document.CanUndo);
        }

        [Fact]
        public void WithNoPythonInstalled_StartingOnTheShippedFolder_WritesTheOneLineAndNothingElse()
        {
            // The sample is a .py in the folder, so the start tries the engine; on a machine with no Python it says so once.
            string missing = Path.Combine(Folder, "nowhere", "python.exe");
            CadPythonEnvironment environment = CadPythonEnvironment.Find(missing);
            var owner = new TestPythonOwner();
            CadCommandSession session = NewSession();
            owner.Session = session;
            var told = new List<string>();

            using (var host = new CadPythonHost(environment, owner))
            {
                host.Output += (sender, e) => told.Add(e.Text);
                IReadOnlyList<string> files = CadPythonStartup.FilesToLoad(
                    new[] { Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pythons") },
                    Array.Empty<string>()
                );

                bool started = new CadPythonStartup(host).Run(session, files);
                owner.Drain();

                Assert.False(started);
                Assert.False(host.IsStarted);
                string[] line = { "Python files were not loaded: " + environment.Problem };
                Assert.Equal(line, session.History);
                Assert.Equal(line, told);
            }
        }
    }
}

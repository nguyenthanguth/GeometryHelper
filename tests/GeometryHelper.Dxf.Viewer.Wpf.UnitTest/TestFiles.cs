using System;
using System.IO;
using System.Runtime.CompilerServices;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

// One test at a time: WPF keeps the XAML of a control as one part for the whole process and hands out its stream through
// a list it does not lock, so two tests building a workspace on two threads at once break that list, and every workspace
// built afterwards fails in PackagePart.CleanUpRequestedStreamsList.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace System.Runtime.CompilerServices
{
    /// <summary>Marks the method the compiler runs when the assembly is loaded; .NET Framework has no such attribute of its own.</summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute { }
}

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The settings file of the view models the tests make: one of this run's own under %TEMP%\gk\drawing\wpf-tests, set
    /// before any test runs and deleted at the end, so that no test reads or writes the user's settings and two runs at once
    /// share no file.
    /// </summary>
    internal static class TestSettings
    {
        private static readonly string Folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "drawing",
            "wpf-tests",
            "settings-" + Guid.NewGuid().ToString("N")
        );

        [ModuleInitializer]
        internal static void Use()
        {
            MainViewModel.DefaultSettingsPath = Path.Combine(Folder, "settings.json");
            AppDomain.CurrentDomain.DomainUnload += (s, e) => Delete();
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Delete();
        }

        private static void Delete()
        {
            try
            {
                if (Directory.Exists(Folder))
                {
                    Directory.Delete(Folder, true);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>A folder of its own under %TEMP%\gk\drawing\wpf-tests for the files of one test, deleted after it.</summary>
    internal sealed class TestFolder : IDisposable
    {
        public TestFolder()
        {
            FullPath = Path.Combine(Path.GetTempPath(), "gk", "drawing", "wpf-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(FullPath);
        }

        public string FullPath { get; }

        /// <summary>Saves a drawing in the folder and gives its path.</summary>
        public string Write(string name, DxfDatabase database)
        {
            string path = Path.Combine(FullPath, name);
            database.Save(path);
            return path;
        }

        /// <summary>Writes a file of text in the folder and gives its path.</summary>
        public string WriteText(string name, string text)
        {
            string path = Path.Combine(FullPath, name);
            File.WriteAllText(path, text);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(FullPath, true);
            }
            catch (IOException)
            {
                // A file still held by the system is left for the next clean of %TEMP%.
            }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>Drawings built in code for the view-model and window tests.</summary>
    internal static class TestFiles
    {
        /// <summary>
        /// A room in millimetres: its bottom wall a line on WALLS from (0, 0) to (4 000, 0), a column a circle on COLUMNS of
        /// radius 100 about (2 000, 1 500), and the top wall a line on WALLS from (0, 3 000) to (4 000, 3 000). WALLS is the
        /// current layer; the template gives the layout Layout1.
        /// </summary>
        public static DxfDatabase Room(out string bottom, out string column)
        {
            var db = new DxfDatabase("AC1027");
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS", ColorIndex = 7 });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "COLUMNS", ColorIndex = 1 });
            db.Clayer = "WALLS";
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(4000, 0, 0)) { Layer = "WALLS" };
            var circle = new DxfCircle(new GeoPoint3(2000, 1500, 0), new GeoVector3(0, 0, 1), 100)
            {
                Layer = "COLUMNS",
            };
            var top = new DxfLine(new GeoPoint3(0, 3000, 0), new GeoPoint3(4000, 3000, 0)) { Layer = "WALLS" };
            db.ModelSpace.AppendEntity(line);
            db.ModelSpace.AppendEntity(circle);
            db.ModelSpace.AppendEntity(top);
            bottom = line.Handle;
            column = circle.Handle;
            return db;
        }

        /// <summary>
        /// The room with a linetype GH_DASH (dashes of 100 with gaps of 50) loaded from a file written in the folder, and its
        /// Layout1 holding a viewport 200 by 150 about (150, 100) on the sheet showing the room at 1:20.
        /// </summary>
        public static DxfDatabase RoomWithViewport(TestFolder folder, out string bottom, out string viewport)
        {
            DxfDatabase db = Room(out bottom, out _);
            string lin = folder.WriteText("gh.lin", "*GH_DASH,Dashes for millimetre drawings __ __ __\nA,100,-50\n");
            db.LoadLineTypeFile("GH_DASH", lin);
            DxfLayout layout = null;

            foreach (DxfLayout candidate in db.Layouts)
            {
                if (candidate.LayoutName == "Layout1")
                {
                    layout = candidate;
                }
            }

            layout.Initialize();
            var frame = new DxfViewport
            {
                CenterPoint = new GeoPoint3(150, 100, 0),
                Width = 200,
                Height = 150,
                ViewHeight = 150 * 20.0,
                ViewCenter = new GeoPoint2(2000, 1500),
            };
            layout.BlockTableRecord.AppendEntity(frame);
            frame.On = true;
            viewport = frame.Handle;
            return db;
        }
    }
}

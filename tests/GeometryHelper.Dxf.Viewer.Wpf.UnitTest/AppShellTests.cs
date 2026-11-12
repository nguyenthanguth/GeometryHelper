using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The shell of GeometryHelper DXF: its icon, the association of .dxf and .dwg files with it (written to a fake of the
    /// user's classes, never to the registry), and the Home tab of its screenshot.
    /// </summary>
    public sealed class AppShellTests
    {
        private const string Exe = @"C:\Tools\GeometryHelper DXF\GeometryHelper.Dxf.App.exe";
        private const string Icon = @"C:\Tools\GeometryHelper DXF\drawing-file.ico";

        [Fact]
        public void Register_WritesTheProgIdAndOpenWithForDxfAndDwg_IntoTheUserClasses()
        {
            var classes = new FakeUserClasses();

            CadFileAssociation.Register(classes, "GeometryHelper DXF", Exe, Icon);

            Assert.Equal("GeometryHelper.Drawing", CadFileAssociation.ProgId);
            Assert.Equal("Drawing (GeometryHelper DXF)", classes.Get("GeometryHelper.Drawing", ""));
            Assert.Equal(Icon, classes.Get(@"GeometryHelper.Drawing\DefaultIcon", ""));
            Assert.Equal("\"" + Exe + "\" \"%1\"", classes.Get(@"GeometryHelper.Drawing\shell\open\command", ""));
            Assert.Equal("", classes.Get(@".dxf\OpenWithProgids", "GeometryHelper.Drawing"));
            Assert.Equal("", classes.Get(@".dwg\OpenWithProgids", "GeometryHelper.Drawing"));

            // No program had the extensions: the app is their default.
            Assert.Equal("GeometryHelper.Drawing", classes.Get(".dxf", ""));
            Assert.Equal("GeometryHelper.Drawing", classes.Get(".dwg", ""));
            Assert.Equal(1, classes.Notified);
        }

        [Fact]
        public void Register_LeavesTheDefaultOfAnExtensionAnotherProgramHas_AndUnregisterLeavesItToo()
        {
            var classes = new FakeUserClasses();
            classes.SetValue(".dwg", "", "Other.Drawing.24");
            classes.SetValue(@".dwg\OpenWithProgids", "Other.Drawing.24", "");

            CadFileAssociation.Register(classes, "GeometryHelper DXF", Exe, Icon);
            Assert.Equal("Other.Drawing.24", classes.Get(".dwg", ""));
            Assert.Equal("", classes.Get(@".dwg\OpenWithProgids", "GeometryHelper.Drawing"));

            CadFileAssociation.Unregister(classes);
            Assert.Equal("Other.Drawing.24", classes.Get(".dwg", ""));
            Assert.Equal("", classes.Get(@".dwg\OpenWithProgids", "Other.Drawing.24"));
            Assert.Null(classes.Get(@".dwg\OpenWithProgids", "GeometryHelper.Drawing"));
            Assert.Null(classes.Get(".dxf", ""));
            Assert.False(classes.HasKey("GeometryHelper.Drawing"));
            Assert.False(classes.HasKey(@"GeometryHelper.Drawing\shell\open\command"));
            Assert.Equal(2, classes.Notified);
        }

        [Fact]
        public void TheRegisterSwitch_WritesThroughTheUserClasses_AndSaysWhatItDid()
        {
            var classes = new FakeUserClasses();
            var host = new CadAppHost("GeometryHelper DXF", Path.GetTempPath());
            var output = new StringWriter();

            Assert.Equal(0, host.Associate("--register", classes, Exe, output));
            Assert.Equal("\"" + Exe + "\" \"%1\"", classes.Get(@"GeometryHelper.Drawing\shell\open\command", ""));
            Assert.Contains(".dxf and .dwg files open with GeometryHelper DXF", output.ToString());

            Assert.Equal(0, host.Associate("--unregister", classes, Exe, output));
            Assert.False(classes.HasKey("GeometryHelper.Drawing"));
            Assert.Contains("no longer open with GeometryHelper DXF", output.ToString());

            // The registry refusing is said and given as 1.
            classes.Refuse = true;
            Assert.Equal(1, host.Associate("--register", classes, Exe, output));
            Assert.Contains("could not be registered", output.ToString());
        }

        [Fact]
        public void TheDxfApp_CarriesItsIconAtFiveSizes_AndTheFileIconBesideIt()
        {
            string exe = AppExe();
            string folder = Path.GetDirectoryName(exe);

            // The icon of the exe (ApplicationIcon): one group of 16, 24, 32, 48 and 256 pixels.
            Assert.Equal(new[] { 16, 24, 32, 48, 256 }, GroupIconSizes(exe));

            // The icon of a drawing file, which --register names, is copied beside the exe with the same sizes.
            string fileIcon = Path.Combine(folder, "drawing-file.ico");
            Assert.True(File.Exists(fileIcon), fileIcon);
            var decoder = new IconBitmapDecoder(new Uri(fileIcon), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            Assert.Equal(new[] { 16, 24, 32, 48, 256 }, decoder.Frames.Select(f => f.PixelWidth).OrderBy(w => w));
        }

        [Fact]
        public void TheScreenshot_ShowsTheHomeTab_AndRefusesATabItDoesNotKnow()
        {
            using (var folder = new TestFolder())
            {
                string png = Path.Combine(folder.FullPath, "home.png");

                (int home, string said) = AppHostTests.RunApp("--screenshot", png, "--tab", "home");
                Assert.Equal(0, home);
                Assert.Contains("(1600 by 960)", said);
                Assert.True(File.Exists(png));

                string none = Path.Combine(folder.FullPath, "none.png");
                (int refused, string why) = AppHostTests.RunApp("--screenshot", none, "--tab", "Nowhere");
                Assert.Equal(2, refused);
                Assert.Contains("--tab Nowhere: no such tab.", why);
                Assert.False(File.Exists(none));
            }
        }

        private static string AppExe()
        {
            var bin = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            string configuration = bin.Parent.Name;
            string root = bin.Parent.Parent.Parent.Parent.Parent.FullName;
            string exe = Path.Combine(
                root,
                "src",
                "GeometryHelper.Dxf.App",
                "bin",
                configuration,
                "net8.0-windows",
                "GeometryHelper.Dxf.App.exe"
            );
            Assert.True(File.Exists(exe), exe);
            return exe;
        }

        /// <summary>The sizes of the first icon group of an exe, read from its resources as data, smallest first.</summary>
        private static int[] GroupIconSizes(string exe)
        {
            const uint LoadAsData = 0x00000002 | 0x00000020; // LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_AS_IMAGE_RESOURCE
            IntPtr module = LoadLibraryEx(exe, IntPtr.Zero, LoadAsData);
            Assert.NotEqual(IntPtr.Zero, module);

            try
            {
                IntPtr found = IntPtr.Zero;
                EnumResourceNames(
                    module,
                    (IntPtr)14, // RT_GROUP_ICON
                    (m, type, name, param) =>
                    {
                        found = FindResource(m, name, (IntPtr)14);
                        return false;
                    },
                    IntPtr.Zero
                );
                Assert.NotEqual(IntPtr.Zero, found);
                int size = (int)SizeofResource(module, found);
                IntPtr data = LockResource(LoadResource(module, found));
                var bytes = new byte[size];
                Marshal.Copy(data, bytes, 0, size);

                // GRPICONDIR: reserved, type, count; then 14 bytes an entry, its width first (0 for 256).
                int count = BitConverter.ToUInt16(bytes, 4);
                var sizes = new List<int>();

                for (int i = 0; i < count; i++)
                {
                    int width = bytes[6 + 14 * i];
                    sizes.Add(width == 0 ? 256 : width);
                }

                sizes.Sort();
                return sizes.ToArray();
            }
            finally
            {
                FreeLibrary(module);
            }
        }

        private delegate bool EnumResNameProc(IntPtr module, IntPtr type, IntPtr name, IntPtr param);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string fileName, IntPtr file, uint flags);

        [DllImport("kernel32.dll")]
        private static extern bool FreeLibrary(IntPtr module);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumResourceNames(IntPtr module, IntPtr type, EnumResNameProc proc, IntPtr param);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);

        [DllImport("kernel32.dll")]
        private static extern uint SizeofResource(IntPtr module, IntPtr info);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadResource(IntPtr module, IntPtr info);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LockResource(IntPtr data);

        /// <summary>The user's classes in memory: keys by their path, case not minded, each with its values.</summary>
        private sealed class FakeUserClasses : ICadUserClasses
        {
            private readonly Dictionary<string, Dictionary<string, string>> _keys = new Dictionary<
                string,
                Dictionary<string, string>
            >(StringComparer.OrdinalIgnoreCase);

            public int Notified { get; private set; }

            public bool Refuse { get; set; }

            public string Get(string key, string name) =>
                _keys.TryGetValue(key, out var values) && values.TryGetValue(name, out string value) ? value : null;

            public bool HasKey(string key) => _keys.ContainsKey(key);

            public string GetValue(string key, string name) => Get(key, name);

            public void SetValue(string key, string name, string value)
            {
                if (Refuse)
                {
                    throw new UnauthorizedAccessException("Access to the registry key is denied.");
                }

                if (!_keys.TryGetValue(key, out var values))
                {
                    values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    _keys[key] = values;
                }

                values[name] = value;
            }

            public void DeleteValue(string key, string name)
            {
                if (_keys.TryGetValue(key, out var values))
                {
                    values.Remove(name);
                }
            }

            public void DeleteKey(string key)
            {
                foreach (string path in _keys.Keys.ToList())
                {
                    if (
                        string.Equals(path, key, StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith(key + @"\", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        _keys.Remove(path);
                    }
                }
            }

            public void NotifyShell() => Notified++;
        }
    }
}

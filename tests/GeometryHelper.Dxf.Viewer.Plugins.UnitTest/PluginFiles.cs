using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer.Plugins;
using GeometryHelper.Dxf.Viewer.Wpf.Plugins;
using GeometryHelper.Geometry;

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace GeometryHelper.Dxf.Viewer.Plugins.UnitTest
{
    /// <summary>
    /// A folder of the tests' own under the temporary folder, with the plugin folder, the settings file and the copies of a
    /// manager in it, deleted at the end; and the drawings and plugin files the tests use.
    /// </summary>
    internal sealed class PluginFiles : IDisposable
    {
        public const string SampleName = "GeometryHelper.Dxf.Viewer.SamplePlugin";

        public PluginFiles()
        {
            string all = Path.Combine(Path.GetTempPath(), "gk-plugin-tests");

            // What runs before left: .NET Framework holds the copies it loaded until the process ends.
            foreach (string left in Directory.Exists(all) ? Directory.GetDirectories(all) : new string[0])
            {
                Delete(left);
            }

            Root = Path.Combine(all, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string PluginFolder => Path.Combine(Root, "plugins");

        public string SettingsPath => Path.Combine(Root, "plugins.json");

        public string ShadowFolder => Path.Combine(Root, "copies");

        /// <summary>The sample plugin as the build left it beside the tests.</summary>
        public static string BuiltSample =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SamplePlugin", SampleName + ".dll");

        public CadPluginManager Manager(bool confirm = false)
        {
            var manager = new CadPluginManager(new CadPluginAssemblyLoader(), PluginFolder, SettingsPath, ShadowFolder);

            if (confirm)
            {
                manager.ConfirmUntrusted = path => true;
            }

            return manager;
        }

        /// <summary>A copy of the sample in a folder of its own, as a plugin's author has it in the output of a build.</summary>
        public string Sample(string folder = "dev")
        {
            string dir = Path.Combine(Root, folder);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, SampleName + ".dll");
            File.Copy(BuiltSample, path, true);
            return path;
        }

        /// <summary>A .ghplugin of the sample, with the manifest given.</summary>
        public string Package(
            string manifest,
            string name = "sample.ghplugin",
            params KeyValuePair<string, string>[] extra
        )
        {
            string path = Path.Combine(Root, name);

            using (FileStream stream = File.Create(path))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                using (
                    var writer = new StreamWriter(
                        zip.CreateEntry(CadPluginManifest.FileName).Open(),
                        new UTF8Encoding(false)
                    )
                )
                {
                    writer.Write(manifest);
                }

                zip.CreateEntryFromFile(BuiltSample, SampleName + ".dll");

                foreach (KeyValuePair<string, string> entry in extra)
                {
                    using (var writer = new StreamWriter(zip.CreateEntry(entry.Key).Open(), new UTF8Encoding(false)))
                    {
                        writer.Write(entry.Value);
                    }
                }
            }

            return path;
        }

        public static string Manifest(
            string id = "number-circles",
            string name = "Number circles",
            string api = "1.0",
            string kind = "command"
        ) =>
            "{ \"id\": \""
            + id
            + "\", \"name\": \""
            + name
            + "\", \"version\": \"1.2.0\", \"author\": \"Nguyen Thang\", \"kind\": \""
            + kind
            + "\", \"minimumApiVersion\": \""
            + api
            + "\", \"commands\": [ \"NUMCIRCLES\", \"FAILNOW\" ], \"icon\": \"icon.png\" }";

        public void Dispose() => Delete(Root);

        private static void Delete(string folder)
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                // .NET Framework holds the copies it loaded until the process ends; a later run deletes them.
            }
        }

        /// <summary>
        /// A plan of R2000 with three circles of radius 10, C1 about (0, 0), C2 about (50, 0), C3 about (100, 20), and on layer
        /// 0 a line L1 from (0, -30) to (100, -30).
        /// </summary>
        public static CadDocument Circles()
        {
            DxfDatabase database = Drawing(
                Circle(0, 0, 10, "C1"),
                Circle(50, 0, 10, "C2"),
                Circle(100, 20, 10, "C3"),
                Entity("LINE", "L1", "10", 0.0, "20", -30.0, "30", 0.0, "11", 100.0, "21", -30.0, "31", 0.0)
            );
            return CadDocument.From(database, "circles.dxf");
        }

        public static CadCommandSession Session(CadDocument document, CadCommandRegistry registry) =>
            new CadCommandSession(document, registry) { PickAperture = 1.0 };

        public static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        /// <summary>The texts of the model space, in the order of the space.</summary>
        public static List<DxfDBText> Texts(CadDocument document)
        {
            var texts = new List<DxfDBText>();

            foreach (DxfEntity entity in document.ModelSpace.Block)
            {
                if (entity is DxfDBText text)
                {
                    texts.Add(text);
                }
            }

            return texts;
        }

        private static string Circle(double x, double y, double radius, string handle) =>
            Entity("CIRCLE", handle, "10", x, "20", y, "30", 0.0, "40", radius);

        private static DxfDatabase Drawing(params string[] entities)
        {
            var text = new StringBuilder();
            text.Append("0\nSECTION\n2\nHEADER\n9\n$ACADVER\n1\nAC1015\n0\nENDSEC\n");
            text.Append("0\nSECTION\n2\nTABLES\n");
            text.Append(
                "0\nTABLE\n2\nLTYPE\n70\n1\n0\nLTYPE\n2\nCONTINUOUS\n70\n0\n3\nSolid line\n72\n65\n73\n0\n40\n0\n0\nENDTAB\n"
            );
            text.Append(
                "0\nTABLE\n2\nLAYER\n70\n1\n0\nLAYER\n2\n0\n70\n0\n62\n7\n6\nCONTINUOUS\n0\nENDTAB\n0\nENDSEC\n"
            );
            text.Append("0\nSECTION\n2\nBLOCKS\n0\nENDSEC\n");
            text.Append("0\nSECTION\n2\nENTITIES\n").Append(string.Concat(entities)).Append("0\nENDSEC\n0\nEOF\n");
            return DxfDatabase.Parse(text.ToString());
        }

        private static string Entity(string type, string handle, params object[] codes)
        {
            var builder = new StringBuilder();
            builder.Append("0\n").Append(type).Append("\n5\n").Append(handle).Append("\n8\n0\n");

            for (int i = 0; i + 1 < codes.Length; i += 2)
            {
                object value = codes[i + 1];
                builder
                    .Append(codes[i])
                    .Append('\n')
                    .Append(value is double number ? number.ToString("R", CultureInfo.InvariantCulture) : value)
                    .Append('\n');
            }

            return builder.ToString();
        }
    }
}

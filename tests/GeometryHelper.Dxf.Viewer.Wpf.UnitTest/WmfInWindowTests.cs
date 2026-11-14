using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>WMFIN in the window: its file asked by the window's Open dialog, titled Import WMF, and the description of the command.</summary>
    public class WmfInWindowTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "wmfwin-" + Guid.NewGuid().ToString("N")
        );

        public WmfInWindowTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file still held is left for the system to clear.
            }
        }

        /// <summary>A metafile of one line from (100, 100) to (900, 900) in a window of 1000 by 1000, without a placeable header.</summary>
        private static byte[] OneLine()
        {
            var records = new MemoryStream();
            var writer = new BinaryWriter(records);

            void Record(ushort function, params short[] parameters)
            {
                writer.Write((uint)(3 + parameters.Length));
                writer.Write(function);

                foreach (short value in parameters)
                {
                    writer.Write(value);
                }
            }

            Record(0x020C, 1000, 1000);
            Record(0x0214, 100, 100);
            Record(0x0213, 900, 900);
            Record(0x0000);
            byte[] body = records.ToArray();
            var file = new MemoryStream();
            var head = new BinaryWriter(file);
            head.Write((ushort)1);
            head.Write((ushort)9);
            head.Write((ushort)0x0300);
            head.Write((uint)((18 + body.Length) / 2));
            head.Write((ushort)0);
            head.Write((uint)6);
            head.Write((ushort)0);
            head.Write(body);
            return file.ToArray();
        }

        private static void Type(MainViewModel model, string text)
        {
            model.CommandText = text;
            model.SubmitCommand();
        }

        [Fact]
        public void TheWindow_AsksTheMetafileOfItsOpenDialog_AndMakesBlockWmf0()
        {
            string metafile = Path.Combine(_folder, "line.wmf");
            File.WriteAllBytes(metafile, OneLine());
            string source = Path.Combine(_folder, "plan.dxf");
            PlotPdfTests.Plan().Database.Save(source);
            var model = new MainViewModel();
            Assert.True(model.Open(source));
            var asked = new List<CadExportJob>();
            model.AskExportFile = job =>
            {
                asked.Add(job);
                job.FileName = metafile;
            };

            model.StartCommand("WMFIN");

            CadExportJob dialog = Assert.Single(asked);
            Assert.True(dialog.Opens);
            Assert.Equal("Import WMF", dialog.Title);
            Assert.Equal(new[] { "Metafile (*.wmf)" }, dialog.FileTypes);
            Type(model, "0,0");
            Type(model, string.Empty);
            Type(model, string.Empty);
            Type(model, string.Empty);
            Assert.True(model.Document.Database.BlockTable.Has("WMF0"));
            Assert.Single(model.Document.Database.BlockTable["WMF0"].OfType<DxfPolyline2d>());
            Assert.Single(model.Document.ModelSpace.Block.OfType<DxfBlockReference>());
        }

        [Fact]
        public void WmfinHasADescription_ForTheShortcutsList()
        {
            var model = new MainViewModel();
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Shortcuts");

            Assert.Equal(
                "Bring a Windows Metafile in as a block",
                dialog.Shortcuts.Single(r => r.Command == "WMFIN").Description
            );
        }
    }
}

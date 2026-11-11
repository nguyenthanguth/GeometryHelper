using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// A feature control frame edited by a double click, as AutoCAD 2021's TEXTEDIT edits one: the Geometric Tolerance dialog
    /// opened on the frame's own boxes, OK writing the words it ends with.
    /// </summary>
    public sealed class GeometricToleranceEditTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void ADoubleClickOnAFrame_OpensTheDialogOnItsBoxes_AndOkWritesItsWords()
        {
            var model = new MainViewModel();
            Assert.True(
                model.Open(_folder.Write("room-" + Guid.NewGuid().ToString("N") + ".dxf", TestFiles.Room(out _, out _)))
            );
            model.ShowGeometricTolerance = dialog =>
            {
                dialog.Rows[0].Symbol = 'j';
                dialog.Rows[0].Tolerance1.Diameter = true;
                dialog.Rows[0].Tolerance1.Value = "0.1";
                dialog.Rows[0].Datums[0].Letter = "A";
                dialog.Height = "5";
                dialog.ProjectedZone = true;
                dialog.Ok();
                return true;
            };
            model.Execute("TOLERANCE");
            model.Execute("10,20");
            DxfTolerance frame = Assert.Single(model.Document.ModelSpace.Block.OfType<DxfTolerance>());

            GeometricToleranceViewModel shown = null;
            model.ShowGeometricTolerance = dialog =>
            {
                shown = dialog;
                Assert.Equal('j', dialog.Rows[0].Symbol);
                Assert.True(dialog.Rows[0].Tolerance1.Diameter);
                Assert.Equal("0.1", dialog.Rows[0].Tolerance1.Value);
                Assert.Equal("A", dialog.Rows[0].Datums[0].Letter);
                Assert.Null(dialog.Rows[1].Symbol);
                Assert.Equal("5", dialog.Height);
                Assert.True(dialog.ProjectedZone);
                dialog.Rows[0].Tolerance1.Value = "0.2";
                dialog.Ok();
                return true;
            };

            Assert.True(model.EditTextAt(frame.Handle));

            Assert.NotNull(shown);
            Assert.Equal(
                "{\\Fgdt;j}%%v{\\Fgdt;n}0.2%%v%%vA\n5{\\Fgdt;p}",
                ((DxfTolerance)model.Document.FindEntity(frame.Handle)).Text
            );
        }
    }
}

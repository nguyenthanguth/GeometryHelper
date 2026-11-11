using System;
using System.IO;
using System.Linq;
using System.Threading;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>The Hatch dialog offers the presenter's own patterns first, an acadiso.pat of the user's folders never ahead of them.</summary>
    public class BuiltInPatternsWindowTests
    {
        [Fact]
        public void TheHatchDialog_OffersEveryOwnPatternFirst_AndAnAcadisoPatAddsOnlyWhatTheOwnSetHasNot()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        string support = Path.GetDirectoryName(
                            folder.WriteText(
                                "acadiso.pat",
                                "*ANSI31,Not ours\n45, 0,0, 0,10\n*MINE,Mine\n0, 0,0, 0,5\n"
                            )
                        );
                        CadDocument document = CadDocument.From(new DxfDatabase("AC1027"), "blank.dxf");
                        document.SupportFolders = new[] { support };

                        var dialog = new HatchSettingsViewModel(document, new CadHatchSettings(), false);
                        Assert.Equal(
                            CadPatternFile.BuiltIn.Select(p => p.Name).Concat(new[] { "MINE" }).ToArray(),
                            dialog.Patterns.Select(p => p.Name).ToArray()
                        );
                        Assert.Equal(
                            CadPatternFile.BuiltInName,
                            dialog.Patterns.Single(p => p.Name == "ANSI31").Source
                        );
                        Assert.Contains(dialog.Patterns, p => p.Name == "AR-BRSTD");
                        Assert.Contains(dialog.Patterns, p => p.Name == "ISO02W100");
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }
    }
}

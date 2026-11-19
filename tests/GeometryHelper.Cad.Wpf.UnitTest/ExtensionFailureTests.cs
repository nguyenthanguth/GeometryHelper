using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Enums;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// An extension that fails must not take the presenter with it. Its menu items and its Properties rows are asked for on
    /// every right click and every selection, so an exception it throws there is caught, said as a Warn line of
    /// <see cref="GeometryHelperLog"/>, and the presenter shows its own items and rows alone. A row's setter that makes the
    /// extension ask for its rows again leaves one set of rows, read once the setter is done. The room has its bottom wall from
    /// (0, 0) to (4 000, 0).
    /// </summary>
    public sealed class ExtensionFailureTests
    {
        private sealed class FailingExtension : CadWorkspaceExtension
        {
            public Func<IReadOnlyList<CadExtensionMenuItem>> Menu = () => null;
            public Func<CadSelectionInfo, CadSelectionInfo> Describe = shown => shown;
            public int Described;

            public override IReadOnlyList<CadExtensionMenuItem> CanvasMenuItems(
                CadDocument document,
                IReadOnlyList<string> handles
            ) => Menu();

            public override CadSelectionInfo DescribeSelection(
                CadDocument document,
                IReadOnlyList<string> handles,
                CadSelectionInfo shown,
                out string banner
            )
            {
                Described++;
                banner = null;
                return Describe(shown);
            }
        }

        private static MainViewModel Open(TestFolder folder, out string bottom)
        {
            DxfDatabase database = TestFiles.Room(out bottom, out _);
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", database)));
            return model;
        }

        private static List<string> Warnings(Action run)
        {
            var lines = new List<string>();
            Action<GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;

            try
            {
                GeometryHelperLog.Writer = (level, text, exception) =>
                {
                    lock (lines)
                    {
                        lines.Add(GeometryHelperLog.Format(level, text, exception));
                    }
                };
                run();
            }
            finally
            {
                GeometryHelperLog.Writer = before;
            }

            return lines;
        }

        private static CadSelectionInfo Rows(string label, params CadPropertyRow[] rows) =>
            new CadSelectionInfo(label, new[] { new CadPropertyGroup("Group", rows) });

        [Fact]
        public void AnExtensionWhoseMenuThrows_LeavesTheEditMenuAsItWas_AndIsSaidInTheLog()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel plain = Open(folder, out string bottomPlain);
                plain.Document.Selection.Set(new[] { bottomPlain });
                string[] before = plain.CanvasMenu().Select(i => i.Header).ToArray();

                MainViewModel model = Open(folder, out string bottom);
                model.AddExtension(
                    new FailingExtension { Menu = () => throw new InvalidOperationException("The drawing is locked.") }
                );
                model.Document.Selection.Set(new[] { bottom });
                string[] after = null;

                List<string> lines = Warnings(() => after = model.CanvasMenu().Select(i => i.Header).ToArray());

                Assert.Equal(before, after);
                Assert.Contains(lines, l =>
                    l.StartsWith("WARN ", StringComparison.Ordinal) && l.Contains("The drawing is locked.")
                );
            }
        }

        [Fact]
        public void OneExtensionWhoseMenuThrows_DoesNotTakeAwayTheItemsOfTheOthers()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string bottom);
                model.AddExtension(
                    new FailingExtension { Menu = () => throw new InvalidOperationException("Broken.") }
                );
                model.AddExtension(
                    new FailingExtension { Menu = () => new[] { new CadExtensionMenuItem("Fit to sheet", () => { }) } }
                );
                model.Document.Selection.Set(new[] { bottom });
                string[] headers = null;

                Warnings(() => headers = model.CanvasMenu().Select(i => i.Header).ToArray());

                Assert.Equal("Fit to sheet", headers[0]);
            }
        }

        [Fact]
        public void AnExtensionWhoseDescriptionThrows_LeavesTheRowsOfThePresenter_AndIsSaidInTheLog()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel plain = Open(folder, out string bottomPlain);
                plain.Document.Selection.Set(new[] { bottomPlain });
                string[] before = plain.PropertyGroups.SelectMany(g => g.Rows).Select(r => r.ToString()).ToArray();

                MainViewModel model = Open(folder, out string bottom);
                model.AddExtension(
                    new FailingExtension { Describe = _ => throw new InvalidOperationException("The source is gone.") }
                );
                string[] after = null;

                List<string> lines = Warnings(() =>
                {
                    model.Document.Selection.Set(new[] { bottom });
                    after = model.PropertyGroups.SelectMany(g => g.Rows).Select(r => r.ToString()).ToArray();
                });

                Assert.NotEmpty(before);
                Assert.Equal(before, after);
                Assert.Contains(lines, l =>
                    l.StartsWith("WARN ", StringComparison.Ordinal) && l.Contains("The source is gone.")
                );
            }
        }

        [Fact]
        public void ASetterWhoseActionAsksTheExtensionsAgain_LeavesOneSetOfRows_ReadOnceTheSetterIsDone()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string bottom);
                string scale = "1:50";
                FailingExtension extension = null;
                extension = new FailingExtension
                {
                    Describe = _ => Rows("View (1)", new CadPropertyRow("Scale", scale, t =>
                            {
                                scale = t;
                                model.RefreshFromExtensions();
                                return null;
                            })),
                };
                model.AddExtension(extension);
                model.Document.Selection.Set(new[] { bottom });
                PropertyRowViewModel old = model.PropertyGroups.SelectMany(g => g.Rows).Single(r => r.Label == "Scale");
                int asked = extension.Described;
                int built = 0;
                model.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MainViewModel.PropertyGroups))
                    {
                        built++;
                    }
                };

                old.Value = "1:20";

                PropertyRowViewModel row = model.PropertyGroups.SelectMany(g => g.Rows).Single(r => r.Label == "Scale");
                Assert.Equal(1, built);
                Assert.Equal(1, extension.Described - asked);
                Assert.NotSame(old, row);
                Assert.Equal("1:20", row.Value);
            }
        }

        [Fact]
        public void ASetterThatRefreshesAndThenRefuses_LeavesOneSetOfRows_AndTheRefusalIsSaid()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string bottom);
                FailingExtension extension = null;
                extension = new FailingExtension
                {
                    Describe = _ => Rows("View (1)", new CadPropertyRow("Scale", "1:50", t =>
                            {
                                model.RefreshFromExtensions();
                                return "A scale is 1: and a number above nought.";
                            })),
                };
                model.AddExtension(extension);
                model.Document.Selection.Set(new[] { bottom });
                PropertyRowViewModel old = model.PropertyGroups.SelectMany(g => g.Rows).Single(r => r.Label == "Scale");
                int asked = extension.Described;

                old.Value = "1:0";

                Assert.Equal(1, extension.Described - asked);
                Assert.Equal("1:50", old.Value);
                Assert.Equal(
                    "1:50",
                    model.PropertyGroups.SelectMany(g => g.Rows).Single(r => r.Label == "Scale").Value
                );
                Assert.Contains("A scale is 1: and a number above nought.", model.History);
            }
        }
    }
}

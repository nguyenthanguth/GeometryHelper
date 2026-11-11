using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The arrowhead lists of the Dimension Style dialog (First, Second, Leader) and of the Multileader Style dialog (Symbol)
    /// with each arrowhead's picture before its name, as AutoCAD 2021's lists show them and as Leader Settings draws them; a
    /// user arrow drawn from its block, the other lists of the dialogs with their words alone.
    /// </summary>
    public sealed class ArrowListPictureTests
    {
        private static void OnSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                throw new Xunit.Sdk.XunitException(failure.ToString());
            }
        }

        /// <summary>A metric drawing with a block TICK of one line, the leader arrow of its current dimension style.</summary>
        private static MainViewModel Opened(out CadDocument document)
        {
            var model = new MainViewModel();
            document = CadDocument.From(CadTemplate.CreateMetric(), "arrows.dxf");
            model.OpenDocument(document);
            document.ApplyEdit("block", null, s =>
            {
                var block = new DxfBlockTableRecord { Name = "TICK", Origin = new GeoPoint3(0, 0, 0) };
                s.Database.BlockTable.Add(block);
                block.AppendEntity(new DxfLine(new GeoPoint3(-0.5, -0.5, 0), new GeoPoint3(0.5, 0.5, 0)));
                s.Database.DimStyleTable[s.Database.Dimstyle].Dimldrblk = block;
            });
            return model;
        }

        private static IEnumerable<T> Within<T>(DependencyObject parent)
            where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T found)
                {
                    yield return found;
                }

                foreach (T deeper in Within<T>(child))
                {
                    yield return deeper;
                }
            }
        }

        private static void LayOut(Window window, FrameworkElement body)
        {
            object context = window.DataContext;
            window.Content = null;
            body.DataContext = context;
            body.Measure(new Size(window.Width, window.Height));
            body.Arrange(new Rect(0, 0, window.Width, window.Height));
            body.UpdateLayout();
        }

        [Fact]
        public void TheDimensionStyleArrowheadLists_ShowEachArrowheadsPicture_AndTheOtherListsTheirWordsAlone()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;
                MainViewModel model = Opened(out CadDocument document);
                DimStyleEditViewModel editor = model.CreateDimStyleManager().CreateModify();
                IReadOnlyList<DimFieldViewModel> arrows = editor.FieldsOf("Symbols and arrows");

                foreach (string list in new[] { "First", "Second", "Leader" })
                {
                    DimFieldViewModel field = arrows.Single(f => f.Label == list);
                    Assert.Contains(field.Choices, c => c.Label == "TICK");
                    Assert.All(field.Choices, c =>
                    {
                        Assert.NotNull(c.Picture);
                        Assert.Equal(22, c.Picture.PixelWidth);
                        Assert.Equal(12, c.Picture.PixelHeight);
                    });
                }

                Assert.All(
                    editor.FieldsOf("Lines").Where(f => f.Kind == DimFieldKind.Choice).SelectMany(f => f.Choices),
                    c => Assert.Null(c.Picture)
                );

                // The window on that tab draws the arrowhead chosen with its picture in the box.
                editor.Tab = "Symbols and arrows";
                var window = new DimStyleEditWindow(editor);
                LayOut(window, window.Body);
                DimChoice first = arrows.Single(f => f.Label == "First").SelectedChoice;
                Assert.Contains(Within<Image>(window.Body), i => ReferenceEquals(i.Source, first.Picture));
                window.Close();
            });
        }

        [Fact]
        public void TheMultileaderStyleSymbolList_ShowsEachArrowheadsPicture_AndTheOtherListsTheirWordsAlone()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;
                MainViewModel model = Opened(out CadDocument document);
                MLeaderStyleEditViewModel editor = model.CreateMLeaderStyleManager().CreateModify();
                MLeaderFieldViewModel symbol = editor.FieldsOf("Leader Format").Single(f => f.Label == "Symbol");

                Assert.Contains(symbol.Choices, c => c.Label == "Closed filled");
                Assert.All(symbol.Choices, c =>
                {
                    Assert.NotNull(c.Picture);
                    Assert.Equal(22, c.Picture.PixelWidth);
                    Assert.Equal(12, c.Picture.PixelHeight);
                });
                Assert.All(
                    editor
                        .FieldsOf("Leader Format")
                        .Where(f => f.Kind == MLeaderFieldKind.Choice && f.Label != "Symbol")
                        .SelectMany(f => f.Choices),
                    c => Assert.Null(c.Picture)
                );

                var window = new MLeaderStyleEditWindow(editor);
                LayOut(window, window.Body);
                MLeaderChoice chosen = symbol.SelectedChoice;
                Assert.Contains(Within<Image>(window.Body), i => ReferenceEquals(i.Source, chosen.Picture));
                window.Close();
            });
        }
    }
}

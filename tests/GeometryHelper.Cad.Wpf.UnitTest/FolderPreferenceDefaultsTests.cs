using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Folder preference shows the default folders beside GeometryHelper.Cad.dll as read-only rows marked "(default)", each with
    /// Open folder, and says in which order fonts, hatch patterns and linetypes are looked for. They are never saved into the
    /// settings file, and Explorer is never started: the window's Open folder is stubbed.
    /// </summary>
    public sealed class FolderPreferenceDefaultsTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private GlobalSettingsViewModel Dialog(string program, out List<string> opened)
        {
            GlobalSettingsViewModel dialog = new MainViewModel().CreateGlobalSettings("Folder preference");
            var list = new List<string>();
            dialog.OpenFolder = list.Add;
            dialog.SupportRoots = new CadSupportFolders(program);
            opened = list;
            return dialog;
        }

        [Fact]
        public void TheDefaultFolders_AreReadOnlyRowsOfTheirKind_MarkedDefaultOrNotFound()
        {
            string program = Path.Combine(_folder.FullPath, "dll");
            Directory.CreateDirectory(Path.Combine(program, "Fonts"));
            Directory.CreateDirectory(Path.Combine(program, "Linetypes"));

            GlobalSettingsViewModel dialog = Dialog(program, out _);

            DefaultFolderRow fonts = Assert.Single(dialog.DefaultFontFolders);
            Assert.Equal("Fonts", fonts.Kind);
            Assert.Equal(Path.Combine(program, "Fonts"), fonts.Folder);
            Assert.Equal("(default)", fonts.Mark);
            Assert.Equal(
                new[] { "Hatch patterns", "Linetypes", "AutoLisps", "Drawing templates" },
                dialog.DefaultSupportFolders.Select(r => r.Kind)
            );
            Assert.Equal("(default) not found", dialog.DefaultSupportFolders[0].Mark);
            Assert.Equal("(default)", dialog.DefaultSupportFolders[1].Mark);
            Assert.Equal(Path.Combine(program, "Hatches"), dialog.DefaultSupportFolders[0].Folder);
            Assert.Empty(dialog.FontFolders);
            Assert.Empty(dialog.SupportFolders);
        }

        [Fact]
        public void TheAutoLispsFolder_IsAReadOnlyRowAfterLinetypes_WithOpenFolder_AndIsNotSaved()
        {
            string program = Path.Combine(_folder.FullPath, "dll");
            GlobalSettingsViewModel missing = Dialog(program, out _);
            DefaultFolderRow absent = Assert.Single(missing.DefaultSupportFolders, r => r.Kind == "AutoLisps");
            Assert.Equal("(default) not found", absent.Mark);
            Directory.CreateDirectory(Path.Combine(program, "AutoLisps"));

            GlobalSettingsViewModel dialog = Dialog(program, out List<string> opened);

            Assert.Equal("AutoLisps", dialog.DefaultSupportFolders[2].Kind);
            DefaultFolderRow lisps = dialog.DefaultSupportFolders[2];
            Assert.Equal(Path.Combine(program, "AutoLisps"), lisps.Folder);
            Assert.Equal("(default)", lisps.Mark);
            Assert.True(dialog.OpenDefaultFolderCommand.CanExecute(lisps));
            dialog.OpenDefaultFolderCommand.Execute(lisps);
            Assert.Equal(new[] { Path.Combine(program, "AutoLisps") }, opened);
            Assert.Empty(dialog.SupportFolders);
        }

        [Fact]
        public void OpenFolder_HandsTheFolderToTheWindow_OnlyWhereItExists_AndNothingIsStarted()
        {
            string program = Path.Combine(_folder.FullPath, "dll");
            Directory.CreateDirectory(Path.Combine(program, "Fonts"));
            GlobalSettingsViewModel dialog = Dialog(program, out List<string> opened);

            Assert.True(dialog.OpenDefaultFolderCommand.CanExecute(dialog.DefaultFontFolders[0]));
            dialog.OpenDefaultFolderCommand.Execute(dialog.DefaultFontFolders[0]);
            Assert.False(dialog.OpenDefaultFolderCommand.CanExecute(dialog.DefaultSupportFolders[0]));
            dialog.OpenDefaultFolderCommand.Execute(null);

            Assert.Equal(new[] { Path.Combine(program, "Fonts") }, opened);
        }

        [Fact]
        public void TheDefaultFolders_AreNotSavedIntoTheSettings()
        {
            string program = Path.Combine(_folder.FullPath, "dll");
            var model = new MainViewModel { SettingsPath = Path.Combine(_folder.FullPath, "settings.json") };
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Folder preference");
            dialog.SupportRoots = new CadSupportFolders(program);
            dialog.AddFontFolderCommand.Execute(@"D:\MyFonts");

            Assert.True(dialog.Apply());

            Assert.Equal(new[] { @"D:\MyFonts" }, model.Settings.FontSearchFolders);
            Assert.Empty(model.Settings.SupportSearchFolders);
            Assert.DoesNotContain(program.Replace(@"\", @"\\"), model.Settings.ToJson());
        }

        [Fact]
        public void TheSearchOrderOfEachKind_IsSaidInAWord()
        {
            GlobalSettingsViewModel dialog = Dialog(Path.Combine(_folder.FullPath, "dll"), out _);

            Assert.Contains("installed in Windows", dialog.FontOrderText);
            Assert.EndsWith("then the built-in font.", dialog.FontOrderText);
            Assert.Contains("default Hatches folder", dialog.HatchOrderText);
            Assert.EndsWith("then the built-in patterns.", dialog.HatchOrderText);
            Assert.Contains("default Linetypes folder", dialog.LinetypeOrderText);
            Assert.EndsWith("then the built-in linetypes.", dialog.LinetypeOrderText);
        }

        [Fact]
        public void ThePage_DrawsTheDefaultRows_OffscreenWithNoWindowShown()
        {
            Exception failure = null;
            int colours = 0;
            var thread = new Thread(() =>
            {
                try
                {
                    string program = Path.Combine(_folder.FullPath, "dll");
                    Directory.CreateDirectory(Path.Combine(program, "Fonts"));
                    Directory.CreateDirectory(Path.Combine(program, "Hatches"));
                    GlobalSettingsViewModel model = Dialog(program, out _);
                    model.AddFontFolderCommand.Execute(@"D:\MyFonts\Shx");
                    model.AddSupportFolderCommand.Execute(@"D:\MySupport");
                    var window = new GlobalSettingsWindow(model);
                    FrameworkElement body = window.Body;
                    window.Content = null;
                    body.DataContext = model;
                    body.Measure(new Size(840, 600));
                    body.Arrange(new Rect(0, 0, 840, 600));
                    body.UpdateLayout();
                    var bitmap = new RenderTargetBitmap(840, 600, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(body);
                    var pixels = new byte[840 * 600 * 4];
                    bitmap.CopyPixels(pixels, 840 * 4, 0);
                    var seen = new HashSet<int>();

                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        seen.Add(BitConverter.ToInt32(pixels, i));
                    }

                    colours = seen.Count;
                    string target = Environment.GetEnvironmentVariable("GH_FOLDER_PREFERENCE_PNG");

                    if (!string.IsNullOrEmpty(target))
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));

                        using (FileStream stream = File.Create(target))
                        {
                            encoder.Save(stream);
                        }
                    }

                    window.Close();
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
            Assert.True(colours > 30, colours + " colours");
        }
    }
}

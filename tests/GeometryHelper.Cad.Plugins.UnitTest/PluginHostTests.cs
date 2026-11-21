using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Plugins.UnitTest
{
    /// <summary>
    /// What a plugin adds to the window, read through the public API only: the sample UI plugin's registrations, the order and
    /// the number of times an application class is initialised and terminated, a plugin whose Initialize fails, two plugins
    /// that want the same tab or keyword, the version of the plugin API, and the icons. Running a callback needs a context
    /// only the window makes; that is tested where the internals are seen (Cad.UnitTest, Cad.Wpf.UnitTest).
    /// </summary>
    public sealed class PluginHostTests : IDisposable
    {
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private readonly PluginFiles _files = new PluginFiles();

        public void Dispose()
        {
            _files.Dispose();
        }

        private string Log => Path.Combine(_files.Root, "log.txt");

        private CadPlugin LoadSample(CadPluginManager manager)
        {
            CadPluginResult result = manager.Load(UiSample.Copy(_files));
            Assert.True(result.Succeeded, result.ToString());
            return result.Plugin.Plugin;
        }

        private CadPluginResult LoadEmitted(CadPluginManager manager, string stem, params EmittedClass[] classes)
        {
            string path = EmittedPlugin.ToFile(
                Path.Combine(_files.Root, stem),
                EmittedPlugin.UniqueName(stem),
                "1.1",
                Log,
                classes
            );
            return manager.Load(path);
        }

        // ---- the sample UI plugin ----

        [Fact]
        public void TheSampleUiPluginIsLoadedWithNoProblemStatingApiOnePointOne()
        {
            using (CadPluginManager manager = _files.Manager(true))
            {
                CadPluginResult result = manager.Load(UiSample.Copy(_files));

                Assert.True(result.Succeeded, result.ToString());
                CadPlugin plugin = result.Plugin.Plugin;
                Assert.Equal(UiSample.Name, plugin.Name);
                Assert.Empty(plugin.Problems);
                Assert.False(plugin.Host.IsEmpty);
                Assert.Equal(new Version(1, 1), plugin.RequiredApiVersion);
                Assert.Equal(UiSample.Name, plugin.Host.PluginName);
                Assert.True(plugin.Host.ShowsCommandButtons);
            }
        }

        [Fact]
        public void TheSampleAddsTheTabCirclesAndTheContextualTabCircleInThatOrder()
        {
            using (CadPluginManager manager = _files.Manager(true))
            {
                CadPluginHost host = LoadSample(manager).Host;

                Assert.Equal(new[] { "circles", "circle" }, host.RibbonTabs.Select(t => t.Id.ToLowerInvariant()));
                Assert.Equal(new[] { "Circles", "Circle" }, host.RibbonTabs.Select(t => t.Title));
                Assert.Null(host.RibbonTabs[0].ShowWhen);
                Assert.NotNull(host.RibbonTabs[1].ShowWhen);
                CadRibbonPanel number = Assert.Single(host.RibbonTabs[0].Panels);
                Assert.Equal("Number", number.Title);
                CadRibbonPanel marks = Assert.Single(host.RibbonTabs[1].Panels);
                Assert.NotEmpty(marks.Title);
            }
        }

        [Fact]
        public void TheNumberPanelHoldsALargeCommandButtonTwoCallbackButtonsAndASplitOfThree()
        {
            using (CadPluginManager manager = _files.Manager(true))
            {
                CadRibbonPanel panel = Assert.Single(LoadSample(manager).Host.RibbonTabs[0].Panels);

                Assert.Equal(4, panel.Items.Count);
                var large = Assert.IsType<CadRibbonButton>(panel.Items[0]);
                Assert.Equal("Number circles", large.Text);
                Assert.Equal(CadRibbonItemSize.Large, large.Size);
                Assert.Equal("NUMCIRCLES2", large.Command);
                Assert.Null(large.Run);
                Assert.NotNull(large.Icon.PathData);
                Assert.Null(large.Icon.Image);
                Assert.NotEmpty(large.ToolTip);
                Assert.NotNull(large.Description);

                foreach (int i in new[] { 1, 2 })
                {
                    var small = Assert.IsType<CadRibbonButton>(panel.Items[i]);
                    Assert.Equal(CadRibbonItemSize.Standard, small.Size);
                    Assert.True(small.ShowText);
                    Assert.Null(small.Command);
                    Assert.NotNull(small.Run);
                }

                Assert.Equal(new[] { "Count", "Clear marks" }, panel.Items.Skip(1).Take(2).Select(i => i.Text));
                var split = Assert.IsType<CadRibbonSplitButton>(panel.Items[3]);
                Assert.Equal("Sizes", split.Text);
                Assert.Equal(3, split.Buttons.Count);
                Assert.True(split.RemembersLast);
            }
        }

        [Fact]
        public void TheCountButtonsIconIsThePngTheSampleEmbedsAndTheLargeButtonsIsPathData()
        {
            using (CadPluginManager manager = _files.Manager(true))
            {
                CadRibbonPanel panel = Assert.Single(LoadSample(manager).Host.RibbonTabs[0].Panels);

                CadPluginIcon count = panel.Items[1].Icon;
                Assert.Null(count.PathData);
                Assert.Equal(PngSignature, count.Image.Take(8).ToArray());
                Assert.True(count.Image.Count > PngSignature.Length);
                Assert.StartsWith("M", ((CadRibbonButton)panel.Items[0]).Icon.PathData, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void TheSampleAddsAPanelToThePluginsTabAProviderADrawingsButtonAndMenuACanvasMenuAndOneSnap()
        {
            using (CadPluginManager manager = _files.Manager(true))
            {
                CadPluginHost host = LoadSample(manager).Host;

                CadRibbonPanel panel = Assert.Single(host.RibbonPanels);
                Assert.Equal("Circle tools", panel.Title);
                Assert.Single(panel.Items);
                Assert.Single(host.PropertyProviders);
                CadPluginButton button = Assert.Single(host.DrawingsButtons);
                Assert.NotEmpty(button.ToolTip);
                Assert.Single(host.DrawingsMenus);
                Assert.Single(host.CanvasMenus);
                Assert.Single(host.ObjectSnaps);
            }
        }

        [Fact]
        public void TheSnapIsCircleTopWithTheKeywordCtopForCirclesAndAFilledTriangleGlyphPointingUp()
        {
            using (CadPluginManager manager = _files.Manager(true))
            {
                CadCustomSnap snap = Assert.Single(LoadSample(manager).Host.ObjectSnaps);

                Assert.Equal("Circle top", snap.Name);
                Assert.Equal("CTOP", snap.Keyword);
                Assert.Equal(typeof(DxfCircle), Assert.Single(snap.EntityTypes));
                Assert.False(snap.IsOnByDefault);
                CadMarkerStroke glyph = Assert.Single(snap.Glyph);
                Assert.True(glyph.IsClosed);
                Assert.True(glyph.IsFilled);
                Assert.Equal(3, glyph.Points.Count);

                // Y is down in a marker, so the tip that points up has the smallest Y, alone.
                double tip = glyph.Points.Min(p => p.Y);
                Assert.Single(glyph.Points, p => p.Y - tip < 1E-9);
                Assert.Equal(2, glyph.Points.Count(p => p.Y - tip > 1E-9));
            }
        }

        [Fact]
        public void HostFolderIsTheFolderThePluginWasLoadedFromAndNotItsShadowCopy()
        {
            using (CadPluginManager manager = _files.Manager(true))
            {
                string path = UiSample.Copy(_files);
                CadPluginResult result = manager.Load(path);
                Assert.True(result.Succeeded, result.ToString());

                string folder = result.Plugin.Plugin.Host.Folder;

                Assert.Equal(Path.GetDirectoryName(path), folder);
                Assert.False(folder.StartsWith(_files.ShadowFolder, StringComparison.OrdinalIgnoreCase));
            }
        }

        [Fact]
        public void EveryAddMethodAfterInitializeHasReturnedThrowsSayingTheyAreForInitializeOnly()
        {
            using (CadPluginManager manager = _files.Manager(true))
            {
                CadPluginHost host = LoadSample(manager).Host;
                string words = "The plugin " + UiSample.Name + " adds to the window only in Initialize.";
                var snap = new CadCustomSnap("Late", "LATESNAP", r => null);

                Action[] adds =
                {
                    () => host.AddRibbonTab(new CadRibbonTab("late", "Late")),
                    () => host.AddRibbonPanel(new CadRibbonPanel("Late")),
                    () => host.AddProperties(r => null),
                    () =>
                        host.AddDrawingsButton(
                            new CadPluginButton(CadPluginIcon.FromPathData("M0 0 L1 1"), "Late", c => { })
                        ),
                    () => host.AddDrawingsMenu(r => null),
                    () => host.AddCanvasMenu(c => null),
                    () => host.AddObjectSnap(snap),
                };

                foreach (Action add in adds)
                {
                    InvalidOperationException refused = Assert.Throws<InvalidOperationException>(add);
                    Assert.Equal(words, refused.Message);
                }

                Assert.Equal(2, host.RibbonTabs.Count);
                Assert.Single(host.ObjectSnaps);
            }
        }

        // ---- Initialize and Terminate ----

        [Fact]
        public void AnInitializeThatThrowsKeepsTheCommandsAndDropsEveryAdditionSayingWhy()
        {
            var registry = CadCommandRegistry.CreateDefault();

            using (CadPluginManager manager = _files.Manager(true))
            {
                manager.Attach(registry);
                var broken = new EmittedClass("Broken")
                {
                    Tabs = new[] { "orphan" },
                    SnapKeyword = "ORPHAN",
                    Command = "KEEPME",
                    InitializeThrows = "boom",
                };

                CadPluginResult result = LoadEmitted(manager, "Broken", broken);

                Assert.True(result.Succeeded, result.ToString());
                CadPlugin plugin = result.Plugin.Plugin;
                Assert.Equal("KEEPME", Assert.Single(plugin.Commands).Name);
                Assert.NotNull(registry.Find("KEEPME"));
                Assert.True(plugin.Host.IsEmpty);
                Assert.Empty(plugin.Host.RibbonTabs);
                Assert.Empty(plugin.Host.ObjectSnaps);
                string line =
                    "The plugin "
                    + plugin.Name
                    + "'s Initialize failed: boom; its ribbon, Properties, DRAWINGS and snap additions are left out.";
                Assert.Contains(line, plugin.Problems);
                Assert.Contains(line, result.Warnings);
            }
        }

        [Fact]
        public void AnApplicationClassAfterTheOneThatThrewIsNotInitialisedAndTheOneBeforeIsTerminatedAtUnload()
        {
            using (CadPluginManager manager = _files.Manager(true))
            {
                CadPluginResult result = LoadEmitted(
                    manager,
                    "Third",
                    new EmittedClass("A") { Tabs = new[] { "from-a" } },
                    new EmittedClass("B") { InitializeThrows = "stop" },
                    new EmittedClass("C") { Tabs = new[] { "from-c" } }
                );

                Assert.True(result.Succeeded, result.ToString());
                Assert.Equal(new[] { "init A", "init B" }, EmittedPlugin.Lines(Log));
                Assert.Empty(result.Plugin.Plugin.Host.RibbonTabs);

                manager.Unload(result.Plugin.Name);

                Assert.Equal(new[] { "init A", "init B", "term A" }, EmittedPlugin.Lines(Log));
            }
        }

        [Fact]
        public void APluginWithOnlyAnApplicationClassIsNotRefusedForHavingNoCommand()
        {
            Assembly assembly = EmittedPlugin.InMemory(
                EmittedPlugin.UniqueName("Only"),
                "1.1",
                Log,
                new EmittedClass("Only") { Tabs = new[] { "only" } }
            );

            CadPlugin plugin = CadPlugin.FromAssemblies("Only app", new[] { assembly });

            Assert.Empty(plugin.Commands);
            Assert.Equal("only", Assert.Single(plugin.Host.RibbonTabs).Id);
            Assert.Equal(new[] { "init Only" }, EmittedPlugin.Lines(Log));
        }

        [Fact]
        public void APluginWithOnlyAnApplicationClassIsLoadedByTheManagerWithNoCommand()
        {
            using (CadPluginManager manager = _files.Manager(true))
            {
                CadPluginResult result = LoadEmitted(
                    manager,
                    "Bare",
                    new EmittedClass("Bare") { Tabs = new[] { "bare" } }
                );

                Assert.True(result.Succeeded, result.ToString());
                Assert.Empty(result.Plugin.Plugin.Commands);
                Assert.Empty(result.Plugin.Plugin.Problems);
                Assert.Equal("bare", Assert.Single(result.Plugin.Plugin.Host.RibbonTabs).Id);
            }
        }

        [Fact]
        public void APluginMadeFromAssembliesWithNoApplicationClassHasAnEmptyHostThatIsNeverNull()
        {
            CadPlugin plugin = CadPlugin.FromAssemblies("Test tools", new[] { typeof(TestTools).Assembly });

            Assert.NotNull(plugin.Host);
            Assert.True(plugin.Host.IsEmpty);
            Assert.Empty(plugin.Host.RibbonTabs);
            Assert.Empty(plugin.Host.PropertyProviders);
        }

        [Fact]
        public void TheApplicationClassesOfAnAssemblyAreInitialisedInOrdinalOrderAndTerminatedInTheReverseOrder()
        {
            using (CadPluginManager manager = _files.Manager(true))
            {
                // Declared in the order C, A, B: the order of the names is what counts.
                CadPluginResult result = LoadEmitted(
                    manager,
                    "Ordered",
                    new EmittedClass("C"),
                    new EmittedClass("A"),
                    new EmittedClass("B")
                );
                Assert.True(result.Succeeded, result.ToString());
                Assert.Equal(new[] { "init A", "init B", "init C" }, EmittedPlugin.Lines(Log));

                manager.Unload(result.Plugin.Name);

                Assert.Equal(
                    new[] { "init A", "init B", "init C", "term C", "term B", "term A" },
                    EmittedPlugin.Lines(Log)
                );
            }
        }

        [Fact]
        public void TerminateRunsOnceWhenThePluginIsUnloadedAndNotAgainWhenTheManagerIsDisposed()
        {
            CadPluginManager manager = _files.Manager(true);

            try
            {
                CadPluginResult result = LoadEmitted(manager, "Once", new EmittedClass("Once"));
                Assert.Equal(new[] { "init Once" }, EmittedPlugin.Lines(Log));

                Assert.True(manager.Unload(result.Plugin.Name).Succeeded);
                Assert.Equal(new[] { "init Once", "term Once" }, EmittedPlugin.Lines(Log));
            }
            finally
            {
                manager.Dispose();
            }

            Assert.Equal(new[] { "init Once", "term Once" }, EmittedPlugin.Lines(Log));
        }

        [Fact]
        public void ReloadingTerminatesTheOldInstanceThenInitialisesANewOne()
        {
            using (CadPluginManager manager = _files.Manager(true))
            {
                CadPluginResult result = LoadEmitted(
                    manager,
                    "Again",
                    new EmittedClass("Again") { Tabs = new[] { "again" } }
                );

                CadPluginResult reloaded = manager.Reload(result.Plugin.Name);

                Assert.True(reloaded.Succeeded, reloaded.ToString());
                Assert.Equal(new[] { "init Again", "term Again", "init Again" }, EmittedPlugin.Lines(Log));
                Assert.Equal("again", Assert.Single(reloaded.Plugin.Plugin.Host.RibbonTabs).Id);
            }
        }

        [Fact]
        public void DisposingTheManagerTerminatesThePluginsStillLoaded()
        {
            CadPluginManager manager = _files.Manager(true);
            LoadEmitted(manager, "Closing", new EmittedClass("Closing"));
            Assert.Equal(new[] { "init Closing" }, EmittedPlugin.Lines(Log));

            manager.Dispose();

            Assert.Equal(new[] { "init Closing", "term Closing" }, EmittedPlugin.Lines(Log));
        }

        [Fact]
        public void ATerminateThatThrowsDoesNotStopTheUnloadOrTheTerminateOfTheOtherClasses()
        {
            using (CadPluginManager manager = _files.Manager(true))
            {
                // B is terminated first (the reverse order) and throws; A still runs and the unload succeeds.
                CadPluginResult result = LoadEmitted(
                    manager,
                    "Fails",
                    new EmittedClass("A"),
                    new EmittedClass("B") { TerminateThrows = "no" }
                );

                CadPluginResult unloaded = manager.Unload(result.Plugin.Name);

                Assert.True(unloaded.Succeeded, unloaded.ToString());
                Assert.False(result.Plugin.IsLoaded);
                Assert.Equal(new[] { "init A", "init B", "term B", "term A" }, EmittedPlugin.Lines(Log));
            }
        }

        // ---- the version of the plugin API ----

        [Fact]
        public void ThePluginApiThisPresenterHasIsOnePointOne()
        {
            Assert.Equal(new Version(1, 1), CadPlugin.ApiVersion);
        }

        [Theory]
        [InlineData("1.0")]
        [InlineData("1.1")]
        public void AnApplicationPluginThatStatesApiOnePointZeroOrOnePointOneIsAccepted(string api)
        {
            Assembly assembly = EmittedPlugin.InMemory(
                EmittedPlugin.UniqueName("Api"),
                api,
                Log,
                new EmittedClass("Api")
            );

            CadPlugin plugin = CadPlugin.FromAssemblies("Api " + api, new[] { assembly });

            Assert.Equal(Version.Parse(api), plugin.RequiredApiVersion);
        }

        [Fact]
        public void AnApplicationPluginThatStatesApiOnePointTwoIsRefusedWithTheExistingWords()
        {
            Assembly assembly = EmittedPlugin.InMemory(
                EmittedPlugin.UniqueName("Newer"),
                "1.2",
                Log,
                new EmittedClass("Newer")
            );

            CadPluginException refused = Assert.Throws<CadPluginException>(() =>
                CadPlugin.FromAssemblies("Needs one two", new[] { assembly })
            );

            Assert.Equal(
                "The plugin Needs one two needs version 1.2 of the plugin API, and this presenter has 1.1: it needs a newer presenter.",
                refused.Message
            );
            Assert.Empty(EmittedPlugin.Lines(Log));
        }

        // ---- two plugins that want the same thing ----

        [Fact]
        public void ASecondPluginWithTheSameTabIdAndSnapKeywordLosesBothAndSaysSo()
        {
            using (CadPluginManager manager = _files.Manager(true))
            {
                CadPlugin first = LoadSample(manager);

                // "ctop" in small letters is CTOP: a keyword is kept in capitals.
                CadPluginResult result = LoadEmitted(
                    manager,
                    "Late",
                    new EmittedClass("Late") { Tabs = new[] { "circles", "mine" }, SnapKeyword = "ctop" }
                );

                Assert.True(result.Succeeded, result.ToString());
                CadPlugin second = result.Plugin.Plugin;
                Assert.Equal("mine", Assert.Single(second.Host.RibbonTabs).Id);
                Assert.Empty(second.Host.ObjectSnaps);
                Assert.Contains(second.Problems, p =>
                    p.Contains("circles") && p.Contains("left out") && p.Contains(UiSample.Name)
                );
                Assert.Contains(second.Problems, p =>
                    p.Contains("CTOP") && p.Contains("left out") && p.Contains(UiSample.Name)
                );
                Assert.Equal(2, second.Problems.Count);
                Assert.All(second.Problems, p => Assert.Contains(p, result.Warnings));

                // The plugin that came first keeps what it has.
                Assert.Equal(new[] { "circles", "circle" }, first.Host.RibbonTabs.Select(t => t.Id.ToLowerInvariant()));
                Assert.Equal("CTOP", Assert.Single(first.Host.ObjectSnaps).Keyword);
                Assert.Empty(first.Problems);
            }
        }

        [Fact]
        public void WhicheverPluginIsLoadedFirstKeepsTheTabAndTheKeyword()
        {
            using (CadPluginManager manager = _files.Manager(true))
            {
                CadPluginResult early = LoadEmitted(
                    manager,
                    "Early",
                    new EmittedClass("Early") { Tabs = new[] { "circles" }, SnapKeyword = "CTOP" }
                );
                Assert.Equal("circles", Assert.Single(early.Plugin.Plugin.Host.RibbonTabs).Id);

                CadPlugin sample = LoadSample(manager);

                Assert.Equal("circle", Assert.Single(sample.Host.RibbonTabs).Id.ToLowerInvariant());
                Assert.Empty(sample.Host.ObjectSnaps);
                Assert.Equal(2, sample.Problems.Count);
                Assert.Empty(early.Plugin.Plugin.Problems);
                Assert.Single(early.Plugin.Plugin.Host.ObjectSnaps);
            }
        }

        [Fact]
        public void TwoTabsOfOnePluginWithTheSameIdLeaveTheSecondOutAndTheirPluginsOwnIdsDoNotClash()
        {
            using (CadPluginManager manager = _files.Manager(true))
            {
                CadPluginResult result = LoadEmitted(
                    manager,
                    "Twice",
                    new EmittedClass("Twice") { Tabs = new[] { "dup", "dup", "other" }, SnapKeyword = "OWNKEY" }
                );

                CadPlugin plugin = result.Plugin.Plugin;

                Assert.Equal(new[] { "dup", "other" }, plugin.Host.RibbonTabs.Select(t => t.Id));
                Assert.Equal("OWNKEY", Assert.Single(plugin.Host.ObjectSnaps).Keyword);
                Assert.Contains("dup", Assert.Single(plugin.Problems), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void APluginUnloadedGivesItsTabIdAndKeywordBackToTheNextOneLoaded()
        {
            using (CadPluginManager manager = _files.Manager(true))
            {
                CadPluginResult first = LoadEmitted(
                    manager,
                    "Gone",
                    new EmittedClass("Gone") { Tabs = new[] { "shared" }, SnapKeyword = "SHARED" }
                );
                Assert.True(manager.Unload(first.Plugin.Name).Succeeded);

                CadPluginResult second = LoadEmitted(
                    manager,
                    "Back",
                    new EmittedClass("Back") { Tabs = new[] { "shared" }, SnapKeyword = "SHARED" }
                );

                Assert.Empty(second.Plugin.Plugin.Problems);
                Assert.Equal("shared", Assert.Single(second.Plugin.Plugin.Host.RibbonTabs).Id);
                Assert.Single(second.Plugin.Plugin.Host.ObjectSnaps);
            }
        }

        // ---- icons ----

        private static byte[] OnePixelPng() =>
            Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=="
            );

        [Fact]
        public void AnIconFromAFullPathKeepsTheBytesAndTheFileCanBeDeletedAtOnce()
        {
            string path = Path.Combine(_files.Root, "icon.png");
            byte[] bytes = OnePixelPng();
            File.WriteAllBytes(path, bytes);

            CadPluginIcon icon = CadPluginIcon.FromFile(path);
            File.Delete(path);

            Assert.False(File.Exists(path));
            Assert.Null(icon.PathData);
            Assert.Equal(bytes, icon.Image.ToArray());
        }

        [Fact]
        public void AnIconFromARelativePathIsRefusedSayingToCombineWithTheHostFolder()
        {
            ArgumentException refused = Assert.Throws<ArgumentException>(() => CadPluginIcon.FromFile("x.png"));

            Assert.StartsWith(
                "The icon file x.png needs a full path: Path.Combine(host.Folder, ...).",
                refused.Message,
                StringComparison.Ordinal
            );
        }

        [Fact]
        public void AnIconFromBytesTakesACopyOfThemSoChangingTheArrayAfterwardsChangesNothing()
        {
            byte[] bytes = OnePixelPng();
            byte[] before = (byte[])bytes.Clone();

            CadPluginIcon icon = CadPluginIcon.FromImage(bytes);
            bytes[0] = 0;

            Assert.Equal(before, icon.Image.ToArray());
        }

        [Fact]
        public void AnIconFromAResourceKeepsItsBytesAndALeftOutResourceIsRefused()
        {
            byte[] bytes = OnePixelPng();
            string path = EmittedPlugin.WithResource(
                Path.Combine(_files.Root, "res"),
                EmittedPlugin.UniqueName("Res"),
                "Probe.dot.png",
                bytes
            );
            Assembly assembly = Assembly.LoadFrom(path);

            CadPluginIcon icon = CadPluginIcon.FromResource(assembly, "Probe.dot.png");

            Assert.Equal(bytes, icon.Image.ToArray());
            Assert.Throws<ArgumentException>(() => CadPluginIcon.FromResource(assembly, "Probe.none.png"));
        }

        [Fact]
        public void AnIconFromBlankPathDataOrNoBytesIsRefused()
        {
            Assert.Throws<ArgumentException>(() => CadPluginIcon.FromPathData("  "));
            Assert.Throws<ArgumentException>(() => CadPluginIcon.FromImage(new byte[0]));
            Assert.Equal("M4 12 H20", CadPluginIcon.FromPathData("M4 12 H20").PathData);
        }
    }
}

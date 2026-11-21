using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The application class of a plugin that adds to the window (<see cref="ICadPluginApplication"/>): made and initialised
    /// when the plugin is made, terminated once, the host closed after Initialize, the keywords of snaps and the clashes
    /// between plugins. The tests' own assembly is read as the plugin, so that <see cref="UiHook"/> is its application class;
    /// the hooks are static, and the tests of this class run one at a time.
    /// </summary>
    public sealed class PluginApplicationTests : IDisposable
    {
        public PluginApplicationTests()
        {
            UiHook.Reset();
        }

        public void Dispose()
        {
            UiHook.Reset();
        }

        private static CadPlugin Make(string name = "Hooked") =>
            CadPlugin.FromAssemblies(name, new[] { typeof(UiHook).Assembly });

        private static CadCustomSnap Snap(string keyword) =>
            new CadCustomSnap("A snap", keyword, request => Array.Empty<GeoPoint2>());

        [Fact]
        public void TheApplicationClass_IsMadeAndInitialisedOnce_WithAHostThatNamesThePlugin()
        {
            CadPluginHost seen = null;
            UiHook.OnInitialize = host => seen = host;

            CadPlugin plugin = Make("Hooked tools");

            Assert.Equal(1, UiHook.Initialized);
            Assert.Same(plugin.Host, seen);
            Assert.Equal("Hooked tools", plugin.Host.PluginName);
            Assert.True(plugin.Host.IsEmpty);
            Assert.True(plugin.Host.ShowsCommandButtons);
            Assert.Equal(new Version(1, 1), CadPlugin.ApiVersion);
        }

        [Fact]
        public void WhatInitializeAdds_IsReadFromTheHostInTheOrderAdded()
        {
            var tab = new CadRibbonTab("Hooked", "Hooked");
            var panel = new CadRibbonPanel("Panel");
            var button = new CadPluginButton(CadPluginIcon.FromPathData("M0 0 L1 1"), "Go", c => { });
            CadPropertyProvider provider = r => null;
            CadDrawingsMenuProvider drawings = r => null;
            CadCanvasMenuProvider canvas = c => null;
            CadCustomSnap snap = Snap("HOOK");
            UiHook.OnInitialize = host =>
            {
                host.AddRibbonTab(tab);
                host.AddRibbonPanel(panel);
                host.AddProperties(provider);
                host.AddDrawingsButton(button);
                host.AddDrawingsMenu(drawings);
                host.AddCanvasMenu(canvas);
                host.AddObjectSnap(snap);
                host.ShowsCommandButtons = false;
            };

            CadPluginHost made = Make().Host;

            Assert.Same(tab, Assert.Single(made.RibbonTabs));
            Assert.Same(panel, Assert.Single(made.RibbonPanels));
            Assert.Same(provider, Assert.Single(made.PropertyProviders));
            Assert.Same(button, Assert.Single(made.DrawingsButtons));
            Assert.Same(drawings, Assert.Single(made.DrawingsMenus));
            Assert.Same(canvas, Assert.Single(made.CanvasMenus));
            Assert.Same(snap, Assert.Single(made.ObjectSnaps));
            Assert.False(made.ShowsCommandButtons);
            Assert.False(made.IsEmpty);
        }

        [Fact]
        public void AddingAfterInitializeHasReturned_Throws()
        {
            CadPluginHost seen = null;
            UiHook.OnInitialize = host => seen = host;
            Make("Late");

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                seen.AddRibbonTab(new CadRibbonTab("Late", "Late"))
            );

            Assert.Equal("The plugin Late adds to the window only in Initialize.", error.Message);
            Assert.Throws<InvalidOperationException>(() => seen.AddObjectSnap(Snap("LATE")));
        }

        [Fact]
        public void AnInitializeThatThrows_DropsEveryAdditionAndSaysSoInTheProblems()
        {
            UiHook.OnInitialize = host =>
            {
                host.AddRibbonTab(new CadRibbonTab("Half", "Half"));
                host.AddObjectSnap(Snap("HALF"));
                throw new InvalidOperationException("boom");
            };

            CadPlugin plugin = Make("Broken");

            Assert.True(plugin.Host.IsEmpty);
            Assert.Contains(
                "The plugin Broken's Initialize failed: boom; its ribbon, Properties, DRAWINGS and snap additions are left out.",
                plugin.Problems
            );
            Assert.True(plugin.IsEnabled);
        }

        [Fact]
        public void Terminate_RunsOnceWhenThePluginIsSwitchedOff()
        {
            CadPlugin plugin = Make();

            plugin.Disable();
            plugin.Disable();

            Assert.Equal(1, UiHook.Terminated);
            Assert.False(plugin.IsEnabled);
        }

        [Fact]
        public void ATerminateThatThrows_DoesNotStopTheUnload()
        {
            UiHook.OnTerminate = () => throw new InvalidOperationException("late boom");
            CadPlugin plugin = Make();

            plugin.Disable();

            Assert.False(plugin.IsEnabled);
            Assert.Equal(1, UiHook.Terminated);
        }

        [Fact]
        public void AnAssemblyWithOnlyAnApplicationClass_IsAPlugin()
        {
            Assembly assembly = EmptyApplication();

            CadPlugin plugin = CadPlugin.FromAssemblies("Only the window", new[] { assembly });

            Assert.Empty(plugin.Commands);
            Assert.True(plugin.HasApplications);
            Assert.Null(plugin.Host.Folder);
        }

        [Fact]
        public void AnAssemblyWithNeitherACommandNorAnApplicationClass_IsStillRefused()
        {
            Assembly assembly = typeof(GeoPoint2).Assembly;

            Assert.Throws<CadPluginException>(() => CadPlugin.FromAssemblies("Nothing", new[] { assembly }));
        }

        [Theory]
        [InlineData("CTOP")]
        [InlineData("ctop")]
        [InlineData("BOLT2")]
        [InlineData("TTX")]
        [InlineData("CT")]
        [InlineData("ABCDEFGHIJKLMNOP")]
        public void AGoodKeyword_IsAddedInCapitals(string keyword)
        {
            CadPluginHost host = new CadPluginHost("X", null);

            host.AddObjectSnap(Snap(keyword));

            Assert.Equal(keyword.ToUpperInvariant(), Assert.Single(host.ObjectSnaps).Keyword);
        }

        [Theory]
        [InlineData("CENTRE")]
        [InlineData("END")]
        [InlineData("end")]
        [InlineData("ENDX")]
        [InlineData("NONE")]
        [InlineData("TT")]
        [InlineData("FROM")]
        [InlineData("M2P")]
        [InlineData("MTP")]
        [InlineData("C T")]
        [InlineData("CT-OP")]
        [InlineData("A")]
        [InlineData("ABCDEFGHIJKLMNOPQ")]
        public void ABadKeyword_IsRefused(string keyword)
        {
            CadPluginHost host = new CadPluginHost("X", null);

            Assert.Throws<ArgumentException>(() => host.AddObjectSnap(Snap(keyword)));
            Assert.Empty(host.ObjectSnaps);
        }

        [Fact]
        public void AnEntityTypeThatIsNotAnEntity_IsRefused()
        {
            CadPluginHost host = new CadPluginHost("X", null);
            CadCustomSnap snap = Snap("ODDD");
            snap.EntityTypes.Add(typeof(string));

            Assert.Throws<ArgumentException>(() => host.AddObjectSnap(snap));

            snap.EntityTypes.Clear();
            snap.EntityTypes.Add(typeof(DxfCircle));
            host.AddObjectSnap(snap);
            Assert.Single(host.ObjectSnaps);
        }

        [Fact]
        public void ASecondPluginWithTheSameTabIdOrSnapKeyword_LosesThemWithAProblemLine()
        {
            UiHook.OnInitialize = host =>
            {
                host.AddRibbonTab(new CadRibbonTab("Circles", "Circles"));
                host.AddRibbonTab(new CadRibbonTab("Other", "Other"));
                host.AddObjectSnap(Snap("CTOP"));
            };
            CadPlugin first = Make("First");
            UiHook.OnInitialize = host =>
            {
                host.AddRibbonTab(new CadRibbonTab("CIRCLES", "Circles again"));
                host.AddRibbonTab(new CadRibbonTab("Mine", "Mine"));
                host.AddObjectSnap(Snap("ctop"));
                host.AddObjectSnap(Snap("MINE"));
            };
            CadPlugin second = Make("Second");

            second.LeaveOutClashes(new[] { first });

            Assert.Single(first.Host.RibbonTabs, t => t.Id == "Circles");
            Assert.Equal(new[] { "Mine" }, new List<string>(Ids(second.Host.RibbonTabs)));
            Assert.Equal("MINE", Assert.Single(second.Host.ObjectSnaps).Keyword);
            Assert.Contains(
                "The ribbon tab CIRCLES of the plugin Second is left out: the plugin First has a tab of that id.",
                second.Problems
            );
            Assert.Contains(
                "The snap CTOP of the plugin Second is left out: the plugin First has a snap of that keyword.",
                second.Problems
            );
        }

        [Fact]
        public void TwoTabsOfOnePluginWithTheSameId_KeepTheFirst()
        {
            UiHook.OnInitialize = host =>
            {
                host.AddRibbonTab(new CadRibbonTab("Same", "One"));
                host.AddRibbonTab(new CadRibbonTab("same", "Two"));
                host.AddObjectSnap(Snap("SAME"));
                host.AddObjectSnap(Snap("SAME"));
            };
            CadPlugin plugin = Make();

            plugin.LeaveOutClashes(null);

            Assert.Equal("One", Assert.Single(plugin.Host.RibbonTabs).Title);
            Assert.Single(plugin.Host.ObjectSnaps);
            Assert.Equal(2, plugin.Problems.Count);
        }

        [Fact]
        public void AnIconFromBytes_KeepsACopy_AndOneFromAResourceOrAFileOrPathData_ReadsRight()
        {
            byte[] bytes = { 1, 2, 3 };
            CadPluginIcon icon = CadPluginIcon.FromImage(bytes);
            bytes[0] = 9;

            Assert.Equal(new byte[] { 1, 2, 3 }, icon.Image);
            Assert.Null(icon.PathData);
            Assert.Equal("M0 0 L1 1", CadPluginIcon.FromPathData("M0 0 L1 1").PathData);
            Assert.Null(CadPluginIcon.FromPathData("M0 0 L1 1").Image);
            Assert.Throws<ArgumentException>(() => CadPluginIcon.FromImage(new byte[0]));
            Assert.Throws<ArgumentException>(() => CadPluginIcon.FromPathData(" "));
            Assert.Throws<ArgumentException>(() => CadPluginIcon.FromResource(typeof(UiHook).Assembly, "no.such.png"));

            ArgumentException relative = Assert.Throws<ArgumentException>(() => CadPluginIcon.FromFile("x.png"));
            Assert.StartsWith("The icon file x.png needs a full path", relative.Message);

            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "gh-icon-" + Guid.NewGuid().ToString("N") + ".png"
            );
            System.IO.File.WriteAllBytes(path, new byte[] { 5, 6, 7, 8 });
            CadPluginIcon fromFile = CadPluginIcon.FromFile(path);
            System.IO.File.Delete(path);

            Assert.Equal(new byte[] { 5, 6, 7, 8 }, fromFile.Image);
            Assert.False(System.IO.File.Exists(path));
        }

        [Fact]
        public void TheRibbonDescriptions_KeepTheirDefaultsAndRefuseWhatCannotBe()
        {
            var button = new CadRibbonButton("Go", "NUMCIRCLES");
            var callback = new CadRibbonButton("Run", c => { });
            var split = new CadRibbonSplitButton("Sizes", new[] { button, callback });

            Assert.Equal("NUMCIRCLES", button.Command);
            Assert.Null(button.Run);
            Assert.Null(callback.Command);
            Assert.NotNull(callback.Run);
            Assert.Equal(CadRibbonItemSize.Standard, button.Size);
            Assert.True(button.ShowText);
            Assert.True(split.RemembersLast);
            Assert.Equal(new[] { button, callback }, split.Buttons);
            Assert.Throws<ArgumentException>(() => new CadRibbonSplitButton("None", new CadRibbonButton[0]));
            Assert.Throws<ArgumentException>(() => new CadRibbonButton("Go", " "));
            Assert.Throws<ArgumentException>(() => new CadRibbonTab(" ", "Title"));
            Assert.Null(new CadRibbonTab("A", "A").ShowWhen);
        }

        [Fact]
        public void AMenuItem_IsARunLineASubmenuOrASeparator()
        {
            var run = new CadPluginMenuItem("Run", c => { }, false, true);
            var sub = new CadPluginMenuItem("More", new[] { run });
            CadPluginMenuItem line = CadPluginMenuItem.Separator();

            Assert.False(run.IsEnabled);
            Assert.True(run.IsChecked.Value);
            Assert.Empty(run.Items);
            Assert.Null(sub.Run);
            Assert.Same(run, Assert.Single(sub.Items));
            Assert.True(line.IsSeparator);
            Assert.False(run.IsSeparator);
            Assert.Null(line.Run);
        }

        private static IEnumerable<string> Ids(IEnumerable<CadRibbonTab> tabs)
        {
            foreach (CadRibbonTab tab in tabs)
            {
                yield return tab.Id;
            }
        }

        /// <summary>A dynamic assembly whose one public class implements the application interface and does nothing.</summary>
        private static Assembly EmptyApplication()
        {
            AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("OnlyApplication" + Guid.NewGuid().ToString("N")),
                AssemblyBuilderAccess.Run
            );
            ModuleBuilder module = assembly.DefineDynamicModule("main");
            TypeBuilder type = module.DefineType(
                "OnlyApplication.Entry",
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(object),
                new[] { typeof(ICadPluginApplication) }
            );
            type.DefineDefaultConstructor(MethodAttributes.Public);
            const MethodAttributes body =
                MethodAttributes.Public
                | MethodAttributes.Virtual
                | MethodAttributes.Final
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot;

            foreach (string name in new[] { "Initialize", "Terminate" })
            {
                MethodBuilder method = type.DefineMethod(
                    name,
                    body,
                    typeof(void),
                    name == "Initialize" ? new[] { typeof(CadPluginHost) } : Type.EmptyTypes
                );
                method.GetILGenerator().Emit(OpCodes.Ret);
            }

            type.CreateType();
            return assembly;
        }
    }

    /// <summary>The application class of the tests' assembly: what it does is set by the test that runs.</summary>
    public sealed class UiHook : ICadPluginApplication
    {
        /// <summary>Gets or sets what Initialize does besides counting.</summary>
        public static Action<CadPluginHost> OnInitialize { get; set; }

        /// <summary>Gets or sets what Terminate does besides counting.</summary>
        public static Action OnTerminate { get; set; }

        /// <summary>Gets how many times Initialize ran since the last reset.</summary>
        public static int Initialized { get; private set; }

        /// <summary>Gets how many times Terminate ran since the last reset.</summary>
        public static int Terminated { get; private set; }

        /// <summary>Puts the counts and the hooks back.</summary>
        public static void Reset()
        {
            OnInitialize = null;
            OnTerminate = null;
            Initialized = 0;
            Terminated = 0;
        }

        /// <inheritdoc/>
        public void Initialize(CadPluginHost host)
        {
            Initialized++;
            OnInitialize?.Invoke(host);
        }

        /// <inheritdoc/>
        public void Terminate()
        {
            Terminated++;
            OnTerminate?.Invoke();
        }
    }
}

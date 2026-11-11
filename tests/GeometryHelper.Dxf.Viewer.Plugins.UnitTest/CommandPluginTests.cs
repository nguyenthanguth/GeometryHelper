using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer.Plugins;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Plugins.UnitTest
{
    public class CommandPluginTests
    {
        private static CadPlugin TestPlugin() =>
            CadPlugin.FromAssemblies("Test tools", new[] { typeof(TestTools).Assembly });

        [Fact]
        public void APluginsCommandsAreTheMethodsMarkedAsCommandsInOrdinalOrderOfTheirNames()
        {
            CadPlugin plugin = TestPlugin();

            Assert.Equal(
                new[] { "DRAWLINE", "LAYERTHENFAIL", "MOVE", "STATICCMD", "TESTALIAS" },
                plugin.Commands.Select(c => c.Name)
            );
            Assert.Equal("DL", plugin.Commands[0].Alias);
            Assert.Equal("Draw a line through two points", plugin.Commands[0].Description);
            Assert.Equal("Test tools", plugin.Name);
            Assert.Equal(new Version(1, 0), plugin.RequiredApiVersion);
            Assert.True(plugin.IsEnabled);
        }

        [Fact]
        public void AMethodThatCannotBeACommandIsLeftOutSayingWhy()
        {
            CadPlugin plugin = TestPlugin();

            Assert.Contains(plugin.Problems, p =>
                p.StartsWith("The command VOIDCMD", StringComparison.Ordinal)
                && p.Contains("returns void")
                && p.Contains("iterator")
            );
            Assert.Contains(plugin.Problems, p =>
                p.StartsWith("The command BADARGS", StringComparison.Ordinal) && p.Contains("one CadCommandContext")
            );
            Assert.Contains(plugin.Problems, p =>
                p.StartsWith("The command DRAWLINE", StringComparison.Ordinal)
                && p.Contains("DrawLineAgain")
                && p.Contains("already")
            );
            Assert.Contains(plugin.Problems, p =>
                p.StartsWith("The command HIDDEN", StringComparison.Ordinal) && p.Contains("not public")
            );
            Assert.Contains(plugin.Problems, p => p.StartsWith("The setting When", StringComparison.Ordinal));
        }

        [Fact]
        public void ACommandThatAsksTwoPointsDrawsALineInOneUndoStep()
        {
            CadDocument document = PluginFiles.Circles();
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();
            TestPlugin().AddTo(registry);
            CadCommandSession session = PluginFiles.Session(document, registry);

            session.Feed("DL");
            Assert.Equal("Specify first point:", session.PromptText);
            session.Feed(PluginFiles.P(0, 100));
            Assert.Equal("Specify second point:", session.PromptText);
            session.Feed(PluginFiles.P(30, 140));

            Assert.Equal(CadCommandOutcome.Completed, session.LastOutcome);
            DxfLine line = Assert.Single(document.ModelSpace.Block.OfType<DxfLine>(), l => l.Handle != "L1");
            Assert.True(line.StartPoint.DistanceTo(new Geometry.GeoPoint3(0, 100, 0)) <= 1E-9);
            Assert.True(line.EndPoint.DistanceTo(new Geometry.GeoPoint3(30, 140, 0)) <= 1E-9);
            Assert.Equal("DRAWLINE", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.Single(document.ModelSpace.Block.OfType<DxfLine>());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void AnEditMadeThroughTheDocumentsOwnMethodsIsTakenBackWhenTheRunFails()
        {
            CadDocument document = PluginFiles.Circles();
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();
            TestPlugin().AddTo(registry);
            CadCommandSession session = PluginFiles.Session(document, registry);

            session.Feed("LAYERTHENFAIL");
            Assert.True(document.Database.LayerTable.Has("Temp"));
            session.Feed("now");

            Assert.Equal(CadCommandOutcome.Failed, session.LastOutcome);
            Assert.Contains(
                "*Error* LAYERTHENFAIL of the plugin Test tools failed: Failed after a layer was added.",
                session.History
            );
            Assert.False(document.Database.LayerTable.Has("Temp"));
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void AStaticCommandRunsWithoutAnInstance()
        {
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();
            TestPlugin().AddTo(registry);
            CadCommandSession session = PluginFiles.Session(PluginFiles.Circles(), registry);

            session.Feed("STATICCMD");

            Assert.Equal(CadCommandOutcome.Completed, session.LastOutcome);
            Assert.Contains("STATICCMD ran.", session.History);
        }

        [Fact]
        public void ANameOrAnAliasThePresenterHasIsLeftOutSayingSoAndTheRestIsAdded()
        {
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();
            CadCommand move = registry.Find("MOVE");

            var refused = TestPlugin().AddTo(registry);

            Assert.Equal(2, refused.Count);
            Assert.Contains(refused, r =>
                r.StartsWith("The command MOVE of the plugin Test tools is not added", StringComparison.Ordinal)
            );
            Assert.Contains(refused, r =>
                r.StartsWith("The alias M of the command TESTALIAS is not added", StringComparison.Ordinal)
                && r.Contains("MOVE")
            );
            Assert.Same(move, registry.Find("MOVE"));
            Assert.Same(move, registry.Find("M"));
            Assert.NotNull(registry.Find("TESTALIAS"));
            Assert.Equal("DRAWLINE", registry.Find("dl").Name);
        }

        [Fact]
        public void RemovingAPluginTakesItsCommandsAndAliasesOutAndLeavesThePresentersOwn()
        {
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();
            int commands = registry.Commands.Count;
            CadPlugin plugin = TestPlugin();
            plugin.AddTo(registry);

            Assert.True(plugin.RemoveFrom(registry));

            Assert.Null(registry.Find("DRAWLINE"));
            Assert.Null(registry.Find("DL"));
            Assert.Null(registry.Find("TESTALIAS"));
            Assert.Equal(commands, registry.Commands.Count);
            Assert.Equal(CadShortcuts.Default.Aliases.Keys, registry.Shortcuts.Aliases.Keys);
            Assert.False(plugin.RemoveFrom(registry));
        }

        [Fact]
        public void APluginThatNeedsANewerApiIsRefusedSayingWhichItNeedsAndWhichThePresenterHas()
        {
            AssemblyBuilder assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName("NeedsApiTwo"),
                AssemblyBuilderAccess.Run
            );
            assembly.SetCustomAttribute(
                new CustomAttributeBuilder(
                    typeof(CadPluginApiAttribute).GetConstructor(new[] { typeof(string) }),
                    new object[] { "2.0" }
                )
            );

            CadPluginException refused = Assert.Throws<CadPluginException>(() =>
                CadPlugin.FromAssemblies("Needs two", new Assembly[] { assembly })
            );

            Assert.Equal("Needs two", refused.PluginName);
            Assert.Equal(
                "The plugin Needs two needs version 2.0 of the plugin API, and this presenter has 1.0: it needs a newer presenter.",
                refused.Message
            );
        }

        [Fact]
        public void AnAssemblyWithNoCommandIsRefused()
        {
            CadPluginException refused = Assert.Throws<CadPluginException>(() =>
                CadPlugin.FromAssemblies(null, new[] { typeof(Xunit.FactAttribute).Assembly })
            );

            Assert.StartsWith("The plugin xunit.core has no command", refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ASettingIsDeclaredWithItsLabelKindDefaultAndRange()
        {
            CadPlugin plugin = TestPlugin();

            Assert.Equal(new[] { "Offset", "Count", "Show", "Kind" }, plugin.Settings.Select(s => s.Label));
            CadPluginSetting offset = plugin.Settings[0];
            Assert.Equal(CadSettingKind.Number, offset.Kind);
            Assert.Equal(1.5, offset.Default);
            Assert.Equal(0.0, offset.Minimum);
            Assert.Equal(10.0, offset.Maximum);
            Assert.Equal("How far.", offset.Description);
            Assert.Equal(CadSettingKind.Integer, plugin.Settings[1].Kind);
            Assert.Equal(CadSettingKind.Boolean, plugin.Settings[2].Kind);
            Assert.Equal(CadSettingKind.Choice, plugin.Settings[3].Kind);
            Assert.Equal("Second", plugin.Settings[3].Default);
            Assert.Equal(new[] { "First", "Second" }, plugin.Settings[3].Choices);
        }

        [Fact]
        public void ASettingTakesAValueInItsRangeAndRefusesOneOutsideKeepingWhatItHad()
        {
            CadPlugin plugin = TestPlugin();
            CadPluginSetting offset = plugin.Settings[0];
            CadPluginSetting count = plugin.Settings[1];

            offset.Value = "2.5";
            Assert.Throws<ArgumentOutOfRangeException>(() => offset.Value = 10.5);
            Assert.Throws<ArgumentException>(() => count.Value = 2.5);
            count.Value = 4;
            plugin.Settings[2].Value = "false";
            plugin.Settings[3].Value = "first";

            Assert.Equal(2.5, offset.Value);
            Assert.Equal(4, count.Value);
            Assert.False((bool)plugin.Settings[2].Value);
            Assert.Equal("First", plugin.Settings[3].Value);
            Assert.Throws<ArgumentException>(() => plugin.Settings[3].Value = "Third");
        }
    }
}

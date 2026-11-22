using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GeometryHelper.Cad.Plugins;
using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// What a .py file adds to the window, as the map of D1660 and the rules of D1661 fix it: the file's own
    /// <see cref="CadPluginHost"/> holds, after the load, what the module-level calls added; a call after the load raises; a
    /// reload replaces the host and an unload drops it; a callback that raises counts as false or as no items and writes one
    /// line; a callback is given a Context. Each test loads a file of its own (the host is shared by the process, so the file
    /// names are made up, and the host of a file is found by its name).
    /// </summary>
    public sealed class PluginParityTests : PythonHostTestBase
    {
        public PluginParityTests(PythonHostFixture fixture)
            : base(fixture) { }

        /// <summary>The import line of every script of this class: the names D1660 maps.</summary>
        private const string Imports =
            "from geometryhelper.cad import (add_ribbon_tab, ribbon_tab, add_ribbon_panel, ribbon_panel, ribbon_button, "
            + "split_button, icon_from_path_data, icon_from_image, icon_from_file, add_drawings_button, add_drawings_menu, "
            + "add_canvas_menu, menu_item, separator, add_properties, show_command_buttons, command)";

        private IEnumerable<CadPluginHost> HostsOf(string path) =>
            Host.PluginHosts.Where(h =>
                h.PluginName.IndexOf(Path.GetFileNameWithoutExtension(path), StringComparison.OrdinalIgnoreCase) >= 0
            );

        private CadPluginHost HostOf(string path) => Assert.Single(HostsOf(path));

        /// <summary>Loads a file whose lines follow <see cref="Imports"/> and gives the file's host.</summary>
        private CadPluginHost Loaded(CadCommandSession session, string name, out string path, params string[] lines)
        {
            path = Script(name, new[] { Imports }.Concat(lines).ToArray());
            CadPythonRunEventArgs run = Load(session, path);
            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            return HostOf(path);
        }

        /// <summary>The lines of the command line and the palette that begin with "Python error:", each once.</summary>
        private string[] ErrorLines(CadCommandSession session) =>
            Errors
                .Concat(session.History)
                .Where(line => line.StartsWith("Python error:", StringComparison.Ordinal))
                .Distinct()
                .ToArray();

        /// <summary>What a window gives a callback: the constructors are internal to the library, so they are called by reflection.</summary>
        private static T Made<T>(params object[] arguments) =>
            (T)typeof(T).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single().Invoke(arguments);

        private static CadPluginContext ContextOf(
            CadDocument document,
            string[] selected,
            Action<string> write = null,
            Action<string> runCommand = null
        ) => Made<CadPluginContext>(document, selected, false, write, runCommand);

        #region What the host holds

        [Fact]
        public void AfterTheLoad_TheFilesHostHoldsItsTab_ItsPanel_AndItsTwoButtons()
        {
            CadCommandSession session = NewSession();

            CadPluginHost host = Loaded(
                session,
                "h2p-tab.py",
                out _,
                "draw = ribbon_panel('Draw', [ribbon_button('Circle', command='CIRCLE'), ribbon_button('Hello', run=lambda c: None)])",
                "add_ribbon_tab(ribbon_tab('h2tab', 'Tools', [draw]))"
            );

            CadRibbonTab tab = Assert.Single(host.RibbonTabs);
            Assert.Equal("h2tab", tab.Id);
            Assert.Equal("Tools", tab.Title);
            Assert.Null(tab.ShowWhen);
            CadRibbonPanel panel = Assert.Single(tab.Panels);
            Assert.Equal("Draw", panel.Title);
            List<CadRibbonButton> buttons = panel.Items.OfType<CadRibbonButton>().ToList();
            Assert.Equal(new[] { "Circle", "Hello" }, buttons.Select(b => b.Text));
            Assert.Equal("CIRCLE", buttons[0].Command);
            Assert.Null(buttons[1].Command);
            Assert.NotNull(buttons[1].Run);
        }

        [Fact]
        public void APanelAddedOnItsOwn_IsOnThePluginsTab()
        {
            CadCommandSession session = NewSession();

            CadPluginHost host = Loaded(
                session,
                "h2p-panel.py",
                out _,
                "add_ribbon_panel(ribbon_panel('Extras', [ribbon_button('Line', command='LINE')]))"
            );

            Assert.Empty(host.RibbonTabs);
            CadRibbonPanel panel = Assert.Single(host.RibbonPanels);
            Assert.Equal("Extras", panel.Title);
            Assert.Equal("LINE", Assert.IsType<CadRibbonButton>(Assert.Single(panel.Items)).Command);
        }

        [Fact]
        public void AButtonsOptions_ReachTheRibbonButton()
        {
            CadCommandSession session = NewSession();

            CadPluginHost host = Loaded(
                session,
                "h2p-options.py",
                out _,
                "big = ribbon_button('Big', command='LINE', icon=icon_from_path_data('M4 12 H20 M12 4 V20'), size='large',"
                    + " show_text=False, tooltip='Draws a line', description='A longer text', can_run=lambda c: True)",
                "add_ribbon_panel(ribbon_panel('Options', [big]))"
            );

            var button = Assert.IsType<CadRibbonButton>(Assert.Single(Assert.Single(host.RibbonPanels).Items));
            Assert.Equal(CadRibbonItemSize.Large, button.Size);
            Assert.False(button.ShowText);
            Assert.Equal("Draws a line", button.ToolTip);
            Assert.Equal("A longer text", button.Description);
            Assert.Equal("M4 12 H20 M12 4 V20", button.Icon.PathData);
            Assert.True(button.CanRun(ContextOf(Blank(), new string[0])));
        }

        [Fact]
        public void TheThreeKindsOfIcon_ReachTheirButtons()
        {
            CadCommandSession session = NewSession();
            string icon = Path.Combine(Folder, "icon.png");
            File.WriteAllBytes(icon, new byte[] { 7, 8, 9 });

            CadPluginHost host = Loaded(
                session,
                "h2p-icons.py",
                out _,
                "add_ribbon_panel(ribbon_panel('Icons', [",
                "    ribbon_button('A', command='LINE', icon=icon_from_path_data('M0 0 L24 24')),",
                "    ribbon_button('B', command='LINE', icon=icon_from_image(bytes([1, 2, 3]))),",
                "    ribbon_button('C', command='LINE', icon=icon_from_file(" + Raw(icon) + ")),",
                "]))"
            );

            List<CadRibbonButton> buttons = Assert.Single(host.RibbonPanels).Items.OfType<CadRibbonButton>().ToList();
            Assert.Equal("M0 0 L24 24", buttons[0].Icon.PathData);
            Assert.Equal(new byte[] { 1, 2, 3 }, buttons[1].Icon.Image);
            Assert.Equal(new byte[] { 7, 8, 9 }, buttons[2].Icon.Image);
        }

        [Fact]
        public void ASplitButton_HoldsItsButtons_AndRemembersTheLastOneUnlessToldNot()
        {
            CadCommandSession session = NewSession();

            CadPluginHost host = Loaded(
                session,
                "h2p-split.py",
                out _,
                "one = ribbon_button('One', command='LINE')",
                "two = ribbon_button('Two', command='CIRCLE')",
                "add_ribbon_panel(ribbon_panel('Splits', [split_button('More', [one, two]), split_button('Fixed', [one], remembers_last=False)]))"
            );

            List<CadRibbonSplitButton> splits = Assert
                .Single(host.RibbonPanels)
                .Items.OfType<CadRibbonSplitButton>()
                .ToList();
            Assert.Equal(new[] { "More", "Fixed" }, splits.Select(s => s.Text));
            Assert.Equal(new[] { "One", "Two" }, splits[0].Buttons.Select(b => b.Text));
            Assert.True(splits[0].RemembersLast);
            Assert.False(splits[1].RemembersLast);
        }

        [Fact]
        public void ADrawingsButton_HoldsItsToolTipIconAndCallbacks()
        {
            CadCommandSession session = NewSession();
            CadPluginHost host = Loaded(
                session,
                "h2p-drawings-button.py",
                out _,
                "add_drawings_button(icon_from_path_data('M4 12 H20'), 'Check drawing', lambda c: c.write('checked'), can_run=lambda c: False)"
            );
            var written = new List<string>();

            CadPluginButton button = Assert.Single(host.DrawingsButtons);

            Assert.Equal("Check drawing", button.ToolTip);
            Assert.Equal("M4 12 H20", button.Icon.PathData);
            button.Run(ContextOf(Blank(), new string[0], written.Add));
            Assert.Equal(new[] { "checked" }, written);
            Assert.False(button.CanRun(ContextOf(Blank(), new string[0])));
        }

        [Fact]
        public void ADrawingsMenu_IsGivenTheRowAndItsDrawing_AndItsItemsAreTheProvidersList()
        {
            CadCommandSession session = NewSession();
            CadPluginHost host = Loaded(
                session,
                "h2p-drawings-menu.py",
                out _,
                "def items(request):",
                "    name = None if request.drawing is None else request.drawing.name",
                "    return [menu_item(f'{request.path}|{name}|{len(request.selected_paths)}', run=lambda c: None)]",
                "add_drawings_menu(items)"
            );
            CadDrawingsMenuProvider provider = Assert.Single(host.DrawingsMenus);

            IReadOnlyList<CadPluginMenuItem> closed = provider(
                Made<CadDrawingsRequest>(@"C:\plans\a.dxf", null, new[] { @"C:\plans\a.dxf", @"C:\plans\b.dxf" })
            );
            IReadOnlyList<CadPluginMenuItem> open = provider(
                Made<CadDrawingsRequest>(@"C:\plans\c.dxf", Blank("c.dxf"), new[] { @"C:\plans\c.dxf" })
            );

            Assert.Equal(@"C:\plans\a.dxf|None|2", Assert.Single(closed).Text);
            Assert.Equal(@"C:\plans\c.dxf|c.dxf|1", Assert.Single(open).Text);
        }

        [Fact]
        public void ACanvasMenu_GivesItsItems_ASeparator_AndASubmenu()
        {
            CadCommandSession session = NewSession();
            CadPluginHost host = Loaded(
                session,
                "h2p-canvas-menu.py",
                out _,
                "def items(context):",
                "    return [",
                "        menu_item(f'Selected {len(context.selected)}', run=lambda c: None, checked=True),",
                "        separator(),",
                "        menu_item('Sub', items=[menu_item('Inner', run=lambda c: None)], enabled=False),",
                "    ]",
                "add_canvas_menu(items)"
            );
            CadDocument document = Blank();
            string handle = AddLine(document, 0).Handle;

            IReadOnlyList<CadPluginMenuItem> items = Assert.Single(host.CanvasMenus)(
                ContextOf(document, new[] { handle })
            );

            Assert.Equal(3, items.Count);
            Assert.Equal("Selected 1", items[0].Text);
            Assert.True(items[0].IsChecked);
            Assert.True(items[1].IsSeparator);
            Assert.Equal("Sub", items[2].Text);
            Assert.False(items[2].IsEnabled);
            Assert.Equal("Inner", Assert.Single(items[2].Items).Text);
        }

        [Fact]
        public void APropertiesProvider_IsGivenTheNetRequest_AndWhatItReturnsIsTheNetInfoAsItIs()
        {
            CadCommandSession session = NewSession();
            CadPluginHost host = Loaded(
                session,
                "h2p-properties.py",
                out _,
                "def rows(request):",
                "    request.Banner = 'from python'",
                "    return request.Shown",
                "add_properties(rows)"
            );
            var shown = new CadSelectionInfo("Line (1)", new CadPropertyGroup[0]);
            var request = Made<CadPropertyRequest>(Blank(), new string[0], shown, null);

            CadSelectionInfo result = Assert.Single(host.PropertyProviders)(request);

            Assert.Same(shown, result);
            Assert.Equal("from python", request.Banner);
        }

        [Fact]
        public void ShowCommandButtonsFalse_ReachesTheHost()
        {
            CadCommandSession session = NewSession();

            CadPluginHost host = Loaded(session, "h2p-buttons.py", out _, "show_command_buttons(False)");

            Assert.False(host.ShowsCommandButtons);
        }

        #endregion

        #region The life of the host

        [Fact]
        public void ACallToAddSomethingAfterTheLoad_RaisesARuntimeError_AndAddsNothing()
        {
            CadCommandSession session = NewSession();
            CadPluginHost host = Loaded(
                session,
                "h2p-late.py",
                out _,
                "@command('H2LATE')",
                "def late():",
                "    try:",
                "        add_ribbon_panel(ribbon_panel('Late', [ribbon_button('X', command='LINE')]))",
                "    except RuntimeError as e:",
                "        print(type(e).__name__)"
            );

            Owner.Session = session;
            Owner.Answer();
            Assert.True(session.Feed("H2LATE"));
            Owner.Drain();

            Assert.Equal(new[] { "RuntimeError" }, Printed);
            Assert.Empty(host.RibbonPanels);
        }

        [Fact]
        public void LoadingAFileAgain_ReplacesItsHost_AndUnloadingItDropsTheHost()
        {
            CadCommandSession session = NewSession();
            CadPluginHost first = Loaded(
                session,
                "h2p-replace.py",
                out string path,
                "add_ribbon_tab(ribbon_tab('h2r1', 'First', [ribbon_panel('P', [ribbon_button('X', command='LINE')])]))"
            );
            Assert.Equal("h2r1", Assert.Single(first.RibbonTabs).Id);

            Loaded(
                session,
                "h2p-replace.py",
                out _,
                "add_ribbon_tab(ribbon_tab('h2r2', 'Second', [ribbon_panel('P', [ribbon_button('X', command='LINE')])]))"
            );

            CadPluginHost second = HostOf(path);
            Assert.NotSame(first, second);
            Assert.Equal("h2r2", Assert.Single(second.RibbonTabs).Id);

            Assert.True(Host.UnloadFile(path));

            Assert.Empty(HostsOf(path));
        }

        [Fact]
        public void TwoFiles_HaveTheirHostsInTheOrderTheyLoaded()
        {
            CadCommandSession session = NewSession();
            Loaded(session, "h2p-order-b.py", out string second, "show_command_buttons(True)");
            Loaded(session, "h2p-order-a.py", out string first, "show_command_buttons(True)");

            List<CadPluginHost> hosts = Host.PluginHosts.ToList();

            Assert.True(hosts.IndexOf(HostOf(second)) < hosts.IndexOf(HostOf(first)));
        }

        #endregion

        #region A callback that raises

        [Fact]
        public void ACanRunThatRaises_CountsAsFalse_AndWritesOneLine()
        {
            CadCommandSession session = NewSession();
            CadPluginHost host = Loaded(
                session,
                "h2p-can-run.py",
                out _,
                "add_ribbon_panel(ribbon_panel('P', [ribbon_button('X', command='LINE', can_run=lambda c: 1 / 0)]))"
            );
            var button = Assert.IsType<CadRibbonButton>(Assert.Single(Assert.Single(host.RibbonPanels).Items));

            bool can = button.CanRun(ContextOf(Blank(), new string[0]));

            Assert.False(can);
            AssertOneErrorLine(session, "ZeroDivisionError", "h2p-can-run.py");
        }

        [Fact]
        public void AShowWhenThatRaises_CountsAsFalse_AndWritesOneLine()
        {
            CadCommandSession session = NewSession();
            CadPluginHost host = Loaded(
                session,
                "h2p-show-when.py",
                out _,
                "add_ribbon_tab(ribbon_tab('h2sw', 'T', [ribbon_panel('P', [ribbon_button('X', command='LINE')])], show_when=lambda c: 1 / 0))"
            );

            bool shown = Assert.Single(host.RibbonTabs).ShowWhen(ContextOf(Blank(), new string[0]));

            Assert.False(shown);
            AssertOneErrorLine(session, "ZeroDivisionError", "h2p-show-when.py");
        }

        [Fact]
        public void AMenuProviderThatRaises_GivesNoItems_AndWritesOneLine()
        {
            CadCommandSession session = NewSession();
            CadPluginHost host = Loaded(session, "h2p-menu-raises.py", out _, "add_canvas_menu(lambda context: 1 / 0)");

            IReadOnlyList<CadPluginMenuItem> items = Assert.Single(host.CanvasMenus)(ContextOf(Blank(), new string[0]));

            Assert.Empty(items ?? new CadPluginMenuItem[0]);
            AssertOneErrorLine(session, "ZeroDivisionError", "h2p-menu-raises.py");
        }

        [Fact]
        public void APropertiesProviderThatRaises_GivesNoInfo_AndWritesOneLine()
        {
            CadCommandSession session = NewSession();
            CadPluginHost host = Loaded(session, "h2p-rows-raise.py", out _, "add_properties(lambda request: 1 / 0)");
            var request = Made<CadPropertyRequest>(Blank(), new string[0], null, null);

            CadSelectionInfo result = Assert.Single(host.PropertyProviders)(request);

            Assert.Null(result);
            AssertOneErrorLine(session, "ZeroDivisionError", "h2p-rows-raise.py");
        }

        private void AssertOneErrorLine(CadCommandSession session, string error, string file)
        {
            string line = Assert.Single(ErrorLines(session));
            Assert.Contains(error, line);
            Assert.Contains(file, line);
        }

        #endregion

        #region The Context

        [Fact]
        public void ACallbacksContext_GivesTheDrawing_TheSelectedEntities_Write_AndRunCommand()
        {
            CadCommandSession session = NewSession();
            CadPluginHost host = Loaded(
                session,
                "h2p-context.py",
                out _,
                "def hello(context):",
                "    names = ','.join(e.handle for e in context.selected)",
                "    context.write(f'{context.drawing.name}|{names}|{context.is_command_running}')",
                "    context.run_command('CIRCLE')",
                "add_ribbon_panel(ribbon_panel('P', [ribbon_button('Hello', run=hello)]))"
            );
            CadDocument document = Blank("ctx.dxf");
            string handle = AddLine(document, 0).Handle;
            AddLine(document, 50);
            var written = new List<string>();
            var commands = new List<string>();
            var button = Assert.IsType<CadRibbonButton>(Assert.Single(Assert.Single(host.RibbonPanels).Items));

            button.Run(ContextOf(document, new[] { handle }, written.Add, commands.Add));

            Assert.Equal(new[] { "ctx.dxf|" + handle + "|False" }, written);
            Assert.Equal(new[] { "CIRCLE" }, commands);
        }

        #endregion
    }
}

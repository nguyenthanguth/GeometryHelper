using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Drawing.Plugins;
using Xunit;

namespace GeometryHelper.Drawing.Wpf.UnitTest
{
    /// <summary>
    /// Drawing plugins run from the presenter: the Run plugin dialog of the Drawing tab, driven with no window, and PLUGIN on
    /// the command line, on a drawing of the column C1 with its four bolts.
    /// </summary>
    public class PluginRunTests
    {
        private static async Task<ModelDrawing> ColumnAsync(Bench bench, params DrawingPlugin[] plugins)
        {
            var catalog = new DrawingPluginCatalog();

            foreach (DrawingPlugin plugin in plugins)
            {
                catalog.Add(plugin);
            }

            bench.Workspace.Plugins = catalog;
            NewDrawingViewModel dialog = bench.Workspace.ShowNewDrawing();
            await dialog.NextAsync();
            dialog.Preset = "Assembly";

            foreach (ObjectRow row in dialog.Objects)
            {
                row.IsChecked = row.Object.ParentId == "C1";
            }

            await dialog.NextAsync();
            ModelDrawing made = await dialog.NextAsync();
            Assert.NotNull(made);
            Assert.Equal(4, made.Drawing.Source.Objects.Count(o => o.Kind == DrawingObjectKind.Bolt));
            return made;
        }

        private static List<DrawingElement> RunDimensions(ModelDrawing made) => made.Drawing.Elements.Where(e => e.Kind == "Dimension" && e.Id.Contains("/R-1/")).ToList();

        [Fact]
        public async Task RunPluginListsTheBoltsOfTheViewTickedAndRunsThePluginAsOneUndoStep()
        {
            var bench = new Bench();
            ModelDrawing made = await ColumnAsync(bench, new BoltPitches());
            int elements = made.Drawing.Elements.Count;

            Assert.True(bench.Workspace.RunPluginCommand.CanExecute(null));
            bench.Workspace.RunPluginCommand.Execute(null);

            DrawingDialogRequestEventArgs request = Assert.Single(bench.Requests, r => r.Kind == DrawingDialogKind.RunPlugin);
            var dialog = Assert.IsType<PluginRunViewModel>(request.Model);
            Assert.Equal("Bolt pitches", dialog.Plugin.Name);
            Assert.Equal(made.Drawing.Views.Select(v => v.Id), dialog.Views.Select(v => v.Id));
            dialog.View = dialog.Views.Single(v => v.Id == "V-FRONT");
            Assert.Equal("Bolts of the view", dialog.ObjectsTitle);
            Assert.Equal(4, dialog.Objects.Count);
            Assert.All(dialog.Objects, o => Assert.True(o.IsChecked));
            Assert.Equal("4 of 4 ticked: every one of the view, those an update adds too.", dialog.ObjectsText);
            PluginValueRow rounding = Assert.Single(dialog.Settings);
            Assert.Equal(("Rounding", "1", "1 by default, from 0.01 to 100"), (rounding.Name, rounding.Text, rounding.RangeText));

            rounding.Text = "500";
            Assert.Equal("Rounding has to be a number from 0.01 to 100.", rounding.Error);
            Assert.False(dialog.RunCommand.CanExecute(null));
            Assert.Null(dialog.Run());
            rounding.Text = "5";
            Assert.True(dialog.RunCommand.CanExecute(null));

            UpdateReport report = dialog.Run();

            Assert.NotNull(report);
            DrawingPluginRun run = Assert.Single(made.Drawing.PluginRuns);
            Assert.Equal(("Bolt pitches", "V-FRONT"), (run.Plugin, run.ViewId));
            Assert.Empty(run.Inputs.ObjectIds);
            Assert.Equal(new[] { new KeyValuePair<string, string>("Rounding", "5") }, run.Settings);
            Assert.NotEmpty(RunDimensions(made));
            Assert.Equal(elements + RunDimensions(made).Count, made.Drawing.Elements.Count);
            Assert.Contains(bench.Model.History, l => l.StartsWith("Bolt pitches in V-FRONT: " + RunDimensions(made).Count + " elements added, 0 changed", StringComparison.Ordinal));
            Assert.Equal("UPDATE", made.Document.NextUndo.Name);
            Assert.Equal("Assembly", made.PresetName);

            Assert.True(bench.Model.Undo());
            Assert.Empty(made.Drawing.PluginRuns);
            Assert.Empty(RunDimensions(made));
            Assert.Equal(elements, made.Drawing.Elements.Count);
        }

        [Fact]
        public async Task ABoltUntickedLeavesThePluginTheTickedOnesOnly()
        {
            var bench = new Bench();
            ModelDrawing made = await ColumnAsync(bench, new BoltPitches());
            PluginRunViewModel dialog = bench.Workspace.ShowRunPlugin();
            dialog.View = dialog.Views.Single(v => v.Id == "V-FRONT");

            dialog.Objects[0].IsChecked = false;
            Assert.Equal("3 of 4 ticked: those only.", dialog.ObjectsText);
            Assert.NotNull(dialog.Run());

            Assert.Equal(dialog.Objects.Skip(1).Select(o => o.Id), Assert.Single(made.Drawing.PluginRuns).Inputs.ObjectIds);
        }

        [Fact]
        public async Task APluginThatFailsIsSaidUnderTheDialogAndOnTheCommandLineAndTheDrawingIsLeftAsItWas()
        {
            var bench = new Bench();
            ModelDrawing made = await ColumnAsync(bench, new BoltPitches { Fails = true }, new BoltPitches { Name2 = "Broken input", FailsToSay = true });
            IReadOnlyList<DrawingElement> before = made.Drawing.Elements;
            string undo = made.Document.NextUndo?.Name;
            PluginRunViewModel dialog = bench.Workspace.ShowRunPlugin();
            Assert.Equal("Bolt pitches", dialog.Plugin.Name);

            Assert.Null(dialog.Run());

            Assert.StartsWith("Bolt pitches in V-", dialog.Notice);
            Assert.Contains("failed: the plugin failed on purpose.", dialog.Notice);
            Assert.Contains(dialog.Notice, bench.Model.History);
            Assert.Equal(before, made.Drawing.Elements);
            Assert.Empty(made.Drawing.PluginRuns);
            Assert.Equal(undo, made.Document.NextUndo?.Name);

            // A plugin that throws while it says what it needs is said too, and cannot be run.
            dialog.Plugin = dialog.Plugins.Single(p => p.Name == "Broken input");
            Assert.Equal("The plugin Broken input failed to say what it needs: no input on purpose.", dialog.Notice);
            Assert.False(dialog.RunCommand.CanExecute(null));
            Assert.Null(dialog.Run());
        }

        [Fact]
        public async Task ARunWhileTheDrawingIsMadeAgainIsRefusedInTheWordsTheOtherChangesUse()
        {
            var bench = new Bench();
            ModelDrawing made = await ColumnAsync(bench, new BoltPitches());
            PluginRunViewModel dialog = bench.Workspace.ShowRunPlugin();
            var gate = new TaskCompletionSource<bool>();
            DrawingSettings settings = made.Drawing.Settings;
            settings.Views.Kinds.Add(ViewKind.Isometric);
            Task<UpdateReport> slow = bench.Workspace.ApplySettingsAsync(made, settings, "Changed", async (text, work) =>
            {
                await gate.Task;
                return work();
            });

            Assert.Null(dialog.Run());

            Assert.Equal("C1 is being made again; wait for it.", dialog.Notice);
            Assert.Contains("C1 is being made again; wait for it.", bench.Model.History);
            gate.SetResult(true);
            Assert.NotNull(await slow);
            Assert.Empty(made.Drawing.PluginRuns);
            Assert.NotNull(dialog.Run());
            Assert.Single(made.Drawing.PluginRuns);
        }

        [Fact]
        public async Task PluginTypedAsksForThePluginTheViewAndEachSettingAndRunsOnceTheCommandHasEnded()
        {
            var bench = new Bench();
            ModelDrawing made = await ColumnAsync(bench, new BoltPitches());
            var later = new List<Action>();
            bench.Workspace.Deferrer = later.Add;

            bench.Model.Execute("PLUGIN");
            Assert.Equal("Plugin name [Bolt pitches] <Bolt pitches>:", bench.Model.Prompt);
            bench.Model.Execute(string.Empty);
            Assert.StartsWith("View [" + string.Join("/", made.Drawing.Views.Select(v => v.Id)) + "] <", bench.Model.Prompt);
            bench.Model.Execute("V-FRONT");
            Assert.Equal("Rounding, from 0.01 to 100 <1>:", bench.Model.Prompt);
            bench.Model.Execute("500");
            Assert.Contains("Rounding has to be from 0.01 to 100.", bench.Model.History);
            Assert.Equal("Rounding, from 0.01 to 100 <1>:", bench.Model.Prompt);
            bench.Model.Execute("2");

            Action run = Assert.Single(later);
            Assert.False(bench.Model.IsCommandRunning);
            Assert.Empty(made.Drawing.PluginRuns);
            run();

            DrawingPluginRun kept = Assert.Single(made.Drawing.PluginRuns);
            Assert.Equal(("Bolt pitches", "V-FRONT"), (kept.Plugin, kept.ViewId));
            Assert.Equal(new[] { new KeyValuePair<string, string>("Rounding", "2") }, kept.Settings);
            Assert.Equal("UPDATE", made.Document.NextUndo.Name);
            Assert.Contains(bench.Model.History, l => l.StartsWith("Bolt pitches in V-FRONT: ", StringComparison.Ordinal));
        }

        [Fact]
        public async Task PluginAndRunPluginWithNoDrawingPluginLoadedSayHowToLoadOne()
        {
            var bench = new Bench();
            await ColumnAsync(bench);

            bench.Model.Execute("PLUGIN");

            Assert.False(bench.Model.IsCommandRunning);
            Assert.Contains("No drawing plugin is loaded: load one with PLUGINLOAD, or in the Plugin manager.", bench.Model.History);
            Assert.Null(bench.Workspace.ShowRunPlugin());
            Assert.DoesNotContain(bench.Requests, r => r.Kind == DrawingDialogKind.RunPlugin);
        }

        /// <summary>A plugin of the tests: the hole pitches of the bolts given, as the sample Bolt dimensions makes them; and failures on purpose.</summary>
        private sealed class BoltPitches : DrawingPlugin
        {
            public bool Fails { get; set; }

            public bool FailsToSay { get; set; }

            public string Name2 { get; set; }

            public override string Name => Name2 ?? "Bolt pitches";

            public override void DefineInput(DrawingPluginInput input)
            {
                if (FailsToSay)
                {
                    throw new NotSupportedException("no input on purpose.");
                }

                input.AddView("Pick a view");
                input.AddObjects("Pick bolts", DrawingObjectKind.Bolt);
                input.AddSetting("Rounding", 1.0, 0.01, 100.0);
            }

            public override void Run(DrawingPluginContext context)
            {
                if (Fails)
                {
                    throw new NullReferenceException("the plugin failed on purpose.");
                }

                var objects = new List<DrawingObject>(context.Objects);

                foreach (string id in context.View.ObjectIds)
                {
                    if (context.Source.TryGetObject(id, out DrawingObject part) && part.Kind == DrawingObjectKind.Part)
                    {
                        objects.Add(part);
                    }
                }

                DimensionSettings settings = context.Settings.Dimensions;
                settings.Scale = context.View.Scale;
                settings.Rounding = context.Setting("Rounding");
                settings.Overall = settings.Chain = settings.Cumulative = settings.Ordinate = false;
                settings.HolesByGroup = true;
                context.AddDimensions(Annotator.Dimension(context.View.Lines, objects, context.View.Frame.CoordinateSystem, settings, context.Tolerance));
            }
        }
    }
}

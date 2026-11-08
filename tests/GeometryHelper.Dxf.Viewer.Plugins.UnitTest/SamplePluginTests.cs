using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer.Plugins;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Plugins.UnitTest
{
    /// <summary>The sample plugin, NUMCIRCLES and FAILNOW, loaded from its .dll as a user loads one and run on the command line.</summary>
    public sealed class SamplePluginTests : IDisposable
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        private readonly PluginFiles _files = new PluginFiles();
        private readonly CadPluginManager _manager;
        private readonly CadCommandRegistry _registry = CadCommandRegistry.CreateDefault();
        private readonly CadLoadedPlugin _sample;

        public SamplePluginTests()
        {
            _manager = _files.Manager();
            string path = _files.Sample();
            _manager.AddTrustedFolder(Path.GetDirectoryName(path));
            _manager.Attach(_registry);
            CadPluginResult result = _manager.Load(path);
            Assert.True(result.Succeeded, result.ToString());
            _sample = result.Plugin;
        }

        public void Dispose()
        {
            _manager.Dispose();
            _files.Dispose();
        }

        [Fact]
        public void NumberCirclesWritesANumberCentredOnEachCircleSelectedOnTheLayerMark()
        {
            CadDocument document = PluginFiles.Circles();
            CadCommandSession session = PluginFiles.Session(document, _registry);

            session.Feed("NUMCIRCLES");
            Assert.Equal("Select objects:", session.PromptText);
            session.FeedSelection(new[] { "C3", "C1", "C2" });
            session.Enter();
            Assert.Equal("Start at <1>:", session.PromptText);
            session.Feed("7");

            Assert.Equal(CadCommandOutcome.Completed, session.LastOutcome);
            List<DxfDBText> texts = PluginFiles.Texts(document);
            Assert.Equal(new[] { "7", "8", "9" }, texts.Select(t => t.TextString));
            Assert.All(texts, t => Assert.Equal("Mark", t.Layer));
            Assert.All(texts, t => Assert.Equal(2.5, t.Height, 12));
            Assert.All(texts, t => Assert.Equal(DxfAttachmentPoint.MiddleCenter, t.Justify));
            Assert.True(texts[0].AlignmentPoint.IsEqualTo(new GeoPoint3(0, 0, 0), Tight));
            Assert.True(texts[1].AlignmentPoint.IsEqualTo(new GeoPoint3(50, 0, 0), Tight));
            Assert.True(texts[2].AlignmentPoint.IsEqualTo(new GeoPoint3(100, 20, 0), Tight));
            Assert.True(document.Database.LayerTable.Has("Mark"));
            Assert.Contains("Numbered 3 circles.", session.History);
        }

        [Fact]
        public void OneUndoTakesBackEveryNumberAndTheLayerTheRunMade()
        {
            CadDocument document = PluginFiles.Circles();
            CadCommandSession session = PluginFiles.Session(document, _registry);
            session.Feed("NUMCIRCLES");
            session.FeedSelection(new[] { "C1", "C2", "C3" });
            session.Enter();
            session.Enter();
            Assert.Equal(3, PluginFiles.Texts(document).Count);
            Assert.Equal("NUMCIRCLES", document.NextUndo.Name);

            Assert.True(document.Undo());

            Assert.Empty(PluginFiles.Texts(document));
            Assert.False(document.Database.LayerTable.Has("Mark"));
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void TheAliasRunsItAndTheCirclesSelectedBeforeAreTakenWithoutAsking()
        {
            CadDocument document = PluginFiles.Circles();
            document.Selection.Add(new[] { "C2" });
            CadCommandSession session = PluginFiles.Session(document, _registry);

            session.Feed("nc");
            Assert.Equal("Start at <1>:", session.PromptText);
            session.Enter();

            DxfDBText text = Assert.Single(PluginFiles.Texts(document));
            Assert.Equal("1", text.TextString);
            Assert.True(text.AlignmentPoint.IsEqualTo(new GeoPoint3(50, 0, 0), Tight));
        }

        [Fact]
        public void ALineInTheSelectionIsNotNumberedAndASelectionOfNoCircleChangesNothing()
        {
            CadDocument document = PluginFiles.Circles();
            CadCommandSession session = PluginFiles.Session(document, _registry);

            session.Feed("NUMCIRCLES");
            session.FeedSelection(new[] { "L1" });
            session.Enter();

            Assert.Equal(CadCommandOutcome.Completed, session.LastOutcome);
            Assert.Contains("No circle was selected.", session.History);
            Assert.Empty(PluginFiles.Texts(document));
            Assert.False(document.CanUndo);

            session.Feed("NUMCIRCLES");
            session.FeedSelection(new[] { "L1", "C1" });
            session.Enter();
            session.Enter();

            Assert.Equal("1", Assert.Single(PluginFiles.Texts(document)).TextString);
        }

        [Fact]
        public void ACommandThatFailsLeavesTheDrawingAsItWasAndSaysWhichPluginFailed()
        {
            CadDocument document = PluginFiles.Circles();
            CadCommandSession session = PluginFiles.Session(document, _registry);
            var lines = new List<string>();
            Action<GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;

            try
            {
                GeometryHelperLog.Writer = (level, text, exception) => lines.Add(GeometryHelperLog.Format(level, text, exception));
                session.Feed("FAILNOW");
                Assert.Single(PluginFiles.Texts(document));
                session.Feed("now");
            }
            finally
            {
                GeometryHelperLog.Writer = before;
            }

            Assert.Equal(CadCommandOutcome.Failed, session.LastOutcome);
            Assert.Empty(PluginFiles.Texts(document));
            Assert.False(document.CanUndo);
            Assert.Contains("*Error* FAILNOW of the plugin " + PluginFiles.SampleName + " failed: The sample fails on purpose.", session.History);
            Assert.Contains(lines, l => l.StartsWith("WARN ", StringComparison.Ordinal) && l.Contains("FAILNOW of the plugin " + PluginFiles.SampleName + " failed"));
        }

        [Fact]
        public void TheSampleDeclaresItsTextHeightAndLayerAndRunsWithTheValuesSet()
        {
            IReadOnlyList<CadPluginSetting> settings = _sample.Plugin.Settings;
            Assert.Equal(new[] { "Text height", "Layer" }, settings.Select(s => s.Label));
            Assert.Equal(0.1, settings[0].Minimum);
            Assert.Equal(1000.0, settings[0].Maximum);
            Assert.Equal("Mark", settings[1].Default);
            settings[0].Value = 5.0;
            settings[1].Value = "Numbers";
            CadDocument document = PluginFiles.Circles();
            CadCommandSession session = PluginFiles.Session(document, _registry);

            session.Feed("NUMCIRCLES");
            session.FeedSelection(new[] { "C1" });
            session.Enter();
            session.Enter();

            DxfDBText text = Assert.Single(PluginFiles.Texts(document));
            Assert.Equal(5.0, text.Height, 12);
            Assert.Equal("Numbers", text.Layer);
            Assert.Throws<ArgumentOutOfRangeException>(() => settings[0].Value = 0.05);
            Assert.Equal(5.0, settings[0].Value);
        }
    }
}

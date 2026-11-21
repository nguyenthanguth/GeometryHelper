using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// What the registrar of a plugin takes and refuses (D502, D507, D516, D517), made here with its internal constructor so
    /// that it is open for additions as it is inside Initialize: the rules for a snap's keyword and entity types, the
    /// arguments of every Add method, the defaults of the ribbon items, the menu items and the snap, and the context a
    /// callback is given.
    /// </summary>
    public class PluginHostRegistrationTests
    {
        private static CadPluginHost Host() => new CadPluginHost("Probe", null);

        private static CadCustomSnap Snap(string keyword) =>
            new CadCustomSnap("Probe snap", keyword, request => new GeoPoint2[0]);

        // ---- the keyword of a snap (D516) ----

        [Theory]
        [InlineData("CTOP")]
        [InlineData("ctop")]
        [InlineData("Bolt2")]
        [InlineData("AB")]
        [InlineData("ABCDEFGHIJKLMNOP")]
        [InlineData("TTX")]
        [InlineData("FROMX")]
        [InlineData("M2PX")]
        public void AKeywordOfTwoToSixteenLettersAndDigitsThatIsNotAnAutoCADSnapIsAccepted(string keyword)
        {
            CadPluginHost host = Host();

            host.AddObjectSnap(Snap(keyword));

            Assert.Equal(keyword.ToUpperInvariant(), Assert.Single(host.ObjectSnaps).Keyword);
        }

        [Theory]
        [InlineData("CENTRE")]
        [InlineData("cen")]
        [InlineData("END")]
        [InlineData("ENDS")]
        [InlineData("MIDPOINT")]
        [InlineData("NEAR")]
        [InlineData("QUAD")]
        [InlineData("INTERSECT")]
        [InlineData("PERP")]
        [InlineData("NODE")]
        [InlineData("GCENTER")]
        [InlineData("EXTEND")]
        [InlineData("INSERT")]
        [InlineData("TAN")]
        [InlineData("APPARENT")]
        [InlineData("PARALLEL")]
        [InlineData("NONE")]
        [InlineData("TT")]
        [InlineData("tt")]
        [InlineData("FROM")]
        [InlineData("M2P")]
        [InlineData("MTP")]
        public void AKeywordThatIsOrStartsWithAnAutoCADSnapOrIsAPointModifierIsRefused(string keyword)
        {
            CadPluginHost host = Host();

            ArgumentException refused = Assert.Throws<ArgumentException>(() => host.AddObjectSnap(Snap(keyword)));

            Assert.Contains(keyword.ToUpperInvariant(), refused.Message, StringComparison.Ordinal);
            Assert.Empty(host.ObjectSnaps);
        }

        [Theory]
        [InlineData("C T")]
        [InlineData("C-T")]
        [InlineData("_CTOP")]
        [InlineData("CTOP!")]
        [InlineData("A")]
        [InlineData("")]
        [InlineData("ABCDEFGHIJKLMNOPQ")]
        public void AKeywordThatIsNotLettersAndDigitsOnlyOrNotTwoToSixteenLongIsRefused(string keyword)
        {
            CadPluginHost host = Host();

            Assert.Throws<ArgumentException>(() => host.AddObjectSnap(Snap(keyword)));

            Assert.Empty(host.ObjectSnaps);
        }

        [Fact]
        public void ASnapForATypeThatIsNotAnEntityIsRefusedAndOneForAnEntityOrASubclassOfOneIsAccepted()
        {
            CadPluginHost host = Host();
            CadCustomSnap text = Snap("TEXTSNAP");
            text.EntityTypes.Add(typeof(string));
            CadCustomSnap none = Snap("NULLSNAP");
            none.EntityTypes.Add(null);
            CadCustomSnap curves = Snap("CURVESNAP");
            curves.EntityTypes.Add(typeof(DxfCurve));
            curves.EntityTypes.Add(typeof(DxfCircle));
            CadCustomSnap every = Snap("EVERYSNAP");
            every.EntityTypes.Add(typeof(DxfEntity));

            Assert.Throws<ArgumentException>(() => host.AddObjectSnap(text));
            Assert.Throws<ArgumentException>(() => host.AddObjectSnap(none));
            host.AddObjectSnap(curves);
            host.AddObjectSnap(every);

            Assert.Equal(new[] { "CURVESNAP", "EVERYSNAP" }, host.ObjectSnaps.Select(s => s.Keyword));
        }

        [Fact]
        public void ASnapMadeWithNoNameNoKeywordOrNoFinderIsRefusedAndTheDefaultsAreNoGlyphNoTypesAndOffUntilChosen()
        {
            Assert.Throws<ArgumentNullException>(() => new CadCustomSnap(null, "CTOP", r => null));
            Assert.Throws<ArgumentNullException>(() => new CadCustomSnap("Circle top", null, r => null));
            Assert.Throws<ArgumentNullException>(() => new CadCustomSnap("Circle top", "CTOP", null));
            Assert.Throws<ArgumentException>(() => new CadCustomSnap("  ", "CTOP", r => null));

            var snap = new CadCustomSnap("Circle top", "ctop", r => null);

            Assert.Equal("Circle top", snap.Name);
            Assert.Equal("CTOP", snap.Keyword);
            Assert.Empty(snap.EntityTypes);
            Assert.Null(snap.Glyph);
            Assert.False(snap.IsOnByDefault);
        }

        // ---- the Add methods ----

        [Fact]
        public void EveryAddMethodRefusesNull()
        {
            CadPluginHost host = Host();

            Assert.Throws<ArgumentNullException>(() => host.AddRibbonTab(null));
            Assert.Throws<ArgumentNullException>(() => host.AddRibbonPanel(null));
            Assert.Throws<ArgumentNullException>(() => host.AddProperties(null));
            Assert.Throws<ArgumentNullException>(() => host.AddDrawingsButton(null));
            Assert.Throws<ArgumentNullException>(() => host.AddDrawingsMenu(null));
            Assert.Throws<ArgumentNullException>(() => host.AddCanvasMenu(null));
            Assert.Throws<ArgumentNullException>(() => host.AddObjectSnap(null));
            Assert.True(host.IsEmpty);
        }

        [Fact]
        public void WhatIsAddedIsKeptInTheOrderAddedAndOneAdditionMakesTheHostNotEmpty()
        {
            CadPluginHost host = Host();
            Assert.True(host.IsEmpty);
            Assert.True(host.ShowsCommandButtons);
            var first = new CadRibbonTab("first", "First");
            var second = new CadRibbonTab("second", "Second");
            var panel = new CadRibbonPanel("Panel");

            host.AddRibbonTab(first);
            host.AddRibbonTab(second);
            host.AddRibbonPanel(panel);

            Assert.False(host.IsEmpty);
            Assert.Equal(new[] { first, second }, host.RibbonTabs);
            Assert.Same(panel, Assert.Single(host.RibbonPanels));
            Assert.Equal("Probe", host.PluginName);
            Assert.Null(host.Folder);
        }

        [Fact]
        public void OnceClosedEveryAddMethodThrowsNamingThePluginAndWhatWasAddedBeforeStays()
        {
            CadPluginHost host = Host();
            host.AddRibbonTab(new CadRibbonTab("kept", "Kept"));

            host.Close();

            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() =>
                host.AddRibbonTab(new CadRibbonTab("late", "Late"))
            );
            Assert.Equal("The plugin Probe adds to the window only in Initialize.", refused.Message);
            Assert.Throws<InvalidOperationException>(() => host.AddObjectSnap(Snap("LATESNAP")));
            Assert.Equal("kept", Assert.Single(host.RibbonTabs).Id);
        }

        // ---- ribbon items ----

        [Fact]
        public void ATabNeedsAnIdAndATitleAndStartsAlwaysShownWithNoPanels()
        {
            Assert.Throws<ArgumentNullException>(() => new CadRibbonTab(null, "T"));
            Assert.Throws<ArgumentException>(() => new CadRibbonTab("", "T"));
            Assert.Throws<ArgumentNullException>(() => new CadRibbonTab("t", null));

            var tab = new CadRibbonTab("t", "T");

            Assert.Equal("t", tab.Id);
            Assert.Equal("T", tab.Title);
            Assert.Empty(tab.Panels);
            Assert.Null(tab.ShowWhen);
        }

        [Fact]
        public void AButtonRunsACommandOrACallbackAndStartsStandardWithItsTextShown()
        {
            var command = new CadRibbonButton("Number", "NUMCIRCLES2");
            var callback = new CadRibbonButton("Count", c => { });

            Assert.Equal("NUMCIRCLES2", command.Command);
            Assert.Null(command.Run);
            Assert.Null(callback.Command);
            Assert.NotNull(callback.Run);
            Assert.All(new CadRibbonItem[] { command, callback }, i =>
            {
                Assert.Equal(CadRibbonItemSize.Standard, i.Size);
                Assert.True(i.ShowText);
                Assert.Null(i.Icon);
                Assert.Null(i.ToolTip);
                Assert.Null(i.Description);
            });
            Assert.Null(command.CanRun);
            Assert.Throws<ArgumentException>(() => new CadRibbonButton("Number", ""));
            Assert.Throws<ArgumentNullException>(() => new CadRibbonButton("Number", (string)null));
            Assert.Throws<ArgumentNullException>(() => new CadRibbonButton("Number", (Action<CadPluginContext>)null));
            Assert.Throws<ArgumentNullException>(() => new CadRibbonButton(null, "NUMCIRCLES2"));
        }

        [Fact]
        public void ASplitButtonNeedsAtLeastOneButtonAndNoNullOneAndRemembersTheLastByDefault()
        {
            var one = new CadRibbonButton("One", c => { });

            Assert.Throws<ArgumentNullException>(() => new CadRibbonSplitButton("Sizes", null));
            Assert.Throws<ArgumentException>(() => new CadRibbonSplitButton("Sizes", new CadRibbonButton[0]));
            Assert.Throws<ArgumentException>(() => new CadRibbonSplitButton("Sizes", new[] { one, null }));

            var split = new CadRibbonSplitButton("Sizes", new[] { one });

            Assert.Same(one, Assert.Single(split.Buttons));
            Assert.True(split.RemembersLast);
        }

        // ---- menu items and buttons ----

        [Fact]
        public void AMenuItemIsEnabledAndNotTickedUntilToldAndASeparatorIsMarked()
        {
            var item = new CadPluginMenuItem("Mark", c => { });
            var ticked = new CadPluginMenuItem("Show", c => { }, true, true);
            var grey = new CadPluginMenuItem("Clear", c => { }, false);
            CadPluginMenuItem submenu = new CadPluginMenuItem("Tools", new[] { item, grey });
            CadPluginMenuItem separator = CadPluginMenuItem.Separator();

            Assert.True(item.IsEnabled);
            Assert.Null(item.IsChecked);
            Assert.True(ticked.IsChecked);
            Assert.False(grey.IsEnabled);
            Assert.Equal(new[] { item, grey }, submenu.Items);
            Assert.Null(submenu.Run);
            Assert.False(item.IsSeparator);
            Assert.True(separator.IsSeparator);
            Assert.Throws<ArgumentNullException>(() => new CadPluginMenuItem(null, c => { }));
            Assert.Throws<ArgumentException>(() => new CadPluginMenuItem("Tools", new CadPluginMenuItem[] { null }));
        }

        [Fact]
        public void ADrawingsButtonNeedsAnIconAndACallbackAndIsAlwaysOnUntilItsCanRunIsSet()
        {
            CadPluginIcon icon = CadPluginIcon.FromPathData("M0 0 L1 1");

            var button = new CadPluginButton(icon, "Report", c => { });

            Assert.Same(icon, button.Icon);
            Assert.Equal("Report", button.ToolTip);
            Assert.Null(button.CanRun);
            Assert.Throws<ArgumentNullException>(() => new CadPluginButton(null, "Report", c => { }));
            Assert.Throws<ArgumentNullException>(() => new CadPluginButton(icon, "Report", null));
        }

        // ---- what a callback is given ----

        [Fact]
        public void TheContextGivesTheDocumentTheSelectionAndForwardsWriteAndRunCommand()
        {
            CadDocument document = EditingDrawings.Blank();
            var written = new List<string>();
            var started = new List<string>();
            var context = new CadPluginContext(document, new[] { "A1", "A2" }, true, written.Add, started.Add);

            context.Write("Circles: 2.");
            context.RunCommand("NUMCIRCLES2");

            Assert.Same(document, context.Document);
            Assert.Equal(new[] { "A1", "A2" }, context.Selected);
            Assert.True(context.IsCommandRunning);
            Assert.Equal(new[] { "Circles: 2." }, written);
            Assert.Equal(new[] { "NUMCIRCLES2" }, started);
        }

        [Fact]
        public void AContextWithNoDocumentAndNoSelectionHasNullAndAnEmptyList()
        {
            var context = new CadPluginContext(null, null, false, s => { }, s => { });

            Assert.Null(context.Document);
            Assert.Empty(context.Selected);
            Assert.False(context.IsCommandRunning);
        }
    }
}

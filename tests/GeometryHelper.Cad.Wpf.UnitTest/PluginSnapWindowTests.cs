using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The object snaps a command plugin adds, in the window: "Circle top" (CTOP) of the sample plugin of the tests and, where a
    /// test needs a snap that fails or one that answers nearer than the built-in ones, snaps the test writes itself. The drawing
    /// is two columns and a wall: a circle of radius 100 about (2 000, 1 500), whose top is (2 000, 1 600), a circle of radius
    /// 50 about (3 000, 1 500) and a line from (0, 0) to (4 000, 0).
    /// </summary>
    public sealed class PluginSnapWindowTests : IDisposable
    {
        // The cursor 5 from the top of the first circle (4 across and 3 down), and the aperture of 20 round it: the top
        // is well within the aperture, the circle's centre 97 away is well outside it.
        private static readonly GeoPoint2 NearTheTop = new GeoPoint2(2004, 1597);
        private const double Aperture = 20.0;

        private readonly PluginWindowKit _kit = new PluginWindowKit();

        public void Dispose() => _kit.Dispose();

        private static string[] Headers(IEnumerable<CadMenuItemViewModel> items) =>
            items.Select(i => i.IsSeparator ? string.Empty : i.Header).ToArray();

        private static CadSnapResult SnapAt(MainViewModel model, GeoPoint2 cursor) =>
            CadPicking.Snap(model.Document, model.Document.ModelSpace, model.Snap, cursor, Aperture, null);

        private static void TurnOn(MainViewModel model, string name) =>
            model.OsnapMenu().Single(i => i.Header == name).Command.Execute(null);

        /// <summary>Adds the snap BOLT of the hook plugin, which answers where <paramref name="find"/> says.</summary>
        private void LoadBolt(MainViewModel model, Func<CadSnapRequest, IEnumerable<GeoPoint2>> find, bool onByDefault)
        {
            _kit.LoadHook(model, host =>
                host.AddObjectSnap(
                    new CadCustomSnap("Bolt centre", "BOLT", r => find(r)) { IsOnByDefault = onByDefault }
                )
            );
        }

        #region The menus

        [Fact]
        public void TheOsnapMenu_ListsCircleTopAfterTheFourteenAndASeparator_WithItsGlyphAndUnticked()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            string[] plain = Headers(model.OsnapMenu());

            _kit.LoadSample(model);

            IReadOnlyList<CadMenuItemViewModel> menu = model.OsnapMenu();
            Assert.Equal(
                plain.Take(14).Concat(new[] { string.Empty, "Circle top" }).Concat(plain.Skip(14)),
                Headers(menu)
            );
            CadMenuItemViewModel top = menu.Single(i => i.Header == "Circle top");
            Assert.NotNull(top.Glyph);
            Assert.True(top.IsCheckable);
            Assert.False(top.IsChecked);
        }

        [Fact]
        public void ChoosingCircleTopInTheOsnapMenu_TurnsItOn_KeepsItByKeyword_AndAnotherChoiceTurnsItOff()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            Assert.Empty(model.Snap.CustomSnaps);

            TurnOn(model, "Circle top");

            Assert.Contains("CTOP=1", model.Settings.PluginSnaps);
            Assert.Equal("Circle top", Assert.Single(model.Snap.CustomSnaps).Name);
            Assert.True(model.OsnapMenu().Single(i => i.Header == "Circle top").IsChecked);

            TurnOn(model, "Circle top");

            Assert.Contains("CTOP=0", model.Settings.PluginSnaps);
            Assert.DoesNotContain("CTOP=1", model.Settings.PluginSnaps);
            Assert.Empty(model.Snap.CustomSnaps);
            Assert.False(model.OsnapMenu().Single(i => i.Header == "Circle top").IsChecked);
        }

        [Fact]
        public void TheShiftMenu_HasCircleTopInAGroupAfterNearestAndBeforeNone()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            model.Execute("LINE");
            string[] plain = Headers(model.SnapOverrideMenu());
            _kit.LoadSample(model);

            IReadOnlyList<CadMenuItemViewModel> menu = model.SnapOverrideMenu();

            int nearest = Array.IndexOf(Headers(menu), "Nearest");
            Assert.Equal(
                new[] { "Nearest", string.Empty, "Circle top", string.Empty, "None" },
                Headers(menu).Skip(nearest).Take(5)
            );
            Assert.NotNull(menu.Single(i => i.Header == "Circle top").Glyph);
            Assert.Equal(plain.Length + 3, menu.Count);
        }

        [Fact]
        public void TwoPluginsSnaps_AreInTheMenusInTheOrderThePluginsWereLoaded()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            LoadBolt(model, r => new GeoPoint2[0], false);
            model.Execute("LINE");

            string[] osnap = Headers(model.OsnapMenu());
            string[] shift = Headers(model.SnapOverrideMenu());

            Assert.Equal(new[] { "Circle top", "Bolt centre" }, osnap.Skip(Array.IndexOf(osnap, "Circle top")).Take(2));
            Assert.Equal(new[] { "Circle top", "Bolt centre" }, shift.Skip(Array.IndexOf(shift, "Circle top")).Take(2));
        }

        [Fact]
        public void AfterUnloading_TheMenusAreAsTheyWereBeforeThePluginWasLoaded()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            model.Execute("LINE");
            string[] osnap = Headers(model.OsnapMenu());
            string[] shift = Headers(model.SnapOverrideMenu());
            _kit.LoadSample(model);
            TurnOn(model, "Circle top");

            Assert.True(model.UnloadPlugin(PluginWindowKit.SampleName).Succeeded);

            Assert.Equal(osnap, Headers(model.OsnapMenu()));
            Assert.Equal(shift, Headers(model.SnapOverrideMenu()));
            Assert.Empty(model.Snap.CustomSnaps);
            Assert.False(model.CreateDraftingSettings().HasPluginSnaps);
        }

        #endregion

        #region Overrides

        [Fact]
        public void ChoosingCircleTopInTheShiftMenu_TakesItAlone_WritesItsKeyword_AndTheNextPointEndsIt()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            model.Execute("LINE");

            model.SnapOverrideMenu().Single(i => i.Header == "Circle top").Command.Execute(null);

            Assert.Equal("Circle top", model.Snap.CustomOverride.Name);
            Assert.Equal("Specify first point: _ctop of", model.History.Last());

            model.Execute("1,1");

            Assert.Null(model.Snap.CustomOverride);
        }

        [Theory]
        [InlineData("CTOP")]
        [InlineData("ctop")]
        [InlineData("CtOp")]
        [InlineData("_ctop")]
        [InlineData("_CTOP")]
        public void TypingTheKeywordWhole_InAnyCaseWithOrWithoutAnUnderscore_TakesTheSnapForTheNextPoint(string typed)
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            model.Execute("LINE");

            model.Execute(typed);

            Assert.Equal("Circle top", model.Snap.CustomOverride.Name);
            Assert.Equal("Specify first point: _ctop of", model.History.Last());
            Assert.Null(model.Snap.Override);
        }

        [Fact]
        public void APartOfTheKeyword_IsNotTheOverride()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            model.Execute("LINE");

            model.Execute("CT");

            Assert.Null(model.Snap.CustomOverride);
        }

        [Fact]
        public void TheKeywordTypedWhereNoPointIsAsked_IsNoOverride_AndWritesNoLineOfOne()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);

            model.Execute("CTOP");

            Assert.Null(model.Snap.CustomOverride);
            Assert.DoesNotContain(model.History, l => l.EndsWith("_ctop of", StringComparison.Ordinal));
        }

        [Fact]
        public void ABuiltInOverrideAfterACustomOne_TakesItsPlace_AndTheOtherWayRound()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            model.Execute("LINE");
            model.Execute("CTOP");
            Assert.NotNull(model.Snap.CustomOverride);

            model.OverrideSnap(CadSnapModes.Endpoint);

            Assert.Null(model.Snap.CustomOverride);
            Assert.Equal(CadSnapModes.Endpoint, model.Snap.Override);

            model.Execute("CTOP");

            Assert.NotNull(model.Snap.CustomOverride);
            Assert.Null(model.Snap.Override);
        }

        [Fact]
        public void UnloadingThePlugin_ClearsAnOverrideOfItsSnap()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            model.Execute("LINE");
            model.Execute("CTOP");
            Assert.NotNull(model.Snap.CustomOverride);

            Assert.True(model.UnloadPlugin(PluginWindowKit.SampleName).Succeeded);

            Assert.Null(model.Snap.CustomOverride);
        }

        #endregion

        #region What a click takes

        [Fact]
        public void WithCircleTopOnAndQuadrantOff_AClickNearTheTopTakesTheTop_AsACustomResultThatNamesTheSnap()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            TurnOn(model, "Circle top");
            model.SnapModes = CadSnapModes.Center;

            CadSnapResult result = SnapAt(model, NearTheTop);

            Assert.Equal(CadSnapKind.Custom, result.Kind);
            Assert.Equal("Circle top", result.CustomSnap.Name);
            Assert.Equal(2000.0, result.Point.X, 6);
            Assert.Equal(1600.0, result.Point.Y, 6);
        }

        [Fact]
        public void WithQuadrantOnToo_TheTopIsATie_AndTheBuiltInQuadrantWins()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            TurnOn(model, "Circle top");
            model.SnapModes = CadSnapModes.Quadrant;

            CadSnapResult result = SnapAt(model, NearTheTop);

            Assert.Equal(CadSnapKind.Quadrant, result.Kind);
            Assert.Null(result.CustomSnap);
        }

        [Fact]
        public void ASnapThatAnswersNearerThanTheBuiltInOnes_WinsEvenWithQuadrantOn()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);

            // 1 right of the cursor: the Quadrant point is 5 away.
            LoadBolt(model, r => new[] { new GeoPoint2(r.Cursor.X + 1.0, r.Cursor.Y) }, true);
            model.SnapModes = CadSnapModes.Quadrant;

            CadSnapResult result = SnapAt(model, NearTheTop);

            Assert.Equal(CadSnapKind.Custom, result.Kind);
            Assert.Equal("BOLT", result.CustomSnap.Keyword);
            Assert.Equal(NearTheTop.X + 1.0, result.Point.X, 6);
        }

        [Fact]
        public void APointOfASnapBeyondTheAperture_IsDropped_AndTheBuiltInSnapAnswers()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);

            // Twice the aperture right of the cursor.
            LoadBolt(model, r => new[] { new GeoPoint2(r.Cursor.X + 2 * Aperture, r.Cursor.Y) }, true);
            model.SnapModes = CadSnapModes.Quadrant;

            CadSnapResult result = SnapAt(model, NearTheTop);

            Assert.Equal(CadSnapKind.Quadrant, result.Kind);
        }

        [Fact]
        public void WithOsnapOff_NoCustomSnapAnswers_ButTheOverrideStillDoes()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            TurnOn(model, "Circle top");
            model.SnapModes = CadSnapModes.Center;
            model.Toggles.First(t => t.Name == "OSNAP").IsOn = false;

            Assert.Equal(CadSnapKind.None, SnapAt(model, NearTheTop).Kind);

            model.Execute("LINE");
            model.Execute("CTOP");

            CadSnapResult result = SnapAt(model, NearTheTop);
            Assert.Equal(CadSnapKind.Custom, result.Kind);
        }

        [Fact]
        public void ASnapOfCirclesIsNotAskedAboutALine_WhileASnapOfEveryEntityIsAskedAboutBoth()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            var circles = new List<string>();
            var every = new List<string>();
            _kit.LoadHook(model, host =>
            {
                var onCircles = new CadCustomSnap("Of circles", "ONCIRCLE", r =>
                {
                    circles.Add(r.Handle);
                    return new GeoPoint2[0];
                }) { IsOnByDefault = true };
                onCircles.EntityTypes.Add(typeof(DxfCircle));
                host.AddObjectSnap(onCircles);
                host.AddObjectSnap(new CadCustomSnap("Of all", "ONALL", r =>
                    {
                        every.Add(r.Handle);
                        return new GeoPoint2[0];
                    }) { IsOnByDefault = true });
            });
            model.SnapModes = CadSnapModes.None;

            // 5 above the middle of the wall, far from both circles: only the wall is under the aperture of 20.
            SnapAt(model, new GeoPoint2(1000, 5));

            Assert.Empty(circles);
            Assert.Single(every);

            // Near the first circle only the circle is.
            SnapAt(model, NearTheTop);

            Assert.Single(circles);
            Assert.Equal(2, every.Count);
        }

        [Fact]
        public void TheDrawingAndTheLookupOfEntities_AreGivenToTheSnapOnlyWhileItIsAsked()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            TurnOn(model, "Circle top");

            SnapAt(model, NearTheTop);

            Assert.Null(model.Snap.Document);
            Assert.Null(model.Snap.EntityOf);
        }

        [Fact]
        public void EscapeEndsTheOverride_SoTheNextCommandsFirstPointIsNotTakenByIt()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            model.Execute("LINE");
            model.Execute("CTOP");
            Assert.NotNull(model.Snap.CustomOverride);

            model.Escape();

            Assert.Null(model.Snap.CustomOverride);
            model.Execute("LINE");
            Assert.Null(model.Snap.CustomOverride);
        }

        [Fact]
        public void AFinderThatGivesNotANumberOrInfinity_HasThosePointsDropped_AndTheBuiltInSnapAnswers()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            LoadBolt(
                model,
                r => new[] { new GeoPoint2(double.NaN, double.NaN), new GeoPoint2(double.PositiveInfinity, 0) },
                true
            );
            model.SnapModes = CadSnapModes.Quadrant;

            CadSnapResult result = SnapAt(model, NearTheTop);

            Assert.Equal(CadSnapKind.Quadrant, result.Kind);
        }

        [Fact]
        public void AFinderThatGivesTwoPointsInTheAperture_HasTheNearerOneTaken()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);

            // 12 right and 3 right of the cursor, with the aperture of 20 and no built-in snap on.
            LoadBolt(
                model,
                r =>
                    new[] { new GeoPoint2(r.Cursor.X + 12.0, r.Cursor.Y), new GeoPoint2(r.Cursor.X + 3.0, r.Cursor.Y) },
                true
            );
            model.SnapModes = CadSnapModes.None;

            CadSnapResult result = SnapAt(model, NearTheTop);

            Assert.Equal(CadSnapKind.Custom, result.Kind);
            Assert.Equal(NearTheTop.X + 3.0, result.Point.X, 6);
        }

        #endregion

        #region A finder that fails

        [Fact]
        public void AFinderThatThrows_WritesItsLineOnce_AndIsNotAskedAgain()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            int asked = 0;
            LoadBolt(
                model,
                r =>
                {
                    asked++;
                    throw new InvalidOperationException("Nope.\r\nsecond line");
                },
                true
            );
            model.SnapModes = CadSnapModes.None;
            int before = model.History.Count;

            SnapAt(model, NearTheTop);
            SnapAt(model, NearTheTop);

            Assert.Equal(1, asked);
            Assert.Equal(
                new[] { "The snap BOLT of HookPlugin failed and is off: Nope." },
                model.History.Skip(before).Where(l => l.Contains("failed and is off"))
            );
        }

        [Fact]
        public void AFailingSnap_LeavesTheBuiltInSnapsAnswering_AndTheOtherPluginsSnap()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            TurnOn(model, "Circle top");
            LoadBolt(model, r => throw new InvalidOperationException("Nope."), true);
            model.SnapModes = CadSnapModes.Center;

            CadSnapResult first = SnapAt(model, NearTheTop);

            Assert.Equal(CadSnapKind.Custom, first.Kind);
            Assert.Equal("Circle top", first.CustomSnap.Name);

            model.SnapModes = CadSnapModes.Quadrant;

            Assert.Equal(CadSnapKind.Quadrant, SnapAt(model, NearTheTop).Kind);
        }

        [Fact]
        public void AFailedSnapTickedOnAgain_IsAskedAgain()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            int asked = 0;
            LoadBolt(
                model,
                r =>
                {
                    asked++;
                    throw new InvalidOperationException("Nope.");
                },
                true
            );
            model.SnapModes = CadSnapModes.None;
            SnapAt(model, NearTheTop);
            Assert.Equal(1, asked);

            TurnOn(model, "Bolt centre");
            TurnOn(model, "Bolt centre");
            SnapAt(model, NearTheTop);

            Assert.Equal(2, asked);
        }

        #endregion

        #region Kept in the settings

        [Fact]
        public void TheChoice_SurvivesAnUnloadAndAReload()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            TurnOn(model, "Circle top");

            Assert.True(model.UnloadPlugin(PluginWindowKit.SampleName).Succeeded);
            Assert.Contains("CTOP=1", model.Settings.PluginSnaps);
            _kit.LoadSample(model);

            Assert.Single(model.Snap.CustomSnaps);
            Assert.True(model.OsnapMenu().Single(i => i.Header == "Circle top").IsChecked);
            Assert.True(model.CreateDraftingSettings().PluginSnaps.Single().IsChecked);
        }

        [Fact]
        public void TheChoice_SurvivesASettingsFileAndApplyingItAgain()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            TurnOn(model, "Circle top");
            string path = Path.Combine(_kit.Folder.FullPath, "settings.json");

            model.Settings.Save(path);
            GlobalSettings back = GlobalSettings.Load(path);

            Assert.Contains("CTOP=1", back.PluginSnaps);

            TurnOn(model, "Circle top");
            Assert.Empty(model.Snap.CustomSnaps);
            model.ApplySettings(back);

            Assert.Single(model.Snap.CustomSnaps);
        }

        [Fact]
        public void ASnapNeverChosen_StartsAsItsIsOnByDefaultSays()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            Assert.Empty(model.Snap.CustomSnaps);

            LoadBolt(model, r => new GeoPoint2[0], true);

            Assert.Equal(new[] { "BOLT" }, model.Snap.CustomSnaps.Select(s => s.Keyword));
            Assert.DoesNotContain(model.Settings.PluginSnaps, e => e.StartsWith("BOLT", StringComparison.Ordinal));
        }

        #endregion

        #region Drafting Settings

        [Fact]
        public void DraftingSettings_HasNoPluginSnapsGroupWithNoPluginLoaded()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);

            DraftingSettingsViewModel dialog = model.CreateDraftingSettings();

            Assert.False(dialog.HasPluginSnaps);
            Assert.Empty(dialog.PluginSnaps);
        }

        [Fact]
        public void DraftingSettings_ListsCircleTopWithItsKeywordAndPlugin_SelectAllAndClearAllTickIt_AndOkWritesTheChoice()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            DraftingSettingsViewModel dialog = model.CreateDraftingSettings();

            Assert.True(dialog.HasPluginSnaps);
            PluginSnapRowViewModel row = Assert.Single(dialog.PluginSnaps);
            Assert.Equal("Circle top", row.Name);
            Assert.Equal("CTOP", row.Keyword);
            Assert.Equal(PluginWindowKit.SampleName, row.PluginName);
            Assert.NotNull(row.Glyph);
            Assert.False(row.IsChecked);

            dialog.SelectAllCommand.Execute(null);
            Assert.True(row.IsChecked);
            dialog.ClearAllCommand.Execute(null);
            Assert.False(row.IsChecked);
            dialog.SelectAllCommand.Execute(null);

            Assert.True(dialog.Ok());
            Assert.Contains("CTOP=1", model.Settings.PluginSnaps);
            Assert.Single(model.Snap.CustomSnaps);
            Assert.True(model.OsnapMenu().Single(i => i.Header == "Circle top").IsChecked);
        }

        [Fact]
        public void DraftingSettings_ShowsASnapOnAsItIs_AndAClearedBoxAndOkTurnsItOff()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            TurnOn(model, "Circle top");
            DraftingSettingsViewModel dialog = model.CreateDraftingSettings();
            PluginSnapRowViewModel row = Assert.Single(dialog.PluginSnaps);
            Assert.True(row.IsChecked);

            row.IsChecked = false;
            Assert.True(dialog.Ok());

            Assert.Empty(model.Snap.CustomSnaps);
            Assert.Contains("CTOP=0", model.Settings.PluginSnaps);
        }

        [Fact]
        public void OkOfDraftingSettings_WritesNothingForASnapLeftAsItsDefault()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            DraftingSettingsViewModel dialog = model.CreateDraftingSettings();
            Assert.False(Assert.Single(dialog.PluginSnaps).IsChecked);

            Assert.True(dialog.Ok());

            Assert.DoesNotContain(model.Settings.PluginSnaps, e => e.StartsWith("CTOP", StringComparison.Ordinal));
            Assert.Empty(model.Snap.CustomSnaps);
        }

        #endregion
    }
}

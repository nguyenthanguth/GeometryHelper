using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Remote.UnitTest
{
    /// <summary>Layers and layouts seen from outside, and a block the program defines itself.</summary>
    public class RemoteTableTests
    {
        private static DxfLine Line(double x1, double y1, double x2, double y2) =>
            new DxfLine(new GeoPoint3(x1, y1, 0), new GeoPoint3(x2, y2, 0));

        /// <summary>A drawing with the layers Steel (red, locked) and Concrete (cyan, off, with a description).</summary>
        private static CadDocument WithLayers(string name)
        {
            var db = new DxfDatabase("AC1027");
            db.LayerTable.Add(
                new DxfLayerTableRecord
                {
                    Name = "Steel",
                    ColorIndex = 1,
                    IsLocked = true,
                }
            );
            var concrete = new DxfLayerTableRecord
            {
                Name = "Concrete",
                ColorIndex = 4,
                IsOff = true,
                LineWeight = DxfLineWeight.LineWeight050,
            };
            db.LayerTable.Add(concrete);
            concrete.Description = "Cast in place";
            return CadDocument.From(db, name);
        }

        private static (Harness harness, TestHost host, CadDocument document, CadRemoteDocument remote) Start(
            CadDocument document
        )
        {
            var harness = new Harness();
            var host = new TestHost();
            host.Add(document);
            harness.Serve(host);
            CadRemoteDocument remote = harness.Connect().Documents.Single();
            return (harness, host, document, remote);
        }

        [Fact]
        public void TheLayers_AreListedInTheOrderOfTheTable_WithTheirLooks()
        {
            var (harness, _, _, remote) = Start(WithLayers("A-101.dxf"));
            using (harness)
            {
                IReadOnlyList<CadRemoteLayer> layers = remote.Layers;

                Assert.Equal(new[] { "0", "Steel", "Concrete" }, layers.Select(l => l.Name).ToArray());
                CadRemoteLayer steel = layers[1];
                Assert.True(steel.IsOn);
                Assert.False(steel.IsFrozen);
                Assert.True(steel.IsLocked);
                Assert.True(steel.IsPlottable);
                Assert.Equal(1, steel.Color.ColorIndex);
                Assert.Equal("Continuous", steel.Linetype);
                Assert.Equal(DxfLineWeight.ByLineWeightDefault, steel.LineWeight);
                Assert.Equal(string.Empty, steel.Description);
                CadRemoteLayer concrete = layers[2];
                Assert.False(concrete.IsOn);
                Assert.Equal(4, concrete.Color.ColorIndex);
                Assert.Equal(DxfLineWeight.LineWeight050, concrete.LineWeight);
                Assert.Equal("Cast in place", concrete.Description);
            }
        }

        [Fact]
        public void ALayerAddedWithAnObjectOnIt_IsOneUndoStep_AndUndoTakesBothAway()
        {
            var (harness, _, document, remote) = Start(WithLayers("A-101.dxf"));
            using (harness)
            {
                remote.AddLayer(
                    new CadRemoteLayer("Bolts")
                    {
                        Color = DxfColor.FromRgb(10, 200, 30),
                        Description = "Bolt marks",
                        IsPlottable = false,
                        LineWeight = DxfLineWeight.LineWeight030,
                    }
                );
                remote.Insert(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0)) { Layer = "Bolts" });
                IReadOnlyList<string> handles = remote.CommitChanges();

                DxfLayerTableRecord made = document.Database.LayerTable["Bolts"];
                Assert.Equal(0x0AC81E, made.TrueColor);
                Assert.Equal("Bolt marks", made.Description);
                Assert.False(made.IsPlottable);
                Assert.Equal(DxfLineWeight.LineWeight030, made.LineWeight);
                Assert.Equal("Bolts", document.FindEntity(Assert.Single(handles)).Layer);
                Assert.Equal("REMOTE", document.NextUndo.Name);

                Assert.True(document.Undo());

                Assert.False(document.Database.LayerTable.Has("Bolts"));
                Assert.Empty(document.Database.ModelSpace);
                Assert.False(document.CanUndo);
            }
        }

        [Fact]
        public void ALayerAddedAfterTheObjectThatNamesIt_IsStillMadeFirst()
        {
            var (harness, _, document, remote) = Start(WithLayers("A-101.dxf"));
            using (harness)
            {
                remote.Insert(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0)) { Layer = "Late" });
                remote.AddLayer(
                    new CadRemoteLayer("Late") { Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3) }
                );
                remote.CommitChanges();

                Assert.Equal(3, document.Database.LayerTable["Late"].ColorIndex);
                Assert.Equal("Late", document.Database.ModelSpace.Single().Layer);
            }
        }

        [Fact]
        public void ALayerModified_TakesEveryLookItIsGiven_InTheStepOfTheCommit()
        {
            var (harness, _, document, remote) = Start(WithLayers("A-101.dxf"));
            using (harness)
            {
                CadRemoteLayer steel = remote.Layers.Single(l => l.Name == "Steel");
                steel.IsFrozen = true;
                steel.IsLocked = false;
                steel.IsOn = false;
                steel.IsPlottable = false;
                steel.Color = DxfColor.FromRgb(255, 128, 0);
                steel.Description = "Steel members";
                remote.ModifyLayer(steel);
                remote.Insert(Line(0, 0, 10, 0));
                remote.CommitChanges();

                DxfLayerTableRecord layer = document.Database.LayerTable["Steel"];
                Assert.True(layer.IsFrozen);
                Assert.False(layer.IsLocked);
                Assert.True(layer.IsOff);
                Assert.False(layer.IsPlottable);
                Assert.Equal(0xFF8000, layer.TrueColor);
                Assert.Equal("Steel members", layer.Description);
                Assert.Equal("REMOTE", document.NextUndo.Name);

                Assert.True(document.Undo());

                Assert.True(document.Database.LayerTable["Steel"].IsLocked);
                Assert.False(document.Database.LayerTable["Steel"].IsFrozen);
                Assert.Equal(1, document.Database.LayerTable["Steel"].ColorIndex);
                Assert.Null(document.Database.LayerTable["Steel"].TrueColor);
            }
        }

        [Fact]
        public void ALayerThatTheDrawingRefuses_AppliesNothingOfTheCommit()
        {
            var (harness, _, document, remote) = Start(WithLayers("A-101.dxf"));
            using (harness)
            {
                string start = document.Database.ToText();

                // A layer the drawing has, in other letters.
                remote.Insert(Line(0, 0, 5, 5));
                remote.AddLayer(new CadRemoteLayer("STEEL"));
                CadRemoteException twice = Assert.Throws<CadRemoteException>(() => remote.CommitChanges());
                Assert.Contains("already exists", twice.Message, StringComparison.Ordinal);

                // A name with a character AutoCAD refuses.
                remote.AddLayer(new CadRemoteLayer("A<B"));
                CadRemoteException invalid = Assert.Throws<CadRemoteException>(() => remote.CommitChanges());
                Assert.Contains("invalid layer name", invalid.Message, StringComparison.Ordinal);

                // The same new name twice in one commit.
                remote.AddLayer(new CadRemoteLayer("Twin"));
                remote.AddLayer(new CadRemoteLayer("twin"));
                Assert.Throws<CadRemoteException>(() => remote.CommitChanges());

                // A linetype the drawing does not list.
                remote.AddLayer(new CadRemoteLayer("Dashed") { Linetype = "NOPE" });
                CadRemoteException linetype = Assert.Throws<CadRemoteException>(() => remote.CommitChanges());
                Assert.Contains("linetype NOPE", linetype.Message, StringComparison.Ordinal);

                // A layer to modify that is not there, and the current layer frozen.
                CadRemoteLayer ghost = new CadRemoteLayer("Ghost");
                remote.ModifyLayer(ghost);
                CadRemoteException missing = Assert.Throws<CadRemoteException>(() => remote.CommitChanges());
                Assert.Contains("no layer \"Ghost\"", missing.Message, StringComparison.Ordinal);
                CadRemoteLayer zero = remote.Layers.Single(l => l.Name == "0");
                zero.IsFrozen = true;
                remote.ModifyLayer(zero);
                CadRemoteException current = Assert.Throws<CadRemoteException>(() => remote.CommitChanges());
                Assert.Contains("CURRENT layer", current.Message, StringComparison.Ordinal);

                Assert.Equal(start, document.Database.ToText());
                Assert.False(document.CanUndo);
            }
        }

        [Fact]
        public void TheCurrentLayer_IsReadAndSet_AndAFrozenLayerIsRefused()
        {
            var (harness, _, document, remote) = Start(WithLayers("A-101.dxf"));
            using (harness)
            {
                Assert.Equal("0", remote.CurrentLayer);

                remote.CurrentLayer = "Steel";

                Assert.Equal("Steel", remote.CurrentLayer);
                Assert.Equal("Steel", document.Database.Clayer);
                Assert.Throws<CadRemoteException>(() => remote.CurrentLayer = "Nowhere");
                Assert.Throws<ArgumentNullException>(() => remote.CurrentLayer = null);
            }
        }

        [Fact]
        public void TheLayouts_AreListedInTheOrderOfTheTabs_AndTheLayoutShownIsSwitched()
        {
            var db = new DxfDatabase("AC1027");
            DxfLayout layout = db.Layouts.First(l => l.LayoutName == "Layout1");
            layout.Initialize();
            layout.BlockTableRecord.AppendEntity(Line(10, 10, 110, 60));
            var (harness, host, document, remote) = Start(CadDocument.From(db, "sheets.dxf"));
            using (harness)
            {
                Assert.Equal("Model", remote.Layouts[0]);
                Assert.Contains("Layout1", remote.Layouts);
                Assert.Equal("Model", remote.CurrentLayout);

                remote.CurrentLayout = "layout1";

                Assert.Equal("Layout1", remote.CurrentLayout);
                Assert.Equal("Layout1", document.CurrentSpace.Name);
                Assert.Equal("LAYOUT", document.NextUndo.Name);
                CadRemoteException none = Assert.Throws<CadRemoteException>(() => remote.CurrentLayout = "Sheet 9");
                Assert.Contains("no layout named Sheet 9", none.Message, StringComparison.Ordinal);

                // A command running holds the layout as it is.
                remote.CurrentLayout = "Model";
                Assert.False(remote.SendCommand("LINE 0,0", TimeSpan.FromMilliseconds(100)).Completed);
                CadRemoteException busy = Assert.Throws<CadRemoteException>(() => remote.CurrentLayout = "Layout1");
                Assert.Contains("busy with the command LINE", busy.Message, StringComparison.Ordinal);
                Assert.Equal("Model", document.CurrentSpace.Name);
                Assert.NotNull(host);
            }
        }

        [Fact]
        public void ABlockDefinedByTheProgram_IsBroughtWithItsInsert_AndTheDrawingsOwnWins()
        {
            var (harness, _, document, remote) = Start(
                new Func<CadDocument>(() =>
                {
                    var db = new DxfDatabase("AC1027");
                    var own = new DxfBlockTableRecord { Name = "OWN" };
                    db.BlockTable.Add(own);
                    own.AppendEntity(Line(0, 0, 1, 0));
                    return CadDocument.From(db, "A-101.dxf");
                })()
            );
            using (harness)
            {
                var mine = new DxfDatabase("AC1027");
                var bolt = new DxfBlockTableRecord { Name = "BOLT" };
                mine.BlockTable.Add(bolt);
                bolt.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 5.0));
                bolt.AppendEntity(Line(-5, 0, 5, 0));
                var ownOfMine = new DxfBlockTableRecord { Name = "OWN" };
                mine.BlockTable.Add(ownOfMine);
                ownOfMine.AppendEntity(Line(0, 0, 999, 0));

                remote.Insert(new DxfBlockReference(new GeoPoint3(100, 100, 0), bolt));
                remote.Insert(new DxfBlockReference(new GeoPoint3(200, 100, 0), bolt));
                remote.Insert(new DxfBlockReference(new GeoPoint3(300, 100, 0), ownOfMine));
                IReadOnlyList<string> handles = remote.CommitChanges();

                Assert.Equal(3, handles.Count);
                DxfBlockTableRecord brought = document.Database.BlockTable["BOLT"];
                Assert.Equal(2, brought.Count(e => !e.IsErased));
                Assert.Equal(2, document.Database.ModelSpace.OfType<DxfBlockReference>().Count(r => r.Name == "BOLT"));
                var third = (DxfBlockReference)document.FindEntity(handles[2]);
                Assert.Equal("OWN", third.Name);
                Assert.Equal(1.0, ((DxfLine)third.BlockTableRecord.Single(e => !e.IsErased)).EndPoint.X, 9);
                Assert.Equal("REMOTE", document.NextUndo.Name);

                Assert.True(document.Undo());

                Assert.Empty(document.Database.ModelSpace);
            }
        }

        [Fact]
        public void ABlockThatIsNeitherInTheDrawingNorDefinedWithObjects_IsStillRefused()
        {
            var (harness, _, document, remote) = Start(CadDocument.From(new DxfDatabase("AC1027"), "A-101.dxf"));
            using (harness)
            {
                var mine = new DxfDatabase("AC1027");
                var empty = new DxfBlockTableRecord { Name = "HOLLOW" };
                mine.BlockTable.Add(empty);
                string start = document.Database.ToText();

                remote.Insert(new DxfBlockReference(new GeoPoint3(0, 0, 0), empty));
                CadRemoteException refused = Assert.Throws<CadRemoteException>(() => remote.CommitChanges());

                Assert.Contains("has no block HOLLOW", refused.Message, StringComparison.Ordinal);
                Assert.Contains("does not define it", refused.Message, StringComparison.Ordinal);
                Assert.Equal(start, document.Database.ToText());
            }
        }
    }
}

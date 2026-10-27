using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The layer states and layer filters of a drawing, as AutoCAD's LayerStateManager and LayerFilters give them: the entries
    /// of the dictionaries ACAD_LAYERSTATES and ACAD_LAYERFILTERS of the layer table's extension dictionary, read and listed,
    /// the drawing left as it was. The drawings are built in code, the records of the states and filters XRECORDs of a text
    /// each; ezdxf 1.4.5 recovered and audited the probe's such drawing finding nothing.
    /// </summary>
    public class DxfLayerStatesAndFiltersTests
    {
        /// <summary>The R2018 drawing with the dictionaries given, each holding an XRECORD of each name, saved and read again.</summary>
        private static DxfDatabase WithDictionaries(string[] states, string[] filters)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            db.LayerTable.CreateExtensionDictionary();
            DxfDBDictionary extension = db.LayerTable.ExtensionDictionary;

            foreach (var kept in new[] { Tuple.Create("ACAD_LAYERSTATES", states), Tuple.Create("ACAD_LAYERFILTERS", filters) })
            {
                if (kept.Item2 == null)
                {
                    continue;
                }

                extension.SetAt(kept.Item1, new DxfDBDictionary { TreatElementsAsHard = true });
                var dictionary = (DxfDBDictionary)extension.GetAt(kept.Item1);

                foreach (string name in kept.Item2)
                {
                    dictionary.SetAt(name, new DxfXrecord { Data = new[] { new DxfTypedValue(1, name) } });
                }
            }

            return DxfDatabase.Parse(db.ToText());
        }

        [Fact]
        public void ADrawingThatKeepsNoLayerStatesOrFilters_HasNone_AndAskingChangesNothing()
        {
            foreach (string text in new[] { DxfObjectsEzdxfDrawings.R12, DxfObjectsEzdxfDrawings.R2000, DxfObjectsEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfLayerStateManager states = db.LayerStateManager;

                Assert.Null(states.LayerStatesDictionary);
                Assert.Empty(states.GetLayerStateNames());
                Assert.False(states.HasLayerState("Plan"));
                Assert.Equal("0 layer states", states.ToString());

                DxfLayerFilter root = db.LayerFilters.Root;
                Assert.Equal("All", root.Name);
                Assert.Null(root.Parent);
                Assert.Empty(root.NestedFilters);
                Assert.True(root.DynamicallyGenerated);
                Assert.False(root.AllowDelete);
                Assert.False(root.AllowRename);
                Assert.Equal("All, 0 filters", db.LayerFilters.ToString());
                Assert.Equal(text, db.ToText());
            }
        }

        [Fact]
        public void TheLayerStatesOfTheLayerTable_AreListedByTheirNames_InTheOrderOfTheRecord()
        {
            DxfDatabase db = WithDictionaries(new[] { "Plan", "Sections", "As built" }, null);
            string text = db.ToText();
            DxfLayerStateManager states = db.LayerStateManager;

            Assert.Same(states, db.LayerStateManager);
            Assert.Equal(new[] { "Plan", "Sections", "As built" }, states.GetLayerStateNames());
            Assert.True(states.HasLayerState("plan"));
            Assert.True(states.HasLayerState(" As built "));
            Assert.False(states.HasLayerState("Elevations"));
            Assert.Throws<ArgumentNullException>(() => states.HasLayerState(null));
            Assert.Equal("3 layer states", states.ToString());

            // Each state's record is the XRECORD its entry names, its groups as the file holds them.
            DxfDBDictionary dictionary = states.LayerStatesDictionary;
            Assert.Same(dictionary, db.LayerTable.ExtensionDictionary.GetAt("ACAD_LAYERSTATES"));
            var sections = Assert.IsType<DxfXrecord>(dictionary.GetAt("Sections"));
            Assert.Equal("Sections", Assert.Single(sections.Data).Value);
            Assert.Equal(text, db.ToText());

            // An entry taken out of the dictionary is a state no more.
            dictionary.Remove("Plan");
            Assert.Equal(new[] { "Sections", "As built" }, db.LayerStateManager.GetLayerStateNames());
        }

        [Fact]
        public void TheNamedLayerFilters_AreNestedInAll_InTheOrderOfTheRecord()
        {
            DxfDatabase db = WithDictionaries(new[] { "Plan" }, new[] { "Steel", "Notes" });
            string text = db.ToText();
            DxfLayerFilterTree tree = db.LayerFilters;

            Assert.Equal("All", tree.Root.Name);
            Assert.Equal(new[] { "Steel", "Notes" }, tree.Root.NestedFilters.Select(f => f.Name));
            Assert.All(tree.Root.NestedFilters, f =>
            {
                Assert.Same(tree.Root, f.Parent);
                Assert.Empty(f.NestedFilters);
                Assert.False(f.DynamicallyGenerated);
                Assert.True(f.AllowDelete);
                Assert.True(f.AllowRename);
            });
            Assert.Equal("All, 2 filters", tree.ToString());
            Assert.Equal("Steel", tree.Root.NestedFilters[0].ToString());

            // A new tree on each call, as the drawing has them then.
            Assert.NotSame(tree, db.LayerFilters);
            Assert.Equal(new[] { "Plan" }, db.LayerStateManager.GetLayerStateNames());
            Assert.Equal(text, db.ToText());
        }

        [Fact]
        public void AnEntryOfTheLayerTableThatIsNoDictionary_HoldsNoStatesOrFilters()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            db.LayerTable.CreateExtensionDictionary();
            db.LayerTable.ExtensionDictionary.SetAt("ACAD_LAYERSTATES", new DxfXrecord { Data = new[] { new DxfTypedValue(1, "Plan") } });
            db.LayerTable.ExtensionDictionary.SetAt("ACAD_LAYERFILTERS", new DxfXrecord { Data = new[] { new DxfTypedValue(1, "Steel") } });

            Assert.Null(db.LayerStateManager.LayerStatesDictionary);
            Assert.Empty(db.LayerStateManager.GetLayerStateNames());
            Assert.False(db.LayerStateManager.HasLayerState("Plan"));
            Assert.Empty(db.LayerFilters.Root.NestedFilters);
        }
    }
}

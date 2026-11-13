using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The dialog items of round 4 driven with no window shown: Annotation Object Scale (OBJECTSCALE) with its Add Scales to
    /// Object picker, and the Output ribbon tab.
    /// </summary>
    public class DialogsSevenWindowTests
    {
        private static readonly string[] ListScales = { "1:1", "1:20", "1:50", "1:100" };

        /// <summary>
        /// A drawing with the scales 1:1, 1:20, 1:50 and 1:100 and three annotative texts: A at 1:1 and 1:50, B at 1:50, C at
        /// 1:50 and 1:100; their handles in that order.
        /// </summary>
        private static MainViewModel Open(TestFolder folder, out string[] handles)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("dialogs7.dxf", new DxfDatabase("AC1027"))));
            DxfDatabase db = model.Document.Database;
            DxfObjectContextCollection list = db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES");

            foreach (string name in ListScales.Where(n => list.GetContext(n) == null))
            {
                list.AddContext(
                    new DxfAnnotationScale
                    {
                        Name = name,
                        PaperUnits = 1,
                        DrawingUnits = double.Parse(name.Substring(2)),
                    }
                );
            }

            var shown = new[] { new[] { "1:1", "1:50" }, new[] { "1:50" }, new[] { "1:50", "1:100" } };
            handles = new string[3];

            for (int i = 0; i < 3; i++)
            {
                var text = new DxfDBText
                {
                    Position = new GeoPoint3(0, 10 * i, 0),
                    TextString = "ABC"[i].ToString(),
                    Height = 2.5,
                };
                db.ModelSpace.AppendEntity(text);
                db.Cannoscale = shown[i][0];
                text.Annotative = DxfAnnotativeStates.True;

                foreach (string name in shown[i].Skip(1))
                {
                    text.AddContext((DxfAnnotationScale)list.GetContext(name));
                }

                handles[i] = text.Handle;
            }

            db.Cannoscale = "1:1";
            return model;
        }

        private static string[] Scales(MainViewModel model, string handle) =>
            model
                .Document.FindEntity(handle)
                .GetContexts()
                .Select(c => c.Name)
                .OrderBy(n => Array.IndexOf(ListScales, n))
                .ToArray();

        private static void Run(MainViewModel model, IReadOnlyList<string> handles)
        {
            model.Execute("OBJECTSCALE");
            model.Session.FeedSelection(handles);
            model.Execute(string.Empty);
        }

        [Fact]
        public void ObjectScale_ListsAllScalesOrThoseCommonToAll_WithThePaperAndDrawingUnitsOfThePicked()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string[] handles);
                bool shown = false;
                model.ShowObjectScale = dialog =>
                {
                    shown = true;
                    Assert.Equal(3, dialog.ObjectCount);
                    Assert.False(dialog.IsCommonOnly);
                    Assert.Equal(new[] { "1:1", "1:50", "1:100" }, dialog.Scales.Select(s => s.Name));
                    Assert.Equal(new[] { "on 1 of 3", "", "on 1 of 3" }, dialog.Scales.Select(s => s.Shared));

                    dialog.Picked = dialog.Scales[2];
                    Assert.Equal("1", dialog.PaperUnits);
                    Assert.Equal("100", dialog.DrawingUnits);

                    dialog.IsCommonOnly = true;
                    Assert.Equal(new[] { "1:50" }, dialog.Scales.Select(s => s.Name));
                    Assert.Equal("1:50", dialog.Picked.Name);
                    return false;
                };

                Run(model, handles);

                Assert.True(shown);
                Assert.False(model.IsCommandRunning);
            }
        }

        [Fact]
        public void ObjectScale_DeleteIsGreyedForAnObjectsLastScale_AddOffersTheOthers_AndOkAppliesBothAsOneUndoStep()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string[] handles);
                model.ShowObjectScale = dialog =>
                {
                    dialog.Picked = dialog.Scales.Single(s => s.Name == "1:50");
                    Assert.False(dialog.CanDelete);

                    dialog.Picked = dialog.Scales.Single(s => s.Name == "1:100");
                    Assert.True(dialog.CanDelete);
                    dialog.Delete();
                    Assert.Equal(new[] { "1:1", "1:50" }, dialog.Scales.Select(s => s.Name));

                    var adding = new ObjectScaleAddViewModel(dialog.AddChoices);
                    Assert.Equal(new[] { "1:20", "1:100" }, adding.Scales.Select(s => s.Name));
                    adding.Scales[0].IsPicked = true;
                    dialog.Add(adding.PickedNames);
                    Assert.Equal(new[] { "1:1", "1:20", "1:50" }, dialog.Scales.Select(s => s.Name));
                    Assert.Equal("", dialog.Scales[1].Shared);
                    return true;
                };

                Run(model, handles);

                Assert.Equal(new[] { "1:1", "1:20", "1:50" }, Scales(model, handles[0]));
                Assert.Equal(new[] { "1:20", "1:50" }, Scales(model, handles[1]));
                Assert.Equal(new[] { "1:20", "1:50" }, Scales(model, handles[2]));
                Assert.Equal("OBJECTSCALE", model.Document.NextUndo.Name);

                Assert.True(model.Document.Undo());
                Assert.Equal(new[] { "1:1", "1:50" }, Scales(model, handles[0]));
                Assert.Equal(new[] { "1:50" }, Scales(model, handles[1]));
                Assert.Equal(new[] { "1:50", "1:100" }, Scales(model, handles[2]));
            }
        }

        [Fact]
        public void ObjectScale_CancelChangesNothing_AndWithNoWindowItAsksAsMinusObjectScale()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string[] handles);
                model.ShowObjectScale = dialog =>
                {
                    dialog.Picked = dialog.Scales[0];
                    dialog.Delete();
                    return false;
                };

                Run(model, handles);

                Assert.Equal(new[] { "1:1", "1:50" }, Scales(model, handles[0]));
                Assert.Null(model.Document.NextUndo);

                model.ShowObjectScale = null;
                Run(model, handles);

                Assert.True(model.IsCommandRunning);
                Assert.Equal("Enter an option [Add/Delete/?] <Add>:", model.Session.PromptText);
                model.Escape();
            }
        }

        [Fact]
        public void ObjectScale_NothingAnnotativeSelected_SaysSoAndShowsNoDialog()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string[] _);
                var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
                model.Document.Database.ModelSpace.AppendEntity(line);
                model.ShowObjectScale = dialog => throw new InvalidOperationException("No dialog expected.");

                Run(model, new[] { line.Handle });

                Assert.Contains("No annotative objects found.", model.History);
            }
        }

        [Fact]
        public void ObjectScaleWindows_LayOutTheListTheButtonsAndThePicker()
        {
            OnSta(() =>
            {
                using (var folder = new TestFolder())
                {
                    MainViewModel model = Open(folder, out string[] handles);
                    model.ShowObjectScale = dialog =>
                    {
                        Lay(new ObjectScaleWindow(dialog).Body);
                        Lay(new ObjectScaleAddWindow(new ObjectScaleAddViewModel(dialog.AddChoices)).Body);
                        return false;
                    };

                    Run(model, handles);
                }
            });
        }

        [Fact]
        public void OutputTab_FollowsView_AndItsButtonsStartPlotPageSetupPreviewStampExportPdfExportAndImport()
        {
            var model = new MainViewModel();

            Assert.Equal("Output", model.OutputTab.TabHeader);
            Assert.IsType<OutputRibbonViewModel>(model.OutputTab.TabContent);
            Assert.Same(model, ((OutputRibbonViewModel)model.OutputTab.TabContent).Model);
        }

        [Fact]
        public void Boundary_NewAsksTheObjectsAndComesBackWithExistingSet_TheAreaFoundAmongThemAlone()
        {
            using (var folder = new TestFolder())
            {
                // A square 100 by 100 with a square 60 by 60 inside it; the point (50, 50) is inside both.
                var db = new DxfDatabase("AC1027");
                var outer = new List<string>();
                var squares = new[] { (0.0, 100.0), (20.0, 80.0) };

                foreach ((double low, double high) in squares)
                {
                    var corners = new[]
                    {
                        new GeoPoint3(low, low, 0),
                        new GeoPoint3(high, low, 0),
                        new GeoPoint3(high, high, 0),
                        new GeoPoint3(low, high, 0),
                    };

                    for (int i = 0; i < 4; i++)
                    {
                        var line = new DxfLine(corners[i], corners[(i + 1) % 4]);
                        db.ModelSpace.AppendEntity(line);

                        if (low == 0.0)
                        {
                            outer.Add(line.Handle);
                        }
                    }
                }

                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("boundary.dxf", db)));
                outer = model.Document.ModelSpace.Block.OfType<DxfLine>().Take(4).Select(l => l.Handle).ToList();
                int shown = 0;
                model.ShowBoundaryCreation = dialog =>
                {
                    shown++;

                    if (shown == 1)
                    {
                        Assert.Equal(new[] { "Current viewport" }, dialog.BoundarySets);
                        dialog.NewSet();
                        return true;
                    }

                    Assert.Equal(new[] { "Existing set", "Current viewport" }, dialog.BoundarySets);
                    Assert.Equal("Existing set", dialog.BoundarySet);
                    dialog.IslandDetection = false;
                    return true;
                };

                model.Execute("BOUNDARY");
                Assert.Equal("Select objects:", model.Session.PromptText);
                model.Session.FeedSelection(outer);
                model.Execute(string.Empty);
                Assert.Equal(2, shown);
                Assert.Equal("Pick internal point:", model.Session.PromptText);
                model.Execute("50,50");
                model.Execute(string.Empty);

                // The square of the set, not the inner one the point is nearer to.
                DxfPolyline made = model.Document.ModelSpace.Block.OfType<DxfPolyline>().Single();
                Assert.Equal(4, made.NumberOfVertices);
                double[] xs = Enumerable.Range(0, 4).Select(i => made.GetPoint2dAt(i).X).ToArray();
                Assert.Equal(100.0, xs.Max() - xs.Min(), 6);
            }
        }

        [Fact]
        public void LineweightSettings_StartsAtLwunitsAndLwdefaultTyped_AndOkSetsThemBack()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string[] _);
                model.Execute("LWUNITS");
                model.Execute("0");
                model.Execute("LWDEFAULT");
                model.Execute("35");
                model.ShowLineweightSettings = dialog =>
                {
                    Assert.True(dialog.IsInches);
                    Assert.Equal(35, dialog.DefaultWeight);
                    dialog.IsInches = false;
                    return true;
                };

                model.Execute("LWEIGHT");

                Assert.Equal(1.0, model.Session.Remembered["lwunits"]);
                Assert.Equal(0.35, model.Document.DefaultLineWeight, 9);
            }
        }

        [Fact]
        public void Xbind_TheTreeStartsClosed_AndAReferenceOrAKindOpensAndClosesItsRows()
        {
            using (var folder = new TestFolder())
            {
                var grid = new DxfDatabase("AC1027");
                grid.LayerTable.Add(new DxfLayerTableRecord { Name = "GRID" });
                grid.LayerTable.Add(new DxfLayerTableRecord { Name = "AXIS" });
                grid.ModelSpace.AppendEntity(
                    new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)) { Layer = "GRID" }
                );
                string gridPath = folder.Write("grid.dxf", grid);
                MainViewModel model = Open(folder, out string[] _);
                model.Execute("-XREF");
                model.Execute("A");
                model.Execute(gridPath);
                model.Execute("0,0");
                model.Execute(string.Empty);
                model.Execute(string.Empty);
                model.Execute(string.Empty);
                bool shown = false;
                model.ShowXbind = dialog =>
                {
                    shown = true;
                    Assert.Equal(new[] { "grid" }, dialog.ShownXrefs.Select(r => r.Text));
                    Assert.Equal("▸", dialog.ShownXrefs[0].Glyph);

                    dialog.Toggle(dialog.ShownXrefs[0]);
                    Assert.Equal(new[] { "grid", "Layer" }, dialog.ShownXrefs.Select(r => r.Text));
                    Assert.Equal("▾", dialog.Xrefs[0].Glyph);

                    dialog.Toggle(dialog.ShownXrefs[1]);
                    Assert.Equal(
                        new[] { "grid", "Layer", "grid|AXIS", "grid|GRID" },
                        dialog.ShownXrefs.Select(r => r.Text)
                    );
                    Assert.Equal(string.Empty, dialog.ShownXrefs[2].Glyph);

                    // A definition does not open; closing the reference hides its kind and the definitions under it.
                    dialog.Toggle(dialog.ShownXrefs[2]);
                    Assert.Equal(4, dialog.ShownXrefs.Count);
                    dialog.Toggle(dialog.ShownXrefs[0]);
                    Assert.Equal(new[] { "grid" }, dialog.ShownXrefs.Select(r => r.Text));
                    return false;
                };

                model.Execute("XBIND");

                Assert.True(shown);
            }
        }

        [Fact]
        public void PlotStamp_TheFieldListsPickFromTheUserFields_AddEditDeleteKeepThem_AndAPssIsSavedAndLoaded()
        {
            using (var folder = new TestFolder())
            {
                var dialog = new PlotStampViewModel(new CadPlotStamp());
                Assert.Equal(new[] { "< none >" }, dialog.UserFieldChoices);

                var fields = new UserDefinedFieldsViewModel(dialog.Stamp.UserFields);
                fields.Add();
                fields.Picked.Text = "Project 2611";
                fields.Add();
                fields.Picked.Text = "Checked: NT";
                fields.Add();
                Assert.Equal("New field", fields.Picked.Text);
                fields.Delete();
                Assert.Equal("Checked: NT", fields.Picked.Text);
                dialog.SetUserFields(fields.Fields);

                Assert.Equal(new[] { "< none >", "Project 2611", "Checked: NT" }, dialog.UserFieldChoices);
                dialog.UserField1Choice = "Checked: NT";
                Assert.Equal("Checked: NT", dialog.Stamp.UserField1);
                Assert.Equal("< none >", dialog.UserField2Choice);

                // A field taken out of the list leaves Field 1 at < none >.
                dialog.SetUserFields(new[] { "Project 2611" });
                Assert.Equal(string.Empty, dialog.Stamp.UserField1);
                dialog.UserField2Choice = "Project 2611";

                string path = System.IO.Path.Combine(folder.FullPath, "site.pss");
                Assert.True(dialog.SaveParameterFile(path));
                Assert.Equal(path, dialog.ParameterFile);

                var other = new PlotStampViewModel(new CadPlotStamp());
                Assert.True(other.LoadParameterFile(path));
                Assert.Equal(new[] { "Project 2611" }, other.Stamp.UserFields);
                Assert.Equal("Project 2611", other.UserField2Choice);

                Assert.False(other.LoadParameterFile(folder.WriteText("other.pss", "not a plot stamp")));
                Assert.Contains("plot stamp parameter file", other.Message);
            }
        }

        [Fact]
        public void PlotStampWindows_LayOutTheFieldListsTheParameterFileAndUserDefinedFields()
        {
            OnSta(() =>
            {
                var dialog = new PlotStampViewModel(new CadPlotStamp { UserFields = new[] { "Project 2611" } });
                Lay(new PlotStampWindow(dialog).Body);
                Lay(new UserDefinedFieldsWindow(new UserDefinedFieldsViewModel(dialog.Stamp.UserFields)).Body);
            });
        }

        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    test();
                }
                catch (Exception e)
                {
                    failure = e;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        private static void Lay(FrameworkElement element)
        {
            element.Measure(new Size(2000, 2000));
            element.Arrange(new Rect(element.DesiredSize));
            Assert.True(element.DesiredSize.Width > 0);
        }
    }
}

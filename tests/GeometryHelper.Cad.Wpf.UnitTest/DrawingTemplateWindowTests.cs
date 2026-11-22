using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Drawing templates in the window: NEW with Select Template (and typed with FILEDIA 0), QNEW and the quick New from the
    /// default template of Global settings, Save As to a .dwt through Template Options, and the Template settings of Global
    /// settings.
    /// </summary>
    public class DrawingTemplateWindowTests
    {
        /// <summary>A template of the office: layers Walls and Doors, a layout Plan, imperial, with a line of model space and a description.</summary>
        private static string WriteOfficeTemplate(TestFolder folder, string name = "office.dwt")
        {
            DxfDatabase database = CadTemplate.CreateImperial();
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "Walls" });
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "Doors" });
            var plan = new DxfLayout { LayoutName = "Plan" };
            plan.AddToLayoutDictionary(database, null);
            plan.Initialize();
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(5, 0, 0)));
            CadDocument
                .From(database, "office.dwg")
                .SaveAsTemplate(Path.Combine(folder.FullPath, name), "Office plan, inches", false, null);
            return Path.Combine(folder.FullPath, name);
        }

        private static MainViewModel ModelWithTemplateFolder(TestFolder folder, string qnew = null)
        {
            var model = new MainViewModel();
            model.ApplySettings(
                new GlobalSettings { TemplateFolder = folder.FullPath, QnewTemplate = qnew ?? string.Empty }
            );
            return model;
        }

        private static string Magic(string path) => Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, 6);

        [Fact]
        public void ANewDrawingFromATemplate_TakesItsLayersLayoutsAndVariables_AndIsUntitled()
        {
            using (var folder = new TestFolder())
            {
                string template = WriteOfficeTemplate(folder);
                var model = new MainViewModel();

                CadDocument document = model.NewDrawingFrom(template);

                Assert.Same(document, model.Document);
                Assert.Equal("Drawing1.dwg", model.Title);
                Assert.False(document.HasFile);
                Assert.Equal(new[] { "0", "Walls", "Doors" }, document.Database.LayerTable.Select(l => l.Name));
                Assert.Contains("Plan", model.SpaceTabs.Select(t => t.Name));
                Assert.Equal(DxfMeasurementValue.English, document.Database.Measurement);
                Assert.Equal("Standard", document.Database.Dimstyle);
                Assert.Equal(new GeoPoint2(12, 9), document.Database.Limmax);
                Assert.Single(document.Database.ModelSpace.OfType<DxfLine>());
                Assert.Contains("Started from the template office.dwt.", model.History);

                // The template stays as it was and the drawing is no template: the next one is Drawing2.dwg.
                Assert.Equal("Drawing2.dwg", model.NewDrawingFrom(template).Title);
            }
        }

        [Fact]
        public void ATemplateThatIsADxf_OrADwg_StartsADrawingToo()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                DxfDatabase database = CadTemplate.CreateMetric();
                database.LayerTable.Add(new DxfLayerTableRecord { Name = "FromDxf" });
                string dxf = folder.Write("sheet.dxf", database);
                string dwg = Path.Combine(folder.FullPath, "sheet.dwg");
                CadDocument.From(database, "sheet.dwg").SaveAs(dwg);

                Assert.Contains("FromDxf", model.NewDrawingFrom(dxf).Database.LayerTable.Select(l => l.Name));
                Assert.Contains("FromDxf", model.NewDrawingFrom(dwg).Database.LayerTable.Select(l => l.Name));
            }
        }

        [Fact]
        public void ATemplateThatCannotBeRead_IsSaid_AndStartsNothing()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.NewDrawing();
                string broken = folder.WriteText("broken.dwt", "AC1032 and nothing else");

                Assert.Null(model.NewDrawingFrom(broken));

                Assert.Equal("Drawing1.dwg", model.Title);
                Assert.Contains(model.History, l =>
                    l.StartsWith("Cannot read the template " + broken, StringComparison.Ordinal)
                );
            }
        }

        [Fact]
        public void NewTypedWithFiledia0_AsksTheTemplate_TakesTheOneTypedFromTheTemplateFolder_WithOrWithoutDwt()
        {
            using (var folder = new TestFolder())
            {
                WriteOfficeTemplate(folder);
                MainViewModel model = ModelWithTemplateFolder(folder);
                model.NewDrawing();
                model.Execute("FILEDIA");
                model.Execute("0");

                model.Execute("NEW");

                Assert.Equal("Enter template file name or [. (for none)] <acadiso.dwt>:", model.Session.PromptText);
                model.Execute("office");

                Assert.False(model.Session.IsRunning);
                Assert.Equal("Drawing2.dwg", model.Title);
                Assert.Contains("Walls", model.Document.Database.LayerTable.Select(l => l.Name));
            }
        }

        [Fact]
        public void NewTypedWithFiledia0_EnterTakesTheBuiltInMetricTemplate_APeriodAsksTheMeasurement()
        {
            var model = new MainViewModel();
            model.NewDrawing();
            model.Execute("FILEDIA");
            model.Execute("0");

            model.Execute("NEW");
            model.Enter();

            Assert.Equal("Drawing2.dwg", model.Title);
            Assert.Equal(DxfMeasurementValue.Metric, model.Document.Database.Measurement);
            Assert.Equal("ISO-25", model.Document.Database.Dimstyle);

            model.Execute("FILEDIA");
            model.Execute("0");
            model.Execute("NEW");
            model.Execute(".");

            Assert.Equal("Enter measurement [English/Metric] <Metric>:", model.Session.PromptText);
            model.Execute("English");

            Assert.Equal("Drawing3.dwg", model.Title);
            Assert.Equal(DxfMeasurementValue.English, model.Document.Database.Measurement);
            Assert.Equal("Standard", model.Document.Database.Dimstyle);
            Assert.Equal(DxfUnits.Inches, model.Document.Database.Insunits);
        }

        [Fact]
        public void NewTypedWithFiledia0_AcadisoAndAcadStandForTheBuiltInTemplates_AFileOfTheNameWins()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = ModelWithTemplateFolder(folder);
                model.NewDrawing();

                foreach (string typed in new[] { "acad", "ACAD.dwt" })
                {
                    model.Execute("FILEDIA");
                    model.Execute("0");
                    model.Execute("NEW");
                    model.Execute(typed);
                    Assert.Equal(DxfMeasurementValue.English, model.Document.Database.Measurement);
                }

                model.Execute("FILEDIA");
                model.Execute("0");
                model.Execute("NEW");
                model.Execute("acadiso.dwt");
                Assert.Equal(DxfMeasurementValue.Metric, model.Document.Database.Measurement);

                WriteOfficeTemplate(folder, "acad.dwt");
                model.Execute("FILEDIA");
                model.Execute("0");
                model.Execute("NEW");
                model.Execute("acad");
                Assert.Contains("Walls", model.Document.Database.LayerTable.Select(l => l.Name));
            }
        }

        [Fact]
        public void NewTypedWithFiledia0_ATemplateNotFound_IsSaid_AndStartsNothing()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = ModelWithTemplateFolder(folder);
                model.NewDrawing();
                model.Execute("FILEDIA");
                model.Execute("0");

                model.Execute("NEW");
                model.Execute("missing");

                Assert.False(model.Session.IsRunning);
                Assert.Equal("Drawing1.dwg", model.Title);
                Assert.Contains("\"missing.dwt\": Can't find file.", model.History);
            }
        }

        [Fact]
        public void NewWithTheDialog_StartsFromTheTemplateChosen_AndFromNoTemplateMetricOrImperial_AndCancelStartsNothing()
        {
            using (var folder = new TestFolder())
            {
                string template = WriteOfficeTemplate(folder);
                MainViewModel model = ModelWithTemplateFolder(folder);
                SelectTemplateViewModel shown = null;
                model.ShowSelectTemplate = dialog =>
                {
                    shown = dialog;
                    dialog.Open();
                    return true;
                };

                model.Execute("NEW");

                Assert.Equal(folder.FullPath, shown.Folder);
                Assert.Equal(new[] { "office.dwt" }, shown.Templates.Select(t => t.Name));
                Assert.Equal("Office plan, inches", shown.SelectedDescription);
                Assert.Equal("Drawing1.dwg", model.Title);
                Assert.Contains("Walls", model.Document.Database.LayerTable.Select(l => l.Name));
                Assert.Equal(template, shown.ChosenFile);

                model.ShowSelectTemplate = dialog =>
                {
                    dialog.OpenImperial();
                    return true;
                };
                model.Execute("NEW");
                Assert.Equal("Drawing2.dwg", model.Title);
                Assert.Equal(DxfMeasurementValue.English, model.Document.Database.Measurement);
                Assert.Equal("Standard", model.Document.Database.Dimstyle);
                Assert.Equal(12.0, model.Document.Database.Limmax.X);

                model.ShowSelectTemplate = dialog =>
                {
                    dialog.OpenMetric();
                    return true;
                };
                model.Execute("NEW");
                Assert.Equal("Drawing3.dwg", model.Title);
                Assert.Equal(DxfMeasurementValue.Metric, model.Document.Database.Measurement);
                Assert.Equal("ISO-25", model.Document.Database.Dimstyle);

                model.ShowSelectTemplate = dialog => false;
                model.Execute("NEW");
                Assert.Equal("Drawing3.dwg", model.Title);
                Assert.Equal(3, model.FileRows.Count(r => r.IsOpen));
            }
        }

        [Fact]
        public void TheSelectTemplateDialog_ListsTheTemplatesInOrder_FirstSelected_BrowseTakesAFileElsewhere_AndOpenWithNoneSelectedDoesNothing()
        {
            using (var folder = new TestFolder())
            {
                var empty = new SelectTemplateViewModel(folder.FullPath, Array.Empty<string>());
                Assert.True(empty.IsEmpty);
                Assert.False(empty.CanOpen);
                Assert.False(empty.Open());
                Assert.Equal(SelectTemplateChoice.Cancel, empty.Choice);
                empty.BrowseCommand.Execute(null);
                Assert.Equal(SelectTemplateChoice.Cancel, empty.Choice);

                string one = WriteOfficeTemplate(folder, "a.dwt");
                string two = folder.Write("b.dxf", CadTemplate.CreateMetric());
                var dialog = new SelectTemplateViewModel(folder.FullPath, CadTemplate.FindIn(folder.FullPath));

                Assert.Equal(new[] { "a.dwt", "b.dxf" }, dialog.Templates.Select(t => t.Name));
                Assert.Equal(new[] { "Drawing template", "DXF" }, dialog.Templates.Select(t => t.Kind));
                Assert.Same(dialog.Templates[0], dialog.SelectedTemplate);
                Assert.Equal("Office plan, inches", dialog.SelectedDescription);

                dialog.SelectedTemplate = dialog.Templates[1];
                Assert.Equal(string.Empty, dialog.SelectedDescription);

                int chosen = 0;
                dialog.Chosen += (s, e) => chosen++;
                dialog.BrowseFile = f => two;
                dialog.BrowseCommand.Execute(null);

                Assert.Equal(1, chosen);
                Assert.Equal(SelectTemplateChoice.File, dialog.Choice);
                Assert.Equal(two, dialog.ChosenFile);
                Assert.NotEqual(one, dialog.ChosenFile);

                dialog.BrowseFile = f => null;
                dialog.BrowseCommand.Execute(null);
                Assert.Equal(1, chosen);
            }
        }

        [Fact]
        public void QnewStartsFromTheDefaultTemplateOfGlobalSettings_AsTheQuickNewAndTheStartScreenDo()
        {
            using (var folder = new TestFolder())
            {
                WriteOfficeTemplate(folder);
                MainViewModel model = ModelWithTemplateFolder(folder, "office");

                model.Execute("QNEW");

                Assert.Equal("Drawing1.dwg", model.Title);
                Assert.Contains("Walls", model.Document.Database.LayerTable.Select(l => l.Name));

                model.NewCommand.Execute(null);
                Assert.Equal("Drawing2.dwg", model.Title);
                Assert.Contains("Doors", model.Document.Database.LayerTable.Select(l => l.Name));

                model.NewDrawing();
                Assert.Contains("Plan", model.SpaceTabs.Select(t => t.Name));
            }
        }

        [Fact]
        public void QnewWithNoDefaultTemplate_OrOneNotFound_StartsTheBuiltInMetricTemplate_AndSaysSo()
        {
            var model = new MainViewModel();

            model.Execute("QNEW");

            Assert.Equal(DxfMeasurementValue.Metric, model.Document.Database.Measurement);
            Assert.Single(model.Document.Database.LayerTable);
            Assert.DoesNotContain(model.History, l => l.Contains("QNEW is not found"));

            model.ApplySettings(
                new GlobalSettings { QnewTemplate = Path.Combine(Path.GetTempPath(), "no-such-template.dwt") }
            );
            model.Execute("QNEW");

            Assert.Equal("Drawing2.dwg", model.Title);
            Assert.Single(model.Document.Database.LayerTable);
            Assert.Contains(model.History, l => l.Contains("for QNEW is not found; the built-in template is used."));
        }

        [Fact]
        public void SaveAsThroughTheDialog_ListsTheTemplateTypeAfterTheDwgTypes_AndTheOldListIsKeptWhereNoDialogIsShown()
        {
            var model = new MainViewModel();
            model.NewDrawing();

            CadExportJob dialog = model.CreateSaveAsDialogJob();
            CadExportJob plain = model.CreateSaveAsJob();

            int at = CadExportJob.DwgSaveTypes.Count;
            Assert.Equal(CadExportJob.Template, dialog.FileTypes[at]);
            Assert.Equal(plain.FileTypes.Count + 1, dialog.FileTypes.Count);
            Assert.DoesNotContain(CadExportJob.Template, plain.FileTypes);
            Assert.Equal(CadExportJob.Dwg, dialog.FileType);
            Assert.Equal("Drawing1.dwg", dialog.FileName);
        }

        [Fact]
        public void SaveAsDrawingTemplateOfTheMenu_OffersTheTemplateTypeAndFolder_WithTheDrawingsNameAsDwt()
        {
            using (var folder = new TestFolder())
            {
                string templates = Path.Combine(folder.FullPath, "made-for-templates");
                MainViewModel model = ModelWithTemplateFolder(folder);
                model.ApplySettings(new GlobalSettings { TemplateFolder = templates });
                model.NewDrawing();
                CadExportJob offered = null;
                model.SaveAsRequested += (s, e) => offered = model.CreateSaveAsDialogJob();

                model.SaveAsTemplate();

                Assert.Equal(CadExportJob.Template, offered.FileType);
                Assert.Equal(Path.Combine(templates, "Drawing1.dwt"), offered.FileName);
                Assert.True(Directory.Exists(templates));

                // Opened again as the plain dialog, the template is not chosen.
                model.SaveAsRequested += (s, e) => offered = model.CreateSaveAsDialogJob();
                offered = model.CreateSaveAsDialogJob();
                Assert.Equal(CadExportJob.Dwg, offered.FileType);
            }
        }

        [Fact]
        public void SaveAsATemplate_ShowsTemplateOptions_AndWritesTheDwtWithItsDescriptionAndMeasurement()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.NewDrawing();
                string path = Path.Combine(folder.FullPath, "workshop.dwt");
                TemplateOptionsViewModel asked = null;
                model.ShowTemplateOptions = dialog =>
                {
                    asked = dialog;
                    dialog.Description = " Workshop sheet A1 ";
                    dialog.Measurement = TemplateOptionsViewModel.English;
                    return true;
                };

                Assert.True(model.SaveAs(path, CadExportJob.Template));

                Assert.Equal(TemplateOptionsViewModel.Metric, asked.Measurements[1]);
                Assert.Equal(path, model.Document.FileName);
                Assert.Equal("AC1032", Magic(path));
                Assert.Equal("Workshop sheet A1", CadTemplate.DescriptionOf(path));
                Assert.Equal(DxfMeasurementValue.English, CadDrawingFile.Open(path).Measurement);
                Assert.Contains("Saved " + path + ".", model.History);
                Assert.Contains(model.FileRows, r => r.Path == path);

                // QSAVE of the template asks nothing and keeps the description.
                asked = null;
                model.Execute("QSAVE");
                Assert.Null(asked);
                Assert.Equal("Workshop sheet A1", CadTemplate.DescriptionOf(path));
            }
        }

        [Fact]
        public void SaveAsATemplate_CancelledInTemplateOptions_WritesNothing_AndAFileNamedDwtIsATemplateWhatTypeSoever()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.NewDrawing();
                string path = Path.Combine(folder.FullPath, "cancelled.dwt");
                model.ShowTemplateOptions = dialog => false;

                Assert.False(model.SaveAs(path, CadExportJob.Template));

                Assert.False(File.Exists(path));
                Assert.Equal("Drawing1.dwg", model.Title);

                // The DWG type chosen with a name ending in .dwt: still a template, its options asked.
                int asked = 0;
                model.ShowTemplateOptions = dialog =>
                {
                    asked++;
                    dialog.Description = "by name";
                    return true;
                };
                Assert.True(model.SaveAs(path, CadExportJob.Dwg));
                Assert.Equal(1, asked);
                Assert.Equal("by name", CadTemplate.DescriptionOf(path));
            }
        }

        [Fact]
        public void SaveAsATemplateWithNoOptionsDialog_TakesTheMeasurementOfTheDrawing_AndGivesTheNameDwt()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.NewDrawingWithNoTemplate(false);

                Assert.True(model.SaveAs(Path.Combine(folder.FullPath, "inches.dwg"), CadExportJob.Template));

                Assert.Equal(Path.Combine(folder.FullPath, "inches.dwt"), model.Document.FileName);
                Assert.Equal(DxfMeasurementValue.English, CadDrawingFile.Open(model.Document.FileName).Measurement);
            }
        }

        [Fact]
        public void SaveAsTemplateTypedWithFiledia0_IsSavedByTheWindow_AndTheDrawingIsListedUnderTheTemplate()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.NewDrawing();
                model.Execute("FILEDIA");
                model.Execute("0");
                string path = Path.Combine(folder.FullPath, "typed.dwt");

                model.Execute("SAVEAS");
                model.Execute("Template");
                model.Execute(path);
                model.Execute("Typed description");
                model.Execute("Metric");

                Assert.False(model.Session.IsRunning);
                Assert.Equal(path, model.Document.FileName);
                Assert.Equal("Typed description", CadTemplate.DescriptionOf(path));
                Assert.Contains(model.FileRows, r => r.Path == path);
            }
        }

        [Fact]
        public void ADrawingOfATemplate_OpenedFromAFile_IsOfferedTheTemplateTypeFirst_AndItsNameWithDwt()
        {
            using (var folder = new TestFolder())
            {
                string template = WriteOfficeTemplate(folder);
                var model = new MainViewModel();

                Assert.True(model.Open(template));
                CadExportJob job = model.CreateSaveAsDialogJob();

                Assert.Equal(CadExportJob.Template, job.FileType);
                Assert.Equal(template, job.FileName);
            }
        }

        private static IEnumerable<DependencyObject> Logical(DependencyObject parent)
        {
            foreach (object child in LogicalTreeHelper.GetChildren(parent))
            {
                if (child is DependencyObject element)
                {
                    yield return element;

                    foreach (DependencyObject below in Logical(element))
                    {
                        yield return below;
                    }
                }
            }
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
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        [Fact]
        public void TheSelectTemplateWindow_HasTheListBrowseTheTwoNoTemplateButtonsAndTheDescription_OpenGreyedWhereNoneIsSelected()
        {
            using (var folder = new TestFolder())
            {
                WriteOfficeTemplate(folder);
                string[] expected =
                {
                    "Templates",
                    "Browse",
                    "Open",
                    "Open with no Template - Metric",
                    "Open with no Template - Imperial",
                    "Description",
                };

                OnSta(() =>
                {
                    var dialog = new SelectTemplateViewModel(folder.FullPath, CadTemplate.FindIn(folder.FullPath));
                    var window = new SelectTemplateWindow(dialog);
                    window.Body.Measure(new Size(520, 440));
                    window.Body.Arrange(new Rect(0, 0, 520, 440));
                    window.Body.UpdateLayout();

                    List<FrameworkElement> named = Logical(window.Body)
                        .OfType<FrameworkElement>()
                        .Where(e => !string.IsNullOrEmpty(AutomationProperties.GetName(e)))
                        .ToList();
                    Assert.Equal(
                        expected.OrderBy(n => n, StringComparer.Ordinal),
                        named.Select(AutomationProperties.GetName).OrderBy(n => n, StringComparer.Ordinal)
                    );
                    Assert.True(named.Single(e => AutomationProperties.GetName(e) == "Open").IsEnabled);

                    // The description is the selected template's, shown under the list.
                    var description = (TextBlock)named.Single(e => AutomationProperties.GetName(e) == "Description");
                    Assert.Equal("Office plan, inches", description.Text);

                    var chosen = 0;
                    window.Model.Chosen += (s, e) => chosen++;
                    window.Model.OpenImperial();
                    Assert.Equal(1, chosen);

                    var none = new SelectTemplateWindow(new SelectTemplateViewModel(folder.FullPath, new string[0]));
                    none.Body.Measure(new Size(520, 440));
                    none.Body.Arrange(new Rect(0, 0, 520, 440));
                    none.Body.UpdateLayout();
                    FrameworkElement open = Logical(none.Body)
                        .OfType<FrameworkElement>()
                        .Single(e => AutomationProperties.GetName(e) == "Open");
                    Assert.False(open.IsEnabled);
                    Assert.Equal(
                        Visibility.Visible,
                        Logical(none.Body)
                            .OfType<TextBlock>()
                            .Single(t => t.Text.StartsWith("No template in this folder", StringComparison.Ordinal))
                            .Visibility
                    );
                });
            }
        }

        [Fact]
        public void TheTemplateSettings_AreOnSaveAndExport_AreTakenByApply_AndKeptInTheSettingsFile()
        {
            using (var folder = new TestFolder())
            {
                string settings = Path.Combine(folder.FullPath, "settings.json");
                var model = new MainViewModel { SettingsPath = settings };
                GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Save and export");

                Assert.Equal(string.Empty, dialog.Row("Save and export", "Template folder (TEMPLATEPATH)").Value);
                Assert.Equal(
                    GlobalSettings.DefaultTemplateFolder,
                    dialog.Row("Save and export", "Where it is left empty").Value
                );
                Assert.Equal(string.Empty, dialog.Row("Save and export", "Default template for QNEW").Value);

                dialog.Row("Save and export", "Template folder (TEMPLATEPATH)").Value = folder.FullPath;
                dialog.Row("Save and export", "Default template for QNEW").Value = "office.dwt";

                Assert.True(dialog.Ok());

                Assert.Equal(folder.FullPath, model.Settings.TemplateFolder);
                Assert.Equal("office.dwt", model.Settings.QnewTemplate);
                GlobalSettings read = GlobalSettings.Load(settings);
                Assert.Equal(folder.FullPath, read.TemplateFolder);
                Assert.Equal("office.dwt", read.QnewTemplate);

                // Reset to defaults puts both back to empty.
                GlobalSettingsViewModel again = model.CreateGlobalSettings("Save and export");
                again.Reset();
                Assert.Equal(string.Empty, again.Row("Save and export", "Default template for QNEW").Value);
            }
        }

        [Fact]
        public void TheApplicationMenusDrawingTemplate_IsBuiltAndEnabledWithADrawingOpen_AndNewShowsSelectTemplate()
        {
            var model = new MainViewModel();

            AppMenuItemViewModel template = model
                .CreateAppMenu()
                .Items.Single(i => i.Key == "saveas")
                .Items.Single(i => i.Name == "Drawing Template");
            Assert.False(template.IsEnabled);

            model.NewDrawing();
            AppMenuViewModel menu = model.CreateAppMenu();
            template = menu.Items.Single(i => i.Key == "saveas").Items.Single(i => i.Name == "Drawing Template");
            Assert.True(template.IsEnabled);
            Assert.DoesNotContain("not built", template.Description);

            // New › Drawing starts NEW's dialog: with none shown, the drawing QNEW makes.
            AppMenuItemViewModel newDrawing = menu.Items.Single(i => i.Key == "new").Items.Single();
            SelectTemplateViewModel shown = null;
            model.ShowSelectTemplate = dialog =>
            {
                shown = dialog;
                dialog.OpenImperial();
                return true;
            };
            newDrawing.Command.Execute(null);
            Assert.NotNull(shown);
            Assert.Equal(DxfMeasurementValue.English, model.Document.Database.Measurement);
        }
    }
}

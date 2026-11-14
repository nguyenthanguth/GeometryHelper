using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The dialogs of rounds 5 to 9 that <c>--dialog</c> draws in screenshot mode: Multiline Style and its pages, Multiline
    /// Edit Tools, Plot Stamp and its pages, Image Adjust, Attribute Extraction, Xbind, Insert Object, the Drawing Recovery
    /// Manager, Check Spelling and its settings, Annotation Object Scale and Add Scales to Object, Insert Layout(s),
    /// Template Options, EXPORTPDF's three dialogs, Rename, Lineweight Settings, Boundary Creation, Viewports and Object
    /// Selection Filters. Each is made as its command makes it, over the drawing open, with a sample state: the styles,
    /// scales, references and filters a drawing of the kind would hold.
    /// </summary>
    internal static class CadAppDialogs
    {
        /// <summary>The names <c>--dialog</c> takes for these dialogs.</summary>
        internal static readonly string[] Names =
        {
            "mlstyle",
            "mlstyle-new",
            "mlstyle-new-page",
            "mlstyle-modify",
            "mlstyle-load",
            "mledit",
            "plotstamp",
            "plotstamp-advanced",
            "plotstamp-fields",
            "imageadjust",
            "attext",
            "xbind",
            "insertobject",
            "recovery",
            "spell",
            "spell-settings",
            "objectscale",
            "objectscale-add",
            "insertlayout",
            "templateoptions",
            "saveaspdf",
            "pdf-override",
            "pdf-options",
            "rename",
            "lweight",
            "boundary",
            "viewports",
            "viewports-named",
            "filter",
        };

        /// <summary>Gets whether a <c>--dialog</c> names one of these dialogs.</summary>
        /// <param name="dialogName">The name given to <c>--dialog</c>.</param>
        /// <returns>True when it is one of <see cref="Names"/>, whatever its case.</returns>
        internal static bool Knows(string dialogName) =>
            dialogName != null && Names.Contains(dialogName, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Makes the window of a dialog over the drawing open, the lines of <c>--run</c> typed first. A dialog the drawing
        /// cannot show (Image Adjust with no picture in it) writes why to the error output.
        /// </summary>
        /// <param name="model">The view model with a drawing open.</param>
        /// <param name="dialogName">The name given to <c>--dialog</c>, one of <see cref="Names"/>.</param>
        /// <param name="run">The lines of <c>--run</c>, typed first and then set to null.</param>
        /// <param name="window">The dialog's window, not shown.</param>
        /// <param name="root">The content of the window that is drawn.</param>
        /// <returns>False when the drawing lacks what the dialog is made of.</returns>
        internal static bool TryCreate(
            MainViewModel model,
            string dialogName,
            ref string run,
            out Window window,
            out FrameworkElement root
        )
        {
            window = null;
            root = null;
            string kind = dialogName.ToLowerInvariant();

            foreach (string line in run == null ? Array.Empty<string>() : run.Split(';'))
            {
                model.Execute(line);
            }

            run = null;
            CadDocument document = model.Document;
            string why = null;

            switch (kind)
            {
                case "mlstyle":
                case "mlstyle-new":
                case "mlstyle-new-page":
                case "mlstyle-modify":
                case "mlstyle-load":
                    (window, root) = MlineStyle(model, kind);
                    break;
                case "mledit":
                    MlineEditToolsViewModel tools = null;
                    model.ShowMlineEditTools = dialog => tools = dialog;
                    model.Execute("MLEDIT");
                    model.ShowMlineEditTools = null;
                    if (!Showed(tools, ref why))
                    {
                        break;
                    }

                    var toolsWindow = new MlineEditToolsWindow(tools);
                    (window, root) = (toolsWindow, toolsWindow.Body);
                    break;
                case "plotstamp":
                case "plotstamp-advanced":
                case "plotstamp-fields":
                    (window, root) = PlotStamp(model, kind);
                    break;
                case "imageadjust":
                    string[] images = document
                        .CurrentSpace.Block.OfType<DxfRasterImage>()
                        .Where(i => !(i is DxfWipeout))
                        .Select(i => i.Handle)
                        .ToArray();

                    if (images.Length == 0)
                    {
                        why = "the space shown has no image (--run \"-IMAGE;...\").";
                        break;
                    }

                    ImageAdjustViewModel adjusting = null;
                    model.ShowImageAdjust = dialog =>
                    {
                        adjusting = dialog;
                        return false;
                    };
                    model.Execute("IMAGEADJUST");
                    model.Session.FeedSelection(images);
                    model.Execute(string.Empty);
                    model.ShowImageAdjust = null;
                    if (!Showed(adjusting, ref why))
                    {
                        break;
                    }

                    var adjustWindow = new ImageAdjustWindow(adjusting);
                    (window, root) = (adjustWindow, adjustWindow.Body);
                    break;
                case "attext":
                    AttributeExtractionViewModel extracting = null;
                    model.ShowAttributeExtraction = dialog => extracting = dialog;
                    model.Execute("ATTEXT");
                    model.ShowAttributeExtraction = null;
                    if (!Showed(extracting, ref why))
                    {
                        break;
                    }

                    var extractWindow = new AttributeExtractionWindow(extracting);
                    (window, root) = (extractWindow, extractWindow.Body);
                    break;
                case "xbind":
                    XbindViewModel binding = null;
                    model.ShowXbind = dialog =>
                    {
                        binding = dialog;
                        return false;
                    };
                    model.Execute("XBIND");
                    model.ShowXbind = null;

                    if (binding == null || binding.Xrefs.Count == 0)
                    {
                        why = "the drawing has no external reference (--run \"-XREF;A;grid.dxf;0,0;;;\").";
                        break;
                    }

                    // The first reference open to its kinds, the first kind to its definitions and listed to bind.
                    binding.Toggle(binding.Xrefs[0]);
                    XbindNode kindRow = binding.Xrefs.FirstOrDefault(r => r.Depth == 1);

                    if (kindRow != null)
                    {
                        binding.Toggle(kindRow);
                        binding.Picked = kindRow;
                        binding.Add();
                        binding.Chosen = binding.ToBind.FirstOrDefault();
                        binding.Picked = binding.ShownXrefs.FirstOrDefault(r => r.Depth == 2);
                    }

                    var bindWindow = new XbindWindow(binding);
                    (window, root) = (bindWindow, bindWindow.Body);
                    break;
                case "insertobject":
                    InsertObjectViewModel inserting = null;
                    model.AskExportFile = job => { };
                    model.ShowInsertObject = dialog =>
                    {
                        inserting = dialog;
                        dialog.File = @"D:\Plans\Workshop A\details\gate.dxf";
                        return false;
                    };
                    model.Execute("INSERTOBJ");
                    model.ShowInsertObject = null;
                    model.AskExportFile = null;
                    if (!Showed(inserting, ref why))
                    {
                        break;
                    }

                    var objectWindow = new InsertObjectWindow(inserting);
                    (window, root) = (objectWindow, objectWindow.Body);
                    break;
                case "recovery":
                    var recoveryWindow = new DrawingRecoveryWindow(RecoveryCopies());
                    (window, root) = (recoveryWindow, recoveryWindow.Body);
                    break;
                case "spell":
                case "spell-settings":
                    (window, root) = Spelling(model, kind);
                    break;
                case "objectscale":
                case "objectscale-add":
                    string[] annotative = document
                        .CurrentSpace.Block.Cast<DxfEntity>()
                        .Where(e => e.Annotative == DxfAnnotativeStates.True)
                        .Select(e => e.Handle)
                        .ToArray();

                    if (annotative.Length == 0)
                    {
                        why = "the space shown has no annotative object.";
                        break;
                    }

                    ObjectScaleViewModel scaling = null;
                    model.ShowObjectScale = dialog =>
                    {
                        scaling = dialog;
                        return false;
                    };
                    model.Execute("OBJECTSCALE");
                    model.Session.FeedSelection(annotative);
                    model.Execute(string.Empty);
                    model.ShowObjectScale = null;
                    if (!Showed(scaling, ref why))
                    {
                        break;
                    }

                    if (kind == "objectscale")
                    {
                        scaling.Picked = scaling.Scales.FirstOrDefault();
                        var scaleWindow = new ObjectScaleWindow(scaling);
                        (window, root) = (scaleWindow, scaleWindow.Body);
                    }
                    else
                    {
                        var picker = new ObjectScaleAddViewModel(scaling.AddChoices);

                        foreach (ObjectScaleChoiceViewModel choice in picker.Scales.Take(2))
                        {
                            choice.IsPicked = true;
                        }

                        var addWindow = new ObjectScaleAddWindow(picker);
                        (window, root) = (addWindow, addWindow.Body);
                    }

                    break;
                case "insertlayout":
                case "templateoptions":
                    (window, root, why) = LayoutTemplate(model, kind);
                    break;
                case "saveaspdf":
                case "pdf-override":
                case "pdf-options":
                    (window, root) = ExportPdf(model, kind);
                    break;
                case "rename":
                    RenameViewModel renaming = null;
                    model.ShowRename = dialog =>
                    {
                        renaming = dialog;
                        return false;
                    };
                    model.Execute("RENAME");
                    model.ShowRename = null;
                    if (!Showed(renaming, ref why))
                    {
                        break;
                    }

                    // Layers: the first renamed at once, the second picked with a name typed and not yet taken.
                    renaming.Kind = "Layers";
                    renaming.Item = renaming.Items.FirstOrDefault();
                    renaming.NewName = "A-" + renaming.OldName;
                    renaming.RenameTo();
                    renaming.Item = renaming.Items.Skip(1).FirstOrDefault() ?? renaming.Item;
                    renaming.NewName = "A-" + renaming.OldName;
                    var renameWindow = new RenameWindow(renaming);
                    (window, root) = (renameWindow, renameWindow.Body);
                    break;
                case "lweight":
                    LineweightSettingsViewModel weights = null;
                    model.ShowLineweightSettings = dialog =>
                    {
                        weights = dialog;
                        return false;
                    };
                    model.Execute("LWEIGHT");
                    model.ShowLineweightSettings = null;
                    if (!Showed(weights, ref why))
                    {
                        break;
                    }

                    var weightWindow = new LineweightSettingsWindow(weights);
                    (window, root) = (weightWindow, weightWindow.Body);
                    break;
                case "boundary":
                    BoundaryCreationViewModel creating = null;
                    model.ShowBoundaryCreation = dialog =>
                    {
                        creating = dialog;
                        return false;
                    };
                    model.Execute("BOUNDARY");
                    model.ShowBoundaryCreation = null;
                    model.Escape();
                    if (!Showed(creating, ref why))
                    {
                        break;
                    }

                    var boundaryWindow = new BoundaryCreationWindow(creating);
                    (window, root) = (boundaryWindow, boundaryWindow.Body);
                    break;
                case "viewports":
                case "viewports-named":
                    (window, root) = Viewports(model, kind);
                    break;
                default:
                    // A layer and a kind of object in the list, and the Layer line being made for the next one.
                    var filter = new SelectionFiltersViewModel(document, new CadSelectionFilter());
                    filter.Choice = filter.Choices.First(c => c.Name == "Layer");
                    filter.Value = filter.Values.FirstOrDefault() ?? "0";
                    filter.AddToList();
                    filter.Choice = filter.Choices.First(c =>
                        c.Property == CadFilterProperty.Object && c.Value != null
                    );
                    filter.AddToList();
                    filter.Choice = filter.Choices.First(c => c.Name == "Layer");
                    filter.SelectedLine = 0;
                    var filterWindow = new SelectionFiltersWindow(filter);
                    (window, root) = (filterWindow, filterWindow.Body);
                    break;
            }

            if (why != null)
            {
                Console.Error.WriteLine("--dialog " + dialogName + ": " + why);
                return false;
            }

            if (window == null)
            {
                Console.Error.WriteLine("--dialog " + dialogName + ": the command showed no dialog.");
                return false;
            }

            return true;
        }

        /// <summary>Whether a command showed its dialog; where it did not, says so in <paramref name="why"/>.</summary>
        private static bool Showed(object dialog, ref string why)
        {
            if (dialog != null)
            {
                return true;
            }

            why = "the command showed no dialog.";
            return false;
        }

        /// <summary>
        /// Multiline Style over MLSTYLE, WALL-240 added to a drawing that has only STANDARD (elements 120, 0 and -120, red
        /// end caps off) and picked; <c>mlstyle-new</c> asks the name PARTITION, <c>mlstyle-new-page</c> shows the page that
        /// follows it, <c>mlstyle-modify</c> the page of WALL-240 and <c>mlstyle-load</c> Load Multiline Styles.
        /// </summary>
        private static (Window, FrameworkElement) MlineStyle(MainViewModel model, string kind)
        {
            MlineStyleViewModel styles = null;
            object page = null;
            model.ShowMlineStyle = dialog =>
            {
                styles = dialog;

                if (!dialog.Names.Contains("WALL-240", StringComparer.OrdinalIgnoreCase))
                {
                    dialog.AskNewStyle = naming =>
                    {
                        naming.Name = "Wall-240";
                        return true;
                    };
                    dialog.EditStyle = editing =>
                    {
                        editing.Description = "Wall 240 mm, centre line";
                        editing.Element = editing.Elements[0];
                        editing.Offset = "120";
                        editing.ElementColor = "Red";
                        editing.Element = editing.Elements[1];
                        editing.Offset = "-120";
                        editing.Add();
                        return editing.TryAccept();
                    };
                    dialog.New();
                }

                dialog.Selected = dialog.Names.FirstOrDefault(n => n == "WALL-240") ?? dialog.Selected;
                dialog.AskNewStyle = naming =>
                {
                    naming.Name = "Partition";
                    page = naming;
                    return kind != "mlstyle-new";
                };
                dialog.EditStyle = editing =>
                {
                    page = editing;
                    return false;
                };
                string styleFile = Path.Combine(
                    Path.GetTempPath(),
                    "GeometryHelper",
                    "ScreenshotStyles",
                    "workshop.mln"
                );
                dialog.AskLoad = loading =>
                {
                    // The file of styles WALL-240 is saved to, so that the list has one to offer.
                    Directory.CreateDirectory(Path.GetDirectoryName(styleFile));
                    dialog.AskSaveFile = name => styleFile;
                    dialog.Save();
                    loading.File = styleFile;
                    page = loading;
                    return false;
                };

                if (kind == "mlstyle-new" || kind == "mlstyle-new-page")
                {
                    dialog.New();
                }
                else if (kind == "mlstyle-modify")
                {
                    dialog.Modify();
                }
                else if (kind == "mlstyle-load")
                {
                    dialog.Load();
                }

                dialog.AskNewStyle = null;
                dialog.EditStyle = null;
                dialog.AskLoad = null;
                return false;
            };
            model.Execute("MLSTYLE");
            model.ShowMlineStyle = null;

            if (styles == null)
            {
                return (null, null);
            }

            switch (page)
            {
                case MlineStyleNewViewModel naming:
                    var nameWindow = new MlineStyleNewWindow(naming);
                    return (nameWindow, nameWindow.Body);
                case MlineStyleEditViewModel editing:
                    var editWindow = new MlineStyleEditWindow(editing);
                    return (editWindow, editWindow.Body);
                case MlineStyleLoadViewModel loading:
                    var loadWindow = new MlineStyleLoadWindow(loading);
                    return (loadWindow, loadWindow.Body);
                default:
                    var listWindow = new MlineStyleWindow(styles);
                    return (listWindow, listWindow.Body);
            }
        }

        /// <summary>
        /// Plot Stamp over PLOTSTAMP, as a drawing named, laid out and dated, with a login name and two user fields; its
        /// Advanced Options, or the User Defined Fields list with four fields.
        /// </summary>
        private static (Window, FrameworkElement) PlotStamp(MainViewModel model, string kind)
        {
            PlotStampViewModel stamp = null;
            model.ShowPlotStamp = dialog =>
            {
                stamp = dialog;
                dialog.LayoutName = true;
                dialog.DateAndTime = true;
                dialog.LoginName = true;
                dialog.UserField1 = "Level 2";
                dialog.UserField2 = "Revision C";
                return false;
            };
            model.Execute("PLOTSTAMP");
            model.ShowPlotStamp = null;

            if (stamp == null)
            {
                return (null, null);
            }

            switch (kind)
            {
                case "plotstamp-advanced":
                    var advancedWindow = new PlotStampAdvancedWindow(stamp);
                    return (advancedWindow, advancedWindow.Body);
                case "plotstamp-fields":
                    var fieldsWindow = new UserDefinedFieldsWindow(
                        new UserDefinedFieldsViewModel(
                            new[] { "Level 2", "Revision C", "Issued for tender", "Checked by" }
                        )
                    );
                    return (fieldsWindow, fieldsWindow.Body);
                default:
                    var window = new PlotStampWindow(stamp);
                    return (window, window.Body);
            }
        }

        /// <summary>
        /// Check Spelling over SPELL, on the entire drawing and started, so that the first word it does not know is shown with
        /// its suggestions; <c>spell-settings</c> is its Settings dialog.
        /// </summary>
        private static (Window, FrameworkElement) Spelling(MainViewModel model, string kind)
        {
            CheckSpellingViewModel check = null;
            model.ShowCheckSpelling = dialog => check = dialog;
            model.Execute("SPELL");
            model.ShowCheckSpelling = null;

            if (check == null)
            {
                return (null, null);
            }

            if (kind == "spell-settings")
            {
                var settingsWindow = new CheckSpellingSettingsWindow(
                    new CheckSpellingSettingsViewModel(check.Check.Settings)
                );
                return (settingsWindow, settingsWindow.Body);
            }

            check.Where = check.Places[0];
            check.Start();
            var window = new CheckSpellingWindow(check);
            return (window, window.Body);
        }

        /// <summary>
        /// Insert Layout(s) over LAYOUT Template on a template written in the temporary folder with the sheets Plan, Sect and
        /// Cover, or Template Options over LAYOUT SAveas to a .dwt there.
        /// </summary>
        private static (Window, FrameworkElement, string) LayoutTemplate(MainViewModel model, string kind)
        {
            string folder = Path.Combine(Path.GetTempPath(), "GeometryHelper", "ScreenshotTemplates");
            Directory.CreateDirectory(folder);
            string template = Path.Combine(folder, "sheets.dxf");
            DxfDatabase sheets = CadTemplate.CreateMetric();

            foreach (string sheet in new[] { "Plan", "Sect", "Cover" })
            {
                var layout = new DxfLayout { LayoutName = sheet };
                layout.AddToLayoutDictionary(sheets, null);
                layout.Initialize();
                layout.BlockTableRecord.AppendEntity(new DxfLine(new GeoPoint3(10, 10, 0), new GeoPoint3(100, 50, 0)));
            }

            sheets.Save(template);

            if (kind == "insertlayout")
            {
                InsertLayoutViewModel layouts = null;
                model.AskExportFile = job => job.FileName = template;
                model.ShowInsertLayout = dialog =>
                {
                    layouts = dialog;
                    return false;
                };
                model.Execute("LAYOUT");
                model.Execute("T");
                model.ShowInsertLayout = null;
                model.AskExportFile = null;

                if (layouts == null)
                {
                    return (null, null, "the command showed no dialog.");
                }

                layouts.Layouts.Skip(1).FirstOrDefault().IsPicked = true;
                var window = new InsertLayoutWindow(layouts);
                return (window, window.Body, null);
            }

            DxfLayout current = model
                .Document.Database.Layouts.Where(l =>
                    !string.Equals(l.LayoutName, "Model", StringComparison.OrdinalIgnoreCase)
                )
                .FirstOrDefault();

            if (current == null)
            {
                return (null, null, "the drawing has no layout to save as a template.");
            }

            TemplateOptionsViewModel options = null;
            model.AskExportFile = job => job.FileName = Path.Combine(folder, "workshop-sheet.dwt");
            model.ShowTemplateOptions = dialog =>
            {
                options = dialog;
                dialog.Description = "Workshop sheet A1, plan at 1:50, title block and stamp.";
                return false;
            };
            model.Execute("LAYOUT");
            model.Execute("SA");
            model.Execute(current.LayoutName);
            model.ShowTemplateOptions = null;
            model.AskExportFile = null;

            if (options == null)
            {
                return (null, null, "the command showed no dialog.");
            }

            var optionsWindow = new TemplateOptionsWindow(options);
            return (optionsWindow, optionsWindow.Body, null);
        }

        /// <summary>
        /// EXPORTPDF's Save As PDF over the space shown, the file saved beside the drawing; <c>pdf-override</c> is its Page
        /// Setup Override on that space's settings and <c>pdf-options</c> its Export to PDF Options.
        /// </summary>
        private static (Window, FrameworkElement) ExportPdf(MainViewModel model, string kind)
        {
            SaveAsPdfViewModel saving = null;
            PageSetupOverrideViewModel overriding = null;
            ExportPdfOptionsViewModel options = null;
            model.ShowSaveAsPdf = dialog =>
            {
                saving = dialog;
                dialog.ShowPageSetupOverride = page =>
                {
                    overriding = page;
                    return false;
                };
                dialog.ShowOptions = page =>
                {
                    options = page;
                    return false;
                };
                dialog.OverridePageSetup();
                dialog.EditOptions();
                dialog.ShowPageSetupOverride = null;
                dialog.ShowOptions = null;
                return false;
            };
            model.Execute("EXPORTPDF");
            model.ShowSaveAsPdf = null;

            if (saving == null || overriding == null || options == null)
            {
                return (null, null);
            }

            switch (kind)
            {
                case "pdf-override":
                    var overrideWindow = new PageSetupOverrideWindow(overriding);
                    return (overrideWindow, overrideWindow.Body);
                case "pdf-options":
                    var optionsWindow = new ExportPdfOptionsWindow(options);
                    return (optionsWindow, optionsWindow.Body);
                default:
                    var window = new SaveAsPdfWindow(saving);
                    return (window, window.Body);
            }
        }

        /// <summary>
        /// Viewports over VPORTS on the model space, Three: Right with the second tile picked; <c>viewports-named</c> shows
        /// its Named Viewports tab with two configurations saved and the first one's name in Rename.
        /// </summary>
        private static (Window, FrameworkElement) Viewports(MainViewModel model, string kind)
        {
            // Two configurations saved from the display, which is made a single viewport again.
            if (kind == "viewports-named")
            {
                CadTiledViewports tiles = model.Document.TiledViewports;
                tiles.SplitAs("Three: Right", true);
                tiles.Save("Plan and section");
                tiles.SplitAs("Four: Equal", true);
                tiles.Save("Quad");
                tiles.Single();
            }

            ViewportsViewModel viewports = null;
            model.ShowViewports = dialog =>
            {
                viewports = dialog;

                if (kind == "viewports-named")
                {
                    dialog.IsNamedTab = true;
                }
                else
                {
                    dialog.Configuration = "Three: Right";
                    dialog.PickedTile = 1;
                    dialog.ChangeViewTo = dialog.Views.Skip(1).FirstOrDefault() ?? dialog.ChangeViewTo;
                }

                return false;
            };
            model.Execute("VPORTS");
            model.ShowViewports = null;

            if (viewports == null)
            {
                return (null, null);
            }

            var window = new ViewportsWindow(viewports);
            return (window, window.Body);
        }

        /// <summary>The Drawing Recovery Manager over three copies autosave kept of two drawings, written in the temporary folder.</summary>
        private static DrawingRecoveryViewModel RecoveryCopies()
        {
            string folder = Path.Combine(Path.GetTempPath(), "GeometryHelper", "ScreenshotRecovery");
            Directory.CreateDirectory(folder);
            var rows = new List<BackupFileRow>();

            foreach (
                string[] copy in new[]
                {
                    new[] { "plan-level2.autosave1.dxf", "plan-level2.dxf", "180" },
                    new[] { "plan-level2.autosave2.dxf", "plan-level2.dxf", "420" },
                    new[] { "site-survey.autosave1.dxf", "site-survey.dxf", "96" },
                }
            )
            {
                string path = Path.Combine(folder, copy[0]);
                File.WriteAllText(path, new string('0', int.Parse(copy[2]) * 1024));
                rows.Add(new BackupFileRow(path, copy[1]));
            }

            return new DrawingRecoveryViewModel(rows, path => { });
        }
    }
}

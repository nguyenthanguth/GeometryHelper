using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The object menus of AutoCAD 2021 the Edit menu takes in for the objects selected, as AutoCAD merges them: those of its
    /// menu file (POP504 to POP536) by the kind of object, OBJECT_ for one object and OBJECTS_ for several of one kind, and
    /// those its table and image modules add. Each item runs the presenter's own command or edit; one it does not build is
    /// grey, with "Not built yet.".
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>
        /// The items of the object menu of the objects selected, as AutoCAD 2021 merges them into the Edit menu: with one
        /// object selected the menu of its kind (OBJECT_TEXT), or the one for several where its kind has only that
        /// (OBJECTS_DIMENSION); with several of one kind the menu for several (OBJECTS_HATCH, OBJECTS_XREF, OBJECTS_VIEWPORT,
        /// OBJECTS_DIMENSION), none where the kind has no such menu; none for objects of more than one kind. A line has none.
        /// </summary>
        private List<CadMenuItemViewModel> ObjectMenu()
        {
            var items = new List<CadMenuItemViewModel>();
            CadDocument document = _current?.Document;

            if (document == null)
            {
                return items;
            }

            var handles = new List<string>(document.Selection.Handles);
            handles.Sort(StringComparer.OrdinalIgnoreCase);
            var kinds = new SortedSet<string>(StringComparer.Ordinal);

            foreach (string handle in handles)
            {
                kinds.Add(ObjectKind(document, document.FindEntity(handle)) ?? string.Empty);
            }

            if (handles.Count == 0 || kinds.Count != 1)
            {
                return items;
            }

            bool one = handles.Count == 1;

            switch (kinds.Min)
            {
                case "DIMENSION":
                    items.Add(DimensionStyleMenu(handles));
                    items.Add(
                        Submenu(
                            "Precision",
                            Enumerable
                                .Range(0, 7)
                                .Select(p => p == 0 ? "0" : "0." + new string('0', p))
                                .Select(p => PropertyItem(p, handles, "Precision", p))
                                .ToList(),
                            'r'
                        )
                    );
                    items.Add(NotBuilt("Remove Style Overrides", 'O'));
                    items.Add(NotBuilt("Edit Constraint"));
                    items.Add(
                        Submenu(
                            "Dimension Name Format",
                            new[] { NotBuilt("Value"), NotBuilt("Name"), NotBuilt("Name and Expression") }
                        )
                    );
                    items.Add(AnnotativeScaleMenu());
                    break;
                case "TEXT" when one:
                    items.Add(new CadMenuItemViewModel("Edit...", () => EditTextAt(handles[0]), accessKey: 'i'));
                    items.Add(AnnotativeScaleMenu());
                    break;
                case "MTEXT" when one:
                    items.Add(new CadMenuItemViewModel("Mtext Edit...", () => EditTextAt(handles[0]), accessKey: 'i'));
                    items.Add(AnnotativeScaleMenu());
                    break;
                case "ATTDEF" when one:
                case "LEADER" when one:
                case "MULTILEADER" when one:
                    items.Add(AnnotativeScaleMenu());
                    break;
                case "HATCH":
                    if (one)
                    {
                        items.Add(OnSelection("Hatch Edit...", "HATCHEDIT"));
                    }

                    items.Add(NotBuilt("Set Origin"));

                    if (one)
                    {
                        items.Add(NotBuilt("Set Boundary"));
                        items.Add(NotBuilt("Generate Boundary"));
                        items.Add(AnnotativeScaleMenu());
                    }

                    break;
                case "LWPOLYLINE" when one:
                case "POLYLINE" when one:
                case "3DPOLYLINE" when one:
                    items.Add(PolylineMenu(kinds.Min == "3DPOLYLINE"));
                    break;
                case "POLYGONMESH" when one:
                    // PEDIT edits no mesh yet.
                    items.Add(Submenu("Polygon Mesh", new[] { NotBuilt("Edit", 'E') }));
                    break;
                case "SPLINE" when one:
                    items.Add(SplineMenu());
                    break;
                case "BLOCKREF" when one:
                case "ATTBLOCKREF" when one:
                    items.Add(OnSelection("Reset Block", "RESETBLOCK", 'R'));
                    items.Add(MenuCommand("Block Editor", "BEDIT"));
                    items.Add(NotBuilt("Edit Block In-place", 'i'));

                    if (kinds.Min == "ATTBLOCKREF")
                    {
                        items.Add(OnSelection("Edit Attribute...", "EATTEDIT", 'u'));
                    }

                    items.Add(AnnotativeScaleMenu());
                    break;
                case "XREF":
                    if (one)
                    {
                        items.Add(NotBuilt("Edit Xref In-place", 'd'));
                    }

                    items.Add(OnSelection("Open Xref", "XOPEN", 'X'));
                    items.Add(OnSelection("Clip Xref", "XCLIP", 'i'));
                    items.Add(MenuCommand("External References...", "EXTERNALREFERENCES", 'n'));
                    break;
                case "VIEWPORT":
                    items.AddRange(ViewportMenu(handles));
                    break;
                case "TABLE" when one:
                    items.AddRange(TableMenu(handles[0]));
                    break;
                case "IMAGE":
                    items.Add(
                        Submenu(
                            "Image",
                            new[]
                            {
                                NotBuilt("Adjust"),
                                one ? OnSelection("Clip", "IMAGECLIP") : NotBuilt("Clip"),
                                OnSelection("Transparency", "TRANSPARENCY"),
                                MenuCommand("External References", "EXTERNALREFERENCES"),
                            }
                        )
                    );
                    break;
            }

            return items;
        }

        /// <summary>
        /// The kind of an object by the name of its object menu in AutoCAD's menu file: DIMENSION, TEXT, MTEXT, ATTDEF, HATCH,
        /// LWPOLYLINE, POLYLINE (2D), 3DPOLYLINE, POLYGONMESH, SPLINE, LEADER, MULTILEADER, BLOCKREF, ATTBLOCKREF (with
        /// attributes), XREF, VIEWPORT, and TABLE and IMAGE of AutoCAD's table and image modules; null for one with no menu.
        /// </summary>
        private static string ObjectKind(CadDocument document, DxfEntity entity)
        {
            switch (entity)
            {
                case DxfDimension _:
                    return "DIMENSION";
                case DxfAttributeDefinition _:
                    return "ATTDEF";
                case DxfAttributeReference _:
                    return null;
                case DxfDBText _:
                    return "TEXT";
                case DxfMText _:
                    return "MTEXT";
                case DxfHatch _:
                    return "HATCH";
                case DxfPolyline _:
                    return "LWPOLYLINE";
                case DxfPolyline2d _:
                    return "POLYLINE";
                case DxfPolyline3d _:
                    return "3DPOLYLINE";
                case DxfPolygonMesh _:
                    return "POLYGONMESH";
                case DxfSpline _:
                    return "SPLINE";
                case DxfLeader _:
                    return "LEADER";
                case DxfMLeader _:
                    return "MULTILEADER";
                case DxfMInsertBlock _:
                    return null;
                case DxfBlockReference insert:
                    DxfBlockTableRecord block = document.Database.BlockTable[insert.Name];

                    return block != null && block.IsFromExternalReference ? "XREF"
                        : insert.AttributeCollection.Count > 0 ? "ATTBLOCKREF"
                        : "BLOCKREF";
                case DxfViewport _:
                    return "VIEWPORT";
                case DxfTable _:
                    return "TABLE";
                case DxfWipeout _:
                    return null;
                case DxfRasterImage _:
                    return "IMAGE";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Dimension Style of the dimension menu: Save as New Style and Other grey, and between them the drawing's first six
        /// dimension styles, each setting the dimensions selected to it, as AutoCAD lists the six it used last.
        /// </summary>
        private CadMenuItemViewModel DimensionStyleMenu(IReadOnlyList<string> handles)
        {
            var styles = new List<CadMenuItemViewModel> { NotBuilt("Save as New Style...", 'S') };
            List<string> names = CadDimStyleEditor
                .Styles(_current.Document.Database)
                .Select(s => s.Name)
                .Take(6)
                .ToList();

            if (names.Count > 0)
            {
                styles.Add(CadMenuItemViewModel.Separator());
                styles.AddRange(names.Select(n => PropertyItem(n, handles, "Dim style", n)));
            }

            styles.Add(NotBuilt("Other...", 'O'));
            return Submenu("Dimension Style", styles, 'D');
        }

        /// <summary>Annotative Object Scale of AutoCAD's object menus: grey, the presenter keeping no annotative scales.</summary>
        private static CadMenuItemViewModel AnnotativeScaleMenu() =>
            Submenu(
                "Annotative Object Scale",
                new[]
                {
                    NotBuilt("Add Current Scale", 'A'),
                    NotBuilt("Delete Current Scale", 'D'),
                    NotBuilt("Add/Delete Scales...", 'c'),
                    CadMenuItemViewModel.Separator(),
                    NotBuilt("Synchronize Multiple-scale Positions", 'S'),
                }
            );

        /// <summary>Polyline of the polyline menus: PEDIT and its options on the polyline selected, as AutoCAD's macros run them.</summary>
        private CadMenuItemViewModel PolylineMenu(bool space)
        {
            var items = new List<CadMenuItemViewModel>
            {
                OnSelection("Edit Polyline", "PEDIT", 'E'),
                CadMenuItemViewModel.Separator(),
                OnSelection("Close", "PEDIT Close ;", 'C'),
                OnSelection("Open", "PEDIT Open ;", 'O'),
                CadMenuItemViewModel.Separator(),
                OnSelection("Join", "PEDIT Join", 'J'),
                OnSelection("Reverse", "PEDIT Reverse ;", 'R'),
            };

            // The width is typed, and PEDIT then asks its options again, as AutoCAD's macro pauses for it.
            if (!space)
            {
                items.Add(OnSelection("Width", "PEDIT Width", 'W'));
            }

            items.Add(CadMenuItemViewModel.Separator());

            if (!space)
            {
                items.Add(OnSelection("Curve Fit", "PEDIT Fit ;", 'F'));
            }

            items.Add(OnSelection("Spline Fit", "PEDIT Spline ;", 'S'));
            items.Add(OnSelection("Decurve", "PEDIT Decurve ;", 'D'));
            return Submenu("Polyline", items);
        }

        /// <summary>Spline of the spline menu: grey, the presenter having no SPLINEDIT.</summary>
        private static CadMenuItemViewModel SplineMenu() =>
            Submenu(
                "Spline",
                new[]
                {
                    NotBuilt("Display Fit Points", 'F'),
                    NotBuilt("Display Control Vertices", 'C'),
                    CadMenuItemViewModel.Separator(),
                    NotBuilt("Add Fit Point", 'P'),
                    NotBuilt("Add Fit Kink", 'K'),
                    CadMenuItemViewModel.Separator(),
                    NotBuilt("Add Control Vertex", 'V'),
                    NotBuilt("Delete Control Vertex", 'D'),
                    NotBuilt("Elevate Order", 'E'),
                    CadMenuItemViewModel.Separator(),
                    NotBuilt("3D Edit Bar", 'B'),
                    NotBuilt("Convert to Polyline", 'P'),
                    NotBuilt("Join", 'J'),
                    NotBuilt("Reverse Direction", 'R'),
                    NotBuilt("Close", 'o'),
                },
                'S'
            );

        /// <summary>
        /// The viewport menus: Display Viewport Objects and Display Locked set On and Display locked of the viewports selected,
        /// as Properties does; Remove Viewport Overrides for All Layers takes every layer's overrides out of them, as the Layer
        /// manager does; Maximize Viewport, Viewport Clip and Shade plot grey.
        /// </summary>
        private List<CadMenuItemViewModel> ViewportMenu(IReadOnlyList<string> handles) =>
            new List<CadMenuItemViewModel>
            {
                NotBuilt("Maximize Viewport", 'M'),
                NotBuilt("Viewport Clip", 'V'),
                Submenu(
                    "Display Viewport Objects",
                    new[]
                    {
                        PropertyItem("Yes", handles, "On", "Yes", 'Y'),
                        PropertyItem("No", handles, "On", "No", 'N'),
                    },
                    'D'
                ),
                Submenu(
                    "Display Locked",
                    new[]
                    {
                        PropertyItem("Yes", handles, "Display locked", "Yes", 'Y'),
                        PropertyItem("No", handles, "Display locked", "No", 'N'),
                    },
                    'L'
                ),
                new CadMenuItemViewModel(
                    "Remove Viewport Overrides for All Layers",
                    () => RemoveViewportOverrides(handles),
                    accessKey: 'i'
                ),
                Submenu(
                    "Shade plot",
                    new[]
                    {
                        NotBuilt("As Displayed", 'A'),
                        NotBuilt("Wireframe", 'W'),
                        NotBuilt("Hidden", 'H'),
                        NotBuilt("Rendered", 'R'),
                    },
                    'S'
                ),
            };

        /// <summary>
        /// The table menu of AutoCAD's table module, for a whole table selected: Table Style grey; Size Columns Equally and Size
        /// Rows Equally give every column or row the mean, one undo step; Remove All Property Overrides takes what every cell
        /// holds over the table style away; Export grey; Table Indicator Color as TABLEINDICATOR's colour.
        /// </summary>
        private List<CadMenuItemViewModel> TableMenu(string handle) =>
            new List<CadMenuItemViewModel>
            {
                Submenu(
                    "Table Style",
                    new[] { NotBuilt("Save as New Table Style..."), NotBuilt("Set As Table in Current Table Style") }
                ),
                new CadMenuItemViewModel("Size Columns Equally", () => SizeTableEqually(handle, true)),
                new CadMenuItemViewModel("Size Rows Equally", () => SizeTableEqually(handle, false)),
                new CadMenuItemViewModel("Remove All Property Overrides", () =>
                    TableEdit(d => CadTableEditor.RemoveOverrides(d, handle, WholeTable(d, handle)))
                ),
                NotBuilt("Export..."),
                new CadMenuItemViewModel("Table Indicator Color...", PickTableIndicatorColor),
            };

        /// <summary>Every column (or row) of a table given the mean of their widths (or heights), one undo step.</summary>
        private void SizeTableEqually(string handle, bool columns)
        {
            TableEdit(d =>
            {
                var table = (DxfTable)d.FindEntity(handle);
                int count = columns ? table.NumColumns : table.NumRows;
                double sum = 0.0;

                for (int k = 0; k < count; k++)
                {
                    sum += columns ? table.ColumnWidth(k) : table.RowHeight(k);
                }

                double[] sizes = Enumerable.Repeat(sum / count, count).ToArray();
                return columns
                    ? CadTableEditor.SetColumnWidths(d, handle, 0, sizes)
                    : CadTableEditor.SetRowHeights(d, handle, 0, sizes);
            });
        }

        /// <summary>The range of every cell of a table.</summary>
        private static DxfCellRange WholeTable(CadDocument document, string handle)
        {
            var table = (DxfTable)document.FindEntity(handle);
            return new DxfCellRange(0, 0, table.NumRows - 1, table.NumColumns - 1);
        }

        /// <summary>An edit of a table from the menu, refused or failed said on the command line.</summary>
        private void TableEdit(Func<CadDocument, CadEditResult> edit)
        {
            if (_current == null || _current.Session.IsRunning)
            {
                return;
            }

            CadEditResult result = edit(_current.Document);

            if (!result.Succeeded)
            {
                Say(result.Message);
            }
        }

        /// <summary>Remove Viewport Overrides for All Layers: every layer's overrides taken out of each viewport selected.</summary>
        private void RemoveViewportOverrides(IReadOnlyList<string> handles)
        {
            if (_current == null || _current.Session.IsRunning)
            {
                return;
            }

            CadDocument document = _current.Document;
            List<string> layers = document.Database.LayerTable.Select(l => l.Name).ToList();

            foreach (string handle in handles)
            {
                CadEditResult result = CadViewportLayerEditor.RemoveOverrides(document, layers, handle);

                if (!result.Succeeded)
                {
                    Say(result.Message);
                }
            }
        }

        /// <summary>An item that sets a row of Properties on the objects selected, as "Precision" to "0.00".</summary>
        private CadMenuItemViewModel PropertyItem(
            string header,
            IReadOnlyList<string> handles,
            string label,
            string value,
            char key = '\0'
        ) =>
            new CadMenuItemViewModel(
                header,
                () =>
                {
                    if (_current == null || _current.Session.IsRunning)
                    {
                        return;
                    }

                    CadEditResult result = CadPropertyEditor.Set(_current.Document, handles.ToList(), label, value);

                    if (!result.Succeeded)
                    {
                        Say(result.Message);
                    }
                },
                accessKey: key
            );

        /// <summary>
        /// An item that runs a command on the objects selected, as AutoCAD's object menus run their macros on the pickfirst set:
        /// "PEDIT Close ;" starts PEDIT, gives the object selected to its first prompt, then each word after it to the
        /// prompts that follow, a semicolon being Enter as in AutoCAD's macros. Grey where the drawing has no such command.
        /// </summary>
        private CadMenuItemViewModel OnSelection(string header, string line, char key = '\0')
        {
            string[] words = line.Split(' ');
            return HasCommand(words[0])
                ? new CadMenuItemViewModel(header, () => RunOnSelection(words), accessKey: key)
                : NotBuilt(header, key);
        }

        /// <summary>A command started on the objects selected, the words after its name answering its prompts in turn.</summary>
        private void RunOnSelection(IReadOnlyList<string> words)
        {
            if (_current == null)
            {
                return;
            }

            CadCommandSession session = _current.Session;
            var handles = new List<string>(_current.Document.Selection.Handles);
            handles.Sort(StringComparer.OrdinalIgnoreCase);
            StartCommand(words[0]);

            if (session.Prompt != null && session.Prompt.Kind == CadPromptKind.Entity && handles.Count > 0)
            {
                session.FeedSelection(new[] { handles[0] });
            }
            else if (session.Prompt != null && session.Prompt.Kind == CadPromptKind.Selection && handles.Count > 0)
            {
                session.FeedSelection(handles);
                session.Enter();
            }

            for (int i = 1; i < words.Count && session.IsRunning; i++)
            {
                session.Feed(words[i] == ";" ? string.Empty : words[i]);
            }

            SyncHistory();
            RunAfterCommand();
        }
    }
}

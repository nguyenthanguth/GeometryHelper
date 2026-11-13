using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The canvas's right-click menus, as AutoCAD 2021's with its default SHORTCUTMENU 11: the Default, Edit, Command and
    /// Grips menus of its menu file, their items in its order with its access keys, each mapped to the presenter's own command;
    /// an item the presenter does not build is grey, with "Not built yet.", and the 3D ones (Subobject Selection Filter, 3D
    /// Osnap) are left out.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>
        /// Gets the menu a right-click on the canvas shows, as AutoCAD 2021's: with no command running, the cell menu where
        /// cells of a table are selected (<see cref="TableCellViewModel.Menu"/>), the Edit menu where objects are selected, the
        /// Default menu where none is; while a grip edit runs, the Grips menu; while a command runs whose prompt offers options,
        /// the Command menu. A prompt with no options, as "Specify first point:" or "Select objects:", has none: a right-click
        /// there is Enter, as AutoCAD's SHORTCUTMENU 11 has it. SHORTCUTMENU (<see cref="GlobalSettings.ShortcutMenu"/>) set
        /// otherwise is weighed as AutoCAD weighs it: without 1 no Default menu, without 2 no Edit or cell menu, with 4 the
        /// Command menu at every prompt, with neither 4 nor 8 none; no menu is Enter.
        /// <para>
        /// The Default menu: Repeat and the command run last (once one has run), Clipboard (Cut, Copy, Copy with Base Point,
        /// Paste, Paste as Block, Paste to Original Coordinates), Isolate, Undo, Redo, Pan, Zoom, SteeringWheels, Action
        /// Recorder, Convert PS to MS, Quick Select..., QuickCalc, Find... and Options.... The Edit menu: Repeat, the items of
        /// the object menu AutoCAD merges in for the objects selected (Edit... for one text, Polyline for one polyline, Size
        /// Columns Equally and the rest for one table, Display Locked for viewports; none for objects of more than one kind),
        /// Clipboard, Isolate, Erase, Move, Copy Selection, Scale, Rotate, Draw Order, Group, Add Selected, Select Similar,
        /// Deselect All, Quick Select..., QuickCalc, Find..., Properties and Quick Properties. The Command menu: Enter, Cancel,
        /// Recent Input (the answers typed at prompts of the kind asked, <see cref="CadCommandSession.RecentInput"/>, where there
        /// are any), the prompt's options ("Close" and "Undo" at LINE's third point), Osnap Overrides (the items of
        /// <see cref="SnapOverrideMenu"/>, grey where no point is asked), Pan and Zoom, which run 'PAN and 'ZOOM inside the
        /// command (grey in ZOOM), SteeringWheels and QuickCalc. The Grips menu: Enter, Stretch, Move, Rotate, Scale, Mirror,
        /// Base Point, Copy, Reference (in ROTATE and SCALE), Undo and Exit.
        /// </para>
        /// </summary>
        /// <returns>The items, made anew each time the menu opens; null for no menu.</returns>
        public IReadOnlyList<CadMenuItemViewModel> CanvasMenu()
        {
            if (_current == null)
            {
                return null;
            }

            CadCommandSession session = _current.Session;
            int shown = _settings.ShortcutMenu;

            if (session.IsRunning)
            {
                return !CadPicking.WantsCommandMenu(session, shown) ? null
                    : session.IsGripEditing ? GripsMenu(session)
                    : CommandMenu(session);
            }

            bool selected = _tableCell != null || _current.Document.Selection.Count > 0;

            if ((shown & (selected ? 2 : 1)) == 0)
            {
                return null;
            }

            if (_tableCell != null)
            {
                return _tableCell.Menu();
            }

            return selected ? EditMenu(session) : DefaultMenu(session);
        }

        /// <summary>AutoCAD's Default menu (POP501), with nothing selected and no command running.</summary>
        private List<CadMenuItemViewModel> DefaultMenu(CadCommandSession session)
        {
            CadDocument document = _current.Document;

            // AutoCAD names the item by the space that is current: on the paper of a layout, model space to paper space.
            bool paper = !document.CurrentSpace.IsModel && document.ActiveViewport == null;
            List<CadMenuItemViewModel> items = RepeatItems(session);
            items.Add(ClipboardMenu());
            items.Add(CadMenuItemViewModel.Separator());
            items.Add(IsolateMenu());
            items.Add(CadMenuItemViewModel.Separator());
            items.Add(new CadMenuItemViewModel("Undo", () => Undo(), accessKey: 'U'));
            items.Add(new CadMenuItemViewModel("Redo", () => Redo(), accessKey: 'd'));
            items.Add(MenuCommand("Pan", "PAN", 'a'));
            items.Add(MenuCommand("Zoom", "ZOOM", 'Z'));
            items.Add(NotBuilt("SteeringWheels"));
            items.Add(CadMenuItemViewModel.Separator());
            items.Add(
                new CadMenuItemViewModel(
                    "Action Recorder",
                    false,
                    new[] { NotBuilt("Play"), CadMenuItemViewModel.Separator(), NotBuilt("Record"), NotBuilt("Stop") }
                )
            );
            items.Add(CadMenuItemViewModel.Separator());
            items.Add(NotBuilt(paper ? "Convert MS to PS" : "Convert PS to MS", 'v'));
            items.Add(MenuCommand("Quick Select...", "QSELECT", 'Q'));
            items.Add(NotBuilt("QuickCalc"));
            items.Add(MenuCommand("Find...", "FIND", 'F'));
            items.Add(MenuCommand("Options...", "OPTIONS", 'O'));
            return items;
        }

        /// <summary>AutoCAD's Edit menu (POP502), with objects selected and no command running; the items of the objects first.</summary>
        private List<CadMenuItemViewModel> EditMenu(CadCommandSession session)
        {
            List<CadMenuItemViewModel> items = RepeatItems(session);
            List<CadMenuItemViewModel> objects = ObjectMenu();

            // The items of the objects selected come before the clipboard, as AutoCAD puts its object menus.
            if (objects.Count > 0)
            {
                items.AddRange(objects);
                items.Add(CadMenuItemViewModel.Separator());
            }

            items.Add(ClipboardMenu());
            items.Add(CadMenuItemViewModel.Separator());
            items.Add(IsolateMenu());
            items.Add(CadMenuItemViewModel.Separator());
            items.Add(MenuCommand("Erase", "ERASE"));
            items.Add(MenuCommand("Move", "MOVE", 'M'));
            items.Add(MenuCommand("Copy Selection", "COPY", 'y'));
            items.Add(MenuCommand("Scale", "SCALE", 'l'));
            items.Add(MenuCommand("Rotate", "ROTATE", 'o'));
            items.Add(
                new CadMenuItemViewModel(
                    "Draw Order",
                    true,
                    new[]
                    {
                        DrawOrderItem("Bring to Front", "F", 'F'),
                        DrawOrderItem("Send to Back", "B", 'B'),
                        CadMenuItemViewModel.Separator(),
                        DrawOrderItem("Bring Above Objects", "A", 'A'),
                        DrawOrderItem("Send Under Objects", "U", 'U'),
                    },
                    'w'
                )
            );
            items.Add(
                Submenu(
                    "Group",
                    new[]
                    {
                        MenuCommand("Group", "GROUP"),
                        MenuCommand("Ungroup", "UNGROUP"),
                        NotBuilt("Add to Group"),
                        NotBuilt("Remove from Group"),
                    }
                )
            );
            items.Add(CadMenuItemViewModel.Separator());
            items.Add(NotBuilt("Add Selected", 'd'));
            items.Add(CadMenuItemViewModel.Separator());
            items.Add(NotBuilt("Select Similar", 't'));
            items.Add(new CadMenuItemViewModel("Deselect All", ClearSelection, accessKey: 'A'));
            items.Add(CadMenuItemViewModel.Separator());
            items.Add(MenuCommand("Quick Select...", "QSELECT", 'Q'));
            items.Add(NotBuilt("QuickCalc"));
            items.Add(MenuCommand("Find...", "FIND", 'F'));
            items.Add(new CadMenuItemViewModel("Properties", () => IsPropertiesPanelShown = true, accessKey: 's'));
            items.Add(NotBuilt("Quick Properties"));
            return items;
        }

        /// <summary>AutoCAD's Command menu (POP503), while a command's prompt offers options: those options after Cancel.</summary>
        private List<CadMenuItemViewModel> CommandMenu(CadCommandSession session)
        {
            var items = new List<CadMenuItemViewModel>
            {
                new CadMenuItemViewModel("Enter", Enter, accessKey: 'E'),
                new CadMenuItemViewModel("Cancel", Escape, accessKey: 'C'),
            };
            IReadOnlyList<string> recent = session.RecentInput;

            // The answers typed before at prompts of this kind, newest first, as AutoCAD fills Recent Input as it runs.
            if (recent.Count > 0)
            {
                items.Add(
                    new CadMenuItemViewModel(
                        "Recent Input",
                        true,
                        recent.Select((t, i) => new CadMenuItemViewModel(t, () => ChooseRecentInput(i))).ToList()
                    )
                );
            }

            items.Add(CadMenuItemViewModel.Separator());
            IReadOnlyList<string> keywords = session.Prompt?.Keywords ?? Array.Empty<string>();

            foreach (string keyword in keywords)
            {
                string chosen = keyword;
                items.Add(new CadMenuItemViewModel(keyword, () => ChooseOption(chosen)));
            }

            if (keywords.Count > 0)
            {
                items.Add(CadMenuItemViewModel.Separator());
            }

            items.Add(
                new CadMenuItemViewModel("Osnap Overrides", CadPicking.WantsPoint(session), SnapOverrideItems(), 'v')
            );
            items.Add(CadMenuItemViewModel.Separator());

            // 'PAN and 'ZOOM inside the command, as AutoCAD's; grey in ZOOM, as its menu file greys them there.
            bool zooming = session.Current != null && session.Current.Name == "ZOOM";
            items.Add(new CadMenuItemViewModel("Pan", () => ChooseOption("'PAN"), !zooming, accessKey: 'P'));
            items.Add(new CadMenuItemViewModel("Zoom", () => ChooseOption("'ZOOM"), !zooming, accessKey: 'Z'));
            items.Add(NotBuilt("SteeringWheels"));
            items.Add(CadMenuItemViewModel.Separator());
            items.Add(NotBuilt("QuickCalc"));
            return items;
        }

        /// <summary>AutoCAD's Grips menu (POP500), while a grip edit runs: the modes and the options of its prompt.</summary>
        private List<CadMenuItemViewModel> GripsMenu(CadCommandSession session)
        {
            bool reference = session.Prompt != null && session.Prompt.Keywords.Contains("Reference");
            return new List<CadMenuItemViewModel>
            {
                new CadMenuItemViewModel("Enter", Enter, accessKey: 'E'),
                CadMenuItemViewModel.Separator(),
                new CadMenuItemViewModel("Stretch", () => ChooseOption("STretch"), accessKey: 'S'),
                new CadMenuItemViewModel("Move", () => ChooseOption("MOve"), accessKey: 'M'),
                new CadMenuItemViewModel("Rotate", () => ChooseOption("ROtate"), accessKey: 'R'),
                new CadMenuItemViewModel("Scale", () => ChooseOption("SCale"), accessKey: 'l'),
                new CadMenuItemViewModel("Mirror", () => ChooseOption("MIrror"), accessKey: 'i'),
                CadMenuItemViewModel.Separator(),
                new CadMenuItemViewModel("Base Point", () => ChooseOption("Base point"), accessKey: 'B'),
                new CadMenuItemViewModel("Copy", () => ChooseOption("Copy"), accessKey: 'C'),
                new CadMenuItemViewModel("Reference", () => ChooseOption("Reference"), reference, accessKey: 'f'),
                new CadMenuItemViewModel("Undo", () => ChooseOption("Undo"), accessKey: 'U'),
                CadMenuItemViewModel.Separator(),
                new CadMenuItemViewModel("Exit", () => ChooseOption("eXit"), accessKey: 'x'),
            };
        }

        /// <summary>"Repeat LINE" and its line, the first items of the Default and Edit menus once a command has run; else none.</summary>
        private List<CadMenuItemViewModel> RepeatItems(CadCommandSession session)
        {
            var items = new List<CadMenuItemViewModel>();
            string last = session.LastCommandName;

            if (last != null)
            {
                items.Add(new CadMenuItemViewModel("Repeat " + last, () => StartCommand(last), accessKey: 'R'));
                items.Add(CadMenuItemViewModel.Separator());
            }

            return items;
        }

        /// <summary>The Clipboard menu of the Default and Edit menus.</summary>
        private CadMenuItemViewModel ClipboardMenu() =>
            Submenu(
                "Clipboard",
                new[]
                {
                    MenuCommand("Cut", "CUTCLIP", 't'),
                    MenuCommand("Copy", "COPYCLIP", 'C'),
                    MenuCommand("Copy with Base Point", "COPYBASE", 'B'),
                    MenuCommand("Paste", "PASTECLIP", 'P'),
                    MenuCommand("Paste as Block", "PASTEBLOCK", 'k'),
                    MenuCommand("Paste to Original Coordinates", "PASTEORIG", 'd'),
                }
            );

        /// <summary>The Isolate menu of the Default and Edit menus.</summary>
        private CadMenuItemViewModel IsolateMenu() =>
            Submenu(
                "Isolate",
                new[]
                {
                    MenuCommand("Isolate Objects", "ISOLATEOBJECTS", 'I'),
                    MenuCommand("Hide Objects", "HIDEOBJECTS", 'H'),
                    MenuCommand("End Object Isolation", "UNISOLATEOBJECTS", 'E'),
                },
                'I'
            );

        /// <summary>An item of Draw Order: DRAWORDER with its option, the selection ordered, as AutoCAD's ai_draworder.</summary>
        private CadMenuItemViewModel DrawOrderItem(string header, string option, char key) =>
            HasCommand("DRAWORDER")
                ? new CadMenuItemViewModel(header, () => StartCommandWithOption("DRAWORDER " + option), accessKey: key)
                : NotBuilt(header, key);

        /// <summary>An item that starts a command of the drawing shown; grey where it has no such command.</summary>
        private CadMenuItemViewModel MenuCommand(string header, string command, char key = '\0') =>
            HasCommand(command)
                ? new CadMenuItemViewModel(header, () => StartCommand(command), accessKey: key)
                : NotBuilt(header, key);

        /// <summary>An item of a menu of its own, grey where none of its items can be chosen.</summary>
        private static CadMenuItemViewModel Submenu(
            string header,
            IReadOnlyList<CadMenuItemViewModel> items,
            char key = '\0'
        ) => new CadMenuItemViewModel(header, items.Any(i => !i.IsSeparator && i.IsEnabled), items, key);

        /// <summary>A grey item AutoCAD has and the presenter does not, "Not built yet." its tooltip.</summary>
        private static CadMenuItemViewModel NotBuilt(string header, char key = '\0') =>
            new CadMenuItemViewModel(header, () => { }, false, NotBuiltYet, accessKey: key);

        /// <summary>Whether the drawing shown has a command of the name.</summary>
        private bool HasCommand(string name) => _current?.Session.Registry.Find(name) != null;

        /// <summary>An entry of Recent Input chosen from the Command menu, answered as it was the first time: a point or number typed again, a set of objects selected again.</summary>
        private void ChooseRecentInput(int index)
        {
            if (_current == null || !_current.Session.IsRunning)
            {
                return;
            }

            _current.Session.FeedRecentInput(index);
            SyncHistory();
            RunAfterCommand();
        }

        /// <summary>An option of the prompt chosen from a menu, as if typed: "Undo", or a grip mode "MOve".</summary>
        private void ChooseOption(string keyword)
        {
            if (_current == null || !_current.Session.IsRunning)
            {
                return;
            }

            _current.Session.Feed(keyword);
            SyncHistory();
            RunAfterCommand();
        }
    }
}

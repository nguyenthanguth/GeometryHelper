using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Drawing Properties (DWGPROPS), hyperlinks and the clean screen: the two dialogs shown through functions the workspace
    /// sets, so that a test drives them with none; following a hyperlink (Ctrl+click on its object, the Hyperlink menu); and
    /// CLEANSCREENON, CLEANSCREENOFF and Ctrl+0, which hide the ribbon, Properties and the Drawings list and bring them back.
    /// </summary>
    public sealed partial class MainViewModel
    {
        // Which of the ribbon, Properties and the Drawings list were shown when the clean screen hid them; null while it is off.
        private bool[] _cleanScreenFrom;

        /// <summary>Gets or sets how the workspace shows Drawing Properties; the action it closed with is in its request.</summary>
        public Func<DrawingPropertiesViewModel, bool> ShowDrawingProperties { get; set; }

        /// <summary>Gets or sets how the workspace shows Insert Hyperlink and Edit Hyperlink; the action it closed with is in its request.</summary>
        public Func<HyperlinkViewModel, bool> ShowHyperlink { get; set; }

        /// <summary>
        /// Gets or sets how the workspace opens a web address, a mail address or a file with the program of the system for it; true
        /// where it was opened. Null for none, where following such a hyperlink says so.
        /// </summary>
        public Func<string, bool> OpenLink { get; set; }

        /// <summary>Gets whether CLEANSCREENON (Ctrl+0) has hidden the ribbon, Properties and the Drawings list, and CLEANSCREENOFF has not yet shown them again.</summary>
        public bool IsCleanScreen => _cleanScreenFrom != null;

        /// <summary>A request of DWGPROPS or HYPERLINK: its dialog shown where the workspace shows it.</summary>
        private void OnMisc2Requested(CadUiRequestEventArgs e)
        {
            switch (e.Payload)
            {
                case CadDrawingPropertiesRequest properties when ShowDrawingProperties != null:
                    e.Handled = ShowDrawingProperties(new DrawingPropertiesViewModel(properties));
                    break;
                case CadHyperlinkRequest link when ShowHyperlink != null:
                    e.Handled = ShowHyperlink(new HyperlinkViewModel(link));
                    break;
            }
        }

        private void AddMisc2WindowCommands(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("CLEANSCREENON", c => RunWindow(CleanScreenOn)));
            registry.Add(new CadCommand("CLEANSCREENOFF", c => RunWindow(CleanScreenOff)));
        }

        /// <summary>
        /// CLEANSCREENON: the ribbon, Properties and the Drawings list hidden, each remembered as it was; the command line and the
        /// status bar stay. A second CLEANSCREENON keeps what the first remembered.
        /// </summary>
        private void CleanScreenOn()
        {
            if (_cleanScreenFrom != null)
            {
                return;
            }

            _cleanScreenFrom = new[] { IsRibbonShown, IsPropertiesPanelShown, IsFileListShown };
            IsRibbonShown = false;
            IsPropertiesPanelShown = false;
            IsFileListShown = false;
            Raise(nameof(IsCleanScreen));
        }

        /// <summary>CLEANSCREENOFF: what CLEANSCREENON hid shown again, each as it was; nothing where none was hidden by it.</summary>
        private void CleanScreenOff()
        {
            bool[] was = _cleanScreenFrom;

            if (was == null)
            {
                return;
            }

            _cleanScreenFrom = null;
            IsRibbonShown = was[0];
            IsPropertiesPanelShown = was[1];
            IsFileListShown = was[2];
            Raise(nameof(IsCleanScreen));
        }

        /// <summary>Ctrl+0: the clean screen switched on, or off where it is on.</summary>
        private void ToggleCleanScreen()
        {
            if (IsCleanScreen)
            {
                CleanScreenOff();
            }
            else
            {
                CleanScreenOn();
            }
        }

        /// <summary>
        /// Follows the first hyperlink of an object, as Ctrl+click on it does: a link to a view of the drawing restores the view
        /// (-VIEW Restore), a drawing file opens in the presenter, any other file, a web address or a mail address opens with the
        /// program of the system through <see cref="OpenLink"/>. A link to nothing says "** No hyperlink to navigate to **", a file
        /// not there "Hyperlink destination cannot be found." No command may be running.
        /// </summary>
        /// <param name="handle">The handle of the object.</param>
        /// <returns>Whether the link was followed.</returns>
        public bool FollowHyperlink(string handle)
        {
            if (_current == null || _current.Session.IsRunning)
            {
                return false;
            }

            DxfHyperLink link = CadHyperlinks.First(_current.Document.FindEntity(handle));

            if (link == null)
            {
                return false;
            }

            CadHyperlinkTarget target = CadHyperlinks.Resolve(_current.Document, link);

            switch (target.Kind)
            {
                case CadHyperlinkTargetKind.View:
                    if (!_current.Document.Database.ViewTable.Has(target.View))
                    {
                        Say("Hyperlink destination cannot be found.");
                        return false;
                    }

                    StartCommand("-VIEW");
                    Execute("Restore");
                    Execute(target.View);
                    return true;
                case CadHyperlinkTargetKind.File:
                    if (!File.Exists(target.Address) && !Directory.Exists(target.Address))
                    {
                        Say("Hyperlink destination cannot be found.");
                        return false;
                    }

                    if (CadDrawingFile.IsDrawingName(target.Address))
                    {
                        return Open(target.Address);
                    }

                    return Launch(target.Address);
                case CadHyperlinkTargetKind.Web:
                case CadHyperlinkTargetKind.Mail:
                    return Launch(target.Address);
                default:
                    Say("** No hyperlink to navigate to **");
                    return false;
            }
        }

        /// <summary>The tooltip of the object under the cursor that has a hyperlink, as AutoCAD shows it; null for none.</summary>
        /// <param name="handle">The handle of the object.</param>
        /// <returns>The words, such as <c>Plate B-12</c> and <c>CTRL + click to follow link</c> on two lines.</returns>
        public string HyperlinkTooltip(string handle) =>
            _current == null ? null : CadHyperlinks.Tooltip(_current.Document.FindEntity(handle));

        private bool Launch(string address)
        {
            if (OpenLink == null)
            {
                Say("The window opens " + address + ".");
                return false;
            }

            bool opened = OpenLink(address);

            if (!opened)
            {
                Say("Hyperlink destination cannot be found.");
            }

            return opened;
        }

        /// <summary>
        /// AutoCAD's Hyperlink menu of the Edit menu, for objects selected that have a hyperlink: Open "link", Copy Hyperlink and
        /// Edit Hyperlink…, on the first of them in the order of their handles; null where none has one.
        /// </summary>
        private CadMenuItemViewModel HyperlinkMenu()
        {
            CadDocument document = _current?.Document;

            if (document == null)
            {
                return null;
            }

            var handles = new List<string>(document.Selection.Handles);
            handles.Sort(StringComparer.OrdinalIgnoreCase);
            DxfEntity first = CadHyperlinks.WithLinks(document, handles).FirstOrDefault();
            DxfHyperLink link = CadHyperlinks.First(first);

            if (link == null)
            {
                return null;
            }

            string shown = link.DisplayString;
            string open = shown.Length > 40 ? shown.Substring(0, 40) + "..." : shown;
            string handle = first.Handle;
            string copy = link.Name.Length > 0 ? link.Name : link.SubLocation;
            return Submenu(
                "Hyperlink",
                new[]
                {
                    new CadMenuItemViewModel("Open \"" + open + "\"", () => FollowHyperlink(handle), accessKey: 'O'),
                    new CadMenuItemViewModel("Copy Hyperlink", () => WriteClipboardText?.Invoke(copy), accessKey: 'C'),
                    MenuCommand("Edit Hyperlink...", "HYPERLINK", 'E'),
                },
                'H'
            );
        }
    }
}

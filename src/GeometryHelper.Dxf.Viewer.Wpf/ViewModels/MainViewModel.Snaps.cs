using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// An item of a menu the window shows, as the view model gives it: its text, whether it is on, ticked or a separator, its
    /// tooltip, and what choosing it does. The window makes the menu from these each time it opens, so that a test reads the
    /// same items.
    /// </summary>
    public sealed class CadMenuItemViewModel
    {
        internal CadMenuItemViewModel(string header, Action run, bool isEnabled = true, string toolTip = null, bool? isChecked = null, CadSnapKind marker = CadSnapKind.None)
        {
            Header = header ?? string.Empty;
            IsEnabled = isEnabled;
            ToolTip = toolTip;
            IsCheckable = isChecked.HasValue;
            IsChecked = isChecked ?? false;
            Marker = marker;
            Command = run == null ? null : new DelegateCommand(_ => run(), _ => isEnabled);
        }

        /// <summary>Gets the text, as "Close All Other Drawings"; empty for a separator.</summary>
        public string Header { get; }

        /// <summary>Gets whether it is a line between groups of items.</summary>
        public bool IsSeparator => Command == null;

        /// <summary>Gets whether it can be chosen.</summary>
        public bool IsEnabled { get; }

        /// <summary>Gets the tooltip, as "Not built yet."; null for none.</summary>
        public string ToolTip { get; }

        /// <summary>Gets whether it shows a tick, as an object snap of the OSNAP menu.</summary>
        public bool IsCheckable { get; }

        /// <summary>Gets whether its tick is shown.</summary>
        public bool IsChecked { get; }

        /// <summary>Gets the snap whose marker is drawn beside it; <see cref="CadSnapKind.None"/> for none.</summary>
        public CadSnapKind Marker { get; }

        /// <summary>Gets what choosing it does; null for a separator.</summary>
        public ICommand Command { get; }

        /// <inheritdoc/>
        public override string ToString() => Header;

        internal static CadMenuItemViewModel Separator() => new CadMenuItemViewModel(string.Empty, null, false);
    }

    /// <summary>
    /// The object snaps as AutoCAD's interface has them: Drafting Settings (DSETTINGS, DS, OS), the OSNAP menu of the status
    /// bar, the Shift+right-click menu at a point prompt, and an override typed at a point prompt (END, MID, CEN) for the next
    /// point alone.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>Said of an object snap AutoCAD has and the presenter does not.</summary>
        public const string NotBuiltYet = "Not built yet.";

        /// <summary>
        /// AutoCAD's fourteen object snaps in the order of its Object Snap tab: the name, the marker, the flag (none for a snap
        /// not built yet), the keyword typed at a point prompt, and the word it starts.
        /// </summary>
        internal static readonly IReadOnlyList<SnapModeInfo> SnapModeTable = new[]
        {
            new SnapModeInfo(CadSnapKind.Endpoint, CadSnapModes.Endpoint, "END", "ENDPOINT"),
            new SnapModeInfo(CadSnapKind.Midpoint, CadSnapModes.Midpoint, "MID", "MIDPOINT"),
            new SnapModeInfo(CadSnapKind.Center, CadSnapModes.Center, "CEN", "CENTER"),
            new SnapModeInfo(CadSnapKind.GeometricCenter, CadSnapModes.GeometricCenter, "GCE", "GCENTER"),
            new SnapModeInfo(CadSnapKind.Node, CadSnapModes.Node, "NOD", "NODE"),
            new SnapModeInfo(CadSnapKind.Quadrant, CadSnapModes.Quadrant, "QUA", "QUADRANT"),
            new SnapModeInfo(CadSnapKind.Intersection, CadSnapModes.Intersection, "INT", "INTERSECTION"),
            new SnapModeInfo(CadSnapKind.Extension, CadSnapModes.Extension, "EXT", "EXTENSION"),
            new SnapModeInfo(CadSnapKind.Insertion, CadSnapModes.Insertion, "INS", "INSERTION"),
            new SnapModeInfo(CadSnapKind.Perpendicular, CadSnapModes.Perpendicular, "PER", "PERPENDICULAR"),
            new SnapModeInfo(CadSnapKind.Tangent, CadSnapModes.Tangent, "TAN", "TANGENT"),
            new SnapModeInfo(CadSnapKind.Nearest, CadSnapModes.Nearest, "NEA", "NEAREST"),
            new SnapModeInfo(CadSnapKind.ApparentIntersection, CadSnapModes.ApparentIntersection, "APP", "APPINT"),
            new SnapModeInfo(CadSnapKind.Parallel, CadSnapModes.Parallel, "PAR", "PARALLEL"),
        };

        /// <summary>The order of AutoCAD's Shift+right-click menu, its object snaps.</summary>
        private static readonly CadSnapKind[] OverrideMenuOrder =
        {
            CadSnapKind.Endpoint, CadSnapKind.Midpoint, CadSnapKind.Intersection, CadSnapKind.ApparentIntersection, CadSnapKind.Extension, CadSnapKind.Center,
            CadSnapKind.GeometricCenter, CadSnapKind.Quadrant, CadSnapKind.Tangent, CadSnapKind.Perpendicular, CadSnapKind.Parallel, CadSnapKind.Node,
            CadSnapKind.Insertion, CadSnapKind.Nearest,
        };

        private EventHandler _draftingSettingsRequested;

        /// <summary>Raised by DSETTINGS (DS, OS) and the menus' "Object Snap Settings...": the window shows Drafting Settings over <see cref="CreateDraftingSettings"/>.</summary>
        public event EventHandler DraftingSettingsRequested
        {
            add => _draftingSettingsRequested += value;
            remove => _draftingSettingsRequested -= value;
        }

        /// <summary>
        /// Gets or sets the object snaps on, as AutoCAD's OSMODE: what the OSNAP menu and Drafting Settings tick. Set, it is
        /// given to <see cref="Snap"/> and to <see cref="Settings"/>, and raises a change a host keeps in the settings file, as
        /// the window does; <see cref="ApplySettings"/> sets it too, without the change.
        /// </summary>
        public CadSnapModes SnapModes
        {
            get => Snap.Modes;
            set
            {
                if (Snap.Modes == value && _settings.Snaps == value)
                {
                    return;
                }

                Snap.Modes = value;
                _settings.Snaps = value;
                Raise(nameof(SnapModes));
            }
        }

        /// <summary>
        /// Takes object snaps for the next point alone, as AutoCAD's override: whether OSNAP is on or off, only these give the
        /// point, and <see cref="CadSnapModes.None"/> none (NONe). The command line says it, "Specify next point: _end of", and
        /// the override ends once the prompt has its answer or goes.
        /// </summary>
        /// <param name="modes">The snaps.</param>
        /// <returns>Whether it was taken: false where no prompt waits for a point.</returns>
        public bool OverrideSnap(CadSnapModes modes)
        {
            if (_current == null || !CadPicking.WantsPoint(_current.Session))
            {
                return false;
            }

            SnapModeInfo info = SnapModeTable.FirstOrDefault(i => i.Mode == modes && modes != CadSnapModes.None);
            Snap.Override = modes;
            _current.Session.Write(_current.Session.Prompt.Text + " _" + (info?.Keyword.ToLowerInvariant() ?? "non") + " of");
            SyncHistory();
            return true;
        }

        /// <summary>Makes the view model of Drafting Settings over the snaps, the grid and the polar tracking in use.</summary>
        /// <param name="tab">The tab shown first: "Snap and Grid", "Polar Tracking" or "Object Snap"; null for Object Snap.</param>
        /// <returns>The view model.</returns>
        public DraftingSettingsViewModel CreateDraftingSettings(string tab = null) => new DraftingSettingsViewModel(this, tab);

        /// <summary>
        /// Gets the items of the menu of the status bar's OSNAP (its arrow, or a right click on it): each object snap of
        /// AutoCAD's ticked when on, one not built yet shown off with "Not built yet.", then "Object Snap Settings...".
        /// </summary>
        /// <returns>The items.</returns>
        public IReadOnlyList<CadMenuItemViewModel> OsnapMenu()
        {
            var items = SnapModeTable.Select(i => i.IsBuilt
                ? new CadMenuItemViewModel(i.Name, () => SnapModes ^= i.Mode, true, null, (SnapModes & i.Mode) != 0, i.Kind)
                : new CadMenuItemViewModel(i.Name, () => { }, false, NotBuiltYet, false, i.Kind)).ToList();
            items.Add(CadMenuItemViewModel.Separator());
            items.Add(new CadMenuItemViewModel("Object Snap Settings...", () => RequestDraftingSettings(null)));
            return items;
        }

        /// <summary>
        /// Gets the items of the Shift+right-click menu while a point is asked, in AutoCAD's order: the fourteen snaps, each taken
        /// for the next point alone (<see cref="OverrideSnap"/>), None, then "Osnap Settings...". Empty while no point is asked.
        /// </summary>
        /// <returns>The items.</returns>
        public IReadOnlyList<CadMenuItemViewModel> SnapOverrideMenu()
        {
            if (_current == null || !CadPicking.WantsPoint(_current.Session))
            {
                return Array.Empty<CadMenuItemViewModel>();
            }

            var items = OverrideMenuOrder.Select(k => SnapModeTable.First(i => i.Kind == k))
                .Select(i => new CadMenuItemViewModel(i.Name, () => OverrideSnap(i.Mode), true, null, null, i.Kind)).ToList();
            items.Add(new CadMenuItemViewModel("None", () => OverrideSnap(CadSnapModes.None)));
            items.Add(CadMenuItemViewModel.Separator());
            items.Add(new CadMenuItemViewModel("Osnap Settings...", () => RequestDraftingSettings(null)));
            return items;
        }

        /// <summary>Asks the window for Drafting Settings on a tab; with no window, says so on the command line.</summary>
        internal void RequestDraftingSettings(string tab)
        {
            if (_draftingSettingsRequested == null)
            {
                Say("Drafting Settings are shown by the window.");
                return;
            }

            _draftingSettingsRequested(this, EventArgs.Empty);
        }

        /// <summary>DSETTINGS: Drafting Settings, shown by the window once the command has ended.</summary>
        private IEnumerable<CadPrompt> DraftingSettingsBody(CadCommandContext c)
        {
            if (_draftingSettingsRequested == null)
            {
                c.Write("Drafting Settings are shown by the window.");
            }
            else
            {
                _afterCommand = () => RequestDraftingSettings(null);
            }

            return Array.Empty<CadPrompt>();
        }

        /// <summary>
        /// A word typed at a point prompt that names an object snap, END or ENDPOINT, takes it for the next point; one not built
        /// yet is said so. A keyword of the prompt itself wins, as in AutoCAD.
        /// </summary>
        private bool TakeSnapOverride(CadCommandSession session, string word)
        {
            if (!CadPicking.WantsPoint(session) || session.Prompt.MatchKeyword(word) != null)
            {
                return false;
            }

            string typed = word.Trim().ToUpperInvariant();
            bool none = typed.Length >= 3 && "NONE".StartsWith(typed, StringComparison.Ordinal);
            SnapModeInfo info = SnapModeTable.FirstOrDefault(i => typed.StartsWith(i.Keyword, StringComparison.Ordinal) && i.Word.StartsWith(typed, StringComparison.Ordinal));

            if (none)
            {
                return OverrideSnap(CadSnapModes.None);
            }

            if (info == null)
            {
                return false;
            }

            if (!info.IsBuilt)
            {
                session.Write(session.Prompt.Text + " " + typed);
                session.Write(info.Name + " object snap is not built yet.");
                return true;
            }

            return OverrideSnap(info.Mode);
        }

        /// <summary>An object snap of AutoCAD's: its marker, its flag (none where not built), its keyword and the word it starts.</summary>
        internal sealed class SnapModeInfo
        {
            public SnapModeInfo(CadSnapKind kind, CadSnapModes mode, string keyword, string word)
            {
                Kind = kind;
                Mode = mode;
                Keyword = keyword;
                Word = word;
            }

            public CadSnapKind Kind { get; }

            public string Name => CadSnapMarker.NameOf(Kind);

            public CadSnapModes Mode { get; }

            public bool IsBuilt => Mode != CadSnapModes.None;

            public string Keyword { get; }

            public string Word { get; }
        }
    }
}

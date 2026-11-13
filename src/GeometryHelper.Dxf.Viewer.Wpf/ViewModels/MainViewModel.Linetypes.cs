using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The linetype box of the ribbon's Properties panel ($CELTYPE), the Linetype Manager (LINETYPE) and the Hatch dialog of
    /// HATCH's seTtings and HATCHEDIT, each shown through a function the workspace sets, so that a test drives them with none.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>The last choice of the linetype box: the Linetype Manager, as AutoCAD's Other... shows it.</summary>
        public const string OtherLinetypeOption = "Other...";

        /// <summary>Gets or sets how the workspace shows the Linetype Manager: true when OK was pressed, the manager then applied.</summary>
        public Func<LinetypeManagerViewModel, bool> ShowLinetypeManager { get; set; }

        /// <summary>Gets or sets how the workspace shows the Hatch dialog: true when OK was pressed.</summary>
        public Func<HatchSettingsViewModel, bool> ShowHatchSettings { get; set; }

        // The Hatch dialog closed by Click to set new origin, and its drawing, shown again once HATCH has the point.
        private HatchSettingsViewModel _hatchPickingOrigin;
        private CadDocument _hatchPickingOriginOf;

        /// <summary>
        /// Gets or sets the linetype of new objects, the drawing's $CELTYPE, as the linetype box of the ribbon's Properties panel
        /// shows it: ByLayer, ByBlock or a linetype loaded. Setting one sets it as one undo step; <see cref="OtherLinetypeOption"/>
        /// shows the Linetype Manager. Empty with no drawing open.
        /// </summary>
        public string CurrentLinetype
        {
            get => _current == null ? string.Empty : _current.Document.Database.Celtype;
            set
            {
                if (_current == null || value == null || value == CurrentLinetype)
                {
                    return;
                }

                if (value == OtherLinetypeOption)
                {
                    OpenLinetypeManager();
                }
                else
                {
                    CadEditResult result = CadPropertyEditor.Set(_current.Document, null, "Linetype", value);

                    if (!result.Succeeded)
                    {
                        Say(result.Message);
                    }
                }

                RaiseCurrentLinetype();
            }
        }

        /// <summary>Gets the choices of the linetype box: ByLayer, ByBlock, the linetypes loaded in the order of their names, and <see cref="OtherLinetypeOption"/>.</summary>
        public IReadOnlyList<string> CurrentLinetypeOptions
        {
            get
            {
                var options = new List<string> { "ByLayer", "ByBlock" };

                if (_current != null)
                {
                    options.AddRange(CadLinetypes.Names(_current.Document.Database));
                }

                options.Add(OtherLinetypeOption);
                return options;
            }
        }

        /// <summary>Makes the Linetype Manager of the drawing shown; null with none open.</summary>
        /// <returns>The manager.</returns>
        public LinetypeManagerViewModel CreateLinetypeManager() =>
            _current == null ? null : new LinetypeManagerViewModel(_current.Document);

        /// <summary>
        /// Gets the .pat file last chosen in the Hatch dialog by Browse… while the window is open, offered again by the next
        /// Hatch dialog of any drawing whose settings name no file of their own; null for the built-in patterns alone.
        /// </summary>
        public string HatchPatternFile { get; private set; }

        /// <summary>Makes the Hatch dialog on the settings of the next hatch of the drawing shown; null with none open.</summary>
        /// <returns>The dialog.</returns>
        public HatchSettingsViewModel CreateHatchSettings() =>
            _current == null ? null : HatchDialog(_current.Document, _current.Document.HatchSettings, false);

        /// <summary>The Hatch dialog on settings, the pattern file of the session offered where the settings name none.</summary>
        private HatchSettingsViewModel HatchDialog(CadDocument document, CadHatchSettings settings, bool editing)
        {
            var dialog = new HatchSettingsViewModel(document, settings, editing);

            if (dialog.IsBuiltInFile && HatchPatternFile != null)
            {
                dialog.PatternFile = HatchPatternFile;
            }

            return dialog;
        }

        /// <summary>Shows the Linetype Manager and applies it where OK is pressed, saying what it refused.</summary>
        private bool OpenLinetypeManager()
        {
            LinetypeManagerViewModel manager = CreateLinetypeManager();

            if (manager == null || ShowLinetypeManager == null)
            {
                return false;
            }

            if (ShowLinetypeManager(manager))
            {
                manager.Apply();

                if (manager.Message.Length > 0)
                {
                    Say(manager.Message);
                }
            }

            RaiseCurrentLinetype();
            return true;
        }

        /// <summary>
        /// The requests of LINETYPE, HATCH's seTtings and HATCHEDIT: the dialog shown where the workspace shows it, the
        /// request handled; with none, the command asks on the command line.
        /// </summary>
        private void OnHatchOrLinetypeRequested(CadUiRequestEventArgs e)
        {
            CadDocument document = e.Document ?? _current?.Document;

            if (document == null)
            {
                return;
            }

            switch (e.Name)
            {
                case "LINETYPE":
                    e.Handled = OpenLinetypeManager();
                    break;
                case "HATCHSETTINGS":
                    if (ShowHatchSettings != null)
                    {
                        // Shown again after Click to set new origin: the same dialog, with the point picked where one was.
                        HatchSettingsViewModel settings =
                            e.Payload != null && _hatchPickingOrigin != null && _hatchPickingOriginOf == document
                                ? _hatchPickingOrigin
                                : HatchDialog(document, document.HatchSettings, false);
                        _hatchPickingOrigin = null;
                        _hatchPickingOriginOf = null;

                        if (e.Payload is GeometryHelper.Geometry.GeoPoint2 origin)
                        {
                            settings.TakeOrigin(origin.X, origin.Y);
                        }
                        else
                        {
                            settings.KeepOrigin();
                        }

                        bool ok = ShowHatchSettings(settings);

                        if (!ok && settings.IsPickingOrigin)
                        {
                            _hatchPickingOrigin = settings;
                            _hatchPickingOriginOf = document;
                            e.Answer = "Click to set new origin";
                        }
                        else if (ok)
                        {
                            document.HatchSettings = settings.Settings;
                            HatchPatternFile = settings.PatternFile;

                            if (settings.IsOriginOnce)
                            {
                                // A specified origin not stored as default lays this HATCH's hatches alone.
                                double x = settings.StoredOriginX;
                                double y = settings.StoredOriginY;
                                Action before = _afterCommand;
                                Action restore = null;
                                restore = () =>
                                {
                                    // Run after each input; the origin is given back once HATCH has ended.
                                    if (IsCommandRunning)
                                    {
                                        _afterCommand = restore;
                                        return;
                                    }

                                    CadHatchSettings kept = document.HatchSettings.Clone();
                                    kept.OriginX = x;
                                    kept.OriginY = y;
                                    document.HatchSettings = kept;
                                };
                                _afterCommand = () =>
                                {
                                    before?.Invoke();
                                    restore();
                                };
                            }
                        }

                        e.Handled = true;
                    }

                    break;
                case "HATCHEDIT":
                    if (ShowHatchSettings != null && document.HatchBeingEdited != null)
                    {
                        HatchSettingsViewModel edited = HatchDialog(document, document.HatchBeingEdited, true);
                        bool ok = ShowHatchSettings(edited);
                        document.HatchBeingEdited = ok ? edited.Settings : null;
                        HatchPatternFile = ok ? edited.PatternFile : HatchPatternFile;
                        e.Handled = true;
                    }

                    break;
            }
        }

        private void RaiseCurrentLinetype()
        {
            Raise(nameof(CurrentLinetype));
            Raise(nameof(CurrentLinetypeOptions));
        }
    }
}

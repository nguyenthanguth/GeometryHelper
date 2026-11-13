using System;
using System.Globalization;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The dialogs of round 2: Rename (RENAME), Lineweight Settings (LWEIGHT), Check Spelling (SPELL) and Boundary Creation
    /// (BOUNDARY), each shown through a function the workspace sets, so that a test drives it with none.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>Gets or sets how the workspace shows Rename: true when OK was pressed.</summary>
        public Func<RenameViewModel, bool> ShowRename { get; set; }

        /// <summary>Gets or sets how the workspace shows Lineweight Settings: true when OK was pressed.</summary>
        public Func<LineweightSettingsViewModel, bool> ShowLineweightSettings { get; set; }

        /// <summary>Gets or sets how the workspace shows Check Spelling, until Close or its Select objects button.</summary>
        public Action<CheckSpellingViewModel> ShowCheckSpelling { get; set; }

        /// <summary>Gets or sets how the workspace shows Boundary Creation: true when Pick Points was pressed.</summary>
        public Func<BoundaryCreationViewModel, bool> ShowBoundaryCreation { get; set; }

        /// <summary>The requests of round 2: RENAME, LWEIGHT, SPELL and BOUNDARY.</summary>
        private void OnRoundTwoDialogRequested(CadUiRequestEventArgs e)
        {
            switch (e.Payload)
            {
                case CadRenameRequest rename when ShowRename != null:
                    var renaming = new RenameViewModel(rename);

                    if (ShowRename(renaming))
                    {
                        renaming.Accept();
                    }

                    e.Handled = true;
                    break;
                case CadSpellCheck check when ShowCheckSpelling != null:
                    ShowCheckSpelling(new CheckSpellingViewModel(check));
                    e.Handled = true;
                    break;
                case CadBoundaryRequest boundary when ShowBoundaryCreation != null:
                    var creation = new BoundaryCreationViewModel(boundary);

                    if (ShowBoundaryCreation(creation))
                    {
                        creation.PickPoints();
                    }

                    e.Handled = true;
                    break;
                case null when e.Name == "LWEIGHT" && ShowLineweightSettings != null && _current != null:
                    ShowLineweights();
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// Lineweight Settings: on OK the lineweight picked is that of new objects (one step of undo named LWEIGHT where it
        /// changed), Display Lineweight is LWDISPLAY, and the units, Default and scale are kept in the settings.
        /// </summary>
        private void ShowLineweights()
        {
            CadDocument document = _current.Document;
            var dialog = new LineweightSettingsViewModel(
                document.Database.Celweight,
                ShowLineWeights,
                _settings.LineweightsInInches,
                _settings.DefaultLineweight,
                _settings.LineweightDisplayScale
            );

            if (!ShowLineweightSettings(dialog))
            {
                return;
            }

            DxfLineWeight old = document.Database.Celweight;
            DxfLineWeight weight = dialog.Current;

            if (weight != old)
            {
                document.ApplyEdit(
                    "LWEIGHT",
                    new[]
                    {
                        new CadEditRecord(
                            CadEditRecordKind.SettingChanged,
                            null,
                            "$CELWEIGHT",
                            ((int)old).ToString(CultureInfo.InvariantCulture),
                            ((int)weight).ToString(CultureInfo.InvariantCulture)
                        ),
                    },
                    s => s.Database.Celweight = weight
                );
            }

            if (dialog.DisplayLineweight != ShowLineWeights)
            {
                SetLineWeightDisplay(dialog.DisplayLineweight);
            }

            if (
                dialog.IsInches != _settings.LineweightsInInches
                || dialog.DefaultWeight != _settings.DefaultLineweight
                || !dialog.Scale.Equals(_settings.LineweightDisplayScale)
            )
            {
                ChangeSettings(s =>
                {
                    s.LineweightsInInches = dialog.IsInches;
                    s.DefaultLineweight = dialog.DefaultWeight;
                    s.LineweightDisplayScale = dialog.Scale;
                });

                // The Default lineweight drawn anew in every drawing open.
                foreach (KnownFile file in _files.Values)
                {
                    if (file.Document != null)
                    {
                        FollowSettings(file);
                    }
                }

                Revision++;
            }
        }
    }
}

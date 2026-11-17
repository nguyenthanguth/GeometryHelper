using System;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The managers of the drawing, each shown through a function the workspace sets, so that a test drives them with none:
    /// the Block Attribute Manager (BATTMAN), Edit Attributes (INSERT with ATTDIA 1), Purge (PURGE) and the question before
    /// each object it purges, Object Grouping (CLASSICGROUP), Leader Settings (QLEADER) and Geometric Tolerance (TOLERANCE).
    /// With none set, each command asks its typed form or says it has none.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>Gets or sets how the workspace shows the Block Attribute Manager; the action it closed with is in its request.</summary>
        public Func<BlockAttributeManagerViewModel, bool> ShowBlockAttributeManager { get; set; }

        /// <summary>Gets or sets how the workspace shows Edit Attributes; the action it closed with is in its request.</summary>
        public Func<EditAttributesViewModel, bool> ShowEditAttributes { get; set; }

        /// <summary>Gets or sets how the workspace shows Purge; Select Objects, where pressed, is in its request.</summary>
        public Func<PurgeViewModel, bool> ShowPurge { get; set; }

        /// <summary>
        /// Gets or sets how the workspace asks whether to purge one object, "Purge layer "TMP"?": true purges it, false keeps
        /// it, null stops the purge. With none set, Purge purges without asking.
        /// </summary>
        public Func<string, bool?> ConfirmPurgeItem { get; set; }

        /// <summary>Gets or sets how the workspace shows Object Grouping; the action it closed with is in its request.</summary>
        public Func<GroupManagerViewModel, bool> ShowGroupManager { get; set; }

        /// <summary>Gets or sets how the workspace shows the Layer States Manager; the action it closed with is in its request.</summary>
        public Func<LayerStatesViewModel, bool> ShowLayerStates { get; set; }

        /// <summary>Gets or sets how the workspace shows Edit Drawing Scales (SCALELISTEDIT); the action it closed with is in its request.</summary>
        public Func<ScaleListEditViewModel, bool> ShowScaleList { get; set; }

        /// <summary>Gets or sets how the workspace shows Leader Settings (QLEADER's Settings); the action it closed with is in its request.</summary>
        public Func<LeaderSettingsViewModel, bool> ShowLeaderSettings { get; set; }

        /// <summary>Gets or sets how the workspace shows the Field dialog (FIELD, and Insert Field of the text editor); the action it closed with is in its request.</summary>
        internal Func<FieldDialogViewModel, bool> ShowField { get; set; }

        /// <summary>A Field dialog for the drawing shown: its previews are evaluated with the session's system variables.</summary>
        internal CadFieldRequest NewFieldRequest() =>
            new CadFieldRequest(
                Session != null
                    ? CadCommands.FieldContext(Session, CadFieldContexts.Demand)
                    : new CadFieldContext(Document)
            );

        /// <summary>
        /// Asks for a field in the Field dialog while a text editor is open, where no command can ask for an object: Select object
        /// gives the object selected in the drawing, said at once where none is.
        /// </summary>
        /// <param name="request">The dialog, with the field to edit loaded where one is.</param>
        /// <returns>The field in its markers; null where the dialog was cancelled or the window shows none.</returns>
        internal string AskField(CadFieldRequest request)
        {
            while (ShowField != null)
            {
                request.Action = CadFieldAction.Cancel;
                ShowField(new FieldDialogViewModel(request));

                if (request.Action != CadFieldAction.SelectObject)
                {
                    return request.Action == CadFieldAction.Ok && request.Code.Length > 0 ? request.Code : null;
                }

                string picked = Document?.Selection.Handles.FirstOrDefault();

                if (picked == null)
                {
                    Session?.Write(
                        "Select an object of the drawing before the text editor is opened: the Object field reads it."
                    );
                }
                else
                {
                    request.SetObject(picked);
                }
            }

            return null;
        }

        /// <summary>
        /// What the % button (Insert field) of the attribute dialogs calls: the Field dialog over the drawing shown, its field in
        /// the markers the text takes; null where the window shows no Field dialog, which leaves the button grey.
        /// </summary>
        private Func<string> AskFieldInDialog =>
            ShowField == null ? (Func<string>)null : () => AskField(NewFieldRequest());

        /// <summary>Gets or sets how the workspace shows Geometric Tolerance (TOLERANCE, and the Tolerance of LEADER and QLEADER); the action it closed with is in its request.</summary>
        public Func<GeometricToleranceViewModel, bool> ShowGeometricTolerance { get; set; }

        /// <summary>A request of a manager: its dialog shown where the workspace shows it, the request answered.</summary>
        private void OnManagerRequested(CadUiRequestEventArgs e)
        {
            switch (e.Payload)
            {
                case CadAttributeManagerRequest manager when ShowBlockAttributeManager != null:
                    e.Handled = ShowBlockAttributeManager(
                        new BlockAttributeManagerViewModel(manager) { AskField = AskFieldInDialog }
                    );
                    break;
                case CadAttributeValuesRequest values when ShowEditAttributes != null:
                    e.Handled = ShowEditAttributes(new EditAttributesViewModel(values));
                    break;
                case CadPurgeRequest purge when ShowPurge != null:
                    e.Handled = ShowPurge(new PurgeViewModel(purge));
                    break;
                case string question when e.Name == "PURGEITEM" && ConfirmPurgeItem != null:
                    bool? answer = ConfirmPurgeItem(question);
                    e.Answer =
                        answer == null ? null
                        : answer.Value ? "Yes"
                        : "No";
                    e.Handled = true;
                    break;
                case CadGroupManagerRequest groups when ShowGroupManager != null:
                    e.Handled = ShowGroupManager(new GroupManagerViewModel(groups));
                    break;
                case CadLayerStatesRequest states when ShowLayerStates != null:
                    e.Handled = ShowLayerStates(new LayerStatesViewModel(states));
                    break;
                case CadScaleListRequest scales when ShowScaleList != null:
                    e.Handled = ShowScaleList(new ScaleListEditViewModel(scales));
                    break;
                case CadQLeaderRequest leader when ShowLeaderSettings != null:
                    e.Handled = ShowLeaderSettings(new LeaderSettingsViewModel(leader));
                    break;
                case CadFieldRequest field when ShowField != null:
                    e.Handled = ShowField(new FieldDialogViewModel(field));
                    break;
                case CadToleranceRequest tolerance when ShowGeometricTolerance != null:
                    e.Handled = ShowGeometricTolerance(new GeometricToleranceViewModel(tolerance));
                    break;
            }
        }
    }
}

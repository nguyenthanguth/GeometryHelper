using System;

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

        /// <summary>Gets or sets how the workspace shows Geometric Tolerance (TOLERANCE, and the Tolerance of LEADER and QLEADER); the action it closed with is in its request.</summary>
        public Func<GeometricToleranceViewModel, bool> ShowGeometricTolerance { get; set; }

        /// <summary>A request of a manager: its dialog shown where the workspace shows it, the request answered.</summary>
        private void OnManagerRequested(CadUiRequestEventArgs e)
        {
            switch (e.Payload)
            {
                case CadAttributeManagerRequest manager when ShowBlockAttributeManager != null:
                    e.Handled = ShowBlockAttributeManager(new BlockAttributeManagerViewModel(manager));
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
                case CadToleranceRequest tolerance when ShowGeometricTolerance != null:
                    e.Handled = ShowGeometricTolerance(new GeometricToleranceViewModel(tolerance));
                    break;
            }
        }
    }
}

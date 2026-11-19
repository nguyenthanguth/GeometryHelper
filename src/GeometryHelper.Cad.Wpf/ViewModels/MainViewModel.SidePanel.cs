using System.Linq;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>The panel an extension adds beside the DRAWINGS list (<see cref="CadWorkspaceExtension.SidePanelContent"/>).</summary>
    public sealed partial class MainViewModel
    {
        /// <summary>
        /// Gets the extension whose panel the window shows right of the DRAWINGS list, before the canvas: the first extension added
        /// that has a <see cref="CadWorkspaceExtension.SidePanelContent"/>; null where none has one, as in GeometryHelper CAD.
        /// </summary>
        public CadWorkspaceExtension SidePanelExtension => _extensions.FirstOrDefault(e => e.SidePanelContent != null);

        /// <summary>Gets whether the window shows a panel of an extension beside the DRAWINGS list: one has content and is visible.</summary>
        public bool IsSidePanelShown => SidePanelExtension?.IsSidePanelVisible == true;

        private void RaiseSidePanel()
        {
            Raise(nameof(SidePanelExtension));
            Raise(nameof(IsSidePanelShown));
        }
    }
}

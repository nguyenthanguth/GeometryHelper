using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The dialogs of the block commands, each shown through a function the workspace sets, so that a test drives them with
    /// none: Block Definition (BLOCK), the Blocks palette (INSERT), Write Block (WBLOCK), Attribute Definition (ATTDEF) and
    /// the Enhanced Attribute Editor (EATTEDIT, ATTEDIT). With none set, each command asks its typed form.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private readonly List<string> _recentBlocks = new List<string>();

        /// <summary>Gets or sets how the workspace shows Block Definition; the action it closed with is in its request.</summary>
        public Func<BlockDefinitionViewModel, bool> ShowBlockDefinition { get; set; }

        /// <summary>Gets or sets how the workspace shows the Blocks palette; the block chosen is in its request.</summary>
        public Func<BlocksPaletteViewModel, bool> ShowBlocksPalette { get; set; }

        /// <summary>Gets or sets how the workspace shows Write Block; the action it closed with is in its request.</summary>
        public Func<WriteBlockViewModel, bool> ShowWriteBlock { get; set; }

        /// <summary>Gets or sets how the workspace shows Attribute Definition; the action it closed with is in its request.</summary>
        public Func<AttributeDefinitionViewModel, bool> ShowAttributeDefinition { get; set; }

        /// <summary>Gets or sets how the workspace shows the Enhanced Attribute Editor; the action it closed with is in its request.</summary>
        public Func<AttributeEditorViewModel, bool> ShowAttributeEditor { get; set; }

        /// <summary>Gets the blocks inserted from the palette in this run of the presenter, the latest first, as its Recent tab lists them.</summary>
        public IReadOnlyList<string> RecentBlocks => _recentBlocks;

        /// <summary>A request of a block command: its dialog shown where the workspace shows it, the request answered.</summary>
        private void OnBlockRequested(CadUiRequestEventArgs e)
        {
            switch (e.Payload)
            {
                case CadBlockDefinitionRequest block when ShowBlockDefinition != null:
                    e.Handled = ShowBlockDefinition(new BlockDefinitionViewModel(block));
                    break;
                case CadInsertRequest insert when ShowBlocksPalette != null:
                    e.Handled = ShowBlocksPalette(new BlocksPaletteViewModel(insert, _recentBlocks));

                    if (insert.Action == CadDialogAction.Ok && !string.IsNullOrEmpty(insert.Name))
                    {
                        _recentBlocks.RemoveAll(n => string.Equals(n, insert.Name, StringComparison.OrdinalIgnoreCase));
                        _recentBlocks.Insert(0, insert.Name);
                    }

                    break;
                case CadWriteBlockRequest write when ShowWriteBlock != null:
                    e.Handled = ShowWriteBlock(new WriteBlockViewModel(write));
                    break;
                case CadAttributeDefinitionRequest definition when ShowAttributeDefinition != null:
                    e.Handled = ShowAttributeDefinition(new AttributeDefinitionViewModel(definition));
                    break;
                case CadAttributeEditRequest edit when ShowAttributeEditor != null:
                    e.Handled = ShowAttributeEditor(new AttributeEditorViewModel(edit));
                    break;
            }
        }
    }
}

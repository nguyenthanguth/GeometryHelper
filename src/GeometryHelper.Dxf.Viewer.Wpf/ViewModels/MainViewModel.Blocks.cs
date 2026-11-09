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

        // The block a tile of Insert's gallery gives the INSERT it starts, which then asks no palette; null for none.
        private string _galleryBlock;

        // The tab of the Blocks palette the INSERT started from Insert's gallery opens it on; null for Current drawing.
        private string _paletteTab;

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

        /// <summary>
        /// Starts INSERT placing a block of the drawing, as a tile of Insert's gallery does: the palette is not shown, the block
        /// is chosen with the palette's options (the point asked, scale 1, no rotation) and listed first under Recent.
        /// </summary>
        /// <param name="name">The name of the block.</param>
        internal void InsertBlock(string name)
        {
            if (_current == null || string.IsNullOrEmpty(name))
            {
                return;
            }

            _galleryBlock = name;

            try
            {
                StartCommand("INSERT");
            }
            finally
            {
                _galleryBlock = null;
            }
        }

        /// <summary>Starts INSERT with the Blocks palette opened on a tab, as Recent Blocks… and Blocks from Other Drawings… of Insert's gallery do.</summary>
        /// <param name="tab">The tab: "Recent" or "Other drawing"; null for Current drawing.</param>
        internal void OpenBlocksPalette(string tab)
        {
            _paletteTab = tab;

            try
            {
                StartCommand("INSERT");
            }
            finally
            {
                _paletteTab = null;
            }
        }

        /// <summary>A request of a block command: its dialog shown where the workspace shows it, the request answered.</summary>
        private void OnBlockRequested(CadUiRequestEventArgs e)
        {
            switch (e.Payload)
            {
                case CadBlockDefinitionRequest block when ShowBlockDefinition != null:
                    e.Handled = ShowBlockDefinition(new BlockDefinitionViewModel(block));
                    break;
                case CadInsertRequest insert when _galleryBlock != null:
                    insert.Name = _galleryBlock;
                    insert.Action = CadDialogAction.Ok;
                    e.Handled = true;
                    RememberInserted(insert.Name);
                    break;
                case CadInsertRequest insert when ShowBlocksPalette != null:
                    var palette = new BlocksPaletteViewModel(insert, _recentBlocks);

                    if (_paletteTab != null)
                    {
                        palette.Tab = _paletteTab;
                    }

                    e.Handled = ShowBlocksPalette(palette);

                    if (insert.Action == CadDialogAction.Ok && !string.IsNullOrEmpty(insert.Name))
                    {
                        RememberInserted(insert.Name);
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

        /// <summary>A block inserted put first under the palette's Recent tab, once.</summary>
        private void RememberInserted(string name)
        {
            _recentBlocks.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            _recentBlocks.Insert(0, name);
        }
    }
}

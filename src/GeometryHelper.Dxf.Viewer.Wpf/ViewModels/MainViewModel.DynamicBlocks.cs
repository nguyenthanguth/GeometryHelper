using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Dynamic blocks in use, as AutoCAD 2021 drives them: a flip grip clicked flips the block, a visibility or lookup grip
    /// clicked shows its list at the grip, the current value ticked; the right-click menu of a selection holding a dynamic
    /// block offers Reset Block (RESETBLOCK); a row of the Custom group of Properties is set by the property's name.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>
        /// A custom grip of a dynamic block reference clicked, as the canvas reports it: a flip grip flips the block at once,
        /// one undo step, and gives no list; a visibility or a lookup grip gives its list, a row of each value allowed in the
        /// parameter's order, the value shown ticked, a row chosen setting it.
        /// </summary>
        /// <param name="handle">The handle of the block reference.</param>
        /// <param name="gripIndex">The index of the grip, as <see cref="CadGrips.Of"/> gives it.</param>
        /// <returns>The rows of the list; null for a flip grip, a grip of another kind, or no drawing.</returns>
        public IReadOnlyList<CadMenuItemViewModel> ClickCustomGrip(string handle, int gripIndex)
        {
            if (_current == null || _current.Session.IsRunning)
            {
                return null;
            }

            CadDocument document = _current.Document;
            IReadOnlyList<string> values = CadDynamicBlocks.ValuesOf(document, handle, gripIndex, out string shown);

            if (values.Count == 0)
            {
                CadEditResult result = CadDynamicBlocks.Click(document, handle, gripIndex);

                if (!result.Succeeded)
                {
                    Say(result.Message);
                }

                return null;
            }

            var rows = new List<CadMenuItemViewModel>();

            foreach (string value in values)
            {
                string chosen = value;
                rows.Add(
                    new CadMenuItemViewModel(
                        value,
                        () =>
                        {
                            CadEditResult result = CadDynamicBlocks.Choose(document, handle, gripIndex, chosen);

                            if (!result.Succeeded)
                            {
                                Say(result.Message);
                            }
                        },
                        isChecked: value == shown
                    )
                );
            }

            return rows;
        }

        /// <summary>
        /// The right-click menu of the drawing shown while no command runs: the menu of the cells of a table where cells are
        /// selected (<see cref="TableCellViewModel.Menu"/>); else Reset Block, which runs RESETBLOCK on the selection, where it
        /// holds a dynamic block reference, and Table Indicator Color where it holds a table; null where it holds neither.
        /// </summary>
        /// <returns>The items; null for no menu.</returns>
        public IReadOnlyList<CadMenuItemViewModel> ShortcutMenu()
        {
            if (_current == null || _current.Session.IsRunning)
            {
                return null;
            }

            if (_tableCell != null)
            {
                return _tableCell.Menu();
            }

            CadDocument document = _current.Document;
            bool dynamic = document.Selection.Handles.Any(h =>
                document.FindEntity(h) is DxfBlockReference insert && IsDynamic(insert)
            );
            var items = new List<CadMenuItemViewModel>();

            if (dynamic)
            {
                items.Add(
                    new CadMenuItemViewModel(
                        "Reset Block",
                        () => StartCommand("RESETBLOCK"),
                        toolTip: "Resets the selected dynamic block references to their default values (RESETBLOCK)"
                    )
                );
            }

            if (TableSelected())
            {
                items.Add(new CadMenuItemViewModel("Table Indicator Color…", PickTableIndicatorColor));
            }

            return items.Count == 0 ? null : items;
        }

        /// <summary>Whether a block reference places a dynamic block; false where that cannot be read.</summary>
        private static bool IsDynamic(DxfBlockReference insert)
        {
            try
            {
                return insert.IsDynamicBlock;
            }
            catch (System.Exception exception)
                when (exception is System.NotSupportedException
                    || exception is System.InvalidOperationException
                    || exception is System.FormatException
                )
            {
                return false;
            }
        }

        /// <summary>A row of the Custom group typed or chosen: the dynamic property of its name set on the selection.</summary>
        private bool CommitCustomProperty(IReadOnlyCollection<string> handles, string name, string text)
        {
            if (_current == null)
            {
                return false;
            }

            CadEditResult result = CadDynamicBlocks.SetProperty(_current.Document, handles, name, text);

            if (!result.Succeeded)
            {
                Say(result.Message);
            }

            return result.Succeeded;
        }
    }
}

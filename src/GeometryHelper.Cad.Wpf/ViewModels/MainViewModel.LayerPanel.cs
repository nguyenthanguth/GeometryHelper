using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// What Home › Layers needs of the model beyond the layer box's list: the three state glyphs of each layer in the box, a
    /// click on one toggling that state of that layer through <see cref="CadLayerEditor"/> as the Layer manager's cells do
    /// (one undo step, taken back by LAYERP), and the Layer State box of the slide-out, which lists the drawing's layer states,
    /// restores the one picked and opens LAYERSTATE for "New Layer State..." and "Manage Layer States...".
    /// </summary>
    public sealed partial class MainViewModel
    {
        private const string UnsavedLayerState = "Unsaved Layer State";
        private const string NewLayerState = "New Layer State...";
        private const string ManageLayerStates = "Manage Layer States...";

        private ICommand _toggleLayerOn;
        private ICommand _toggleLayerFreeze;
        private ICommand _toggleLayerLock;
        private List<CadLayerStateRow> _layerStateRows;

        /// <summary>
        /// Gets the command of the bulb glyph of a layer in the layer box: its parameter is the layer, and it switches the layer
        /// on or off, the current layer too, as AutoCAD's box does.
        /// </summary>
        public ICommand ToggleLayerOnCommand =>
            _toggleLayerOn ??= new DelegateCommand(p =>
                ToggleLayer(p, (d, l) => CadLayerEditor.SetOn(d, l.Name, !l.IsOn))
            );

        /// <summary>
        /// Gets the command of the sun or snowflake glyph of a layer: it freezes the layer or thaws it; freezing the current
        /// layer is refused in the words of the Layer manager, on the command line, and nothing changes.
        /// </summary>
        public ICommand ToggleLayerFreezeCommand =>
            _toggleLayerFreeze ??= new DelegateCommand(p =>
                ToggleLayer(p, (d, l) => CadLayerEditor.SetFrozen(d, l.Name, !l.IsFrozen))
            );

        /// <summary>Gets the command of the padlock glyph of a layer: it locks the layer or unlocks it.</summary>
        public ICommand ToggleLayerLockCommand =>
            _toggleLayerLock ??= new DelegateCommand(p =>
                ToggleLayer(p, (d, l) => CadLayerEditor.SetLocked(d, l.Name, !l.IsLocked))
            );

        /// <summary>
        /// Gets the entries of the Layer State box: "Unsaved Layer State" while no saved state matches the layers, the drawing's
        /// layer states in ordinal order of their names, then "New Layer State..." and "Manage Layer States...".
        /// </summary>
        public IReadOnlyList<string> LayerStateItems
        {
            get
            {
                var items = new List<string>();
                List<CadLayerStateRow> rows = LayerStateRows();

                if (!rows.Any(r => r.SameAsDrawing))
                {
                    items.Add(UnsavedLayerState);
                }

                items.AddRange(rows.Select(r => r.Name));
                items.Add(NewLayerState);
                items.Add(ManageLayerStates);
                return items;
            }
        }

        /// <summary>
        /// Gets or sets the entry of the Layer State box: the state the layers are in (the one restored last where several match),
        /// else "Unsaved Layer State". Setting a state restores it as LAYERSTATE's Restore does, one undo step; the two last
        /// entries start LAYERSTATE. The box goes back to the state the drawing is in after any pick.
        /// </summary>
        public string SelectedLayerState
        {
            get
            {
                List<CadLayerStateRow> rows = LayerStateRows();
                CadLayerStateRow same = rows.FirstOrDefault(r =>
                    r.SameAsDrawing && (r.Mask & CadLayerStates.LastRestored) != 0
                ) ?? rows.FirstOrDefault(r => r.SameAsDrawing);
                return same?.Name ?? UnsavedLayerState;
            }
            set
            {
                if (_current != null && value != null && value != UnsavedLayerState)
                {
                    if (value == NewLayerState || value == ManageLayerStates)
                    {
                        StartCommand("LAYERSTATE");
                    }
                    else
                    {
                        StartCommand("-LAYER");

                        foreach (string line in new[] { "stAte", "Restore", value, "", "" })
                        {
                            Execute(line);
                        }
                    }
                }

                // The box shows what it was picked to; the state the drawing is in is put back once the pick is done.
                (System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).BeginInvoke(
                    new Action(() => Raise(nameof(SelectedLayerState)))
                );
            }
        }

        /// <summary>The layers or their states changed: the Layer State box reads its list and its selection again.</summary>
        private void RaiseLayerStates()
        {
            _layerStateRows = null;
            Raise(nameof(LayerStateItems));
            Raise(nameof(SelectedLayerState));
        }

        private List<CadLayerStateRow> LayerStateRows() =>
            _layerStateRows ??=
                _current == null
                    ? new List<CadLayerStateRow>()
                    : CadLayerStates.Rows(_current.Document.Database, false);

        private void ToggleLayer(object parameter, Func<CadDocument, CadLayerInfo, CadEditResult> edit)
        {
            if (_current != null && parameter is CadLayerInfo layer)
            {
                CadEditResult result = edit(_current.Document, layer);

                if (!result.Succeeded)
                {
                    Say(result.Message);
                }
            }
        }
    }
}

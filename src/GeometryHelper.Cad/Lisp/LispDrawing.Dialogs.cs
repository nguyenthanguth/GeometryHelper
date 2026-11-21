using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Cad.Dcl;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// The 22 functions of AutoLISP's DCL dialogs, which every session's interpreter has as AutoCAD has them from the start:
    /// <c>load_dialog</c>, <c>unload_dialog</c>, <c>new_dialog</c>, <c>start_dialog</c>, <c>done_dialog</c>,
    /// <c>term_dialog</c>, <c>action_tile</c>, <c>get_tile</c>, <c>set_tile</c>, <c>mode_tile</c>, <c>get_attr</c>,
    /// <c>client_data_tile</c>, <c>start_list</c>, <c>add_list</c>, <c>end_list</c>, <c>start_image</c>, <c>fill_image</c>,
    /// <c>vector_image</c>, <c>slide_image</c>, <c>end_image</c>, <c>dimx_tile</c> and <c>dimy_tile</c>. A dialog is shown by
    /// the session's <see cref="CadCommandSession.DclHost"/>; with none, <c>new_dialog</c> gives nil as AutoCAD's console does.
    /// While <c>start_dialog</c> runs, it takes the events the host reports one at a time and runs the action each one stands
    /// for in the routine's own evaluation, with <c>$key</c>, <c>$value</c>, <c>$reason</c>, <c>$data</c>, <c>$x</c> and
    /// <c>$y</c> bound for the action alone.
    /// </summary>
    internal static partial class LispDrawing
    {
        static partial void RegisterDialogs(LispInterpreter lisp, CadCommandSession session)
        {
            new DialogFunctions(lisp, session).Register();
        }

        /// <summary>The dialogs of one interpreter: the ones made and not yet ended, newest last, and the list and image being filled.</summary>
        private sealed class DialogFunctions
        {
            private static readonly string[] ActionVariables = { "$KEY", "$VALUE", "$REASON", "$DATA", "$X", "$Y" };

            private readonly LispInterpreter _lisp;
            private readonly CadCommandSession _session;
            private readonly List<DclDialog> _dialogs = new List<DclDialog>();
            private DclTile _list;
            private int _listOperation;
            private int _listIndex;
            private DclTile _image;

            internal DialogFunctions(LispInterpreter lisp, CadCommandSession session)
            {
                _lisp = lisp;
                _session = session;
            }

            private DclDialog Current => _dialogs.Count == 0 ? null : _dialogs[_dialogs.Count - 1];

            internal void Register()
            {
                _lisp.Define("LOAD_DIALOG", 1, 1, LoadDialog);
                _lisp.Define("UNLOAD_DIALOG", 1, 1, UnloadDialog);
                _lisp.Define("NEW_DIALOG", 2, 4, NewDialog);
                _lisp.Define("START_DIALOG", 0, 0, a => StartDialog());
                _lisp.Define("DONE_DIALOG", 0, 1, DoneDialog);
                _lisp.Define("TERM_DIALOG", 0, 0, a => TermDialog());
                _lisp.Define("ACTION_TILE", 2, 2, ActionTile);
                _lisp.Define("GET_TILE", 1, 1, GetTile);
                _lisp.Define("SET_TILE", 2, 2, SetTile);
                _lisp.Define("MODE_TILE", 2, 2, ModeTile);
                _lisp.Define("GET_ATTR", 2, 2, GetAttr);
                _lisp.Define("CLIENT_DATA_TILE", 2, 2, ClientDataTile);
                _lisp.Define("START_LIST", 1, 3, StartList);
                _lisp.Define("ADD_LIST", 1, 1, AddList);
                _lisp.Define("END_LIST", 0, 0, a => EndList());
                _lisp.Define("START_IMAGE", 1, 1, StartImage);
                _lisp.Define("FILL_IMAGE", 5, 5, FillImage);
                _lisp.Define("VECTOR_IMAGE", 5, 5, VectorImage);
                _lisp.Define("SLIDE_IMAGE", 5, 5, SlideImage);
                _lisp.Define("END_IMAGE", 0, 0, a => EndImage());
                _lisp.Define("DIMX_TILE", 1, 1, a => Size(LispInterpreter.Str(a[0]), width: true));
                _lisp.Define("DIMY_TILE", 1, 1, a => Size(LispInterpreter.Str(a[0]), width: false));
            }

            #region Files and dialogs

            /// <summary>
            /// <c>(load_dialog file)</c>: the id of the file, -1 where it is not found. A file that cannot be parsed shows its
            /// message through the session's alert and gives -1 too.
            /// </summary>
            private object LoadDialog(object[] a)
            {
                string name = LispInterpreter.Str(a[0]);

                try
                {
                    return _session.DclLibrary.Load(name);
                }
                catch (DclException error)
                {
                    _lisp.OnOwner(() =>
                    {
                        _session.ShowAlert(error.Message);
                        return null;
                    });
                    return -1;
                }
            }

            private object UnloadDialog(object[] a)
            {
                _session.DclLibrary.Unload(LispInterpreter.Int(a[0]));
                return null;
            }

            /// <summary>
            /// <c>(new_dialog name id [action [screen-pt]])</c>: T and the dialog is the current one, not yet shown; nil where the
            /// session has no host, the id or the name is unknown, or the dialog has no tile that can end it.
            /// </summary>
            private object NewDialog(object[] a)
            {
                string name = LispInterpreter.Str(a[0]);
                int id = LispInterpreter.Int(a[1]);
                string action = a.Length > 2 ? LispInterpreter.Str(a[2]) : string.Empty;
                int? x = null;
                int? y = null;

                if (a.Length > 3)
                {
                    ScreenPoint(a[3], out x, out y);
                }

                DclTile root = _session.DclHost == null ? null : _session.DclLibrary.Get(id)?.NewDialog(name);

                if (root == null)
                {
                    return null;
                }

                var dialog = new DclDialog(root, id, name, _dialogs.LastOrDefault(open => open.IsStarted));

                if (!dialog.HasExitTile())
                {
                    return null;
                }

                dialog.Initialise();
                dialog.ScreenX = x;
                dialog.ScreenY = y;
                dialog.DefaultAction = action.Length == 0 ? null : action;

                // A dialog made and never started is given up for the new one.
                _dialogs.RemoveAll(open => !open.IsStarted);
                _dialogs.Add(dialog);
                _list = null;
                _image = null;
                return _lisp.Bool(true);
            }

            private static void ScreenPoint(object point, out int? x, out int? y)
            {
                List<object> items = point is LispCons ? LispInterpreter.ItemsOf(point) : null;

                if (items == null || items.Count != 2 || !items.All(item => item is int || item is double))
                {
                    throw new LispException(
                        "bad argument value: dialog position: " + LispInterpreter.Print(point, true)
                    );
                }

                // A coordinate below zero, as the -1 of '(-1 -1), leaves the dialog centred along that axis.
                int first = Convert.ToInt32(Math.Round(Convert.ToDouble(items[0])));
                int second = Convert.ToInt32(Math.Round(Convert.ToDouble(items[1])));
                x = first < 0 ? (int?)null : first;
                y = second < 0 ? (int?)null : second;
            }

            #endregion

            #region Running a dialog

            /// <summary>
            /// <c>(start_dialog)</c>: shows the current dialog through the host and runs what the user does until it ends. Gives
            /// the status <c>done_dialog</c> gave (0 by default), -1 where <c>term_dialog</c> ended it; 0 where no dialog is
            /// waiting to be shown.
            /// </summary>
            private object StartDialog()
            {
                DclDialog dialog = Current;
                IDclHost host = _session.DclHost;

                if (dialog == null || dialog.IsStarted || host == null)
                {
                    return 0;
                }

                dialog.IsStarted = true;
                _lisp.RunningDialogs++;

                try
                {
                    _lisp.OnOwner(() =>
                    {
                        host.Open(dialog);
                        return null;
                    });

                    while (!dialog.IsDone)
                    {
                        var next = (DclEvent)_lisp.OnOwner(() => host.Next(dialog));

                        // A host that ends the dialog itself has nothing more to report.
                        if (dialog.IsDone)
                        {
                            break;
                        }

                        Handle(dialog, next);
                    }

                    if (!dialog.IsTerminated)
                    {
                        LeaveEditBox(dialog);
                    }
                }
                finally
                {
                    // The dialog ends however it ends: done, terminated, or an error that ends every dialog.
                    dialog.IsDone = true;
                    _lisp.RunningDialogs--;
                    _dialogs.Remove(dialog);
                    _list = null;
                    _image = null;
                    _lisp.OnOwner(() =>
                    {
                        host.Close(dialog);
                        return null;
                    });
                }

                return dialog.Status;
            }

            private void Handle(DclDialog dialog, DclEvent dclEvent)
            {
                switch (dclEvent.Kind)
                {
                    case DclEventKind.Tile:
                        DclTile tile = dialog.Find(dclEvent.Key);

                        if (tile != null && tile.IsEnabled)
                        {
                            Press(dialog, tile, dclEvent.Value, dclEvent.Reason, dclEvent.X, dclEvent.Y);
                        }

                        break;
                    case DclEventKind.Enter:
                        Enter(dialog, dclEvent.Key);
                        break;
                    default:
                        Escape(dialog);
                        break;
                }
            }

            /// <summary>
            /// A tile is used: the value goes into it, then its action runs, and then what the tile does by itself (D324, D340):
            /// OK with no action ends the dialog with 1, Cancel ends it with 0 whatever its action did, and an OK whose action
            /// does not end the dialog leaves it open.
            /// </summary>
            private void Press(DclDialog dialog, DclTile tile, string value, int reason, int x, int y)
            {
                dialog.ApplyEvent(tile, value);

                if (reason == 2 && string.Equals(tile.Key, dialog.PendingEditKey, StringComparison.Ordinal))
                {
                    dialog.PendingEditKey = null;
                }

                if (!RunActionOf(dialog, tile, value, reason, x, y))
                {
                    if (DclDialog.IsAccept(tile))
                    {
                        End(dialog, 1);
                    }
                    else if (DclDialog.IsCancel(tile))
                    {
                        End(dialog, 0);
                    }
                }
                else if (DclDialog.IsCancel(tile) && !DclDialog.IsAccept(tile) && !dialog.IsDone)
                {
                    End(dialog, 0);
                }

                // reference: a double click in a list box with allow_accept presses the default tile after the action
                if (reason == 4 && tile.Kind == "list_box" && tile.IsTrue("allow_accept") && !dialog.IsDone)
                {
                    PressDefault(dialog);
                }
            }

            private void PressDefault(DclDialog dialog)
            {
                DclTile accept = dialog.DefaultTile();

                if (accept != null && accept.IsEnabled)
                {
                    Press(dialog, accept, "1", 1, 0, 0);
                }
            }

            /// <summary>
            /// Enter (D340): in an edit box it runs the box's action with <c>$reason</c> 1 and leaves the dialog open, or with
            /// <c>allow_accept</c> presses the default tile instead; on a button it presses that button; anywhere else it presses
            /// the default tile.
            /// </summary>
            private void Enter(DclDialog dialog, string key)
            {
                DclTile tile = key == null ? null : dialog.Find(key);

                if (tile != null && !tile.IsEnabled)
                {
                    return;
                }

                if (tile != null && tile.Kind == "edit_box" && !tile.IsTrue("allow_accept"))
                {
                    Press(dialog, tile, dialog.GetValue(tile), 1, 0, 0);
                }
                else if (tile != null && (tile.Kind == "button" || tile.Kind == "image_button"))
                {
                    Press(dialog, tile, "1", 1, 0, 0);
                }
                else
                {
                    PressDefault(dialog);
                }
            }

            /// <summary>Esc or the close box presses the tile that cancels; with none the dialog ends with 0.</summary>
            private void Escape(DclDialog dialog)
            {
                DclTile cancel = dialog.CancelTile();

                if (cancel == null)
                {
                    End(dialog, 0);
                }
                else if (cancel.IsEnabled)
                {
                    Press(dialog, cancel, "1", 1, 0, 0);
                }
            }

            /// <summary>The edit box the user typed in and did not leave gets its action, with <c>$reason</c> 2, as the dialog closes.</summary>
            private void LeaveEditBox(DclDialog dialog)
            {
                string key = dialog.PendingEditKey;
                dialog.PendingEditKey = null;
                DclTile tile = key == null ? null : dialog.Find(key);

                if (tile != null)
                {
                    RunActionOf(dialog, tile, dialog.GetValue(tile), 2, 0, 0);
                }
            }

            private static void End(DclDialog dialog, int status)
            {
                dialog.IsDone = true;
                dialog.Status = status;
            }

            /// <summary>
            /// Finds the action of a tile and runs it (D323): its <c>action_tile</c>, else its <c>action</c> attribute, else, for
            /// a radio button, its cluster's (with the cluster's key as <c>$key</c> and the button's key as <c>$value</c>), else
            /// the default action of <c>new_dialog</c>. Gives false when there is none.
            /// </summary>
            private bool RunActionOf(DclDialog dialog, DclTile tile, string value, int reason, int x, int y)
            {
                string key = tile.Key;
                string given = value;
                string action = ActionOf(tile);

                if (action == null && tile.Kind == "radio_button")
                {
                    DclTile cluster = DclDialog.ClusterOf(tile);
                    action = cluster == null ? null : ActionOf(cluster);

                    if (action != null)
                    {
                        key = cluster.Key;
                        given = tile.Key;
                    }
                }

                action = action ?? dialog.DefaultAction;

                if (action == null)
                {
                    return false;
                }

                RunAction(action, key, given, reason, tile.ClientData, x, y);
                return true;
            }

            private static string ActionOf(DclTile tile)
            {
                string action = tile.Action ?? tile.Get("action");
                return string.IsNullOrEmpty(action) ? null : action;
            }

            /// <summary>
            /// Evaluates an action's string with the six variables bound for it alone, and given back as they were after it. An
            /// error in it ends the whole evaluation, every dialog with it (D343).
            /// </summary>
            private void RunAction(string action, string key, string value, int reason, string data, int x, int y)
            {
                object[] saved = ActionVariables.Select(_lisp.GetValue).ToArray();
                _lisp.SetValue("$KEY", key);
                _lisp.SetValue("$VALUE", value);
                _lisp.SetValue("$REASON", reason);
                _lisp.SetValue("$DATA", data);
                _lisp.SetValue("$X", x);
                _lisp.SetValue("$Y", y);

                try
                {
                    _lisp.EvalText(action);
                }
                catch (LispException error) when (!error.IsHard)
                {
                    throw _lisp.EndEvaluation(error.Message, error);
                }
                finally
                {
                    for (int i = 0; i < ActionVariables.Length; i++)
                    {
                        _lisp.SetValue(ActionVariables[i], saved[i]);
                    }
                }
            }

            /// <summary>
            /// <c>(done_dialog [status])</c>: ends the dialog being shown when the action returns, with the status given, 0 by
            /// default; gives where the dialog stood, as a list of two integers, <c>(0 0)</c> where none is being shown.
            /// </summary>
            private object DoneDialog(object[] a)
            {
                int? status = a.Length > 0 ? LispInterpreter.Int(a[0]) : (int?)null;
                DclDialog dialog = Current;

                if (dialog == null || !dialog.IsStarted)
                {
                    return LispInterpreter.ListOf(new object[] { 0, 0 });
                }

                End(dialog, status ?? 0);
                return LispInterpreter.ListOf(new object[] { dialog.PositionX, dialog.PositionY });
            }

            /// <summary><c>(term_dialog)</c>: ends every dialog, each <c>start_dialog</c> giving -1.</summary>
            private object TermDialog()
            {
                _dialogs.RemoveAll(open => !open.IsStarted);

                foreach (DclDialog dialog in _dialogs)
                {
                    dialog.IsTerminated = true;
                    End(dialog, -1);
                }

                return null;
            }

            #endregion

            #region Tiles

            /// <summary><c>(action_tile key action)</c>: T, also for a key no tile has; nil with no dialog. <c>""</c> takes the action away.</summary>
            private object ActionTile(object[] a)
            {
                string key = LispInterpreter.Str(a[0]);
                string action = LispInterpreter.Str(a[1]);
                DclDialog dialog = Current;

                if (dialog == null)
                {
                    return null;
                }

                DclTile tile = dialog.Find(key);

                if (tile != null)
                {
                    tile.Action = action.Length == 0 ? null : action;
                }

                return _lisp.Bool(true);
            }

            private object GetTile(object[] a)
            {
                string key = LispInterpreter.Str(a[0]);
                DclDialog dialog = Current;
                DclTile tile = dialog?.Find(key);
                return tile == null ? null : dialog.GetValue(tile);
            }

            /// <summary><c>(set_tile key value)</c>: the value given, nil for a key no tile has or with no dialog.</summary>
            private object SetTile(object[] a)
            {
                string key = LispInterpreter.Str(a[0]);
                string value = LispInterpreter.Str(a[1]);
                DclDialog dialog = Current;
                DclTile tile = dialog?.Find(key);

                if (tile == null)
                {
                    return null;
                }

                dialog.SetValue(tile, value);
                return value;
            }

            /// <summary><c>(mode_tile key mode)</c>: 0 enables, 1 disables, 2 gives the focus, 3 selects an edit box's text, 4 flips an image button's highlight; any other number does nothing. Gives nil.</summary>
            private object ModeTile(object[] a)
            {
                string key = LispInterpreter.Str(a[0]);
                int mode = LispInterpreter.Int(a[1]);
                DclDialog dialog = Current;
                DclTile tile = dialog?.Find(key);

                if (tile == null)
                {
                    return null;
                }

                switch (mode)
                {
                    case 0:
                        tile.IsEnabled = true;
                        break;
                    case 1:
                        tile.IsEnabled = false;
                        break;
                    case 2:
                        dialog.FocusKey = key;
                        break;
                    case 3:
                        dialog.SelectTextKey = key;
                        break;
                    case 4:
                        tile.IsFlipped = !tile.IsFlipped;
                        break;
                }

                return null;
            }

            /// <summary><c>(get_attr key attribute)</c>: the attribute's text as written in the DCL file; <c>""</c> where the tile or the attribute is not there.</summary>
            private object GetAttr(object[] a)
            {
                string key = LispInterpreter.Str(a[0]);
                string attribute = LispInterpreter.Str(a[1]);
                return Current?.Find(key)?.Get(attribute) ?? string.Empty;
            }

            /// <summary><c>(client_data_tile key data)</c>: keeps the string as the <c>$data</c> of the tile's actions and gives it; nil for a key no tile has.</summary>
            private object ClientDataTile(object[] a)
            {
                string key = LispInterpreter.Str(a[0]);
                string data = LispInterpreter.Str(a[1]);
                DclTile tile = Current?.Find(key);

                if (tile == null)
                {
                    return null;
                }

                tile.ClientData = data;
                return data;
            }

            /// <summary>The width or height of a tile as the layout makes it, in whole pixels; 0 for a key no tile has or with no dialog.</summary>
            private object Size(string key, bool width)
            {
                DclDialog dialog = Current;
                DclTile tile = dialog?.Find(key);
                return tile == null ? 0 : Size(dialog, tile, width);
            }

            // Laid out with the host's metrics, so that the size is what its window draws; on the owner thread, where the host's
            // calls come. With no host, or one that draws nothing, the headless defaults.
            private int Size(DclDialog dialog, DclTile tile, bool width)
            {
                IDclHost host = _session.DclHost;
                var placed =
                    host == null
                        ? DclLayout.Arrange(dialog.Root, DclMetrics.Default)
                        : (IReadOnlyDictionary<DclTile, DclRect>)
                            _lisp.OnOwner(() => DclLayout.Arrange(dialog.Root, host.Metrics));

                if (!placed.TryGetValue(tile, out DclRect rect))
                {
                    return 0;
                }

                return (int)Math.Floor(width ? rect.Width : rect.Height);
            }

            #endregion

            #region Lists

            /// <summary>
            /// <c>(start_list key [operation [index]])</c>: starts filling the list box or popup of the key: operation 3 (the
            /// default) a new list, 2 appends, 1 replaces the item at the index. Gives the key; nil for a key no tile has.
            /// </summary>
            private object StartList(object[] a)
            {
                string key = LispInterpreter.Str(a[0]);
                int operation = a.Length > 1 ? LispInterpreter.Int(a[1]) : 3;
                int index = a.Length > 2 ? LispInterpreter.Int(a[2]) : 0;
                _list = Current?.Find(key);

                if (_list == null)
                {
                    return null;
                }

                _listOperation = operation == 1 || operation == 2 ? operation : 3;
                _listIndex = index;

                if (_listOperation == 3)
                {
                    _list.Items.Clear();
                }

                return key;
            }

            /// <summary><c>(add_list string)</c>: adds the item, or replaces the one <c>start_list</c> named, and gives the string; nil where no list is being filled.</summary>
            private object AddList(object[] a)
            {
                string item = LispInterpreter.Str(a[0]);

                if (_list == null)
                {
                    return null;
                }

                if (_listOperation != 1)
                {
                    _list.Items.Add(item);
                }
                else if (_listIndex >= 0 && _listIndex < _list.Items.Count)
                {
                    _list.Items[_listIndex] = item;
                }

                return item;
            }

            private object EndList()
            {
                if (_list != null)
                {
                    Current?.ListEnded(_list, _listOperation, _listIndex);
                }

                _list = null;
                return null;
            }

            #endregion

            #region Images

            /// <summary>
            /// <c>(start_image key)</c>: starts drawing on the image or image button of the key and gives the key, also where
            /// no tile has it (the drawing then goes nowhere); nil with no dialog.
            /// </summary>
            private object StartImage(object[] a)
            {
                string key = LispInterpreter.Str(a[0]);
                DclDialog dialog = Current;

                if (dialog == null)
                {
                    return null;
                }

                DclTile tile = dialog.Find(key);
                _image = tile != null && (tile.Kind == "image" || tile.Kind == "image_button") ? tile : null;
                return key;
            }

            /// <summary>
            /// <c>(fill_image x y width height color)</c>: a filled rectangle on the image, in pixels from its top-left. One that
            /// covers the whole tile clears the earlier drawing first. Gives the colour; nil with no dialog.
            /// </summary>
            private object FillImage(object[] a)
            {
                int x = LispInterpreter.Int(a[0]);
                int y = LispInterpreter.Int(a[1]);
                int width = LispInterpreter.Int(a[2]);
                int height = LispInterpreter.Int(a[3]);
                int color = LispInterpreter.Int(a[4]);
                DclDialog dialog = Current;

                if (dialog == null)
                {
                    return null;
                }

                if (_image != null)
                {
                    if (
                        x <= 0
                        && y <= 0
                        && x + width >= Size(dialog, _image, width: true)
                        && y + height >= Size(dialog, _image, width: false)
                    )
                    {
                        _image.Picture.Clear();
                    }

                    _image.Picture.Add(new DclImageOp(DclImageOpKind.Fill, x, y, width, height, color));
                }

                return color;
            }

            /// <summary><c>(vector_image x1 y1 x2 y2 color)</c>: a line on the image. Gives the colour; nil with no dialog.</summary>
            private object VectorImage(object[] a)
            {
                int x1 = LispInterpreter.Int(a[0]);
                int y1 = LispInterpreter.Int(a[1]);
                int x2 = LispInterpreter.Int(a[2]);
                int y2 = LispInterpreter.Int(a[3]);
                int color = LispInterpreter.Int(a[4]);

                if (Current == null)
                {
                    return null;
                }

                _image?.Picture.Add(new DclImageOp(DclImageOpKind.Vector, x1, y1, x2, y2, color));
                return color;
            }

            /// <summary>
            /// <c>(slide_image x y width height name)</c>: draws nothing, as slides are not read yet (D320), and says so once for
            /// the dialog on the command line. Gives nil.
            /// </summary>
            private object SlideImage(object[] a)
            {
                LispInterpreter.Int(a[0]);
                LispInterpreter.Int(a[1]);
                LispInterpreter.Int(a[2]);
                LispInterpreter.Int(a[3]);
                LispInterpreter.Str(a[4]);
                DclDialog dialog = Current;

                if (dialog != null && !dialog.SlideNoticeWritten)
                {
                    dialog.SlideNoticeWritten = true;
                    _lisp.Write("slide_image: slides are not supported by GeometryHelper.Cad yet\n");
                }

                return null;
            }

            private object EndImage()
            {
                _image = null;
                return null;
            }

            #endregion
        }
    }
}

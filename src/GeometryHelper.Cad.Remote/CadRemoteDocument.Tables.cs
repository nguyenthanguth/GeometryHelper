using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Remote
{
    /// <summary>The layers and the layouts of a drawing.</summary>
    public sealed partial class CadRemoteDocument
    {
        /// <summary>
        /// Gets the layers of the drawing as the Layer manager lists them, in the order of its table. They are copies, asked of
        /// the app each time: change one and give it to <see cref="ModifyLayer"/>.
        /// </summary>
        /// <exception cref="CadRemoteException">Thrown when the drawing is closed.</exception>
        public IReadOnlyList<CadRemoteLayer> Layers
        {
            get
            {
                var layers = new List<CadRemoteLayer>();

                foreach (object item in Ask(RemoteOps.Layers).GetArray("layers"))
                {
                    layers.Add(CadRemoteLayer.FromJson((JsonObject)item));
                }

                return layers;
            }
        }

        /// <summary>
        /// Queues a new layer, to be made when <see cref="CommitChanges"/> is called. The layers of a commit are made before its
        /// objects, so that an object inserted in the same commit can be on the layer. The layer is copied now, so changing it
        /// afterwards changes nothing.
        /// </summary>
        /// <param name="layer">The layer.</param>
        /// <exception cref="ArgumentNullException">Thrown when the layer is null.</exception>
        public void AddLayer(CadRemoteLayer layer) =>
            _queue.Add(new Pending { Kind = "addLayer", Layer = CopyOf(layer) });

        /// <summary>
        /// Queues the look of a layer to be written over the look the layer of that name has, when <see cref="CommitChanges"/>
        /// is called: the layer read from <see cref="Layers"/> and changed since. Everything but the name is written; a property
        /// left as the drawing has it changes nothing. The app refuses a layer of an external reference and the freezing of the
        /// current layer.
        /// </summary>
        /// <param name="layer">The layer.</param>
        /// <exception cref="ArgumentNullException">Thrown when the layer is null.</exception>
        public void ModifyLayer(CadRemoteLayer layer) =>
            _queue.Add(new Pending { Kind = "modifyLayer", Layer = CopyOf(layer) });

        private static CadRemoteLayer CopyOf(CadRemoteLayer layer)
        {
            if (layer == null)
            {
                throw new ArgumentNullException(nameof(layer));
            }

            return CadRemoteLayer.FromJson(layer.ToJson());
        }

        /// <summary>Gets or sets the name of the layer new objects are drawn on, which an object inserted with no layer of its own is put on.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="CadRemoteException">Thrown when the drawing is closed, or on setting a layer the drawing has not, a frozen layer or a layer of an external reference.</exception>
        public string CurrentLayer
        {
            get => Ask(RemoteOps.CurrentLayer).GetString("name");
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                Ask(RemoteOps.CurrentLayer, request => request.Set("name", value));
            }
        }

        /// <summary>
        /// Gets the text styles of the drawing, sorted by name without regard to case, then by ordinal order. They are copies,
        /// asked of the app each time: changing one changes nothing in the app.
        /// </summary>
        /// <exception cref="CadRemoteException">Thrown when the drawing is closed.</exception>
        public IReadOnlyList<DxfTextStyleTableRecord> TextStyles
        {
            get
            {
                JsonObject answer = Ask(RemoteOps.TextStyles);
                DxfDatabase fragment = DxfDatabase.Parse(answer.GetString("fragment"));
                var styles = new List<DxfTextStyleTableRecord>();

                foreach (object name in answer.GetArray("names"))
                {
                    styles.Add(fragment.TextStyleTable[(string)name]);
                }

                return styles;
            }
        }

        /// <summary>
        /// Gets the dimension styles of the drawing, sorted by name without regard to case, then by ordinal order. They are
        /// copies, asked of the app each time, with the text style, linetypes and arrowhead blocks they point at: changing one
        /// changes nothing in the app.
        /// </summary>
        /// <exception cref="CadRemoteException">Thrown when the drawing is closed.</exception>
        public IReadOnlyList<DxfDimStyleTableRecord> DimensionStyles
        {
            get
            {
                JsonObject answer = Ask(RemoteOps.DimStyles);
                DxfDatabase fragment = DxfDatabase.Parse(answer.GetString("fragment"));
                var styles = new List<DxfDimStyleTableRecord>();

                foreach (object name in answer.GetArray("names"))
                {
                    styles.Add(fragment.DimStyleTable[(string)name]);
                }

                return styles;
            }
        }

        /// <summary>
        /// Gets the blocks of the drawing with their objects, sorted by name without regard to case, then by ordinal order, as
        /// the Block manager lists them: the blocks of the layouts (<c>*Model_Space</c>, <c>*Paper_Space</c>) and those AutoCAD
        /// names itself (<c>*D1</c>, <c>*U1</c>) are left out. They are copies, asked of the app each time: changing one
        /// changes nothing in the app.
        /// </summary>
        /// <exception cref="CadRemoteException">Thrown when the drawing is closed.</exception>
        public IReadOnlyList<DxfBlockTableRecord> Blocks
        {
            get
            {
                JsonObject answer = Ask(RemoteOps.Blocks);
                DxfDatabase fragment = DxfDatabase.Parse(answer.GetString("fragment"));
                var blocks = new List<DxfBlockTableRecord>();

                foreach (object name in answer.GetArray("names"))
                {
                    blocks.Add(fragment.BlockTable[(string)name]);
                }

                return blocks;
            }
        }

        /// <summary>
        /// Gets the names of the tabs of the drawing in the order they are shown: <c>Model</c>, then each layout. Asked of the app
        /// each time.
        /// </summary>
        /// <exception cref="CadRemoteException">Thrown when the drawing is closed.</exception>
        public IReadOnlyList<string> Layouts
        {
            get
            {
                var names = new List<string>();

                foreach (object item in Ask(RemoteOps.Layouts).GetArray("layouts"))
                {
                    names.Add(item as string ?? string.Empty);
                }

                return names;
            }
        }

        /// <summary>
        /// Gets or sets the tab the drawing shows, <c>Model</c> or the name of a layout, as clicking the tab does: switching is
        /// an undo step of the app (LAYOUT), and the objects of the layout shown are what the user sees and picks. The drawing
        /// is shown in the app first.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="CadRemoteException">Thrown when the drawing is closed, has no tab of that name, or has a command running in it.</exception>
        public string CurrentLayout
        {
            get => Ask(RemoteOps.CurrentLayout).GetString("name");
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                Ask(RemoteOps.CurrentLayout, request => request.Set("name", value));
            }
        }
    }
}

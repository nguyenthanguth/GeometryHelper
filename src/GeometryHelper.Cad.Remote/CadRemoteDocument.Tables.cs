using System;
using System.Collections.Generic;

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

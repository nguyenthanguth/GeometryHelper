using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Cad.Remote;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>Reading objects: by handle, by filter, and the packing of them into the small drawing they travel in.</summary>
    public sealed partial class CadRemoteServer
    {
        private JsonObject Get(CadDocument document, JsonArray handles)
        {
            var entities = new List<DxfEntity>();

            foreach (object item in handles)
            {
                DxfEntity entity = item is string handle ? document.FindEntity(handle) : null;

                if (entity != null && !entity.IsErased)
                {
                    entities.Add(entity);
                }
            }

            return Pack(document, entities);
        }

        private JsonObject Query(CadDocument document, JsonObject request)
        {
            Type kind = null;
            string kindName = request.GetString("kind");

            if (kindName != null)
            {
                kind = typeof(DxfEntity).Assembly.GetType(kindName);

                if (kind == null || !typeof(DxfEntity).IsAssignableFrom(kind))
                {
                    throw new Refusal(kindName + " is not a kind of object the drawing holds.");
                }
            }

            string layer = request.GetString("layer");
            string layout = request.GetString("layout");
            IEnumerable<CadSpace> spaces = document.Spaces.Where(s => !s.IsBlockEditor);

            if (layout != null)
            {
                spaces = spaces.Where(s => string.Equals(s.Name, layout, StringComparison.OrdinalIgnoreCase));

                if (!spaces.Any())
                {
                    throw new Refusal("The drawing " + document.Title + " has no layout named " + layout + ".");
                }
            }

            var found = new List<DxfEntity>();

            foreach (CadSpace space in spaces)
            {
                foreach (DxfEntity entity in space.Block)
                {
                    if (
                        !entity.IsErased
                        && (kind == null || kind.IsInstanceOfType(entity))
                        && (layer == null || string.Equals(entity.Layer, layer, StringComparison.OrdinalIgnoreCase))
                    )
                    {
                        found.Add(entity);
                    }
                }
            }

            return Pack(document, found);
        }

        /// <summary>
        /// Puts the objects into a small drawing, each with the layers, styles and blocks it needs: the DXF text, and for each
        /// object that could be put in, its handle in the drawing and its handle in the small drawing, which the client reads
        /// the object by. An object that cannot be copied (a record that follows another, as an ATTRIB) is left out.
        /// </summary>
        private static JsonObject Pack(CadDocument document, IReadOnlyList<DxfEntity> entities)
        {
            var handles = new JsonArray();
            var references = new JsonArray();

            if (entities.Count == 0)
            {
                return new JsonObject().Set("handles", handles).Set("refs", references);
            }

            var fragment = new DxfDatabase(RemoteFragment.Version);
            IReadOnlyList<DxfEntity> copies = null;

            try
            {
                copies = document.Database.WblockCloneObjects(entities, fragment.ModelSpace);
            }
            catch (ArgumentException ex)
            {
                GeometryHelperLog.Debug("CadRemoteServer: the objects were copied one by one: " + ex.Message);
            }

            if (copies != null)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    Note(entities[i], copies[i], handles, references);
                }
            }
            else
            {
                foreach (DxfEntity entity in entities)
                {
                    try
                    {
                        Note(
                            entity,
                            document.Database.WblockCloneObjects(new[] { entity }, fragment.ModelSpace)[0],
                            handles,
                            references
                        );
                    }
                    catch (ArgumentException ex)
                    {
                        GeometryHelperLog.Debug("CadRemoteServer: " + entity + " was left out: " + ex.Message);
                    }
                }
            }

            return new JsonObject().Set("fragment", fragment.ToText()).Set("handles", handles).Set("refs", references);
        }

        private static void Note(DxfEntity original, DxfEntity copy, JsonArray handles, JsonArray references)
        {
            if (!string.IsNullOrEmpty(copy.Handle) && !copy.IsErased && !string.IsNullOrEmpty(original.Handle))
            {
                handles.Add(original.Handle);
                references.Add(copy.Handle);
            }
        }
    }
}

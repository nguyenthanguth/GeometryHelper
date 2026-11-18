using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Remote
{
    /// <summary>
    /// One drawing of a running GeometryHelper CAD. It always means its own drawing, whichever tab the user has shown
    /// meanwhile; if the user closes the drawing, the next call throws a <see cref="CadRemoteException"/> and never reaches
    /// another drawing in its place.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads are live: <see cref="GetObject"/>, <see cref="GetObjects"/> and <see cref="GetSelectedObjects"/> ask the app now,
    /// and give the library's own entities (<c>DxfLine</c>, <c>DxfMText</c>, <c>DxfBlockReference</c>...) made from the DXF text
    /// of the objects. They are copies: changing one changes nothing in the app until <see cref="Modify"/> and
    /// <see cref="CommitChanges"/>. The <c>Handle</c> of such a copy is its handle in the copy's own small drawing; the handle
    /// in the app's drawing is <see cref="HandleOf"/>.
    /// </para>
    /// <para>
    /// Writes are queued: <see cref="Insert"/>, <see cref="Modify"/> and <see cref="Delete(string)"/> keep what they were
    /// given as it is at the call, and <see cref="CommitChanges"/> sends them all and the app applies them in order as one
    /// undo step with the canvas drawn again. If the app refuses any of them (the owner of an object does not take the
    /// change, a block is missing from the drawing) nothing is applied, <see cref="CommitChanges"/> throws with the reason and
    /// the queue is empty.
    /// </para>
    /// <para>
    /// An object inserted is not in the drawing until the commit, so <see cref="Insert"/> returns a ticket such as
    /// <c>~1</c> instead of its handle. A ticket stands for the object in <see cref="GetObject"/>, <see cref="Delete(string)"/>
    /// and <see cref="Select"/> once the commit is done, and <see cref="CommitChanges"/> returns the real handles.
    /// </para>
    /// </remarks>
    public sealed class CadRemoteDocument
    {
        private sealed class ReadObject
        {
            public CadRemoteDocument Document;
            public string Handle;
            public DxfDatabase Source;
        }

        private sealed class Pending
        {
            public string Kind;
            public string Handle;
            public string Reference;
            public string Layout;
            public string Ticket;
        }

        // What each object read is the copy of: the drawing it came from, its handle there and the small drawing it lives in.
        private static readonly ConditionalWeakTable<DxfEntity, ReadObject> Read =
            new ConditionalWeakTable<DxfEntity, ReadObject>();

        private readonly CadApplication _app;
        private readonly List<Pending> _queue = new List<Pending>();
        private readonly Dictionary<string, string> _tickets = new Dictionary<string, string>(StringComparer.Ordinal);
        private DxfDatabase _batch;
        private int _ticketCount;

        internal CadRemoteDocument(CadApplication app, int id)
        {
            _app = app;
            Id = id;
        }

        internal void Update(JsonObject entry)
        {
            Name = entry.GetString("name") ?? string.Empty;
            Path = entry.GetString("path") ?? string.Empty;
            IsActive = entry.GetBool("active");
            IsModified = entry.GetBool("modified");
        }

        /// <summary>Gets the id of the drawing, which holds as long as the app runs and is never given to another drawing.</summary>
        public int Id { get; }

        /// <summary>Gets the name of the drawing with its extension, as <c>A-101.dwg</c>, or <c>Drawing1.dwg</c> for one with no file yet; as the app last said.</summary>
        public string Name { get; private set; }

        /// <summary>Gets the full path of the drawing's file; empty for a drawing with no file yet; as the app last said.</summary>
        public string Path { get; private set; }

        /// <summary>Gets whether the drawing is the one the user works in; as the app last said (<see cref="CadRemoteDocuments"/> asks again each time it is walked).</summary>
        public bool IsActive { get; private set; }

        /// <summary>Gets whether the drawing has changes not saved; as the app last said.</summary>
        public bool IsModified { get; private set; }

        private JsonObject Ask(string op, Action<JsonObject> fill = null)
        {
            var request = new JsonObject().Set("op", op).Set("doc", (long)Id);
            fill?.Invoke(request);
            JsonObject answer = _app.Connection.Request(request);

            if (answer.Get("document") is JsonObject entry)
            {
                Update(entry);
            }

            return answer;
        }

        /// <summary>Shows the drawing in the app, as clicking its tab does.</summary>
        /// <exception cref="CadRemoteException">Thrown when the drawing has been closed.</exception>
        public void Activate() => Ask(RemoteOps.Activate);

        private string Resolve(string handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (handle.Length == 0 || handle[0] != '~')
            {
                return handle;
            }

            if (_tickets.TryGetValue(handle, out string real))
            {
                return real;
            }

            throw new CadRemoteException(
                "The object " + handle + " is not in the drawing yet: call CommitChanges first."
            );
        }

        /// <summary>Gets the handle an object read from this drawing has in the app's drawing.</summary>
        /// <param name="entity">An object returned by <see cref="GetObject"/>, <see cref="GetObjects"/> or <see cref="GetSelectedObjects"/> of this drawing.</param>
        /// <returns>The handle in the app's drawing, or null for an object this drawing did not give.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        public string HandleOf(DxfEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            return Read.TryGetValue(entity, out ReadObject read) && ReferenceEquals(read.Document, this)
                ? read.Handle
                : null;
        }

        private DxfDatabase Batch => _batch ?? (_batch = new DxfDatabase(RemoteFragment.Version));

        /// <summary>
        /// Queues an object to be added to the drawing. The entity is copied now, so changing it afterwards changes nothing;
        /// it can be one made in code (<c>new DxfLine(...)</c>), or one read from this or another drawing of the same app,
        /// which is copied with the layers, styles and blocks it needs. A layer the drawing has not is made. An INSERT of a
        /// block the drawing has not is refused at the commit, and a dimension made in code is drawn by the app, with the
        /// drawing's current dimension style unless it names one.
        /// </summary>
        /// <param name="entity">The object.</param>
        /// <param name="layout">The name of the layout to put it in; null for model space.</param>
        /// <returns>A ticket for the object, such as <c>~1</c>, which stands for its handle once <see cref="CommitChanges"/> is done.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the entity is in a drawing of the caller's own, not made in code or read from the app.</exception>
        public string Insert(DxfEntity entity, string layout = null)
        {
            DxfEntity copy = Copy(entity);
            string ticket = "~" + (++_ticketCount).ToString(CultureInfo.InvariantCulture);
            _queue.Add(
                new Pending
                {
                    Kind = "insert",
                    Reference = copy.Handle,
                    Layout = layout,
                    Ticket = ticket,
                }
            );
            return ticket;
        }

        /// <summary>
        /// Queues the changes made to an object read from this drawing, to be written over the object it was read from when
        /// <see cref="CommitChanges"/> is called, which keeps its handle. A dimension is drawn again by the app.
        /// </summary>
        /// <param name="entity">The object, as <see cref="GetObject"/> returned it and changed since.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the entity was not read from this drawing.</exception>
        public void Modify(DxfEntity entity)
        {
            string handle = HandleOf(entity ?? throw new ArgumentNullException(nameof(entity)));

            if (handle == null)
            {
                throw new ArgumentException(
                    "Modify takes an object read from this drawing with GetObject or GetObjects.",
                    nameof(entity)
                );
            }

            DxfEntity copy = Copy(entity);
            _queue.Add(
                new Pending
                {
                    Kind = "modify",
                    Handle = handle,
                    Reference = copy.Handle,
                }
            );
        }

        /// <summary>Queues the erasing of an object.</summary>
        /// <param name="handle">Its handle in the drawing, or the ticket <see cref="Insert"/> gave.</param>
        /// <exception cref="ArgumentNullException">Thrown when the handle is null.</exception>
        /// <exception cref="CadRemoteException">Thrown when the handle is the ticket of an object not committed yet.</exception>
        public void Delete(string handle) => _queue.Add(new Pending { Kind = "delete", Handle = Resolve(handle) });

        /// <summary>Queues the erasing of an object read from this drawing.</summary>
        /// <param name="entity">The object, as <see cref="GetObject"/> returned it.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the entity was not read from this drawing.</exception>
        public void Delete(DxfEntity entity)
        {
            string handle = HandleOf(entity ?? throw new ArgumentNullException(nameof(entity)));

            if (handle == null)
            {
                throw new ArgumentException(
                    "Delete takes an object read from this drawing, or a handle.",
                    nameof(entity)
                );
            }

            _queue.Add(new Pending { Kind = "delete", Handle = handle });
        }

        private DxfEntity Copy(DxfEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            DxfDatabase batch = Batch;

            if (Read.TryGetValue(entity, out ReadObject read))
            {
                return read.Source.WblockCloneObjects(new[] { entity }, batch.ModelSpace)[0];
            }

            if (entity.Handle != null)
            {
                throw new ArgumentException(
                    "The object is in a drawing of your own. Insert takes an object made in code, such as new DxfLine(...) or a Clone() of one, or one read from the app.",
                    nameof(entity)
                );
            }

            DxfEntity copy = entity.Clone();

            // The app draws a dimension's picture; the copy only needs a block of its own to be added with.
            if (copy is DxfDimension dimension && dimension.DimBlock == null)
            {
                var picture = new DxfBlockTableRecord { Name = "*D" };
                batch.BlockTable.Add(picture);
                dimension.DimBlock = picture;
            }

            // The block an INSERT places is the drawing's own; the copy only carries its name, in a block left empty.
            if (copy is DxfBlockReference insert && !batch.BlockTable.Has(insert.Name))
            {
                batch.BlockTable.Add(new DxfBlockTableRecord { Name = insert.Name });
            }

            batch.ModelSpace.AppendEntity(copy);
            return copy;
        }

        /// <summary>
        /// Sends the queued inserts, modifies and deletes and has the app apply them in order as one undo step, the canvas drawn
        /// again. Nothing queued is no request.
        /// </summary>
        /// <returns>The handles of the objects inserted, in the order of the <see cref="Insert"/> calls.</returns>
        /// <exception cref="CadRemoteException">
        /// Thrown when the drawing is closed, a command is running in it, the owner of an object refuses the change, an object
        /// to modify or delete is not in the drawing, or a block an INSERT places is not. Nothing is applied and the queue is
        /// emptied.
        /// </exception>
        public IReadOnlyList<string> CommitChanges()
        {
            if (_queue.Count == 0)
            {
                return Array.Empty<string>();
            }

            var operations = new JsonArray();

            foreach (Pending item in _queue)
            {
                var operation = new JsonObject().Set("op", item.Kind);

                if (item.Handle != null)
                {
                    operation.Set("handle", item.Handle);
                }

                if (item.Reference != null)
                {
                    operation.Set("ref", item.Reference);
                }

                if (item.Layout != null)
                {
                    operation.Set("layout", item.Layout);
                }

                operations.Add(operation);
            }

            string fragment = _batch == null ? string.Empty : _batch.ToText();
            List<Pending> sent = new List<Pending>(_queue);
            _queue.Clear();
            _batch = null;
            JsonObject answer = Ask(RemoteOps.Commit, request =>
                request.Set("fragment", fragment).Set("operations", operations)
            );
            var handles = new List<string>();
            JsonArray made = answer.GetArray("handles");
            int next = 0;

            foreach (Pending item in sent)
            {
                if (item.Kind == "insert")
                {
                    string real = next < made.Count ? made[next++] as string : null;
                    _tickets[item.Ticket] = real;

                    if (real != null)
                    {
                        handles.Add(real);
                    }
                }
            }

            return handles;
        }

        /// <summary>Reads an object by its handle.</summary>
        /// <param name="handle">The handle in the drawing, or the ticket of an object inserted and committed.</param>
        /// <returns>A copy of the object as it is now, or null when the drawing has no object of that handle.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the handle is null.</exception>
        /// <exception cref="CadRemoteException">Thrown when the drawing is closed or the handle is the ticket of an object not committed yet.</exception>
        public DxfEntity GetObject(string handle)
        {
            string real = Resolve(handle);
            JsonObject answer = Ask(RemoteOps.Get, request =>
                request.Set("handles", new JsonArray(new object[] { real }))
            );
            IReadOnlyList<DxfEntity> found = Objects(answer);
            return found.Count == 0 ? null : found[0];
        }

        /// <summary>Reads the objects that match a filter, in the order of the drawing: model space first, then each layout, each in the order its objects were made.</summary>
        /// <param name="filter">The filter; null for every object.</param>
        /// <returns>Copies of the objects as they are now.</returns>
        /// <exception cref="CadRemoteException">Thrown when the drawing is closed or the filter names a layout the drawing has not.</exception>
        public IReadOnlyList<DxfEntity> GetObjects(CadObjectFilter filter = null)
        {
            JsonObject answer = Ask(RemoteOps.Query, request =>
                request.Set("kind", filter?.Kind?.FullName).Set("layer", filter?.Layer).Set("layout", filter?.Layout)
            );
            return Objects(answer);
        }

        /// <summary>Reads the objects the user has selected in the drawing, in the order of their handles.</summary>
        /// <returns>Copies of the objects, none when nothing is selected.</returns>
        /// <exception cref="CadRemoteException">Thrown when the drawing is closed.</exception>
        public IReadOnlyList<DxfEntity> GetSelectedObjects() => Objects(Ask(RemoteOps.Selected));

        /// <summary>Selects objects in the drawing, as the user does, in place of what was selected.</summary>
        /// <param name="handles">The handles, or tickets of objects committed; none clears the selection.</param>
        /// <exception cref="ArgumentNullException">Thrown when the handles are null.</exception>
        /// <exception cref="CadRemoteException">Thrown when the drawing is closed.</exception>
        public void Select(IEnumerable<string> handles)
        {
            if (handles == null)
            {
                throw new ArgumentNullException(nameof(handles));
            }

            var list = new JsonArray();

            foreach (string handle in handles)
            {
                list.Add(Resolve(handle));
            }

            Ask(RemoteOps.Select, request => request.Set("handles", list));
        }

        private IReadOnlyList<DxfEntity> Objects(JsonObject answer)
        {
            string fragment = answer.GetString("fragment");
            JsonArray handles = answer.GetArray("handles");
            JsonArray references = answer.GetArray("refs");
            var objects = new List<DxfEntity>();

            if (fragment == null || handles.Count == 0)
            {
                return objects;
            }

            DxfDatabase source = DxfDatabase.Parse(fragment);
            var byReference = new Dictionary<string, DxfEntity>(StringComparer.Ordinal);

            foreach (DxfEntity entity in source.ModelSpace)
            {
                if (entity.Handle != null)
                {
                    byReference[entity.Handle] = entity;
                }
            }

            for (int i = 0; i < handles.Count && i < references.Count; i++)
            {
                if (
                    references[i] is string reference
                    && byReference.TryGetValue(reference, out DxfEntity entity)
                    && handles[i] is string handle
                )
                {
                    Read.Add(
                        entity,
                        new ReadObject
                        {
                            Document = this,
                            Handle = handle,
                            Source = source,
                        }
                    );
                    objects.Add(entity);
                }
            }

            return objects;
        }

        /// <summary>
        /// Types text on the drawing's command line, as the user would, and waits for the command to end. The drawing is
        /// shown first, since a command works on the drawing the user sees. Lines parted by a line feed are typed one after
        /// the other, and words in a line as if parted by Space, so <c>"ZOOM E "</c> zooms to the extents and <c>"LINE
        /// 0,0 100,0"</c> starts a line and gives two points. Waits up to 30 seconds.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>Whether the command ended and what the command line wrote.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        /// <exception cref="CadRemoteException">Thrown when the drawing is closed.</exception>
        public CadRemoteCommandResult SendCommand(string text) => SendCommand(text, TimeSpan.FromSeconds(30));

        /// <summary>
        /// Types text on the drawing's command line as <see cref="SendCommand(string)"/> does, and waits up to a time for the
        /// command to end. A command that asks the user for a point stays running after the time: the result says so, and
        /// the command goes on in the app.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="timeout">How long to wait for the command to end; zero does not wait.</param>
        /// <returns>Whether the command ended and what the command line wrote.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the time is negative.</exception>
        /// <exception cref="CadRemoteException">Thrown when the drawing is closed.</exception>
        public CadRemoteCommandResult SendCommand(string text, TimeSpan timeout)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (timeout < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "The time to wait cannot be negative.");
            }

            JsonObject answer = Ask(RemoteOps.Command, request =>
                request.Set("text", text).Set("timeout", (long)timeout.TotalMilliseconds)
            );
            var output = new List<string>();

            foreach (object line in answer.GetArray("output"))
            {
                output.Add(line as string ?? string.Empty);
            }

            return new CadRemoteCommandResult(answer.GetBool("completed"), output);
        }

        /// <summary>Saves the drawing to its file.</summary>
        /// <exception cref="CadRemoteException">Thrown when the drawing is closed, has no file yet (use <see cref="SaveAs"/>) or cannot be written.</exception>
        public void Save() => Ask(RemoteOps.Save);

        /// <summary>Saves the drawing to a file, which it is known by from then on.</summary>
        /// <param name="path">The path; a name ending in .dwg is written as a DWG, any other as a DXF.</param>
        /// <param name="version">The version of the file, as <c>AC1024</c> for AutoCAD 2010; null for the drawing's own.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="CadRemoteException">Thrown when the drawing is closed, or the file cannot be written or is open as another drawing.</exception>
        public void SaveAs(string path, string version = null)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            string full = System.IO.Path.GetFullPath(path);
            Ask(RemoteOps.SaveAs, request => request.Set("path", full).Set("version", version));
        }

        /// <inheritdoc/>
        public override string ToString() => Name + (IsActive ? " (active)" : string.Empty);
    }
}

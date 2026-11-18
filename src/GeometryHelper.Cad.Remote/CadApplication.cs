using System;
using System.Collections.Generic;
using System.IO;

namespace GeometryHelper.Cad.Remote
{
    /// <summary>
    /// A running GeometryHelper CAD, driven from another program the way a Tekla Open API program drives a running Tekla
    /// Structures: <c>new CadApplication()</c> connects, <see cref="Documents"/> lists the drawings it has open and each
    /// <see cref="CadRemoteDocument"/> reads and edits one of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The program talks to the app over a named pipe that only the Windows user who runs the app can open, and the
    /// entities travel as DXF text, so what is read and written are the library's own <c>DxfLine</c>, <c>DxfMText</c>,
    /// <c>DxfDimension</c> and the rest. Nothing else is installed on the app's side: it is on by default in the app.
    /// </para>
    /// <para>
    /// One request is run at a time in an app. A second program that asks while the first is being served waits for it.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// using (var app = new CadApplication())
    /// {
    ///     CadRemoteDocument drawing = app.ActiveDocument;
    ///     drawing.Insert(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)) { Layer = "Steel" });
    ///     drawing.CommitChanges();
    ///     drawing.SendCommand("ZOOM E ");
    /// }
    /// </code>
    /// </example>
    public sealed class CadApplication : IDisposable
    {
        private readonly RemoteConnection _connection;
        private readonly Dictionary<int, CadRemoteDocument> _documents = new Dictionary<int, CadRemoteDocument>();

        /// <summary>Connects to the app the user activated last.</summary>
        /// <exception cref="CadRemoteException">Thrown when no GeometryHelper CAD is running or it does not take the connection.</exception>
        public CadApplication()
            : this(RemoteInstance.DefaultFolder()) { }

        /// <summary>Connects to one of the apps <see cref="GetRunning()"/> listed.</summary>
        /// <param name="app">The app.</param>
        /// <exception cref="ArgumentNullException">Thrown when the app is null.</exception>
        /// <exception cref="CadRemoteException">Thrown when the app has closed or does not take the connection.</exception>
        public CadApplication(CadRunningApp app)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            Running = app;
            _connection = new RemoteConnection(app.Instance);
            _connection.Request(new JsonObject().Set("op", RemoteOps.Hello));
            Documents = new CadRemoteDocuments(this);
        }

        /// <summary>Connects to the app that announced itself last in a folder: the apps of a test, or of a second Windows profile.</summary>
        internal CadApplication(string folder)
            : this(Pick(folder)) { }

        private static CadRunningApp Pick(string folder)
        {
            List<RemoteInstance> found = RemoteInstance.ReadAll(folder);

            if (found.Count == 0)
            {
                throw new CadRemoteException(
                    "No GeometryHelper CAD is running. Start the app, or check that it allows other programs to connect."
                );
            }

            return new CadRunningApp(found[0], Array.Empty<string>(), folder);
        }

        /// <summary>Gets the app this one is connected to.</summary>
        public CadRunningApp Running { get; }

        /// <summary>Gets the drawings the app has open.</summary>
        public CadRemoteDocuments Documents { get; }

        internal RemoteConnection Connection => _connection;

        /// <summary>
        /// Lists the apps that are running, the one activated last first: each with its process id, its window's title and the
        /// drawings it has open. An app that has gone leaves no trace; an app busy with a long command is listed with no
        /// drawings.
        /// </summary>
        /// <returns>The running apps, none when none is running.</returns>
        public static IReadOnlyList<CadRunningApp> GetRunning() => GetRunning(RemoteInstance.DefaultFolder());

        internal static IReadOnlyList<CadRunningApp> GetRunning(string folder)
        {
            var apps = new List<CadRunningApp>();

            foreach (RemoteInstance instance in RemoteInstance.ReadAll(folder))
            {
                var names = new List<string>();

                using (var connection = new RemoteConnection(instance) { ConnectTimeout = 2000 })
                {
                    try
                    {
                        foreach (
                            object item in connection
                                .Request(new JsonObject().Set("op", RemoteOps.Documents))
                                .GetArray("documents")
                        )
                        {
                            var entry = (JsonObject)item;
                            string path = entry.GetString("path");
                            names.Add(string.IsNullOrEmpty(path) ? entry.GetString("name") : path);
                        }
                    }
                    catch (CadRemoteException ex)
                    {
                        GeometryHelperLog.Debug(
                            "Remote: process " + instance.ProcessId + " was not asked: " + ex.Message
                        );
                    }
                }

                apps.Add(new CadRunningApp(instance, names, folder));
            }

            return apps;
        }

        /// <summary>Connects to the app that has a file open.</summary>
        /// <param name="path">The path of the drawing's file.</param>
        /// <returns>The connection to the first app, the one activated last first, that has the file open.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="CadRemoteException">Thrown when no running app has the file open.</exception>
        public static CadApplication ForDocument(string path) => ForDocument(path, RemoteInstance.DefaultFolder());

        internal static CadApplication ForDocument(string path, string folder)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            string full = Path.GetFullPath(path);

            foreach (CadRunningApp app in GetRunning(folder))
            {
                foreach (string drawing in app.Drawings)
                {
                    if (string.Equals(drawing, full, StringComparison.OrdinalIgnoreCase))
                    {
                        return new CadApplication(app);
                    }
                }
            }

            throw new CadRemoteException("No running GeometryHelper CAD has " + full + " open.");
        }

        /// <summary>Gets the drawing the user works in, or null when the app has none open.</summary>
        public CadRemoteDocument ActiveDocument
        {
            get
            {
                foreach (CadRemoteDocument document in Documents)
                {
                    if (document.IsActive)
                    {
                        return document;
                    }
                }

                return null;
            }
        }

        /// <summary>Opens a drawing from a file and shows it, or, where the app has the file open already, returns that drawing and shows it.</summary>
        /// <param name="path">The path of a DXF or DWG file.</param>
        /// <returns>The drawing.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="CadRemoteException">Thrown when the file cannot be opened.</exception>
        public CadRemoteDocument Open(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            JsonObject answer = _connection.Request(
                new JsonObject().Set("op", RemoteOps.Open).Set("path", Path.GetFullPath(path))
            );
            return Known((JsonObject)answer.Get("document"));
        }

        /// <summary>Starts a new drawing and shows it.</summary>
        /// <param name="template">The path of a template to start from; null for the app's own template.</param>
        /// <returns>The drawing.</returns>
        /// <exception cref="CadRemoteException">Thrown when the template cannot be read.</exception>
        public CadRemoteDocument New(string template = null)
        {
            JsonObject answer = _connection.Request(
                new JsonObject()
                    .Set("op", RemoteOps.New)
                    .Set("template", template == null ? null : Path.GetFullPath(template))
            );
            return Known((JsonObject)answer.Get("document"));
        }

        /// <summary>The drawing of an entry of the app's answer: the one this connection has made already, its facts brought up to date.</summary>
        internal CadRemoteDocument Known(JsonObject entry)
        {
            int id = (int)entry.GetLong("id");

            if (!_documents.TryGetValue(id, out CadRemoteDocument document))
            {
                document = new CadRemoteDocument(this, id);
                _documents[id] = document;
            }

            document.Update(entry);
            return document;
        }

        /// <summary>Closes the connection; the app keeps running and its drawings open.</summary>
        public void Dispose() => _connection.Dispose();
    }
}

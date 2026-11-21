using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using GeometryHelper.Cad.Remote;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The side of the remote channel that lives in the app: it listens on a named pipe for the programs that drive the
    /// app (<see cref="CadApplication"/> of GeometryHelper.Cad.Remote), announces itself in a file so that they find it, and
    /// answers each request on the thread of the drawings through the dispatcher it is given. It has no UI and is started
    /// by the app's host; where nothing starts it, nothing listens.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pipe is <c>GeometryHelper.Cad.&lt;process id&gt;</c>, open to the current Windows user alone (see
    /// <see cref="CadRemotePipe"/>). The file <c>%LocalAppData%\GeometryHelper\Remote\&lt;process id&gt;.json</c> holds the
    /// process id, the window's title and the time the app was last activated; it is deleted when the server stops, and a
    /// file left by a process that died is ignored and removed by the next client that finds it.
    /// </para>
    /// <para>
    /// Messages are a length and UTF-8 JSON. The entities of the drawings travel as the DXF text of the library, so the
    /// client has the library's own types. A request is run on the dispatcher's thread, one at a time for the whole app: a
    /// second client's request waits for the first to finish.
    /// </para>
    /// </remarks>
    public sealed partial class CadRemoteServer : IDisposable
    {
        private readonly ICadRemoteHost _host;
        private readonly ICadRemoteDispatcher _dispatcher;
        private readonly string _folder;
        private readonly string _instance;
        private readonly object _requests = new object();
        private readonly object _gate = new object();
        private readonly List<Stream> _connections = new List<Stream>();
        private Thread _listener;
        private RemoteInstance _announced;
        private volatile bool _stopping;

        /// <summary>Makes a server for an app; nothing listens until <see cref="Start"/>.</summary>
        /// <param name="host">The app's drawings and their commands.</param>
        /// <param name="dispatcher">Runs work on the thread the drawings belong to.</param>
        /// <exception cref="ArgumentNullException">Thrown when the host or the dispatcher is null.</exception>
        public CadRemoteServer(ICadRemoteHost host, ICadRemoteDispatcher dispatcher)
            : this(host, dispatcher, RemoteInstance.DefaultFolder(), null) { }

        /// <summary>
        /// Makes a server that announces itself in a folder of its own, and under an instance name when a process runs more
        /// than one server (<c>GeometryHelper.Cad.&lt;process id&gt;.&lt;instance&gt;</c>, <c>&lt;process id&gt;.&lt;instance&gt;.json</c>): how the tests keep to
        /// themselves while the real app is open.
        /// </summary>
        internal CadRemoteServer(ICadRemoteHost host, ICadRemoteDispatcher dispatcher, string folder, string instance)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _folder = folder ?? throw new ArgumentNullException(nameof(folder));
            _instance = instance;
            int process = System.Diagnostics.Process.GetCurrentProcess().Id;
            PipeName = RemoteInstance.PipeName(process, instance);
            _announced = new RemoteInstance
            {
                ProcessId = process,
                Pipe = PipeName,
                FilePath = Path.Combine(_folder, RemoteInstance.FileName(process, instance)),
            };
        }

        /// <summary>Gets the name of the pipe the server listens on.</summary>
        public string PipeName { get; }

        /// <summary>Gets whether the server is listening.</summary>
        public bool IsRunning => _listener != null && !_stopping;

        /// <summary>
        /// Opens the pipe, writes the instance file and starts listening. Call it on the thread of the drawings: it reads the
        /// window's title.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the server is running already or was stopped.</exception>
        /// <exception cref="IOException">Thrown when the pipe cannot be made, or the instance file cannot be written.</exception>
        public void Start()
        {
            lock (_gate)
            {
                if (_listener != null || _stopping)
                {
                    throw new InvalidOperationException("The remote server has been started already.");
                }

                NamedPipeServerStream first = CadRemotePipe.Create(PipeName, true);
                _listener = new Thread(() => Listen(first))
                {
                    IsBackground = true,
                    Name = "CadRemoteServer " + PipeName,
                };
                _listener.Start();

                // A client of this process on the thread the dispatcher owns would wait for itself: it is refused (RemoteSelf).
                if (_dispatcher is ICadRemoteOwnerThread owner)
                {
                    RemoteSelf.Register(PipeName, owner.IsOwnerThread);
                }
            }

            try
            {
                Announce();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Stop();
                throw new IOException(
                    "The instance file " + _announced.FilePath + " could not be written: " + ex.Message,
                    ex
                );
            }

            GeometryHelperLog.Info("CadRemoteServer: listening on " + PipeName + ".");
        }

        /// <summary>
        /// Writes the instance file again with the window's title and the time now as the time the app was last activated.
        /// The host calls it when the user activates the window, on the thread of the drawings.
        /// </summary>
        public void NoteActivated()
        {
            try
            {
                Announce();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                GeometryHelperLog.Warn("CadRemoteServer: the instance file could not be written.", ex);
            }
        }

        private void Announce()
        {
            _announced.Title = _host.Title;
            _announced.LastActivated = DateTime.UtcNow;
            _announced.Write();
        }

        /// <summary>Stops listening, closes the connections and deletes the instance file. Calling it again does nothing.</summary>
        public void Stop()
        {
            Thread listener;
            List<Stream> open;

            lock (_gate)
            {
                if (_stopping)
                {
                    return;
                }

                _stopping = true;
                listener = _listener;
                RemoteSelf.Unregister(PipeName);
                open = new List<Stream>(_connections);
            }

            if (listener != null)
            {
                // The listener waits in a blocking call for the next client: one that connects and leaves wakes it.
                try
                {
                    using (var wake = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
                    {
                        wake.Connect(1000);
                    }
                }
                catch (Exception ex)
                    when (ex is TimeoutException || ex is IOException || ex is UnauthorizedAccessException)
                {
                    GeometryHelperLog.Debug("CadRemoteServer: the listener was not woken: " + ex.Message);
                }

                listener.Join(2000);
            }

            foreach (Stream stream in open)
            {
                try
                {
                    stream.Dispose();
                }
                catch (IOException) { }
            }

            _announced.Delete();
            GeometryHelperLog.Info("CadRemoteServer: stopped " + PipeName + ".");
        }

        /// <summary>Stops the server.</summary>
        public void Dispose() => Stop();

        private void Listen(NamedPipeServerStream pipe)
        {
            try
            {
                Accept(pipe);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                // A thread of the pipe must never take the app down with it.
                GeometryHelperLog.Warn("CadRemoteServer: listening stopped.", ex);
            }
        }

        private void Accept(NamedPipeServerStream pipe)
        {
            while (!_stopping)
            {
                try
                {
                    pipe.WaitForConnection();
                }
                catch (IOException ex)
                {
                    GeometryHelperLog.Debug("CadRemoteServer: a client left before it was taken: " + ex.Message);
                    pipe.Dispose();
                    pipe = Next();

                    if (pipe == null)
                    {
                        return;
                    }

                    continue;
                }

                if (_stopping)
                {
                    pipe.Dispose();
                    return;
                }

                NamedPipeServerStream connected = pipe;

                lock (_gate)
                {
                    _connections.Add(connected);
                }

                new Thread(() => Serve(connected)) { IsBackground = true, Name = "CadRemoteServer client" }.Start();
                pipe = Next();

                if (pipe == null)
                {
                    return;
                }
            }

            pipe.Dispose();
        }

        private NamedPipeServerStream Next()
        {
            try
            {
                return CadRemotePipe.Create(PipeName, false);
            }
            catch (IOException ex)
            {
                GeometryHelperLog.Warn(
                    "CadRemoteServer: no pipe could be made for the next client; listening stops.",
                    ex
                );
                return null;
            }
        }

        private void Serve(NamedPipeServerStream pipe)
        {
            try
            {
                while (!_stopping)
                {
                    JsonObject request = RemoteFrames.Read(pipe);

                    if (request == null)
                    {
                        break;
                    }

                    JsonObject answer = Answer(request);

                    try
                    {
                        RemoteFrames.Write(pipe, answer);
                    }
                    catch (Exception ex) when (ex is CadRemoteException || ex is ArgumentException)
                    {
                        // An answer too long for the channel, or holding a number it cannot write: told as a refusal instead.
                        RemoteFrames.Write(pipe, Failure(request.GetLong("id"), ex.Message));
                    }
                }
            }
            catch (Exception ex)
                when (ex is IOException || ex is ObjectDisposedException || ex is InvalidOperationException)
            {
                GeometryHelperLog.Debug("CadRemoteServer: a client's connection ended: " + ex.Message);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                // A thread of the pipe must never take the app down with it.
                GeometryHelperLog.Warn("CadRemoteServer: a client's connection failed.", ex);
            }
            finally
            {
                lock (_gate)
                {
                    _connections.Remove(pipe);
                }

                try
                {
                    pipe.Dispose();
                }
                catch (IOException) { }
            }
        }
    }
}

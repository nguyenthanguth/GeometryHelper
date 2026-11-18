using System;
using System.IO;
using System.Windows.Threading;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// Keeps the app's remote channel as Global settings' "Allow other programs to connect" says: a <see cref="CadRemoteServer"/>
    /// started on the window's thread while the setting is on, stopped (its pipe closed and its file deleted) when it is off,
    /// and a new one started when it is on again, since a stopped server cannot start twice. It also keeps the line Global
    /// settings shows about the channel (<see cref="MainViewModel.RemoteStatus"/>).
    /// </summary>
    internal sealed class CadRemoteSwitch : IDisposable
    {
        private readonly MainViewModel _model;
        private readonly ICadRemoteHost _host;
        private readonly ICadRemoteDispatcher _dispatcher;
        private readonly string _folder;
        private readonly string _instance;
        private CadRemoteServer _server;

        /// <summary>Makes the switch for the app's window; nothing runs until <see cref="Apply"/>.</summary>
        public CadRemoteSwitch(MainViewModel model, Func<string> title, Dispatcher dispatcher)
            : this(model, new CadAppRemoteHost(model, title, dispatcher)) { }

        private CadRemoteSwitch(MainViewModel model, CadAppRemoteHost host)
            : this(model, host, host, null, null) { }

        /// <summary>Makes the switch over a host, the server announcing itself in a folder of its own: how the tests keep to themselves.</summary>
        internal CadRemoteSwitch(
            MainViewModel model,
            ICadRemoteHost host,
            ICadRemoteDispatcher dispatcher,
            string folder,
            string instance
        )
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _folder = folder;
            _instance = instance;
            model.RemoteStatus = "The remote channel has not been started.";
        }

        /// <summary>Gets the server now running; null while the setting is off or the pipe could not be opened.</summary>
        public CadRemoteServer Server => _server;

        /// <summary>
        /// Brings the server in line with the setting: starts one where it is on and none runs, stops the one running where it
        /// is off. A server that could not start is logged and said in the status line; the app goes on without it.
        /// </summary>
        public void Apply()
        {
            bool allowed = _model.Settings.AllowRemoteConnections;

            if (!allowed)
            {
                Stop();
                _model.RemoteStatus = "Other programs cannot connect.";
                return;
            }

            if (_server != null)
            {
                return;
            }

            var server =
                _folder == null
                    ? new CadRemoteServer(_host, _dispatcher)
                    : new CadRemoteServer(_host, _dispatcher, _folder, _instance);

            try
            {
                server.Start();
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException)
            {
                GeometryHelperLog.Warn(
                    "Program: other programs cannot connect to the app; the remote server did not start.",
                    ex
                );
                _model.RemoteStatus =
                    "Other programs cannot connect: the pipe could not be opened (" + ex.Message + ").";
                return;
            }

            _server = server;
            _model.RemoteStatus = "Other programs can connect: " + server.PipeName;
        }

        /// <summary>Has the running server write its file again with the time now as the time the window was last activated.</summary>
        public void NoteActivated() => _server?.NoteActivated();

        /// <summary>Stops the server if one runs.</summary>
        public void Stop()
        {
            CadRemoteServer server = _server;
            _server = null;
            server?.Stop();
        }

        /// <summary>Stops the server.</summary>
        public void Dispose() => Stop();
    }
}

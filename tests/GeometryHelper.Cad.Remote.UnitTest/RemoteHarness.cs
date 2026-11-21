using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Cad.Remote;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Remote.UnitTest
{
    /// <summary>
    /// Runs the work on the thread that calls it, one piece at a time: a headless app has no window thread to hand work to, and
    /// the gate stands for the window's thread, which a test takes to touch a drawing while the server is waiting on the user.
    /// </summary>
    internal sealed class InlineDispatcher : ICadRemoteDispatcher
    {
        private readonly object _gate;

        public InlineDispatcher()
            : this(new object()) { }

        public InlineDispatcher(object gate)
        {
            _gate = gate;
        }

        public void Invoke(Action action)
        {
            lock (_gate)
            {
                action();
            }
        }
    }

    /// <summary>
    /// An app with no window: a few documents, the one that is active, and a command session for each, as the window's view
    /// model would keep them.
    /// </summary>
    internal sealed class TestHost : ICadRemoteHost
    {
        private readonly List<CadDocument> _documents = new List<CadDocument>();
        private readonly Dictionary<CadDocument, CadCommandSession> _sessions =
            new Dictionary<CadDocument, CadCommandSession>();
        private int _newCount;

        public string Title { get; set; } = "GeometryHelper CAD";

        /// <summary>Gets the gate the server's work is run under: what a test locks to act as the window's thread.</summary>
        public object Gate { get; } = new object();

        /// <summary>Runs an action as the window's thread would, between two pieces of the server's work.</summary>
        public void OnAppThread(Action action)
        {
            lock (Gate)
            {
                action();
            }
        }

        /// <summary>Runs a function as the window's thread would, between two pieces of the server's work.</summary>
        public T OnAppThread<T>(Func<T> function)
        {
            lock (Gate)
            {
                return function();
            }
        }

        public IReadOnlyList<CadDocument> Documents => _documents;

        public CadDocument ActiveDocument { get; private set; }

        /// <summary>Puts a document in the app and shows it, as opening it does.</summary>
        public CadDocument Add(CadDocument document)
        {
            _documents.Add(document);
            _sessions[document] = new CadCommandSession(document) { PickAperture = 1.0 };
            ActiveDocument = document;
            return document;
        }

        /// <summary>Takes a document out, as the user closing it does.</summary>
        public void Close(CadDocument document)
        {
            _documents.Remove(document);
            _sessions.Remove(document);

            if (ReferenceEquals(ActiveDocument, document))
            {
                ActiveDocument = _documents.LastOrDefault();
            }
        }

        public CadDocument Open(string path)
        {
            CadDocument open = _documents.FirstOrDefault(d =>
                d.HasFile && string.Equals(d.FileName, path, StringComparison.OrdinalIgnoreCase)
            );

            if (open != null)
            {
                ActiveDocument = open;
                return open;
            }

            try
            {
                return Add(CadDocument.Open(path));
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException("Cannot open " + path + ": " + ex.Message);
            }
        }

        public CadDocument New(string template) =>
            Add(CadDocument.From(new DxfDatabase("AC1027"), "Drawing" + (++_newCount) + ".dxf"));

        public void Activate(CadDocument document) => ActiveDocument = document;

        public CadCommandSession SessionOf(CadDocument document) => _sessions[document];

        public void Type(CadDocument document, string line)
        {
            CadCommandSession session = _sessions[document];

            if (line.Trim().Length == 0)
            {
                session.Enter();
                return;
            }

            foreach (string word in line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                session.Feed(word);
            }
        }

        public void Save(CadDocument document) => document.Save();

        public void SaveAs(CadDocument document, string path, string version) => document.SaveAs(path, version, null);
    }

    /// <summary>The folder, the servers and the clients of one test, all of them its own so that a test runs while the real app is open.</summary>
    internal sealed class Harness : IDisposable
    {
        private readonly List<CadRemoteServer> _servers = new List<CadRemoteServer>();
        private readonly List<CadApplication> _clients = new List<CadApplication>();

        public Harness()
        {
            Folder = Path.Combine(Path.GetTempPath(), "GeometryHelper.RemoteTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Folder);
        }

        /// <summary>Gets the folder the servers announce themselves in and the test's own files go to.</summary>
        public string Folder { get; }

        /// <summary>Starts a server for a host under an instance name of its own.</summary>
        public CadRemoteServer Serve(TestHost host) => Serve(host, new InlineDispatcher(host.Gate));

        /// <summary>Starts a server for a host under an instance name of its own, its work run by a dispatcher of the test's.</summary>
        public CadRemoteServer Serve(TestHost host, ICadRemoteDispatcher dispatcher)
        {
            var server = new CadRemoteServer(host, dispatcher, Folder, Guid.NewGuid().ToString("N").Substring(0, 8));
            server.Start();
            _servers.Add(server);
            return server;
        }

        /// <summary>Connects to the app activated last.</summary>
        public CadApplication Connect()
        {
            var app = new CadApplication(Folder);
            _clients.Add(app);
            return app;
        }

        public CadApplication Connect(CadRunningApp running)
        {
            var app = new CadApplication(running);
            _clients.Add(app);
            return app;
        }

        public void Dispose()
        {
            foreach (CadApplication client in _clients)
            {
                client.Dispose();
            }

            foreach (CadRemoteServer server in _servers)
            {
                server.Dispose();
            }

            try
            {
                Directory.Delete(Folder, true);
            }
            catch (IOException) { }
        }
    }
}

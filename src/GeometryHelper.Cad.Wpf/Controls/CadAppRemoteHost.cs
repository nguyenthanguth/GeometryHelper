using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The app's drawings for the remote channel: a <see cref="CadRemoteServer"/> reads the window's view model through this,
    /// and runs its work on the window's thread through <see cref="Dispatcher"/>.
    /// </summary>
    internal sealed class CadAppRemoteHost : ICadRemoteHost, ICadRemoteDispatcher, ICadRemoteOwnerThread
    {
        private readonly MainViewModel _model;
        private readonly Func<string> _title;
        private readonly Dispatcher _dispatcher;

        public CadAppRemoteHost(MainViewModel model, Func<string> title, Dispatcher dispatcher)
        {
            _model = model;
            _title = title;
            _dispatcher = dispatcher;
        }

        public string Title => _title();

        public IReadOnlyList<CadDocument> Documents => _model.RemoteDocuments();

        public CadDocument ActiveDocument => _model.Document;

        public CadDocument Open(string path) => _model.OpenForRemote(path);

        public CadDocument New(string template) => _model.NewForRemote(template);

        public void Activate(CadDocument document) => _model.ShowDocument(document);

        public CadCommandSession SessionOf(CadDocument document) => _model.SessionOfDocument(document);

        public void Type(CadDocument document, string line)
        {
            _model.ShowDocument(document);
            _model.TypeForRemote(line);
        }

        public void Save(CadDocument document) => _model.SaveForRemote(document);

        public void SaveAs(CadDocument document, string path, string version) =>
            _model.SaveAsForRemote(document, path, version);

        public void Close(CadDocument document) => _model.CloseForRemote(document);

        public void Invoke(Action action) => _dispatcher.Invoke(action, DispatcherPriority.Normal);

        public bool IsOwnerThread() => _dispatcher.CheckAccess();
    }
}

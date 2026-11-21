using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Remote.TestServer
{
    /// <summary>
    /// Runs the work on the thread that calls it, one piece at a time, under a gate: the server has no window thread to hand
    /// work to, and the gate stands for it, so that what the console does to a drawing waits for the piece the server is in.
    /// </summary>
    internal sealed class InlineDispatcher : ICadRemoteDispatcher
    {
        private readonly object _gate;

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
    /// An app with no window and no drawing to begin with: the drawings that are made or opened, the one that is active, and
    /// a command session for each, as the window's view model would keep them.
    /// </summary>
    internal sealed class TestHost : ICadRemoteHost
    {
        private readonly List<CadDocument> _documents = new List<CadDocument>();
        private readonly Dictionary<CadDocument, CadCommandSession> _sessions =
            new Dictionary<CadDocument, CadCommandSession>();
        private int _newCount;

        public string Title => "GeometryHelper CAD (test server)";

        public IReadOnlyList<CadDocument> Documents => _documents;

        public CadDocument ActiveDocument { get; private set; }

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

        /// <summary>Makes <c>Drawing&lt;n&gt;.dxf</c> from an empty database; the template is ignored.</summary>
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

        private CadDocument Add(CadDocument document)
        {
            _documents.Add(document);
            _sessions[document] = new CadCommandSession(document) { PickAperture = 1.0 };
            ActiveDocument = document;
            return document;
        }
    }
}

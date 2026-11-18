using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace GeometryHelper.Cad.Remote
{
    /// <summary>
    /// The drawings a running app has open, in the order of their ids (a drawing the app has not listed before gets the next id; drawings listed together get theirs by name, so two runs agree). The list is asked of the app each time it is
    /// walked, so that a drawing the user opened a moment ago is in it.
    /// </summary>
    public sealed class CadRemoteDocuments : IEnumerable<CadRemoteDocument>
    {
        private readonly CadApplication _app;

        internal CadRemoteDocuments(CadApplication app)
        {
            _app = app;
        }

        private List<CadRemoteDocument> Read()
        {
            var documents = new List<CadRemoteDocument>();

            foreach (
                object item in _app
                    .Connection.Request(new JsonObject().Set("op", RemoteOps.Documents))
                    .GetArray("documents")
            )
            {
                documents.Add(_app.Known((JsonObject)item));
            }

            return documents;
        }

        /// <summary>Gets the number of drawings open.</summary>
        public int Count => Read().Count;

        /// <summary>
        /// Finds a drawing by its full path, else by its name with the extension (<c>A-101.dwg</c>), compared without regard
        /// to case.
        /// </summary>
        /// <param name="pathOrName">The path or the name.</param>
        /// <returns>The drawing, or null when none is open by that path or name.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path or name is null.</exception>
        public CadRemoteDocument Get(string pathOrName)
        {
            if (pathOrName == null)
            {
                throw new ArgumentNullException(nameof(pathOrName));
            }

            List<CadRemoteDocument> documents = Read();
            string full = FullPath(pathOrName);

            foreach (CadRemoteDocument document in documents)
            {
                if (full != null && string.Equals(document.Path, full, StringComparison.OrdinalIgnoreCase))
                {
                    return document;
                }
            }

            foreach (CadRemoteDocument document in documents)
            {
                if (string.Equals(document.Name, pathOrName, StringComparison.OrdinalIgnoreCase))
                {
                    return document;
                }
            }

            return null;
        }

        private static string FullPath(string path)
        {
            try
            {
                return System.IO.Path.IsPathRooted(path) ? System.IO.Path.GetFullPath(path) : null;
            }
            catch (Exception ex)
                when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return null;
            }
        }

        /// <summary>Walks the drawings open now.</summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<CadRemoteDocument> GetEnumerator() => Read().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

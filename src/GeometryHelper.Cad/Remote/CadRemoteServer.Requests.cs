using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using GeometryHelper.Cad.Remote;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>The requests the server answers.</summary>
    public sealed partial class CadRemoteServer
    {
        /// <summary>How long the server waits between looks at a command it is waiting to end, in milliseconds.</summary>
        private const int CommandPoll = 20;

        /// <summary>How long a request waits for the one before it before it looks whether that one is a pick, in milliseconds.</summary>
        private const int PickLook = 50;

        // A request refused for a reason the client is told in words: not a fault of the app, so not logged as one.
        private sealed class Refusal : Exception
        {
            public Refusal(string message, bool cancelled = false)
                : base(message)
            {
                Cancelled = cancelled;
            }

            // The user ended a pick: the client is told so, apart from a failure.
            public bool Cancelled { get; }
        }

        // Set while a request waits for the user to pick: any other request is refused as busy until the pick ends.
        private volatile bool _picking;

        private sealed class Known
        {
            public CadDocument Document;
            public string Name;
        }

        // The drawings the server has given an id: touched only on the dispatcher's thread.
        private readonly Dictionary<CadDocument, int> _ids = new Dictionary<CadDocument, int>();
        private readonly Dictionary<int, Known> _known = new Dictionary<int, Known>();
        private int _lastId;

        private JsonObject Answer(JsonObject request)
        {
            long id = request.GetLong("id");
            string op = request.GetString("op");

            try
            {
                JsonObject answer;

                // One request at a time for the whole app: a second client waits here for the first, unless the first waits
                // for the user to pick, which can take as long as the user likes: it is told the app is busy instead.
                while (!Monitor.TryEnter(_requests, PickLook))
                {
                    if (_picking)
                    {
                        throw new Refusal(
                            "GeometryHelper CAD is busy: it is waiting for the user to pick in a drawing, for a program that asked it to. Try again when the pick is done."
                        );
                    }

                    if (_stopping)
                    {
                        throw new Refusal("GeometryHelper CAD is shutting down.");
                    }
                }

                try
                {
                    answer =
                        op == RemoteOps.Command ? RunCommand(request)
                        : op == RemoteOps.Pick ? RunPick(request)
                        : Dispatch(() => Execute(op, request));
                }
                finally
                {
                    Monitor.Exit(_requests);
                }

                return answer.Set("id", id).Set("ok", true);
            }
            catch (Refusal refusal)
            {
                return Failure(id, refusal.Message, refusal.Cancelled);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                GeometryHelperLog.Warn("CadRemoteServer: the request " + op + " failed.", ex);
                return Failure(id, "GeometryHelper CAD could not do the request " + op + ": " + ex.Message);
            }
        }

        private static JsonObject Failure(long id, string message, bool cancelled = false)
        {
            JsonObject failure = new JsonObject().Set("id", id).Set("ok", false).Set("error", message);
            return cancelled ? failure.Set("cancelled", true) : failure;
        }

        /// <summary>Runs a function on the dispatcher's thread, an exception it throws thrown again on this one.</summary>
        private T Dispatch<T>(Func<T> work)
        {
            T result = default(T);
            Exception failure = null;
            bool ran = false;
            _dispatcher.Invoke(() =>
            {
                try
                {
                    ran = true;
                    result = work();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            if (!ran)
            {
                throw new Refusal("GeometryHelper CAD is shutting down.");
            }

            if (failure != null)
            {
                if (failure is Refusal)
                {
                    throw new Refusal(failure.Message);
                }

                throw new InvalidOperationException(failure.Message, failure);
            }

            return result;
        }

        private JsonObject Execute(string op, JsonObject request)
        {
            switch (op)
            {
                case RemoteOps.Hello:
                    return new JsonObject()
                        .Set("protocol", (long)RemoteOps.Version)
                        .Set("processId", (long)System.Diagnostics.Process.GetCurrentProcess().Id)
                        .Set("title", _host.Title ?? string.Empty);
                case RemoteOps.Documents:
                    return new JsonObject().Set("documents", Documents());
                case RemoteOps.Open:
                    return Shown(Opened(request.GetString("path")));
                case RemoteOps.New:
                    return Shown(Made(request.GetString("template")));
                case RemoteOps.Activate:
                    CadDocument toShow = Find(request);
                    _host.Activate(toShow);
                    return Shown(toShow);
                case RemoteOps.Get:
                    return Get(Find(request), request.GetArray("handles"));
                case RemoteOps.Query:
                    return Query(Find(request), request);
                case RemoteOps.Selected:
                    return Selected(Find(request));
                case RemoteOps.Select:
                    return Select(Find(request), request.GetArray("handles"));
                case RemoteOps.Commit:
                    return Commit(Find(request), request);
                case RemoteOps.Save:
                    return Save(Find(request));
                case RemoteOps.SaveAs:
                    return SaveAs(Find(request), request);
                case RemoteOps.Layers:
                    return Layers(Find(request));
                case RemoteOps.CurrentLayer:
                    return CurrentLayer(Find(request), request.GetString("name"));
                case RemoteOps.Layouts:
                    return Layouts(Find(request));
                case RemoteOps.CurrentLayout:
                    return CurrentLayout(Find(request), request.GetString("name"));
                default:
                    throw new Refusal(
                        "GeometryHelper CAD does not know the request " + op + ": it is of an older version."
                    );
            }
        }

        // ---- The drawings and their ids ------------------------------------------------------------------------------

        /// <summary>The id of a drawing: given the first time it is seen, in the order of the host's list for drawings seen together.</summary>
        private int IdOf(CadDocument document)
        {
            if (!_ids.TryGetValue(document, out int id))
            {
                id = ++_lastId;
                _ids[document] = id;
                _known[id] = new Known { Document = document };
            }

            _known[id].Name = document.Title;
            return id;
        }

        private JsonObject Info(CadDocument document)
        {
            CadDocument active = _host.ActiveDocument;
            return new JsonObject()
                .Set("id", (long)IdOf(document))
                .Set("name", document.Title)
                .Set(
                    "path",
                    document.HasFile && Path.IsPathRooted(document.FileName) ? document.FileName : string.Empty
                )
                .Set("active", ReferenceEquals(document, active))
                .Set("modified", document.IsModified);
        }

        private JsonArray Documents()
        {
            // Drawings met for the first time are given their ids by name, so that two runs of a test give the same ids.
            foreach (
                CadDocument document in _host
                    .Documents.Where(d => !_ids.ContainsKey(d))
                    .OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(d => d.Title, StringComparer.Ordinal)
                    .ThenBy(d => d.FileName ?? string.Empty, StringComparer.Ordinal)
                    .ToList()
            )
            {
                IdOf(document);
            }

            var list = new JsonArray();

            foreach (CadDocument document in _host.Documents.OrderBy(d => _ids[d]))
            {
                list.Add(Info(document));
            }

            return list;
        }

        private JsonObject Shown(CadDocument document)
        {
            IdOf(document);
            return new JsonObject().Set("document", Info(document));
        }

        private CadDocument Opened(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new Refusal("Open needs the path of a file.");
            }

            try
            {
                return _host.Open(path) ?? throw new Refusal("GeometryHelper CAD could not open " + path + ".");
            }
            catch (InvalidOperationException ex)
            {
                throw new Refusal(ex.Message);
            }
        }

        private CadDocument Made(string template)
        {
            try
            {
                return _host.New(template) ?? throw new Refusal("GeometryHelper CAD could not start a new drawing.");
            }
            catch (InvalidOperationException ex)
            {
                throw new Refusal(ex.Message);
            }
        }

        /// <summary>The drawing a request names by its id: refused when the app never had it, and when it was closed meanwhile.</summary>
        private CadDocument Find(JsonObject request)
        {
            long id = request.GetLong("doc", -1);

            if (!_known.TryGetValue((int)id, out Known known))
            {
                throw new Refusal("GeometryHelper CAD has no drawing with the id " + id + ".");
            }

            if (!_host.Documents.Contains(known.Document))
            {
                throw new Refusal("The drawing " + known.Name + " is no longer open.");
            }

            known.Name = known.Document.Title;
            return known.Document;
        }

        // ---- Save ----------------------------------------------------------------------------------------------------

        private JsonObject Save(CadDocument document)
        {
            if (!document.HasFile)
            {
                throw new Refusal("The drawing " + document.Title + " has no file yet: use SaveAs.");
            }

            try
            {
                _host.Save(document);
            }
            catch (Exception ex)
                when (ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new Refusal("The drawing " + document.Title + " could not be saved: " + ex.Message);
            }

            return Shown(document);
        }

        private JsonObject SaveAs(CadDocument document, JsonObject request)
        {
            string path = request.GetString("path");

            if (string.IsNullOrEmpty(path))
            {
                throw new Refusal("SaveAs needs the path of a file.");
            }

            try
            {
                _host.SaveAs(document, path, request.GetString("version"));
            }
            catch (Exception ex)
                when (ex is InvalidOperationException
                    || ex is IOException
                    || ex is UnauthorizedAccessException
                    || ex is ArgumentException
                    || ex is NotSupportedException
                )
            {
                throw new Refusal("The drawing " + document.Title + " could not be saved: " + ex.Message);
            }

            return Shown(document);
        }

        // ---- Commands ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Types the text and waits for the command to end. The typing is one dispatch; the waiting is a look now and then,
        /// each its own dispatch, so that the thread of the drawings is free for the user, who may be the one to answer the
        /// command's prompt.
        /// </summary>
        private JsonObject RunCommand(JsonObject request)
        {
            string text = request.GetString("text") ?? string.Empty;
            long timeout = Math.Max(0, request.GetLong("timeout", 30000));
            CadCommandSession session = null;
            int before = 0;

            Dispatch(() =>
            {
                CadDocument document = Find(request);

                if (!ReferenceEquals(document, _host.ActiveDocument))
                {
                    _host.Activate(document);
                }

                session = _host.SessionOf(document);
                before = session.History.Count;

                foreach (string line in Lines(text))
                {
                    _host.Type(document, line);
                }

                return 0;
            });

            DateTime end = DateTime.UtcNow.AddMilliseconds(timeout);

            while (Dispatch(() => session.IsRunning) && DateTime.UtcNow < end && !_stopping)
            {
                Thread.Sleep(CommandPoll);
            }

            var output = new JsonArray();
            bool running = false;
            Dispatch(() =>
            {
                running = session.IsRunning;

                for (int i = before; i < session.History.Count; i++)
                {
                    output.Add(session.History[i]);
                }

                // A command left waiting shows its prompt on the command line, which the history does not hold yet.
                if (running && session.PromptText.Length > 0)
                {
                    output.Add(session.PromptText);
                }

                return 0;
            });
            return new JsonObject().Set("completed", !running).Set("output", output);
        }

        /// <summary>The lines of the text to type: parted at line feeds, the last one dropped where the text ends with one.</summary>
        private static IEnumerable<string> Lines(string text)
        {
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            int count = lines[lines.Length - 1].Length == 0 ? lines.Length - 1 : lines.Length;

            for (int i = 0; i < count; i++)
            {
                yield return lines[i];
            }
        }

        // ---- Selection -----------------------------------------------------------------------------------------------

        private JsonObject Selected(CadDocument document)
        {
            var entities = new List<DxfEntity>();

            foreach (
                string handle in document.Selection.Handles.OrderBy(HandleNumber).ThenBy(h => h, StringComparer.Ordinal)
            )
            {
                DxfEntity entity = document.FindEntity(handle);

                if (entity != null && !entity.IsErased)
                {
                    entities.Add(entity);
                }
            }

            return Pack(document, entities);
        }

        private JsonObject Select(CadDocument document, JsonArray handles)
        {
            var wanted = new List<string>();

            foreach (object item in handles)
            {
                string handle = item as string;
                DxfEntity entity = handle == null ? null : document.FindEntity(handle);

                if (entity == null || entity.IsErased)
                {
                    throw new Refusal("The drawing " + document.Title + " has no object " + handle + ".");
                }

                wanted.Add(handle);
            }

            document.Selection.Set(wanted);
            return new JsonObject();
        }

        /// <summary>A handle as a number, for ordering by the order the objects were made; the largest where it is not hexadecimal.</summary>
        private static long HandleNumber(string handle) =>
            long.TryParse(handle, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out long number)
                ? number
                : long.MaxValue;
    }
}

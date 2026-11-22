using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace GeometryHelper.Cad.Remote
{
    /// <summary>
    /// The words of the channel. A request is <c>{"id":7,"op":"commit","doc":2,...}</c>, its answer
    /// <c>{"id":7,"ok":true,...}</c> or <c>{"id":7,"ok":false,"error":"..."}</c>; the members beside <c>op</c> and
    /// <c>doc</c> belong to the operation. A new operation is a new word here, and an app that does not know one answers
    /// with an error, so that an old client and a new app (or the other way about) keep working for what they share.
    /// </summary>
    internal static class RemoteOps
    {
        /// <summary>The version of the protocol; an app and a client of different versions refuse each other.</summary>
        public const int Version = 1;

        /// <summary>The first request of a connection: the app answers with its version, process id and title.</summary>
        public const string Hello = "hello";

        /// <summary>The open drawings: id, name, path, whether active, whether changed.</summary>
        public const string Documents = "documents";

        /// <summary>Opens a drawing from a file, or shows it where it is open already.</summary>
        public const string Open = "open";

        /// <summary>Starts a new drawing, from a template where one is named.</summary>
        public const string New = "new";

        /// <summary>Shows a drawing.</summary>
        public const string Activate = "activate";

        /// <summary>Reads objects by handle.</summary>
        public const string Get = "get";

        /// <summary>Reads the objects that match a filter.</summary>
        public const string Query = "query";

        /// <summary>The handles of the objects selected.</summary>
        public const string Selected = "selected";

        /// <summary>Selects objects by handle.</summary>
        public const string Select = "select";

        /// <summary>Applies the queued inserts, modifies and deletes as one undo step.</summary>
        public const string Commit = "commit";

        /// <summary>Types text on the drawing's command line.</summary>
        public const string Command = "command";

        /// <summary>Saves a drawing to its file.</summary>
        public const string Save = "save";

        /// <summary>Saves a drawing to a file of a name.</summary>
        public const string SaveAs = "saveAs";

        /// <summary>The text styles of a drawing, in a fragment, with the names in order.</summary>
        public const string TextStyles = "textStyles";

        /// <summary>The dimension styles of a drawing, in a fragment, with the names in order.</summary>
        public const string DimStyles = "dimStyles";

        /// <summary>The blocks of a drawing that are no layout and not anonymous, in a fragment, with the names in order.</summary>
        public const string Blocks = "blocks";

        /// <summary>Closes a drawing without saving; refused where it has changes not saved, unless told to discard them.</summary>
        public const string Close = "close";

        /// <summary>Waits for the user to pick a point, an object or objects in a drawing.</summary>
        public const string Pick = "pick";

        /// <summary>The layers of a drawing with their looks.</summary>
        public const string Layers = "layers";

        /// <summary>Reads or sets the current layer of a drawing.</summary>
        public const string CurrentLayer = "currentLayer";

        /// <summary>The names of the model tab and the layouts of a drawing, in the order of the tabs.</summary>
        public const string Layouts = "layouts";

        /// <summary>Reads or sets the layout a drawing shows.</summary>
        public const string CurrentLayout = "currentLayout";
    }

    /// <summary>One app that has announced itself in the instance folder.</summary>
    internal sealed class RemoteInstance
    {
        /// <summary>Gets or sets the process id of the app.</summary>
        public int ProcessId { get; set; }

        /// <summary>Gets or sets the title of the app's window.</summary>
        public string Title { get; set; }

        /// <summary>Gets or sets the time the user last activated the app, in UTC.</summary>
        public DateTime LastActivated { get; set; }

        /// <summary>Gets or sets the name of the app's pipe.</summary>
        public string Pipe { get; set; }

        /// <summary>Gets or sets the file the instance was read from or is written to.</summary>
        public string FilePath { get; set; }

        /// <summary>The folder the apps announce themselves in: <c>%LocalAppData%\GeometryHelper\Remote</c>.</summary>
        public static string DefaultFolder() =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GeometryHelper",
                "Remote"
            );

        /// <summary>The pipe of an app: <c>GeometryHelper.Cad.&lt;process id&gt;</c>, with <c>.&lt;instance&gt;</c> after it for a second server of one process.</summary>
        public static string PipeName(int processId, string instance) =>
            "GeometryHelper.Cad." + processId.ToString(CultureInfo.InvariantCulture) + Suffix(instance);

        /// <summary>The file of an app: <c>&lt;process id&gt;.json</c>, with <c>.&lt;instance&gt;</c> before the extension for a second server of one process.</summary>
        public static string FileName(int processId, string instance) =>
            processId.ToString(CultureInfo.InvariantCulture) + Suffix(instance) + ".json";

        private static string Suffix(string instance) => string.IsNullOrEmpty(instance) ? string.Empty : "." + instance;

        /// <summary>Writes the instance to its file, whole or not at all.</summary>
        public void Write()
        {
            string json = RemoteJson.Write(
                new JsonObject()
                    .Set("protocol", (long)RemoteOps.Version)
                    .Set("processId", (long)ProcessId)
                    .Set("title", Title ?? string.Empty)
                    .Set("lastActivated", LastActivated.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture))
                    .Set("pipe", Pipe)
            );
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            string temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, json, new UTF8Encoding(false));

            if (File.Exists(FilePath))
            {
                File.Replace(temporary, FilePath, null);
            }
            else
            {
                File.Move(temporary, FilePath);
            }
        }

        /// <summary>Takes the instance's file away, as the app does on exit; a file already gone is no error.</summary>
        public void Delete()
        {
            try
            {
                File.Delete(FilePath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                GeometryHelperLog.Debug("Remote: " + FilePath + " could not be deleted: " + ex.Message);
            }
        }

        /// <summary>
        /// The apps running now, the last activated first (a tie by process id, then by file name): the files of the folder
        /// whose process is alive. A file of a process that is gone, or one that does not read, is taken away.
        /// </summary>
        public static List<RemoteInstance> ReadAll(string folder)
        {
            var found = new List<RemoteInstance>();

            if (!Directory.Exists(folder))
            {
                return found;
            }

            string[] files = Directory.GetFiles(folder, "*.json");
            Array.Sort(files, StringComparer.Ordinal);

            foreach (string file in files)
            {
                RemoteInstance instance = TryRead(file);

                // A file that does not read, or is of another protocol, may be a newer app's: it is left alone.
                if (instance == null)
                {
                    continue;
                }

                if (!IsAlive(instance.ProcessId))
                {
                    instance.Delete();
                    continue;
                }

                found.Add(instance);
            }

            found.Sort(
                (a, b) =>
                {
                    int order = b.LastActivated.CompareTo(a.LastActivated);
                    order = order != 0 ? order : a.ProcessId.CompareTo(b.ProcessId);
                    return order != 0 ? order : string.CompareOrdinal(a.FilePath, b.FilePath);
                }
            );
            return found;
        }

        private static RemoteInstance TryRead(string file)
        {
            try
            {
                var o = RemoteJson.Parse(File.ReadAllText(file, Encoding.UTF8)) as JsonObject;
                string pipe = o?.GetString("pipe");

                if (pipe == null || o.GetLong("protocol") != RemoteOps.Version)
                {
                    return null;
                }

                return new RemoteInstance
                {
                    ProcessId = (int)o.GetLong("processId"),
                    Title = o.GetString("title") ?? string.Empty,
                    LastActivated = DateTime.TryParse(
                        o.GetString("lastActivated"),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                        out DateTime when
                    )
                        ? when
                        : DateTime.MinValue,
                    Pipe = pipe,
                    FilePath = file,
                };
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is FormatException)
            {
                GeometryHelperLog.Debug("Remote: " + file + " does not read: " + ex.Message);
                return null;
            }
        }

        private static bool IsAlive(int processId)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    return !process.HasExited;
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}

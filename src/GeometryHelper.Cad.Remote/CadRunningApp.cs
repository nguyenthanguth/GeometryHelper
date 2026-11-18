using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad.Remote
{
    /// <summary>
    /// One GeometryHelper CAD that is running, as <see cref="CadApplication.GetRunning()"/> lists it: which process, what its
    /// window says, when the user last worked in it and which drawings it has open.
    /// </summary>
    public sealed class CadRunningApp
    {
        internal CadRunningApp(RemoteInstance instance, IReadOnlyList<string> drawings, string folder)
        {
            Instance = instance;
            Folder = folder;
            Drawings = drawings;
        }

        internal RemoteInstance Instance { get; }

        internal string Folder { get; }

        /// <summary>Gets the id of the app's process.</summary>
        public int ProcessId => Instance.ProcessId;

        /// <summary>Gets the title of the app's window when it last said, as <c>A-101.dwg - GeometryHelper CAD</c>.</summary>
        public string Title => Instance.Title;

        /// <summary>Gets the time the user last activated the app, in UTC; the app that was activated last is the one <c>new CadApplication()</c> connects to.</summary>
        public DateTime LastActivated => Instance.LastActivated;

        /// <summary>
        /// Gets the drawings the app has open: the full path of each saved one, the name of one with no file yet. Empty when
        /// the app did not answer in time to be asked, a long command running in it.
        /// </summary>
        public IReadOnlyList<string> Drawings { get; }

        /// <inheritdoc/>
        public override string ToString() => "Process " + ProcessId + ": " + Title + " (" + Drawings.Count + " open)";
    }
}

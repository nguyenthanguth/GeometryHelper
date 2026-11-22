using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// What a <see cref="CadRemoteServer"/> needs of the app it serves: the drawings open, the one the user works in, and
    /// the ways to open, make, show and save one and to type on a drawing's command line. The app's window implements it
    /// over its view model; a test implements it over a few documents and no window. Every member is called on the thread
    /// of the <see cref="ICadRemoteDispatcher"/> the server was given, never from the pipe's own thread.
    /// </summary>
    public interface ICadRemoteHost
    {
        /// <summary>Gets the title of the app's window, as <c>A-101.dwg - GeometryHelper CAD</c>.</summary>
        string Title { get; }

        /// <summary>Gets the drawings open now.</summary>
        IReadOnlyList<CadDocument> Documents { get; }

        /// <summary>Gets the drawing the user works in, or null when none is open.</summary>
        CadDocument ActiveDocument { get; }

        /// <summary>Opens a drawing from a file and shows it, or shows it where it is open already.</summary>
        /// <param name="path">The full path of a DXF or DWG file.</param>
        /// <returns>The drawing.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the file cannot be opened; the message says why.</exception>
        CadDocument Open(string path);

        /// <summary>Starts a new drawing and shows it.</summary>
        /// <param name="template">The full path of a template, or null for the app's own.</param>
        /// <returns>The drawing.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the drawing cannot be made; the message says why.</exception>
        CadDocument New(string template);

        /// <summary>Shows a drawing open in the app, as clicking its entry in the list does.</summary>
        /// <param name="document">The drawing.</param>
        void Activate(CadDocument document);

        /// <summary>Gets the command session of a drawing: its command line, the commands running in it and what they wrote.</summary>
        /// <param name="document">The drawing.</param>
        /// <returns>The session.</returns>
        CadCommandSession SessionOf(CadDocument document);

        /// <summary>
        /// Types a line on the command line of the drawing, as the user does: a command's name or the answer to its prompt,
        /// words parted by spaces going in one by one, an empty line being Enter.
        /// </summary>
        /// <param name="document">The drawing, shown already.</param>
        /// <param name="line">The line.</param>
        void Type(CadDocument document, string line);

        /// <summary>Saves a drawing to its file.</summary>
        /// <param name="document">The drawing, which has a file.</param>
        /// <exception cref="InvalidOperationException">Thrown when it cannot be written; the message says why.</exception>
        void Save(CadDocument document);

        /// <summary>Saves a drawing to a file, which it is known by afterwards.</summary>
        /// <param name="document">The drawing.</param>
        /// <param name="path">The full path.</param>
        /// <param name="version">The version of the file, as <c>AC1024</c>, or null for the drawing's own.</param>
        /// <exception cref="InvalidOperationException">Thrown when it cannot be written; the message says why.</exception>
        void SaveAs(CadDocument document, string path, string version);

        /// <summary>
        /// Closes a drawing without asking and without saving, as the x of its row does once the question is answered Don't
        /// save: it leaves <see cref="Documents"/>, and the next one is shown, or the start screen where it was the last. The
        /// server has checked already that its changes are to be thrown away and that nothing runs in it, so the host asks the
        /// user nothing and shows no dialog.
        /// </summary>
        /// <param name="document">The drawing, open.</param>
        void Close(CadDocument document);
    }

    /// <summary>
    /// Runs work on the thread the app's drawings belong to. The server reads its pipe on threads of its own and hands every
    /// request to the dispatcher, so that a drawing is only touched by the thread that owns it.
    /// </summary>
    public interface ICadRemoteDispatcher
    {
        /// <summary>Runs the action on the thread of the drawings and returns when it has finished. An exception is the action's to catch: the server's actions do.</summary>
        /// <param name="action">The action.</param>
        void Invoke(Action action);
    }
}

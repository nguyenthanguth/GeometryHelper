namespace GeometryHelper.Cad.Python
{
    /// <summary>What <see cref="CadPythonHost.Enter"/> did with a palette entry.</summary>
    public enum CadPythonEntry
    {
        /// <summary>The entry was complete and has been run, or was a syntax error and has been reported.</summary>
        Ran,

        /// <summary>The entry is not complete yet (<c>def f():</c>): the palette shows "... " and keeps the text.</summary>
        Incomplete,

        /// <summary>Nothing ran: a command or a script is running, or there is no Python to run it.</summary>
        Refused,
    }

    /// <summary>How a run of Python ended.</summary>
    public enum CadPythonOutcome
    {
        /// <summary>It ended with no error, or with <c>SystemExit</c> of code none or 0.</summary>
        Completed,

        /// <summary>It raised an error (or <c>SystemExit</c> of another code): its edits were taken back.</summary>
        Failed,

        /// <summary>Esc ended it (a <c>KeyboardInterrupt</c>): its edits were taken back.</summary>
        Cancelled,

        /// <summary>It did not start: another run goes on, the file was refused or not found, or there is no Python.</summary>
        Refused,
    }
}

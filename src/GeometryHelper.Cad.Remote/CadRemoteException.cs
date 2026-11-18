using System;

namespace GeometryHelper.Cad.Remote
{
    /// <summary>
    /// What the remote channel throws when the app cannot be reached or refuses a request: no GeometryHelper CAD is running,
    /// the connection was lost, a drawing was closed meanwhile, the owner of an object refused an edit, a block is not in
    /// the drawing. The message says why, in the words the app's own command line would.
    /// </summary>
    public sealed class CadRemoteException : Exception
    {
        /// <summary>Makes the exception.</summary>
        /// <param name="message">Why the request failed.</param>
        public CadRemoteException(string message)
            : base(message) { }

        /// <summary>Makes the exception.</summary>
        /// <param name="message">Why the request failed.</param>
        /// <param name="inner">What went wrong underneath.</param>
        public CadRemoteException(string message, Exception inner)
            : base(message, inner) { }
    }
}

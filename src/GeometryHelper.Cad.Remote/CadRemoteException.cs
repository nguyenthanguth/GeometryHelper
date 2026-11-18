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

        /// <summary>Makes the exception for a pick the user cancelled.</summary>
        /// <param name="message">Why the request failed.</param>
        /// <param name="isCancelled">Whether the user ended the pick with Esc.</param>
        public CadRemoteException(string message, bool isCancelled)
            : base(message)
        {
            IsCancelled = isCancelled;
        }

        /// <summary>Makes the exception.</summary>
        /// <param name="message">Why the request failed.</param>
        /// <param name="inner">What went wrong underneath.</param>
        public CadRemoteException(string message, Exception inner)
            : base(message, inner) { }

        /// <summary>
        /// Gets whether the user ended a pick (<see cref="CadRemoteDocument.PickPoint"/>, <see cref="CadRemoteDocument.PickObject"/>
        /// or <see cref="CadRemoteDocument.PickObjects"/>) with Esc, or with Enter where nothing was picked, rather than
        /// something having gone wrong; false for every other failure, a pick that ran out of time included.
        /// </summary>
        public bool IsCancelled { get; }
    }
}

namespace GeometryHelper.Cad
{
    /// <summary>
    /// What a dispatcher that owns one thread can say about it: whether the caller is on that thread. A server whose dispatcher
    /// says so tells the clients of its own process, which then refuse to connect from that thread: a client that waits there
    /// for the app's answer waits for the very thread that would give it, as a Python script run by the window does.
    /// </summary>
    internal interface ICadRemoteOwnerThread
    {
        /// <summary>Gets whether the calling thread is the one the dispatcher runs its work on.</summary>
        /// <returns>True on that thread.</returns>
        bool IsOwnerThread();
    }
}

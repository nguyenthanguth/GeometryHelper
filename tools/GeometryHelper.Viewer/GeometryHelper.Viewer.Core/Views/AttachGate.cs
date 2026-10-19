namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Whether a view holds the elements of the scene, and which time it took them: work that was started for one time must not put its result into
    /// the next. A view that is not shown lets go of its elements and takes them again when it is shown; geometry being made for it on another thread when
    /// it let go is out of date by the time it comes back, and is dropped, not put into the elements the view has made since.
    /// </summary>
    public sealed class AttachGate
    {
        private int _epoch;
        private bool _attached;

        /// <summary>
        /// Gets whether the view holds the elements now.
        /// </summary>
        public bool IsAttached => _attached;

        /// <summary>
        /// Gets the ticket for work that starts now: it stays current until the view lets go or takes the elements, whichever comes first.
        /// </summary>
        public int Ticket => _epoch;

        /// <summary>
        /// Records that the view takes the elements.
        /// </summary>
        /// <returns>false when it held them already, and nothing changes.</returns>
        public bool Attach()
        {
            if (_attached)
            {
                return false;
            }

            _attached = true;
            _epoch++;
            return true;
        }

        /// <summary>
        /// Records that the view lets go of the elements; the work under way for them is out of date.
        /// </summary>
        /// <returns>false when it held none, and nothing changes.</returns>
        public bool Detach()
        {
            if (!_attached)
            {
                return false;
            }

            _attached = false;
            _epoch++;
            return true;
        }

        /// <summary>
        /// Gets whether work started under a ticket may still put its result in: the view holds the elements, and has held these since the ticket was given.
        /// </summary>
        /// <param name="ticket">The ticket <see cref="Ticket"/> gave when the work started.</param>
        public bool IsCurrent(int ticket) => _attached && ticket == _epoch;
    }
}

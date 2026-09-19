namespace GeometryHelper.CommonGeometry.Enums
{
    /// <summary>
    /// How much a message written through <see cref="GeometryHelperLog"/> matters.
    /// </summary>
    public enum GeometryHelperLogLevel
    {
        /// <summary>
        /// Detail that helps follow what happened, such as an object skipped for an expected reason.
        /// </summary>
        Debug,

        /// <summary>
        /// The ordinary outcome of an operation.
        /// </summary>
        Info,

        /// <summary>
        /// Something was left out or could not be done, and the work went on without it.
        /// </summary>
        Warn,

        /// <summary>
        /// Something failed that the caller should know about.
        /// </summary>
        Err
    }
}

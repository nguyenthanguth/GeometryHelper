using System;
using System.Diagnostics;
using GeometryHelper.CommonGeometry.Enums;

namespace GeometryHelper.CommonGeometry
{
    /// <summary>
    /// Where the GeometryHelper libraries report what they left out or could not do.
    /// <para>
    /// Conversions that walk many objects skip what they cannot read rather than throw, because one bad object
    /// should not cost the rest. What was skipped, and why, is written here, so that an empty or short result can
    /// be explained: an IFC file that could not be opened, a reference model that is not IFC, a body the
    /// transformation could not rebuild.
    /// </para>
    /// <para>
    /// Messages go to <see cref="Trace"/> under the category <see cref="TraceCategory"/> unless <see cref="Writer"/>
    /// is set. The Output window of Visual Studio shows them while debugging, and DebugView shows them from a plugin
    /// running inside Tekla or AutoCAD. Set <see cref="Writer"/> to send them to a file, a status bar or another
    /// logging library instead, and <see cref="Enable"/> to false to turn them off. <see cref="Writer"/> has examples
    /// for the console, the status bar of Tekla Structures and a file.
    /// </para>
    /// <para>
    /// Writing a message never throws: a writer that fails is ignored, so logging cannot break the work it reports
    /// on. Like <see cref="Tolerance.Global"/>, the settings are shared by the whole process.
    /// </para>
    /// </summary>
    public static class GeometryHelperLog
    {
        /// <summary>
        /// The <see cref="Trace"/> category messages are written under when no <see cref="Writer"/> is set.
        /// </summary>
        public const string TraceCategory = "GeometryHelper";

        private static volatile bool _enable = true;
        private static volatile Action<GeometryHelperLogLevel, string, Exception> _writer;

        /// <summary>
        /// Gets or sets whether messages are written at all. Defaults to <c>true</c>.
        /// </summary>
        public static bool Enable
        {
            get => _enable;
            set => _enable = value;
        }

        /// <summary>
        /// Gets or sets what receives each message: its level, its text and the exception behind it, if any.
        /// Null, the default, writes <see cref="Format"/> of the message to <see cref="Trace"/>.
        /// </summary>
        /// <example>
        /// On the console of a console application:
        /// <code>
        /// GeometryHelperLog.Writer = (level, message, exception) =>
        ///     Console.WriteLine(GeometryHelperLog.Format(level, message, exception));
        ///
        /// GeometryHelperLog.Info("Started.");   // prints: INFO Started.
        /// </code>
        /// On the status bar of Tekla Structures (<c>using Tekla.Structures.Model.Operations;</c> and
        /// <c>using GeometryHelper.CommonGeometry.Enums;</c>). The bar shows one line at a time, the last one written,
        /// so Debug detail is left out and the summary each conversion ends with stays on the bar:
        /// <code>
        /// GeometryHelperLog.Writer = (level, message, exception) =>
        /// {
        ///     if (level >= GeometryHelperLogLevel.Info)
        ///     {
        ///         Operation.DisplayPrompt(GeometryHelperLog.Format(level, message, exception));
        ///     }
        /// };
        /// </code>
        /// Every message in a file:
        /// <code>
        /// GeometryHelperLog.Writer = (level, message, exception) =>
        ///     File.AppendAllText(logPath, GeometryHelperLog.Format(level, message, exception) + Environment.NewLine);
        /// </code>
        /// </example>
        public static Action<GeometryHelperLogLevel, string, Exception> Writer
        {
            get => _writer;
            set => _writer = value;
        }

        /// <summary>
        /// Writes detail that helps follow what happened, such as an object skipped for an expected reason.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="exception">The exception behind it, if any.</param>
        public static void Debug(string message, Exception exception = null) => Write(GeometryHelperLogLevel.Debug, message, exception);

        /// <summary>
        /// Writes the ordinary outcome of an operation.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="exception">The exception behind it, if any.</param>
        public static void Info(string message, Exception exception = null) => Write(GeometryHelperLogLevel.Info, message, exception);

        /// <summary>
        /// Writes that something was left out or could not be done, and the work went on without it.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="exception">The exception behind it, if any.</param>
        public static void Warn(string message, Exception exception = null) => Write(GeometryHelperLogLevel.Warn, message, exception);

        /// <summary>
        /// Writes that something failed that the caller should know about.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="exception">The exception behind it, if any.</param>
        public static void Err(string message, Exception exception = null) => Write(GeometryHelperLogLevel.Err, message, exception);

        /// <summary>
        /// Formats a message the way the default writer does, for example
        /// <c>WARN The IFC file cannot be read. [IOException: The file is in use.]</c>.
        /// </summary>
        /// <param name="level">The level of the message.</param>
        /// <param name="message">The message.</param>
        /// <param name="exception">The exception behind it, if any.</param>
        /// <returns>The level in capitals, the message, and the type and message of the exception.</returns>
        public static string Format(GeometryHelperLogLevel level, string message, Exception exception)
        {
            string text = level.ToString().ToUpperInvariant() + " " + (message ?? string.Empty);

            return exception == null
                ? text
                : text + " [" + exception.GetType().Name + ": " + exception.Message + "]";
        }

        private static void Write(GeometryHelperLogLevel level, string message, Exception exception)
        {
            if (!_enable)
            {
                return;
            }

            try
            {
                Action<GeometryHelperLogLevel, string, Exception> writer = _writer;

                if (writer != null)
                {
                    writer(level, message ?? string.Empty, exception);
                }
                else
                {
                    Trace.WriteLine(Format(level, message, exception), TraceCategory);
                }
            }
            catch (Exception)
            {
                // Logging never breaks the work it reports on.
            }
        }
    }
}

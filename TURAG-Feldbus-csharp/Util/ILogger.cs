namespace Fraunhofer.IKTS.EddyCurrent.EdCAL.Utils
{
    /// <summary>
    /// Interface for logging debug messages. Implementations
    /// are required to be thread-safe.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs a verbose debug message.
        /// </summary>
        /// <param name="message">Verbose debug message.</param>
        /// <param name="context">Reference to calling object.</param>
        void Verbose(string message, object context);

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">Debug message.</param>
        /// <param name="context">Reference to calling object.</param>
        void Debug(string message, object context);

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">Informational message.</param>
        /// <param name="context">Reference to calling object.</param>
        void Information(string message, object context);

        /// <summary>
        /// Logs a warning.
        /// </summary>
        /// <param name="message">Warning to log.</param>
        /// <param name="context">Reference to calling object.</param>
        void Warning(string message, object context);

        /// <summary>
        /// Logs an error.
        /// </summary>
        /// <param name="message">Error message to log.</param>
        /// <param name="context">Reference to calling object.</param>
        void Error(string message, object context);

        /// <summary>
        /// Logs a fatal error.
        /// </summary>
        /// <param name="message">Fatal error message to log.</param>
        /// <param name="context">Reference to calling object.</param>
        void Fatal(string message, object context);
    }
}

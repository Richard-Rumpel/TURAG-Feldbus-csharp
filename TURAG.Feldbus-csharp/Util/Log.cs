namespace TURAG.Feldbus.Util
{
    /// <summary>
    /// Static class representing the logger used 
    /// to communicate debug messages. By default no log sink is set
    /// and no messages are emitted.
    /// </summary>
#if __DOXYGEN__
    public class Log
#else
    public static class Log
#endif
    {
        /// <summary>
        /// Sets the log sink.
        /// </summary>
        /// <param name="logger">Log sink log messages are relayed to.</param>
        public static void SetLogSink(ILogger logger)
        {
            Logger = logger;
        }

        internal static ILogger Logger { get; set; }
    }
}

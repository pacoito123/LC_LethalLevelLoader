using System.Diagnostics;

namespace LethalLevelLoader
{
    internal static class DebugStopwatch
    {
        private static string currentText = string.Empty;
        private static Stopwatch currentStopwatch;

        internal static void StartStopWatch(string newStopWatchText)
        {
            if (currentStopwatch != null)
                StopStopWatch(currentText);

            currentStopwatch = Stopwatch.StartNew();
            currentText = newStopWatchText;
        }

        internal static void StopStopWatch(string stopWatchText)
        {
            if (currentStopwatch == null) return;

            currentStopwatch.Stop();
            DebugHelper.Log($"[Debug Stopwatch] {stopWatchText} : {currentStopwatch.Elapsed.TotalSeconds:0.##} Seconds. ({currentStopwatch.ElapsedMilliseconds}ms)", DebugType.IAmBatby);

            currentText = string.Empty;
            currentStopwatch = null;
        }
    }
}

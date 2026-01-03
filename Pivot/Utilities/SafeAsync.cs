using System;
using System.Threading.Tasks;

namespace Pivot.Utilities
{
    /// <summary>
    /// Safe async helpers to prevent fire-and-forget exception loss.
    /// </summary>
    public static class SafeAsync
    {
        /// <summary>
        /// Executes an async task safely, logging any exceptions instead of losing them.
        /// Use this for fire-and-forget scenarios like property changed handlers.
        /// </summary>
        /// <param name="task">The task to execute</param>
        /// <param name="context">Optional context for logging (e.g., method name)</param>
        public static async void FireAndForget(Task task, string? context = null)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var contextInfo = string.IsNullOrEmpty(context) ? "" : $"[{context}] ";
                System.Diagnostics.Debug.WriteLine($"{contextInfo}Fire-and-forget task failed: {ex.Message}");
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
#endif
            }
        }

        /// <summary>
        /// Executes an async action safely, logging any exceptions instead of losing them.
        /// </summary>
        /// <param name="asyncAction">The async action to execute</param>
        /// <param name="context">Optional context for logging (e.g., method name)</param>
        public static async void FireAndForget(Func<Task> asyncAction, string? context = null)
        {
            try
            {
                await asyncAction().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var contextInfo = string.IsNullOrEmpty(context) ? "" : $"[{context}] ";
                System.Diagnostics.Debug.WriteLine($"{contextInfo}Fire-and-forget task failed: {ex.Message}");
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
#endif
            }
        }
    }
}

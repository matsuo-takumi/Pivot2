using System;

namespace Pivot.CodeModule.ViewModels
{
    /// <summary>
    /// Event args for snippet creation from QuickAdd control.
    /// </summary>
    public class QuickAddEventArgs : EventArgs
    {
        public string Title { get; set; } = string.Empty;
        public string Language { get; set; } = "text";
        public string Code { get; set; } = string.Empty;
        public string[] Tags { get; set; } = Array.Empty<string>();
    }
}

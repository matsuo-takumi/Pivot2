using Windows.System;

namespace Pivot.Models
{
    /// <summary>
    /// Represents a customizable keyboard shortcut binding
    /// </summary>
    public class KeyBinding
    {
        /// <summary>
        /// Display name of the action (e.g., "Lit", "Depth", "World Normal")
        /// </summary>
        public string ActionName { get; set; } = string.Empty;

        /// <summary>
        /// The primary key for this binding
        /// </summary>
        public VirtualKey Key { get; set; } = VirtualKey.None;

        /// <summary>
        /// Required modifier keys (Alt, Ctrl, Shift)
        /// </summary>
        public VirtualKeyModifiers Modifiers { get; set; } = VirtualKeyModifiers.None;

        /// <summary>
        /// String representation for display (e.g., "Alt+1")
        /// </summary>
        public string DisplayString
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                
                if ((Modifiers & VirtualKeyModifiers.Control) != 0)
                    parts.Add("Ctrl");
                if ((Modifiers & VirtualKeyModifiers.Menu) != 0)
                    parts.Add("Alt");
                if ((Modifiers & VirtualKeyModifiers.Shift) != 0)
                    parts.Add("Shift");
                
                if (Key != VirtualKey.None)
                {
                    var keyName = Key switch
                    {
                        VirtualKey.Number1 => "1",
                        VirtualKey.Number2 => "2",
                        VirtualKey.Number3 => "3",
                        VirtualKey.Number4 => "4",
                        VirtualKey.Number5 => "5",
                        VirtualKey.Number6 => "6",
                        VirtualKey.Number7 => "7",
                        VirtualKey.Number8 => "8",
                        VirtualKey.Number9 => "9",
                        VirtualKey.Number0 => "0",
                        _ => Key.ToString()
                    };
                    parts.Add(keyName);
                }
                
                return string.Join("+", parts);
            }
        }

        /// <summary>
        /// Create default Lit shortcut (Alt+1)
        /// </summary>
        public static KeyBinding DefaultLit => new()
        {
            ActionName = "Lit",
            Key = VirtualKey.Number1,
            Modifiers = VirtualKeyModifiers.Menu
        };

        /// <summary>
        /// Create default Depth shortcut (Alt+2)
        /// </summary>
        public static KeyBinding DefaultDepth => new()
        {
            ActionName = "Depth",
            Key = VirtualKey.Number2,
            Modifiers = VirtualKeyModifiers.Menu
        };

        /// <summary>
        /// Create default World Normal shortcut (Alt+3)
        /// </summary>
        public static KeyBinding DefaultWorldNormal => new()
        {
            ActionName = "World Normal",
            Key = VirtualKey.Number3,
            Modifiers = VirtualKeyModifiers.Menu
        };
    }
}

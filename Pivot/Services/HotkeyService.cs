using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Pivot.Services
{
    public class HotkeyService
    {
        // P/Invoke constants
        private const int WM_HOTKEY = 0x0312;
        private const int GWLP_WNDPROC = -4;
        private const int MOD_ALT = 0x0001;
        private const uint VK_V = 0x56;
        private const int HOTKEY_ID = 9000;

        // Delegates
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // Win32 APIs
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public const int SW_RESTORE = 9;
        public const int SW_MINIMIZE = 6;

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        // Valid for 64-bit. On 32-bit this maps to SetWindowLong.
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        private IntPtr _hwnd;
        private IntPtr _oldWndProc;
        private WndProcDelegate _newWndProcDelegate; // Prevent GC
        private bool _isRegistered;

        public event EventHandler? HotkeyPressed;

        public void Register(Window window)
        {
            if (_isRegistered) return;

            try 
            {
                _hwnd = WindowNative.GetWindowHandle(window);
                if (_hwnd == IntPtr.Zero) return;

                // Subclass WndProc
                _newWndProcDelegate = new WndProcDelegate(NewWndProc);
                _oldWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newWndProcDelegate));

                // Register Hotkey
                if (RegisterHotKey(_hwnd, HOTKEY_ID, MOD_ALT, VK_V))
                {
                    _isRegistered = true;
                    System.Diagnostics.Debug.WriteLine($"[HotkeyService] Registered Alt+V on HWND {_hwnd}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[HotkeyService] Failed to register Alt+V on HWND {_hwnd}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HotkeyService] Error registering hotkey: {ex.Message}");
            }
        }

        public void Unregister()
        {
            if (!_isRegistered || _hwnd == IntPtr.Zero) return;

            try
            {
                UnregisterHotKey(_hwnd, HOTKEY_ID);
                
                // Restore WndProc
                if (_oldWndProc != IntPtr.Zero)
                {
                    SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _oldWndProc);
                    _oldWndProc = IntPtr.Zero;
                }
                
                _isRegistered = false;
                System.Diagnostics.Debug.WriteLine($"[HotkeyService] Unregistered hotkey");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HotkeyService] Error unregistering hotkey: {ex.Message}");
            }
        }

        private IntPtr NewWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_HOTKEY)
            {
                if (wParam.ToInt32() == HOTKEY_ID)
                {
                    HotkeyPressed?.Invoke(this, EventArgs.Empty);
                }
            }

            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        public IntPtr GetHwnd() => _hwnd;
    }
}

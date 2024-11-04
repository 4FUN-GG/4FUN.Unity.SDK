using System;
using System.Runtime.InteropServices;

namespace FourFun
{
    /// <summary>
    /// Provides methods for interacting with Windows API to manage window focus and visibility.
    /// </summary>
    public class WinAPI
    {
        // PInvoke declarations for user32.dll functions

        /// <summary>
        /// Brings the specified window to the foreground.
        /// </summary>
        /// <param name="hWnd">A handle to the window to be brought to the foreground.</param>
        /// <returns>True if the window was brought to the foreground; otherwise, false.</returns>
        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// Activates the specified window.
        /// </summary>
        /// <param name="hWnd">A handle to the window to be activated.</param>
        /// <returns>A handle to the previously active window.</returns>
        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr SetActiveWindow(IntPtr hWnd);

        /// <summary>
        /// Shows the specified window.
        /// </summary>
        /// <param name="hWnd">A handle to the window to be shown.</param>
        /// <param name="nCmdShow">Specifies how the window is to be shown.</param>
        /// <returns>True if the window was shown; otherwise, false.</returns>
        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        /// <summary>
        /// Updates the client area of the specified window.
        /// </summary>
        /// <param name="hWnd">A handle to the window to be updated.</param>
        /// <returns>True if the window was updated; otherwise, false.</returns>
        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern bool UpdateWindow(IntPtr hWnd);

        /// <summary>
        /// Sets the keyboard focus to the specified window.
        /// </summary>
        /// <param name="hWnd">A handle to the window to receive the focus.</param>
        /// <returns>A handle to the window that previously had the keyboard focus.</returns>
        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        /// <summary>
        /// Retrieves the handle to the currently active window.
        /// </summary>
        /// <returns>A handle to the active window.</returns>
        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetActiveWindow();

        /// <summary>
        /// Finds a window by its class name or window name.
        /// </summary>
        /// <param name="lpClassName">The class name of the window to be found.</param>
        /// <param name="lpWindowName">The title of the window to be found.</param>
        /// <returns>A handle to the window if found; otherwise, IntPtr.Zero.</returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern IntPtr FindWindowW(string lpClassName, string lpWindowName);

        /// <summary>
        /// Brings the specified window to the top of the Z order and activates it.
        /// If the active window is not found, it attempts to find a window by the specified fallback name.
        /// </summary>
        /// <param name="fallBackWindowName">The title of the window to use as a fallback.</param>
        public void ForceActiveWindowToTop(string fallBackWindowName = "")
        {
            IntPtr activeWindowHandle = GetActiveWindow();

            // If there is no active window, try to find the fallback window by name.
            if (IntPtr.Zero == activeWindowHandle && !string.IsNullOrEmpty(fallBackWindowName))
            {
                activeWindowHandle = FindWindowW(null, fallBackWindowName);
            }

            // If a valid window handle is found, bring it to the foreground.
            if (IntPtr.Zero != activeWindowHandle)
            {
                ShowWindow(activeWindowHandle, 3); // SW_MAXIMIZE
                UpdateWindow(activeWindowHandle);
                SetForegroundWindow(activeWindowHandle);
                SetFocus(activeWindowHandle);
                SetActiveWindow(activeWindowHandle);
            }
        }
    }
}

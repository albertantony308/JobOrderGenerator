using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ClientApp.Services
{
    public static class WindowDwmFixer
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;

        /// <summary>
        /// Fixes Windows 10 DWM rendering bug for WPF custom borderless / WindowChrome / AllowsTransparency windows
        /// by forcing a Win32 frame refresh and a queued post-render size invalidation.
        /// </summary>
        public static void ApplyFix(Window window)
        {
            if (window == null) return;

            window.Loaded += (s, e) =>
            {
                try
                {
                    var hwnd = new WindowInteropHelper(window).Handle;
                    if (hwnd != IntPtr.Zero)
                    {
                        // 1. Send SWP_FRAMECHANGED to DWM to invalidate non-client titlebar/chrome rects
                        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
                    }

                    // 2. Queue a micro-adjustment on Dispatcher to guarantee Windows 10 DWM size update
                    window.Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        await Task.Delay(100);
                        if (!window.IsLoaded) return;
                        var origWidth = window.Width;
                        if (!double.IsNaN(origWidth) && origWidth > 10)
                        {
                            window.Width = origWidth - 0.5;
                            await Task.Delay(20);
                            window.Width = origWidth;
                        }
                    }), DispatcherPriority.ApplicationIdle);
                }
                catch
                {
                    // Fail-safe swallow
                }
            };
        }
    }
}

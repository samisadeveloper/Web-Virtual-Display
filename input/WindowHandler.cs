using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using WebVirtualDisplayClient.util;
using static WebVirtualDisplayClient.input.RawWindowHandler;
using static WebVirtualDisplayClient.input.WindowManager;
using static WebVirtualDisplayClient.util.MouseUtil;

namespace WebVirtualDisplayClient.input;

class WindowHandler : BackgroundService
{
        private static Point extent = ScreenExtent.GetScreenExtent();
        private static WindowCachedTrackData draggedWindow;
        private static bool beyondExtent = false;
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
                MouseHandler.onMouseMoveGlobal += onMouseMoveGlobal;

                await Task.Run(() => {
                        MouseUtil.mouseClickEvent += onMouseClick;
                        RawWindowHandler.rawWindowHeld += onWindowHeld;
                        RawWindowHandler.rawWindowMovement += onWindowMove;

                        RawWindowHandler.initializeRawInput(stoppingToken);
                });
        }

        public void onWindowMove(Object? sender, WindowTrackData window) {
                int x = window.x + window.cachedData.width;
                int y = window.y + window.cachedData.height;

                Point point = new Point() {X = x, Y = y};

                if (point.BeyondExtent(extent)) {
                        if (!beyondExtent) {
                                SendMessage(window.cachedData.hwnd, WM_CANCELMODE, IntPtr.Zero, IntPtr.Zero); 
                                // let go of the window when its beyond the extent.

                                beyondExtent = true;
                        }
                }
        }

        public void onMouseMoveGlobal(Object? sender, Point cursor) {
                if (draggedWindow.hwnd == 0) return;
                if (!beyondExtent) return;

                DragOffset offset = draggedWindow.dragOffset;

                SetWindowPos(draggedWindow.hwnd, IntPtr.Zero, cursor.X - offset.FromLeft, cursor.Y - offset.FromTop, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);

                WindowData windowData = new WindowData() {
                        hwnd = (int) draggedWindow.hwnd,
                        x = (cursor.X - offset.FromLeft) - extent.X,
                        y = (cursor.Y - offset.FromTop),
                        width = draggedWindow.width,
                        height = draggedWindow.height,
                };

                WindowManager.updateWindow(windowData);
        }

        public void onWindowHeld(Object? sender, WindowCachedTrackData data) {
                RECT rect = new RECT();

                GetWindowRect(data.hwnd, ref rect);

                Point point = new Point(){X = rect.Left, Y = rect.Top};

                if (point.BeyondExtent(extent)) beyondExtent = true;

                draggedWindow = data;
        }

        public void onMouseClick(Object? sender, MouseEventType type) {
                if (type.Equals(MouseEventType.RELEASED)) {
                                draggedWindow = default;
                                beyondExtent = false;
                }
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        // Flags to optimize performance and prevent unintended changes
        const uint SWP_NOSIZE = 0x0001;       // Ignore the cx and cy parameters (keep current size)
        const uint SWP_NOZORDER = 0x0004;     // Retain the current Z order (don't bring to front/back)
        const uint SWP_NOACTIVATE = 0x0010;   // Do not activate the window (keeps focus on your app)
        const uint SWP_FRAMECHANGED = 0x0020; // Forces the window to redraw its borders (useful if styles changed)

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        const uint WM_CANCELMODE = 0x001F;
}

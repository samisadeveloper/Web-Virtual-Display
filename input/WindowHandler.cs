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

        private void onWindowMove(Object? sender, WindowTrackData window) {
                int x = window.x + window.cachedData.width;
                int y = window.y + window.cachedData.height;

                Point point = new Point() {X = x, Y = y};

                if (point.BeyondExtent(extent)) {
                        // this window should no longer be tracked

                        if (!beyondExtent) {
                                SendMessage(window.cachedData.hwnd, WM_CANCELMODE, IntPtr.Zero, IntPtr.Zero); 
                                // let go of the window when its beyond the extent.

                                beyondExtent = true;
                        }
                }
        }

        private void onMouseMoveGlobal(Object? sender, Point cursor) {
                if (draggedWindow.hwnd == 0) return;
                if (!beyondExtent) return;

                DragOffset offset = draggedWindow.dragOffset;

                int windowX = cursor.X - offset.FromLeft;
                int windowY = cursor.Y - offset.FromTop;

                int width = draggedWindow.width;
                int height = draggedWindow.height;

                if (windowX > extent.X) {
                        LayeredWindow.Apply(draggedWindow.hwnd, 1, false);
                } else {
                        LayeredWindow.Reset(draggedWindow.hwnd);
                }

                MoveWindow(draggedWindow.hwnd, new Point(){X = windowX, Y = windowY});

                WindowData windowData = new WindowData() {
                        hwnd = draggedWindow.hwnd,
                        rawX = windowX,
                        x = (windowX) - extent.X,
                        y = (windowY),
                        width = width,
                        height = height,
                };

                WindowManager.updateWindow(windowData);
        }

        private void onWindowHeld(Object? sender, WindowCachedTrackData data) {
                RECT rect = new RECT();

                GetWindowRect(data.hwnd, ref rect);

                int width = rect.Right - rect.Left;

                Point point = new Point(){X = rect.Left + width, Y = rect.Top};

                if (point.BeyondExtent(extent)) beyondExtent = true;

                draggedWindow = data;
        }

        private static void onMouseClick(Object? sender, MouseUtil.MouseEventArgs mouseArgs) {
                MouseEventType clickType = mouseArgs.type;

                if (clickType.Equals(MouseEventType.LEFT_RELEASED)) {
                        draggedWindow = default;
                        beyondExtent = false;
                }


                Point globalPoint = MouseHandler.GlobalMousePoint;
                Point point = new Point(){ X = globalPoint.X - extent.X, Y = globalPoint.Y };

                // find the window which was clicked on
                WindowData window = WindowManager.Windows.FirstOrDefault(window => window.isInWindow(point));

                if (window.hwnd == 0) return;

                DragOffset offset = new DragOffset();
                offset.FromLeft = point.X - window.x;
                offset.FromRight = window.width - offset.FromLeft;
                offset.FromTop = point.Y - window.y;

                WindowCachedTrackData windowData = new WindowCachedTrackData() {
                        dragOffset = offset,
                        hwnd = window.hwnd,
                        // window is fully beyond extent and should be virtual
                        width = window.width,
                        height = window.height,
                };

                // get mouse position relative to the window
                Point relativePoint = new Point() { X = point.X - window.x, Y = point.Y - window.y };

                // TODO: use a different number instead of a harcdoded one, it should scale depending on the monitor DPI or whatever
                if (relativePoint.Y < 50) {
                        if (clickType.Equals(MouseEventType.LEFT_PRESSED)) {
                                RawWindowHandler.rawWindowHeld?.Invoke(null, windowData);
                        } else if (clickType.Equals(MouseEventType.LEFT_RELEASED)) {
                                RawWindowHandler.rawWindowReleased?.Invoke(null, windowData);
                        }
                }

                // TODO: there is a bug where you cannot pick up a window at all due to another window being ontop of it

                if (mouseArgs.type.Equals(MouseEventType.SCROLL)) {
                        SetForegroundWindow(window.hwnd);

                        // first capture the window position
                        int x = window.rawX;
                        int y = window.y;

                        WindowHandler.MoveWindow(window.hwnd, new Point(){X = 0, Y = 0});

                        ClickWindowAt(window.hwnd, relativePoint, mouseArgs);

                        WindowHandler.MoveWindow(window.hwnd, new Point(){X = x, Y = y});
                } else {
                        ClickWindowAt(window.hwnd, relativePoint, mouseArgs);
                }
        }

        public static void MoveWindow(IntPtr hwnd, Point point) {
                int clampedX = int.Clamp(point.X, 0, extent.X - 8);

                SetWindowPos(hwnd, IntPtr.Zero, clampedX, point.Y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        // Flags to optimize performance and prevent unintended changes
        public const uint SWP_NOSIZE = 0x0001;       // Ignore the cx and cy parameters (keep current size)
        public const uint SWP_NOZORDER = 0x0004;     // Retain the current Z order (don't bring to front/back)
        public const uint SWP_NOACTIVATE = 0x0010;   // Do not activate the window (keeps focus on your app)
        public const uint SWP_FRAMECHANGED = 0x0020; // Forces the window to redraw its borders (useful if styles changed)

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        const uint WM_CANCELMODE = 0x001F;
}

using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using WebVirtualDisplayClient.util;
using static WebVirtualDisplayClient.util.MouseUtil;

namespace WebVirtualDisplayClient.input;

class WindowHandler : BackgroundService
{
        private struct DragOffset {
                public int FromLeft;
                public int FromRight;
                public int FromTop;
        }

        private struct DraggedWindow {
                public IntPtr hwnd;
                public DragOffset offset;
        }

        private static Point extent = ScreenExtent.getScreenExtent();
        private static DraggedWindow? draggedWindow;
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
                MouseHandler.onMouseMoveGlobal += onMouseMoveGlobal;

                await Task.Run(() => {
                        // attach the event before starting input
                        // why? initialize raw input blocks the thread forever
                        RawWindowHandler.rawWindowMovement += onRawWindowMovement; 

                        RawWindowHandler.initializeRawInput(stoppingToken);
                });
        }

        public void onMouseMoveGlobal(Object? sender, Point cursor) {
                if (draggedWindow != null) {
                        DragOffset offset = draggedWindow.Value.offset;

                        Console.WriteLine($"moving window to {cursor.X} {cursor.Y}");
                        SetWindowPos(draggedWindow.Value.hwnd, IntPtr.Zero, cursor.X - offset.FromLeft, cursor.Y - offset.FromTop, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                } else {
                        Console.WriteLine("hello bald");
                }
        }

        public void onRawWindowMovement(Object? sender, RawWindowHandler.RawWindowInputEventArgs args) {
                int x = args.x + args.cachedData.width;

                if (x >= extent.X) {
                        Console.WriteLine("window is beyond extent!");

                        // compute the offset and then store it
                        DragOffset offset = new DragOffset();

                        GetCursorPos(out Point cursor);

                        offset.FromLeft = cursor.X - args.x;
                        offset.FromRight = args.cachedData.width - offset.FromLeft;
                        offset.FromTop = cursor.Y - args.y;

                        // now lets test the offset by setting the position of the window to the position of the cursor with the offset
                        draggedWindow = new DraggedWindow() {hwnd = args.cachedData.hwnd, offset = offset};

                        SendMessage(args.cachedData.hwnd, WM_CANCELMODE, IntPtr.Zero, IntPtr.Zero); // let go of the window
                } else {
                        draggedWindow = null;
                }

                // WindowManager.WindowData windowData = new WindowManager.WindowData() {
                //         hwnd = (int) args.cachedData.hwnd,
                //
                //         x = args.x,
                //         y = args.y,
                //
                //         width = args.cachedData.width,
                //         height = args.cachedData.height,
                // };

                // so unfortunately we can't just send the window movement data directly we have to do a few things
                // 1: we have to check if the window has crossed the boundary, unfortunately once the window crosses the boundary we can no longer grab it
                // 2: when the window is "passed" the boundary we can stop grabbing it and move it programatically wherever the mouse is located
                // 3: whenever the window gets moved programatically we must set window movement

                // WindowManager.sendWindowMovement(windowData);
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

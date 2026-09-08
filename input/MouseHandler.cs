using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using static WebVirtualDisplayClient.input.RawMouseHandler;

namespace WebVirtualDisplayClient.input;

class MouseHandler : BackgroundService
{
        private static Point lastMousePoint = new Point(){X = 0, Y = 0};
        private static Point globalMousePoint = new Point(){X = 0, Y = 0};

        private static bool enableRawInput = false;

        private static Point extent = getScreenExtent(); // capture the max extent of the screen
        private static Point smallerExtend = new Point(){X = extent.X - 120, Y = extent.Y - 120}; // declare a smaller extent

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
                RawMouseHandler.rawMouseMovement += onRawMouseMovement;

                // create hooks for when windows are added and deleted (yes)
                // when a the mouse goes beyond the extent, drop the current window and update the position accordingly

                while (!stoppingToken.IsCancellationRequested) {
                        GetCursorPos(out Point point);

                        if (point.BeyondExtent(extent)) { // is the mouse beyond the horizontal extent?
                                if (!enableRawInput) { // raw input is not already enabled
                                        lastMousePoint = point;
                                        globalMousePoint = point;

                                        // TODO: hide the mouse
                                } 

                                enableRawInput = true; // enable it
                        } else if (!globalMousePoint.BeyondExtent(smallerExtend)) { // make sure the virtual mouse is NOT beyond the extent
                                enableRawInput = false;

                                // TODO: show the mouse
                        }

                        try {
                                await Task.Delay(TimeSpan.FromMilliseconds(50));
                        } catch (OperationCanceledException) {
                                break;
                        }
                }
        }

        private static void onRawMouseMovement(Object? sender, RawMouseInputEventArgs args) {
                if (enableRawInput) {
                        globalMousePoint.Add(args.deltaX, args.deltaY);

                        if (globalMousePoint.BeyondExtent(extent)) { // is the mouse beyond the horizontal extent?
                                SetCursorPos(lastMousePoint.X, lastMousePoint.Y);

                                // here we can drop the current held window
                                // and then use SetWindowPos() -- I think this is a method
                                // make sure to use the offset position for this
                        }

                        // if the mosue is beyond the smaller extent we can send the coordinates to the host
                        // we use a smaller extent here so the mouse can seemlessly go between screens
                        if (globalMousePoint.BeyondExtent(smallerExtend)) {
                                WindowManager.sendMouseMovement(new Point(){X = globalMousePoint.X - extent.X, Y = globalMousePoint.Y});

                        }
                }
        }

        // [DllImport("user32.dll", SetLastError = true)]
        // public static extern IntPtr SetWinEventHook(
        //         uint eventMin,
        //         uint eventMax,
        //         IntPtr hmodWinEventProc,
        //         WinEventDelegate lpfnWinEventProc,
        //         uint idProcess,
        //         uint idThread,
        //         uint dwFlags
        // );
        //
        // [DllImport("user32.dll", SetLastError = true)]
        // [return: MarshalAs(UnmanagedType.Bool)]
        // public static extern bool UnhookWinEvent(IntPtr hWinEventHook);
        //
        // public delegate void WinEventDelegate(
        //         IntPtr hWinEventHook,
        //         uint eventType,
        //         IntPtr hwnd,
        //         int idObject,
        //         int idChild,
        //         uint dwEventThread,
        //         uint dwmsEventTime
        // );    


        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
                public int X;
                public int Y;

                public void Add(int x, int y) {
                        this.X += x;
                        this.Y += y;
                }

                // TODO: we should probably return which axis(s) the point is beyond extent in 
                // EX: the point may be beyont the X extent AND the Y extent or the point may be beyond the X axis in the negative direction
                public bool BeyondExtent(Point extent) {
                        return ((this.X >= extent.X - 1) || (this.X <= 0));
                }

                public float Distance(Point point) { return Vector2.DistanceSquared(new Vector2(point.X, point.Y), new Vector2(this.X, this.Y)); }
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point lpPoint); 

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        // calculate maximum screen width
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        private static Point getScreenExtent() {
                int virtualLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
                int virtualWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);

                return new Point(){X = virtualLeft + virtualWidth, Y = 1080}; // Assume the max height extent is 1080 (TODO: change this)
        }

        
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
}

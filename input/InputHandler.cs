using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using SIPSorcery.Net;
using static WebVirtualDisplayClient.RawInputHandler;

namespace WebVirtualDisplayClient;

class InputHandler : BackgroundService
{
        private static Point lastMousePoint = new Point(){X = 0, Y = 0};
        private static Point globalMousePoint = new Point(){X = 0, Y = 0};

        private static bool enableRawInput = false;
        private static Point extent = getScreenExtent(); // capture the max extent of the screen

        private static RTCDataChannel? dataChannel;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
                RawInputHandler.rawMouseMovement += onRawMouseMovement;

                dataChannel = await WebRTCClient.createDataChannel();

                dataChannel.onopen += () => {
                        dataChannel.send("jhello world");
                };

                while (!stoppingToken.IsCancellationRequested) {
                        GetCursorPos(out Point point);

                        if (point.BeyondExtent(extent)) { // is the mouse beyond the horizontal extent?
                                if (!enableRawInput) { // raw input is not already enabled
                                        lastMousePoint = point;
                                        globalMousePoint = point;

                                        // TODO: hide the mouse
                                } 

                                enableRawInput = true; // enable it
                        } else if (!globalMousePoint.BeyondExtent(extent)) { // make sure the virtual mouse is NOT beyond the extent
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
                        globalMousePoint.Add(args.deltaX, args.deltaX);

                        if (globalMousePoint.BeyondExtent(extent)) { // is the mouse beyond the horizontal extent?
                                SetCursorPos(lastMousePoint.X, lastMousePoint.Y);
                        }

                        dataChannel?.send($"{globalMousePoint.X - extent.X} {globalMousePoint.Y}");

                        // TODO: work on Y and left
                        // Console.WriteLine($"raw mouse moved and is now at {globalMousePoint.X - extent.X} {globalMousePoint.Y}");
                }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
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

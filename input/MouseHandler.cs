using Microsoft.Extensions.Hosting;
using WebVirtualDisplayClient.util;
using static WebVirtualDisplayClient.input.RawMouseHandler;
using static WebVirtualDisplayClient.util.MouseUtil;

namespace WebVirtualDisplayClient.input;

class MouseHandler : BackgroundService
{
        private static Point lastMousePoint = new Point(){X = 0, Y = 0};
        private static Point globalMousePoint = new Point(){X = 0, Y = 0};

        private static bool enableRawInput = false;

        private static Point extent = ScreenExtent.getScreenExtent(); // capture the max extent of the screen
        private static Point smallerExtend = new Point(){X = extent.X - 120, Y = extent.Y - 120}; // declare a smaller extent

        public static EventHandler<Point>? onMouseMoveGlobal;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
                RawMouseHandler.rawMouseMovement += onRawMouseMovement;

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
                                onMouseMoveGlobal?.Invoke(null, point);

                                enableRawInput = false;

                                // TODO: show the mouse
                        } else {
                                onMouseMoveGlobal?.Invoke(null, point);
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

                        // if the mouse is beyond the smaller extent we can send the coordinates to the host
                        // we use a smaller extent here so the mouse can seemlessly go between screens
                        if (globalMousePoint.BeyondExtent(smallerExtend)) {
                                WindowManager.sendMouseMovement(new Point(){X = globalMousePoint.X - extent.X, Y = globalMousePoint.Y});

                                onMouseMoveGlobal?.Invoke(null, globalMousePoint);
                        }
                }
        }
}

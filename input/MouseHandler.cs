using Microsoft.Extensions.Hosting;
using WebVirtualDisplayClient.util;
using static WebVirtualDisplayClient.input.RawMouseHandler;
using static WebVirtualDisplayClient.util.MouseUtil;

namespace WebVirtualDisplayClient.input;

class MouseHandler : BackgroundService
{
        private static Point lastMousePoint = new Point(){X = 0, Y = 0};
        private static Point _globalMousePoint = new Point(){X = 0, Y = 0};

        // encapsulate global mouse point
        public static Point GlobalMousePoint { get { return _globalMousePoint; } }

        private static bool enableRawInput = false;

        private static Point extent = ScreenExtent.GetScreenExtent(); // capture the max extent of the screen
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
                                        _globalMousePoint = point;

                                        // TODO: hide the mouse
                                } 

                                enableRawInput = true; // enable it
                        } else if (!_globalMousePoint.BeyondExtent(smallerExtend)) { // make sure the virtual mouse is NOT beyond the extent
                                onMouseMoveGlobal?.Invoke(null, point);

                                enableRawInput = false;

                                // TODO: show the mouse
                        } else {
                                onMouseMoveGlobal?.Invoke(null, point);
                        }

                        try {
                                await Task.Delay(TimeSpan.FromMilliseconds(5));
                        } catch (OperationCanceledException) {
                                break;
                        }
                }
        }

        private static void onRawMouseMovement(Object? sender, RawMouseInputEventArgs args) {
                if (enableRawInput) {
                        _globalMousePoint.Add(args.deltaX, args.deltaY);

                        if (_globalMousePoint.BeyondExtent(extent)) { // is the mouse beyond the horizontal extent?
                                SetCursorPos(lastMousePoint.X, lastMousePoint.Y);
                        }

                        // if the mouse is beyond the smaller extent we can send the coordinates to the host
                        // we use a smaller extent here so the mouse can seemlessly go between screens
                        if (_globalMousePoint.BeyondExtent(smallerExtend)) {
                                WindowManager.sendMouseMovement(new Point(){X = _globalMousePoint.X - extent.X, Y = _globalMousePoint.Y});

                                onMouseMoveGlobal?.Invoke(null, _globalMousePoint);
                        }
                }
        }
}

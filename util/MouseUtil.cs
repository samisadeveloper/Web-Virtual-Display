using System.Numerics;
using System.Runtime.InteropServices;

namespace WebVirtualDisplayClient.util;

class MouseUtil {
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
        public static extern bool GetCursorPos(out Point lpPoint); 

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);
}

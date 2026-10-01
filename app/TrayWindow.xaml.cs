using System.Windows;

namespace WebVirtualDisplayClient.app;

public partial class TrayWindow : Window {
        public void generateCode() {
                // TODO: install QR CODER
                // use it to get a byte array
                // put the byte array in an image
                // done!
                //
                // https://dev.to/auyeungdavid_2847435260/generate-qr-codes-for-free-in-c-a-step-by-step-guide-4h9d
        }

        public void PositionNearTray() {
                var wa = SystemParameters.WorkArea;
                var screen = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
                const double margin = 12;

                if (wa.Bottom < screen.Bottom)        // taskbar at bottom
                {
                        Left = wa.Right - Width - margin;
                        Top = wa.Bottom - Height - margin;
                }
                else if (wa.Top > screen.Top)         // taskbar at top
                {
                        Left = wa.Right - Width - margin;
                        Top = wa.Top + margin;
                }
                else if (wa.Left > screen.Left)       // taskbar at left
                {
                        Left = wa.Left + margin;
                        Top = wa.Bottom - Height - margin;
                }
                else                                  // taskbar at right (or auto-hide)
                {
                        Left = wa.Right - Width - margin;
                        Top = wa.Bottom - Height - margin;
                }
        }
}

using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Media.Imaging;
using QRCoder;

namespace WebVirtualDisplayClient.app;

public partial class TrayWindow : Window {
        private String ipAddress = "";

        protected override void OnSourceInitialized(EventArgs e)
        {
                base.OnSourceInitialized(e);

                ipAddress = GetSystemIpAddress();

                LoadQRCode(ipAddress);
                IpAddressText.Text = $"{ipAddress}:5000";

                ClipboardButton.Click += (sender, args) => {
                        Clipboard.SetText($"http://{ipAddress}:5000");
                };
        }

        private string GetSystemIpAddress() {
                String hostname = Dns.GetHostName();

                IPAddress[] addresses = Dns.GetHostEntry(hostname).AddressList;

                foreach (var ip in addresses)
                {
                        if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip)) {
                                return ip.ToString();
                        }
                }

                throw new Exception("IPV4 Address not found!");
        }

        private BitmapImage BitmapToImage(Bitmap bitmap) {
                using (MemoryStream memory = new MemoryStream()) {

                        bitmap.Save(memory, ImageFormat.Png); // first save to a memory stream
                        memory.Position = 0; // is this even required?

                        // initialize a new bitmap image
                        BitmapImage bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();

                        // set stream source to memory stream
                        bitmapImage.StreamSource = memory;

                        // deinitialize bitmap image
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();

                        return bitmapImage;
                }
        }

        private void LoadQRCode(String ipAddress) {
                String inputString = $"http://{ipAddress}:5000";

                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData data = qrGenerator.CreateQrCode(inputString, QRCodeGenerator.ECCLevel.Default);
                QRCode qrCode = new QRCode(data);

                Bitmap bitmap = qrCode.GetGraphic(20);

                BitmapImage image = BitmapToImage(bitmap);

                QRCodeImage.Source = image;
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

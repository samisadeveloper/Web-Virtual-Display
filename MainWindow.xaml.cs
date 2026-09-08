using System.Windows;
using System.Windows.Interop;
using WebVirtualDisplayClient.input;

namespace WebVirtualDisplayClient {
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            HwndSource? source = PresentationSource.FromVisual(this) as HwndSource;

            if (source == null) throw new NullReferenceException("Could not find a reference for Main Window Hwnd source!");

            RawMouseHandler.InitializeRawInput(source);
        }
    }
}

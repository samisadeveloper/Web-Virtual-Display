using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

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

            RawInputHandler.InitializeRawInput(source);
        }
    }
}

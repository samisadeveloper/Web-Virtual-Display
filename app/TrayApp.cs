using System.Windows;
using System.Windows.Controls;
using SIPSorcery.Net;
using Wpf.Ui.Tray;

namespace WebVirtualDisplayClient.app;

public class TrayApp {
        MenuItem? statusItem;

        public void Initialize(Window parent) {
                RTCPeerConnection peerConnection = WebRTCClient.getPeerConnection();

                peerConnection.OnStarted += () => {
                        if (statusItem == null) return;

                        // this needs to run on the main thread
                        App.Current.Dispatcher.BeginInvoke(() => {
                                statusItem.Header = "Status: Connected";
                                statusItem.IsEnabled = true;
                        });
                };
                
                ContextMenu contextMenu = new ContextMenu();

                statusItem = new MenuItem { Header = "Status: Disconnected", IsEnabled = false };
                contextMenu.Items.Add(statusItem);
                contextMenu.Items.Add(new Separator());

                MenuItem clientsItem = new MenuItem { Header = "View Clients" };

                contextMenu.Items.Add(clientsItem);

                clientsItem.Click += (Object? sender, RoutedEventArgs args) => {
                        App.clientWindow?.Show();
                };

                MenuItem exitItem = new MenuItem { Header = "Exit" };

                exitItem.Click += (Object? sender, RoutedEventArgs args) => {
                        App.Current.Shutdown();
                };

                contextMenu.Items.Add(exitItem);

                NotifyIconService service = new NotifyIconService();

                service.TooltipText = "Web Virtual Display";

                service.ContextMenu = contextMenu;
                service.SetParentWindow(parent);

                service.Register();

        }
}

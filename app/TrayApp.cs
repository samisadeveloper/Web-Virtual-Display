using System.Windows;
using System.Windows.Controls;
using SIPSorcery.Net;
using Wpf.Ui.Tray;

namespace WebVirtualDisplayClient.app;

public class TrayApp {
        MenuItem? statusItem;

        public async void Initialize(TrayWindow trayWindow) {
                Task PeerConnected(RTCPeerConnection peerConnection) {
                        peerConnection.OnStarted += () => {
                                if (statusItem == null) return;

                                App.Current.Dispatcher.BeginInvoke(() => {
                                                statusItem.Header = "Status: Connected";
                                                statusItem.IsEnabled = true;

                                                trayWindow.ConnectionStatus.Text = "Connected";
                                                });
                        };

                        peerConnection.OnClosed += () => {
                                if (statusItem == null) return;

                                App.Current.Dispatcher.BeginInvoke(() => {
                                                statusItem.Header = "Status: Disconnected";
                                                statusItem.IsEnabled = false;

                                                trayWindow.ConnectionStatus.Text = "Disconnected";
                                                });
                        };

                        return Task.CompletedTask;
                };

                await WebRTCClient.OnConnection(PeerConnected);

                ContextMenu contextMenu = new ContextMenu();

                statusItem = new MenuItem { Header = "Status: Disconnected", IsEnabled = false };
                contextMenu.Items.Add(statusItem);
                contextMenu.Items.Add(new Separator());

                MenuItem exitItem = new MenuItem { Header = "Exit" };

                exitItem.Click += (Object? sender, RoutedEventArgs args) => {
                        App.Current.Shutdown();
                };

                contextMenu.Items.Add(exitItem);

                NotifyIconService service = new NotifyIconService();

                service.TooltipText = "Web Virtual Display";

                service.ContextMenu = contextMenu;
                service.SetParentWindow(trayWindow);

                service.Register();

        }
}

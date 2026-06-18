using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using UdpSimulator.ViewModels;

namespace UdpSimulator
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Closed += (_, _) => (DataContext as MainViewModel)?.Dispose();
            if (DataContext is MainViewModel vm)
                vm.PacketLog.CollectionChanged += OnPacketLogChanged;
        }

        private void OnPacketLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm?.IsAutoScrollEnabled != true) return;
            if (PacketListView.Items.Count > 0)
                PacketListView.ScrollIntoView(PacketListView.Items[PacketListView.Items.Count - 1]);
        }
    }
}

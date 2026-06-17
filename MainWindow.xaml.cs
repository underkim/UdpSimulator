using System.Windows;
using UdpSimulator.ViewModels;

namespace UdpSimulator
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Closed += (_, _) => (DataContext as MainViewModel)?.Dispose();
        }
    }
}

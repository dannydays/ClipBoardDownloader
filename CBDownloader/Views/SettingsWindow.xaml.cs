using System.ComponentModel;
using System.Windows;
using CBDownloader.ViewModels;

namespace CBDownloader.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = new SettingsViewModel();
        }
    }
}

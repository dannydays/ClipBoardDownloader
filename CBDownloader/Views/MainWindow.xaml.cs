using System.Windows;

namespace CBDownloader.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is CBDownloader.ViewModels.MainViewModel viewModel)
            {
                viewModel.CancelDownload();
            }
            e.Cancel = true;
            this.Hide();
        }
    }
}

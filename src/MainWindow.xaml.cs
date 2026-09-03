using System.Windows;
using GamebuinoAKA.IDE.ViewModels;

namespace GamebuinoAKA.IDE
{
    
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}

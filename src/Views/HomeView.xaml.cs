using System.Windows.Controls;
using GamebuinoAKA.IDE.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GamebuinoAKA.IDE.Views
{
    
    public partial class HomeView : UserControl
    {
        public HomeView(HomeViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}

using System.Windows.Controls;
using GamebuinoAKA.IDE.ViewModels;

namespace GamebuinoAKA.IDE.Views
{
    
    public partial class SettingsView : UserControl
    {
        public SettingsView(SettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}

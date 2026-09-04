using System.Windows.Controls;
using GamebuinoAKA.IDE.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GamebuinoAKA.IDE.Views
{
    public partial class SoundBankView : UserControl
    {
        public SoundBankView()
        {
            InitializeComponent();
            DataContext = App.Services.GetRequiredService<SoundBankViewModel>();
        }
    }
}

using System.Windows.Controls;
using GamebuinoAKA.IDE.ViewModels;

namespace GamebuinoAKA.IDE.Views
{
    
    public partial class NewProjectView : UserControl
    {
        public NewProjectView(NewProjectViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}

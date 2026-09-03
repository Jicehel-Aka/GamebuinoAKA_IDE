using System.Windows.Controls;
using GamebuinoAKA.IDE.ViewModels;

namespace GamebuinoAKA.IDE.Views
{
    
    public partial class ProjectsView : UserControl
    {
        public ProjectsView(ProjectsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}

using System.Windows.Controls;
using GamebuinoAKA.IDE.ViewModels;

namespace GamebuinoAKA.IDE.Views
{
    
    public partial class SpriteEditorView : UserControl
    {
        public SpriteEditorView(SpriteEditorViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}

using System.Windows.Controls;
using GamebuinoAKA.IDE.ViewModels;

namespace GamebuinoAKA.IDE.Views
{
    
    public partial class TilemapEditorView : UserControl
    {
        public TilemapEditorView(TilemapEditorViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}

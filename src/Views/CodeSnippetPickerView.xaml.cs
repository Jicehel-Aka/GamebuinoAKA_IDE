using System.Windows.Controls;
using GamebuinoAKA.IDE.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GamebuinoAKA.IDE.Views
{
    public partial class CodeSnippetPickerView : UserControl
    {
        public CodeSnippetPickerView()
        {
            InitializeComponent();
            DataContext = App.Services.GetRequiredService<CodeSnippetPickerViewModel>();
        }
    }
}

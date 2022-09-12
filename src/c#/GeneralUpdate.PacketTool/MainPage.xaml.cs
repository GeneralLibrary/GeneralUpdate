using GeneralUpdate.Infrastructure.DataServices.Pick;
using GeneralUpdate.Infrastructure.MVVM;
using GeneralUpdate.PacketTool.ViewModels;

namespace GeneralUpdate.PacketTool
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
using GeneralUpdate.PacketTool.ViewModels;

namespace GeneralUpdate.PacketTool.Views;

public partial class DifferentPage : ContentPage
{
	public DifferentPage(DifferentViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
using GeneralUpdate.Infrastructure.DataServices.Pick;
using GeneralUpdate.PacketTool.ViewModels;
using GeneralUpdate.PacketTool.Views;

namespace GeneralUpdate.PacketTool
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .RegisterViewModels()
                .RegisterView()
                .RegisterAppServices().RegisterOther()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });
            Routing.RegisterRoute("DifferentPage", typeof(DifferentPage));
            return builder.Build();
        }

        public static MauiAppBuilder RegisterOther(this MauiAppBuilder mauiAppBuilder)
        {
            mauiAppBuilder.Services.AddTransient<App>();
            return mauiAppBuilder;
        }

        public static MauiAppBuilder RegisterView(this MauiAppBuilder mauiAppBuilder)
        {
            mauiAppBuilder.Services.AddTransient<MainPage>();
            mauiAppBuilder.Services.AddTransient<DifferentPage>();
            return mauiAppBuilder;
        }

        public static MauiAppBuilder RegisterViewModels(this MauiAppBuilder mauiAppBuilder)
        {
            mauiAppBuilder.Services.AddTransient<MainViewModel>();
            mauiAppBuilder.Services.AddTransient<DifferentViewModel>();
            return mauiAppBuilder;
        }

        public static MauiAppBuilder RegisterAppServices(this MauiAppBuilder mauiAppBuilder)
        {
#if WINDOWS
		mauiAppBuilder.Services.AddTransient<IFolderPickerService, Platforms.Windows.FolderPicker>();
#elif MACCATALYST
		mauiAppBuilder.Services.AddTransient<IFolderPickerService, Platforms.MacCatalyst.FolderPicker>();
#endif
            return mauiAppBuilder;
        }
    }
}
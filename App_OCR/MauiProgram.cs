using Microsoft.Extensions.Logging;

namespace App_OCR
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("BeVietnamPro-Regular.ttf", "BeVietnamRegular");
                    fonts.AddFont("BeVietnamPro-Medium.ttf", "BeVietnamMedium");
                    fonts.AddFont("BeVietnamPro-SemiBold.ttf", "BeVietnamSemibold");
                    fonts.AddFont("BeVietnamPro-Bold.ttf", "BeVietnamBold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
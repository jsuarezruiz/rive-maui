using SkiaSharp.Views.Maui.Controls.Hosting;

namespace RiveSharp.Views
{
    public static class AppBuilderExtensions
    {
        public static MauiAppBuilder UseRiveSharp(this MauiAppBuilder builder)
        {
            builder.UseSkiaSharp();

            return builder;
        }
    }
}

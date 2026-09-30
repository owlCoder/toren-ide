using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Toren.UnitTests.Ui.UiTestApplication))]

namespace Toren.UnitTests.Ui;

public static class UiTestApplication
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Toren.App.App>()
        .UseSkia()
        .WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

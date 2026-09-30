using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using NUnit.Framework;
using Toren.App.Settings.ViewModels;
using Toren.App.ViewModels;
using Toren.App.Views.Settings;
using Toren.App.Views;

namespace Toren.UnitTests.Ui;

[TestFixture]
public sealed class ThemeSynchronizationTests
{
    [AvaloniaTest]
    public void StatusBarThemeToggleUpdatesSettingsWithoutRevertingOnEditorChanges()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        var profile = Path.Combine(TestContext.CurrentContext.WorkDirectory, "TestResults", $"theme-{Guid.NewGuid():N}");
        var shell = Toren.App.App.CreateMainWindow(profile);
        ToolDialog? dialog = null;
        using var opened = Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) =>
        {
            if (window is ToolDialog tool && window.Title == "Settings") dialog = tool;
        });
        shell.FindControl<Button>("SettingsActivityButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        try
        {
            Assert.That(dialog, Is.Not.Null);
            var settings = (ApplicationSettingsViewModel)dialog!.GetLogicalDescendants()
                .OfType<ApplicationSettingsPanel>().Single().DataContext!;
            var toggle = shell.FindControl<Button>("ThemeToggleButton")!;
            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.That(Application.Current.RequestedThemeVariant, Is.EqualTo(ThemeVariant.Light));
            Assert.That(settings.ThemeIndex, Is.EqualTo(1));

            settings.EditorFontSize = 15;
            Assert.That(Application.Current.RequestedThemeVariant, Is.EqualTo(ThemeVariant.Light));

            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.That(Application.Current.RequestedThemeVariant, Is.EqualTo(ThemeVariant.Dark));
            Assert.That(settings.ThemeIndex, Is.Zero);
        }
        finally
        {
            dialog?.Close();
            ((MainWindowViewModel)shell.DataContext!).Dispose();
        }
    }
}

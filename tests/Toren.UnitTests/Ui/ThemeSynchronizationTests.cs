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
        try
        {
            var settings = (ApplicationSettingsViewModel)shell.GetLogicalDescendants()
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
            ((MainWindowViewModel)shell.DataContext!).Dispose();
        }
    }
}

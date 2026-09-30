using Avalonia.Input;
using NUnit.Framework;
using Toren.App.Settings.Services;

namespace Toren.UnitTests.Settings;

[TestFixture]
public sealed class ApplicationKeybindingMatcherTests
{
    [TestCase("primary+s", Key.S, KeyModifiers.Control, true)]
    [TestCase("primary+s", Key.S, KeyModifiers.Meta, true)]
    [TestCase("primary+s", Key.S, KeyModifiers.Control | KeyModifiers.Shift, false)]
    [TestCase("primary+shift+s", Key.S, KeyModifiers.Control | KeyModifiers.Shift, true)]
    [TestCase("alt+s", Key.S, KeyModifiers.Alt, true)]
    [TestCase("primary+alt+s", Key.S, KeyModifiers.Meta | KeyModifiers.Alt, true)]
    [TestCase("primary+alt+shift+s", Key.S, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift, true)]
    [TestCase("f12", Key.F12, KeyModifiers.None, true)]
    [TestCase("f12", Key.F12, KeyModifiers.Control, false)]
    public void MatchesSupportedGestures(
        string gesture,
        Key key,
        KeyModifiers modifiers,
        bool expected)
    {
        Assert.That(ApplicationKeybindingMatcher.Matches(gesture, key, modifiers), Is.EqualTo(expected));
    }
}

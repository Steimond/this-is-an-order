using NUnit.Framework;
using Order.Application;
using Order.Content;
using Order.Contracts;

namespace Order.Tests.EditMode
{
    public sealed class MapGridTests
    {
        [Test] public void SquareCentreAndSubSquareFollowCommandsSpec()
        {
            Assert.That(MapGrid.Name(4, 60, 64), Is.EqualTo("A-1-5"), "Rows count from the top (high Z).");
            Assert.That(MapGrid.TryFind("иди в d-2-9", 64, out var x, out var z, out var name), Is.True);
            Assert.That(name, Is.EqualTo("D-2-9"));
            Assert.That(x, Is.EqualTo(24 + 2.5f * 8 / 3).Within(.001));
            Assert.That(z, Is.EqualTo(56 - 2.5f * 8 / 3).Within(.001));
            Assert.That(MapGrid.TryFind("d2", 64, out x, out z, out name), Is.True);
            Assert.That(name, Is.EqualTo("D-2")); Assert.That(x, Is.EqualTo(28)); Assert.That(z, Is.EqualTo(52));
        }

        [Test] public void EveryCoordinateRoundTrips()
        {
            for (var column = 0; column < 8; column++)
                for (var row = 1; row <= 8; row++)
                    for (var sub = 1; sub <= 9; sub++)
                    {
                        var text = $"{(char)('a' + column)}-{row}-{sub}";
                        Assert.That(MapGrid.TryFind(text, 64, out var x, out var z, out var name), Is.True, text);
                        Assert.That(MapGrid.Name(x, z, 64), Is.EqualTo(name), text);
                    }
        }

        [TestCase("i-2")] [TestCase("a-9")] [TestCase("без координат")]
        public void InvalidCoordinatesAreNotFound(string text) => Assert.That(MapGrid.TryFind(text, 64, out _, out _, out _), Is.False);

        private sealed class Store : ISettingsStore
        {
            public SettingsSnapshot Value = SettingsSnapshot.Default;
            public SettingsSnapshot Load() => Value;
            public void Save(SettingsSnapshot value) => Value = value;
        }

        [Test] public void TestMapIsAScreenSettingsReturnTo()
        {
            var app = new AppController(new Store());
            AppRequest? requested = null;
            app.Requested += r => requested = r;
            CommandResult Run(AppCommand command) => app.Execute(command, new CommandContext(app.Session, CommandSource.Manual));
            Assert.That(AppScreen.TestMap.IsSettings(), Is.False);
            Run(AppCommand.LaunchTestMap());
            Assert.That(app.Screen, Is.EqualTo(AppScreen.TestMap)); Assert.That(requested, Is.EqualTo(AppRequest.LaunchTestMap));
            Assert.That(Run(AppCommand.Back()).Applied, Is.False, "Back does not leave the match by accident.");
            Run(AppCommand.Navigate(AppScreen.GraphicsSettings));
            Run(AppCommand.Back());
            Assert.That(app.Screen, Is.EqualTo(AppScreen.TestMap), "Settings opened from the match return to it.");
            Assert.That(Run(AppCommand.LaunchTestMap()).Applied, Is.False);
            Run(AppCommand.Navigate(AppScreen.MainMenu));
            Assert.That(app.Screen, Is.EqualTo(AppScreen.MainMenu));
            requested = null;
            Run(AppCommand.Navigate(AppScreen.TestMap));
            Assert.That(requested, Is.EqualTo(AppRequest.LaunchTestMap));
        }
    }
}

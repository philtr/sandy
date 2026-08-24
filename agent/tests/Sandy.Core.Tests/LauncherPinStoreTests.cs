using Sandy.Core.Launcher;

namespace Sandy.Core.Tests;

public sealed class LauncherPinStoreTests
{
    [Fact]
    public async Task Round_trips_explicit_manual_pins()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sandy-pins-{Guid.NewGuid():N}");
        try
        {
            var store = new LauncherPinStore(Path.Combine(directory, "pins.json"));
            var pins = new[]
            {
                new LauncherPin(Guid.NewGuid(), "Steam game", LauncherPinKind.Uri, "steam://rungameid/123"),
                new LauncherPin(Guid.NewGuid(), "Game", LauncherPinKind.Executable, @"C:\Games\Game.exe")
            };

            await store.SaveAsync(pins);

            Assert.Equal(pins, await store.LoadAsync());
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/plain,hello")]
    [InlineData("not-a-uri")]
    public void Rejects_unsafe_or_relative_uris(string target)
    {
        var pin = new LauncherPin(Guid.NewGuid(), "Bad", LauncherPinKind.Uri, target);
        Assert.Throws<InvalidDataException>(pin.Validate);
    }
}

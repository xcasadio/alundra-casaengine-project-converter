using AlundraCasaEngineProjectConverter.Readers;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// MapLocation is the single authority on where a converted map's files go, so the target layout is
/// locked here rather than in each writer's own test: everything a map owns lives in one folder,
/// Maps/{Zone}/{Name}-{id}/, with the tilemap assets, the dialogue table and the event bytecode in
/// lower-case subfolders and the .world at the root of that folder.
/// </summary>
public class MapLocationTests
{
    private static readonly MapLocation Location = new("The Klark", "Ship Klark (beginning)-389");

    [Fact]
    public void EveryFileOfAMapLivesUnderThatMapsOwnFolder()
    {
        Assert.Equal(Combine("Maps", "The Klark", "Ship Klark (beginning)-389"), Location.MapFolder);

        Assert.Equal(Combine(Location.MapFolder, "tilemap"), Location.TileMapDirectory);
        Assert.Equal(Combine(Location.MapFolder, "dialogues"), Location.DialoguesDirectory);
        Assert.Equal(Combine(Location.MapFolder, "events"), Location.EventsDirectory);

        Assert.Equal(
            Combine(Location.MapFolder, "tilemap", "Ship Klark (beginning)-389.tileMap"),
            Location.TileMapRelativePath);
        Assert.Equal(
            Combine(Location.MapFolder, "tilemap", "Ship Klark (beginning)-389.tmj"),
            Location.TiledMapRelativePath);
        Assert.Equal(
            Combine(Location.MapFolder, "dialogues", "Ship Klark (beginning)-389.strings.json"),
            Location.StringsRelativePath);
        Assert.Equal(
            Combine(Location.MapFolder, "events", "Ship Klark (beginning)-389.events.json"),
            Location.EventsRelativePath);

        // The world sits at the root of the map folder, not in a subfolder of its own.
        Assert.Equal(
            Combine(Location.MapFolder, "Ship Klark (beginning)-389.world"),
            Location.WorldRelativePath);
    }

    [Fact]
    public void EveryPathIsRelativeAndStartsAtTheMapsRootFolder()
    {
        var paths = new[]
        {
            Location.MapFolder,
            Location.TileMapDirectory,
            Location.DialoguesDirectory,
            Location.EventsDirectory,
            Location.TileMapRelativePath,
            Location.TiledMapRelativePath,
            Location.StringsRelativePath,
            Location.EventsRelativePath,
            Location.WorldRelativePath,
        };

        foreach (var path in paths)
        {
            Assert.False(Path.IsPathRooted(path));
            Assert.StartsWith(MapLocation.MapsRootFolder + Path.DirectorySeparatorChar, path, StringComparison.Ordinal);
        }
    }

    private static string Combine(params string[] parts) => Path.Combine(parts);
}

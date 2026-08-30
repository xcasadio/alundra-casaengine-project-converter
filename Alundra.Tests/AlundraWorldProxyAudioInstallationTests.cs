using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Audio;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// The end-to-end recette point of docs/plan-e11-audio.md, slice E11.a's own acceptance (§4): on the
/// REAL install path (<see cref="AlundraWorldProxy.InstallAudioSystems"/>) for world
/// "Ship Klark (beginning)-389", the four intro sound ids (300/301/302/61) resolve to their tone files -
/// and the same wiring fails (no <see cref="AlundraWorldProxy.SoundPlayer"/> at all) when there is no
/// <c>Game</c>/<c>AudioSystemComponent</c> to install against, i.e. the bank is never reachable through
/// this seam without installation.
///
/// A live <see cref="AudioSystemComponent"/> needs a real MonoGame <c>Game</c>/<c>GraphicsDevice</c> to
/// construct normally - unavailable in this headless test process - so, like
/// <see cref="HeroWorldFixture.BuildWorld"/> already does for <c>World.Game</c> itself, both the
/// <c>Game</c> and its <see cref="AudioSystemComponent"/> are built via
/// <see cref="RuntimeHelpers.GetUninitializedObject"/> plus direct backing-field writes: this is still
/// the REAL <see cref="AlundraWorldProxy.InstallAudioSystems"/>/<see cref="AudioService"/>/
/// <see cref="AlundraSoundPlayer"/> production code, only the otherwise-unconstructible MonoGame shell
/// around it is faked.
/// </summary>
/// docs/plan-e11c-musique.md, slice C1: <see cref="InstallAudioSystems"/> now also re-points the
/// SESSION-scoped <see cref="AlundraMusicPlayer.Instance"/> singleton (D-C-6) - so this class shares
/// the <see cref="AlundraMusicPlayerSingletonCollection"/> xunit collection with
/// <see cref="AlundraMusicPlayerTests"/>, the only other class touching that same shared instance,
/// keeping them from racing (xunit runs different test CLASSES in parallel by default).
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public class AlundraWorldProxyAudioInstallationTests
{
    private const string WorldName = "Ship Klark (beginning)-389";

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Sounds")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"AlundraWorldProxyAudioInstallationTests: no 'alundra-project/Sounds' directory found above "
            + $"'{AppContext.BaseDirectory}' (docs/plan-e11-audio.md, slice E11.a).");
    }

    private static CasaEngineGame BuildGameWithAudio(FakeAudioBackend backend, FakeAudioClipProvider provider)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));

        var componentsField = typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());

        // AudioSystemComponent's real constructor needs a live AssetContentManager (for its
        // AssetContentManagerAudioClipProvider) that this headless game never has - so the component
        // itself is built uninitialized too, with its own real AudioService wired directly onto its
        // backing field. Everything downstream of THAT (AlundraWorldProxy.InstallAudioSystems,
        // AlundraSoundPlayer, AlundraSoundBank) runs unmodified production code.
        var audioComponent = (AudioSystemComponent)RuntimeHelpers.GetUninitializedObject(typeof(AudioSystemComponent));
        var serviceField = typeof(AudioSystemComponent).GetField("<Service>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        serviceField.SetValue(audioComponent, new AudioService(backend) { ClipProvider = provider });

        var audioComponentField = typeof(CasaEngineGame).GetField("<AudioSystemComponent>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        audioComponentField.SetValue(game, audioComponent);

        return game;
    }

    [Fact]
    public void InstallAudioSystems_RealGame_WiresARealSoundPlayer_ResolvingAllFourIntroSoundsToToneFiles()
    {
        var projectRoot = FindProjectRoot();
        var world = new World();
        var game = BuildGameWithAudio(new FakeAudioBackend(), new FakeAudioClipProvider());
        HeroWorldFixture.SetProperty(world, nameof(World.Game), game);

        var proxy = new AlundraWorldProxy { SoundBank = new AlundraSoundBank(projectRoot) };
        proxy.InstallAudioSystems(world);

        Assert.NotNull(proxy.SoundPlayer);
        Assert.IsType<AlundraSoundPlayer>(proxy.SoundPlayer);

        // The exact seam AlundraSoundPlayer itself resolves through - all four intro ids (§1.1) must be
        // playable on the real install path.
        foreach (var (sfxId, expectedFirstToneFile) in new[]
                 {
                     (300, "sfx_0300.wav"),
                     (301, "sfx_0301.wav"),
                     (302, "sfx_0302_0.wav"),
                     (61, "sfx_0061.wav"),
                 })
        {
            var resolved = proxy.SoundBank.TryResolve(sfxId, soundGroup: null, out var resolution);
            Assert.True(resolved, $"expected sfx id {sfxId} to resolve on the real install path.");
            Assert.Equal(expectedFirstToneFile, resolution.Tones[0].File);
        }
    }

    /// <summary>
    /// Pins the OWNER that the real install path hands to <see cref="AlundraSoundPlayer"/> — the
    /// production decision itself, not just the constructor plumbing.
    ///
    /// <para>Found in main-session verification of slice C1: the ownership test in
    /// <c>AlundraSoundPlayerTests</c> builds the player DIRECTLY with an explicit owner, so it proves
    /// the parameter is honoured but is blind to what <see cref="AlundraWorldProxy.InstallAudioSystems"/>
    /// actually passes. Changing that one call site to any non-world object left the whole suite green
    /// — the "no slice without a test traversing the production call site" rule, unsatisfied.</para>
    ///
    /// <para>What it guards (fact 1.7 of docs/plan-e11c-musique.md): <c>World.Clear</c> stops voices by
    /// <c>ReferenceEquals(entry.Owner, world)</c>, and a fresh <see cref="AlundraSoundPlayer"/> is built
    /// per world — so any owner other than the world leaves sound effects running past their world.</para>
    /// </summary>
    [Fact]
    public void InstallAudioSystems_PlaysSfxOwnedByTheWorldItself_SoClearingTheWorldStopsThem()
    {
        var projectRoot = FindProjectRoot();
        var world = new World { Name = WorldName };
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        var game = BuildGameWithAudio(backend, provider);
        HeroWorldFixture.SetProperty(world, nameof(World.Game), game);

        var proxy = new AlundraWorldProxy { SoundBank = new AlundraSoundBank(projectRoot) };
        proxy.InstallAudioSystems(world);
        Assert.NotNull(proxy.SoundPlayer);

        var service = game.AudioSystemComponent.Service;

        // Register a clip for every tone of sfx 300 so the real player can actually start a voice.
        Assert.True(proxy.SoundBank.TryResolve(300, soundGroup: null, out var resolution));
        foreach (var tone in resolution.Tones)
        {
            provider.Register(tone.AssetId, new FakeAudioClip());
        }

        proxy.SoundPlayer!.PlaySfx(300);
        Assert.True(service.ActiveVoiceCount > 0, "the sfx should have started a voice on the real install path.");

        // The production decision under test: these voices must belong to the WORLD, which is what
        // World.Clear passes when it tears the world down.
        service.StopVoicesOwnedBy(world);

        Assert.Equal(0, service.ActiveVoiceCount);
    }

    [Fact]
    public void InstallAudioSystems_NoGame_LeavesSoundPlayerNull_TheSoundsAreUnreachableWithoutInstallation()
    {
        var world = new World(); // World.Game left null - no Game/AudioSystemComponent to install against.
        var proxy = new AlundraWorldProxy { SoundBank = new AlundraSoundBank(FindProjectRoot()) };

        proxy.InstallAudioSystems(world);

        // The bank itself would still resolve the ids (it's a plain file read) - but PROVING the
        // installation matters: without it, the interpreter's own seam (IEntityWorldContext.SoundPlayer)
        // is null, so 0xBD/0xBE/0x12/0x75 can never reach AlundraSoundPlayer.PlaySfx at all, regardless
        // of what the bank could resolve.
        Assert.Null(proxy.SoundPlayer);
    }
}

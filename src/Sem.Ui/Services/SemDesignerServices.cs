using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Sem.Ui.Services;

/// <summary>
/// The services the designer needs whichever host it is running in.
/// </summary>
/// <remarks>
/// Both hosts registered these, separately and in different words, and the two lists had already
/// begun to disagree: the desktop registered <see cref="SessionHost"/> by its type alone and got its
/// design store and its content-pack answer from whatever the container happened to hold, while the
/// web stated both. That is fine until one of the optional arguments changes meaning, at which point
/// one host silently follows and the other does not.
/// </remarks>
public static class SemDesignerServices
{
    /// <summary>
    /// Registers everything both hosts share, leaving each to say where its data and files come from.
    /// </summary>
    /// <param name="services">The host's container.</param>
    /// <param name="assumeAllPacks">
    /// Whether to open with every content pack enabled. True on the web, where the installation the
    /// data was read from is mine rather than the player's; false on the desktop, where it is theirs.
    /// </param>
    /// <remarks>
    /// <see cref="IGameDataSource"/> and <see cref="IFileExchange"/> are deliberately not here: they
    /// are the whole of what differs between a browser tab and a window with a file system behind it,
    /// and a host that did not register them should fail loudly rather than be given a default.
    /// </remarks>
    public static IServiceCollection AddSemDesigner(this IServiceCollection services, bool assumeAllPacks)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The header can start an empire or open a file from any page, so the question about unsaved
        // work has to be askable from outside the designer that knows how to ask it.
        services.AddScoped<UnsavedWorkGuard>();
        services.AddScoped<EditorState>();

        // How the pickers are drawn, which is a setting rather than the player's work, and is kept
        // apart from it.
        services.AddScoped(s => new Preferences(s.GetRequiredService<IJSRuntime>()));

        // One session for the whole app, so moving between the list and the designer keeps unsaved
        // work. The store is asked for rather than required: the desktop keeps none, because the
        // player's own file is the one that counts and a second copy would be a rival to it.
        services.AddScoped(s => new SessionHost(
            s.GetRequiredService<IGameDataSource>(),
            s.GetRequiredService<IFileExchange>(),
            s.GetService<IDesignStore>(),
            assumeAllPacks,
            s.GetRequiredService<Preferences>()));

        // Keeping that file and the app in step, where the host has a file of its own. Registered
        // for both, because it answers "not here" rather than needing to be absent: a browser's
        // exchange does not save in place and has nothing to watch, so it is never offered.
        services.AddScoped(s => new DesignSync(
            s.GetRequiredService<SessionHost>(),
            s.GetRequiredService<IFileExchange>(),
            s.GetRequiredService<Preferences>()));

        return services;
    }
}

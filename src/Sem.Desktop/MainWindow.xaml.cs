using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.Web.WebView2.Core;
using Sem.Io;
using Sem.Ui.Services;

namespace Sem.Desktop;

/// <summary>
/// The application window: a startup panel while the game files are read, then the designer.
/// </summary>
/// <remarks>
/// The designer itself is the same set of components the web app renders. The difference is where
/// the data comes from, which is this machine's own installation, and where a save goes, which is
/// the player's real designs file rather than a download.
/// </remarks>
public partial class MainWindow : Window
{
    /// <summary>
    /// The hostname the embedded browser is given for the extracted images. Mapping a folder to a
    /// name lets the designer fetch assets exactly as it does on the web, so one implementation
    /// serves both.
    /// </summary>
    private const string AssetHost = "gamedata.sem";

    /// <summary>
    /// Writes only into this app's own cache. Extraction uses this, and it cannot reach the game
    /// or the player's saves even by accident.
    /// </summary>
    private readonly SafeFile _cacheFile = new(WritePolicy.ForApplication().Named("cache"));

    private string? _installRoot;
    private string? _designsPath;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await StartAsync();
    }

    private async Task StartAsync()
    {
        ShowStatus("Looking for your Stellaris installation…", detail: null, busy: true);

        _installRoot ??= StellarisLocator.FindInstallRoot();

        if (_installRoot is null)
        {
            ShowStatus(
                "Stellaris could not be found on this computer.",
                "Choose the folder the game is installed in and this will carry on.",
                busy: false,
                offerChoice: true);
            return;
        }

        _designsPath = FindDesignsFile(_installRoot);

        var cache = new GameDataCache(_installRoot);

        if (!cache.IsUsable(out var reason))
        {
            ShowStatus(
                "Reading your Stellaris installation…",
                reason is null ? null : $"The game data needs building: {reason}. This takes a few seconds.",
                busy: true);

            var progress = new Progress<string>(message => Dispatcher.Invoke(() => DetailText.Text = message));

            try
            {
                await Task.Run(() => cache.Rebuild(_cacheFile, progress));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ShowStatus(
                    "The game data could not be read.",
                    ex.Message,
                    busy: false,
                    offerChoice: true);
                return;
            }
        }

        ShowStatus("Starting the designer…", null, busy: true);
        StartDesigner(cache);
    }

    private void StartDesigner(GameDataCache cache)
    {
        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();

        // The designer fetches its data over HTTP whichever host it runs in. Here that is the
        // local cache, mapped to a hostname the embedded browser can reach.
        services.AddScoped(_ => new HttpClient { BaseAddress = new Uri($"https://{AssetHost}/") });
        services.AddScoped<IGameDataSource>(s =>
            new HttpGameDataSource(s.GetRequiredService<HttpClient>(), baseUrl: string.Empty));

        services.AddScoped<SessionHost>();
        services.AddScoped(_ => CreateFileExchange());

        // Which way round the pickers are drawn is kept here too. The reason this host keeps no
        // copy of the designs — that the player's own file is the one that counts, and a second
        // copy would be a rival to it — says nothing about a setting.
        services.AddScoped(s => new Preferences(s.GetRequiredService<IJSRuntime>()));

        // Asked by the header before it starts an empire or opens a file, answered by the designer.
        services.AddScoped<UnsavedWorkGuard>();

        WebView.Services = services.BuildServiceProvider();
        WebView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(DesktopApp),
        });

        WebView.BlazorWebViewInitialized += (_, e) =>
        {
            e.WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                AssetHost,
                cache.Directory,
                CoreWebView2HostResourceAccessKind.Allow);

            // Nothing here is a web page the user should be able to leave or right-click into.
            e.WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            e.WebView.CoreWebView2.Settings.IsSwipeNavigationEnabled = false;
        };

        StartupPanel.Visibility = Visibility.Collapsed;
        WebView.Visibility = Visibility.Visible;

        Title = _designsPath is { Length: > 0 }
            ? $"Stellaris Empire Manager - {Path.GetFileName(_designsPath)}"
            : "Stellaris Empire Manager";
    }

    /// <summary>
    /// Builds the saving side of the app, granting write access to the one folder the designs file
    /// lives in and nothing else.
    /// </summary>
    /// <remarks>
    /// Least privilege is worth the small ceremony here. Everything else in the app runs under a
    /// policy that cannot touch the player's saves at all, so only a deliberate save can reach
    /// them, and only that one folder.
    /// </remarks>
    private IFileExchange CreateFileExchange()
    {
        if (_designsPath is not { Length: > 0 } path)
        {
            return new UnavailableFileExchange();
        }

        var policy = WritePolicy.ForApplication()
            .Allowing(Path.GetDirectoryName(path)!)
            .Named("application (designs file)");

        return new DesktopFileExchange(new SafeFile(policy), path);
    }

    /// <summary>
    /// Finds the player's designs file, which lives with their saves rather than with the game.
    /// </summary>
    private static string? FindDesignsFile(string installRoot)
    {
        if (StellarisLocator.FindUserDataRoot(installRoot) is not { } userData)
        {
            return null;
        }

        return Path.Combine(userData, Sem.Designs.EmpireDesignsFile.FileName);
    }

    private void ShowStatus(string status, string? detail, bool busy, bool offerChoice = false)
    {
        StatusText.Text = status;
        DetailText.Text = detail ?? string.Empty;
        Progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ChoosePanel.Visibility = offerChoice ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnChooseInstall(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Where is Stellaris installed?",
        };

        if (dialog.ShowDialog(this) == true && StellarisLocator.IsInstallRoot(dialog.FolderName))
        {
            _installRoot = dialog.FolderName;
            _ = StartAsync();
        }
        else if (dialog.FolderName is { Length: > 0 })
        {
            DetailText.Text = "That folder does not look like a Stellaris installation.";
        }
    }

    private void OnRetry(object sender, RoutedEventArgs e) => _ = StartAsync();
}

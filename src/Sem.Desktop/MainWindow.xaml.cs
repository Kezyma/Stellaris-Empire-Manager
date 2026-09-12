using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>How the designer reaches the player's file, kept so the window can ask it things.</summary>
    private IFileExchange? _files;

    /// <summary>Builds the window, and starts reading the game data once it is shown.</summary>
    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await StartAsync();
        Closing += AskBeforeClosing;

        // Before the window has a handle, since that is what it hooks. Nothing to do with the
        // buttons in the header; it is the rest of what the title bar was taking care of.
        WindowControls.KeepMaximisedInsideTheScreen(this);
    }

    /// <summary>Whether a start is already running, so a second one cannot be begun over it.</summary>
    private bool _starting;

    /// <summary>
    /// Refuses to close over work nobody has saved, unless the player says to.
    /// </summary>
    /// <remarks>
    /// The web has beforeunload for this and the desktop had nothing: closing the window threw away
    /// an unsaved empire without a word. Asked natively rather than in the designer, because Closing
    /// is synchronous and cannot wait for a dialog drawn by Blazor.
    /// </remarks>
    private void AskBeforeClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_files is not DesktopFileExchange { HasUnsavedWork: true })
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            "The empire you are editing has changes that have not been saved. Close anyway?",
            "Unsaved changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        e.Cancel = answer != MessageBoxResult.Yes;
    }

    /// <summary>
    /// Finds the game, builds the data if it needs building, and opens the designer.
    /// </summary>
    /// <remarks>
    /// Everything is caught. This runs from an async void handler on Loaded, where an escaping
    /// exception ends the process with a Windows crash dialog, and from Retry and Choose folder as a
    /// discarded task, where an escaping exception is swallowed and the window sits on "Looking for
    /// your Stellaris installation…" for ever. Both of those are reachable: the extraction reads
    /// image formats and throws NotSupportedException at a DDS it does not know, which a modded or
    /// newly patched install can easily have, and only IOException and UnauthorizedAccessException
    /// were being caught.
    ///
    /// Guarded against a second run as well, since Retry is a button the player can press while the
    /// first attempt is still going.
    /// </remarks>
    private async Task StartAsync()
    {
        if (_starting)
        {
            return;
        }

        _starting = true;

        try
        {
            await StartCoreAsync();
        }
        catch (Exception ex)
        {
            ShowStatus(
                "The designer could not be started.",
                $"{ex.GetType().Name}: {ex.Message}",
                busy: false,
                offerChoice: true);
        }
        finally
        {
            _starting = false;
        }
    }

    private async Task StartCoreAsync()
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

#if DEBUG
        // Only in a debug build. Without it there is no way into the embedded browser at all, which
        // is how a host page that never started Blazor went unnoticed: the window simply sat there
        // saying "Starting…" with no console to ask.
        services.AddBlazorWebViewDeveloperTools();
#endif

        // Read off disk rather than fetched: this host extracted the data itself, moments earlier,
        // into a folder it chose. The images are a different matter - those are fetched by the page,
        // so they go through the virtual host mapped below, which is what that mapping is for.
        services.AddScoped<IGameDataSource>(_ =>
            new FileGameDataSource(cache.Directory, $"https://{AssetHost}/assets"));

        _files = CreateFileExchange();
        services.AddScoped(_ => _files);

        // Registered here and nowhere else, which is the whole gate on the window buttons: the
        // header draws them only where something answers this, and in a browser nothing does.
        services.AddScoped<IWindowControls>(_ => new WindowControls(this));

        // No design store, because the player's own file is the one that counts and a second copy
        // would be a rival to it. Only the packs they actually have: this installation is theirs.
        services.AddSemDesigner(assumeAllPacks: false);

        WebView.Services = services.BuildServiceProvider();
        WebView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(DesktopApp),
        });

        // So <PageTitle> reaches the window's own title bar. Without it every page's title was
        // rendered into nothing, and the web host had this and the desktop did not.
        WebView.RootComponents.Add(new RootComponent
        {
            Selector = "head::after",
            ComponentType = typeof(Microsoft.AspNetCore.Components.Web.HeadOutlet),
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

        // The whole layer, so the startup screen's own window buttons go with it: from here on the
        // designer's header draws them, and two sets at once would be one set too many.
        StartupLayer.Visibility = Visibility.Collapsed;
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

    /// <summary>
    /// Moves the window by its startup screen, which has no title bar to be dragged by.
    /// </summary>
    /// <remarks>
    /// Only here. Once the designer is showing, the web view owns the pointer and the drag is begun
    /// from its header instead - see <see cref="WindowControls"/>, which also explains why this call
    /// is the one that works on this side and not on that one.
    /// </remarks>
    private void OnDragWindow(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // DragMove throws outright if the button has already come up, which a fast click can do
        // between the event being raised and this running.
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnMinimise(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnToggleMaximise(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    /// <summary>Closes rather than exits, so the unsaved-work question is still asked.</summary>
    private void OnCloseWindow(object sender, RoutedEventArgs e) => Close();
}

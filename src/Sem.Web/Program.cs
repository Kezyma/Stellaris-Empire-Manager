using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Sem.Ui.Services;
using Sem.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// The extracted game data is fetched from the site itself. The designs the user opens are read in
// this tab and never sent anywhere.
builder.Services.AddScoped<IGameDataSource>(services =>
    new HttpGameDataSource(services.GetRequiredService<HttpClient>()));

builder.Services.AddScoped<IFileExchange, BrowserFileExchange>();

// The header can start an empire or open a file from any page, so the question about unsaved work
// has to be askable from outside the designer that knows how to ask it.
builder.Services.AddScoped<UnsavedWorkGuard>();
builder.Services.AddScoped<EditorState>();

// A tab that is closed should not take an evening's work with it, so the designs are kept in the
// browser between visits.
builder.Services.AddScoped<IDesignStore, BrowserDesignStore>();

// How the pickers are drawn, which is a setting rather than the player's work, and is kept apart
// from it.
builder.Services.AddScoped(services =>
    new Preferences(services.GetRequiredService<IJSRuntime>()));

// One session for the whole app, so moving between the list and the designer keeps unsaved work.
// Every content pack is assumed: the installation the data was read from is not the player's, and a
// designer that hides half the game until a setting is found is worse than one that offers too much.
builder.Services.AddScoped(services => new SessionHost(
    services.GetRequiredService<IGameDataSource>(),
    services.GetRequiredService<IFileExchange>(),
    services.GetRequiredService<IDesignStore>(),
    assumeAllPacks: true,
    services.GetRequiredService<Preferences>()));

await builder.Build().RunAsync();

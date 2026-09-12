using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
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

// Through a router rather than registered outright, because where a save goes stops being fixed
// for the visit the moment a cloud provider can be connected to part-way through one. Nothing
// switches it yet; what it forwards to today is the same browser exchange as before.
builder.Services.AddScoped<BrowserFileExchange>();
builder.Services.AddScoped(s => new FileExchangeRouter(s.GetRequiredService<BrowserFileExchange>()));
builder.Services.AddScoped<IFileExchange>(s => s.GetRequiredService<FileExchangeRouter>());

// A tab that is closed should not take an evening's work with it, so the designs are kept in the
// browser between visits.
builder.Services.AddScoped<IDesignStore, BrowserDesignStore>();

// Every content pack is assumed here: the installation the data was read from is not the player's,
// and a designer that hides half the game until a setting is found is worse than one that offers
// too much.
builder.Services.AddSemDesigner(assumeAllPacks: true);

await builder.Build().RunAsync();

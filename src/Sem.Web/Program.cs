using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Sem.Ui.Services;
using Sem.Ui.Services.Cloud;
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

// OneDrive, for a player who keeps their designs file there - which Windows arranges by itself for
// a great many people by redirecting Documents into it.
//
// The identifier below is public and belongs in this file. It is not a credential: an application
// that runs in somebody else's browser cannot keep one, so what refuses an impostor is the redirect
// allowlist Microsoft holds and the PKCE verifier this tab keeps to itself. docs/cloud-setup.md
// sets out the registration this names, and why that is enough.
builder.Services.AddScoped<ITokenStore, BrowserTokenStore>();

builder.Services.AddScoped(s => new OneDriveAuth(
    // A client of its own, because the site's carries the site's base address and these do not go
    // to the site. Microsoft's endpoints are absolute.
    new HttpClient(),
    s.GetRequiredService<ITokenStore>(),
    clientId: "3275b739-5f92-47b4-9210-3f1def80ec25",

    // Wherever this copy of the app is served from, which is the address registered against it:
    // the published site in production, the dev server locally. Both are on the allowlist.
    redirectUri: builder.HostEnvironment.BaseAddress));

builder.Services.AddScoped(s => new OneDriveProvider(
    new HttpClient(), s.GetRequiredService<OneDriveAuth>()));

builder.Services.AddScoped<ICloudProvider>(s => s.GetRequiredService<OneDriveProvider>());

// Registered last, because it is the piece that knows about all of the others - signing in, the
// file chosen, and the router that decides where Save goes.
builder.Services.AddScoped(s => new CloudConnection(
    s.GetRequiredService<ICloudProvider>(),
    s.GetRequiredService<OneDriveAuth>(),
    s.GetRequiredService<FileExchangeRouter>(),
    s.GetRequiredService<BrowserFileExchange>(),
    s.GetRequiredService<SessionHost>(),
    s.GetRequiredService<Preferences>(),
    s.GetRequiredService<DesignSync>()));

// Every content pack is assumed here: the installation the data was read from is not the player's,
// and a designer that hides half the game until a setting is found is worse than one that offers
// too much.
builder.Services.AddSemDesigner(assumeAllPacks: true);

await builder.Build().RunAsync();

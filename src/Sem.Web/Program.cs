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

builder.Services.AddScoped<IFileExchange, BrowserFileExchange>();

// One session for the whole app, so moving between the list and the designer keeps unsaved work.
builder.Services.AddScoped<SessionHost>();

await builder.Build().RunAsync();

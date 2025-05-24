using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Globalization;
using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddBlazorise(options => { options.Immediate = true; })
    .AddBootstrap5Providers()
    .AddFontAwesomeIcons();

var culture = new CultureInfo("de");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var host = builder.Build();

await host.Services.UseBootstrap5Providers().UseFontAwesomeIcons();
await host.RunAsync();

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Globalization;
using CryptoTracker.Shared;
using CryptoTracker.Client.RestClients;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<IWalletApi, WalletRestClient>();
builder.Services.AddScoped<IFlowApi, FlowRestClient>();
builder.Services.AddScoped<IBalanceApi, BalanceRestClient>();
builder.Services.AddScoped<IDataImportApi, DataImportRestClient>();
builder.Services.AddScoped<IImportEntriesApi, ImportEntriesRestClient>();

builder.Services.AddRadzenComponents();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var culture = new CultureInfo("de");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var host = builder.Build();

await host.RunAsync();

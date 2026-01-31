using Microsoft.EntityFrameworkCore;
using CryptoTracker.Components;
using CryptoTracker.Client.Pages;
using Microsoft.AspNetCore.Components;
using CryptoTracker.Services;
using NoobsMuc.Coinmarketcap.Client;
using CryptoTracker.Shared;
using CryptoTracker.Controllers;
using Radzen;

namespace CryptoTracker
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            services.AddApplicationInsightsTelemetry();
            services.AddControllersWithViews();

            services.AddDbContext<CryptoTrackerDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

            services.AddRazorComponents()
                .AddInteractiveServerComponents();
                //.AddInteractiveWebAssemblyComponents();


            services.AddMemoryCache();
            services.AddSingleton<ICoinmarketcapClient>(sp =>
                new CoinmarketcapClient(Configuration["COINMARKETCAP_API_KEY"]!));
            services.AddHttpClient("CoinMarketCap", client =>
            {
                client.BaseAddress = new Uri("https://pro-api.coinmarketcap.com/");
                var apiKey = Configuration["COINMARKETCAP_API_KEY"];
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    client.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", apiKey);
                }
            });

            services.AddScoped<HttpClient>(sp =>
            {
                var navigationManager = sp.GetRequiredService<NavigationManager>();
                return new HttpClient { BaseAddress = new Uri(navigationManager.BaseUri) };
            });

            services.AddRadzenComponents();

            services.AddScoped<DataImportService>();
            services.AddScoped<ImportAutoService>();
            services.AddScoped<WalletService>();
            services.AddScoped<FlowService>();
            services.AddScoped<IFinanceValueProvider, FinanceValueProvider>();
            services.AddSingleton<CoinMarketCapService>();
            services.AddScoped<CoinRateService>();
            services.AddScoped<BalanceService>();
            services.AddScoped<OverviewService>();
            services.AddScoped<TransactionService>();
            services.AddScoped<LotService>();

            services.AddScoped<IWalletApi, WalletController>();
            services.AddScoped<IFlowApi, FlowController>();
            services.AddScoped<IBalanceApi, BalanceController>();
            services.AddScoped<IOverviewApi, OverviewController>();
            services.AddScoped<ITransactionsApi, TransactionsController>();
            services.AddScoped<ICoinRatesApi, CoinRatesController>();
            services.AddScoped<IDataImportApi, DataImportController>();
            services.AddScoped<IImportEntriesApi, ImportEntriesController>();
            services.AddScoped<IImportOverviewApi, ImportOverviewController>();
            services.AddScoped<ILotsApi, LotsController>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        { 
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseWebAssemblyDebugging();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }


            //app.UseBlazorFrameworkFiles();

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseAntiforgery();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorComponents<App>()
                    .AddInteractiveServerRenderMode()
                    //.AddInteractiveWebAssemblyRenderMode()
                    .AddAdditionalAssemblies(typeof(Overview).Assembly);
                endpoints.MapControllers();
                endpoints.MapDefaultControllerRoute();
                endpoints.MapFallbackToFile("index.html");
            });
        }
    }
}

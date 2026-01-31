using Microsoft.EntityFrameworkCore;
using CryptoTracker.Components;
using CryptoTracker.Client.Pages;
using Microsoft.AspNetCore.Components;
using CryptoTracker.Services;
using NoobsMuc.Coinmarketcap.Client;
using CryptoTracker.Shared;
using CryptoTracker.Controllers;
using Radzen;
using CryptoTracker.Agent.Common;
using CryptoTracker.Agent.Definitions;
using CryptoTracker.Agent.Services;
using CryptoTracker.Agent.Tools;
using CryptoTracker.Hubs;
using Azure.AI.OpenAI;
using Azure;
using Azure.Identity;

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
            services.AddScoped<LotFlowValidator>();

            // === AI Linking Agent Services ===
            // OpenAI settings werden direkt aus Environment-Variablen / secrets.json gebunden
            services.Configure<OpenAISettings>(options =>
            {
                options.OpenAiEndpoint = Configuration["OpenAiEndpoint"] ?? "";
                options.OpenAiDeploymentName = Configuration["OpenAiDeploymentName"] ?? "gpt-5.2";
                options.OpenAiFastDeploymentName = Configuration["OpenAiFastDeploymentName"] ?? "gpt-5-nano";
                options.OpenAiEmbeddingDeploymentName = Configuration["OpenAiEmbeddingDeploymentName"] ?? "text-embedding-3-large";
                if (int.TryParse(Configuration["OpenAiEmbeddingVectorDimensions"], out var dims))
                    options.OpenAiEmbeddingVectorDimensions = dims;
                options.OpenAiKey = Configuration["OpenAiKey"];
            });

            // Azure OpenAI Client (mit API Key oder DefaultAzureCredential)
            services.AddSingleton<AzureOpenAIClient>(sp =>
            {
                var endpoint = Configuration["OpenAiEndpoint"];
                var apiKey = Configuration["OpenAiKey"];

                if (string.IsNullOrEmpty(endpoint))
                {
                    // Dummy-Client wenn nicht konfiguriert
                    return new AzureOpenAIClient(
                        new Uri("https://placeholder.openai.azure.com/"),
                        new AzureKeyCredential("placeholder"));
                }

                if (!string.IsNullOrEmpty(apiKey))
                {
                    return new AzureOpenAIClient(
                        new Uri(endpoint),
                        new AzureKeyCredential(apiKey));
                }
                return new AzureOpenAIClient(
                    new Uri(endpoint),
                    new DefaultAzureCredential());
            });

            // Agent Builder
            services.AddScoped<AILinkingAgentBuilder>();

            // Agent Tools
            services.AddScoped<GetUnlinkedTransactionsTool>();
            services.AddScoped<GetTransactionDetailsTool>();
            services.AddScoped<FindMatchingTransactionsTool>();
            services.AddScoped<LinkTransactionsTool>();
            services.AddScoped<MarkAsIntentionallyUnlinkedTool>();
            services.AddScoped<SaveAgentMemoryTool>();
            services.AddScoped<GetAgentMemoryTool>();

            // Agent Definitions
            services.AddScoped<IAgentDefinition, TransactionLinkingAgentDefinition>();

            // Agent Services
            services.AddScoped<TransactionLinkingService>();
            services.AddSingleton<InteractiveLinkingService>();
            services.AddSingleton<InteractiveLotLinkingService>();

            // SignalR
            services.AddSignalR();

            services.AddScoped<IWalletApi, WalletController>();
            services.AddScoped<IFlowApi, FlowController>();
            services.AddScoped<IBalanceApi, BalanceController>();
            services.AddScoped<IOverviewApi, OverviewController>();
            services.AddScoped<ITransactionsApi, TransactionsController>();
            services.AddScoped<ITransactionLinkingApi, TransactionLinkingController>();
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
                endpoints.MapHub<LinkingHub>("/hubs/linking");
                endpoints.MapDefaultControllerRoute();
                endpoints.MapFallbackToFile("index.html");
            });
        }
    }
}

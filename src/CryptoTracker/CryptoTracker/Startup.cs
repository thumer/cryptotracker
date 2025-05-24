using Microsoft.EntityFrameworkCore;
using CryptoTracker.Components;
using CryptoTracker.Client.Pages;
using Microsoft.AspNetCore.Components;
using CryptoTracker.Services;
using Finance.Net;
using Finance.Net.Extensions;
using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;

namespace CryptoTracker
{
    public class Startup
    {
        private readonly ILogger<Startup> _logger;

        public Startup(IConfiguration configuration, ILogger<Startup> logger)
        {
            Configuration = configuration;
            _logger = logger;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry();
            services.AddControllersWithViews();

            services.AddDbContext<CryptoTrackerDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

            services.AddRazorComponents()
                .AddInteractiveServerComponents()
                .AddInteractiveWebAssemblyComponents();

            services.AddBlazorise(options => { options.Immediate = true; })
                .AddBootstrap5Providers()
                .AddFontAwesomeIcons();

            services.AddFinanceNet(new FinanceNetConfiguration()
            {
                HttpRetryCount = 3
            });

            services.AddScoped<HttpClient>(sp =>
            {
                var navigationManager = sp.GetRequiredService<NavigationManager>();
                return new HttpClient { BaseAddress = new Uri(navigationManager.BaseUri) };
            });

            services.AddScoped<DataImportService>();
            services.AddScoped<WalletService>();
            services.AddScoped<FlowService>();
            services.AddScoped<IFinanceValueProvider, FinanceValueProvider>();
            services.AddScoped<BalanceService>();
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

            app.UseBlazorFrameworkFiles();

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            // Initialize Blazorise providers
            app.ApplicationServices
                .UseBootstrap5Providers()
                .UseFontAwesomeIcons();

            app.UseRouting();
            app.UseAntiforgery();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorComponents<App>()
                    .AddInteractiveServerRenderMode()
                    .AddInteractiveWebAssemblyRenderMode()
                    .AddAdditionalAssemblies(typeof(Overview).Assembly);
                endpoints.MapControllers();
                endpoints.MapDefaultControllerRoute();
                endpoints.MapFallbackToFile("index.html");
            });
            MigrateDatabase(app);
        }

        private static void MigrateDatabase(IApplicationBuilder app)
        {
            using (var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var dbContext = serviceScope.ServiceProvider.GetRequiredService<CryptoTrackerDbContext>();

                //TODO: Wenn das Schema steht -> Migrations anlegen!
                if (!dbContext.Database.EnsureCreated())
                    dbContext.Database.Migrate();
            }
        }
    }
}
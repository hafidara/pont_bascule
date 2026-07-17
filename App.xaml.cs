using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PontBascule.Services;
using PontBascule.ViewModels;
using PontBascule.Views;
using System.IO;
using System.Windows;

namespace PontBascule
{
    public partial class App : Application
    {
        private readonly ServiceProvider _serviceProvider;

        public App()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            services.AddSingleton<IConfiguration>(configuration);

            // Services
            services.AddSingleton<IScaleService, ScaleService>();
            services.AddSingleton<ISapService, SapService>();
            services.AddSingleton<IDatabaseService, DatabaseService>();
            services.AddSingleton<IWeighingWorkflowService, WeighingWorkflowService>();
            services.AddSingleton<ISageService, SageService>();
            services.AddSingleton<IPaperlessService, PaperlessService>();

            // ViewModels
            services.AddTransient<MainViewModel>();

            // Views
            services.AddTransient<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider.Dispose();
            base.OnExit(e);
        }
    }
}

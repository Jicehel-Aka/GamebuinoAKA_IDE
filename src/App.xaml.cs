using System;
using System.Windows;
using GamebuinoAKA.IDE.Services;
using GamebuinoAKA.IDE.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GamebuinoAKA.IDE
{
    
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;
    
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
    
            var collection = new ServiceCollection();
            ConfigureServices(collection);
            Services = collection.BuildServiceProvider();
    
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    
        private static void ConfigureServices(IServiceCollection services)
        {
            // Services
            services.AddSingleton<SettingsService>();
            services.AddSingleton<ProjectService>();
            services.AddSingleton<TemplateService>();
            services.AddSingleton<PlatformIOService>();
            services.AddSingleton<VSCodeService>();
            services.AddSingleton<AssetService>();
            services.AddSingleton<GitService>();
    
            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<HomeViewModel>();
            services.AddSingleton<ProjectsViewModel>();
            services.AddSingleton<NewProjectViewModel>();
            services.AddSingleton<SpriteEditorViewModel>();
            services.AddSingleton<TilemapEditorViewModel>();
            services.AddSingleton<SettingsViewModel>();
    
            // Views
            services.AddTransient<Views.HomeView>();
            services.AddTransient<Views.ProjectsView>();
            services.AddTransient<Views.NewProjectView>();
            services.AddTransient<Views.SpriteEditorView>();
            services.AddTransient<Views.TilemapEditorView>();
            services.AddTransient<Views.SettingsView>();
    
            // Windows
            services.AddSingleton<MainWindow>();
        }
    }
}

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using GamebuinoAKA.IDE.Services;
using GamebuinoAKA.IDE.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GamebuinoAKA.IDE
{

    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        // Empêche les boucles d'erreurs : une exception PENDANT la récupération
        // signifie qu'on ne peut pas revenir à un état stable → fermeture.
        private bool _handlingError;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            Log.Info("Démarrage de l'IDE.");

            try
            {
                var collection = new ServiceCollection();
                ConfigureServices(collection);
                Services = collection.BuildServiceProvider();

                var mainWindow = Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                // Échec au démarrage : rien à stabiliser → fermeture.
                FatalShutdown(ex, "L'IDE n'a pas pu démarrer.");
            }
        }

        // ── Politique de gestion des erreurs ─────────────────────────────────────

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true; // on prend la main sur l'erreur
            HandleUiException(e.Exception);
        }

        /// <summary>
        /// Thread UI : log + message, puis
        ///  - erreur critique                 → fermeture ;
        ///  - erreur pendant une récupération → fermeture (état non stabilisable) ;
        ///  - sinon                           → retour à un état stable (accueil) ;
        ///  - si la remise en état échoue     → fermeture.
        /// </summary>
        private void HandleUiException(Exception ex)
        {
            if (_handlingError)
            {
                FatalShutdown(ex, "Une erreur est survenue pendant la récupération. L'IDE va se fermer.");
                return;
            }

            _handlingError = true;
            try
            {
                Log.Error("Exception non gérée (thread UI).", ex);

                if (IsFatal(ex))
                {
                    FatalShutdown(ex, "Une erreur critique s'est produite. L'IDE va se fermer.");
                    return;
                }

                ShowErrorDialog("Une erreur est survenue. L'IDE va tenter de revenir à un état stable.", ex);

                if (!TryRestoreStableState())
                    FatalShutdown(ex, "Impossible de revenir à un état stable. L'IDE va se fermer.");
            }
            finally
            {
                _handlingError = false;
            }
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            Log.Error($"Exception non gérée (thread de fond, terminaison={e.IsTerminating}).", ex);
            // Sur un thread de fond, on ne peut pas « réparer » l'état de l'UI.
            // On informe ; si le CLR termine, la fermeture est inévitable (déjà loguée).
            ShowErrorDialog(e.IsTerminating
                ? "Une erreur critique s'est produite. L'IDE va se fermer."
                : "Une erreur est survenue sur un thread de fond. Elle a été journalisée.", ex);
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            // Continuation asynchrone non attendue : bénin pour l'UI.
            Log.Error("Exception de tâche non observée.", e.Exception);
            e.SetObserved();
        }

        /// <summary>Ramène l'appli à une vue sûre (accueil). false si impossible.</summary>
        private bool TryRestoreStableState()
        {
            try
            {
                if (Services?.GetService(typeof(MainViewModel)) is MainViewModel main
                    && main.NavigateToHomeCommand.CanExecute(null))
                {
                    main.NavigateToHomeCommand.Execute(null);
                    Log.Info("Retour à l'accueil après erreur.");
                    return true;
                }
                // Pas de navigation possible mais l'appli tourne toujours : on reste
                // sur la vue courante plutôt que de fermer.
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Échec de la remise en état stable.", ex);
                return false;
            }
        }

        private void FatalShutdown(Exception? ex, string message)
        {
            Log.Error("Fermeture automatique après erreur non récupérable.", ex);
            ShowErrorDialog(message, ex);
            try { Shutdown(-1); } catch { Environment.Exit(-1); }
        }

        /// <summary>Types d'exceptions qui compromettent le processus (non récupérables).</summary>
        private static bool IsFatal(Exception? ex) =>
            ex is OutOfMemoryException
               or AccessViolationException
               or System.Runtime.InteropServices.SEHException
               or AppDomainUnloadedException
               or BadImageFormatException;

        private void ShowErrorDialog(string message, Exception? ex)
        {
            try
            {
                var detail = ex?.Message ?? "Erreur inconnue.";
                var text =
                    $"{message}\n\n{detail}\n\n" +
                    $"Le détail complet a été enregistré dans le journal :\n{Log.LogFilePath}\n\n" +
                    "(Paramètres → Journal permet de l'ouvrir ou de le supprimer.)";

                void Show() => MessageBox.Show(text, "Gamebuino AKA IDE — Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                if (Dispatcher.CheckAccess()) Show();
                else Dispatcher.Invoke(Show);
            }
            catch { /* l'affichage de l'erreur ne doit jamais lever d'erreur */ }
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Services
            services.AddSingleton<SettingsService>();
            services.AddSingleton<ProjectService>();
            services.AddSingleton<TemplateService>();
            services.AddSingleton<PlatformIOService>();
            services.AddSingleton<EspIdfService>();
            services.AddSingleton<BuildService>();
            services.AddSingleton<VSCodeService>();
            services.AddSingleton<AssetService>();
            services.AddSingleton<GitService>();
            services.AddSingleton<SoundBankService>();
            services.AddSingleton<CodeSnippetService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<HomeViewModel>();
            services.AddSingleton<ProjectsViewModel>();
            services.AddSingleton<NewProjectViewModel>();
            services.AddSingleton<SpriteEditorViewModel>();
            services.AddSingleton<TilemapEditorViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<SoundBankViewModel>();
            services.AddSingleton<CodeSnippetPickerViewModel>();

            // Views
            services.AddTransient<Views.HomeView>();
            services.AddTransient<Views.ProjectsView>();
            services.AddTransient<Views.NewProjectView>();
            services.AddTransient<Views.SpriteEditorView>();
            services.AddTransient<Views.TilemapEditorView>();
            services.AddTransient<Views.SettingsView>();
            services.AddTransient<Views.SoundBankView>();
            services.AddTransient<Views.CodeSnippetPickerView>();

            // Windows
            services.AddSingleton<MainWindow>();
        }
    }
}

using NLog;
using OptimizationXY.Charger.Infrastructure;
using OptimizationXY.Charger.Services.Configuration;
using OptimizationXY.Charger.Services.Core;
using OptimizationXY.Charger.UI;

namespace OptimizationXY.Charger;

/// <summary>
/// The main entry point for the application.
/// </summary>
internal static class Program
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    [STAThread]
    private static void Main()
    {
        using var instanceMutex = new Mutex(
            initiallyOwned: true,
            name: @"Arbor.OptimizationXY.Charger.SingleInstance",
            createdNew: out bool createdNew);

        if (!createdNew)
        {
            MessageBox.Show(
                "Optimization XY Charger is already running. A second controller instance is blocked.",
                "Optimization XY Charger",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        ApplicationConfiguration.Initialize();

        try
        {
            ApplicationPaths.EnsureCreated();
            ConfigureLogging();
            ConfigureGlobalExceptionHandling();
            Logger.Info("Optimization XY Charger starting.");

            PipelineState.Instance.Settings =
                AppSettingsStore.LoadOrCreateDefault();

            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            Logger.Fatal(ex, "Application startup failed.");
            ShowFatalError(ex);
        }
        finally
        {
            Logger.Info("Optimization XY Charger stopped.");
            LogManager.Shutdown();
        }
    }

    /// <summary>
    /// Configures logging for the application.
    /// </summary>
    private static void ConfigureLogging()
    {
        ApplicationPaths.EnsureCreated();

        string configPath = Path.Combine(
            AppContext.BaseDirectory,
            "NLog.config");

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                $"NLog configuration file was not found: {configPath}",
                configPath);
        }

        LogManager.Setup()
            .LoadConfigurationFromFile(configPath);

        if (LogManager.Configuration is null)
        {
            throw new InvalidOperationException(
                $"NLog failed to load its configuration from: {configPath}");
        }

        LogManager.Configuration.Variables["logDirectory"] =
            ApplicationPaths.LogDirectory;

        LogManager.ReconfigExistingLoggers();

        Logger.Info(
            "NLog initialized. Log directory: {0}",
            ApplicationPaths.LogDirectory);
    }
    /// <summary>
    /// Configures global exception handling for the application.
    /// </summary>
    private static void ConfigureGlobalExceptionHandling()
    {
        Application.SetUnhandledExceptionMode(
            UnhandledExceptionMode.CatchException);

        Application.ThreadException += (_, args) =>
        {
            Logger.Error(args.Exception, "Unhandled UI thread exception.");
            ShowFatalError(args.Exception);

            // Fail closed after an unhandled UI exception. Continuing the
            // operator process with unknown state is not acceptable for a
            // machine-control application. Form shutdown disposes the single
            // PLC connection and stops camera acquisition.
            Application.Exit();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Exception exception = args.ExceptionObject as Exception ??
                new InvalidOperationException(
                    $"Unhandled exception object: {args.ExceptionObject}");

            Logger.Fatal(exception, "Unhandled application-domain exception.");
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Logger.Error(args.Exception, "Unobserved task exception.");
            args.SetObserved();
        };
    }

    /// <summary>
    /// Shows a fatal error message box to the user.
    /// </summary>
    private static void ShowFatalError(Exception exception)
    {
        MessageBox.Show(
            "Optimization XY Charger encountered an unexpected error." +
            Environment.NewLine +
            $"Log directory (when logging initialized):{Environment.NewLine}" +
            ApplicationPaths.LogDirectory + Environment.NewLine + Environment.NewLine +
            exception.Message,
            "Optimization XY Charger",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}

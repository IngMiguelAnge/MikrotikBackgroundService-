using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging.EventLog;
using MikrotikBackgroundService;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]

// Cambiamos a CreateDefaultBuilder para que el ciclo de vida de Windows funcione al 100%
IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "MikrotikServices";
    })
    .ConfigureLogging((hostContext, logging) =>
    {
        logging.ClearProviders();

        // Guarda logs en el Visor de Eventos
        logging.AddEventLog(options =>
        {
            options.SourceName = "MikrotikServices";
        });

        // Solo activa la pantalla negra si estás en modo Debug en Visual Studio
#if DEBUG
        logging.AddConsole();
#endif
    })
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
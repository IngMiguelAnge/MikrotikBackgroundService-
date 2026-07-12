using MikrotikBackgroundService.Class;
using MikrotikBackgroundService.Model;

namespace MikrotikBackgroundService
{
    public class Worker : BackgroundService
    {
        MK mikrotik;

        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //_logger.LogInformation("¡El servicio de prueba de mikrotik ha iniciado con éxito!");
            bool unavez = false;
            while (!stoppingToken.IsCancellationRequested)
            {
                // Imprime en la consola la fecha y la hora exacta del ciclo actual
                //_logger.LogInformation("El ciclo se ejecutó a las: {time}", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                //MikrotikModel  mikro = new MikrotikModel();
                //mikrotik = new MK(mikro.IP, Convert.ToInt32(mikro.Port));
                if (unavez == false)
                {
                    try
                    {
                        _logger.LogInformation("Intentando conectar a SQL Server...");

                        // Usamos 'using' para que al llegar a la llave de cierre se ejecute tu Dispose()
                        using (AppRepository obj = new AppRepository())
                        {
                            bool resultado = await obj.UpdateStatusBanco(1);

                            if (resultado)
                            {
                                _logger.LogInformation("¡Proceso almacenado ejecutado correctamente!");
                                unavez = true; // Éxito, no se vuelve a ejecutar
                            }
                            else
                            {
                                _logger.LogWarning("El repositorio devolvió 'false'. Reintentando en el próximo ciclo...");
                                unavez = false; // Falló internamente, reintenta en 10 segundos
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Evita que el servicio se caiga o se quede en START_PENDING si hay un error fatal
                        _logger.LogError(ex, "Error crítico al conectar o ejecutar en la base de datos.");
                        unavez = false;
                    }
                }
                // Para la prueba, haremos que despierte cada 10 segundos
                await Task.Delay(10 * 1000, stoppingToken);
            }
        }
    }
}

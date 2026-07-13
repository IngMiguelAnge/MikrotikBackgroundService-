using MikrotikBackgroundService.Class;
using MikrotikBackgroundService.Model;
using System.Numerics;

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

        public async Task Actualizaplanes(List<TiempoCambioModel> tc, string Modo)
        {
            AppRepository obj = new AppRepository();
            int MikrotikActual = 0;
            MikrotikModel mikro;
            List<int> MikrotiksInabilitados = new List<int>();

            foreach (var item in tc)
            {
                if (MikrotiksInabilitados.Contains(item.IdMikrotik))
                {
                    await obj.SaveTiempoCambioEstatus(item.Id, "Error", "Mikrotik inactivo");
                    continue;
                }
                if (MikrotikActual != item.IdMikrotik)
                {
                    mikro = new MikrotikModel();
                    mikro = await obj.GetMikrotikById(item.IdMikrotik);
                    MikrotikActual = item.IdMikrotik;
                    if (mikro.Estatus == false)
                    {
                        MikrotiksInabilitados.Add(item.IdMikrotik);
                        await obj.SaveTiempoCambioEstatus(item.Id, "Error", "Mikrotik inactivo");
                        continue;
                    }
                    if (mikrotik != null)
                    {
                        await Task.Run(() => mikrotik.Close());
                    }
                    mikrotik = new MK(mikro.IP, Convert.ToInt32(mikro.Port));
                    bool login = await Task.Run(() =>
                    {
                        return mikrotik.ConectarYLogin(mikro.Usuario, mikro.Password);
                    });
                    if (login == false)
                    {
                        MikrotiksInabilitados.Add(item.IdMikrotik);
                        await obj.SaveTiempoCambioEstatus(item.Id, "Error", "Mikrotik inactivo");
                        continue;
                    }
                }
                //Si se pudo conectar al mikrotik
                bool Result1 = false;
                int BuscarID = Modo == "Pendiente" ? item.IdPlan : item.IdPlanOriginal;
                var Plan = await obj.GetPlanById(BuscarID); //Se busca el plan nuevo
                if (Plan.Estatus == false)
                {
                    await obj.SaveTiempoCambioEstatus(item.Id, "Error", "Plan inactivo");
                    continue;
                }
                var Usuario = await obj.GetUsuariosMikrotiksById(item.IdUsuarioM);
                if (Usuario.Estatus != "Activo")
                {
                    await obj.SaveTiempoCambioEstatus(item.Id, "Error", "Usuario inactivo");
                    continue;
                }
                if (Plan.IsAntena == true)
                    Result1 = mikrotik.ActualizarVelocidadQueue(Usuario.Usuario, Plan.Velocidad);
                else
                {
                    Result1 = mikrotik.ActualizarUsuarioPPP(Usuario.IdInterno, Plan.Nombre, Plan.Velocidad);
                    if (Result1 == true)
                    {
                        var Result2 = await Task.Run(() =>
                        {
                            return mikrotik.DeleteInterfacebyPlan(Plan.Nombre);
                        });
                    }
                }
                if (Result1 == true)
                {
                    string ModoEnviar = Modo == "Pendiente" ? item.Modo : "Permanente";
                    var Ressult = await obj.UpdatePlanGeneral(item.Id, Plan.Id, ModoEnviar);
                    if (item.Modo == "Permanente")
                        await obj.SaveTiempoCambioEstatus(item.Id, "Completado", "Se actualizo el plan a " + Plan.Nombre);
                    else
                    {
                        if (item.Modo == "Pendiente")
                        {
                            await obj.SaveTiempoCambioEstatus(item.Id, "En ejecución", "Se actualizo el plan a " + Plan.Nombre);
                        }
                        else
                        {
                            await obj.SaveTiempoCambioEstatus(item.Id, "Completado", "Se actualizo el plan a " + Plan.Nombre);
                        }
                    }                      
                }
            }
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //_logger.LogInformation("¡El servicio de prueba de mikrotik ha iniciado con éxito!");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    //_logger.LogInformation( "Intentando conectar a SQL Server...");

                    AppRepository obj = new AppRepository();
                    List<TiempoCambioModel> tc = await obj.GetTiempoCambiobyEstatus("Pendiente");
                    if(tc.Count > 0)
                    {
                        await Actualizaplanes(tc, "Pendiente");
                    }
                    AppRepository obj2 = new AppRepository();
                    List<TiempoCambioModel> tc2 = await obj2.GetTiempoCambiobyEstatus("En ejecución");
                    if (tc2.Count > 0)
                    {
                       await Actualizaplanes(tc2, "En ejecución");
                    }
                }  
                catch (Exception ex)
                {
                    // Evita que el servicio se caiga o se quede en START_PENDING si hay un error fatal
                    _logger.LogError(ex, "Error crítico al conectar o ejecutar en la base de datos.");
                    throw; //Finaliza el servicio
                }
                // Para la prueba, haremos que despierte cada 10 segundos
                await Task.Delay(10 * 1000, stoppingToken);
            }
        }
    }
}

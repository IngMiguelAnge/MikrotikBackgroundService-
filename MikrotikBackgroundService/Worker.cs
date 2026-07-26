using MikrotikBackgroundService.Class;
using MikrotikBackgroundService.Model;
using System.Net;

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


        public async Task Actualizaplanes(List<TiempoCambioModel> tc, string Modo, CancellationToken stoppingToken)
        {
            AppRepository obj = new AppRepository();
            int MikrotikActual = 0;
            MikrotikModel mikro;
            List<int> MikrotiksInabilitados = new List<int>();

            try
            {
                bool Conecta = false;
                string PlanAceptado = string.Empty;
                string IpMikrotik = string.Empty, PasswordMikrotik = string.Empty, UsuarioMikrotik = string.Empty, PortMikrotik = string.Empty;
                foreach (var item in tc)
                {
                    PlanAceptado = string.Empty;
                    if (stoppingToken.IsCancellationRequested) break;

                    // Envolvemos el flujo por cada registro en un try-catch SIN finally interno
                    try
                    {                        
                        if (MikrotiksInabilitados.Contains(item.IdMikrotikReceptor))
                        {
                           //_logger.LogInformation("Mikrotik inactivo, Id:" + item.IdMikrotikReceptor.ToString());
                            await obj.SaveTiempoCambioEstatus(item.Id, "Error", "Mikrotik inactivo id " + item.IdMikrotikReceptor.ToString() + " se cancela la solicitud");
                            continue;
                        }
                        int BuscarID = Modo == "Pendiente" ? item.IdPlan : item.IdPlanOriginal;
                        int EnviarMikrotik = Modo == "Pendiente" ? item.IdMikrotikReceptor : item.IdMikrotikOriginal;
                        var PlanNuevo = await obj.GetPlanById(BuscarID);
                        if (PlanNuevo.Estatus == false)
                        {
                           //_logger.LogInformation("Plan Inactivo" + PlanNuevo.Nombre);
                            await obj.SaveTiempoCambioEstatus(item.Id, "Error", "Plan inactivo se cancela la solicitud");
                            continue;
                        }
                        string RevisaPlan = PlanNuevo.IsAntena == true ? "Antena" : "Fibra";
                     
                        var Usuario = await obj.GetUsuariosMikrotiksById(item.IdUsuarioM);
                        if (Usuario.Estatus != "Activo")
                        {
                           //_logger.LogInformation("Usuario Inactivo" + Usuario.Usuario);
                            await obj.SaveTiempoCambioEstatus(item.Id, "Error", "Usuario inactivo se cancela la solicitud");
                            continue;
                        }

                        if (MikrotikActual != item.IdMikrotikReceptor)
                        {
                                              
                            mikro = new MikrotikModel();
                            mikro = await obj.GetMikrotikById(item.IdMikrotikReceptor);
                            PlanAceptado = mikro.PlanAceptado;
                            MikrotikActual = item.IdMikrotikReceptor;
                            IpMikrotik = mikro.IP;
                            PasswordMikrotik = mikro.Password;
                            UsuarioMikrotik = mikro.Usuario;
                            PortMikrotik = mikro.Port;

                            if (mikro.Estatus == false)
                            {
                                MikrotiksInabilitados.Add(item.IdMikrotik);
                               //_logger.LogInformation("Mikrotik inactivo Id:" + item.IdMikrotik.ToString());
                                await obj.SaveTiempoCambioEstatus(item.Id, "Error", "Mikrotik inactivo se cancela la solicitud");
                                continue;
                            }
                            Conecta = true;
                        }
                        if (PlanAceptado != "Ambos" && PlanAceptado != RevisaPlan)
                        {
                            //_logger.LogInformation("Mikrotik no permite el modo " + item.Modo + " Id:" + item.IdMikrotik.ToString());
                            await obj.SaveTiempoCambioEstatus(item.Id, "Error", "Mikrotik no permite plan de " + item.Modo + " se cancela la solicitud");
                            continue;
                        }
                        if (Conecta == true)
                        {
                            Conecta = false;
                            // Si cambiamos de MikroTik, cerramos limpiamente el anterior antes de abrir el nuevo
                            if (mikrotik != null)
                            {
                                await Task.Run(() => mikrotik.Close());
                                mikrotik = null;
                            }
                            mikrotik = new MK(IpMikrotik, Convert.ToInt32(PortMikrotik));
                            bool login = await Task.Run(() =>
                            {
                                return mikrotik.ConectarYLogin(UsuarioMikrotik, PasswordMikrotik);
                            });
                            if (login == false)
                            {
                                MikrotiksInabilitados.Add(item.IdMikrotik);
                                //_logger.LogInformation("Error al conectar con el Mikrotik Id:" + item.IdMikrotik.ToString());
                                await obj.SaveTiempoCambioEstatus(item.Id, "Error", "Error al conectar con el Mikrotik se cancela la solicitud");
                                mikrotik = null;
                                continue;
                            }
                        }
                       
                        if (mikrotik == null) continue;
                     
                        // Si se pudo conectar al mikrotik
                        bool Result1 = false;
                 
                        if (item.IdMikrotikReceptor == item.IdMikrotik) //Mikrotik a mover vs mikrotik que tiene el usuario actualmente
                        {
                            var PlanActual = await obj.GetPlanById(item.IdPlanActual);
                           if(PlanActual.IsAntena == PlanNuevo.IsAntena)
                            {
                                //Si es en el mismo mikrotik y el mismo modo de plan
                                //Solo se actuaizaran las velocidades.
                               //_logger.LogInformation("Comenzando actualización de mikrotik");
                                if (PlanNuevo.IsAntena == true)
                                    Result1 = mikrotik.ActualizarVelocidadQueue(Usuario.Usuario, PlanNuevo.Velocidad);
                                else
                                {
                                    Result1 = mikrotik.ActualizarUsuarioPPP(Usuario.IdInterno, PlanNuevo.Nombre, PlanNuevo.Velocidad);
                                    if (Result1 == true)
                                    {
                                        var Result2 = await Task.Run(() =>
                                        {
                                            return mikrotik.DeleteInterfacebyPlan(PlanNuevo.Nombre);
                                        });
                                    }
                                }
                                if (Result1 == true)
                                {
                                    //_logger.LogInformation("Comenzando actualización de plan de base general");
                                    string ModoEnviar = Modo == "Pendiente" ? item.Modo : "Permanente";
                                    var Ressult = await obj.UpdatePlanGeneral(item.Id, PlanNuevo.Id, EnviarMikrotik, ModoEnviar);
                                    if (item.Modo == "Permanente")
                                        await obj.SaveTiempoCambioEstatus(item.Id, "Completado", "Se actualizo el plan a " + PlanNuevo.Nombre);
                                    else
                                    {
                                        if (Modo == "Pendiente")
                                            await obj.SaveTiempoCambioEstatus(item.Id, "Ejecutando", "Se actualizo el plan a " + PlanNuevo.Nombre);
                                        else
                                            await obj.SaveTiempoCambioEstatus(item.Id, "Completado", "Se actualizo el plan a " + PlanNuevo.Nombre);
                                    }
                                }
                                else
                                {
                                    await obj.SaveTiempoCambioEstatus(item.Id, "Error", "Fallo actualización del mikrotik");
                                    //_logger.LogInformation("Fallo actualización de mikrotik");
                                }

                            }
                            else
                            {
                                //Si estan en el mismo mikroti pero el modo es distinto hay que
                                //insertar o reactivar al usuario si es que existe
                                //y actualizar su velocidad, y desactivarlo del modo anterior
                               //_logger.LogInformation("Se busca existencia del usuario con el plan solicitado");
                                //string ExisteUsuario = string.Empty;
                                //if (PlanNuevo.IsAntena == true)
                                //{
                                //    ExisteUsuario = mikrotik.VerIdQueue(Usuario.Usuario);
                                //    if(ExisteUsuario == string.Empty)//no existe el usuario en el mikrotik receptor
                                //    {
                                //       //_logger.LogInformation("El usuario no existe en el nuevo mikrotik, se procede a crearlo");
                                //        var listacomments = await Task.Run(() => obj.GetCommentsActivos(item.IdMikrotikReceptor));
                                //        string Comment = listacomments.First().Nombre;
                                //       //_logger.LogInformation("Se obtiene el comment" + Comment);
                                //        if(Comment == string.Empty)
                                //        {
                                //            await obj.SaveTiempoCambioEstatus(item.Id, "Error", "No se encontro comment para el mikrotik con id " + item.IdMikrotikReceptor + " se cancela la solicitud");
                                //            continue;
                                //        }
                                //        var IPDisponible= obj.GetIPDisponible(item.IdMikrotikReceptor,true);
                                //        if(IPDisponible.Result == string.Empty) //ya no hay mas ips hay que crear una nueva serie
                                //        {
                                //            var IPDisponibleWireles = obj.GetIPDisponibleAdresslist(item.IdMikrotikReceptor);//Busca entre todos los registrados
                                //            var lista = await Task.Run(() => mikrotik.VerAddres());
                                //            var listaFinal = lista?.ToList() ?? new List<AddressModel>();
                                //            var existewireles = listaFinal.Count > 0 ?
                                //                listaFinal.Where(x => x.address == IPDisponibleWireles.Result).First(): new AddressModel();
                                //            if (existewireles.id != string.Empty)
                                //            {
                                //                //Lo agregamos a la base general porque no lo tnemos
                                //                InsertListWirelessModel model = new InsertListWirelessModel
                                //                {
                                //                    IdMikrotik = item.IdMikrotikReceptor,
                                //                    Address = existewireles.address,
                                //                    Comment = existewireles.comment,
                                //                    Estatus = existewireles.estatus,
                                //                    IdInterno = existewireles.id
                                //                };
                                //                if (obj.SaveWireless(model).Result == false)
                                //                {
                                //                    MessageBox.Show("Error al actualizar wireless. id: " + item.id, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                //                    return;
                                //                }
                                //            }
                                //        }
                                //       bool exito =  mikrotik.CrearSimpleQueue(Usuario.Usuario, "192.168.1.50/32", "5M", "20M", "Fibra - Juan Perez");

                                //        if (exito)
                                //        {
                                //            MessageBox.Show("Queue creado correctamente en MikroTik RouterOS v7.");
                                //        }
                                //        else
                                //        {
                                //            MessageBox.Show("Error al crear el Queue (revisa si el nombre o la IP ya existen).");
                                //        }

                                //        // 1. Agregar a la lista de "Morosos" (bloqueados)
                                //        bool agregado = AgregarAddressList("Morosos", "192.168.1.50", "Cliente - Juan Perez", false);

                                //        // 2. O si manejas listas de IPs permitidas ("Clientes_Activos"):
                                //        bool registrado = AgregarAddressList("Clientes_Activos", "192.168.1.50/32", "ID: 1002 - Fibra", false);

                                //        if (agregado)
                                //        {
                                //            MessageBox.Show("IP agregada al Firewall correctamente.");
                                //        }
                                //        else
                                //        {
                                //            MessageBox.Show("Error al agregar la IP (es posible que ya exista en esa misma lista).");
                                //        }
                                //        //Ya que se creo desconectamos
                                //        if (mikrotik != null)
                                //        {
                                //            try { mikrotik.Close(); } catch { }
                                //            mikrotik = null;
                                //        }
                                //        //Regresamos al mikrotik anterior y desactivamos al usuario
                                //        MikrotikActual = 0;
                                //        mikro = new MikrotikModel();
                                //        mikro = await obj.GetMikrotikById(item.IdMikrotikOriginal);
                                //        if (mikro.Estatus == false)
                                //        {
                                //            MikrotiksInabilitados.Add(item.IdMikrotik);
                                //           //_logger.LogInformation("Mikrotik orignen inactivo Id:" + item.IdMikrotik.ToString());
                                //            await obj.SaveTiempoCambioEstatus(item.Id, "Error", "Se logro crear el usuario en el nuevo mikrotik, pero el mikrotik origen esta inactivo Idmikrotik:" + item.IdMikrotikOriginal.ToString());
                                //            continue;
                                //        }
                                //        // Si cambiamos de MikroTik, cerramos limpiamente el anterior antes de abrir el nuevo
                                //        if (mikrotik != null)
                                //        {
                                //            await Task.Run(() => mikrotik.Close());
                                //            mikrotik = null;
                                //        }
                                //        mikrotik = new MK(mikro.IP, Convert.ToInt32(mikro.Port));
                                //        bool login = await Task.Run(() =>
                                //        {
                                //            return mikrotik.ConectarYLogin(mikro.Usuario, mikro.Password);
                                //        });
                                //        if (login == false)
                                //        {
                                //            MikrotiksInabilitados.Add(item.IdMikrotik);
                                //           //_logger.LogInformation("Error al conectar con el Mikrotik Id:" + item.IdMikrotik.ToString());
                                //            await obj.SaveTiempoCambioEstatus(item.Id, "Error", "Error al conectar con el Mikrotik origen, no se logro desactivar al usuario:"+ Usuario.Usuario + " del mikrotik id:"+item.IdMikrotikOriginal);
                                //            mikrotik = null;
                                //            continue;
                                //        }
                                //         mikrotik.CambiarEstatusAntena(Usuario.IdInterno,"Activo");
                                //        if (mikrotik != null)
                                //        {
                                //            await Task.Run(() => mikrotik.Close());
                                //            mikrotik = null;
                                //        }
                                //    }
                                //    else
                                //    {
                                //       //_logger.LogInformation("Se encontro el usuario en el otro mikrotik, se procede a reactivarlo");

                                //    }
                                //}

                            }
                        }
                        else
                        {
                            //Si es en otro mikrotik el cambio entonces
                            //Se desactivara del mikrotik actual
                            //Se insertara o reactivara el usuario en el otro mikrotik
                            //y se actualizara su velocidad
                        }
                     
                     
                    }
                    catch (Exception ex)
                    {
                        // Si falla un cliente o router específico, se registra en BD, se añade a inhabilitados y pasamos al siguiente sin tumbar el servicio entero
                        //_logger.LogError(ex, $"Error procesando el registro Id {item.Id} para el MikroTik {item.IdMikrotik}.");
                        MikrotiksInabilitados.Add(item.IdMikrotik);
                        await obj.SaveTiempoCambioEstatus(item.Id, "Error", "Error crítico en procesamiento: " + ex.Message + " se cancela la solicitud");

                        // Si falló de forma drástica, nos aseguramos de limpiar la variable para forzar reconexión en el próximo router diferente
                        if (mikrotik != null)
                        {
                            try { mikrotik.Close(); } catch { }
                            mikrotik = null;
                        }
                        continue;
                    }
                }
            }
            finally
            {
                // EL FINALLY VA AQUÍ: Al terminar toda la lista de planes, cerramos el último MikroTik que haya quedado abierto
                if (mikrotik != null)
                {
                    await Task.Run(() => mikrotik.Close());
                    mikrotik = null;
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    AppRepository obj = new AppRepository();
                   //_logger.LogInformation("Buscando pendientes...");
                    List<TiempoCambioModel> tc = await obj.GetTiempoCambiobyEstatus("Pendiente");
                    if (tc.Count > 0)
                    {
                       _logger.LogInformation("Se va a actualizar los planes pendientes...");
                        await Actualizaplanes(tc, "Pendiente", stoppingToken);
                    }
                    AppRepository obj2 = new AppRepository();
                   //_logger.LogInformation("Buscando en ejecución...");
                    List<TiempoCambioModel> tc2 = await obj2.GetTiempoCambiobyEstatus("Ejecutando");
                    if (tc2.Count > 0)
                    {
                       _logger.LogInformation("Se va a actualizar los planes en ejecucion...");
                        await Actualizaplanes(tc2, "Ejecutando", stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    // Si el error es de SQL Server o algo que impida leer los pendientes, aquí SÍ tiramos el throw para matar el servicio
                    _logger.LogError(ex, "Error crítico general en la base de datos. Deteniendo servicio.");
                    throw;
                }

                await Task.Delay(10 * 1000, stoppingToken);
            }
        }
    }
}
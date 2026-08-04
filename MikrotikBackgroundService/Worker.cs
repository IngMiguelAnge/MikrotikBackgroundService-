using MikrotikBackgroundService.Class;
using MikrotikBackgroundService.Model;
using System;
using System.Net;
using System.Xml.Linq;

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
            string NombreMikrotikConectado = string.Empty;

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
                            NombreMikrotikConectado = mikro.Nombre;
                            if (mikro.Estatus == false)
                            {
                                MikrotiksInabilitados.Add(item.IdMikrotik);
                                //_logger.LogInformation("Mikrotik inactivo Id:" + item.IdMikrotik.ToString());
                                await obj.SaveTiempoCambioEstatus(item.Id, "Error", "Mikrotik inactivo se cancela la solicitud");
                                continue;
                            }
                            Conecta = true;
                        }
                        if (item.Programacion == "Cambio de plan")
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
                            if (item.Programacion == "Cambio de plan")
                            {
                                var PlanActual = await obj.GetPlanById(item.IdPlanActual);
                                if (PlanActual.IsAntena == PlanNuevo.IsAntena)
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
                                                return mikrotik.DeleteInterfacebyPlan(PlanActual.Nombre);
                                            });
                                            Result2 = await Task.Run(() =>
                                            {
                                                return mikrotik.DeleteInterfacebyPlan(PlanNuevo.Nombre);
                                            });
                                        }
                                    }
                                    if (Result1 == true)
                                    {
                                        //_logger.LogInformation("Comenzando actualización de plan de base general");
                                        string ModoEnviar = Modo == "Pendiente" ? item.Modo : "Permanente";
                                        var Ressult = await obj.UpdatePlanGeneral(Usuario.Id, PlanNuevo.Id, EnviarMikrotik, ModoEnviar);
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
                                    //Si estan en el mismo mikrotik pero el modo es distinto hay que
                                    //insertar y eliminar al usuario del servicio anterior
                                    //_logger.LogInformation("Se busca existencia del usuario con el plan solicitado");

                                    if (PlanNuevo.IsAntena == true)
                                    {
                                        //Mismo mikrotik pero es  cambio a antena
                                        string ExisteEnQueue = string.Empty;
                                        ExisteEnQueue = mikrotik.VerIdQueue(Usuario.Usuario);
                                        if (ExisteEnQueue != string.Empty)
                                        {
                                            await obj.SaveTiempoCambioEstatus(item.Id, "Error", "El usuario " + Usuario.Usuario + " ya existe previamente en queues revisar, se cancela la solicitud");
                                            continue;
                                        }
                                        List<AntenasModel> ExisteEnAntenas = new List<AntenasModel>();
                                        ExisteEnAntenas = mikrotik.VerAntenasbyComment(Usuario.Usuario);
                                        if (ExisteEnAntenas.Count() > 0)
                                        {
                                            await obj.SaveTiempoCambioEstatus(item.Id, "Error", "El usuario " + Usuario.Usuario + " ya existe previamente en firewall revisar, se cancela la solicitud");
                                            continue;
                                        }
                                        var listacomments = await Task.Run(() => obj.GetCommentsActivos(item.IdMikrotikReceptor));
                                        string Comment = listacomments.First().Nombre;
                                        if (Comment == string.Empty)
                                        {
                                            await obj.SaveTiempoCambioEstatus(item.Id, "Error", "No se encontro comment para el mikrotik con id " + item.IdMikrotikReceptor + ", se cancela la solicitud");
                                            continue;
                                        }
                                    buscaotraipAntena:
                                        var IPDisponible = obj.GetIPDisponible(item.IdMikrotikReceptor, true);
                                        if (IPDisponible.Result != string.Empty)
                                        {
                                            //Checamos que no exista el ip que continua, si existe mandaremos una mensaje para que lo revisen
                                            ExisteEnQueue = mikrotik.VerIdQueuebyAddress(IPDisponible.Result);//Se extrae el id del queues
                                            ExisteEnAntenas = mikrotik.VerAntenasbyAddress(IPDisponible.Result);
                                            if (ExisteEnAntenas.Count() == 0 && ExisteEnQueue != string.Empty)//No existe en firewall pero si en queue
                                            {
                                                await obj.SaveTiempoCambioEstatus(item.Id, "Error", "En el recorrido de las ips se encontro un error logico, en quest existe la ip " + IPDisponible + " pero en firewall no se encontro cohincidencia, perteneciente al mikrotik " + NombreMikrotikConectado + ", se cancela la solicitud");
                                                continue;
                                            }
                                            if (ExisteEnAntenas.Count() > 0) //Si existe en firewall
                                            {
                                                HistorialMovimientosModel H = new HistorialMovimientosModel
                                                {
                                                    Id = 0,
                                                    Descripcion = "Ya se encuentra registrado el ip " + IPDisponible.Result + " para antena, en el mikrotik " + NombreMikrotikConectado + " y no esta informado el sistema favor de actualizar, se procedera a guardarlo en el sistema, favor de revisar",
                                                    Pagina = "Servicio automatico de planes",
                                                    IdUsuario = 1,
                                                    Estatus = true
                                                };
                                                await obj.SaveHistorialMovimientos(H);
                                                //Insertamos el encontrado para que mas tarde lo revise el administrador y tambien para que no cuente para nuestra busqueda
                                                if (ExisteEnAntenas.First().velocidad == string.Empty)
                                                {
                                                    H = new HistorialMovimientosModel
                                                    {
                                                        Id = 0,
                                                        Descripcion = "La ip " + IPDisponible + " no se encuentra registrada en el sistema, y no se ecnontro velocidad designada, se procedera a guardarlo en el sistema con velocidad de 1k/1k, favor de revisar",
                                                        Pagina = "Servicio automatico de planes",
                                                        IdUsuario = 1,
                                                        Estatus = true
                                                    };    //solo quedara registrado en el sistema mas no afectara a mikrotik
                                                    await obj.SaveHistorialMovimientos(H);
                                                }
                                                PlanModel objPlan = new PlanModel();
                                                objPlan.Velocidad = ExisteEnAntenas.First().velocidad == string.Empty ? "1k/1k" : ExisteEnAntenas.First().velocidad;
                                                objPlan.IsAntena = true;
                                                var result = obj.SavePlanByMigracion(objPlan);
                                                if (result.Result == 0)
                                                {
                                                    await obj.SaveTiempoCambioEstatus(item.Id, "Error", "No se logro guardar el plan para la solicitud asignada en la base de datos favor de revisar.");
                                                    continue;
                                                }
                                                objPlan.Id = result.Result;
                                                PlanAnidadoModel objAnidado = new PlanAnidadoModel();
                                                objAnidado.IdMikrotik = item.IdMikrotikReceptor;
                                                objAnidado.IdPlanInterno = string.Empty;
                                                objAnidado.IdPlan = objPlan.Id;
                                                objAnidado.IsAntena = true;
                                                objAnidado.Id = 0;
                                                var ress = obj.SavePlanAnidadoByMigracion(objAnidado);
                                                SaveUsuariosGeneralModel objuser = new SaveUsuariosGeneralModel();
                                                objuser.IdMikrotik = item.IdMikrotikReceptor;
                                                objuser.Nombre = Usuario.Usuario;
                                                objuser.Address = IPDisponible.Result;
                                                objuser.IdInterno = ExisteEnAntenas.First().id;
                                                objuser.Estatus = ExisteEnAntenas.First().estatus;
                                                objuser.Id = 0;
                                                objuser.IdPlan = objPlan.Id;
                                                var res = obj.SaveUsuariosGeneral(objuser, 1).Result;

                                                goto buscaotraipAntena;
                                            }
                                            else
                                            {
                                                //No existe en el mikrotik ahora si podemos meter el nuevo ip
                                                PlanModel objPlan = new PlanModel();
                                                objPlan.Velocidad = PlanNuevo.Velocidad;
                                                objPlan.IsAntena = true;
                                                var result = obj.SavePlanByMigracion(objPlan);
                                                if (result.Result == 0)
                                                {
                                                    await obj.SaveTiempoCambioEstatus(item.Id, "Error", "No se logro guardar el plan para la solicitud asignada en la base de datos favor de revisar.");
                                                    continue;
                                                }
                                                objPlan.Id = result.Result;
                                                PlanAnidadoModel objAnidado = new PlanAnidadoModel();
                                                objAnidado.IdMikrotik = item.IdMikrotikReceptor;
                                                objAnidado.IdPlanInterno = string.Empty;
                                                objAnidado.IdPlan = objPlan.Id;
                                                objAnidado.IsAntena = true;
                                                objAnidado.Id = 0;
                                                var ress = obj.SavePlanAnidadoByMigracion(objAnidado);

                                                //Insertamos en mikrotik
                                                bool r = mikrotik.CrearSimpleQueue(Usuario.Usuario, IPDisponible.Result, PlanNuevo.Velocidad, Comment);
                                                bool r2 = mikrotik.AgregarAntena(Comment, IPDisponible.Result, Usuario.Usuario, true);
                                                ExisteEnAntenas = new List<AntenasModel>();
                                                ExisteEnAntenas = mikrotik.VerAntenasbyAddress(IPDisponible.Result);
                                                SaveUsuariosGeneralModel objuser = new SaveUsuariosGeneralModel();
                                                objuser.IdMikrotik = item.IdMikrotikReceptor;
                                                objuser.Nombre = Usuario.Usuario;
                                                objuser.Address = IPDisponible.Result;
                                                objuser.IdInterno = ExisteEnAntenas.First().id;
                                                objuser.Estatus = ExisteEnAntenas.First().estatus;
                                                objuser.Id = Usuario.Id;
                                                objuser.IdPlan = objPlan.Id;
                                                var res = obj.SaveUsuariosGeneral(objuser, 1).Result;
                                                if (item.Modo == "Permanente")
                                                    await obj.SaveTiempoCambioEstatus(item.Id, "Completado", "Se transfirio exitosamente al usuario " + Usuario.Usuario + " de antena a fibra");
                                                else
                                                {
                                                    if (Modo == "Pendiente")
                                                        await obj.SaveTiempoCambioEstatus(item.Id, "Ejecutando", "Se transfirio exitosamente al usuario " + Usuario.Usuario + " de antena a fibra");
                                                    else
                                                        await obj.SaveTiempoCambioEstatus(item.Id, "Completado", "Se transfirio exitosamente al usuario " + Usuario.Usuario + " de antena a fibra");
                                                }
                                                mikrotik.EliminarFibra(Usuario.IdInterno);
                                                mikrotik.DeleteInterfacebyName(Usuario.Usuario);
                                                HistorialMovimientosModel H = new HistorialMovimientosModel
                                                {
                                                    Id = 0,
                                                    Descripcion = "Se movio el usuario " + Usuario.Usuario + " de fibra ha antena dentro del mismo mikrotik " + NombreMikrotikConectado,
                                                    Pagina = "Servicio automatico de planes",
                                                    IdUsuario = 1,
                                                    Estatus = false
                                                };    //solo quedara registrado en el sistema mas no afectara a mikrotik
                                                await obj.SaveHistorialMovimientos(H);
                                            }
                                        }
                                        else
                                        {
                                        NuevaIpAddres:
                                            //Se acabaron las ips disponibles de esa serie 
                                            var IPDisponibleAddress = obj.GetIPDisponibleAdresslist(item.IdMikrotikReceptor, true);
                                            var ExisteAddresList = mikrotik.VerAddresbyAddress(IPDisponibleAddress.Result);
                                            string IpExist = obj.GetIPExist(item.IdMikrotikReceptor, true, IPDisponibleAddress.Result).Result;
                                            if (IpExist == string.Empty && ExisteAddresList.ToList().Count() > 0)
                                            {
                                                //No existe en la base pero si en el mikrotik
                                                //Lo introduciremos para que lo saltemos y no recorreremos su serie
                                                InsertListWirelessModel model = new InsertListWirelessModel
                                                {
                                                    IdMikrotik = item.IdMikrotikReceptor,
                                                    Address = IPDisponibleAddress.Result,
                                                    Comment = ExisteAddresList.First().comment,
                                                    Estatus = ExisteAddresList.First().estatus,
                                                    IdInterno = ExisteAddresList.First().id,
                                                    Completado = true
                                                };
                                                await obj.SaveWireless(model);
                                                HistorialMovimientosModel H = new HistorialMovimientosModel
                                                {
                                                    Id = 0,
                                                    Descripcion = "La ip " + IPDisponibleAddress.Result + " se encontro en el addres list del mikrotik " + NombreMikrotikConectado + " pero no esta registrado en la base, se agregara a la base de forma automatica",
                                                    Pagina = "Servicio automatico de planes",
                                                    IdUsuario = 1,
                                                    Estatus = false
                                                };
                                                await obj.SaveHistorialMovimientos(H);
                                                goto NuevaIpAddres;
                                            }
                                            if (ExisteAddresList.ToList().Count() == 0)
                                            {
                                                //No existe en el mikrotik se procede a instroducirlo
                                                var result = mikrotik.AgregarIPAddress(IPDisponibleAddress.Result, "LAN_BOT" + item.Id.ToString(), "LAN_BOT" + item.Id.ToString());
                                                string text = result == true ? "La ip " + IPDisponibleAddress.Result + " no se encontro en el addres list del mikrotik " + NombreMikrotikConectado + ", se agregara a la base e introducira en el mikrotik de forma automatica" :
                                                    "La ip " + IPDisponibleAddress.Result + " no se logro introducir en el addres list del mikrotik " + NombreMikrotikConectado;
                                                bool Estatushistory = result == true ? false : true;
                                                HistorialMovimientosModel H = new HistorialMovimientosModel
                                                {
                                                    Id = 0,
                                                    Descripcion = text,
                                                    Pagina = "Servicio automatico de planes",
                                                    IdUsuario = 1,
                                                    Estatus = Estatushistory
                                                };
                                                await obj.SaveHistorialMovimientos(H);
                                                if (Estatushistory == true)
                                                {
                                                    await obj.SaveTiempoCambioEstatus(item.Id, "Error", "No se logro introducir la ip en el addres list del mikrotik " + NombreMikrotikConectado + ", se cancela la solicitud");
                                                    continue;
                                                }
                                                else
                                                {
                                                    goto buscaotraipAntena;
                                                }
                                            }
                                            if (IpExist != string.Empty && ExisteAddresList.ToList().Count() > 0)
                                            {
                                                //Existe en el mikrotik y tambien en la base
                                                goto buscaotraipAntena;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        //Mismo mikrotik pero es cambio a fibra
                                        List<FibrasModel> ExisteEnFibra = mikrotik.VerFibra(Usuario.Usuario);
                                        if (ExisteEnFibra.Count() > 0)
                                        {
                                            await obj.SaveTiempoCambioEstatus(item.Id, "Error", "El usuario " + Usuario.Usuario + " ya existe previamente en fibra revisar, se cancela la solicitud");
                                            continue;
                                        }
                                    buscaotraipFibra:
                                        var IPDisponibleFibra = obj.GetIPDisponible(item.IdMikrotikReceptor, false);

                                        if (IPDisponibleFibra.Result != string.Empty)
                                        {
                                            ExisteEnFibra = mikrotik.VerFibrabyAddress(IPDisponibleFibra.Result);//Se extrae el id del queues
                                            if (ExisteEnFibra.Count() > 0) //Ya existe en secret
                                            {
                                                HistorialMovimientosModel H = new HistorialMovimientosModel
                                                {
                                                    Id = 0,
                                                    Descripcion = "Ya se encuentra registrado el ip " + IPDisponibleFibra.Result + " para fibra, en el mikrotik " + NombreMikrotikConectado + " y no esta informado el sistema favor de actualizar, se procedera a guardarlo en el sistema, favor de revisar",
                                                    Pagina = "Servicio automatico de planes",
                                                    IdUsuario = 1,
                                                    Estatus = true
                                                };
                                                await obj.SaveHistorialMovimientos(H);

                                                PlanModel objPlan = new PlanModel();
                                                objPlan.Velocidad = ExisteEnFibra.First().velocidad == string.Empty ? "1k/1k" : ExisteEnFibra.First().velocidad;
                                                objPlan.IsAntena = true;
                                                var result = obj.SavePlanByMigracion(objPlan);
                                                if (result.Result == 0)
                                                {
                                                    await obj.SaveTiempoCambioEstatus(item.Id, "Error", "No se logro guardar el plan para la solicitud asignada en la base de datos favor de revisar.");
                                                    continue;
                                                }
                                                objPlan.Id = result.Result;
                                                PlanAnidadoModel objAnidado = new PlanAnidadoModel();
                                                objAnidado.IdMikrotik = item.IdMikrotikReceptor;
                                                objAnidado.IdPlanInterno = ExisteEnFibra.First().idplan;
                                                objAnidado.IdPlan = objPlan.Id;
                                                objAnidado.IsAntena = false;
                                                objAnidado.Id = 0;
                                                var ress = obj.SavePlanAnidadoByMigracion(objAnidado);
                                                SaveUsuariosGeneralModel objuser = new SaveUsuariosGeneralModel();
                                                objuser.IdMikrotik = item.IdMikrotikReceptor;
                                                objuser.Nombre = Usuario.Usuario;
                                                objuser.Address = IPDisponibleFibra.Result;
                                                objuser.IdInterno = ExisteEnFibra.First().id;
                                                objuser.Estatus = ExisteEnFibra.First().estatus;
                                                objuser.Id = 0;
                                                objuser.IdPlan = objPlan.Id;
                                                var res = obj.SaveUsuariosGeneral(objuser, 1).Result;

                                                goto buscaotraipFibra;
                                            }
                                            else
                                            {
                                                //No existe en el mikrotik ahora si podemos meter el nuevo ip
                                                //Insertamos en mikrotik
                                                string idCreado = mikrotik.CrearFibra(Usuario.Usuario, IPDisponibleFibra.Result, PlanNuevo.Nombre);

                                                PlanModel objPlan = new PlanModel();
                                                objPlan.Velocidad = PlanNuevo.Velocidad;
                                                objPlan.IsAntena = false;
                                                var result = obj.SavePlanByMigracion(objPlan);
                                                if (result.Result == 0)
                                                {
                                                    await obj.SaveTiempoCambioEstatus(item.Id, "Error", "No se logro guardar el plan para la solicitud asignada en la base de datos favor de revisar.");
                                                    continue;
                                                }
                                                string IdPlanInterno = mikrotik.BuscarPerfil(PlanNuevo.Nombre);
                                                if (IdPlanInterno == string.Empty)
                                                {
                                                    await obj.SaveTiempoCambioEstatus(item.Id, "Error", "No se logro extraer el perfil del plan para la solicitud asignada en el mikrotik, es posible que lo hayan borrado fuera del sistema. Favor de revisar.");
                                                    continue;
                                                }
                                                objPlan.Id = result.Result;
                                                PlanAnidadoModel objAnidado = new PlanAnidadoModel();
                                                objAnidado.IdMikrotik = item.IdMikrotikReceptor;
                                                objAnidado.IdPlanInterno = IdPlanInterno;
                                                objAnidado.IdPlan = objPlan.Id;
                                                objAnidado.IsAntena = false;
                                                objAnidado.Id = 0;
                                                var ress = obj.SavePlanAnidadoByMigracion(objAnidado);

                                                SaveUsuariosGeneralModel objuser = new SaveUsuariosGeneralModel();
                                                objuser.IdMikrotik = item.IdMikrotikReceptor;
                                                objuser.Nombre = Usuario.Usuario;
                                                objuser.Address = IPDisponibleFibra.Result;
                                                objuser.IdInterno = idCreado;
                                                objuser.Estatus = "Activo";
                                                objuser.Id = Usuario.Id;
                                                objuser.IdPlan = objPlan.Id;
                                                var res = obj.SaveUsuariosGeneral(objuser, 1).Result;
                                                if (item.Modo == "Permanente")
                                                    await obj.SaveTiempoCambioEstatus(item.Id, "Completado", "Se transfirio exitosamente al usuario " + Usuario.Usuario + " de fibra a antena");
                                                else
                                                {
                                                    if (Modo == "Pendiente")
                                                        await obj.SaveTiempoCambioEstatus(item.Id, "Ejecutando", "Se transfirio exitosamente al usuario " + Usuario.Usuario + " de fibra a antena");
                                                    else
                                                        await obj.SaveTiempoCambioEstatus(item.Id, "Completado", "Se transfirio exitosamente al usuario " + Usuario.Usuario + " de fibra a antena");
                                                }
                                                mikrotik.EliminarQueuePorNombre(Usuario.Usuario);
                                                mikrotik.EliminarAntena(Usuario.IdInterno);
                                                HistorialMovimientosModel H = new HistorialMovimientosModel
                                                {
                                                    Id = 0,
                                                    Descripcion = "Se movio el usuario " + Usuario.Usuario + " de antena ha fibra dentro del mismo mikrotik " + NombreMikrotikConectado,
                                                    Pagina = "Servicio automatico de planes",
                                                    IdUsuario = 1,
                                                    Estatus = false
                                                };    //solo quedara registrado en el sistema mas no afectara a mikrotik
                                                await obj.SaveHistorialMovimientos(H);
                                            }
                                        }
                                        else
                                        {
                                        NuevaIpAddressFibra:
                                            //Se acabaron las ips disponibles de esa serie 
                                            var IPDisponibleAddress = obj.GetIPDisponibleAdresslist(item.IdMikrotikReceptor, false);
                                            var ExisteAddresList = mikrotik.BuscarPoolbyAddress(IPDisponibleAddress.Result);
                                            string IpExist = obj.GetIPExist(item.IdMikrotikReceptor, false, IPDisponibleAddress.Result).Result;

                                            if (IpExist == string.Empty && ExisteAddresList == true)
                                            {
                                                //No existe en la base pero si en el mikrotik
                                                //Lo introduciremos para que lo saltemos y no recorreremos su serie
                                                await obj.SavePool(item.IdMikrotikReceptor, IPDisponibleAddress.Result, true);
                                                HistorialMovimientosModel H = new HistorialMovimientosModel
                                                {
                                                    Id = 0,
                                                    Descripcion = "La ip " + IPDisponibleAddress.Result + " se encontro en el addres list del mikrotik " + NombreMikrotikConectado + " pero no esta registrado en la base, se agregara a la base de forma automatica",
                                                    Pagina = "Servicio automatico de planes",
                                                    IdUsuario = 1,
                                                    Estatus = false
                                                };
                                                await obj.SaveHistorialMovimientos(H);
                                                goto NuevaIpAddressFibra;
                                            }
                                            if (ExisteAddresList == false)
                                            {
                                                //No existe en el mikrotik se procede a instroducirlo
                                                var result = mikrotik.AgregarPool(IPDisponibleAddress.Result);
                                                string text = result == true ? "La ip " + IPDisponibleAddress.Result + " no se encontro en el pool del mikrotik " + NombreMikrotikConectado + ", se agregara a la base e introducira en el mikrotik de forma automatica" :
                                                    "La ip " + IPDisponibleAddress.Result + " no se logro introducir en el pool del mikrotik " + NombreMikrotikConectado;
                                                bool Estatushistory = result == true ? false : true;
                                                HistorialMovimientosModel H = new HistorialMovimientosModel
                                                {
                                                    Id = 0,
                                                    Descripcion = text,
                                                    Pagina = "Servicio automatico de planes",
                                                    IdUsuario = 1,
                                                    Estatus = Estatushistory
                                                };
                                                await obj.SaveHistorialMovimientos(H);
                                                if (Estatushistory == true)
                                                {
                                                    await obj.SaveTiempoCambioEstatus(item.Id, "Error", "No se logro introducir la ip en el pool del mikrotik " + NombreMikrotikConectado + ", se cancela la solicitud");
                                                    continue;
                                                }
                                                else
                                                {
                                                    goto buscaotraipFibra;
                                                }
                                            }
                                            if (IpExist != string.Empty && ExisteAddresList == true)
                                            {
                                                //Existe en el mikrotik y tambien en la base
                                                goto buscaotraipFibra;
                                            }
                                        }
                                    }
                                }
                            }
                            if (item.Programacion == "Suspensión")
                            {
                                var PlanActual = await obj.GetPlanById(item.IdPlanActual);
                                bool Results1 = false;
                                bool Results2 = false;
                                string Status = Modo == "Pendiente" ? "Activo" : "Inactivo";
                                if (item.Modo != "Permanente")
                                {
                                    if (PlanActual.IsAntena == true)
                                    {
                                        Results1 = mikrotik.CambiarEstatusAntena(Usuario.IdInterno, Status);
                                        Results2 = mikrotik.CambiarEstatusQueues(Usuario.Usuario, Status);
                                    }
                                    else
                                    {
                                        Result1 = mikrotik.CambiarEstatusFibra(Usuario.IdInterno, Status);
                                        Results2 = true;
                                    }
                                    string nuevoEstatus = Modo == "Pendiente" ? "Inactivo" : "Activo";
                                    var Res = await obj.UpdateEstatusGeneral(Usuario.Id, nuevoEstatus, 1);

                                    HistorialMovimientosModel H = new HistorialMovimientosModel
                                    {
                                        Id = 0,
                                        Descripcion = "Se " + nuevoEstatus + " el usuario " + Usuario.Usuario + " de forma automatica",
                                        Pagina = "Servicio automatico de desactivación",
                                        IdUsuario = 1,
                                        Estatus = false
                                    };
                                    await obj.SaveHistorialMovimientos(H);

                                    if (Modo == "Pendiente")
                                        await obj.SaveTiempoCambioEstatus(item.Id, "Ejecutando", "Se " + nuevoEstatus);
                                    else
                                        await obj.SaveTiempoCambioEstatus(item.Id, "Completado", "Se " + nuevoEstatus);


                                }
                                else
                                {
                                    if (PlanActual.IsAntena == true)
                                    {
                                        mikrotik.EliminarQueuePorNombre(Usuario.Usuario);
                                        mikrotik.EliminarAntena(Usuario.IdInterno);
                                    }
                                    else
                                    {
                                        mikrotik.EliminarFibra(Usuario.IdInterno);
                                        mikrotik.DeleteInterfacebyName(Usuario.Usuario);
                                    }
                                    obj.UpdateEstatusGeneral(Usuario.Id, "Eliminado", 1).Wait();
                                    HistorialMovimientosModel H = new HistorialMovimientosModel
                                    {
                                        Id = 0,
                                        Descripcion = "Se elimino el usuario " + Usuario.Usuario + " de forma automatica",
                                        Pagina = "Servicio automatico de eliminación",
                                        IdUsuario = 1,
                                        Estatus = false
                                    };
                                    await obj.SaveHistorialMovimientos(H);
                                    await obj.SaveTiempoCambioEstatus(item.Id, "Completado", "Se elimino");

                                }
                            }
                            if (item.Programacion == "Reactivación")
                            {
                                var PlanActual = await obj.GetPlanById(item.IdPlanActual);
                                bool Results1 = false;
                                bool Results2 = false;
                                string Status = Modo == "Pendiente" ? "Inactivo" : "Activo";
                                if (PlanActual.IsAntena == true)
                                {
                                    Results1 = mikrotik.CambiarEstatusAntena(Usuario.IdInterno, Status);
                                    Results2 = mikrotik.CambiarEstatusQueues(Usuario.Usuario, Status);
                                }
                                else
                                {
                                    Result1 = mikrotik.CambiarEstatusFibra(Usuario.IdInterno, Status);
                                    Results2 = true;
                                }
                                string nuevoEstatus = Modo == "Pendiente" ? "Activo" : "Inactivo";
                                var Res = await obj.UpdateEstatusGeneral(Usuario.Id, nuevoEstatus, 1);

                                HistorialMovimientosModel H = new HistorialMovimientosModel
                                {
                                    Id = 0,
                                    Descripcion = "Se " + nuevoEstatus + " el usuario " + Usuario.Usuario + " de forma automatica",
                                    Pagina = "Servicio automatico de reactivación",
                                    IdUsuario = 1,
                                    Estatus = false
                                };
                                await obj.SaveHistorialMovimientos(H);
                                if (item.Modo == "Permanente")
                                    await obj.SaveTiempoCambioEstatus(item.Id, "Completado", "Se " + nuevoEstatus);
                                else
                                {
                                    if (Modo == "Pendiente")
                                        await obj.SaveTiempoCambioEstatus(item.Id, "Ejecutando", "Se " + nuevoEstatus);
                                    else
                                        await obj.SaveTiempoCambioEstatus(item.Id, "Completado", "Se " + nuevoEstatus);
                                }
                               
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
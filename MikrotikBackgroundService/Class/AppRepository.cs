using Microsoft.Data.SqlClient;
using MikrotikBackgroundService.Model;
using System.Net.NetworkInformation;

namespace MikrotikBackgroundService.Class
{
    public class AppRepository : IDisposable
    {
        public string MikrotikConnection { get; set; }
        public AppRepository(bool isUnitOfWork = false)
        {
            MikrotikConnection = "Data Source=DESKTOP-K9P8F3O;Initial Catalog=Mikrotiks;User ID=sa;Password=admin123;Trust Server Certificate=True";
        }
        public void Dispose()
        {
            GC.Collect();
        }
        #region PlanesAnidados
        public async Task<int> SavePlanAnidadoByMigracion(PlanAnidadoModel obj)
        {
            try
            {
                using (SqlConnection sql = new SqlConnection(MikrotikConnection))
                {
                    using (SqlCommand cmd = new SqlCommand("SavePlanAnidadoByMigracion", sql))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@IdMikrotik", obj.IdMikrotik));
                        cmd.Parameters.Add(new SqlParameter("@IdPlanInterno", obj.IdPlanInterno));
                        cmd.Parameters.Add(new SqlParameter("@IdPlan", obj.IdPlan));
                        cmd.Parameters.Add(new SqlParameter("@IsAntena", obj.IsAntena));
                        SqlParameter outputParam = new SqlParameter("@VResp", System.Data.SqlDbType.Int)
                        {
                            Direction = System.Data.ParameterDirection.Output
                        };
                        cmd.Parameters.Add(outputParam);
                        await sql.OpenAsync().ConfigureAwait(false);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        int idGenerado = (outputParam.Value != DBNull.Value) ? Convert.ToInt32(outputParam.Value) : 0;

                        return idGenerado;
                    }
                }
            }
            catch (Exception ex)
            {
                return 0;
            }
        }
        #endregion
        #region Planes
        public async Task<int> SavePlanByMigracion(PlanModel obj)
        {
            try
            {
                using (SqlConnection sql = new SqlConnection(MikrotikConnection))
                {
                    using (SqlCommand cmd = new SqlCommand("SavePlanByMigracion", sql))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@Velocidad", obj.Velocidad));
                        cmd.Parameters.Add(new SqlParameter("@IsAntena", obj.IsAntena));
                        SqlParameter outputParam = new SqlParameter("@VResp", System.Data.SqlDbType.Int)
                        {
                            Direction = System.Data.ParameterDirection.Output
                        };
                        cmd.Parameters.Add(outputParam);
                        await sql.OpenAsync().ConfigureAwait(false);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        int idGenerado = (outputParam.Value != DBNull.Value) ? Convert.ToInt32(outputParam.Value) : 0;

                        return idGenerado;
                    }
                }
            }
            catch (Exception ex)
            {
                return 0;
            }
        }
        #endregion
        #region HistorialMovimientos
        public async Task<bool> SaveHistorialMovimientos(HistorialMovimientosModel obj)
        {
            try
            {
                using (SqlConnection sql = new SqlConnection(MikrotikConnection))
                {
                    using (SqlCommand cmd = new SqlCommand("SaveHistorialMovimientos", sql))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@Id", obj.Id));
                        cmd.Parameters.Add(new SqlParameter("@Descripcion", obj.Descripcion));
                        cmd.Parameters.Add(new SqlParameter("@Pagina", obj.Pagina));
                        cmd.Parameters.Add(new SqlParameter("@IdUsuario", obj.IdUsuario));
                        cmd.Parameters.Add(new SqlParameter("@Estatus", obj.Estatus));
                        await sql.OpenAsync().ConfigureAwait(false);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        #endregion
        #region IPDisponible
        public async Task<string> GetIPExist(int IdMikrotik, bool IsAntena, string IP)
        {
            try
            {
                using (SqlConnection sql = new SqlConnection(MikrotikConnection))
                {
                    using (SqlCommand cmd = new SqlCommand("GetIPExist", sql))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;

                        // Parámetros obligatorios para el SP
                        cmd.Parameters.Add(new SqlParameter("@IdMikrotik", IdMikrotik));
                        cmd.Parameters.Add(new SqlParameter("@IsAntena", IsAntena));
                        cmd.Parameters.Add(new SqlParameter("@IP", IP));
                        await sql.OpenAsync().ConfigureAwait(false);

                        // ExecuteScalarAsync ejecuta la consulta y retorna únicamente la 1ra columna de la 1ra fila
                        object result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);

                        // Si no devolvió NULL o DBNull, lo retornamos como string
                        if (result != null && result != DBNull.Value)
                        {
                            return result.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Puedes registrar la excepción si lo requieres
            }

            return string.Empty;
        }
        public async Task<string> GetIPDisponible(int IdMikrotik, bool IsAntena)
        {
            try
            {
                using (SqlConnection sql = new SqlConnection(MikrotikConnection))
                {
                    using (SqlCommand cmd = new SqlCommand("GetIPDisponible", sql))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;

                        // Parámetros obligatorios para el SP
                        cmd.Parameters.Add(new SqlParameter("@IdMikrotik", IdMikrotik));
                        cmd.Parameters.Add(new SqlParameter("@IsAntena", IsAntena));

                        await sql.OpenAsync().ConfigureAwait(false);

                        // ExecuteScalarAsync ejecuta la consulta y retorna únicamente la 1ra columna de la 1ra fila
                        object result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);

                        // Si no devolvió NULL o DBNull, lo retornamos como string
                        if (result != null && result != DBNull.Value)
                        {
                            return result.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Puedes registrar la excepción si lo requieres
            }

            return string.Empty;
        }
        public async Task<string> GetIPDisponibleAdresslist(int IdMikrotik, bool IsAntena)
        {
            try
            {
                using (SqlConnection sql = new SqlConnection(MikrotikConnection))
                {
                    using (SqlCommand cmd = new SqlCommand("GetIPDisponibleAdresslist", sql))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;

                        // Parámetros obligatorios para el SP
                        cmd.Parameters.Add(new SqlParameter("@IdMikrotik", IdMikrotik));
                        cmd.Parameters.Add(new SqlParameter("@IsAntena", IsAntena));
                        await sql.OpenAsync().ConfigureAwait(false);

                        // ExecuteScalarAsync ejecuta la consulta y retorna únicamente la 1ra columna de la 1ra fila
                        object result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);

                        // Si no devolvió NULL o DBNull, lo retornamos como string
                        if (result != null && result != DBNull.Value)
                        {
                            return result.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Puedes registrar la excepción si lo requieres
            }

            return string.Empty;
        }
        #endregion
        #region Commet
        public async Task<List<ListCommentsModel>> GetCommentsActivos(int IdMikrotik)
        {
            List<ListCommentsModel> list = new List<ListCommentsModel>();
            try
            {
                using (SqlConnection sql = new SqlConnection(MikrotikConnection))
                {
                    using (SqlCommand cmd = new SqlCommand("GetCommentsActivos", sql))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@IdMikrotik", IdMikrotik));
                        await sql.OpenAsync().ConfigureAwait(false);
                        using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                list.Add(MapToListComments(reader));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
            return list;
        }
        private ListCommentsModel MapToListComments(SqlDataReader reader)
        {
            return new ListCommentsModel()
            {
                Id = (int)reader["Id"],
                Nombre = (string)reader["Nombre"],
                IdMikrotik = (int)reader["IdMikrotik"],
                Mikrotik = (string)reader["Mikrotik"],
                Estatus = Convert.IsDBNull(reader["Estatus"]) ? string.Empty : (string)reader["Estatus"],
            };
        }
        #endregion
        #region UsuariosMikrotik
        public async Task<bool> SaveUsuariosGeneral(SaveUsuariosGeneralModel obj, int IdUsuario)
        {
            try
            {
                using (SqlConnection sql = new SqlConnection(MikrotikConnection))
                {
                    using (SqlCommand cmd = new SqlCommand("SaveUsuariosGeneral", sql))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@Nombre", obj.Nombre));
                        cmd.Parameters.Add(new SqlParameter("@Address", obj.Address));
                        cmd.Parameters.Add(new SqlParameter("@IdMikrotik", obj.IdMikrotik));
                        cmd.Parameters.Add(new SqlParameter("@IdInterno", obj.IdInterno));
                        cmd.Parameters.Add(new SqlParameter("@Estatus", obj.Estatus));
                        cmd.Parameters.Add(new SqlParameter("@IdPlan", obj.IdPlan));
                        cmd.Parameters.Add(new SqlParameter("@Responsable", IdUsuario));
                        await sql.OpenAsync().ConfigureAwait(false);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public async Task<UsuariosGeneralModel> GetUsuariosMikrotiksById(int Id)
        {
            UsuariosGeneralModel response = new UsuariosGeneralModel();
            List<UsuariosGeneralModel> list = new List<UsuariosGeneralModel>();
            try
            {
                using (SqlConnection sql = new SqlConnection(MikrotikConnection))
                {
                    using (SqlCommand cmd = new SqlCommand("GetUsuariosMikrotiksById", sql))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@Id", Id));
                        await sql.OpenAsync().ConfigureAwait(false);
                        using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                list.Add(MapToUsuarioMikrotik(reader));
                            }
                            response = list.Count() > 0 ? list[0] : new UsuariosGeneralModel();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
            return response;
        }
        private UsuariosGeneralModel MapToUsuarioMikrotik(SqlDataReader reader)
        {
            return new UsuariosGeneralModel()
            {
                Id = (int)reader["Id"],
                IdInterno = (string)reader["IdInterno"],
                Usuario = (string)reader["Usuario"],
                Estatus = (string)reader["Estatus"],
            };
        }
        public async Task<bool> UpdateEstatusGeneral(int Id, string Estatus, int Responsable)
        {
            try
            {
                using (SqlConnection sql = new SqlConnection(MikrotikConnection))
                {
                    using (SqlCommand cmd = new SqlCommand("UpdateEstatusGeneral", sql))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@Id", Id));
                        cmd.Parameters.Add(new SqlParameter("@Estatus", Estatus));
                        cmd.Parameters.Add(new SqlParameter("@Responsable", Responsable));
                        await sql.OpenAsync().ConfigureAwait(false);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        #endregion
        #region Mikrotik
        public async Task<MikrotikModel> GetMikrotikById(int Id)
        {
            MikrotikModel response = new MikrotikModel();
            List<MikrotikModel> list = new List<MikrotikModel>();
            try
            {
                using (SqlConnection sql = new SqlConnection(MikrotikConnection))
                {
                    using (SqlCommand cmd = new SqlCommand("GetMikrotikById", sql))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@Id", Id));
                        await sql.OpenAsync().ConfigureAwait(false);
                        using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                list.Add(MapToMikrotik(reader));
                            }
                            response = list.Count() > 0 ? list[0] : new MikrotikModel();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                response = new MikrotikModel();
            }
            return response;
        }
        private MikrotikModel MapToMikrotik(SqlDataReader reader)
        {
            return new MikrotikModel()
            {
                Id = (int)reader["Id"],
                Nombre = (string)reader["Nombre"],
                IP = (string)reader["IP"],
                Port = (string)reader["Port"],
                Usuario = (string)reader["Usuario"],
                Password = (string)reader["Password"],
                Estatus = (bool)reader["Estatus"],
                Limite_Alcanzado = (bool)reader["Limite_Alcanzado"],
                PlanAceptado = (string)reader["PlanAceptado"],
            };
        }
        #endregion
        #region TiempoCambio
        public async Task<bool> SaveTiempoCambioEstatus(int Id, string Estatus, string Nota)
        {
            try
            {
                using (SqlConnection sql = new SqlConnection(MikrotikConnection))
                {
                    using (SqlCommand cmd = new SqlCommand("SaveTiempoCambioEstatus", sql))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@Id", Id));
                        cmd.Parameters.Add(new SqlParameter("@Estatus", Estatus));
                        cmd.Parameters.Add(new SqlParameter("@Nota", Nota));
                        await sql.OpenAsync().ConfigureAwait(false);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public async Task<List<TiempoCambioModel>> GetTiempoCambiobyEstatus(string Estatus)
        {
            List<TiempoCambioModel> list = new List<TiempoCambioModel>();
            try
            {
                using (SqlConnection sql = new SqlConnection(MikrotikConnection))
                {
                    using (SqlCommand cmd = new SqlCommand("GetTiempoCambiobyEstatus", sql))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@Estatus", Estatus));
                        await sql.OpenAsync().ConfigureAwait(false);
                        using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                list.Add(MapToListTiempoCambio(reader));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
            return list;
        }
        private TiempoCambioModel MapToListTiempoCambio(SqlDataReader reader)
        {
            return new TiempoCambioModel()
            {
                Id = (int)reader["Id"],
                Dias = (int)reader["Dias"],
                Horas = (int)reader["Horas"],
                FechaInicio = (DateTime)reader["FechaInicio"],
                FechaFin = (DateTime)reader["FechaFin"],
                Estatus = (string)reader["Estatus"],
                Modo = (string)reader["Modo"],
                IdUsuarioM = (int)reader["IdUsuarioM"],
                Nota = Convert.IsDBNull(reader["Nota"]) ? string.Empty : (string)reader["Nota"],
                IdPlan = (int)reader["IdPlan"],
                IdPlanActual = (int)reader["IdPlanActual"],
                IdMikrotik = (int)reader["IdMikrotik"],
                IdPlanOriginal = (int)reader["IdPlanOriginal"],
                IdMikrotikReceptor = (int)reader["IdMikrotikReceptor"],
                IdMikrotikOriginal = (int)reader["IdMikrotikOriginal"],
            };
        }
        #endregion
        #region Plan
        public async Task<bool> UpdatePlanGeneral(int Id, int IdPlan,int IdMikrotik, string Modo)
        {
            try
            {
                using (SqlConnection sql = new SqlConnection(MikrotikConnection))
                {
                    using (SqlCommand cmd = new SqlCommand("UpdatePlanGeneral", sql))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@Id", Id));
                        cmd.Parameters.Add(new SqlParameter("@IdPlan", IdPlan));
                        cmd.Parameters.Add(new SqlParameter("@IdMikrotik", IdMikrotik));
                        cmd.Parameters.Add(new SqlParameter("@Modo", Modo));
                        await sql.OpenAsync().ConfigureAwait(false);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public async Task<PlanModel> GetPlanById(int Id)
        {
            PlanModel response = new PlanModel();
            List<PlanModel> list = new List<PlanModel>();
            try
            {
                using (SqlConnection sql = new SqlConnection(MikrotikConnection))
                {
                    using (SqlCommand cmd = new SqlCommand("GetPlanById", sql))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@Id", Id));
                        await sql.OpenAsync().ConfigureAwait(false);
                        using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                list.Add(MapToPlan(reader));
                            }
                            response = list.Count() > 0 ? list[0] : new PlanModel();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                response = new PlanModel();
            }
            return response;
        }
        private PlanModel MapToPlan(SqlDataReader reader)
        {
            return new PlanModel()
            {
                Id = (int)reader["Id"],
                Nombre = (string)reader["Nombre"],
                Precio = Convert.IsDBNull(reader["Precio"]) ? 0 : (decimal)reader["Precio"],
                Velocidad = Convert.IsDBNull(reader["Velocidad"]) ? string.Empty : (string)reader["Velocidad"],
                Estatus = Convert.IsDBNull(reader["Estatus"]) ? false : (bool)reader["Estatus"],
                IsAntena = Convert.IsDBNull(reader["IsAntena"]) ? false : (bool)reader["IsAntena"],
            };
        }
        #endregion
        #region wireless
        public async Task<bool> SavePool(int IdMikrotik, string IP, bool Completado)
        {
            try
            {
                using (SqlConnection sql = new SqlConnection(MikrotikConnection))
                {
                    using (SqlCommand cmd = new SqlCommand("SavePool", sql))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@IdMikrotik", IdMikrotik));
                        cmd.Parameters.Add(new SqlParameter("@IP", IP));
                        cmd.Parameters.Add(new SqlParameter("@Completado", Completado));
                        await sql.OpenAsync().ConfigureAwait(false);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public async Task<bool> SaveWireless(InsertListWirelessModel obj)
        {
            try
            {
                using (SqlConnection sql = new SqlConnection(MikrotikConnection))
                {
                    using (SqlCommand cmd = new SqlCommand("SaveWireless", sql))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@Address", obj.Address));
                        cmd.Parameters.Add(new SqlParameter("@Comment", obj.Comment));
                        cmd.Parameters.Add(new SqlParameter("@IdMikrotik", obj.IdMikrotik));
                        cmd.Parameters.Add(new SqlParameter("@Estatus", obj.Estatus));
                        cmd.Parameters.Add(new SqlParameter("@IdInterno", obj.IdInterno));
                        cmd.Parameters.Add(new SqlParameter("@Completado", obj.Completado));
                        await sql.OpenAsync().ConfigureAwait(false);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        #endregion
    }
}

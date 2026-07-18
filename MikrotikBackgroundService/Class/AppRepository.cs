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
        #region UsuariosMikrotik
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
                IdMikrotik = (int)reader["IdMikrotik"],
                IdPlanOriginal = (int)reader["IdPlanOriginal"],
            };
        }
        #endregion
        #region Plan
        public async Task<bool> UpdatePlanGeneral(int Id, int IdPlan, string Modo)
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

    }
}

using Microsoft.Data.SqlClient;

namespace MikrotikBackgroundService.Class
{
    public class AppRepository : IDisposable
    {
        public string MikrotikConnection { get; set; }
        public AppRepository(bool isUnitOfWork = false)
        {
            MikrotikConnection = "Data Source=.;Initial Catalog=Mikrotiks;User ID=sa;Password=admin123;Trust Server Certificate=True";
        }
        public void Dispose()
        {
            GC.Collect();
        }
        public async Task<bool> UpdateStatusBanco(int Id)
        {
            try
            {
                using (SqlConnection sql = new SqlConnection(MikrotikConnection))
                {
                    using (SqlCommand cmd = new SqlCommand("UpdateStatusBanco", sql))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@Id", Id));
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
    }
}

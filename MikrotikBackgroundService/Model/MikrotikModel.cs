namespace MikrotikBackgroundService.Model
{
    public class MikrotikModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string IP { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
        public string Usuario { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool Estatus { get; set; }
        public bool Limite_Alcanzado { get; set; }
        public string PlanAceptado { get; set; } = string.Empty;
    }
}

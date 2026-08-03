namespace MikrotikBackgroundService.Model
{
    public class InsertListWirelessModel
    {
        public string Address { get; set; }
        public string Comment { get; set; }
        public int IdMikrotik { get; set; }
        public string Estatus { get; set; }
        public string IdInterno { get; set; } //Es el id que da el mikrotik real
        public bool Completado { get; set; }
    }
}

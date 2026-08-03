namespace MikrotikBackgroundService.Model
{
    public class TiempoCambioModel
    {
        public int Id { get; set; }
        public int Dias { get; set; }
        public int Horas { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public string Estatus { get; set; }
        public string Modo { get; set; }
        public int IdUsuarioM { get; set; }
        public string Nota { get; set; }
        public int IdPlan { get; set; }
        public int IdPlanActual { get; set; }
        public int IdMikrotikReceptor { get; set; }
        public string Programacion { get; set; }
        public int IdMikrotik { get; set; }
        public int IdPlanOriginal { get; set; }
        public int IdMikrotikOriginal { get; set; }
    }
}

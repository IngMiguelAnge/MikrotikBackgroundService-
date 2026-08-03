namespace MikrotikBackgroundService.Model
{
    public class PlanAnidadoModel
    {
        public int Id { get; set; }
        public int IdMikrotik { get; set; }
        public string IdPlanInterno { get; set; }
        public int IdPlan { get; set; }
        public bool IsAntena { get; set; }
    }
}

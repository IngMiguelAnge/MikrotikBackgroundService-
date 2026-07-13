namespace MikrotikBackgroundService.Model
{
    public class PlanModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public decimal Precio { get; set; }
        public string Velocidad { get; set; }
        public bool Estatus { get; set; }
        public bool IsAntena { get; set; }
    }
}

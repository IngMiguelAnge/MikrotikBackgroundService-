using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MikrotikBackgroundService.Model
{
    public class ListCommentsModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public int IdMikrotik { get; set; }
        public string Mikrotik { get; set; }
        public string Estatus { get; set; }
    }
}

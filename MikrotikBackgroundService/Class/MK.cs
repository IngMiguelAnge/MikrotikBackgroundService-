using MikrotikBackgroundService.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MikrotikBackgroundService.Class
{
    public class MK
    {
        Stream connection;
        TcpClient con;
        string _ip;
        int _port;

        // El constructor ahora solo guarda los datos, NO CONECTA
        public MK(string ip, int port)
        {
            _ip = ip;
            _port = port;
        }
        public bool ConectarYLogin(string user, string pass)
        {
            try
            {
                con = new TcpClient();
                con.NoDelay = true; // Desactiva el algoritmo de Nagle (crucial para CCR)
                con.Connect(_ip, _port);
                connection = con.GetStream();

                // Esperamos 50ms a que el CCR estabilice la conexión antes de mandar los bytes
                System.Threading.Thread.Sleep(50);

                return this.Login(user, pass);
            }
            catch
            {
                return false;
            }
        }
        public bool Close()
        {
            try
            {
                if (connection != null)
                {
                    // Intentar enviar /quit solo si el socket sigue vivo
                    if (con != null && con.Connected)
                    {
                        Send("/quit", true);
                        System.Threading.Thread.Sleep(50);
                    }
                    connection.Dispose(); // Usar Dispose es más agresivo y limpio
                }
                if (con != null) con.Close();
                return true;
            }
            catch { return false; }
        }
        public void Send(string co)
        {
            Send(co, false);
        }
        public void Send(string co, bool endsentence = false)
        {
            byte[] bajty = Encoding.Default.GetBytes(co); // v7 prefiere UTF8
            byte[] velikost = EncodeLength(bajty.Length);

            byte[] paquete = new byte[velikost.Length + bajty.Length + (endsentence ? 1 : 0)];
            System.Buffer.BlockCopy(velikost, 0, paquete, 0, velikost.Length);
            System.Buffer.BlockCopy(bajty, 0, paquete, velikost.Length, bajty.Length);

            if (endsentence) paquete[paquete.Length - 1] = 0;

            connection.Write(paquete, 0, paquete.Length);
            // NO uses Flush después de cada palabra, deja que el buffer de red decida
        }
        private byte[] EncodeLength(int delka)
        {
            if (delka < 128)
            {
                return new byte[] { (byte)delka };
            }
            else if (delka < 16384)
            {
                return new byte[] { (byte)((delka >> 8) | 0x80), (byte)(delka & 0xFF) };
            }
            else if (delka < 2097152)
            {
                return new byte[] { (byte)((delka >> 16) | 0xC0), (byte)((delka >> 8) & 0xFF), (byte)(delka & 0xFF) };
            }
            return new byte[] { (byte)delka };
        }
        public List<string> Read()
        {
            List<string> output = new List<string>();

            // 1. Espera activa más flexible
            int waitAttempts = 100;
            while (con.Available == 0 && waitAttempts > 0)
            {
                System.Threading.Thread.Sleep(20);
                waitAttempts--;
            }

            if (con.Available == 0)
            {
                System.Diagnostics.Debug.WriteLine("El router no mandó datos de respuesta.");
                return output;
            }

            // Variable para rastrear la última etiqueta de bloque (ej. !done, !re, !trap)
            string lastTag = "";

            while (true)
            {
                int curByte = connection.ReadByte();
                if (curByte == -1) break;

                // Decodificación de longitud del estándar RouterOS API
                long count = 0;
                if (curByte < 0x80) { count = curByte; }
                else if (curByte < 0xC0) { count = ((curByte ^ 0x80) << 8) + connection.ReadByte(); }
                else if (curByte < 0xE0) { count = ((curByte ^ 0xC0) << 16) + (connection.ReadByte() << 8) + connection.ReadByte(); }
                else if (curByte < 0xF0) { count = ((curByte ^ 0xE0) << 24) + (connection.ReadByte() << 16) + (connection.ReadByte() << 8) + connection.ReadByte(); }
                else if (curByte == 0xF0) { count = (connection.ReadByte() << 24) + (connection.ReadByte() << 16) + (connection.ReadByte() << 8) + connection.ReadByte(); }

                // Si la palabra tiene longitud 0, indica FIN DE SENTENCIA
                if (count == 0)
                {
                    // Si la etiqueta del bloque actual fue !done, !trap o !fatal, TERMINAMOS
                    if (lastTag == "!done" || lastTag == "!trap" || lastTag == "!fatal")
                    {
                        break;
                    }
                    continue;
                }

                byte[] buffer = new byte[count];
                int read = 0;
                while (read < count)
                {
                    int result = connection.Read(buffer, read, (int)count - read);
                    if (result <= 0) break;
                    read += result;
                }

                // Se recomienda UTF8 para compatibilidad con MikroTik v7+
                string word = Encoding.UTF8.GetString(buffer);
                output.Add(word);

                // Guardamos las etiquetas especiales (!done, !re, !trap, etc.)
                if (word.StartsWith("!"))
                {
                    lastTag = word;
                }
            }

            return output;
        }
        public bool Login(string username, string password)
        {
            try
            {
                // Forzamos el envío de los 3 parámetros en un solo Flush
                Send("/login");
                Send("=name=" + username);
                Send("=password=" + password, true);
                connection.Flush(); // Solo un Flush al final de la frase

                // Espera obligatoria para el CCR2116 (proceso de autenticación interno)
                System.Threading.Thread.Sleep(250);

                List<string> respuesta = Read();

                if (respuesta.Count == 0)
                {
                    // Si llega vacío, intentamos una segunda lectura rápida
                    System.Threading.Thread.Sleep(250);
                    respuesta = Read();
                }

                return respuesta.Any(s => s.Contains("!done")) && !respuesta.Any(s => s.Contains("!trap"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error crítico: " + ex.Message);
                return false;
            }
        }
        public string VerIdQueuebyAddress(string IPDisponible)
        {
            string Id = string.Empty;
            try
            {
                Send("/queue/simple/print");
                Send("=.proplist=.id");// Esto ayuda a que el router no se pierda enviando datos extra
                Send("?address=" + IPDisponible, true);
                foreach (string row in Read())
                {
                    if (row.StartsWith("!re"))
                    {
                        continue;
                    }
                    if (row.StartsWith("!done")) break;

                    if (row.StartsWith("="))
                    {
                        string[] parts = row.Split(new char[] { '=' }, 3);
                        if (parts.Length < 3) continue;

                        string key = parts[1];
                        string value = parts[2];

                        if (key == ".id")
                            return value;
                    }
                }
            }
            catch (Exception e)
            {

            }
            return Id;
        }
        public string VerIdQueue(string name)
        {
            string Id = string.Empty;
            try
            {
                Send("/queue/simple/print");
                Send("=.proplist=.id");// Esto ayuda a que el router no se pierda enviando datos extra
                Send("?name=" + name, true);
                foreach (string row in Read())
                {
                    if (row.StartsWith("!re"))
                    {
                        continue;
                    }
                    if (row.StartsWith("!done")) break;

                    if (row.StartsWith("="))
                    {
                        string[] parts = row.Split(new char[] { '=' }, 3);
                        if (parts.Length < 3) continue;

                        string key = parts[1];
                        string value = parts[2];

                        if (key == ".id")
                            return value;
                    }
                }
            }
            catch (Exception e)
            {

            }
            return Id;
        }
        public void EliminarQueuePorNombre(string nombreQueue)
        {
            try
            {
                // 1. Buscamos el .id de la queue que coincida con el nombre
                Send("/queue/simple/print");
                Send("?name=" + nombreQueue); // Filtro de búsqueda
                Send("=.proplist=.id", true);

                List<string> respuesta = Read();
                string idEncontrado = "";

                // Procesamos la respuesta para extraer el .id
                foreach (string row in respuesta)
                {
                    if (row.StartsWith("=.id="))
                    {
                        idEncontrado = row.Split('=')[2];
                        break;
                    }
                }

                // 2. Si encontramos el ID, procedemos a borrar
                if (!string.IsNullOrEmpty(idEncontrado))
                {
                    Send("/queue/simple/remove");
                    Send("=.id=" + idEncontrado, true);
                    Read(); // Limpiamos el buffer de respuesta
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al eliminar Queue: " + ex.Message);
            }
        }
        public bool EliminarFibra(string idInterno)
        {
            try
            {
                // 1. Enviamos el comando de remoción para el módulo PPP Secret
                Send("/ppp/secret/remove");

                // 2. Pasamos el ID del registro que queremos borrar
                // El parámetro 'true' indica que es el final de la sentencia
                Send("=.id=" + idInterno, true);

                // 3. Leemos la respuesta para limpiar el buffer
                List<string> respuesta = Read();

                // Verificamos si hubo algún error (!trap)
                foreach (string r in respuesta)
                {
                    if (r.StartsWith("!trap")) return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al eliminar Secret: " + ex.Message);
                return false;
            }
        }
        public void EliminarAntena(string idInterno)
        {
            try
            {
                // El comando es /path/del/modulo/remove
                Send("/ip/firewall/address-list/remove");
                // Se pasa el atributo .id indispensable para borrar
                Send("=.id=" + idInterno, true);

                List<string> respuesta = Read();
                // Opcional: Verificar si MikroTik respondió con !done o !trap (error)
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al eliminar AddressList: " + ex.Message);
            }
        }
        public bool CambiarEstatusFibra(string Id, string Estatus)
        {
            try
            {
                Send("/ppp/secret/set");
                Send("=.id=" + Id);
                if (Estatus == "Activo")
                    Send("=disabled=yes", true);
                else
                    Send("=disabled=no", true);

                List<string> respuesta = Read();

                // Si el router responde con !trap es que hubo un error (ej: el usuario no existe)
                return !respuesta.Any(r => r.Contains("!trap"));
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public bool CambiarEstatusQueues(string Name, string Estatus)
        {
            try
            {
                string Id = VerIdQueue(Name);
                Send("/queue/simple/set");
                Send("=.id=" + Id);
                if (Estatus == "Activo")
                    Send("=disabled=yes", true);
                else
                    Send("=disabled=no", true);

                List<string> respuesta = Read();
                return !respuesta.Any(r => r.Contains("!trap"));
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public bool CambiarEstatusAntena(string Id, string Estatus)
        {
            try
            {
                Send("/ip/firewall/address-list/set");
                Send("=.id=" + Id);
                if (Estatus == "Activo")
                    Send("=disabled=yes", true);
                else
                    Send("=disabled=no", true);

                List<string> respuesta = Read();
                // Si no hay errores (!trap), asumimos éxito
                return !respuesta.Any(r => r.Contains("!trap"));
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public bool AgregarAntena(string listName, string ipAddress, string comment = "", bool disabled = false)
        {
            try
            {
                // 1. Comando principal para agregar
                Send("/ip/firewall/address-list/add");

                // 2. Parámetros obligatorios
                Send("=list=" + listName);
                Send("=address=" + ipAddress);

                // 3. Parámetros opcionales
                if (!string.IsNullOrEmpty(comment))
                {
                    Send("=comment=" + comment);
                }

                // El 'true' en el último Send envía la señal de fin de frase al RouterOS
                string statusDisabled = disabled ? "no" : "yes";
                Send("=disabled=" + statusDisabled, true);

                // 4. Leer la respuesta y verificar que no devuelva un error (!trap)
                List<string> respuesta = Read();
                return !respuesta.Any(r => r.Contains("!trap"));
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        private string FormatearNumero(string numeroStr)
        {
            if (!long.TryParse(numeroStr, out long bits)) return "0";

            if (bits >= 1000000) // Si es mayor o igual a 1 Megabit
                return (bits / 1000000) + "M";

            if (bits >= 1000) // Si es mayor o igual a 1 Kilobit
                return (bits / 1000) + "k";

            return bits.ToString(); // Si es menor a 1k, lo deja igual
        }
        public string VerVelocidadQueue(string name)
        {
            string MaxLimit = string.Empty;
            try
            {
                Send("/queue/simple/print");
                Send("=.proplist=max-limit");// Esto ayuda a que el router no se pierda enviando datos extra
                Send("?name=" + name, true);
                foreach (string row in Read())
                {
                    if (row.StartsWith("!re"))
                    {
                        continue;
                    }
                    if (row.StartsWith("!done")) break;

                    if (row.StartsWith("="))
                    {
                        string[] parts = row.Split(new char[] { '=' }, 3);
                        if (parts.Length < 3) continue;

                        string key = parts[1];
                        string value = parts[2];

                        if (key == "max-limit")
                        {
                            if (value.Contains("/"))
                            {
                                string[] partes = value.Split('/');
                                return $"{FormatearNumero(partes[0])} / {FormatearNumero(partes[1])}";
                            }
                            return FormatearNumero(value);
                        }
                    }
                }
            }
            catch (Exception e)
            {

            }
            return MaxLimit;
        }
        public List<AntenasModel> VerAntenasbyComment(string name)
        {
            List<AntenasModel> listaFinal = new List<AntenasModel>();
            try
            {
                Send("/ip/firewall/address-list/print");
                Send("=.proplist=list,.id,address,comment,disabled");
                Send("?comment=" + name); // Busca coincidencia exacta en el comentario
                Send("", true);
                AntenasModel currentObj = null;
                List<string> respuesta = Read();
                bool objetoValido = true;
                AppRepository obj = new AppRepository();
                foreach (string row in respuesta)
                {
                    if (row.StartsWith("!re"))
                    {
                        currentObj = new AntenasModel();
                        currentObj.comment = "Sin Comentario";
                        objetoValido = true;
                        continue;
                    }

                    if (!objetoValido && row.StartsWith("=")) continue;

                    if (row.StartsWith("!done") ||
                        row.StartsWith("!done") && (name != string.Empty && currentObj.comment.Contains(name))
                        ) break;

                    if (row.StartsWith("="))
                    {
                        string[] parts = row.Split(new char[] { '=' }, 3);
                        if (parts.Length < 3) continue;

                        string key = parts[1];
                        string value = parts[2];
                        //ListWireless
                        switch (key)
                        {
                            case "list":
                                value = value.Replace("\r", "").Replace("\n", "").Trim();
                                break;
                            case ".id": currentObj.id = value; break;
                            case "comment":
                                string valueLimpio = value.Replace("\r", "").Replace("\n", "").Trim();
                                currentObj.comment = value;
                                currentObj.idplan = string.Empty;
                                currentObj.velocidad = VerVelocidadQueue(value.Replace("\r", "").Replace("\n", "").Trim());
                                break;
                            case "address":
                                currentObj.address = value;
                                if (currentObj != null && !string.IsNullOrEmpty(currentObj.address))
                                {
                                    // Evitar duplicados si el !re se procesa varias veces
                                    if (!listaFinal.Any(a => a.id == currentObj.id))
                                    {
                                        listaFinal.Add(currentObj);
                                    }
                                }

                                break;
                            case "disabled": currentObj.estatus = value == "false" ? "Activo" : "Inactivo"; break;
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en ver antenas: " + ex.Message);
            }
            return name != string.Empty ? listaFinal.Where(r => r.comment == name).ToList() : listaFinal;
        }
        public List<AntenasModel> VerAntenasbyAddres(string name)
        {
            List<AntenasModel> listaFinal = new List<AntenasModel>();
            try
            {
                Send("/ip/firewall/address-list/print");
                Send("=.proplist=list,.id,address,comment,disabled");
                Send("?comment=" + name); // Busca coincidencia exacta en el comentario
                Send("", true);
                AntenasModel currentObj = null;
                List<string> respuesta = Read();
                bool objetoValido = true;
                AppRepository obj = new AppRepository();
                foreach (string row in respuesta)
                {
                    if (row.StartsWith("!re"))
                    {
                        currentObj = new AntenasModel();
                        currentObj.comment = "Sin Comentario";
                        objetoValido = true;
                        continue;
                    }

                    if (!objetoValido && row.StartsWith("=")) continue;

                    if (row.StartsWith("!done") ||
                        row.StartsWith("!done") && (name != string.Empty && currentObj.comment.Contains(name))
                        ) break;

                    if (row.StartsWith("="))
                    {
                        string[] parts = row.Split(new char[] { '=' }, 3);
                        if (parts.Length < 3) continue;

                        string key = parts[1];
                        string value = parts[2];
                        //ListWireless
                        switch (key)
                        {
                            case "list":
                                value = value.Replace("\r", "").Replace("\n", "").Trim();
                                break;
                            case ".id": currentObj.id = value; break;
                            case "comment":
                                string valueLimpio = value.Replace("\r", "").Replace("\n", "").Trim();
                                currentObj.comment = value;
                                currentObj.idplan = string.Empty;
                                currentObj.velocidad = VerVelocidadQueue(value.Replace("\r", "").Replace("\n", "").Trim());
                                break;
                            case "address":
                                currentObj.address = value;
                                if (currentObj != null && !string.IsNullOrEmpty(currentObj.address))
                                {
                                    // Evitar duplicados si el !re se procesa varias veces
                                    if (!listaFinal.Any(a => a.id == currentObj.id))
                                    {
                                        listaFinal.Add(currentObj);
                                    }
                                }

                                break;
                            case "disabled": currentObj.estatus = value == "false" ? "Activo" : "Inactivo"; break;
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en ver antenas: " + ex.Message);
            }
            return name != string.Empty ? listaFinal.Where(r => r.comment == name).ToList() : listaFinal;
        }
        public List<AntenasModel> VerAntenasbyAddress(string IPDisponible)
        {
            List<AntenasModel> listaFinal = new List<AntenasModel>();
            try
            {
                Send("/ip/firewall/address-list/print");
                Send("=.proplist=list,.id,address,comment,disabled");
                Send("?address=" + IPDisponible);// Busca coincidencia exacta en el comentario
                Send("", true);
                AntenasModel currentObj = null;
                List<string> respuesta = Read();
                bool objetoValido = true;
                AppRepository obj = new AppRepository();
                foreach (string row in respuesta)
                {
                    if (row.StartsWith("!re"))
                    {
                        currentObj = new AntenasModel();
                        currentObj.comment = "Sin Comentario";
                        objetoValido = true;
                        continue;
                    }

                    if (!objetoValido && row.StartsWith("=")) continue;

                    if (row.StartsWith("!done") ||
                        row.StartsWith("!done") && (IPDisponible != string.Empty && currentObj.address.Contains(IPDisponible))
                        ) break;

                    if (row.StartsWith("="))
                    {
                        string[] parts = row.Split(new char[] { '=' }, 3);
                        if (parts.Length < 3) continue;

                        string key = parts[1];
                        string value = parts[2];
                        //ListWireless
                        switch (key)
                        {
                            case "list":
                                value = value.Replace("\r", "").Replace("\n", "").Trim();
                                break;
                            case ".id": currentObj.id = value; break;
                            case "comment":
                                string valueLimpio = value.Replace("\r", "").Replace("\n", "").Trim();
                                currentObj.comment = value;
                                currentObj.idplan = string.Empty;
                                currentObj.velocidad = VerVelocidadQueue(value.Replace("\r", "").Replace("\n", "").Trim());
                                break;
                            case "address":
                                currentObj.address = value;
                                if (currentObj != null && !string.IsNullOrEmpty(currentObj.address))
                                {
                                    // Evitar duplicados si el !re se procesa varias veces
                                    if (!listaFinal.Any(a => a.id == currentObj.id))
                                    {
                                        listaFinal.Add(currentObj);
                                    }
                                }

                                break;
                            case "disabled": currentObj.estatus = value == "false" ? "Activo" : "Inactivo"; break;
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en ver antenas: " + ex.Message);
            }
            return IPDisponible != string.Empty ? listaFinal.Where(r => r.address == IPDisponible).ToList() : listaFinal;
        }
        public List<AddressModel> VerAddresbyAddress(string Address)
        {
            List<AddressModel> listaFinal = new List<AddressModel>();
            try
            {
                // Enviamos el comando a la ruta de IP -> Address
                Send("/ip/address/print");
                Send("=.proplist=.id,address,comment,network,interface,actual-interface,disabled");
                Send("?address=" + Address); // <-- Filtro de coincidencia exacta
                Send("", true);
                AddressModel currentObj = null;
                List<string> respuesta = Read();
                foreach (string row in respuesta)
                {
                    // Cada vez que aparece !re, es una nueva fila/registro
                    if (row.StartsWith("!re"))
                    {
                        currentObj = new AddressModel();
                        currentObj.comment = "Sin Comentario"; // Valor por defecto
                        listaFinal.Add(currentObj);
                        continue;
                    }

                    if (row.StartsWith("!done")) break;

                    // Procesamos las propiedades del objeto actual
                    if (row.StartsWith("=") && currentObj != null)
                    {
                        string[] parts = row.Substring(1).Split(new char[] { '=' }, 2);
                        if (parts.Length < 2) continue;

                        string key = parts[0];
                        string value = parts[1];

                        switch (key)
                        {
                            case ".id": currentObj.id = value; break;
                            case "address": currentObj.address = value; break;
                            case "comment":
                                string valueLimpio = value.Replace("\r", "").Replace("\n", "").Trim();
                                byte[] bytesMalos = Encoding.GetEncoding("ISO-8859-1").GetBytes(valueLimpio);
                                currentObj.comment = Encoding.UTF8.GetString(bytesMalos);
                                break;
                            case "disabled":
                                currentObj.estatus = value == "false" ? "Activo" : "Inactivo";
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en VerAddres: " + ex.Message);
            }
            return listaFinal;
        }
        public bool AgregarIPAddress(string ipAddressWithMask, string interfaceName, string comment = "")
        {
            try
            {
                // Enviar el comando para agregar
                Send("/ip/address/add");
                Send("=address=" + ipAddressWithMask); // Ej: "192.168.88.1/24"
                Send("=interface=" + interfaceName);   // Ej: "ether1" o "bridge"

                if (!string.IsNullOrEmpty(comment))
                {
                    Send("=comment=" + comment);
                }

                // Finalizar el comando
                Send("=disabled=no", true);

                // Leer la respuesta y validar éxito
                List<string> respuesta = Read();
                return !respuesta.Any(r => r.Contains("!trap"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al agregar IP Address: " + ex.Message);
                return false;
            }
        }
        public bool CrearSimpleQueue(string name, string targetIp, string Velocidad, string comment = "")
        {
            try
            {
                // 1. Iniciar la ruta del comando add
                Send("/queue/simple/add");

                // 2. Enviar los parámetros con la sintaxis de la API de MikroTik (=propiedad=valor)
                Send("=name=" + name);
                Send("=target=" + targetIp);

                // max-limit se define como "Upload/Download" (Ejemplo: "5M/10M")
                string maxLimit = Velocidad;
                Send("=max-limit=" + maxLimit);

                if (!string.IsNullOrEmpty(comment))
                {
                    Send("=comment=" + comment);
                }

                // 3. El segundo parámetro 'true' en el último Send indica el fin de la frase/comando
                Send("=disabled=no", true);

                // 4. Leer la respuesta del MikroTik para verificar si se creó exitosamente
                foreach (string row in Read())
                {
                    if (row.StartsWith("!trap"))
                    {
                        // Un paquete !trap indica que MikroTik devolvió un error (ej. IP o Nombre ya existente)
                        return false;
                    }
                    if (row.StartsWith("!done"))
                    {
                        // !done significa que la operación terminó con éxito
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                // Manejar o registrar el error si es necesario
            }

            return false;
        }
        public bool ActualizarVelocidadQueue(string Name, string Velocidad)
        {
            try
            {
                string Id = VerIdQueue(Name);
                // El comando 'set' requiere identificar el item, usualmente por su nombre (.id o name)
                Send("/queue/simple/set");

                // Indicamos cuál queue queremos modificar usando su nombre
                Send("=.id=" + Id);

                // Construimos el string de velocidad, ej: "2M/5M" o "512k/1M"
                // MikroTik acepta perfectamente los sufijos M y k
                Send("=max-limit=" + Velocidad, true);

                // Leemos la respuesta para confirmar que no hubo errores
                foreach (string row in Read())
                {
                    if (row.StartsWith("!trap"))
                    {
                        // Si el router devuelve !trap, hubo un error (ej. el nombre no existe)
                        return false;
                    }
                    if (row.StartsWith("!done"))
                    {
                        return true; // Éxito
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }
        public bool ActualizarUsuarioPPP(string Id, string nombrePerfil, string Velocidad)
        {
            try
            {
                // Paso 1: Asegurarnos que el perfil existe
                if (!AsegurarPerfil(nombrePerfil, Velocidad)) return false;

                // Paso 2: Asignar el perfil al Secret del usuario
                Send("/ppp/secret/set");
                Send("=.id=" + Id); // Usamos el nombre como ID
                Send("=profile=" + nombrePerfil, true);

                foreach (string row in Read())
                {
                    if (row.StartsWith("!trap")) return false;
                    if (row.StartsWith("!done")) return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }
        public string BuscarPerfil(string Nombre)
        {
            string idPerfil = "";
            // 1. Enviar comando de búsqueda a MikroTik
            Send("/ppp/profile/print");
            Send("=.proplist=.id");
            Send("?name=" + Nombre, true);

            // 2. Leer la respuesta del RouterOS
            List<string> respuesta = Read();


            foreach (string linea in respuesta)
            {
                if (linea.StartsWith("=.id="))
                {
                    idPerfil = linea.Replace("=.id=", "").Trim(); // Ejemplo: "*1" o "*A"
                    break;
                }
            }

         return idPerfil;
        }
        public bool AsegurarPerfil(string nombrePerfil, string velocidad)
        {
            string idEncontrado = string.Empty;
            bool existe = false;

            try
            {
                // --- PASO 1: BÚSQUEDA ---
                Send("/ppp/profile/print");
                Send("=.proplist=.id");
                Send("?name=" + nombrePerfil, true);

                // Leemos TODA la respuesta del print hasta el !done
                foreach (string row in Read())
                {
                    if (row.StartsWith("!re"))
                    {
                        existe = true;
                    }
                    else if (row.StartsWith("="))
                    {
                        string[] parts = row.Split(new char[] { '=' }, 3);
                        if (parts.Length >= 3 && parts[1] == ".id")
                        {
                            idEncontrado = parts[2];
                        }
                    }
                    else if (row.StartsWith("!done"))
                    {
                        break; // Salimos del foreach del print
                    }
                }

                // --- PASO 2: ACCIÓN (SET o ADD) ---
                if (existe && !string.IsNullOrEmpty(idEncontrado))
                {
                    Send("/ppp/profile/set");
                    Send("=.id=" + idEncontrado);
                    Send("=rate-limit=" + velocidad, true);
                }
                else
                {
                    Send("/ppp/profile/add");
                    Send("=name=" + nombrePerfil);
                    Send("=rate-limit=" + velocidad, true);
                }

                // --- PASO 3: CONFIRMACIÓN FINAL ---
                // Leemos la respuesta del SET o ADD
                foreach (string row in Read())
                {
                    if (row.StartsWith("!trap")) return false;
                    if (row.StartsWith("!done")) return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }
        public string DeleteInterfacebyPlan(string Plan)
        {
            try
            {
                Send("/ppp/secret/print");
                Send("?profile=" + Plan);
                Send("=.proplist=name", true);

                //Send("=.proplist=name", true);
                List<string> planes = new List<string>();
                foreach (string row in Read())
                {
                    if (row.StartsWith("!re"))
                    {
                        continue;
                    }
                    if (row.StartsWith("!done")) break;

                    if (row.StartsWith("="))
                    {
                        string[] parts = row.Split(new char[] { '=' }, 3);
                        if (parts.Length < 3) continue;

                        string key = parts[1];
                        string value = parts[2];

                        if (key == "name")
                        {
                            DeleteInterfacebyName(value);
                        }
                    }
                }

            }
            catch (Exception e)
            {
                return e.Message;
            }
            return string.Empty;
        }
        public void DeleteInterfacebyName(string Name)
        {
            Send("/ppp/active/print");
            Send("?name=" + Name);
            Send("=.proplist=.id", true);
            foreach (string row2 in Read())
            {
                if (row2.StartsWith("!re"))
                {
                    continue;
                }
                if (row2.StartsWith("!done")) break;

                if (row2.StartsWith("="))
                {
                    string[] parts2 = row2.Split(new char[] { '=' }, 3);
                    if (parts2.Length < 3) continue;

                    string key2 = parts2[1];
                    string value2 = parts2[2];

                    if (key2 == ".id")
                    {
                        Send("/ppp/active/remove");
                        Send("=.id=" + value2, true);
                    }
                }
            }
        }
        public List<LimiteModel> VerProfilebyName(string Name)
        {
            List<LimiteModel> lista = new List<LimiteModel>();
            try
            {
                Send("/ppp/profile/print");
                Send("=.proplist=.id,name,rate-limit");
                Send("?name=" + Name, true);
                LimiteModel obj = null;
                foreach (string row in Read())
                {
                    if (row.StartsWith("!re"))
                    {
                        obj = new LimiteModel();
                        lista.Add(obj);
                        continue;
                    }
                    if (row.StartsWith("!done")) break;

                    if (row.StartsWith("="))
                    {
                        string[] parts = row.Split(new char[] { '=' }, 3);
                        if (parts.Length < 3) continue;

                        string key = parts[1];
                        string value = parts[2];
                        if (key == ".id") obj.Id = value;
                        if (key == "name") obj.Name = value;
                        if (key == "rate-limit") obj.Velocidad = value;
                    }
                }
            }
            catch (Exception e)
            {

            }
            return lista;
        }
        public List<FibrasModel> VerFibra(string name)
        {
            List<FibrasModel> listaFinal = new List<FibrasModel>();
            try
            {

                if (name == string.Empty)
                {
                    Send("/ppp/secret/print");
                    Send("=.proplist=.id,name,profile,remote-address,disabled", true);// Esto ayuda a que el router no se pierda enviando datos extra
                }
                if (name != string.Empty)
                {
                    Send("/ppp/secret/print");
                    Send("=.proplist=.id,name,profile,remote-address,disabled");// Esto ayuda a que el router no se pierda enviando datos extra
                    Send("?name=" + name, true);
                }
                FibrasModel currentObj = null;
                foreach (string row in Read())
                {
                    if (row.StartsWith("!re"))
                    {
                        currentObj = new FibrasModel();
                        listaFinal.Add(currentObj);
                        continue;
                    }

                    if (row.StartsWith("!done")) break;

                    if (row.StartsWith("="))
                    {
                        string[] parts = row.Split(new char[] { '=' }, 3);
                        if (parts.Length < 3) continue;

                        string key = parts[1];
                        string value = parts[2];

                        if (key == ".id") currentObj.id = value;
                        if (key == "name")
                        {
                            currentObj.comment = value;
                        }
                        if (key == "remote-address") currentObj.address = value;
                        if (key == "disabled") currentObj.estatus = value == "false" ? "Activo" : "Inactivo";
                        if (key == "profile")
                        {
                            var Listalimites = VerProfilebyName(value);
                            var perfil = Listalimites.FirstOrDefault(p => p.Name == value);
                            if (perfil != null)
                            {
                                currentObj.idplan = perfil.Id;
                                currentObj.velocidad = perfil.Velocidad;
                            }
                            else
                            {
                                currentObj.idplan = string.Empty;
                                currentObj.velocidad = string.Empty;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {

            }
            return listaFinal;
        }
        public string CrearFibra(string Usuario,  string IPDisponible, string Perfil)
        {
            string idCreado = "";
            // Enviar el comando para crear el usuario
            Send("/ppp/secret/add");
            Send("=name=" + Usuario);
            //Send("=password=" + contraseñaUsuario);
            Send("=service=pppoe");
            Send("=profile=" + Perfil);
            Send("=remote-address=" + IPDisponible);
            Send("=comment=Creado desde servicio", true);

            // Leer la respuesta de MikroTik para obtener el .id
            List<string> respuesta = Read();

            foreach (string linea in respuesta)
            {
                if (linea.StartsWith("=ret="))
                {
                    idCreado = linea.Replace("=ret=", ""); // Ejemplo de resultado: "*1A"
                    break;
                }
            }
            return idCreado;
        }
        public List<FibrasModel> VerFibrabyAddress(string IPDisponibleFibra)
        {
            List<FibrasModel> listaFinal = new List<FibrasModel>();
            try
            {
                Send("/ppp/secret/print");
                Send("=.proplist=.id,name,profile,remote-address,disabled");// Esto ayuda a que el router no se pierda enviando datos extra
                Send("?remote-address=" + IPDisponibleFibra, true);
                FibrasModel currentObj = null;
                foreach (string row in Read())
                {
                    if (row.StartsWith("!re"))
                    {
                        currentObj = new FibrasModel();
                        listaFinal.Add(currentObj);
                        continue;
                    }

                    if (row.StartsWith("!done")) break;

                    if (row.StartsWith("="))
                    {
                        string[] parts = row.Split(new char[] { '=' }, 3);
                        if (parts.Length < 3) continue;

                        string key = parts[1];
                        string value = parts[2];

                        if (key == ".id") currentObj.id = value;
                        if (key == "name")
                        {
                            currentObj.comment = value;
                        }
                        if (key == "remote-address") currentObj.address = value;
                        if (key == "disabled") currentObj.estatus = value == "false" ? "Activo" : "Inactivo";
                        if (key == "profile")
                        {
                            var Listalimites = VerProfilebyName(value);
                            var perfil = Listalimites.FirstOrDefault(p => p.Name == value);
                            if (perfil != null)
                            {
                                currentObj.idplan = perfil.Id;
                                currentObj.velocidad = perfil.Velocidad;
                            }
                            else
                            {
                                currentObj.idplan = string.Empty;
                                currentObj.velocidad = string.Empty;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {

            }
            return listaFinal;
        }
        public bool BuscarPoolbyAddress(string IdDisponibleFibra)
        {
            // 1. Pedir la propiedad 'ranges' del Pool que te interesa
            Send("/ip/pool/print");
            Send("=.proplist=ranges");
            Send("?name=pool-PPPoE", true);

            List<string> respuesta = Read();
            string cadenaRanges = "";

            foreach (string linea in respuesta)
            {
                if (linea.StartsWith("=ranges="))
                {
                    cadenaRanges = linea.Replace("=ranges=", "").Trim();
                    break;
                }
            }

            // Ejemplo de valor en 'cadenaRanges': "192.168.10.10-192.168.10.50,192.168.10.100-192.168.10.200"
            return ExisteIpEnRanges(IdDisponibleFibra, cadenaRanges);
        }
        public bool ExisteIpEnRanges(string ipABuscar, string rangesString)
        {
            if (string.IsNullOrWhiteSpace(ipABuscar) || string.IsNullOrWhiteSpace(rangesString))
                return false;

            long targetIpNum = IpToLong(ipABuscar.Split('/')[0].Trim());

            // Un pool puede tener varios rangos separados por comas
            string[] rangos = rangesString.Split(',');

            foreach (string rango in rangos)
            {
                string r = rango.Trim();

                if (r.Contains("-"))
                {
                    // Es un rango (ej: 192.168.10.10-192.168.10.50)
                    string[] partes = r.Split('-');
                    long ipInicio = IpToLong(partes[0].Trim());
                    long ipFin = IpToLong(partes[1].Trim());

                    if (targetIpNum >= ipInicio && targetIpNum <= ipFin)
                        return true;
                }
                else
                {
                    // Es una IP individual dentro del pool
                    long ipUnica = IpToLong(r);
                    if (targetIpNum == ipUnica)
                        return true;
                }
            }

            return false;
        }
        // Función para convertir una IP string a un valor numérico BIGINT (long)
        private long IpToLong(string ip)
        {
            System.Net.IPAddress address;
            if (System.Net.IPAddress.TryParse(ip, out address))
            {
                byte[] bytes = address.GetAddressBytes();
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(bytes);

                return BitConverter.ToUInt32(bytes, 0);
            }
            return 0;
        }
        public bool AgregarPool(string IPDisponibleFibra)
        {
            // 1. Obtener el .id y los rangos actuales del Pool
            Send("/ip/pool/print");
            Send("=.proplist=.id,ranges");
            Send("?name=pool-PPPoE", true);

            List<string> respuesta = Read();

            string idPool = "";
            string rangesActual = "";

            foreach (string linea in respuesta)
            {
                if (linea.StartsWith("=.id="))
                    idPool = linea.Replace("=.id=", "").Trim();
                else if (linea.StartsWith("=ranges="))
                    rangesActual = linea.Replace("=ranges=", "").Trim();
            }

            // Si no encontró el Pool con ese nombre, retornamos false
            if (string.IsNullOrEmpty(idPool))
            {
                return false;
            }

            // 2. Armar el nuevo formato (ej. "192.168.11.10-192.168.11.254")
            string nuevoRango = ArmarNuevoRango(IPDisponibleFibra);

            // Concatenar separado por coma
            string rangesFinal = string.IsNullOrEmpty(rangesActual)
                ? nuevoRango
                : $"{rangesActual},{nuevoRango}";

            // 3. Actualizar en MikroTik
            Send("/ip/pool/set");
            Send("=.id=" + idPool);
            Send("=ranges=" + rangesFinal, true);

            List<string> respSet = Read();

            // 4. Validar la respuesta del RouterOS
            // Si la respuesta contiene "!trap", MikroTik rechazó el comando por algún error
            foreach (string linea in respSet)
            {
                if (linea.StartsWith("!trap"))
                {
                    return false; // Error en MikroTik
                }
            }

            // Si no hubo ningún !trap, se ejecutó con éxito
            return true;
        }
        public string ArmarNuevoRango(string ipEntrante)
        {
            // Ejemplo de entrada: "192.168.1.1"
            string cleanIp = ipEntrante.Split('/')[0].Trim();
            string[] octetos = cleanIp.Split('.');

            // Toma los primeros 3 octetos (ej. "192.168.1") y arma el rango hasta el .254
            string baseRed = $"{octetos[0]}.{octetos[1]}.{octetos[2]}";

            // Resultado: "192.168.11.10-192.168.1.254"
            return $"{cleanIp}-{baseRed}.254";
        }
    }
}

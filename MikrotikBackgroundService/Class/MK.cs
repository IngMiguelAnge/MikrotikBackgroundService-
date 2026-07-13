using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

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

            int waitAttempts = 100;
            while (con.Available == 0 && waitAttempts > 0)
            {
                System.Threading.Thread.Sleep(50);
                waitAttempts--;
            }

            // Si después de la espera sigue en 0, es que el router recibió 
            // el comando pero no lo entendió o no lo aceptó.
            if (con.Available == 0)
            {
                System.Diagnostics.Debug.WriteLine("El router no mandó datos de respuesta.");
                return output;
            }

            while (true)
            {
                int curByte = connection.ReadByte();
                if (curByte == -1) break;

                long count = 0;
                if (curByte < 0x80) { count = curByte; }
                else if (curByte < 0xC0) { count = ((curByte ^ 0x80) << 8) + connection.ReadByte(); }
                else if (curByte < 0xE0) { count = ((curByte ^ 0xC0) << 16) + (connection.ReadByte() << 8) + connection.ReadByte(); }
                else if (curByte < 0xF0) { count = ((curByte ^ 0xE0) << 24) + (connection.ReadByte() << 16) + (connection.ReadByte() << 8) + connection.ReadByte(); }
                else if (curByte == 0xF0) { count = (connection.ReadByte() << 24) + (connection.ReadByte() << 16) + (connection.ReadByte() << 8) + connection.ReadByte(); }

                if (count == 0)
                {
                    if (output.Count > 0 && (output.Last().StartsWith("!done") || output.Last().StartsWith("!trap") || output.Last().StartsWith("!fatal")))
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

                string word = Encoding.Default.GetString(buffer);// Usar UTF8 es mejor en v7
                output.Add(word);
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
    }
}

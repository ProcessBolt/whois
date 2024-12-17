using System;
using System.Text;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Whois.Net
{
    /// <summary>
    /// Class to allow access to TCP services
    /// </summary>
    public class TcpReader : ITcpReader
    {
        private TcpClient tcpClient;

        private StreamReader reader;
        private StreamWriter writer;

        private async Task<bool> Connect(string domain, int port, Encoding encoding)
        {
            try
            {
                await tcpClient.ConnectAsync(domain, port);

                reader = new StreamReader(tcpClient.GetStream(), encoding);
                writer = new StreamWriter(tcpClient.GetStream())
                {
                    NewLine = "\r\n",
                };
            }
            catch (SocketException ex)
            {
                throw new WhoisException("Couldn't connect to " + domain + ": " + ex.Message, ex);
            }

            return tcpClient.Connected;
        }

        private void Write(string content)
        {
            try
            {
                writer.WriteLine(content);
                writer.FlushAsync();
            }
            catch (Exception ex)
            {
                throw new WhoisException("Error whilst writing data: " + ex.Message);
            }
        }

        private void Read(StringBuilder sb)
        {
            try
            {
                //var response = await reader.ReadLineAsync();
                var response = reader.ReadLine();

                while (response != null)
                {
                    sb.AppendLine(response);

                    //response = await reader.ReadLineAsync();
                    response = reader.ReadLine();
                }
            }
            catch (Exception ex)
            {
                throw new WhoisException("Error whilst reading data: " + ex.Message);
            }
        } 

        public async Task<string> Read(string url, int port, string command, Encoding encoding, int timeoutSeconds)
        {
            var task = Read(url, port, command, encoding);

            if (task == await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds))))
            {
                return await task;
            }

            throw new TimeoutException();
        }

        public async Task<string> Read(string url, int port, string command, Encoding encoding)
        {
            var sb = new StringBuilder();

            try
            {
                tcpClient = new TcpClient();
                
                var connected = await Connect(url, port, encoding);

                if (connected)
                {
                    Write(command);

                    Read(sb);
                }
            }
            finally
            {
                if (tcpClient?.Connected == true) tcpClient.Close();
#if !NET452
                tcpClient?.Dispose();
#endif
                reader?.Dispose();
                writer?.Dispose();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (tcpClient?.Connected == true)
            {
                tcpClient.Close();
            }

#if !NET452
            tcpClient?.Dispose();
#endif
            reader?.Dispose();
            writer?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}

using System.Net;
using System.Net.Sockets;
using System.Text;



    class FileServer
    {
        private readonly string _saveDirectory;
        private readonly int _fileTransferPort;
        private readonly int _heartbeatPort;

        public FileServer(string saveDirectory, int fileTransferPort, int heartbeatPort)
        {
            _saveDirectory = saveDirectory;
            _fileTransferPort = fileTransferPort;
            _heartbeatPort = heartbeatPort;
            Directory.CreateDirectory(_saveDirectory);
        }

        public void Start()
        {
            Task.Run(() => StartFileTransferListener());
            Task.Run(() => StartHeartbeatListener());
        }

        private async Task StartFileTransferListener()
        {
            var fileListener = new TcpListener(IPAddress.Any, _fileTransferPort);
            fileListener.Start();
            Console.WriteLine($"File transfer server started on port {_fileTransferPort}");

            while (true)
            {
                var client = await fileListener.AcceptTcpClientAsync();
                Console.WriteLine("File transfer client connected.");
                _ = HandleFileTransferClientAsync(client);
            }
        }

        private async Task StartHeartbeatListener()
        {
            var heartbeatListener = new TcpListener(IPAddress.Any, _heartbeatPort);
            heartbeatListener.Start();
            Console.WriteLine($"Heartbeat server started on port {_heartbeatPort}");

            while (true)
            {
                var client = await heartbeatListener.AcceptTcpClientAsync();
                Console.WriteLine("Heartbeat client connected.");
                _ = HandleHeartbeatClientAsync(client);
            }
        }

        private async Task HandleFileTransferClientAsync(TcpClient client)
{
    if (client == null || !client.Connected)
    {
        Console.WriteLine("Client is not connected.");
        return;
    }

    NetworkStream networkStream = null;
    try
    {
        networkStream = client.GetStream();
        if (networkStream == null)
        {
            Console.WriteLine("Network stream is null.");
            return;
        }

        await using (networkStream)
        {
            using var reader = new StreamReader(networkStream, Encoding.UTF8);

            // Read the metadata
            var metadataLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(metadataLine))
            {
                Console.WriteLine("Metadata is null or empty.");
                return;
            }

            var metadataParts = metadataLine.Split(';');
            var fileNamePart = metadataParts.FirstOrDefault(p => p.StartsWith("FileName:"));
            var sizePart = metadataParts.FirstOrDefault(p => p.StartsWith("Size:"));

            if (fileNamePart == null || sizePart == null)
            {
                Console.WriteLine("Invalid metadata received.");
                return;
            }

            var fileName = fileNamePart.Replace("FileName:", "").Trim();
            var clientFileSizeStr = sizePart.Replace("Size:", "").Trim();

            if (!long.TryParse(clientFileSizeStr, out long clientFileSize))
            {
                Console.WriteLine("Invalid file size in metadata.");
                return;
            }

            var fullPath = Path.Combine(_saveDirectory, fileName);

            Log($"Received file request for: {fileName}, size: {clientFileSizeStr} bytes");

            FileInfo fileInfo = new FileInfo(fullPath);

            long serverFileSize = 0;
            if (fileInfo.Exists)
            {
                serverFileSize = fileInfo.Length;
                Log($"File exists. Sending file size: {serverFileSize} bytes");
            }

            byte[] fileSizeBytes = BitConverter.GetBytes(serverFileSize);
            await networkStream.WriteAsync(fileSizeBytes, 0, fileSizeBytes.Length);

            if (serverFileSize < clientFileSize)
            {
                Log($"Receiving file from client. Starting at byte: {serverFileSize}");
                await using var fileStream = new FileStream(fullPath, FileMode.Append, FileAccess.Write);
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead;
                    long totalReceived = 0;
                    while ((bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalReceived += bytesRead;
                        //Log($"Received {serverFileSize+totalReceived} of {clientFileSize} bytes");
                    }
                }

                Log($"File transfer complete. File saved to {fullPath}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error handling file transfer: {ex.Message}");
    }
    finally
    {
        client?.Close();
    }
}

        

        private async Task HandleHeartbeatClientAsync(TcpClient client)
        {
            await using var networkStream = client.GetStream();
            using var reader = new StreamReader(networkStream, Encoding.UTF8);

            try
            {
                while (true)
                {
                    var message = await reader.ReadLineAsync();
                    if (message == null) break; // Client disconnected

                    if (message.Equals("ping", StringComparison.Ordinal))
                    {
                        Console.WriteLine($"[{DateTime.Now}] Ping received.");
                        byte[] pongMessage = Encoding.UTF8.GetBytes("pong\n");
                        await networkStream.WriteAsync(pongMessage, 0, pongMessage.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling heartbeat: {ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }
        private static void LogToFile(string message)
        {
            string logFilePath = "file_transfer_log.txt";

            try
            {
                // AppendAllText will create the file if it does not exist
                File.AppendAllText(logFilePath, $"{DateTime.Now}: {message}\n");
            }
            catch (IOException ioEx)
            {
                Console.WriteLine($"I/O Error while writing to log file: {ioEx.Message}");
            }
            catch (UnauthorizedAccessException uaEx)
            {
                Console.WriteLine($"Access Error while writing to log file: {uaEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while writing to log file: {ex.Message}");
            }
        }
        private static void Log(string message)
        {
            Console.WriteLine($"[{DateTime.Now}] {message}");
            // Additional logging to file can be implemented here.
        }
    }


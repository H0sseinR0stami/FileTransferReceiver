
using System.Net;
using System.Net.Sockets;
using System.Text;
using server;


class FileServer
    {
        private static readonly LogData LogData = new LogData();
        private static readonly Configuration Config = new Configuration("../../../config.txt");
        private readonly string _saveDirectory;
        private readonly int _fileTransferPort;
        private readonly int _pingPort;
        private readonly int _fileBufferSize = Config.GetIntValue("fileBufferSize");
        public FileServer(string saveDirectory, int fileTransferPort, int pingPort)
        {
            _saveDirectory = saveDirectory;
            _fileTransferPort = fileTransferPort;
            _pingPort = pingPort;
            Directory.CreateDirectory(_saveDirectory);
        }

        public void Start()
        {
            Task.Run(() => StartFileTransferListener());
            Task.Run(() => StartPingListener());
        }

        private async Task StartFileTransferListener()
        {
            var fileListener = new TcpListener(IPAddress.Any, _fileTransferPort);
            fileListener.Start();
            LogData.Log($"File transfer server started on port {_fileTransferPort}");

            while (true)
            {
                var client = await fileListener.AcceptTcpClientAsync();
                LogData.Log($"File transfer client connected.");
                _ = HandleFileTransferClientAsync(client);
            }
        }

        private async Task StartPingListener()
        {
            var pingListener = new TcpListener(IPAddress.Any, _pingPort);
            pingListener.Start();
            LogData.Log($"ping server started on port {_pingPort}");

            while (true)
            {
                var client = await pingListener.AcceptTcpClientAsync();
                LogData.Log("ping client connected.");
                _ = HandlePingClientAsync(client);
            }
        }

        private async Task HandleFileTransferClientAsync(TcpClient client)
{
    if (client == null || !client.Connected)
    {
        LogData.Log("Client is not connected.");
        return;
    }

    NetworkStream networkStream = null;
    try
    {
        networkStream = client.GetStream();
        if (networkStream == null)
        {
            LogData.Log("Network stream is null.");
            return;
        }

        await using (networkStream)
        {
            using var reader = new StreamReader(networkStream, Encoding.UTF8);

            // Read the metadata
            var metadataLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(metadataLine))
            {
                LogData.Log("Metadata is null or empty.");
                return;
            }

            var metadataParts = metadataLine.Split(';');
            var fileNamePart = metadataParts.FirstOrDefault(p => p.StartsWith("FileName:"));
            var sizePart = metadataParts.FirstOrDefault(p => p.StartsWith("Size:"));

            if (fileNamePart == null || sizePart == null)
            {
                LogData.Log("Invalid metadata received.");
                return;
            }

            var fileName = fileNamePart.Replace("FileName:", "").Trim();
            var clientFileSizeStr = sizePart.Replace("Size:", "").Trim();

            if (!long.TryParse(clientFileSizeStr, out long clientFileSize))
            {
                LogData.Log("Invalid file size in metadata.");
                return;
            }

            var fullPath = Path.Combine(_saveDirectory, fileName);

            LogData.Log($"Received file request for: {fileName}, size: {clientFileSizeStr} bytes");

            FileInfo fileInfo = new FileInfo(fullPath);

            long serverFileSize = 0;
            if (fileInfo.Exists)
            {
                serverFileSize = fileInfo.Length;
                LogData.Log($"File exists. Sending file size: {serverFileSize} bytes");
            }

            byte[] fileSizeBytes = BitConverter.GetBytes(serverFileSize);
            await networkStream.WriteAsync(fileSizeBytes, 0, fileSizeBytes.Length);

            if (serverFileSize < clientFileSize)
            {
                LogData.Log($"Receiving file from client. Starting at byte: {serverFileSize}");
                await using var fileStream = new FileStream(fullPath, FileMode.Append, FileAccess.Write);
                {
                    byte[] buffer = new byte[_fileBufferSize];
                    int bytesRead;
                    long totalReceived = 0;
                    while ((bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalReceived += bytesRead;
                        //LogData.Log($"Received {serverFileSize+totalReceived} of {clientFileSize} bytes");
                    }
                }

                LogData.Log($"File transfer complete. File saved to {fullPath}");
            }
        }
    }
    catch (Exception ex)
    {
        LogData.Log($"Error handling file transfer: {ex.Message}");
    }
    finally
    {
        client?.Close();
    }
}

        

        private async Task HandlePingClientAsync(TcpClient client)
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
                        LogData.Log($"Ping received.");
                        byte[] pongMessage = Encoding.UTF8.GetBytes("pong\n");
                        await networkStream.WriteAsync(pongMessage, 0, pongMessage.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                LogData.Log($"Error handling ping: {ex.Message}");
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
                LogData.Log($"I/O Error while writing to log file: {ioEx.Message}");
            }
            catch (UnauthorizedAccessException uaEx)
            {
                LogData.Log($"Access Error while writing to log file: {uaEx.Message}");
            }
            catch (Exception ex)
            {
                LogData.Log($"An error occurred while writing to log file: {ex.Message}");
            }
        }
    }


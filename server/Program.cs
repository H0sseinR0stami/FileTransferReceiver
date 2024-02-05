
namespace server;

internal static class Program
{
    private static readonly LogData LogData = new LogData();
    private static readonly Configuration Config = new Configuration("../../../config.txt");
    private static readonly int FileTransferPort = Config.GetIntValue("fileTransferPort");
    private static readonly int PingPort = Config.GetIntValue("pingPort");   
    private static readonly string RelativePath = Config.GetOsDependentPath();
    
    
    private static async Task Main(string[] args)
    {
        
        string saveDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativePath);

        // Check if the folder exists, if not, create it
        if (!Directory.Exists(saveDirectory))
        {
            Directory.CreateDirectory(saveDirectory);
            LogData.Log($"Folder created at: \"{saveDirectory}\"");
        }

        var server = new FileServer(saveDirectory, FileTransferPort, PingPort);
        server.Start(); // Start the server to listen on both ports

        var ipAddress = Config.GetLocalIPAddress(); 
        LogData.Log($"Server is running on {ipAddress}.");
        Console.ReadLine(); // Keep the server running until Enter is pressed
    }
}
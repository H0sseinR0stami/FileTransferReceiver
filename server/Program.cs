internal static class Program
{
    private static async Task Main(string[] args)
    {
        const int fileTransferPort = 1234; // Port for file transfers
        const int heartbeatPort = 1235;    // Separate port for heartbeat messages
        const string relativePath = @"C:\ReceivedFolder"; // Relative path for the save directory
        //const string relativePath = @"/home/manager/ReceivedFile"; // Alternative path for Unix-based systems

        string saveDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);

        // Check if the folder exists, if not, create it
        if (!Directory.Exists(saveDirectory))
        {
            Directory.CreateDirectory(saveDirectory);
            Console.WriteLine($"Folder created at: {saveDirectory}");
        }

        var server = new FileServer(saveDirectory, fileTransferPort, heartbeatPort);
        server.Start(); // Start the server to listen on both ports

        Console.WriteLine("Server is running. Press Enter to exit.");
        Console.ReadLine(); // Keep the server running until Enter is pressed
    }
}
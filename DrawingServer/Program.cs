using System.Threading.Tasks;

namespace DrawingServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Server server = new Server();
            await server.StartAsync();
        }
    }
}
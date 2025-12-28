using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace TaskPilot
{
    public class StatusHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[StatusHub] Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            System.Diagnostics.Debug.WriteLine($"[StatusHub] Client disconnected: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }
    }
}

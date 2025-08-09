using Microsoft.AspNetCore.SignalR;
using SmartCloud.Core.Models;

namespace SmartCloud.Dashboard.Hubs;

/// <summary>
/// SignalR hub for real-time dashboard updates
/// </summary>
public class DashboardHub : Hub
{
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task SendDataUpdate(PlcData data)
    {
        await Clients.All.SendAsync("ReceiveData", data);
    }

    public async Task SendAlarmUpdate(Alarm alarm)
    {
        await Clients.All.SendAsync("ReceiveAlarm", alarm);
    }

    public async Task SendMaintenancePrediction(MaintenancePrediction prediction)
    {
        await Clients.All.SendAsync("ReceivePrediction", prediction);
    }
}

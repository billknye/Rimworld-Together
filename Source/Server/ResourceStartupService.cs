using Microsoft.Extensions.Hosting;
using RimworldTogether.GameServer.Managers;

namespace RimworldTogether.GameServer.Core;

public static partial class Program
{
    internal sealed class ResourceStartupService : BackgroundService
    {
        private readonly ModManager modManager;
        private readonly Network.Network network;

        public ResourceStartupService(ModManager modManager, Network.Network network)
        {
            this.modManager = modManager;
            this.network = network;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            LoadResources(modManager, network);
            return Task.CompletedTask;
        }
    }
}
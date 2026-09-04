using System;
using System.Threading;
using System.Threading.Tasks;
using GamebuinoAKA.IDE.Models;

namespace GamebuinoAKA.IDE.Services
{
    /// <summary>
    /// Aiguille chaque commande vers la bonne chaîne selon project.BuildSystem.
    /// Les signatures sont identiques à celles de PlatformIOService pour que
    /// ProjectsViewModel puisse les brancher sans changer sa mécanique.
    /// </summary>
    public class BuildService
    {
        private readonly PlatformIOService _pio;
        private readonly EspIdfService _idf;

        public BuildService(PlatformIOService pio, EspIdfService idf)
        {
            _pio = pio;
            _idf = idf;
        }

        public Task BuildAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default)
            => p.IsEspIdf ? _idf.BuildAsync(p, o, ct) : _pio.BuildAsync(p, o, ct);

        public Task FlashAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default)
            => p.IsEspIdf ? _idf.FlashAsync(p, o, ct) : _pio.FlashAsync(p, o, ct);

        public Task MonitorAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default)
            => p.IsEspIdf ? _idf.MonitorAsync(p, o, ct) : _pio.MonitorAsync(p, o, ct);

        public Task CleanAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default)
            => p.IsEspIdf ? _idf.CleanAsync(p, o, ct) : _pio.CleanAsync(p, o, ct);
    }
}

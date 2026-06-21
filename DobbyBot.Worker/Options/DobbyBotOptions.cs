using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DobbyBot.Worker.Options
{
    public sealed class DobbyBotOptions
    {
        public string Token { get; init; } = string.Empty;
        public long AdminTelegramId { get; init; }
    }
}

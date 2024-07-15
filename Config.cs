using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace ShowDamage
{
    public class ShowDamageConfig : BasePluginConfig
    {
        public int DamageType { get; set; } = 1; // 1 - HUD , 2 - Faceit, 3 - Game
        public float NotifyDuration { get; set; } = 3.5f;

        [JsonPropertyName("NormalSize")]
        public int NormalSize { get; set; } = 80;

        [JsonPropertyName("NormalHeadshotSize")]
        public int NormalHeadshotSize { get; set; } = 90;

        [JsonPropertyName("KillSize")]
        public int KillSize { get; set; } = 120;

        [JsonPropertyName("KillHeadshotSize")]
        public int KillHeadshotSize { get; set; } = 120;

        [JsonPropertyName("NormalColor")]
        public string NormalColor { get; set; } = "#ffffff";

        [JsonPropertyName("NormalHeadshotColor")]
        public string NormalHeadshotColor { get; set; } = "#ffff00";

        [JsonPropertyName("KillColor")]
        public string KillColor { get; set; } = "#ff0000";

        [JsonPropertyName("KillHeadshotColor")]
        public string KillHeadshotColor { get; set; } = "#ff0000";

        [JsonPropertyName("TextDisplayDuration")]
        public float TextDisplayDuration { get; set; } = 0.5f;

        [JsonPropertyName("EnableShadow")]
        public bool EnableShadow { get; set; } = false;
    }
}
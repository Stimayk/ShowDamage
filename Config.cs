using CounterStrikeSharp.API.Core;

namespace ShowDamage
{
    public class ShowDamageConfig : BasePluginConfig
    {
        public float NotifyDuration { get; set; } = 3.5f;
        public int Prioritize { get; set; } = 2;
    }
}
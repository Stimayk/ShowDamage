using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;
using static CounterStrikeSharp.API.Core.Listeners;

namespace ShowDamage
{
    public class ShowDamage : BasePlugin, IPluginConfig<ShowDamageConfig>
    {
        public override string ModuleName => "ShowDamage";
        public override string ModuleDescription => "";
        public override string ModuleAuthor => "E!N";
        public override string ModuleVersion => "v1.0.0";

        public ShowDamageConfig Config { get; set; } = new();

        private bool messageToHudEnabled = false;

        private readonly Dictionary<CCSPlayerController, Dictionary<CCSPlayerController, int>> g_DamageDone = [];
        private readonly Dictionary<CCSPlayerController, Dictionary<CCSPlayerController, int>> g_DamageDoneHits = [];

        private readonly Random random = new();

        public void OnConfigParsed(ShowDamageConfig config)
        {
            Config = config;
        }

        public override void Load(bool hotReload)
        {
        }

        [GameEventHandler]
        public HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
        {
            var died = @event?.Userid;
            var killer = @event?.Attacker;
            var health = @event?.Health ?? 0;
            var damage = @event?.DmgHealth ?? 0;

            if (died != null && killer != null && killer != died && killer.TeamNum != died.TeamNum)
            {
                switch (Config.DamageType)
                {
                    case 1:
                        DisplayDamageMessage(killer, damage, died.PlayerName ?? "Unknown", health);
                        ScheduleHudMessage(killer, damage, died, health);
                        break;

                    case 2:
                        TrackDamageStats(killer, died, damage);
                        break;

                    case 3:
                        if (HandleGameDamage(@event, died, killer, damage))
                            return HookResult.Continue;
                        break;
                }
            }
            return HookResult.Continue;
        }

        private void DisplayDamageMessage(CCSPlayerController killer, int damage, string playerName, int health)
        {
            killer?.PrintToCenterHtml(Localizer["HUD", damage, playerName, health]);
        }

        private void ScheduleHudMessage(CCSPlayerController killer, int damage, CCSPlayerController died, int health)
        {
            Server.NextFrame(() =>
            {
                RegisterListener<OnTick>(() =>
                {
                    if (messageToHudEnabled)
                    {
                        OnTick(killer, damage, died, health);
                    }
                });
                ToggleMessageToHud(Config.NotifyDuration);
            });
        }

        private void TrackDamageStats(CCSPlayerController killer, CCSPlayerController died, int damage)
        {
            if (!g_DamageDone.TryGetValue(killer, out Dictionary<CCSPlayerController, int>? value))
            {
                value = ([]);
                g_DamageDone[killer] = value;
                g_DamageDoneHits[killer] = [];
            }

            value[died] = value.GetValueOrDefault(died, 0) + damage;
            g_DamageDoneHits[killer][died] = g_DamageDoneHits[killer].GetValueOrDefault(died, 0) + 1;

            if (!g_DamageDone.ContainsKey(died))
            {
                g_DamageDone[died] = [];
                g_DamageDoneHits[died] = [];
            }
            g_DamageDone[died][killer] = g_DamageDone[died].GetValueOrDefault(killer, 0);
            g_DamageDoneHits[died][killer] = g_DamageDoneHits[died].GetValueOrDefault(killer, 0);
        }

        private void OnTick(CCSPlayerController killer, int damage, CCSPlayerController? died, int health)
        {
            DisplayDamageMessage(killer, damage, died?.PlayerName ?? "Unknown", health);
        }

        private void ToggleMessageToHud(float duration)
        {
            messageToHudEnabled = true;
            AddTimer(duration, () => messageToHudEnabled = false);
        }

        private bool HandleGameDamage(EventPlayerHurt? @event, CCSPlayerController? died, CCSPlayerController? killer, int damage)
        {
            var headshot = @event?.Hitgroup == 1;
            var kill = @event?.Health <= 0;

            if (died?.PlayerPawn?.Value == null || killer?.PlayerPawn?.Value == null) return true;

            var offset = 40;
            var attackerOrigin = killer.PlayerPawn.Value.AbsOrigin ?? new Vector();
            var victimOrigin = died.PlayerPawn.Value.AbsOrigin ?? new Vector();
            var delta = attackerOrigin - victimOrigin;
            var distance = delta.Length();
            var position = victimOrigin + (delta / distance) * offset;
            position += new Vector((float)GetRandomDouble(10, 15, false), (float)GetRandomDouble(10, 15, false), (float)GetRandomDouble(20, 80));

            var angle = new QAngle
            {
                X = died.PlayerPawn.Value.AbsRotation?.X ?? 0,
                Z = died.PlayerPawn.Value.AbsRotation?.Z ?? 0 + 90.0f,
                Y = killer.PlayerPawn.Value.EyeAngles?.Y ?? 0 - 90f
            };

            var (color, size) = GetTextStyle(kill, headshot);

            ShowDamageText(damage.ToString(), color, size, position, angle);

            if (Config.EnableShadow)
            {
                var shadowPosition = position - (delta / distance) - new Vector(0, 0, size * 0.005f);
                ShowDamageText(damage.ToString(), "#080808", size, shadowPosition, angle);
            }

            return false;
        }

        private (string color, int size) GetTextStyle(bool kill, bool headshot)
        {
            if (kill)
            {
                return headshot ? (Config.KillHeadshotColor, Config.KillHeadshotSize) : (Config.KillColor, Config.KillSize);
            }
            else
            {
                return headshot ? (Config.NormalHeadshotColor, Config.NormalHeadshotSize) : (Config.NormalColor, Config.NormalSize);
            }
        }

        [GameEventHandler]
        public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            if (Config.DamageType == 2)
            {
                foreach (var killer in g_DamageDone.Keys)
                {
                    foreach (var died in g_DamageDone[killer].Keys)
                    {
                        var killerDamageDone = g_DamageDone[killer].GetValueOrDefault(died, 0);
                        var killerDamageHits = g_DamageDoneHits[killer].GetValueOrDefault(died, 0);
                        var diedDamageDone = g_DamageDone.TryGetValue(died, out Dictionary<CCSPlayerController, int>? value) ? value.GetValueOrDefault(killer, 0) : 0;
                        var diedDamageHits = g_DamageDoneHits.ContainsKey(died) ? g_DamageDoneHits[died].GetValueOrDefault(killer, 0) : 0;

                        killer.PrintToChat(Localizer["FACEIT", killerDamageDone, killerDamageHits, diedDamageDone, diedDamageHits, died.PlayerName, died.Health]);
                    }
                }
                g_DamageDone.Clear();
                g_DamageDoneHits.Clear();
            }
            return HookResult.Continue;
        }

        public double GetRandomDouble(double min, double max, bool positive = true)
        {
            return positive || random.Next(0, 2) == 0 ? min + random.NextDouble() * (max - min) : -min + random.NextDouble() * (-max - -min);
        }

        private void ShowDamageText(string text, string color, int size, Vector position, QAngle angle)
        {
            var entity = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext");
            if (entity == null) return;

            entity.DispatchSpawn();
            entity.MessageText = text;
            entity.Enabled = true;
            entity.Color = ColorTranslator.FromHtml(color);
            entity.FontSize = size;
            entity.Fullbright = true;
            entity.WorldUnitsPerPx = 0.1f;
            entity.DepthOffset = 0.0f;
            entity.JustifyHorizontal = PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER;
            entity.JustifyVertical = PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_CENTER;
            entity.Teleport(position, angle, new Vector(0, 0, 0));

            AddTimer(Config.TextDisplayDuration, entity.Remove);
        }
    }
}
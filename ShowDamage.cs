using Clientprefs.API;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using PlayerSettings;
using System.Collections.Concurrent;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace ShowDamage
{
    public class ShowDamage : BasePlugin, IPluginConfig<ShowDamageConfig>
    {
        public override string ModuleName => "ShowDamage";
        public override string ModuleDescription => "Displays damage dealt to players.";
        public override string ModuleAuthor => "E!N";
        public override string ModuleVersion => "v1.1.0";

        public ShowDamageConfig Config { get; set; } = new ShowDamageConfig();

        private readonly ConcurrentDictionary<CCSPlayerController, string> messages = new();
        private readonly ConcurrentDictionary<CCSPlayerController, Timer> deleteTimers = new();
        private ISettingsApi? _settings;
        private readonly PluginCapability<ISettingsApi?> _settingsCapability = new("settings:nfcore");
        private readonly PluginCapability<IClientprefsApi?> _clientprefsCapability = new("Clientprefs");
        private IClientprefsApi? _clientprefs;
        private int _showDamageCookieId = -1;
        private readonly ConcurrentDictionary<CCSPlayerController, string> _playerCookies = new();

        public void OnConfigParsed(ShowDamageConfig config)
        {
            Config = config;
        }

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            _settings = _settingsCapability.Get();
            _clientprefs = _clientprefsCapability.Get();

            if (_settings != null && _clientprefs != null)
            {
                switch (Config.Prioritize)
                {
                    case 1:
                        _settings = null;
                        break;
                    case 2:
                        _clientprefs = null;
                        break;
                    default:
                        break;
                }
            }

            if (_settings == null && _clientprefs == null)
            {
                Logger.LogInformation("No settings provider found!");
            }

            if (_clientprefs != null)
            {
                _clientprefs.OnDatabaseLoaded += OnClientprefsDatabaseReady;
                _clientprefs.OnPlayerCookiesCached += OnPlayerCookiesCached;
            }

            RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
            RegisterListener<Listeners.OnTick>(ShowDamageMessages);

            if (hotReload && _clientprefs != null)
            {
                foreach (CCSPlayerController? player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
                {
                    if (_clientprefs.ArePlayerCookiesCached(player))
                    {
                        _playerCookies[player] = _clientprefs.GetPlayerCookie(player, _showDamageCookieId);
                    }
                }
            }
        }

        public override void Unload(bool hotReload)
        {
            foreach (Timer timer in deleteTimers.Values)
            {
                timer.Kill();
            }
            deleteTimers.Clear();
            messages.Clear();

            DeregisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
            RemoveListener<Listeners.OnTick>(ShowDamageMessages);

            if (_clientprefs != null)
            {
                _clientprefs.OnDatabaseLoaded -= OnClientprefsDatabaseReady;
                _clientprefs.OnPlayerCookiesCached -= OnPlayerCookiesCached;
            }
        }

        private void OnClientprefsDatabaseReady()
        {
            if (_clientprefs == null)
            {
                return;
            }

            _showDamageCookieId = _clientprefs.RegPlayerCookie(
                "showdamage",
                "Whether to show damage notifications",
                CookieAccess.CookieAccess_Public
            );

            if (_showDamageCookieId == -1)
            {
                Logger.LogError("Failed to register showdamage cookie!");
            }
        }

        private void OnPlayerCookiesCached(CCSPlayerController player)
        {
            if (_clientprefs == null || _showDamageCookieId == -1)
            {
                return;
            }

            if (!player.IsValid || player.IsBot)
            {
                return;
            }

            _playerCookies[player] = _clientprefs.GetPlayerCookie(player, _showDamageCookieId);
        }

        [ConsoleCommand("css_damage", "Toggle damage display")]
        public void OnToggleDamageCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || (_settings == null && _clientprefs == null))
            {
                return;
            }

            bool newState;

            if (_settings != null)
            {
                bool currentState = _settings.GetPlayerSettingsValue(player, "showdamage", "true") == "true";
                newState = !currentState;
                _settings.SetPlayerSettingsValue(player, "showdamage", newState.ToString().ToLower());
            }
            else
            {
                if (!_playerCookies.TryGetValue(player, out string? currentValue))
                {
                    currentValue = "true";
                }

                newState = currentValue != "true";
                string newValue = newState ? "true" : "false";

                _clientprefs?.SetPlayerCookie(player, _showDamageCookieId, newValue);
                _playerCookies[player] = newValue;
            }

            player.PrintToChat(Localizer["Damage", newState ? "on" : "off"]);
        }

        [GameEventHandler()]
        public HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
        {
            CCSPlayerController? attacker = @event.Attacker;
            CCSPlayerController? userid = @event.Userid;

            if (attacker == null || !attacker.IsValid || attacker.IsBot || attacker.IsHLTV || attacker == userid ||
                (attacker.TeamNum == (userid?.TeamNum ?? 0)))
            {
                return HookResult.Continue;
            }

            bool showDamage = true;

            if (_settings != null)
            {
                showDamage = _settings.GetPlayerSettingsValue(attacker, "showdamage", "true") == "true";
            }
            else if (_clientprefs != null)
            {
                _ = _playerCookies.TryGetValue(attacker, out string? cookieValue);
                showDamage = cookieValue != "false";
            }

            if (!showDamage)
            {
                return HookResult.Continue;
            }

            int dmgHealth = @event.DmgHealth;
            int health = @event.Health;
            Microsoft.Extensions.Localization.LocalizedString hudMessage = Localizer["HUD", dmgHealth, userid?.PlayerName ?? "Unknown", health];

            ManageTimerAndMessage(attacker, hudMessage);

            return HookResult.Continue;
        }

        private void ManageTimerAndMessage(CCSPlayerController attacker, string hudMessage)
        {
            if (deleteTimers.TryRemove(attacker, out Timer? timer))
            {
                timer.Kill();
            }

            messages[attacker] = hudMessage;
            Timer newTimer = new(Config.NotifyDuration, () =>
            {
                if (messages.TryRemove(attacker, out _) && deleteTimers.TryRemove(attacker, out Timer? removeTimer))
                {
                    removeTimer?.Kill();
                }
            });

            deleteTimers[attacker] = newTimer;
        }

        private void ShowDamageMessages()
        {
            foreach (KeyValuePair<CCSPlayerController, string> entry in messages)
            {
                PrintHtml(entry.Key, entry.Value);
            }
        }

        private void PrintHtml(CCSPlayerController player, string hudContent)
        {
            EventShowSurvivalRespawnStatus eventShowSurvivalRespawnStatus = new(false)
            {
                LocToken = hudContent,
                Duration = 5L,
                Userid = player
            };
            try
            {
                eventShowSurvivalRespawnStatus.FireEvent(false);
            }
            catch (NativeException ex)
            {
                Logger.LogError($"Failed to fire event: {ex.Message}");
            }
        }
    }
}
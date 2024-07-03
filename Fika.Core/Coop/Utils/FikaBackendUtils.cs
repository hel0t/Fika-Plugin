using EFT;
using EFT.UI.Matchmaker;
using Fika.Core.Networking.Http;
using Fika.Core.Networking.Http.Models;
using System;
using System.Linq;
using System.Reflection;

namespace Fika.Core.Coop.Utils
{
    public static class FikaBackendUtils
    {
        public static MatchMakerAcceptScreen MatchMakerAcceptScreenInstance;
        public static Profile Profile;
        public static string PMCName;
        public static EMatchingType MatchingType = EMatchingType.Single;
        public static bool IsServer => MatchingType == EMatchingType.GroupLeader;
        public static bool IsClient => MatchingType == EMatchingType.GroupPlayer;
        public static bool IsSinglePlayer => MatchingType == EMatchingType.Single;
        public static PlayersRaidReadyPanel PlayersRaidReadyPanel;
        public static MatchMakerGroupPreview MatchMakerGroupPreview;
        public static int HostExpectedNumberOfPlayers = 1;
        public static WeatherClass[] Nodes = null;
        public static string RemoteIp;
        public static int RemotePort;
        private static string _serverId;
        private static string _raidCode;

        public static MatchmakerTimeHasCome.GClass3187 ScreenController;

        public static string GetServerId()
        {
            return _serverId;
        }

        public static void SetServerId(string newId)
        {
            _serverId = newId;
        }

        public static void SetRaidCode(string newCode)
        {
            _raidCode = newCode;
        }

        public static string GetRaidCode()
        {
            return _raidCode;
        }

        public static bool JoinMatch(string profileId, string serverId, out CreateMatch result, out string errorMessage)
        {
            result = new CreateMatch();
            errorMessage = $"No server matches the data provided or the server no longer exists";

            if (MatchMakerAcceptScreenInstance == null)
            {
                return false;
            }

            MatchJoinRequest body = new(serverId, profileId);
            result = FikaRequestHandler.RaidJoin(body);

            if (result.GameVersion != FikaPlugin.EFTVersionMajor)
            {
                errorMessage = $"You are attempting to use a different version of EFT than what the server is running.\nClient: {FikaPlugin.EFTVersionMajor}\nServer: {result.GameVersion}";
                return false;
            }

            Version detectedFikaVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (result.FikaVersion != detectedFikaVersion)
            {
                errorMessage = $"You are attempting to use a different version of Fika than what the server is running.\nClient: {detectedFikaVersion}\nServer: {result.FikaVersion}";
                return false;
            }

            SetRaidCode(result.RaidCode);
            
            return true;
        }

        public static void CreateMatch(string profileId, string hostUsername, RaidSettings raidSettings)
        {
            long timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            var raidCode = GenerateRaidCode(6);
            var body = new CreateMatch(raidCode, profileId, hostUsername, timestamp, raidSettings, HostExpectedNumberOfPlayers, raidSettings.Side, raidSettings.SelectedDateTime);

            FikaRequestHandler.RaidCreate(body);

            SetServerId(profileId);
            MatchingType = EMatchingType.GroupLeader;
            
            SetRaidCode(raidCode);
        }

        private static string GenerateRaidCode(int length)
        {
            var random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray())
                .ToUpper();
        }
    }
}

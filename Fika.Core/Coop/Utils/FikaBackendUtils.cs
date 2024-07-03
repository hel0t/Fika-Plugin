﻿using EFT;
using EFT.UI.Matchmaker;
using Fika.Core.Networking.Http;
using Fika.Core.Networking.Http.Models;
using System;
using System.Linq;
using System.Reflection;
using Fika.Core.EssentialPatches;
using UnityEngine;
using Random = System.Random;

namespace Fika.Core.Coop.Utils
{
    public enum EMatchmakerType
    {
        Single = 0,
        GroupPlayer = 1,
        GroupLeader = 2
    }

    public static class FikaBackendUtils
    {
        #region Fields/Properties
        public static MatchMakerAcceptScreen MatchMakerAcceptScreenInstance;
        public static Profile Profile;
        public static string PMCName;
        public static EMatchmakerType MatchingType = EMatchmakerType.Single;
        public static bool IsServer => MatchingType == EMatchmakerType.GroupLeader;
        public static bool IsClient => MatchingType == EMatchmakerType.GroupPlayer;
        public static bool IsSinglePlayer => MatchingType == EMatchmakerType.Single;
        public static PlayersRaidReadyPanel PlayersRaidReadyPanel { get; set; }
        public static MatchMakerGroupPreview MatchMakerGroupPreview { get; set; }
        public static int HostExpectedNumberOfPlayers { get; set; } = 1;
        public static WeatherClass[] Nodes { get; set; } = null;
        public static string RemoteIp;
        public static int RemotePort;
        private static string groupId;
        private static long timestamp;
        #endregion

        #region Static Fields

        public static object MatchmakerScreenController
        {
            get
            {
                object screenController = typeof(MatchMakerAcceptScreen).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy).Where(x => x.Name == "ScreenController")
                    .FirstOrDefault().GetValue(MatchMakerAcceptScreenInstance);
                if (screenController != null)
                {
                    return screenController;
                }
                return null;
            }
        }

        public static GameObject EnvironmentUIRoot { get; internal set; }
        public static MatchmakerTimeHasCome.GClass3187 ScreenController { get; internal set; }
        #endregion

        public static string GetGroupId()
        {
            return groupId;
        }

        public static void SetGroupId(string newId)
        {
            groupId = newId;
        }

        public static long GetTimestamp()
        {
            return timestamp;
        }

        public static void SetTimestamp(long ts)
        {
            timestamp = ts;
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

            FikaVersionLabelUpdate_Patch.raidCode = result.RaidCode;
            
            return true;
        }

        public static void CreateMatch(string profileId, string hostUsername, RaidSettings raidSettings)
        {
            long timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            var raidCode = GenerateRaidCode(6);
            var body = new CreateMatch(raidCode, profileId, hostUsername, timestamp, raidSettings, HostExpectedNumberOfPlayers, raidSettings.Side, raidSettings.SelectedDateTime);

            FikaRequestHandler.RaidCreate(body);

            SetGroupId(profileId);
            SetTimestamp(timestamp);
            MatchingType = EMatchmakerType.GroupLeader;
            
            FikaVersionLabelUpdate_Patch.raidCode = raidCode;
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

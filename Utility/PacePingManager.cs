﻿using Celeste.Mod.ConsistencyTracker.Models;
using Celeste.Mod.ConsistencyTracker.PhysicsLog;
using Celeste.Mod.ConsistencyTracker.Stats;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.ConsistencyTracker.Utility {
    public class PacePingManager {

        private ConsistencyTrackerModule Mod => ConsistencyTrackerModule.Instance;

        private const string FolderName = "pace-ping";
        private const string SavedStateFileName = "state.json";
        public const string SaveStateSecretFileName = "state-secret_DONT_SHOW_ON_STREAM.json";
        private bool PingedThisRun { get; set; } = false;
        private bool PBPingedThisRun { get; set; } = false;

        public enum PbPingType {
            NoPing,
            PingOnPbEntry,
            PingOnPbPassed,
        }
        
        public class PaceStateSecret {
            [JsonProperty("webhookUrl")]
            public string WebhookUrl { get; set; } = null;
            
            [JsonProperty("webhookUrlAllDeaths")]
            public string WebhookUrlAllDeaths { get; set; } = null;
        }
        
        public class PaceState {
            //Defaults
            [JsonProperty("webhookUsername")]
            public string WebhookUsername { get; set; } = $"Pace-Bot";
            
            [JsonProperty("defaultPingMessage")]
            public string DefaultPingMessage { get; set; } = $"We got a run to '{{room:name}}'!";

            [JsonProperty("defaultPingDescription")]
            public string DefaultPingDescription { get; set; } = $"Of the campaign '{{campaign:name}}'";

            [JsonProperty("afterPingDeathMessage")]
            public string AfterPingDeathMessage { get; set; } = $"Death to '{{room:name}}' ({{room:roomNumberInChapter}}/{{chapter:roomCount}})";
            
            [JsonProperty("pingCooldownSeconds")]
            public int PingCooldownSeconds { get; set; } = 60;

            //On every golden death
            [JsonProperty("allDeathsMessage")]
            public string AllDeathsMessage { get; set; } = $"Death to '{{room:name}}' ({{room:roomNumberInChapter}}/{{chapter:roomCount}})";

            //On PB
            [JsonProperty("pbPingMessage")]
            public string PbPingMessage { get; set; } = $"On PB pace right now! Room '{{room:name}}' ({{room:roomNumberInChapter}}/{{chapter:roomCount}})";

            //On Win
            [JsonProperty("winMessage")]
            public string WinMessage { get; set; } = $"WIN!!!";
            

            //Ping Timings
            [JsonProperty("pacePingTimings")]
            public Dictionary<string, List<PaceTiming>> PacePingTimings { get; set; } = new Dictionary<string, List<PaceTiming>>();

            //Additional Embed Info
            [JsonProperty("additionalEmbed1Title")]
            public string AdditionEmbed1Title { get; set; } = "Best Runs";
            [JsonProperty("additionalEmbed1Content")]
            public string AdditionEmbed1Content { get; set; } = "{pb:best} | {pb:best#2} | {pb:best#3} | {pb:best#4} | {pb:best#5}";

            [JsonProperty("additionalEmbed2Title")]
            public string AdditionEmbed2Title { get; set; } = "Best Runs (Session)";
            [JsonProperty("additionalEmbed2Content")]
            public string AdditionEmbed2Content { get; set; } = "{pb:bestSession} | {pb:bestSession#2} | {pb:bestSession#3} | {pb:bestSession#4} | {pb:bestSession#5}";

            [JsonProperty("additionalEmbed3Title")]
            public string AdditionEmbed3Title { get; set; } = null;
            [JsonProperty("additionalEmbed3Content")]
            public string AdditionEmbed3Content { get; set; } = null;

            [JsonProperty("additionalEmbed4Title")]
            public string AdditionEmbed4Title { get; set; } = null;
            [JsonProperty("additionalEmbed4Content")]
            public string AdditionEmbed4Content { get; set; } = null;


        }

        public class PaceTiming {
            [JsonProperty("debugRoomName")]
            public string DebugRoomName { get; set; } //"a-08"

            [JsonProperty("customPingMessage")]
            public string CustomPingMessage { get; set; }

            [JsonProperty("enableEmbeds")]
            public bool EmbedsEnabled { get; set; } = true;

            [JsonProperty("lastPingedAt")]
            public DateTime LastPingedAt { get; set; }
        }



        public PaceStateSecret StateSecret { get; set; }
        public PaceState State { get; set; }

        public PacePingManager() {
            LoadState();
        }


        #region State IO
        private void LoadState() {
            ConsistencyTrackerModule.CheckFolderExists(ConsistencyTrackerModule.GetPathToFile(FolderName));
            bool doSave = false;
            
            string stateFilePath = ConsistencyTrackerModule.GetPathToFile(FolderName, SavedStateFileName);
            string stateSecretFilePath = ConsistencyTrackerModule.GetPathToFile(FolderName, SaveStateSecretFileName);

            if (File.Exists(stateFilePath)) {
                string stateFileContents = File.ReadAllText(stateFilePath);
                State = JsonConvert.DeserializeObject<PaceState>(stateFileContents);
            } else {
                State = new PaceState();
                doSave = true;
            }

            if (File.Exists(stateSecretFilePath)) {
                string stateSecretFileContents = File.ReadAllText(stateSecretFilePath);
                StateSecret = JsonConvert.DeserializeObject<PaceStateSecret>(stateSecretFileContents);
            } else {
                StateSecret = new PaceStateSecret();
                doSave = true;
            }

            if (doSave) {
                SaveState();
            }
        }

        private void SaveState() {
            string stateFilePath = ConsistencyTrackerModule.GetPathToFile(FolderName, SavedStateFileName);
            string stateSecretFilePath = ConsistencyTrackerModule.GetPathToFile(FolderName, SaveStateSecretFileName);
            File.WriteAllText(stateFilePath, JsonConvert.SerializeObject(State, Formatting.Indented));
            File.WriteAllText(stateSecretFilePath, JsonConvert.SerializeObject(StateSecret));
        }
        #endregion

        #region Mod Options Actions
        public bool SetCurrentRoomPacePingEnabled(bool isEnabled) {
            bool isNowEnabled = false;
            PathInfo path = Mod.CurrentChapterPath;
            if (path == null || path.CurrentRoom == null) return isNowEnabled;
            
            string id = path.ChapterSID;

            PaceTiming paceTiming = GetPaceTiming(id, path.CurrentRoom.DebugRoomName);
            if (paceTiming == null && isEnabled) {
                if (!State.PacePingTimings.ContainsKey(id)) {
                    State.PacePingTimings.Add(id, new List<PaceTiming>());
                }

                State.PacePingTimings[id].Add(new PaceTiming() {
                    DebugRoomName = path.CurrentRoom.DebugRoomName,
                    CustomPingMessage = null,
                    LastPingedAt = DateTime.MinValue
                });

                isNowEnabled = true;
            } else if (paceTiming != null && isEnabled) {
                isNowEnabled = true;
            } else if (paceTiming != null && !isEnabled) {
                State.PacePingTimings[id].Remove(paceTiming);
            }

            SaveState();
            return isNowEnabled;
        }

        public void TestPingForCurrentRoom() {
            PathInfo path = Mod.CurrentChapterPath;
            if (path == null || path.CurrentRoom == null) return;

            CheckPacePing(path, Mod.CurrentChapterStats, ignoreGolden:true);
        }

        public void ReloadStateFile() {
            LoadState();
        }

        public void SaveDiscordWebhook(string webhook) {
            StateSecret.WebhookUrl = webhook;
            SaveState();
        }
        public void SaveDiscordWebhookAllDeaths(string webhook) {
            StateSecret.WebhookUrlAllDeaths = webhook;
            SaveState();
        }

        public void SaveDefaultPingMessage(string message) {
            State.DefaultPingMessage = message;
            SaveState();
        }
        public void SavePBPingMessage(string message) {
            State.PbPingMessage = message;
            SaveState();
        }
        public void SaveAllDeathsMessage(string message) {
            State.AllDeathsMessage = message;
            SaveState();
        }

        public void SaveCustomPingMessage(string message) {
            PathInfo path = Mod.CurrentChapterPath;
            if (path == null || path.CurrentRoom == null) return;

            string id = path.ChapterSID;

            PaceTiming paceTiming = GetPaceTiming(id, path.CurrentRoom.DebugRoomName);
            if (paceTiming == null) return;

            paceTiming.CustomPingMessage = message;
            SaveState();
        }
        public void SavePaceTimingEmbedsEnabled(bool enabled) {
            PathInfo path = Mod.CurrentChapterPath;
            if (path == null || path.CurrentRoom == null) return;

            string id = path.ChapterSID;

            PaceTiming paceTiming = GetPaceTiming(id, path.CurrentRoom.DebugRoomName);
            if (paceTiming == null) return;

            paceTiming.EmbedsEnabled = enabled;
            SaveState();
        }
        #endregion

        public PaceTiming GetPaceTiming(string chapterSID, string debugRoomName, bool dontLog = false) {
            if (State.PacePingTimings == null) {
                State.PacePingTimings = new Dictionary<string, List<PaceTiming>>();
            }
            if (chapterSID == null) {
                Mod.Log($"{nameof(chapterSID)} '{chapterSID}' is was null");
                return null;
            }
            
            if (!State.PacePingTimings.TryGetValue(chapterSID, out List<PaceTiming> timings)) {
                if (!dontLog) {
                    Mod.Log($"Didn't find room timings for chapter {chapterSID}");
                }
                return null;
            }

            return timings.FirstOrDefault(timing => timing.DebugRoomName == debugRoomName);
        }

        public void CheckPacePing(PathInfo path, ChapterStats stats, bool ignoreGolden = false) {
            if (path == null) return; //No path = no ping
            
            RoomInfo currentRoom = path.CurrentRoom;
            if (currentRoom == null) return; //Not on path = no ping
            if (!Mod.ModSettings.PacePingEnabled) return;

            if (!stats.ModState.PlayerIsHoldingGolden && ignoreGolden == false) return; //No golden = no ping

            if (Mod.ModSettings.PacePingPbPingType != PbPingType.NoPing && !PBPingedThisRun && CheckPbRunPing(path, stats)) {
                return; //Pinged from the PB ping, skip checking normal pace ping
            }

            PaceTiming paceTiming = GetPaceTiming(path.ChapterSID, currentRoom.DebugRoomName);
            if (paceTiming == null) {
                Mod.LogVerbose($"No ping timing setup for room '{currentRoom.GetFormattedRoomName(StatManager.RoomNameType)}'");
                return; //No pace ping setup for current room = no ping
            }

            if (paceTiming.LastPingedAt != DateTime.MinValue && DateTime.Now - paceTiming.LastPingedAt < TimeSpan.FromSeconds(State.PingCooldownSeconds)) {
                return; //On cooldown = no ping
            }
            paceTiming.LastPingedAt = DateTime.Now;
            SaveState();
            
            string message = paceTiming.CustomPingMessage ?? State.DefaultPingMessage;
            SendPing(path, stats, currentRoom, message, paceTiming.EmbedsEnabled);
        }

        /// <summary>
        /// Checks if the player is running a PB attempt and pings if they are
        /// </summary>
        /// <returns>true if pinged, false otherwise</returns>
        public bool CheckPbRunPing(PathInfo path, ChapterStats stats) {
            RoomInfo currentRoom = path.CurrentRoom;
            RoomInfo pbRoom = PersonalBestStat.GetFurthestDeathRoom(path, stats);
            if (pbRoom == null) {
                return false; //First ever golden run = no ping
            }

            if (Mod.ModSettings.PacePingPbPingType == PbPingType.PingOnPbEntry && currentRoom == pbRoom) {
                SendPing(path, stats, currentRoom, State.PbPingMessage, true);
                PBPingedThisRun = true;
                return true;
            } else if (Mod.ModSettings.PacePingPbPingType == PbPingType.PingOnPbPassed && currentRoom.RoomNumberInChapter > pbRoom.RoomNumberInChapter) {
                SendPing(path, stats, currentRoom, State.PbPingMessage, true);
                PBPingedThisRun = true;
                return true;
            }

            return false;
        }
        
        public void SendPing(PathInfo path, ChapterStats stats, RoomInfo pingRoom, string message, bool enableEmbeds) {
            Mod.Log($"Sending pace ping! (Room: {pingRoom.GetFormattedRoomName(StatManager.RoomNameType)}, message: '{message}')");
            try {
                message = Mod.StatsManager.FormatVariableFormat(message);

                string description = State.DefaultPingDescription;
                description = Mod.StatsManager.FormatVariableFormat(description);

                string campaign = path.CampaignName;
                string chapterName = path.ChapterName.Replace(":monikadsidespack_cassette_finale: ", "");
                string sideAddition = path.SideName == "A-Side" ? "" : $" {path.SideName}";
                string chapterField = $"{chapterName}{sideAddition}";

                Dictionary<RoomInfo, Tuple<int, float, int, float>> roomData = ChokeRateStat.GetRoomData(path, stats);
                Tuple<int, float, int, float> currentRoomData = roomData[pingRoom];
                int entries = currentRoomData.Item1;
                int entriesSession = currentRoomData.Item3;

                int totalDeaths = path.Stats.GoldenBerryDeaths;
                int totalDeathsSession = path.Stats.GoldenBerryDeathsSession;

                float totalSuccessRate = path.Stats.SuccessRate;

                //string pbs = "{pb:best} | {pb:best#2} | {pb:best#3} | {pb:best#4} | {pb:best#5}";
                //pbs = Mod.StatsManager.FormatVariableFormat(pbs);
                //string pbsSession = "{pb:bestSession} | {pb:bestSession#2} | {pb:bestSession#3} | {pb:bestSession#4} | {pb:bestSession#5}";
                //pbsSession = Mod.StatsManager.FormatVariableFormat(pbsSession);

                List<DiscordWebhookRequest.Embed> embeds = null;
                if (enableEmbeds) {
                    embeds = new List<DiscordWebhookRequest.Embed>() {
                        new DiscordWebhookRequest.Embed(){
                            //Author = new DiscordWebhookRequest.Author() { Name = "Embed Author" },
                            Title = chapterField,
                            Description = description,
                            Color = 15258703,
                            Fields = new List<DiscordWebhookRequest.Field>(){
                                new DiscordWebhookRequest.Field() { Inline = true, Name = $"Entries to '{pingRoom.GetFormattedRoomName(StatManager.RoomNameType)}'", Value = $"{entries}" },
                                new DiscordWebhookRequest.Field() { Inline = true, Name = "Entries This Session", Value = $"{entriesSession}" },
                                new DiscordWebhookRequest.Field() { Inline = true, Name = "Chapter Success Rate", Value = $"{StatManager.FormatPercentage(totalSuccessRate)}" },
                                new DiscordWebhookRequest.Field() { Inline = true, Name = "Golden Deaths", Value = $"{totalDeaths}" },
                                new DiscordWebhookRequest.Field() { Inline = true, Name = "Golden Deaths (Session)", Value = $"{totalDeathsSession}" },
                                //new DiscordWebhookRequest.Field() { Inline = false, Name = "Best Runs", Value = $"> {pbs}" },
                                //new DiscordWebhookRequest.Field() { Inline = false, Name = "Best Runs (Session)", Value = $"> {pbsSession}" },
                            }
                        },
                    };

                    if (!string.IsNullOrEmpty(State.AdditionEmbed1Title) && !string.IsNullOrEmpty(State.AdditionEmbed1Content)) {
                        string additionEmbed1Title = Mod.StatsManager.FormatVariableFormat(State.AdditionEmbed1Title);
                        string additionEmbed1Content = Mod.StatsManager.FormatVariableFormat(State.AdditionEmbed1Content);
                        embeds[0].Fields.Add(new DiscordWebhookRequest.Field() { Inline = false, Name = additionEmbed1Title, Value = additionEmbed1Content });
                    }
                    if (!string.IsNullOrEmpty(State.AdditionEmbed2Title) && !string.IsNullOrEmpty(State.AdditionEmbed2Content)) {
                        string additionEmbed2Title = Mod.StatsManager.FormatVariableFormat(State.AdditionEmbed2Title);
                        string additionEmbed2Content = Mod.StatsManager.FormatVariableFormat(State.AdditionEmbed2Content);
                        embeds[0].Fields.Add(new DiscordWebhookRequest.Field() { Inline = false, Name = additionEmbed2Title, Value = additionEmbed2Content });
                    }
                    if (!string.IsNullOrEmpty(State.AdditionEmbed3Title) && !string.IsNullOrEmpty(State.AdditionEmbed3Content)) {
                        string additionEmbed3Title = Mod.StatsManager.FormatVariableFormat(State.AdditionEmbed3Title);
                        string additionEmbed3Content = Mod.StatsManager.FormatVariableFormat(State.AdditionEmbed3Content);
                        embeds[0].Fields.Add(new DiscordWebhookRequest.Field() { Inline = false, Name = additionEmbed3Title, Value = additionEmbed3Content });
                    }
                    if (!string.IsNullOrEmpty(State.AdditionEmbed4Title) && !string.IsNullOrEmpty(State.AdditionEmbed4Content)) {
                        string additionEmbed4Title = Mod.StatsManager.FormatVariableFormat(State.AdditionEmbed4Title);
                        string additionEmbed4Content = Mod.StatsManager.FormatVariableFormat(State.AdditionEmbed4Content);
                        embeds[0].Fields.Add(new DiscordWebhookRequest.Field() { Inline = false, Name = additionEmbed4Title, Value = additionEmbed4Content });
                    }
                }

                SendDiscordWebhookMessage(new DiscordWebhookRequest() {
                    Username = State.WebhookUsername,
                    Content = message,
                    Embeds = embeds,
                }, StateSecret.WebhookUrl);

                PingedThisRun = true;
            } catch (Exception ex) {
                Mod.Log($"An exception occurred while trying to send pace ping: {ex}", isFollowup:true);
            }
        }

        public void ResetRun() {
            PingedThisRun = false;
            PBPingedThisRun = false;
        }
        public void DiedWithGolden(PathInfo path, ChapterStats stats) {
            if (Mod.ModSettings.PacePingAllDeathsEnabled) SendAllDeathsMessage(path, stats);
            
            if (!PingedThisRun) return; //no ping, no follow-up message
            PingedThisRun = false;
            PBPingedThisRun = false;

            string message = State.AfterPingDeathMessage;
            message = Mod.StatsManager.FormatVariableFormat(message);

            SendDiscordWebhookMessage(new DiscordWebhookRequest() {
                Username = State.WebhookUsername,
                Content = message,
            }, StateSecret.WebhookUrl);
        }

        public void SendAllDeathsMessage(PathInfo path, ChapterStats stats) {
            string message = State.AllDeathsMessage;
            message = Mod.StatsManager.FormatVariableFormat(message);

            SendDiscordWebhookMessage(new DiscordWebhookRequest() {
                Username = State.WebhookUsername,
                Content = message,
            }, StateSecret.WebhookUrlAllDeaths);
        }

        public void CollectedGolden() {
            try { //Just in case. We DONT want a crash when winning
                if (!PingedThisRun) return; //no ping, no follow-up message
                PingedThisRun = false;

                string message = State.WinMessage;
                message = Mod.StatsManager.FormatVariableFormat(message);

                SendDiscordWebhookMessage(new DiscordWebhookRequest() {
                    Username = State.WebhookUsername,
                    Content = message,
                }, StateSecret.WebhookUrl);
            } catch (Exception ex) {
                Mod.Log($"An exception occurred while trying to send win message: {ex}", isFollowup: true);
            }
        }

        public void SendDiscordWebhookMessage(DiscordWebhookRequest request, string url) {
            Task.Run(() => {
                WebClient client = new WebClient();
                client.Headers.Add("Content-Type", "application/json");
                string payload = JsonConvert.SerializeObject(request);
                client.UploadData(url, Encoding.UTF8.GetBytes(payload));
            });
        }
    }
}

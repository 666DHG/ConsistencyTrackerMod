﻿using Celeste.Mod.ConsistencyTracker.Enums;
using Celeste.Mod.ConsistencyTracker.EverestInterop;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Celeste.Mod.ConsistencyTracker.Models {

    [Serializable]
    public class PathInfo {

        [JsonProperty("campaignName")]
        public string CampaignName { get; set; }

        [JsonProperty("chapterName")]
        public string ChapterName { get; set; }

        [JsonProperty("chapterSID")]
        public string ChapterSID { get; set; }

        [JsonProperty("sideName")]
        public string SideName { get; set; }

        [JsonProperty("checkpoints")]
        public List<CheckpointInfo> Checkpoints { get; set; } = new List<CheckpointInfo>();

        [JsonProperty("roomCount")]
        public int RoomCount {
            get {
                return Checkpoints.Sum((cpInfo) => cpInfo.RoomCount);
            }
        }
        [JsonProperty("gameplayRoomCount")]
        public int GameplayRoomCount {
            get {
                return Checkpoints.Sum((cpInfo) => cpInfo.GameplayRoomCount);
            }
        }

        [JsonProperty("ignoredRooms")]
        public List<string> IgnoredRooms { get; set; } = new List<string>();

        [JsonIgnore]
        public AggregateStats Stats { get; set; } = null;
        
        [JsonIgnore]
        public RoomInfo CurrentRoom { get; set; } = null;
        
        [JsonIgnore]
        public RoomInfo SpeedrunToolSaveStateRoom { get; set; } = null;

        [JsonIgnore]
        public string ParseError { get; set; }

        public static PathInfo GetTestPathInfo() {
            return new PathInfo() {
                Checkpoints = new List<CheckpointInfo>() {
                    new CheckpointInfo(){ Name="Start", Abbreviation="0M" },
                    new CheckpointInfo(){ Name="500 M", Abbreviation="500M" },
                },
            };
        }

        public RoomInfo GetRoom(RoomStats roomStats) {
            return GetRoom(roomStats.DebugRoomName);
        }
        public RoomInfo GetRoom(string roomName) {
            foreach (CheckpointInfo cpInfo in Checkpoints) {
                RoomInfo rInfo = cpInfo.Rooms.Find((r) => r.DebugRoomName == roomName);
                if (rInfo != null) return rInfo;
            }

            return null;
        }

        /// <summary>
        /// Returns the actual RoomInfo objects to a given list of room debug names.
        /// Removes deaths to rooms not on the path, but keeps null values (golden win runs)
        /// </summary>
        public List<RoomInfo> GetRoomsForLastRuns(List<string> lastRuns) {
            List<RoomInfo> filtered = new List<RoomInfo>();

            foreach (string roomName in lastRuns) {
                if (roomName == null) {
                    filtered.Add(null);
                    continue;
                }

                RoomInfo rInfo = GetRoom(roomName);
                if (rInfo != null) {
                    filtered.Add(rInfo);
                }
            }
            
            return filtered;
        }

        /// <summary>
        /// Returns the room number of a given run. If the run is a golden win, rInfo is null and GameplayRoomCount+1 is returned.
        /// </summary>
        /// <param name="rInfo">RoomInfo of the run, or null if it's a golden win run</param>
        /// <returns>Room number associated with the run</returns>
        public int GetRunRoomNumberInChapter(RoomInfo rInfo) {
            if (rInfo == null) return GetWinRoomNumber();
            return rInfo.RoomNumberInChapter;
        }
        public int GetRunRoomNumberInChapter(string debugRoomName) {
            if (debugRoomName == null) return GetWinRoomNumber();

            RoomInfo rInfo = GetRoom(debugRoomName); //Room not on path
            if (rInfo == null) return 0;

            return rInfo.RoomNumberInChapter;
        }
        public int GetWinRoomNumber() {
            return GameplayRoomCount + 1;
        }

        public override string ToString() {
            List<string> lines = new List<string>();

            foreach (CheckpointInfo cpInfo in Checkpoints) {
                lines.Add(cpInfo.ToString());
            }

            return string.Join("\n", lines);
        }
        public static PathInfo ParseString(string content) {
            ConsistencyTrackerModule.Instance.Log($"Parsing path info string");
            List<string> lines = content.Trim().Split(new string[] { "\n" }, StringSplitOptions.None).ToList();

            PathInfo pathInfo = new PathInfo();

            foreach (string line in lines) {
                ConsistencyTrackerModule.Instance.Log($"\tParsing line '{line}'");
                pathInfo.Checkpoints.Add(CheckpointInfo.ParseString(line));
            }

            return pathInfo;
        }

        public void SetCheckpointRefs() {
            foreach (CheckpointInfo cpInfo in Checkpoints) {
                cpInfo.Chapter = this;
                cpInfo.SetCheckpointRefs();
            }
        }
    }

    [Serializable]
    public class CheckpointInfo {

        [JsonIgnore]
        public PathInfo Chapter { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonProperty("roomCount")]
        public int RoomCount {
            get => Rooms.Count;
        }
        [JsonProperty("gameplayRoomCount")]
        public int GameplayRoomCount {
            get => GameplayRooms.Count;
        }

        [JsonProperty("rooms")]
        public List<RoomInfo> Rooms { get; set; } = new List<RoomInfo>();

        [JsonIgnore]
        public List<RoomInfo> GameplayRooms {
            get => Rooms.Where((r) => r.IsNonGameplayRoom == false).ToList();
        }

        [JsonIgnore]
        public AggregateStats Stats { get; set; } = null;
        [JsonIgnore]
        public int CPNumberInChapter { get; set; } = -1;
        [JsonIgnore]
        public double GoldenChance { get; set; } = 1;

        public override string ToString() {
            string toRet = $"{Name};{Abbreviation};{Rooms.Count}";
            string debugNames = string.Join(",", Rooms);
            return $"{toRet};{debugNames}";
        }

        public static CheckpointInfo ParseString(string line) {
            List<string> parts = line.Trim().Split(new string[] { ";" }, StringSplitOptions.None).ToList();
            string name = parts[0];
            string abbreviation = parts[1];

            List<string> rooms = parts[3].Split(new string[] { "," }, StringSplitOptions.None).ToList();
            List<RoomInfo> roomInfos = new List<RoomInfo>();

            CheckpointInfo cpInfo = new CheckpointInfo() {
                Name = name,
                Abbreviation = abbreviation,
            };

            foreach (string room in rooms) {
                roomInfos.Add(new RoomInfo() { DebugRoomName = room, Checkpoint = cpInfo });
            }

            cpInfo.Rooms = roomInfos;

            return cpInfo;
        }

        public void SetCheckpointRefs() {
            foreach (RoomInfo rInfo in Rooms) {
                rInfo.Checkpoint = this;
            }
        }
    }

    [Serializable]
    public class RoomInfo {

        [JsonIgnore]
        public CheckpointInfo Checkpoint { get; set; }

        [JsonIgnore]
        public RoomInfo NextRoomInCheckpoint { 
            get {
                if (Checkpoint == null) return null;

                int index = Checkpoint.Rooms.IndexOf(this);
                if (index == Checkpoint.Rooms.Count - 1) return null;

                return Checkpoint.Rooms[index + 1];
            }
        }
        [JsonIgnore]
        public RoomInfo NextGameplayRoomInCheckpoint {
            get {
                RoomInfo nextRoom = NextRoomInCheckpoint;
                while (nextRoom != null && nextRoom.IsNonGameplayRoom) {
                    nextRoom = nextRoom.NextRoomInCheckpoint;
                }
                if (nextRoom == null || nextRoom.IsNonGameplayRoom) return null;
                return nextRoom;
            }
        }
        [JsonIgnore]
        public RoomInfo PreviousRoomInCheckpoint {
            get {
                if (Checkpoint == null) return null;

                int index = Checkpoint.Rooms.IndexOf(this);
                if (index == 0) return null;

                return Checkpoint.Rooms[index - 1];
            }
        }
        [JsonIgnore]
        public RoomInfo PreviousGameplayRoomInCheckpoint {
            get {
                RoomInfo prevRoom = PreviousRoomInCheckpoint;
                while (prevRoom != null && prevRoom.IsNonGameplayRoom) {
                    prevRoom = prevRoom.PreviousRoomInCheckpoint;
                }
                if (prevRoom == null || prevRoom.IsNonGameplayRoom) return null;
                return prevRoom;
            }
        }
        [JsonIgnore]
        public RoomInfo NextRoomInChapter {
            get {
                if (Checkpoint == null) return null;

                RoomInfo nextRoom = NextRoomInCheckpoint;
                if (nextRoom != null) return nextRoom;

                bool retNext = false;
                CheckpointInfo nextCp = null;
                foreach (CheckpointInfo cpInfo in Checkpoint.Chapter.Checkpoints) {
                    if (retNext == true) {
                        nextCp = cpInfo;
                        break;
                    }
                    if (cpInfo == Checkpoint) {
                        retNext = true;
                    }
                }

                if (nextCp == null) return null;
                if (nextCp.RoomCount == 0) return null;
                return nextCp.Rooms[0];
            }
        }
        [JsonIgnore]
        public RoomInfo NextGameplayRoomInChapter {
            get {
                RoomInfo nextRoom = NextRoomInChapter;
                while (nextRoom != null && nextRoom.IsNonGameplayRoom) {
                    nextRoom = nextRoom.NextRoomInChapter;
                }
                if (nextRoom == null || nextRoom.IsNonGameplayRoom) return null;
                return nextRoom;
            }
        }
        [JsonIgnore]
        public RoomInfo PreviousRoomInChapter {
            get {
                if (Checkpoint == null) return null;

                RoomInfo previousRoom = PreviousRoomInCheckpoint;
                if (previousRoom != null) return previousRoom;
                
                CheckpointInfo prevCp = null;
                foreach (CheckpointInfo cpInfo in Checkpoint.Chapter.Checkpoints) {
                    if (cpInfo == Checkpoint) {
                        break;
                    }
                    prevCp = cpInfo;
                }

                if (prevCp == null) return null;
                if (prevCp.RoomCount == 0) return null;
                return prevCp.Rooms[prevCp.RoomCount - 1];
            }
        }
        [JsonIgnore]
        public RoomInfo PreviousGameplayRoomInChapter {
            get {
                RoomInfo prevRoom = PreviousRoomInCheckpoint;
                while (prevRoom != null && prevRoom.IsNonGameplayRoom) {
                    prevRoom = prevRoom.PreviousRoomInCheckpoint;
                }
                if (prevRoom == null || prevRoom.IsNonGameplayRoom) return null;
                return prevRoom;
            }
        }



        [JsonProperty("debugRoomName")]
        public string DebugRoomName { get; set; }

        [JsonProperty("groupedRooms")]
        public List<string> GroupedRooms { get; set; } = new List<string>();

        [JsonProperty("customRoomName")]
        public string CustomRoomName { get; set; }

        [JsonProperty("isNonGameplayRoom")]
        public bool IsNonGameplayRoom { get; set; }

        public override string ToString() {
            return DebugRoomName;
        }

        [JsonIgnore]
        public int RoomNumberInCP { get; set; } = -1;
        [JsonIgnore]
        public int RoomNumberInChapter { get; set; } = -1;

        public string GetFormattedRoomName(RoomNameDisplayType format) {
            if (!string.IsNullOrEmpty(CustomRoomName)) return CustomRoomName;

            if (format != RoomNameDisplayType.DebugRoomName && IsNonGameplayRoom) {
                RoomInfo nextRoom = NextGameplayRoomInChapter;

                if (nextRoom == null) {
                    switch (format) {
                        case RoomNameDisplayType.AbbreviationAndRoomNumberInCP:
                        case RoomNameDisplayType.FullNameAndRoomNumberInCP:
                            return $"End";
                    }
                } else {
                    if (string.IsNullOrEmpty(nextRoom.CustomRoomName)) {
                        switch (format) {
                            case RoomNameDisplayType.AbbreviationAndRoomNumberInCP:
                                return $"{nextRoom.Checkpoint.Abbreviation}-T{nextRoom.RoomNumberInCP}";
                            case RoomNameDisplayType.FullNameAndRoomNumberInCP:
                                return $"{nextRoom.Checkpoint.Name}-T{nextRoom.RoomNumberInCP}";
                        }
                    } else {
                        switch (format) {
                            case RoomNameDisplayType.AbbreviationAndRoomNumberInCP:
                                return $"T-{nextRoom.CustomRoomName}";
                            case RoomNameDisplayType.FullNameAndRoomNumberInCP:
                                return $"Transition-{nextRoom.CustomRoomName}";
                        }
                    }
                }
            }
            
            switch (format) {
                case RoomNameDisplayType.AbbreviationAndRoomNumberInCP:
                    return $"{Checkpoint.Abbreviation}-{RoomNumberInCP}";
                case RoomNameDisplayType.FullNameAndRoomNumberInCP:
                    return $"{Checkpoint.Name}-{RoomNumberInCP}";
                case RoomNameDisplayType.DebugRoomName:
                    return DebugRoomName;
            }

            return DebugRoomName;
        }

    }



    public class AggregateStats {
        public int CountSuccesses { get; set; } = 0;
        public int CountAttempts { get; set; } = 0;
        public int CountFailures {
            get {
                return CountAttempts - CountSuccesses;
            }
        }
        public float SuccessRate {
            get {
                if (CountAttempts == 0) return 0;

                return (float)CountSuccesses / CountAttempts;
            }
        }

        public int GoldenBerryDeaths { get; set; } = 0;
        public int GoldenBerryDeathsSession { get; set; } = 0;

        public float GoldenChance { get; set; } = 1;
    }
}

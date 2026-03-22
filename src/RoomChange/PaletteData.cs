using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace RoomChange;

public class PaletteData
{
    [JsonProperty("palette", NullValueHandling = NullValueHandling.Ignore)]
    public List<int> BasePalette { get; set; }

    [JsonProperty("time", NullValueHandling = NullValueHandling.Ignore)]
    public List<float> BaseTime { get; set; }

    [JsonProperty("terrain", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> TerrainPalette { get; set; }

    [JsonProperty("terrainTime", NullValueHandling = NullValueHandling.Ignore)]
    public List<float> TerrainTime { get; set; }


    [JsonProperty("effectA", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> EffectAPalette { get; set; }

    [JsonProperty("effectATime", NullValueHandling = NullValueHandling.Ignore)]
    public List<float> EffectATime { get; set; }

     [JsonProperty("effectB", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> EffectBPalette { get; set; }

    [JsonProperty("effectBTime", NullValueHandling = NullValueHandling.Ignore)]
    public List<float> EffectBTime { get; set; }

    public int BaseLength => BasePalette?.Count ?? 0;

    public int TerrainLength => TerrainPalette?.Count ?? 0;

    public int EffectALength => EffectAPalette?.Count ?? 0;
    public int EffectBLength => EffectBPalette?.Count ?? 0;

}

/// <summary>
/// Represents generic palette sequence information with timing data
/// </summary>
public struct PaletteSequence<T>
{
    public List<T> Palettes;
    public List<float> Times;
    public int Length => Palettes?.Count ?? 0;

    public PaletteSequence(List<T> palettes, List<float> times)
    {
        Palettes = palettes;
        Times = times;
    }

    public bool IsValid() => Palettes != null && Times != null && Palettes.Count > 0 && Palettes.Count == Times.Count;
}

/// <summary>
/// Information about palette intervals for blending
/// </summary>
public struct PaletteInterval
{
    public int CurrentIndex;
    public int PrevIndex;
    public int NextIndex;
    public float LastTime;
    public float NextTime;
    public float BlendFactor;

    public bool IsLastPalette;
}

public static class PaletteInfo
{
    // Old system (for compatibility)
    public static Dictionary<string, RoomChange.PaletteData> Palettes = new Dictionary<string, RoomChange.PaletteData>();

    // New cycle configuration system
    // Key: Region name or full room name
    // Value: List of PaletteData indexed by file ID (01, 02, 03, etc.)
    public static Dictionary<string, List<PaletteData>> CycleConfigurations = new Dictionary<string, List<PaletteData>>();

    private static int RainCycleLength;

    public static void SetRainCycleLength(int length)
    {
        RainCycleLength = length;
    }

    public static bool IsRegionPaletteAvailable(Room self, ref string room)
    {
        Region region = self.world.region;

        // Priority 1: Check CycleConfigurations for specific room
        if (CycleConfigurations.ContainsKey(self.abstractRoom.name))
        {
            room = self.abstractRoom.name;
            return true;
        }

        // Priority 2: Check CycleConfigurations for region
        if (CycleConfigurations.ContainsKey(region.name))
        {
            room = region.name;
            return true;
        }

        // Priority 3: Check legacy Palettes system for specific room
        if (Palettes != null && Palettes.ContainsKey(self.abstractRoom.name))
        {
            room = self.abstractRoom.name;
            
            if (Palettes[room].BasePalette == null || Palettes[room].BasePalette.Count == 0)
            {
                PDEBUG.Log($"Palette not found for {room}");
                return false;
            }
            return true;
        }

        // Priority 4: Check legacy Palettes system for region
        if (Palettes != null && Palettes.ContainsKey(region.name))
        {
            room = region.name;
            
            if (Palettes[room].BasePalette == null || Palettes[room].BasePalette.Count == 0)
            {
                PDEBUG.Log($"Palette not found for {room}");
                return false;
            }
            return true;
        }

        // No configuration found
        PDEBUG.Log($"NOT FOUND | No palettes found for region: {region.name}");
        return false;
    }

    /// <summary>
    /// Generic method to calculate palette intervals for any palette sequence
    /// </summary>
    public static PaletteInterval CalculateIntervals<T>(float currentTime, PaletteSequence<T> sequence)
    {
        if (!sequence.IsValid())
        {
            PDEBUG.Log("Invalid palette sequence provided.");
            return default;
        }

        PaletteInterval interval = new PaletteInterval();

        // Find the current palette interval based on time
        for (int i = 1; i < sequence.Length; i++)
        {
            float endTimePalette = sequence.Times[i] * RainCycleLength;
            if (currentTime < endTimePalette)
            {
                interval.CurrentIndex = i - 1;
                interval.PrevIndex = Math.Max(interval.CurrentIndex, 0);
                interval.NextIndex = Math.Min(interval.CurrentIndex + 1, sequence.Length - 1);
                interval.LastTime = sequence.Times[interval.CurrentIndex] * RainCycleLength;
                interval.NextTime = endTimePalette;
                interval.BlendFactor = Transitions.Linear.GetBlend(currentTime, interval.LastTime, interval.NextTime);
                interval.IsLastPalette = false;
                return interval;
            }
        }

        // We've passed all transitions - use the last palette
        interval.CurrentIndex = sequence.Length - 1;
        interval.PrevIndex = interval.CurrentIndex;
        interval.NextIndex = interval.CurrentIndex;
        interval.LastTime = RainCycleLength * sequence.Times[interval.CurrentIndex];
        interval.NextTime = Mathf.Infinity;
        interval.BlendFactor = 1f;
        interval.IsLastPalette = true;

        return interval;
    }

    /// <summary>
    /// Helper method to get a palette sequence from PaletteData
    /// </summary>
    public static PaletteSequence<int> GetBasePaletteSequence(PaletteData data)
    {
        return new PaletteSequence<int>(data.BasePalette, data.BaseTime);
    }

    public static PaletteSequence<string> GetEffectAPaletteSequence(PaletteData data)
    {
        return new PaletteSequence<string>(data.EffectAPalette, data.EffectATime);
    }

    public static PaletteSequence<string> GetEffectBPaletteSequence(PaletteData data)
    {
        return new PaletteSequence<string>(data.EffectBPalette, data.EffectBTime);
    }

    public static PaletteSequence<string> GetTerrainPaletteSequence(PaletteData data)
    {
        return new PaletteSequence<string>(data.TerrainPalette, data.TerrainTime);
    }

    // ============================================
    // Methods for the cycle configuration system
    // ============================================

    /// <summary>
    /// Gets a palette for a specific region or room by cycle index
    /// </summary>
    public static PaletteData GetCyclePalette(string key, int cycleIndex)
    {
        if (CycleConfigurations.TryGetValue(key, out var configs))
        {
            if (cycleIndex < 0)
            {
                //Yield Error?????
                cycleIndex = 0;
                PDEBUG.Log($"Invalid cycle index {cycleIndex} for key {key}.");
            }

            cycleIndex = cycleIndex % configs.Count;

            if (configs[cycleIndex] != null)
            {
                return configs[cycleIndex];
            }
        }else{
            Palettes.TryGetValue(key, out var hotPalette);
            if (hotPalette != null)
            {
                return hotPalette;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the total number of configured cycles for a region/room
    /// </summary>
    public static int GetCycleCount(string key)
    {
        if (CycleConfigurations.TryGetValue(key, out var configs))
        {
            return configs.Count;
        }

        return 0;
    }

    /// <summary>
    /// Clears all cycle configuration data
    /// </summary>
    public static void CleanupCycleConfigurations()
    {
        CycleConfigurations.Clear();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RainCycles.Snapshot;
using RainCycles.Core;
using System.IO;

namespace RainCycles.Blend;

public static partial class RoomCameraExtensions
{
    private class CachedStateData
    {
        public Color32[] mainPixels;
        public Color32[] fadePixels;
        public int fadePaletteId;
        public float[] fadeOpacities;
        public int mainPaletteId;
    }
    
    private static Dictionary<(string roomName, int state), CachedStateData> _stateCache = 
        new Dictionary<(string, int), CachedStateData>();
    
    private static bool _preloadHooksInitialized = false;
    
    // ============================================================
    // ROOMBLENDSTATE
    // ============================================================
    public readonly struct RoomBlendState
    {
        public readonly bool IsBlend;
        public readonly bool IsStatic;
        public readonly bool HasFullStates;
        public readonly bool HasView;
        public readonly bool HasTint;
        public readonly ViewType View;
        public readonly SkyType Sky;

        public RoomBlendState(bool isBlend, bool isStatic, bool hasFullStates,
                              bool hasView, bool hasTint, ViewType view, SkyType sky)
        {
            IsBlend = isBlend;
            IsStatic = isStatic;
            HasFullStates = hasFullStates;
            HasView = hasView;
            HasTint = hasTint;
            View = view;
            Sky = sky;
        }
    }

    private static readonly Dictionary<string, RoomBlendState> _roomStateCache =
        new Dictionary<string, RoomBlendState>(StringComparer.OrdinalIgnoreCase);

    public static RoomBlendState GetRoomBlendState(Room room)
    {
        string roomName = room?.abstractRoom?.name;
        if (string.IsNullOrEmpty(roomName)) return default;

        if (_roomStateCache.TryGetValue(roomName, out var state)) return state;

        state = ComputeRoomBlendState(room);
        _roomStateCache[roomName] = state;
        return state;
    }

    private static RoomBlendState ComputeRoomBlendState(Room room)
    {
        var snap = SettingsSnapshot.GetCached(room?.roomSettings?.filePath, room?.abstractRoom?.name);
        if (snap == null) return default;

        string roomName = room.abstractRoom?.name;
        bool isBlend = snap.HasRcType && snap.RcType == RcType.Blend;
        bool isStatic = snap.HasRcType && snap.RcType == RcType.Static;
        bool hasFull = isBlend && StateFileResolver.HasFullStates(roomName ?? "");

        return new RoomBlendState(
            isBlend, isStatic, hasFull,
            snap.HasView, snap.HasTint, snap.ViewType,
            snap.ViewType == ViewType.ACV ? SkyType.ACV
                : snap.ViewType == ViewType.RTV ? SkyType.RTV
                : snap.ViewType == ViewType.PSV ? SkyType.PSV : SkyType.None);
    }

    public static void InvalidateRoomCache(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return;
        _roomStateCache.Remove(roomName);
    }
    
    public static void InvalidateAllRoomCaches()
    {
        _roomStateCache.Clear();
    }
    
    public static void InitPreloadHooks()
    {
        if (_preloadHooksInitialized) return;
        
        On.Room.Loaded += OnRoomLoaded;
        On.Room.Unloaded += OnRoomUnloaded;
        
        _preloadHooksInitialized = true;
    }
    
    private static void OnRoomLoaded(On.Room.orig_Loaded orig, Room self)
    {
        orig(self);
        
        if (self?.abstractRoom == null) return;
        
        string roomName = self.abstractRoom.name;
        if (string.IsNullOrEmpty(roomName)) return;
        
        if (!GetRoomBlendState(self).IsBlend) return;
        
        PreloadRoomStates(roomName);
        GetOrCreateTerrainGrid(roomName);
    }
    
    private static void OnRoomUnloaded(On.Room.orig_Unloaded orig, Room self)
    {
        string roomName = self?.abstractRoom?.name;
        if (!string.IsNullOrEmpty(roomName))
        {
            UnloadRoomCache(roomName);
            UnloadRoomTerrainCache(roomName);
            InvalidateRoomCache(roomName);
        }
        
        orig(self);
    }
    
    public static void PreloadRoomStates(string roomName)
    {
        for (int state = 1; state <= 4; state++)
        {
            string path = StateFileResolver.GetRainStateSettingsFile(roomName, state);
            if (path != null)
            {
                var snap = SettingsSnapshot.GetCached(path, roomName);
                if (snap != null)
                {
                    GetOrLoadState(roomName, state, snap, out _, out _, out _, out _);
                }
            }
        }
    }
    
    public static void UnloadRoomCache(string roomName)
    {
        var keysToRemove = _stateCache.Keys.Where(k => k.roomName == roomName).ToList();
        foreach (var key in keysToRemove)
        {
            _stateCache.Remove(key);
        }
    }
    
    internal static void ClearAllCaches()
    {
        _stateCache.Clear();
        InvalidateAllRoomCaches();
        ClearAllTerrainCaches();
    }
    
    // ============================================================
    // MÉTODOS PÚBLICOS PARA RECARGA DE TERRAIN CACHE
    // ============================================================
    
    public static void InvalidateRoomTerrainCache(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return;
        UnloadRoomTerrainCache(roomName);
    }
    
    public static void ReloadRoomTerrainCache(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return;
        UnloadRoomTerrainCache(roomName);
        GetOrCreateTerrainGrid(roomName);
    }
    
    // ============================================================
    // MÉTODOS PRIVADOS - ROOM PALETTE
    // ============================================================
    
    private static void GetOrLoadState(string roomName, int state, SettingsSnapshot snap,
        out Color32[] mainPixels, out Color32[] fadePixels, out int fadePaletteId, out float[] fadeOpacities)
    {
        var key = (roomName, state);
        
        if (_stateCache.TryGetValue(key, out var cached))
        {
            mainPixels = cached.mainPixels;
            fadePixels = cached.fadePixels;
            fadePaletteId = cached.fadePaletteId;
            fadeOpacities = cached.fadeOpacities;
            return;
        }
        
        LoadPaletteTextureToArray(snap.Palette, out mainPixels);
        
        fadePixels = null;
        fadePaletteId = -1;
        fadeOpacities = null;
        
        if (snap._hasFadePalette && snap.FadePaletteID >= 0)
        {
            LoadPaletteTextureToArray(snap.FadePaletteID, out fadePixels);
            fadePaletteId = snap.FadePaletteID;
            fadeOpacities = snap.FadePaletteOpacities;
        }
        
        ApplyEffectColorsToTextureArray(ref mainPixels, snap.EffectColorA, snap.EffectColorB);
        if (fadePixels != null)
        {
            ApplyEffectColorsToTextureArray(ref fadePixels, snap.EffectColorA, snap.EffectColorB);
        }
        
        _stateCache[key] = new CachedStateData
        {
            mainPixels = mainPixels,
            fadePixels = fadePixels,
            fadePaletteId = fadePaletteId,
            fadeOpacities = fadeOpacities,
            mainPaletteId = snap.Palette
        };
    }
    
    private static void LoadPaletteTextureToArray(int palId, out Color32[] pixels)
    {
        if (palId < 0) 
        { 
            pixels = null; 
            return; 
        }
        
        Texture2D texture = new Texture2D(32, 16, TextureFormat.RGBA32, false);
        
        string path = AssetManager.ResolveFilePath($"palettes{Path.DirectorySeparatorChar}palette{palId}.png");
        
        try { AssetManager.SafeWWWLoadTexture(ref texture, "file:///" + path, false, true); }
        catch {
            RSPlugin.log.LogWarning($"[PaletteCache] No se pudo cargar palette {palId}, fallback a palette-1");
            path = AssetManager.ResolveFilePath($"palettes{Path.DirectorySeparatorChar}palette-1.png");
            AssetManager.SafeWWWLoadTexture(ref texture, "file:///" + path, false, true);
        }
        
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        pixels = texture.GetPixels32();
        UnityEngine.Object.Destroy(texture);
    }
    
    private static void ApplyEffectColorsToTextureArray(ref Color32[] pixels, int effectColorA, int effectColorB)
    {
        if (pixels == null || pixels.Length != 512) return;
        if (RoomCamera.allEffectColorsTexture == null) return;
        
        if (effectColorA >= 0)
        {
            Color[] light = RoomCamera.allEffectColorsTexture.GetPixels(effectColorA * 2, 0, 2, 2);
            Color[] dark = RoomCamera.allEffectColorsTexture.GetPixels(effectColorA * 2, 2, 2, 2);
            
            int[] lightIndices = { 158, 159, 190, 191 };
            int[] darkIndices = { 414, 415, 446, 447 };
            
            for (int i = 0; i < 4; i++)
            {
                pixels[lightIndices[i]] = new Color32(
                    (byte)(light[i].r * 255), (byte)(light[i].g * 255), (byte)(light[i].b * 255), 255);
                pixels[darkIndices[i]] = new Color32(
                    (byte)(dark[i].r * 255), (byte)(dark[i].g * 255), (byte)(dark[i].b * 255), 255);
            }
        }
        
        if (effectColorB >= 0)
        {
            Color[] light = RoomCamera.allEffectColorsTexture.GetPixels(effectColorB * 2, 0, 2, 2);
            Color[] dark = RoomCamera.allEffectColorsTexture.GetPixels(effectColorB * 2, 2, 2, 2);
            
            int[] lightIndices = { 94, 95, 126, 127 };
            int[] darkIndices = { 350, 351, 382, 383 };
            
            for (int i = 0; i < 4; i++)
            {
                pixels[lightIndices[i]] = new Color32(
                    (byte)(light[i].r * 255), (byte)(light[i].g * 255), (byte)(light[i].b * 255), 255);
                pixels[darkIndices[i]] = new Color32(
                    (byte)(dark[i].r * 255), (byte)(dark[i].g * 255), (byte)(dark[i].b * 255), 255);
            }
        }
    }
}
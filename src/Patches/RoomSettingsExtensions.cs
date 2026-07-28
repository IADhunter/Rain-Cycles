using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using RainCycles.Snapshot;

namespace RainCycles.Patches;

// Almacena datos extendidos de RoomSettings que el juego vanilla no maneja
public static class RoomSettingsExtensions
{
    private static readonly ConditionalWeakTable<RoomSettings, ExtData> _table
        = new ConditionalWeakTable<RoomSettings, ExtData>();

    private class ExtData
    {
        public RcType RcType = RcType.None;
        public ViewType ViewType = ViewType.None;
        public Color? TintMultiply = null;
        public Color? TintAtmosphere = null;
    }

    private static ExtData GetOrCreate(RoomSettings settings)
    {
        if (!_table.TryGetValue(settings, out var data))
        {
            data = new ExtData();
            _table.Add(settings, data);
        }
        return data;
    }

    public static RcType GetRcType(this RoomSettings settings)
    {
        return GetOrCreate(settings).RcType;
    }

    public static void SetRcType(this RoomSettings settings, RcType value)
    {
        var data = GetOrCreate(settings);
        data.RcType = value;
        if (!HasRcType(settings))
        {
            data.ViewType = ViewType.None;
            data.TintMultiply = null;
            data.TintAtmosphere = null;
        }
    }

    public static bool HasRcType(this RoomSettings settings)
    {
        return GetOrCreate(settings).RcType != RcType.None;
    }

    public static ViewType GetViewType(this RoomSettings settings)
    {
        return GetOrCreate(settings).ViewType;
    }

    public static void SetViewType(this RoomSettings settings, ViewType value)
    {
        if (!HasRcType(settings)) return;
        var data = GetOrCreate(settings);
        data.ViewType = value;
        if (data.ViewType == ViewType.None)
        {
            data.TintMultiply = null;
            data.TintAtmosphere = null;
        }
    }

    public static bool HasView(this RoomSettings settings)
    {
        var data = GetOrCreate(settings);
        return data.RcType != RcType.None && data.ViewType != ViewType.None;
    }

    public static Color? GetTintMultiply(this RoomSettings settings)
    {
        return GetOrCreate(settings).TintMultiply;
    }

    public static void SetTintMultiply(this RoomSettings settings, Color? value)
    {
        if (!HasView(settings)) return;
        GetOrCreate(settings).TintMultiply = value;
    }

    public static Color? GetTintAtmosphere(this RoomSettings settings)
    {
        return GetOrCreate(settings).TintAtmosphere;
    }

    public static void SetTintAtmosphere(this RoomSettings settings, Color? value)
    {
        if (!HasView(settings)) return;
        GetOrCreate(settings).TintAtmosphere = value;
    }

    public static bool HasTint(this RoomSettings settings)
    {
        var data = GetOrCreate(settings);
        return HasView(settings) && (data.TintMultiply.HasValue || data.TintAtmosphere.HasValue);
    }

    public static void ClearTint(this RoomSettings settings)
    {
        var data = GetOrCreate(settings);
        data.TintMultiply = null;
        data.TintAtmosphere = null;
    }

    public static void ClearViewAndTint(this RoomSettings settings)
    {
        var data = GetOrCreate(settings);
        data.ViewType = ViewType.None;
        data.TintMultiply = null;
        data.TintAtmosphere = null;
    }

    public static void ClearExtendedData(this RoomSettings settings)
    {
        var data = GetOrCreate(settings);
        data.RcType = RcType.None;
        data.ViewType = ViewType.None;
        data.TintMultiply = null;
        data.TintAtmosphere = null;
    }
}

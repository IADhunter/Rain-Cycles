using System;
using UnityEngine;
using RainCycles.Snapshot;

namespace RainCycles.Patches;

// Almacena datos extendidos de RoomSettings que el juego vanilla no maneja
public static class RoomSettingsExtensions
{
    private static class Storage
    {
        public static RcType RcType = RcType.None;
        public static ViewType ViewType = ViewType.None;
        public static Color? TintMultiply = null;
        public static Color? TintAtmosphere = null;
    }

    public static RcType GetRcType(this RoomSettings settings)
    {
        return Storage.RcType;
    }

    public static void SetRcType(this RoomSettings settings, RcType value)
    {
        Storage.RcType = value;
        if (!HasRcType(settings))
        {
            Storage.ViewType = ViewType.None;
            Storage.TintMultiply = null;
            Storage.TintAtmosphere = null;
        }
    }

    public static bool HasRcType(this RoomSettings settings)
    {
        return Storage.RcType != RcType.None;
    }

    public static ViewType GetViewType(this RoomSettings settings)
    {
        return Storage.ViewType;
    }

    public static void SetViewType(this RoomSettings settings, ViewType value)
    {
        if (!HasRcType(settings)) return;
        Storage.ViewType = value;
        if (Storage.ViewType == ViewType.None)
        {
            Storage.TintMultiply = null;
            Storage.TintAtmosphere = null;
        }
    }

    public static bool HasView(this RoomSettings settings)
    {
        return HasRcType(settings) && Storage.ViewType != ViewType.None;
    }

    public static Color? GetTintMultiply(this RoomSettings settings)
    {
        return Storage.TintMultiply;
    }

    public static void SetTintMultiply(this RoomSettings settings, Color? value)
    {
        if (!HasView(settings)) return;
        Storage.TintMultiply = value;
    }

    public static Color? GetTintAtmosphere(this RoomSettings settings)
    {
        return Storage.TintAtmosphere;
    }

    public static void SetTintAtmosphere(this RoomSettings settings, Color? value)
    {
        if (!HasView(settings)) return;
        Storage.TintAtmosphere = value;
    }

    public static bool HasTint(this RoomSettings settings)
    {
        return HasView(settings) && (Storage.TintMultiply.HasValue || Storage.TintAtmosphere.HasValue);
    }

    public static void ClearTint(this RoomSettings settings)
    {
        Storage.TintMultiply = null;
        Storage.TintAtmosphere = null;
    }

    public static void ClearViewAndTint(this RoomSettings settings)
    {
        Storage.ViewType = ViewType.None;
        Storage.TintMultiply = null;
        Storage.TintAtmosphere = null;
    }

    public static void ClearExtendedData(this RoomSettings settings)
    {
        Storage.RcType = RcType.None;
        Storage.ViewType = ViewType.None;
        Storage.TintMultiply = null;
        Storage.TintAtmosphere = null;
    }
}
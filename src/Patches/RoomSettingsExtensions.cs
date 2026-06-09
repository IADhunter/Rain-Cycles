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
        public static bool HasRcType = false;
        public static ViewType ViewType = ViewType.None;
        public static Color? TintMultiply = null;
        public static Color? TintAtmosphere = null;
        public static Color? TintCloudAtmosphere = null;
    }

    public static RcType GetRcType(this RoomSettings settings)
    {
        return Storage.RcType;
    }

    public static void SetRcType(this RoomSettings settings, RcType value)
    {
        Storage.RcType = value;
        Storage.HasRcType = value != RcType.None;
    }

    public static bool HasRcType(this RoomSettings settings)
    {
        return Storage.HasRcType;
    }

    public static ViewType GetViewType(this RoomSettings settings)
    {
        return Storage.ViewType;
    }

    public static void SetViewType(this RoomSettings settings, ViewType value)
    {
        Storage.ViewType = value;
    }

    public static Color? GetTintMultiply(this RoomSettings settings)
    {
        return Storage.TintMultiply;
    }

    public static void SetTintMultiply(this RoomSettings settings, Color? value)
    {
        Storage.TintMultiply = value;
    }

    public static Color? GetTintAtmosphere(this RoomSettings settings)
    {
        return Storage.TintAtmosphere;
    }

    public static void SetTintAtmosphere(this RoomSettings settings, Color? value)
    {
        Storage.TintAtmosphere = value;
    }

    public static Color? GetTintCloudAtmosphere(this RoomSettings settings)
    {
        return Storage.TintCloudAtmosphere;
    }

    public static void SetTintCloudAtmosphere(this RoomSettings settings, Color? value)
    {
        Storage.TintCloudAtmosphere = value;
    }

    public static void ClearExtendedData(this RoomSettings settings)
    {
        Storage.RcType = RcType.None;
        Storage.HasRcType = false;
        Storage.ViewType = ViewType.None;
        Storage.TintMultiply = null;
        Storage.TintAtmosphere = null;
        Storage.TintCloudAtmosphere = null;
    }
}
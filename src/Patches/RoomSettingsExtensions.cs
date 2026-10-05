using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using RainCycles.Snapshot;

namespace RainCycles.Patches;

// Orden relativo de <Mod> y <Tint> dentro de la línea RainCycles:.
// El segmento definido PRIMERO (edición en DevTools o en el archivo a mano)
// se emite primero al guardar; el segundo va detrás.
public enum RcSegmentOrder
{
    Unset = 0,
    ModFirst = 1,
    TintFirst = 2
}

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
        public BkgTag.Data Bkg = default;
        public RcSegmentOrder SegmentOrder = RcSegmentOrder.Unset;
        public bool Loaded = false;
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
        data.Loaded = true;
        data.RcType = value;
        if (!HasRcType(settings))
        {
            data.ViewType = ViewType.None;
            data.TintMultiply = null;
            data.TintAtmosphere = null;
            data.Bkg = default;
            data.SegmentOrder = RcSegmentOrder.Unset;
        }
    }

    public static bool HasRcType(this RoomSettings settings)
    {
        return GetOrCreate(settings).RcType != RcType.None;
    }

    public static bool IsRcStateLoaded(this RoomSettings settings)
    {
        return GetOrCreate(settings).Loaded;
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
            // Sin view no hay tinte NI bkg: <Mod> exige <View> para saber
            // si la imagen es acv/psv (regla del sistema, 10/2026).
            data.TintMultiply = null;
            data.TintAtmosphere = null;
            data.Bkg = default;
            data.SegmentOrder = RcSegmentOrder.Unset;
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
        var data = GetOrCreate(settings);
        if (value.HasValue)
            MarkSegmentOrder(data, preferIfBothAbsent: RcSegmentOrder.TintFirst);
        data.TintMultiply = value;
    }

    public static Color? GetTintAtmosphere(this RoomSettings settings)
    {
        return GetOrCreate(settings).TintAtmosphere;
    }

    public static void SetTintAtmosphere(this RoomSettings settings, Color? value)
    {
        if (!HasView(settings)) return;
        var data = GetOrCreate(settings);
        if (value.HasValue)
            MarkSegmentOrder(data, preferIfBothAbsent: RcSegmentOrder.TintFirst);
        data.TintAtmosphere = value;
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
        data.Bkg = default;
        data.SegmentOrder = RcSegmentOrder.Unset;
    }

    public static BkgTag.Data GetBkg(this RoomSettings settings)
    {
        return GetOrCreate(settings).Bkg;
    }

    public static void SetBkg(this RoomSettings settings, BkgTag.Data value)
    {
        // <Mod> exige <View>: sin view no se sabe si la imagen es acv/psv.
        if (!HasView(settings)) return;
        var data = GetOrCreate(settings);
        if (value.IsValid)
            MarkSegmentOrder(data, preferIfBothAbsent: RcSegmentOrder.ModFirst);
        data.Bkg = value.IsValid ? value : default;
    }

    // Marca quién llegó primero (solo la primera vez; luego el orden se
    // preserva aunque se borre y reponga un segmento — orden estable).
    private static void MarkSegmentOrder(ExtData data, RcSegmentOrder preferIfBothAbsent)
    {
        if (data.SegmentOrder != RcSegmentOrder.Unset) return;
        bool otherPresent = preferIfBothAbsent == RcSegmentOrder.ModFirst
            ? data.TintMultiply.HasValue || data.TintAtmosphere.HasValue
            : data.Bkg.IsValid;
        data.SegmentOrder = otherPresent
            ? (preferIfBothAbsent == RcSegmentOrder.ModFirst ? RcSegmentOrder.TintFirst : RcSegmentOrder.ModFirst)
            : preferIfBothAbsent;
    }

    public static RcSegmentOrder GetSegmentOrder(this RoomSettings settings)
    {
        return GetOrCreate(settings).SegmentOrder;
    }

    public static void SetSegmentOrder(this RoomSettings settings, RcSegmentOrder order)
    {
        GetOrCreate(settings).SegmentOrder = order;
    }

    public static bool HasBkg(this RoomSettings settings)
    {
        return GetOrCreate(settings).Bkg.IsValid;
    }

    public static void ClearBkg(this RoomSettings settings)
    {
        GetOrCreate(settings).Bkg = default;
    }

    public static void ClearExtendedData(this RoomSettings settings)
    {
        var data = GetOrCreate(settings);
        data.Loaded = true;
        data.RcType = RcType.None;
        data.ViewType = ViewType.None;
        data.TintMultiply = null;
        data.TintAtmosphere = null;
        data.Bkg = default;
        data.SegmentOrder = RcSegmentOrder.Unset;
    }
}

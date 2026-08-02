using System;
using System.Reflection;
using MonoMod.RuntimeDetour;
using RainCycles.Snapshot;

namespace RainCycles.Patches;

public static class DayNightBlocker
{
    private static bool _initialized = false;
    
    // El Hook se conserva en campo para mantenerlo vivo (MonoMod exige
    // referencia activa). El mod no se deshabilita en caliente — CS0414 esperado.
#pragma warning disable CS0414
    private static Hook _hookLoad;
#pragma warning restore CS0414

    public static void Init()
    {
        if (_initialized) return;

        var loadMethod = typeof(RoomSettings).GetMethod("Load",
            BindingFlags.Public | BindingFlags.Instance,
            null, new Type[] { typeof(SlugcatStats.Timeline) }, null);

        if (loadMethod != null)
        {
            _hookLoad = new Hook(loadMethod,
                new Func<Func<RoomSettings, SlugcatStats.Timeline, bool>, RoomSettings, SlugcatStats.Timeline, bool>(OnLoad));
        }
        else
        {
            RSPlugin.log.LogWarning("[DayNightBlocker] No se encontró RoomSettings.Load(SlugcatStats.Timeline) — el bloqueo no funcionará.");
            return;
        }

        _initialized = true;
    }

    private static bool OnLoad(
        Func<RoomSettings, SlugcatStats.Timeline, bool> orig,
        RoomSettings self,
        SlugcatStats.Timeline timelinePoint)
    {
        bool result = orig(self, timelinePoint);

        if (!IsManagedRoom(self))
            return result;

        bool removedEffect = false;
        bool removedObject = false;

        // Eliminar efecto DayNight
        for (int i = self.effects.Count - 1; i >= 0; i--)
        {
            if (self.effects[i].type == RoomSettings.RoomEffect.Type.DayNight)
            {
                self.effects.RemoveAt(i);
                removedEffect = true;
            }
        }

        // Eliminar objeto DayNightSettings
        for (int i = self.placedObjects.Count - 1; i >= 0; i--)
        {
            if (self.placedObjects[i].type?.ToString() == "DayNightSettings")
            {
                self.placedObjects.RemoveAt(i);
                removedObject = true;
            }
        }

        if (removedEffect || removedObject)
        {
            RSPlugin.log.LogInfo($"[DayNightBlocker] Eliminado DayNight de sala '{self.name}' (efecto: {removedEffect}, objeto: {removedObject})");
        }

        return result;
    }

    private static bool IsManagedRoom(RoomSettings settings)
    {
        if (settings == null) return false;
        RcType rcType = settings.GetRcType();
        return rcType == RcType.Blend || rcType == RcType.Static;
    }
}
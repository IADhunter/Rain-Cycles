using System;
using System.Reflection;
using MonoMod.RuntimeDetour;
using RainCycles.Snapshot;

namespace RainCycles.Patches;

public static class DayNightBlocker
{
    private static bool _initialized = false;
    private static Hook _hookLoad;

    public static void Init()
    {
        if (_initialized) return;

        // Método público: public bool Load(SlugcatStats.Timeline timelinePoint)
        var loadMethod = typeof(RoomSettings).GetMethod("Load",
            BindingFlags.Public | BindingFlags.Instance,
            null, new Type[] { typeof(SlugcatStats.Timeline) }, null);

        if (loadMethod != null)
        {
            _hookLoad = new Hook(loadMethod,
                new Func<Func<RoomSettings, SlugcatStats.Timeline, bool>, RoomSettings, SlugcatStats.Timeline, bool>(OnLoad));
            RSPlugin.log.LogDebug("[DayNightBlocker] Hook Load aplicado correctamente");
        }
        else
        {
            RSPlugin.log.LogWarning("[DayNightBlocker] No se encontró RoomSettings.Load(SlugcatStats.Timeline) — el bloqueo no funcionará.");
            return;
        }

        _initialized = true;
        RSPlugin.log.LogInfo("[DayNightBlocker] Inicializado");
    }

    public static void Terminate()
    {
        if (!_initialized) return;

        _hookLoad?.Dispose();
        _hookLoad = null;

        _initialized = false;
        RSPlugin.log.LogInfo("[DayNightBlocker] Terminado");
    }

    private static bool OnLoad(
        Func<RoomSettings, SlugcatStats.Timeline, bool> orig,
        RoomSettings self,
        SlugcatStats.Timeline timelinePoint)
    {
        // Log ANTES de cargar
        RSPlugin.log.LogDebug($"[DayNightBlocker] Load iniciado para sala '{self.name}' (path: {self.filePath})");

        // 1. Ejecutar la carga original (incluye modify settings y herencia)
        bool result = orig(self, timelinePoint);

        // Log DESPUÉS de cargar, ANTES de limpiar
        int effectsBefore = self.effects.Count;
        int placedBefore = self.placedObjects.Count;
        bool hasDayNightEffect = false;
        bool hasDayNightObject = false;

        foreach (var effect in self.effects)
        {
            if (effect.type == RoomSettings.RoomEffect.Type.DayNight)
            {
                hasDayNightEffect = true;
                break;
            }
        }

        foreach (var obj in self.placedObjects)
        {
            // PlacedObject.Type.DayNightSettings puede no existir en tiempo de compilación,
            // usamos comparación de string para ser seguros.
            if (obj.type?.ToString() == "DayNightSettings")
            {
                hasDayNightObject = true;
                break;
            }
        }

        RSPlugin.log.LogDebug($"[DayNightBlocker] Antes de limpiar: efectos={effectsBefore}, objetos={placedBefore}, DayNightEffect={hasDayNightEffect}, DayNightObject={hasDayNightObject}");

        // 2. Verificar si la sala es administrada por nosotros
        bool isManaged = IsManagedRoom(self);
        RcType rcType = self.GetRcType();
        RSPlugin.log.LogDebug($"[DayNightBlocker] ¿Es sala administrada? {isManaged} (RcType={rcType})");

        if (isManaged)
        {
            // Eliminar efecto DayNight
            int removedEffects = 0;
            for (int i = self.effects.Count - 1; i >= 0; i--)
            {
                if (self.effects[i].type == RoomSettings.RoomEffect.Type.DayNight)
                {
                    RSPlugin.log.LogDebug($"[DayNightBlocker] ✅ Eliminado efecto DayNight de sala '{self.name}'");
                    self.effects.RemoveAt(i);
                    removedEffects++;
                }
            }

            // Eliminar objeto DayNightSettings
            int removedObjects = 0;
            for (int i = self.placedObjects.Count - 1; i >= 0; i--)
            {
                if (self.placedObjects[i].type?.ToString() == "DayNightSettings")
                {
                    RSPlugin.log.LogDebug($"[DayNightBlocker] ✅ Eliminado objeto DayNightSettings de sala '{self.name}'");
                    self.placedObjects.RemoveAt(i);
                    removedObjects++;
                }
            }

            if (removedEffects == 0 && removedObjects == 0)
            {
                RSPlugin.log.LogDebug($"[DayNightBlocker] Sala '{self.name}' administrada pero NO tenía DayNight que eliminar.");
            }
        }
        else
        {
            RSPlugin.log.LogDebug($"[DayNightBlocker] Sala '{self.name}' NO es administrada — no se modifican efectos/objetos.");
        }

        // Log DESPUÉS de limpiar
        RSPlugin.log.LogDebug($"[DayNightBlocker] Después de limpiar: efectos={self.effects.Count}, objetos={self.placedObjects.Count}");

        return result;
    }

    private static bool IsManagedRoom(RoomSettings settings)
    {
        if (settings == null) return false;
        RcType rcType = settings.GetRcType();
        return rcType == RcType.Blend || rcType == RcType.Static;
    }
}
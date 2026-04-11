using UnityEngine;
using RainCycles.Settings;
using RainCycles.Clock;

namespace RainCycles.Core;

// Sky helpers: AssignSkySlots, SyncSkySlots, RefreshSlotSprite, ApplySkyForState, NextStateIn
public static partial class SettingsBlendController
{
    // ── Sky helpers ───────────────────────────────────────────────────────
    /// según la secuencia declarada en blend_settings. Llama LoadGraphic por cada
    /// imagen y registra el atlas en BlendSkyAtlasCache.
    /// Corre en el ctor — antes de elementsAddedToRoom, sin crear objetos nuevos.
    /// </summary>
    private static void AssignSkySlots(BackgroundScene scene, Room room,
        BlendSettings settings, SkyType sky)
    {
        // Resolver la secuencia de estados: A=activo, B=destino, C=siguiente
        int stateA = BlendClock.IsRunning ? BlendClock.StateA : 1;
        int stateB = BlendClock.IsRunning ? BlendClock.StateB : NextStateIn(settings, stateA);
        int stateC = NextStateIn(settings, stateB);

        SetSlotDay  (sky, stateA);
        SetSlotDusk (sky, stateB);
        SetSlotNight(sky, stateC);

        string region = BlendSettingsLoader.ActiveRegion;

        LoadAndAssignSlot(scene, sky, stateA, region, settings,
            slot => {
                if (scene is RoofTopView rtv) rtv.daySky.illustrationName = slot;
                else if (scene is AboveCloudsView acv) acv.daySky.illustrationName = slot;
            });

        LoadAndAssignSlot(scene, sky, stateB, region, settings,
            slot => {
                if (scene is RoofTopView rtv) rtv.duskSky.illustrationName = slot;
                else if (scene is AboveCloudsView acv) acv.duskSky.illustrationName = slot;
            });

        LoadAndAssignSlot(scene, sky, stateC, region, settings,
            slot => {
                if (scene is RoofTopView rtv) rtv.nightSky.illustrationName = slot;
                else if (scene is AboveCloudsView acv) acv.nightSky.illustrationName = slot;
            });
    }

    private static void LoadAndAssignSlot(BackgroundScene scene, SkyType sky,
        int state, string region, BlendSettings settings,
        System.Action<string> assign)
    {
        string file = settings.GetBkgFileForState(state, sky);
        if (string.IsNullOrEmpty(file)) return;
        string name = System.IO.Path.GetFileNameWithoutExtension(file);
        // El atlas ya fue precargado en OnRegionChanged via BlendSkyAtlasCache.PreloadRegion.
        BlendSkyAtlasCache.Register(region, name);
        assign(name);
    }

    // Aplica alphas a los tres slots según el T actual del blend. El crossfade es unidireccional como en vanilla: duskSky (B) siempre alpha=1 — está debajo, siempre visible daySky (A) baja de 1→0 revelando duskSky nightSky (C) siempre alpha=0 — esperando para el próximo ciclo Esto evita el flash blanco que ocurre cuando ambos alphas suman <1.
    private static void PreOrigForceSkyAlpha(
        BackgroundScene.Simple2DBackgroundIllustration day,
        BackgroundScene.Simple2DBackgroundIllustration dusk,
        BackgroundScene.Simple2DBackgroundIllustration night,
        SkyType sky = SkyType.None)
    {
        if (day == null) return;

        if (_active && _externalT)
        {
            // Blend activo normal
            float t = _forcedT;
            day.alpha   = 1f - t;
            dusk.alpha  = 1f;
            night.alpha = 0f;
            // Mantener savedAlphas actualizados para el frame de transición
            _savedDayAlpha   = day.alpha;
            _savedDuskAlpha  = 1f;
            _savedNightAlpha = 0f;
            _hasSavedAlphas  = true;
        }
        else if (BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending)
        {
            int slotDay = sky != SkyType.None ? GetSlotDay(sky) : -1;
            if (slotDay != BlendClock.StateA)
            {
                if (_hasSavedAlphas)
                {
                    day.alpha   = _savedDayAlpha;
                    dusk.alpha  = _savedDuskAlpha;
                    night.alpha = _savedNightAlpha;
                }
                else
                {
                    day.alpha   = 0f;
                    dusk.alpha  = 1f;
                    night.alpha = 0f;
                }
            }
            else
            {
                float t = BlendClock.SubPhaseLocalT;
                day.alpha   = 1f - t;
                dusk.alpha  = 1f;
                night.alpha = 0f;
                _savedDayAlpha   = day.alpha;
                _savedDuskAlpha  = 1f;
                _savedNightAlpha = 0f;
                _hasSavedAlphas  = true;
            }
        }
        else if (BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Done)
        {
            int slotDay = sky != SkyType.None ? GetSlotDay(sky) : -1;
            // En Phase.Done el blend terminó: los slots apuntan al estado B completado.
            bool slotsMatchB = slotDay == BlendClock.StateB;
            if (slotsMatchB)
            {
                // Slots correctos para Done: mostrar estado B estático (daySky visible).
                day.alpha   = 1f;
                dusk.alpha  = 1f;
                night.alpha = 0f;
                _savedDayAlpha   = 1f;
                _savedDuskAlpha  = 1f;
                _savedNightAlpha = 0f;
                _hasSavedAlphas  = true;
            }
            else if (_hasSavedAlphas)
            {
                // Slots aún no sincronizados a B — usar savedAlphas del último frame activo.
                day.alpha   = _savedDayAlpha;
                dusk.alpha  = _savedDuskAlpha;
                night.alpha = _savedNightAlpha;
            }
            else
            {
                day.alpha   = 0f;
                dusk.alpha  = 1f;
                night.alpha = 0f;
            }
        }
        else if (_hasSavedAlphas && (_exitedManagedRoomLastFrame || _detachedThisFrame))
        {
            day.alpha   = _savedDayAlpha;
            dusk.alpha  = _savedDuskAlpha;
            night.alpha = _savedNightAlpha;
        }
        else if (_hasSavedAlphas && BlendClock.IsRunning && sky != SkyType.None
                 && GetSlotDay(sky) > 0 && GetSlotDay(sky) != BlendClock.StateA)
        {
            day.alpha   = 0f;
            dusk.alpha  = 1f;
            night.alpha = 0f;
        }
    }

    private static void ApplySkyAlphas(
        BackgroundScene.Simple2DBackgroundIllustration day,
        BackgroundScene.Simple2DBackgroundIllustration dusk,
        BackgroundScene.Simple2DBackgroundIllustration night)
    {
        if (_active && _externalT)
        {
            // Blend activo normal.
            float t = _forcedT;
            day.alpha   = 1f - t;
            dusk.alpha  = 1f;
            night.alpha = 0f;
            // Guardar alphas en tiempo real para usarlos al hacer Detach.
            _savedDayAlpha   = day.alpha;
            _savedDuskAlpha  = dusk.alpha;
            _savedNightAlpha = night.alpha;
            _hasSavedAlphas  = true;
        }
        else if (_exitedManagedRoomLastFrame || _detachedThisFrame)
        {
            // Frames de SALIDA: la cámara acaba de salir de la sala.
            if (_hasSavedAlphas)
            {
                day.alpha   = _savedDayAlpha;
                dusk.alpha  = _savedDuskAlpha;
                night.alpha = _savedNightAlpha;
            }
            // Si no hay alphas guardados (idle), mantener los valores actuales tal cual.
        }
        else if (_moveCameraThisFrame && BlendClock.IsRunning &&
                 BlendClock.CurrentPhase == BlendClock.Phase.Blending)
        {
            // de este frame, antes de que SubPhase→1 pudiera avanzar el clock.
            float t = _entryFrameT >= 0f ? _entryFrameT : BlendClock.SubPhaseLocalT;
            day.alpha   = 1f - t;
            dusk.alpha  = 1f;
            night.alpha = 0f;
        }
        else
        {
            // Idle genuino: mostrar solo daySky (estado A).
            day.alpha   = 1f;
            dusk.alpha  = 1f;
            night.alpha = 0f;
            // Guardar alphas del estado idle también, por si el jugador sale desde aquí.
            _savedDayAlpha   = 1f;
            _savedDuskAlpha  = 1f;
            _savedNightAlpha = 0f;
            _hasSavedAlphas  = true;
        }
    }

    // Rota los slots al avanzar la sub-fase: B pasa a ser A, C pasa a ser B, el nuevo C se calcula de la secuencia. Cambia illustrationName en el slot correspondiente y recrea su FSprite via el patrón de SaintsJourneyIllustration.
    public static void RotateSkySlots()
    {
        // La sincronización real ocurre en AttachWithExternalT via SyncSkySlots.
    }

    // Cambia illustrationName del slot y recrea su FSprite si la cámara está disponible. Patrón de SaintsJourneyIllustration: RemoveAllSpritesFromContainer + InitiateSprites.


    /// <summary>
    /// Intenta recrear el FSprite del slot con el nuevo nombre.
    /// Devuelve true si encontró el spriteLeaser y recreó el sprite.
    private static bool RefreshSlotSprite(
        BackgroundScene.Simple2DBackgroundIllustration slot,
        string newName, RoomCamera cam, float initialAlpha = 0f)
    {
        if (slot.illustrationName == newName) return true;
        slot.illustrationName = newName;

        // Buscar el leaser en la cámara activa.
        var cameras = cam?.room?.game?.cameras
                   ?? slot.scene?.room?.game?.cameras;
        if (cameras == null) return false;

        foreach (var anyCamera in cameras)
        {
            if (anyCamera?.spriteLeasers == null) continue;
            foreach (var sLeaser in anyCamera.spriteLeasers)
            {
                if (sLeaser.drawableObject != slot) continue;
                if (sLeaser.sprites == null || sLeaser.sprites.Length == 0) continue;

                var oldSprite = sLeaser.sprites[0];
                var container = oldSprite?.container;
                if (container == null) break;

                int childIndex = -1;
                for (int i = 0; i < container.GetChildCount(); i++)
                    if (container.GetChildAt(i) == oldSprite) { childIndex = i; break; }

                var newSprite = new FSprite(newName, true);
                newSprite.x = oldSprite.x; newSprite.y = oldSprite.y;
                newSprite.shader = oldSprite.shader;
                newSprite.alpha = initialAlpha;
                oldSprite.RemoveFromContainer();
                sLeaser.sprites[0] = newSprite;
                if (childIndex >= 0 && childIndex <= container.GetChildCount())
                    container.AddChildAtIndex(newSprite, childIndex);
                else
                    container.AddChild(newSprite);
                return true;
            }
        }

        // No hay leaser — cámara en otra sala.
        return false;
    }


    // Sincroniza los tres slots de cielo (daySky/duskSky/nightSky) con el par stateA/stateB del blend actual. Corre en cada AttachWithExternalT. A diferencia de RotateSkySlots (que rota incrementalmente), este método calcula directamente desde los estados actuales — siempre correcto.
    private static void SyncSkySlots(Room room, int stateA, int stateB)
    {
        var settings = BlendSettingsLoader.Active;
        if (settings == null || room == null) return;
        string roomName = room.abstractRoom?.name;
        if (roomName == null) return;

        SkyType sky = settings.GetSkyType(roomName);
        if (sky == SkyType.None) return;

        // Verificar que tenemos la escena correcta para esta sala
        if (sky == SkyType.RTV && (_rtvScene == null || _rtvScene.room?.abstractRoom?.name != roomName)) return;
        if (sky == SkyType.ACV && (_acvScene == null || _acvScene.room?.abstractRoom?.name != roomName)) return;

        int stateC = NextStateIn(settings, stateB);

        // Sin cambio — no hacer nada
        if (GetSlotDay(sky) == stateA && GetSlotDusk(sky) == stateB && GetSlotNight(sky) == stateC) return;

        string fileA = settings.GetBkgFileForState(stateA, sky);
        string fileB = settings.GetBkgFileForState(stateB, sky);
        string fileC = settings.GetBkgFileForState(stateC, sky);

        var cam = room.game?.cameras?[0];

        RoofTopView    rtv = sky == SkyType.RTV ? _rtvScene : null;
        AboveCloudsView acv = sky == SkyType.ACV ? _acvScene : null;

        if (GetSlotDay(sky) != stateA)
        {
            bool refreshed = true;
            if (!string.IsNullOrEmpty(fileA))
            {
                if (rtv != null) refreshed &= RefreshSlotSprite(rtv.daySky, System.IO.Path.GetFileNameWithoutExtension(fileA), cam);
                if (acv != null) refreshed &= RefreshSlotSprite(acv.daySky, System.IO.Path.GetFileNameWithoutExtension(fileA), cam);
            }
            float dayAlpha = 1f - (_entryFrameT >= 0f ? _entryFrameT : BlendClock.SubPhaseLocalT);
            if (rtv != null) rtv.daySky.alpha = dayAlpha;
            if (acv != null) acv.daySky.alpha = dayAlpha;
            SetSlotDay(sky, stateA);
            if (!refreshed) { _pendingSkySync = true; _pendingSkyStateA = stateA; _pendingSkyStateB = stateB; }
        }
        if (GetSlotDusk(sky) != stateB)
        {
            bool refreshed = true;
            if (!string.IsNullOrEmpty(fileB))
            {
                if (rtv != null) refreshed &= RefreshSlotSprite(rtv.duskSky, System.IO.Path.GetFileNameWithoutExtension(fileB), cam);
                if (acv != null) refreshed &= RefreshSlotSprite(acv.duskSky, System.IO.Path.GetFileNameWithoutExtension(fileB), cam);
            }
            if (rtv != null) rtv.duskSky.alpha = 1f;
            if (acv != null) acv.duskSky.alpha = 1f;
            SetSlotDusk(sky, stateB);
            if (!refreshed) { _pendingSkySync = true; _pendingSkyStateA = stateA; _pendingSkyStateB = stateB; }
        }
        if (GetSlotNight(sky) != stateC)
        {
            bool refreshed = true;
            if (!string.IsNullOrEmpty(fileC))
            {
                if (rtv != null) refreshed &= RefreshSlotSprite(rtv.nightSky, System.IO.Path.GetFileNameWithoutExtension(fileC), cam);
                if (acv != null) refreshed &= RefreshSlotSprite(acv.nightSky, System.IO.Path.GetFileNameWithoutExtension(fileC), cam);
            }
            SetSlotNight(sky, stateC);
            if (!refreshed) { _pendingSkySync = true; _pendingSkyStateA = stateA; _pendingSkyStateB = stateB; }
        }
    }

    // Aplica instantáneamente la imagen de cielo correspondiente a un estado. Llamar desde RCPanel al hacer swap (cambio instantáneo de estado).
    public static void ApplySkyForState(int state, Room room)
    {
        if (room == null) return;
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;
        string roomName = room.abstractRoom?.name;
        if (roomName == null) return;

        SkyType sky = settings.GetSkyType(roomName);
        if (sky == SkyType.None) return;

        string fileDay  = settings.GetBkgFileForState(state, sky);
        if (string.IsNullOrEmpty(fileDay)) return;
        string nameDay  = System.IO.Path.GetFileNameWithoutExtension(fileDay);

        int stateB = NextStateIn(settings, state);
        int stateC = NextStateIn(settings, stateB);
        string fileDusk = settings.GetBkgFileForState(stateB, sky);
        string nameDusk = string.IsNullOrEmpty(fileDusk) ? null
            : System.IO.Path.GetFileNameWithoutExtension(fileDusk);

        var cam = room.game?.cameras?[0];

        if (sky == SkyType.RTV && _rtvScene != null)
        {
            bool r1 = RefreshSlotSprite(_rtvScene.daySky,  nameDay,  cam, 1f);
            bool r2 = nameDusk != null ? RefreshSlotSprite(_rtvScene.duskSky, nameDusk, cam, 1f) : true;
            _rtvScene.daySky.alpha   = 1f;
            _rtvScene.duskSky.alpha  = 1f;
            _rtvScene.nightSky.alpha = 0f;
            if (!r1 || !r2) { _pendingSkySync = true; _pendingSkyStateA = state; _pendingSkyStateB = stateB; }
        }
        else if (sky == SkyType.ACV && _acvScene != null)
        {
            bool r1 = RefreshSlotSprite(_acvScene.daySky,  nameDay,  cam, 1f);
            bool r2 = nameDusk != null ? RefreshSlotSprite(_acvScene.duskSky, nameDusk, cam, 1f) : true;
            _acvScene.daySky.alpha   = 1f;
            _acvScene.duskSky.alpha  = 1f;
            _acvScene.nightSky.alpha = 0f;
            if (!r1 || !r2) { _pendingSkySync = true; _pendingSkyStateA = state; _pendingSkyStateB = stateB; }
        }

        SetSlotDay  (sky, state);
        SetSlotDusk (sky, stateB);
        SetSlotNight(sky, stateC);
    }
    /// Si no hay secuencia declarada, devuelve el mismo estado.
    /// </summary>
    private static int NextStateIn(BlendSettings settings, int state)
    {
        if (settings == null || state < 1) return state;
        // Buscar en sequences el estado que sigue a 'state'
        foreach (var kv in settings.Sequences)
        {
            var seq = kv.Value;
            int idx = seq.IndexOf(state);
            if (idx >= 0)
                return seq[(idx + 1) % seq.Count];
        }
        // Fallback: state+1 cíclico dentro del rango de bkg declarados
        int max = 0;
        foreach (var alias in settings.StateBkgAlias.Keys)
            if (alias > max) max = alias;
        if (max < 1) return state;
        return (state % max) + 1;
    }

    // RoomCamera.DrawUpdate setea backgroundGraphic.color cada frame usando

}
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RainCycles.Settings;
using RainCycles.Clock;
using Watcher;

namespace RainCycles.Core;

public static partial class SettingsBlendController
{
    // ============================================================
    // OBTENER SLOTS POR TIPO DE SKY
    // ============================================================
    private static List<BackgroundScene.Simple2DBackgroundIllustration> GetSlotsForSky(SkyType sky)
    {
        if (_activeSlots == null) return null;
        return sky switch
        {
            SkyType.ACV => _activeSlots.blend,
            SkyType.RTV => _activeSlots.blend,
            SkyType.PSV => _activeSlots.blend,
            SkyType.ORV => _activeSlots.blend,
            _ => null
        };
    }

    private static List<BackgroundScene.Simple2DBackgroundIllustration> GetFogSlotsForSky()
    {
        return _activeSlots?.fog;
    }

    private static List<BackgroundScene.Simple2DBackgroundIllustration> GetSunSlotsForSky()
    {
        return _activeSlots?.sun;
    }

    // ============================================================
    // FORCE SUN SHADER
    // ============================================================
    private static void ForceSunShader(List<BackgroundScene.Simple2DBackgroundIllustration> sunSlots, RoomCamera cam)
    {
        if (sunSlots == null || cam?.spriteLeasers == null) return;
        var additiveShader = cam.game.rainWorld.Shaders["BackgroundAdditive"];
        foreach (var slot in sunSlots)
        {
            foreach (var sl in cam.spriteLeasers)
            {
                if (sl.drawableObject == slot && sl.sprites != null && sl.sprites.Length > 0)
                    sl.sprites[0].shader = additiveShader;
            }
        }
    }

    // ============================================================
    // CREAR SLOTS - 4 slots en orden INVERSO
    // ============================================================
    private static List<BackgroundScene.Simple2DBackgroundIllustration> CreateRcSlotsVanilla(
        BackgroundScene scene, Room room, SkyType sky)
    {
        var slots = new List<BackgroundScene.Simple2DBackgroundIllustration>();
        for (int i = 3; i >= 0; i--)
        {
            var slot = new BackgroundScene.Simple2DBackgroundIllustration(
                scene, "RC_Transparent", new Vector2(683f, 384f));
            slot.alpha = 0f;
            scene.AddElement(slot);
            slots.Insert(0, slot);
        }
        return slots;
    }

    // ============================================================
    // DESTRUIR SLOTS - limpia escena, sprite y referencia
    // ============================================================
    private static void DestroyRcSlots(
        List<BackgroundScene.Simple2DBackgroundIllustration> slots, BackgroundScene scene)
    {
        if (slots == null) return;
        var cam = scene?.room?.game?.cameras?[0];
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;
            if (cam != null && cam.spriteLeasers != null)
            {
                for (int j = 0; j < cam.spriteLeasers.Count; j++)
                {
                    var sl = cam.spriteLeasers[j];
                    if (sl.drawableObject == slot && sl.sprites != null && sl.sprites.Length > 0)
                    {
                        sl.sprites[0]?.RemoveFromContainer();
                        break;
                    }
                }
            }
            scene?.elements?.Remove(slot);
            slot.Destroy();
        }
    }

    private static List<BackgroundScene.Simple2DBackgroundIllustration> CreateStaticSlotsVanilla(
        BackgroundScene scene, Room room, SkyType sky)
    {
        var slots = new List<BackgroundScene.Simple2DBackgroundIllustration>();
        var slot = new BackgroundScene.Simple2DBackgroundIllustration(
            scene, "RC_Transparent", new Vector2(683f, 384f));
        slot.alpha = 0f;
        scene.AddElement(slot);
        slots.Add(slot);
        return slots;
    }

    private static List<BackgroundScene.Simple2DBackgroundIllustration> CreateSunSlots(
        BackgroundScene scene, Room room, SkyType sky, bool isStatic)
    {
        int count = isStatic ? 1 : 4;
        var slots = new List<BackgroundScene.Simple2DBackgroundIllustration>();
        for (int i = count - 1; i >= 0; i--)
        {
            var slot = new BackgroundScene.Simple2DBackgroundIllustration(
                scene, "RC_Transparent", new Vector2(683f, 384f));
            slot.depth = 22.5f;
            slot.alpha = 0f;
            scene.AddElement(slot);
            slots.Insert(0, slot);
        }
        return slots;
    }

    // ============================================================
    // ACTUALIZAR SLOTS RC - Asigna imágenes a los 4 slots
    // ============================================================
    private static void UpdateRcSlots(SkyType sky, int stateA, int stateB, RoomCamera forcedCam = null, Room targetRoom = null)
    {
        var slots = GetSlotsForSky(sky);
        if (slots == null || slots.Count < 4) return;

        string regionCode = null;
        if (targetRoom != null)
            regionCode = targetRoom.world?.region?.name?.ToUpperInvariant();
        else if (_room != null)
            regionCode = _room.world?.region?.name?.ToUpperInvariant();
        
        BlendSettings effectiveSettings = BlendSettingsLoader.Active;
        if (effectiveSettings == null && !string.IsNullOrEmpty(regionCode))
            effectiveSettings = BlendSettingsLoader.GetForRegion(regionCode);
        
        if (effectiveSettings == null)
        {
            return;
        }

        var cam = forcedCam ?? _room?.game?.cameras?[0];
        if (cam == null && targetRoom != null)
            cam = targetRoom.game?.cameras?[0];

        if (cam == null || (_room != null && cam.room != _room && (targetRoom == null || cam.room != targetRoom)))
        {
            return;
        }

        ViewType view = sky == SkyType.ACV ? ViewType.ACV :
                        sky == SkyType.RTV ? ViewType.RTV :
                        sky == SkyType.PSV ? ViewType.PSV : ViewType.ORV;

        for (int state = 1; state <= 4; state++)
        {
            string file = effectiveSettings.GetBkgFileForState(state, view);
            int slotIndex = state - 1;
            
            if (!string.IsNullOrEmpty(file) && slots[slotIndex].illustrationName != Path.GetFileNameWithoutExtension(file))
            {
                string imgName = Path.GetFileNameWithoutExtension(file);
                RefreshSlotSprite(slots[slotIndex], imgName, cam);
            }
        }

        bool isBlending = BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending;
        float t = isBlending ? BlendClock.SubPhaseLocalT : 0f;
        
        ApplyRcSlotsAlpha(sky, t, isBlending, stateA, stateB);

        if (sky == SkyType.PSV)
        {
            UpdatePsvSlots(stateA, stateB, cam);
            ApplyPsvAlphas(t, isBlending, stateA, stateB);
        }
    }

    // ============================================================
    // APLICAR ALPHAS A SLOTS RC
    // ============================================================
    private static void ApplyRcSlotsAlpha(SkyType sky, float t, bool isBlending, int? overrideStateA = null, int? overrideStateB = null)
    {
        var slots = GetSlotsForSky(sky);
        if (slots == null || slots.Count < 4) return;

        int stateA, stateB;
        if (overrideStateA.HasValue && overrideStateB.HasValue)
        {
            stateA = overrideStateA.Value;
            stateB = overrideStateB.Value;
        }
        else if (BlendClock.IsRunning)
        {
            stateA = BlendClock.StateA;
            stateB = BlendClock.StateB;
        }
        else if (_externalT)
        {
            stateA = _manualStateA;
            stateB = _manualStateB;
        }
        else
        {
            stateA = BlendClock.StateA > 0 ? BlendClock.StateA : 1;
            stateB = stateA;
        }

        int slotA = stateA - 1;
        int slotB = stateB - 1;
        bool isClosingTransition = (stateA == 4 && stateB == 1);
        bool actuallyBlending = isBlending || (_externalT && stateA != stateB) || (overrideStateA.HasValue && overrideStateA.Value != overrideStateB.Value);

        if (actuallyBlending)
        {
            for (int i = 0; i < 4; i++)
                slots[i].alpha = 0f;

            if (sky == SkyType.PSV)
            {
                if (slotA >= 0 && slotA < 4)
                    slots[slotA].alpha = 1f - t;
                if (slotB >= 0 && slotB < 4)
                    slots[slotB].alpha = t;
            }
            else
            {
                if (isClosingTransition)
                {
                    if (slotA >= 0 && slotA < 4)
                        slots[slotA].alpha = 1f;
                    if (slotB >= 0 && slotB < 4)
                        slots[slotB].alpha = t;
                }
                else
                {
                    if (slotA >= 0 && slotA < 4)
                        slots[slotA].alpha = 1f - t;
                    if (slotB >= 0 && slotB < 4)
                        slots[slotB].alpha = 1f;
                }
            }
        }
        else
        {
            int currentState = stateA;
            int activeSlot = currentState - 1;
            
            for (int i = 0; i < 4; i++)
                slots[i].alpha = (i == activeSlot) ? 1f : 0f;
        }
    }

    // ============================================================
    // ACTUALIZAR SLOTS PSV (fog y sun)
    // ============================================================
    private static void UpdatePsvSlots(int stateA, int stateB, RoomCamera cam)
    {
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;

        var fogSlots = GetFogSlotsForSky();
        if (fogSlots != null && fogSlots.Count >= 4)
        {
            for (int state = 1; state <= 4; state++)
            {
                string fog = settings.GetBkgFogForState(state);
                int idx = state - 1;
                if (!string.IsNullOrEmpty(fog) && fogSlots[idx].illustrationName != Path.GetFileNameWithoutExtension(fog))
                {
                    RefreshSlotSprite(fogSlots[idx], Path.GetFileNameWithoutExtension(fog), cam);
                    fogSlots[idx].depth = 195f;
                }
            }
        }

        var sunSlots = GetSunSlotsForSky();
        if (sunSlots != null && sunSlots.Count >= 4)
        {
            for (int state = 1; state <= 4; state++)
            {
                string sun = settings.GetBkgSunForState(state);
                int idx = state - 1;
                if (!string.IsNullOrEmpty(sun) && sunSlots[idx].illustrationName != Path.GetFileNameWithoutExtension(sun))
                    RefreshSlotSprite(sunSlots[idx], Path.GetFileNameWithoutExtension(sun), cam);
            }
            ForceSunShader(sunSlots, cam);
        }
    }

    // ============================================================
    // APLICAR ALPHAS PSV
    // ============================================================
    public static void ApplyPsvAlphas(float t, bool isBlending, int? overrideStateA = null, int? overrideStateB = null)
    {
        var fogSlots = GetFogSlotsForSky();
        var sunSlots = GetSunSlotsForSky();
        var allSlots = new[] { fogSlots, sunSlots };
        
        foreach (var slots in allSlots)
        {
            if (slots == null || slots.Count < 4) continue;

            int stateA, stateB;
            if (overrideStateA.HasValue && overrideStateB.HasValue)
            {
                stateA = overrideStateA.Value;
                stateB = overrideStateB.Value;
            }
            else if (BlendClock.IsRunning)
            {
                stateA = BlendClock.StateA;
                stateB = BlendClock.StateB;
            }
            else if (_externalT)
            {
                stateA = _manualStateA;
                stateB = _manualStateB;
            }
            else
            {
                stateA = BlendClock.StateA > 0 ? BlendClock.StateA : 1;
                stateB = stateA;
            }

            int slotA = stateA - 1;
            int slotB = stateB - 1;
            bool actuallyBlending = isBlending || (_externalT && stateA != stateB) || (overrideStateA.HasValue && overrideStateA.Value != overrideStateB.Value);

            if (actuallyBlending)
            {
                for (int i = 0; i < 4; i++)
                    slots[i].alpha = 0f;

                if (slotA >= 0 && slotA < 4)
                    slots[slotA].alpha = 1f - t;
                if (slotB >= 0 && slotB < 4)
                    slots[slotB].alpha = t;
            }
            else
            {
                int currentState = stateA;
                int activeSlot = currentState - 1;
                
                for (int i = 0; i < 4; i++)
                    slots[i].alpha = (i == activeSlot) ? 1f : 0f;
            }
        }
    }

    // ============================================================
    // RESOLVE ACTIVE SLOTS FOR ROOM — finds the scene in the room and sets _activeSlots
    // ============================================================
    private static void ResolveActiveSlotsForRoom(Room room)
    {
        if (room?.updateList == null) return;

        for (int i = 0; i < room.updateList.Count; i++)
        {
            var scene = room.updateList[i] as BackgroundScene;
            if (scene != null && _sceneSlots.TryGetValue(scene, out var slotSet))
            {
                _activeSlots = slotSet;

                if (scene is AboveCloudsView acv)
                {
                    _acvScene = acv;
                    var snap = SettingsSnapshot.GetCached(room.roomSettings?.filePath, room.abstractRoom?.name);
                    if (snap?.HasView == true && snap.ViewType == ViewType.PSV)
                        _psvScene = acv;
                }
                else if (scene is RoofTopView rtv)
                {
                    _rtvScene = rtv;
                }
                else if (scene is OuterRimView orv)
                {
                    _orvScene = orv;
                }
                return;
            }
        }
    }

    // ============================================================
    // SYNC SKY SLOTS - Punto de entrada unificado para todos los casos
    // ============================================================
    public static void SyncSkySlots(Room room, int stateA, int stateB)
    {
        if (room == null) return;
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;

        string roomName = room.abstractRoom?.name;
        if (roomName == null) return;

        var skyType = GetViewFromLoadedSettings(room);
        if (skyType == SkyType.None) return;

        ResolveActiveSlotsForRoom(room);
        UpdateRcSlots(skyType, stateA, stateB, null, room);
    }

    // ============================================================
    // APPLY SKY FOR STATE
    // ============================================================
    public static void ApplySkyForState(int state, Room room)
    {
        if (room == null) return;
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;
        string roomName = room.abstractRoom?.name;
        if (roomName == null) return;

        ResolveActiveSlotsForRoom(room);
        var skyType = GetViewFromLoadedSettings(room);
        if (skyType == SkyType.None) return;

        UpdateRcSlots(skyType, state, state, null, room);
    }

    // ============================================================
    // REFRESH SLOT SPRITE
    // ============================================================
    private static bool RefreshSlotSprite(
        BackgroundScene.Simple2DBackgroundIllustration slot,
        string newName, RoomCamera cam)
    {
        if (slot.illustrationName == newName) return true;

        float currentAlpha = slot.alpha;
        string oldName = slot.illustrationName;
        
        string finalName = newName;

        if (!Futile.atlasManager.DoesContainElementWithName(newName))
        {
            string modName = BlendSettingsLoader.ActiveModName;
            if (!string.IsNullOrEmpty(modName))
            {
                string path = ResolveIllustrationPath(modName, newName);
                if (path != null && File.Exists(path))
                {
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    AssetManager.SafeWWWLoadTexture(ref tex, "file:///" + path, true, true);
                    HeavyTexturesCache.LoadAndCacheAtlasFromTexture(newName, tex, false);
                }
                else
                {
                    RSPlugin.log.LogWarning($"[RC][SkyHelpers] RefreshSlotSprite: IMAGE NOT FOUND \"{newName}\" mod={modName} path={path ?? "null"}");
                }
            }
            else
            {
                RSPlugin.log.LogWarning($"[RC][SkyHelpers] RefreshSlotSprite: NO MOD NAME for \"{newName}\"");
            }
        }
        
        if (!Futile.atlasManager.DoesContainElementWithName(finalName))
        {
            RSPlugin.log.LogWarning($"[RC][SkyHelpers] RefreshSlotSprite: FALLBACK to RC_Transparent for \"{newName}\" (atlas not loaded)");
            finalName = "RC_Transparent";
        }
        
        slot.illustrationName = finalName;

        var cameras = cam?.room?.game?.cameras
                   ?? slot.scene?.room?.game?.cameras;
        if (cameras == null) return false;

        bool success = false;
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

                var newSprite = new FSprite(finalName, true);
                newSprite.x = oldSprite.x;
                newSprite.y = oldSprite.y;
                bool isSunSlot = (GetSunSlotsForSky()?.Contains(slot) == true);
                newSprite.shader = isSunSlot
                    ? cam.game.rainWorld.Shaders["BackgroundAdditive"]
                    : oldSprite.shader;
                newSprite.alpha = currentAlpha;

                newSprite.UpdateLocalVertices();

                oldSprite.RemoveFromContainer();
                sLeaser.sprites[0] = newSprite;

                if (childIndex >= 0 && childIndex <= container.GetChildCount())
                    container.AddChildAtIndex(newSprite, childIndex);
                else
                    container.AddChild(newSprite);

                success = true;
                break;
            }
            if (success) break;
        }

        return success;
    }

    // ================================================================
    // RESOLVER RUTA DE IMAGEN
    // ================================================================
    private const string DEFAULT_MOD_NAME = "Default";

    private static string ResolveIllustrationPath(string modName, string imageName)
    {
        if (string.IsNullOrEmpty(modName) || string.IsNullOrEmpty(imageName))
            return null;

        if (string.Equals(modName, DEFAULT_MOD_NAME, System.StringComparison.OrdinalIgnoreCase))
        {
            string basePath = Path.Combine(Application.streamingAssetsPath, "Illustrations", imageName + ".png");
            return File.Exists(basePath) ? basePath : null;
        }

        foreach (var mod in ModManager.ActiveMods)
        {
            string modFolderName = Path.GetFileName(mod.path);
            
            if (string.Equals(modFolderName, modName, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mod.name, modName, System.StringComparison.OrdinalIgnoreCase))
            {
                string candidate = Path.Combine(mod.path, "Illustrations", imageName + ".png");
                if (File.Exists(candidate))
                    return candidate;
                
                string illustrationsDir = Path.Combine(mod.path, "Illustrations");
                if (Directory.Exists(illustrationsDir))
                {
                    foreach (string file in Directory.GetFiles(illustrationsDir, imageName + ".png", SearchOption.AllDirectories))
                        return file;
                }
            }
        }

        string fallback = AssetManager.ResolveFilePath("Illustrations" + Path.DirectorySeparatorChar + imageName + ".png");
        if (!string.IsNullOrEmpty(fallback) && File.Exists(fallback))
            return fallback;

        return null;
    }

    // ============================================================
    // SYNC FOG POSITION - Copia la posición X e Y del fog vanilla al slot RC
    // ============================================================
    private static void SyncFogSlotPosition(RoomCamera cam)
    {
        if (_psvScene == null)
            return;

        var fogSlots = GetFogSlotsForSky();
        if (fogSlots == null || fogSlots.Count == 0)
            return;

        const float Y_OFFSET = 68f;
        const float X_OFFSET = 0f;

        if (_cachedVanillaFog == null)
        {
            foreach (var elem in _psvScene.elements)
            {
                if (elem is AboveCloudsView.HorizonFog fog)
                {
                    _cachedVanillaFog = fog;
                    break;
                }
            }
            if (_cachedVanillaFog == null)
                return;
        }

        FSprite vanillaSprite = null;
        for (int i = 0; i < cam.spriteLeasers.Count; i++)
        {
            if (cam.spriteLeasers[i].drawableObject == _cachedVanillaFog)
            {
                if (cam.spriteLeasers[i].sprites != null && cam.spriteLeasers[i].sprites.Length > 0)
                    vanillaSprite = cam.spriteLeasers[i].sprites[0];
                break;
            }
        }
        if (vanillaSprite == null) return;

        for (int j = 0; j < fogSlots.Count; j++)
        {
            var slot = fogSlots[j];
            if (slot.alpha <= 0.01f)
                continue;

            for (int k = 0; k < cam.spriteLeasers.Count; k++)
            {
                if (cam.spriteLeasers[k].drawableObject == slot)
                {
                    var sprites = cam.spriteLeasers[k].sprites;
                    if (sprites != null && sprites.Length > 0)
                    {
                        sprites[0].x = vanillaSprite.x + X_OFFSET;
                        sprites[0].y = vanillaSprite.y + Y_OFFSET;
                    }
                    break;
                }
            }
        }
    }

    // ============================================================
    // CLEAR ALL SLOTS
    // ============================================================
    public static void ClearAllSlots()
    {
        _sceneSlots.Clear();
        _activeSlots = null;
        _acvScene = null;
        _rtvScene = null;
        _psvScene = null;
        _orvScene = null;

        _rcSlotsStaticACV = null;
        _rcSlotsStaticRTV = null;
        _rcSlotsStaticPSV = null;
        _rcSlotsStaticORV = null;

        _forceSkyRefresh = false;
        ClearCachedVanillaFog();
    }

    // ============================================================
    // HOOK: Sincronizar fog RC durante DrawSprites del vanilla
    // ============================================================
    private static void OnHorizonFogDrawSprites(
        On.AboveCloudsView.HorizonFog.orig_DrawSprites orig,
        AboveCloudsView.HorizonFog self,
        RoomCamera.SpriteLeaser sLeaser,
        RoomCamera rCam,
        float timeStacker,
        Vector2 camPos)
    {
        orig(self, sLeaser, rCam, timeStacker, camPos);

        if (_psvScene != null)
        {
            var fogSlots = GetFogSlotsForSky();
            if (fogSlots != null && fogSlots.Count > 0)
            {
                const float Y_OFFSET = 68f;
                const float X_OFFSET = 0f;

                if (sLeaser.sprites != null && sLeaser.sprites.Length > 0)
                {
                    float vanillaX = sLeaser.sprites[0].x;
                    float vanillaY = sLeaser.sprites[0].y;
                    _cachedVanillaFog = self;

                    for (int j = 0; j < fogSlots.Count; j++)
                    {
                        var slot = fogSlots[j];
                        if (slot.alpha <= 0.01f)
                            continue;

                        for (int k = 0; k < rCam.spriteLeasers.Count; k++)
                        {
                            if (rCam.spriteLeasers[k].drawableObject == slot)
                            {
                                var sprites = rCam.spriteLeasers[k].sprites;
                                if (sprites != null && sprites.Length > 0)
                                {
                                    sprites[0].x = vanillaX + X_OFFSET;
                                    sprites[0].y = vanillaY + Y_OFFSET;
                                }
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
}

using System;
using System.IO;
using MoreSlugcats;
using RWCustom;
using UnityEngine;

namespace RainCycles.Patches
{
    public static class SnowLightController
    {
        private static bool _initialized = false;
        private static AssetBundle _bundle;
        
        // Shaders originales cacheados
        private static FShader _originalDisplaySnowShader;
        private static FShader _originalSnowFallShader;
        private static FShader _originalFastSnowFallShader;
        private static FShader _originalBlizzardShader;
        private static FShader _originalFastBlizzardShader;

        // Shaders tintados
        private static FShader _snowTintShader;
        private static FShader _snowFallTintShader;
        private static FShader _fastSnowFallTintShader;
        private static FShader _blizzardTintShader;
        private static FShader _fastBlizzardTintShader;
        
        // Efectos registrados en DevTools
        public static readonly RoomSettings.RoomEffect.Type SnowLightEffect = 
            new RoomSettings.RoomEffect.Type("SnowLight", true);
        
        public static readonly RoomSettings.RoomEffect.Type SnowSparkleEffect = 
            new RoomSettings.RoomEffect.Type("SnowSparkle", true);
        
        public static void Init()
        {
            if (_initialized) return;
            
            try
            {
                LoadAssetBundle();
                RegisterShaders();
                CacheOriginalShaders();
                RegisterEffectCategories();
                HookSnowInitiateSprites();
                HookBlizzardGraphicsInitiateSprites();
                HookRoomCameraDrawUpdate();
                
                _initialized = true;
            }
            catch (Exception ex)
            {
                RSPlugin.log.LogError($"[SnowLightController] Error en Init: {ex}");
            }
        }
        
        private static void LoadAssetBundle()
        {
            string assemblyDir = Path.GetDirectoryName(typeof(RSPlugin).Assembly.Location);
            string bundlePath = Path.GetFullPath(Path.Combine(assemblyDir, "..", "assetbundles", "snowtint"));

            if (!File.Exists(bundlePath))
            {
                throw new FileNotFoundException($"Asset bundle no encontrado: {bundlePath}");
            }
            
            _bundle = AssetBundle.LoadFromFile(bundlePath);
            
            if (_bundle == null)
            {
                throw new Exception($"AssetBundle.LoadFromFile devolvió null para: {bundlePath}");
            }
        }
        
        private static void RegisterShaders()
        {
            Shader snowShader = _bundle.LoadAsset<Shader>("Assets/Shaders/SnowTintShader.shader");
            if (snowShader == null) throw new Exception("No se encontró SnowTintShader.shader");
            _snowTintShader = FShader.CreateShader("SnowTintShader", snowShader);
            Custom.rainWorld.Shaders["SnowTintShader"] = _snowTintShader;
            
            Shader snowFallShader = _bundle.LoadAsset<Shader>("Assets/Shaders/SnowFallTintShader.shader");
            if (snowFallShader == null) throw new Exception("No se encontró SnowFallTintShader.shader");
            _snowFallTintShader = FShader.CreateShader("SnowFallTintShader", snowFallShader);
            Custom.rainWorld.Shaders["SnowFallTintShader"] = _snowFallTintShader;
            
            Shader fastSnowFallShader = _bundle.LoadAsset<Shader>("Assets/Shaders/FastSnowFallTintShader.shader");
            if (fastSnowFallShader == null) throw new Exception("No se encontró FastSnowFallTintShader.shader");
            _fastSnowFallTintShader = FShader.CreateShader("FastSnowFallTintShader", fastSnowFallShader);
            Custom.rainWorld.Shaders["FastSnowFallTintShader"] = _fastSnowFallTintShader;
            
            Shader blizzardShader = _bundle.LoadAsset<Shader>("Assets/Shaders/BlizzardTintShader.shader");
            if (blizzardShader == null) throw new Exception("No se encontró BlizzardTintShader.shader");
            _blizzardTintShader = FShader.CreateShader("BlizzardTintShader", blizzardShader);
            Custom.rainWorld.Shaders["BlizzardTintShader"] = _blizzardTintShader;
            
            Shader fastBlizzardShader = _bundle.LoadAsset<Shader>("Assets/Shaders/FastBlizzardTintShader.shader");
            if (fastBlizzardShader == null) throw new Exception("No se encontró FastBlizzardTintShader.shader");
            _fastBlizzardTintShader = FShader.CreateShader("FastBlizzardTintShader", fastBlizzardShader);
            Custom.rainWorld.Shaders["FastBlizzardTintShader"] = _fastBlizzardTintShader;
        }
        
        private static void CacheOriginalShaders()
        {
            Custom.rainWorld.Shaders.TryGetValue("DisplaySnowShader", out _originalDisplaySnowShader);
            Custom.rainWorld.Shaders.TryGetValue("SnowFall", out _originalSnowFallShader);
            Custom.rainWorld.Shaders.TryGetValue("FastSnowFall", out _originalFastSnowFallShader);
            Custom.rainWorld.Shaders.TryGetValue("Blizzard", out _originalBlizzardShader);
            Custom.rainWorld.Shaders.TryGetValue("FastBlizzard", out _originalFastBlizzardShader);
        }
        private static void RegisterEffectCategories()
        {
            On.DevInterface.RoomSettingsPage.DevEffectGetCategoryFromEffectType += OnDevEffectGetCategory;
            On.RoomSettings.RoomEffect.GetSliderDefault += OnGetSliderDefault;
        }
        
        private static float OnGetSliderDefault(
            On.RoomSettings.RoomEffect.orig_GetSliderDefault orig,
            RoomSettings.RoomEffect.Type type,
            int index)
        {
            if (index == 0 && type == SnowLightEffect) return 0.5f;
            return orig(type, index);
        }
        
        private static DevInterface.RoomSettingsPage.DevEffectsCategories OnDevEffectGetCategory(
            On.DevInterface.RoomSettingsPage.orig_DevEffectGetCategoryFromEffectType orig,
            DevInterface.RoomSettingsPage self,
            RoomSettings.RoomEffect.Type type)
        {
            if (type == SnowLightEffect)
                return DevInterface.RoomSettingsPage.DevEffectsCategories.Lighting;
            
            if (type == SnowSparkleEffect)
                return DevInterface.RoomSettingsPage.DevEffectsCategories.Decorations;
            
            return orig(self, type);
        }
        
        // ============================================
        // HOOK: Snow (suelo)
        // ============================================
        private static void HookSnowInitiateSprites()
        {
            On.MoreSlugcats.Snow.InitiateSprites += SnowOnInitiateSprites;
        }
        
        private static void SnowOnInitiateSprites(
            On.MoreSlugcats.Snow.orig_InitiateSprites orig,
            MoreSlugcats.Snow self,
            RoomCamera.SpriteLeaser sLeaser,
            RoomCamera rCam)
        {
            orig(self, sLeaser, rCam);
        }
        
        // ============================================
        // HOOK: BlizzardGraphics (nieve cayendo + ventisca)
        // ============================================
        private static void HookBlizzardGraphicsInitiateSprites()
        {
            On.MoreSlugcats.BlizzardGraphics.InitiateSprites += BlizzardGraphicsOnInitiateSprites;
        }
        
        private static void BlizzardGraphicsOnInitiateSprites(
            On.MoreSlugcats.BlizzardGraphics.orig_InitiateSprites orig,
            MoreSlugcats.BlizzardGraphics self,
            RoomCamera.SpriteLeaser sLeaser,
            RoomCamera rCam)
        {
            orig(self, sLeaser, rCam);
        }
        
        // ============================================
        // HOOK: RoomCamera.DrawUpdate (cambio en tiempo real)
        // ============================================
        private static void HookRoomCameraDrawUpdate()
        {
            On.RoomCamera.DrawUpdate += RoomCameraOnDrawUpdate;
        }
        
        private static void RoomCameraOnDrawUpdate(
            On.RoomCamera.orig_DrawUpdate orig,
            RoomCamera self,
            float timeStacker,
            float timeSpeed)
        {
            orig(self, timeStacker, timeSpeed);
            
            if (self.room == null) return;

            RoomSettings.RoomEffect snowLightEffect = self.room.roomSettings.GetEffect(SnowLightEffect);
            bool hasSnowLight = snowLightEffect != null;
            float snowLightAmount = hasSnowLight ? Mathf.Clamp01(snowLightEffect.amount) : 0.5f;
            
            if (hasSnowLight)
            {
                Shader.SetGlobalFloat("_SnowTintAmount", snowLightAmount);
            }
            
            RoomSettings.RoomEffect snowSparkleEffect = self.room.roomSettings.GetEffect(SnowSparkleEffect);
            bool hasSnowSparkle = snowSparkleEffect != null;
            float snowSparkleAmount = hasSnowSparkle ? Mathf.Clamp01(snowSparkleEffect.amount) : 0f;
            
            if (hasSnowSparkle)
            {
                Shader.SetGlobalFloat("_SnowGrainAmount", snowSparkleAmount);
            }
            else
            {
                Shader.SetGlobalFloat("_SnowGrainAmount", 0f);
            }
            
            if (self.room.snow)
            {
                ProcessSnowSpriteLeaser(self, hasSnowLight);
            }
            
            ProcessBlizzardGraphics(self, hasSnowLight);
        }
        
        private static void ProcessSnowSpriteLeaser(RoomCamera rCam, bool hasEffect)
        {
            for (int i = 0; i < rCam.spriteLeasers.Count; i++)
            {
                var sLeaser = rCam.spriteLeasers[i];
                if (sLeaser.drawableObject is MoreSlugcats.Snow snow && snow.room == rCam.room)
                {
                    if (sLeaser.sprites.Length == 0) continue;
                    
                    FSprite snowSprite = sLeaser.sprites[0];
                    
                    if (hasEffect)
                    {
                        if (snowSprite.shader != _snowTintShader)
                        {
                            snowSprite.shader = _snowTintShader;
                        }
                    }
                    else
                    {
                        if (snowSprite.shader == _snowTintShader)
                        {
                            snowSprite.shader = _originalDisplaySnowShader ?? rCam.room.game.rainWorld.Shaders["DisplaySnowShader"];
                        }
                    }
                    break;
                }
            }
        }
        
        private static void ProcessBlizzardGraphics(RoomCamera rCam, bool hasEffect)
        {
            for (int i = 0; i < rCam.spriteLeasers.Count; i++)
            {
                var sLeaser = rCam.spriteLeasers[i];
                if (sLeaser.drawableObject is MoreSlugcats.BlizzardGraphics blizzard && blizzard.room == rCam.room)
                {
                    if (sLeaser.sprites.Length < 2) continue;
                    
                    FSprite snowFallSprite = sLeaser.sprites[0];
                    FSprite blizzardSprite = sLeaser.sprites[1];
                    
                    bool isLowQuality = ModManager.MMF && rCam.room.game.rainWorld.options.quality == Options.Quality.LOW;
                    
                    FShader targetSnowFallShader = isLowQuality ? _fastSnowFallTintShader : _snowFallTintShader;
                    FShader targetBlizzardShader = isLowQuality ? _fastBlizzardTintShader : _blizzardTintShader;
                    
                    FShader originalSnowFallShader = isLowQuality 
                        ? (_originalFastSnowFallShader ?? rCam.room.game.rainWorld.Shaders["FastSnowFall"])
                        : (_originalSnowFallShader ?? rCam.room.game.rainWorld.Shaders["SnowFall"]);
                    
                    FShader originalBlizzardShader = isLowQuality
                        ? (_originalFastBlizzardShader ?? rCam.room.game.rainWorld.Shaders["FastBlizzard"])
                        : (_originalBlizzardShader ?? rCam.room.game.rainWorld.Shaders["Blizzard"]);
                    
                    if (hasEffect)
                    {
                        if (snowFallSprite.shader != targetSnowFallShader)
                        {
                            snowFallSprite.shader = targetSnowFallShader;
                        }
                        if (blizzardSprite.shader != targetBlizzardShader)
                        {
                            blizzardSprite.shader = targetBlizzardShader;
                        }
                    }
                    else
                    {
                        if (snowFallSprite.shader == targetSnowFallShader)
                        {
                            snowFallSprite.shader = originalSnowFallShader;
                        }
                        if (blizzardSprite.shader == targetBlizzardShader)
                        {
                            blizzardSprite.shader = originalBlizzardShader;
                        }
                    }
                    break;
                }
            }
        }
        
    }
}

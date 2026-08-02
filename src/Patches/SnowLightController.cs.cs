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
        
        // Último valor loggeado de SnowGrain (evitar spam)
        private static float _lastLoggedSnowGrain = -1f;

        // Shaders tintados
        private static FShader _snowTintShader;
        private static FShader _snowFallTintShader;
        private static FShader _fastSnowFallTintShader;
        private static FShader _blizzardTintShader;
        private static FShader _fastBlizzardTintShader;
        
        // Efectos registrados en DevTools
        public static readonly RoomSettings.RoomEffect.Type SnowLightEffect = 
            new RoomSettings.RoomEffect.Type("SnowLight", true);
        
        public static readonly RoomSettings.RoomEffect.Type SnowGrainEffect = 
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
                RSPlugin.log.LogInfo("[SnowLightController] Inicializado con SnowLight y SnowGrain");
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
            
            RSPlugin.log.LogDebug($"[SnowLightController] Assembly en: {assemblyDir}");
            RSPlugin.log.LogDebug($"[SnowLightController] Intentando cargar asset bundle desde: {bundlePath}");
            
            if (!File.Exists(bundlePath))
            {
                throw new FileNotFoundException($"Asset bundle no encontrado: {bundlePath}");
            }
            
            _bundle = AssetBundle.LoadFromFile(bundlePath);
            
            if (_bundle == null)
            {
                throw new Exception($"AssetBundle.LoadFromFile devolvió null para: {bundlePath}");
            }
            
            RSPlugin.log.LogInfo("[SnowLightController] Asset bundle cargado correctamente");
        }
        
        private static void RegisterShaders()
        {
            // Snow (suelo)
            Shader snowShader = _bundle.LoadAsset<Shader>("Assets/Shaders/SnowTintShader.shader");
            if (snowShader == null) throw new Exception("No se encontró SnowTintShader.shader");
            _snowTintShader = FShader.CreateShader("SnowTintShader", snowShader);
            Custom.rainWorld.Shaders["SnowTintShader"] = _snowTintShader;
            
            // SnowFall (nieve cayendo, calidad normal)
            Shader snowFallShader = _bundle.LoadAsset<Shader>("Assets/Shaders/SnowFallTintShader.shader");
            if (snowFallShader == null) throw new Exception("No se encontró SnowFallTintShader.shader");
            _snowFallTintShader = FShader.CreateShader("SnowFallTintShader", snowFallShader);
            Custom.rainWorld.Shaders["SnowFallTintShader"] = _snowFallTintShader;
            
            // FastSnowFall (nieve cayendo, calidad baja)
            Shader fastSnowFallShader = _bundle.LoadAsset<Shader>("Assets/Shaders/FastSnowFallTintShader.shader");
            if (fastSnowFallShader == null) throw new Exception("No se encontró FastSnowFallTintShader.shader");
            _fastSnowFallTintShader = FShader.CreateShader("FastSnowFallTintShader", fastSnowFallShader);
            Custom.rainWorld.Shaders["FastSnowFallTintShader"] = _fastSnowFallTintShader;
            
            // Blizzard (ventisca, calidad normal)
            Shader blizzardShader = _bundle.LoadAsset<Shader>("Assets/Shaders/BlizzardTintShader.shader");
            if (blizzardShader == null) throw new Exception("No se encontró BlizzardTintShader.shader");
            _blizzardTintShader = FShader.CreateShader("BlizzardTintShader", blizzardShader);
            Custom.rainWorld.Shaders["BlizzardTintShader"] = _blizzardTintShader;
            
            // FastBlizzard (ventisca, calidad baja)
            Shader fastBlizzardShader = _bundle.LoadAsset<Shader>("Assets/Shaders/FastBlizzardTintShader.shader");
            if (fastBlizzardShader == null) throw new Exception("No se encontró FastBlizzardTintShader.shader");
            _fastBlizzardTintShader = FShader.CreateShader("FastBlizzardTintShader", fastBlizzardShader);
            Custom.rainWorld.Shaders["FastBlizzardTintShader"] = _fastBlizzardTintShader;
            
            RSPlugin.log.LogInfo("[SnowLightController] 5 shaders registrados correctamente");
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
        }
        
        private static DevInterface.RoomSettingsPage.DevEffectsCategories OnDevEffectGetCategory(
            On.DevInterface.RoomSettingsPage.orig_DevEffectGetCategoryFromEffectType orig,
            DevInterface.RoomSettingsPage self,
            RoomSettings.RoomEffect.Type type)
        {
            if (type == SnowLightEffect)
                return DevInterface.RoomSettingsPage.DevEffectsCategories.Lighting;
            
            if (type == SnowGrainEffect)
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
            
            // ============================================
            // PROCESAR SNOWLIGHT
            // ============================================
            RoomSettings.RoomEffect snowLightEffect = self.room.roomSettings.GetEffect(SnowLightEffect);
            bool hasSnowLight = snowLightEffect != null;
            float snowLightAmount = hasSnowLight ? Mathf.Clamp01(snowLightEffect.amount) : 0.5f;
            
            if (hasSnowLight)
            {
                Shader.SetGlobalFloat("_SnowTintAmount", snowLightAmount);
            }
            
            // ============================================
            // PROCESAR SNOWGRAIN
            // ============================================
            RoomSettings.RoomEffect snowGrainEffect = self.room.roomSettings.GetEffect(SnowGrainEffect);
            bool hasSnowGrain = snowGrainEffect != null;
            float snowGrainAmount = hasSnowGrain ? Mathf.Clamp01(snowGrainEffect.amount) : 0f;
            
            if (hasSnowGrain)
            {
                Shader.SetGlobalFloat("_SnowGrainAmount", snowGrainAmount);
                if (!Mathf.Approximately(snowGrainAmount, _lastLoggedSnowGrain))
                {
                    _lastLoggedSnowGrain = snowGrainAmount;
                    RSPlugin.log.LogDebug($"[SnowGrain] Activo en {self.room.abstractRoom.name} con valor: {snowGrainAmount}");
                }
            }
            else
            {
                Shader.SetGlobalFloat("_SnowGrainAmount", 0f);
            }
            
            // ============================================
            // PROCESAR Snow (suelo)
            // ============================================
            if (self.room.snow)
            {
                ProcessSnowSpriteLeaser(self, hasSnowLight);
            }
            
            // ============================================
            // PROCESAR BlizzardGraphics (nieve cayendo + ventisca)
            // ============================================
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
                            RSPlugin.log.LogDebug($"[SnowLight] Snow shader aplicado en: {rCam.room.abstractRoom.name}");
                        }
                    }
                    else
                    {
                        if (snowSprite.shader == _snowTintShader)
                        {
                            snowSprite.shader = _originalDisplaySnowShader ?? rCam.room.game.rainWorld.Shaders["DisplaySnowShader"];
                            RSPlugin.log.LogDebug($"[SnowLight] Snow shader restaurado en: {rCam.room.abstractRoom.name}");
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

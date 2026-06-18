using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using RainCycles.Snapshot;

namespace RainCycles.Patches;

public static class RoomSettingsPatches
{
    private static bool _isSaving = false;

    public static void Init()
    {
        On.RoomSettings.Load_Timeline += OnLoad;
        On.RoomSettings.Save += OnSave;
    }

    private static bool OnLoad(On.RoomSettings.orig_Load_Timeline orig, RoomSettings self, SlugcatStats.Timeline timelinePoint)
    {
        self.ClearExtendedData();
        
        string filePath = self.filePath;
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            ParseExtendedData(self, filePath);
        }
        
        return orig(self, timelinePoint);
    }

    private static void OnSave(On.RoomSettings.orig_Save orig, RoomSettings self)
    {
        if (_isSaving)
        {
            orig(self);
            return;
        }
        
        _isSaving = true;
        orig(self);
        PreserveExtendedData(self);
        _isSaving = false;
        
        string filePath = self.filePath;
        if (!string.IsNullOrEmpty(filePath))
        {
            RSPlugin.log.LogDebug($"[RoomSettingsPatches] Settings guardado: {Path.GetFileName(filePath)}");
            
            StaticTintManager.InvalidateCache(filePath);
            
            var rw = UnityEngine.Object.FindObjectOfType<RainWorld>();
            var game = rw?.processManager?.currentMainLoop as RainWorldGame;
            if (game?.cameras != null)
            {
                foreach (var cam in game.cameras)
                {
                    if (cam?.room?.roomSettings?.filePath == filePath)
                    {
                        RSPlugin.log.LogDebug($"[RoomSettingsPatches] Recargando efectos visuales para sala {cam.room.abstractRoom?.name}");
                        
                        var freshSnap = StaticTintManager.GetCachedSnapshot(cam.room);
                        
                        if (freshSnap != null)
                        {
                            var rs = cam.room.roomSettings;
                            Color? tintMultiply = rs.GetTintMultiply();
                            Color? tintAtmosphere = rs.GetTintAtmosphere();
                            
                            if (tintMultiply.HasValue)
                            {
                                var c = tintMultiply.Value;
                                Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, new Vector4(c.r, c.g, c.b, 1f));
                                RSPlugin.log.LogDebug($"[RoomSettingsPatches] TintMultiply aplicado: {c}");
                            }
                            if (tintAtmosphere.HasValue)
                            {
                                var c = tintAtmosphere.Value;
                                Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, new Vector4(c.r, c.g, c.b, 1f));
                                RSPlugin.log.LogDebug($"[RoomSettingsPatches] TintAtmosphere aplicado: {c}");
                                
                                // También aplicar a ACV si existe en la sala
                                for (int i = 0; i < cam.room.updateList.Count; i++)
                                {
                                    if (cam.room.updateList[i] is AboveCloudsView acv)
                                    {
                                        acv.atmosphereColor = c;
                                        RSPlugin.log.LogDebug($"[RoomSettingsPatches] TintAtmosphere aplicado a ACV: {c}");
                                        break;
                                    }
                                }
                            }
                            
                            if (freshSnap.HasRcType && freshSnap.RcType == RcType.Blend)
                            {
                                if (SettingsBlendController.IsActive && SettingsBlendController.ActiveRoom == cam.room)
                                {
                                    RSPlugin.log.LogDebug($"[RoomSettingsPatches] Sala blend activa, refrescando snapshots del blend");
                                    SettingsBlendController.RefreshActiveSnapshots();
                                }
                                else
                                {
                                    if (freshSnap._hasPalette)
                                    {
                                        cam.ChangeMainPalette(freshSnap.Palette);
                                        if (freshSnap._hasFadePalette)
                                        {
                                            int fadePal = freshSnap.FadePaletteID;
                                            float fadeOp = cam.currentCameraPosition < freshSnap.FadePaletteOpacities.Length 
                                                ? freshSnap.FadePaletteOpacities[cam.currentCameraPosition] 
                                                : 0f;
                                            cam.ChangeFadePalette(fadePal, fadeOp);
                                        }
                                        cam.ApplyFade();
                                        RSPlugin.log.LogDebug($"[RoomSettingsPatches] Paleta recargada: {freshSnap.Palette}");
                                    }
                                }
                            }
                            
                            RoomEffectsApplier.ApplyScalarEffects(cam.room, freshSnap);
                            RoomEffectsApplier.ApplyTerrainScalars(cam.room, freshSnap);
                            Shader.SetGlobalFloat(RainWorld.ShadPropGrime, cam.room.roomSettings.Grime);
                        }
                    }
                }
            }
            
            SettingsBlendController.ForceRefreshSkySlots();
        }
    }

    private static void ParseExtendedData(RoomSettings self, string filePath)
    {
        try
        {
            foreach (string line in File.ReadAllLines(filePath, Encoding.UTF8))
            {
                string trimmed = line.TrimEnd('\r');
                
                if (trimmed.StartsWith("RC_TYPE:"))
                {
                    string value = trimmed.Substring("RC_TYPE:".Length).Trim().ToUpperInvariant();
                    self.SetRcType(value switch
                    {
                        "STATIC" => RcType.Static,
                        "BLEND" => RcType.Blend,
                        _ => RcType.None
                    });
                }
                else if (trimmed.StartsWith("RC_VIEW:"))
                {
                    string value = trimmed.Substring("RC_VIEW:".Length).Trim().ToUpperInvariant();
                    self.SetViewType(value switch
                    {
                        "ACV" => ViewType.ACV,
                        "RTV" => ViewType.RTV,
                        "PSV" => ViewType.PSV,
                        _ => ViewType.None
                    });
                }
                else if (trimmed.StartsWith("RC_TINT:"))
                {
                    string hexes = trimmed.Substring("RC_TINT:".Length).Trim();
                    string[] parts = hexes.Split(' ');
                    // Solo 2 colores: Multiply y Atmosphere
                    if (parts.Length >= 1)
                        self.SetTintMultiply(ParseHexColor(parts[0]));
                    if (parts.Length >= 2)
                        self.SetTintAtmosphere(ParseHexColor(parts[1]));
                    // Ya no hay tercer color
                }
            }
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[RoomSettingsPatches] Error parsing: {ex.Message}");
        }
    }

    private static void PreserveExtendedData(RoomSettings self)
    {
        string filePath = self.filePath;
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
        
        try
        {
            string content = File.ReadAllText(filePath, Encoding.UTF8);
            var lines = new List<string>(content.Split('\n'));
            
            lines.RemoveAll(l => l.Trim().StartsWith("RC_TYPE:"));
            lines.RemoveAll(l => l.Trim().StartsWith("RC_VIEW:"));
            lines.RemoveAll(l => l.Trim().StartsWith("RC_TINT:"));
            
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                lines.RemoveAt(lines.Count - 1);
            
            var newLinesList = new List<string>();
            
            if (self.HasRcType() && self.GetRcType() != RcType.None)
            {
                string typeValue = self.GetRcType() == RcType.Static ? "Static" : "Blend";
                newLinesList.Add($"RC_TYPE: {typeValue}");
            }
            
            if (self.GetViewType() != ViewType.None)
            {
                string viewValue = self.GetViewType() == ViewType.ACV ? "ACV" :
                                   self.GetViewType() == ViewType.RTV ? "RTV" : "PSV";
                newLinesList.Add($"RC_VIEW: {viewValue}");
            }
            
            Color? tintMultiply = self.GetTintMultiply();
            Color? tintAtmosphere = self.GetTintAtmosphere();
            
            // Solo 2 colores: Multiply y Atmosphere
            if (tintMultiply.HasValue || tintAtmosphere.HasValue)
            {
                string mul = tintMultiply.HasValue ? ColorToHex(tintMultiply.Value) : "FFFFFF";
                string atm = tintAtmosphere.HasValue ? ColorToHex(tintAtmosphere.Value) : "FFFFFF";
                newLinesList.Add($"RC_TINT: #{mul} #{atm}");
                RSPlugin.log.LogDebug($"[RoomSettingsPatches] RC_TINT guardado: #{mul} #{atm}");
            }
            
            if (newLinesList.Count > 0)
            {
                lines.AddRange(newLinesList);
                File.WriteAllText(filePath, string.Join("\n", lines), Encoding.UTF8);
                RSPlugin.log.LogDebug($"[RoomSettingsPatches] Datos RC_* guardados en archivo");
            }
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[RoomSettingsPatches] Error preserving: {ex.Message}");
        }
    }

    private static Color ParseHexColor(string hex)
    {
        hex = hex.Trim().TrimStart('#');
        if (hex.Length != 6) return Color.white;
        try
        {
            return new Color(
                Convert.ToByte(hex.Substring(0, 2), 16) / 255f,
                Convert.ToByte(hex.Substring(2, 2), 16) / 255f,
                Convert.ToByte(hex.Substring(4, 2), 16) / 255f);
        }
        catch { return Color.white; }
    }

    private static string ColorToHex(Color color)
    {
        return $"{Mathf.RoundToInt(color.r * 255f):X2}{Mathf.RoundToInt(color.g * 255f):X2}{Mathf.RoundToInt(color.b * 255f):X2}";
    }
}
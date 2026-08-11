using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using RainCycles.Snapshot;
using RainCycles.Blend;

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
            SettingsSnapshot.InvalidateCache(filePath);

            // Un guardado puede apuntar a cualquier archivo de estado (el panel
            // redirige roomSettings.filePath al estado seleccionado antes de guardar),
            // no solo al que tiene la cámara. Derivamos la sala desde el nombre del
            // archivo y limpiamos TODAS sus caches — incluida la de píxeles (_stateCache),
            // que InvalidateRoomCache no toca — para que el blend reconstruya desde los
            // archivos recién guardados (fix 08/2026: saves no reflejados en el blend).
            var rw = UnityEngine.Object.FindObjectOfType<RainWorld>();
            var game = rw?.processManager?.currentMainLoop as RainWorldGame;

            string derivedRoom = DeriveRoomNameFromPath(filePath);
            if (!string.IsNullOrEmpty(derivedRoom))
            {
                RoomCameraExtensions.UnloadRoomCache(derivedRoom);
                RoomCameraExtensions.InvalidateRoomCache(derivedRoom);
                RoomCameraExtensions.ReloadRoomTerrainCache(derivedRoom);

                if (SettingsBlendController.IsActive &&
                    string.Equals(SettingsBlendController.ActiveRoom?.abstractRoom?.name,
                        derivedRoom, StringComparison.OrdinalIgnoreCase))
                {
                    SettingsBlendController.RefreshActiveSnapshots();
                }
            }

            if (game?.cameras != null)
            {
                foreach (var cam in game.cameras)
                {
                    if (cam?.room?.roomSettings?.filePath == filePath)
                    {
                        string roomName = cam.room.abstractRoom?.name;

                        var freshSnap = SettingsSnapshot.GetCached(filePath, cam.room.abstractRoom?.name);

                        if (freshSnap != null)
                        {
                            var rs = cam.room.roomSettings;

                            if (rs.HasTint())
                            {
                                Color? tintMultiply = rs.GetTintMultiply();
                                Color? tintAtmosphere = rs.GetTintAtmosphere();

                                if (tintMultiply.HasValue)
                                {
                                    var c = tintMultiply.Value;
                                    Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, new Vector4(c.r, c.g, c.b, 1f));
                                }
                                if (tintAtmosphere.HasValue)
                                {
                                    var c = tintAtmosphere.Value;
                                    Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, new Vector4(c.r, c.g, c.b, 1f));

                                    for (int i = 0; i < cam.room.updateList.Count; i++)
                                    {
                                        if (cam.room.updateList[i] is AboveCloudsView acv)
                                        {
                                            acv.atmosphereColor = c;
                                            break;
                                        }
                                    }
                                }
                            }

                            if (freshSnap.HasRcType && freshSnap.RcType == RcType.Blend)
                            {
                                if (SettingsBlendController.IsActive && SettingsBlendController.ActiveRoom == cam.room)
                                {
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
                                    }
                                }
                            }

                            cam.room.ApplyScalarEffects(freshSnap, freshSnap, 0f);
                            Shader.SetGlobalFloat(RainWorld.ShadPropGrime, cam.room.roomSettings.Grime);
                        }
                    }
                }
            }

            SettingsBlendController.ForceRefreshSkySlots();
        }
    }

    // ============================================================
    // PARSEAR RAINCYCLES
    // ============================================================
    private static void ParseExtendedData(RoomSettings self, string filePath)
    {
        try
        {
            foreach (string line in File.ReadAllLines(filePath, Encoding.UTF8))
            {
                string trimmed = line.TrimEnd('\r');

                if (trimmed.StartsWith("RainCycles:"))
                {
                    string content = trimmed.Substring("RainCycles:".Length).Trim();
                    ParseRainCyclesContent(self, content);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[RoomSettingsPatches] Error parsing: {ex.Message}");
        }
    }

    private static void ParseRainCyclesContent(RoomSettings self, string content)
    {
        bool hasType = false;
        bool hasView = false;
        RcType type = RcType.None;
        ViewType view = ViewType.None;
        Color? tintMultiply = null;
        Color? tintAtmosphere = null;

        int pos = 0;
        while (pos < content.Length)
        {
            int start = content.IndexOf('<', pos);
            if (start < 0) break;
            int end = content.IndexOf('>', start);
            if (end < 0) break;

            string segment = content.Substring(start + 1, end - start - 1);
            int sep = segment.IndexOf(':');
            if (sep > 0)
            {
                string field = segment.Substring(0, sep).Trim();
                string value = segment.Substring(sep + 1).Trim();

                switch (field)
                {
                    case "Type":
                        hasType = true;
                        type = value.ToUpperInvariant() switch
                        {
                            "STATIC" => RcType.Static,
                            "BLEND" => RcType.Blend,
                            _ => RcType.None
                        };
                        break;
                    case "View":
                        if (hasType)
                        {
                            hasView = true;
                            view = value.ToUpperInvariant() switch
                            {
                                "ACV" => ViewType.ACV,
                                "RTV" => ViewType.RTV,
                                "PSV" => ViewType.PSV,
                                "AUV" => ViewType.AUV,
                                "ORV" => ViewType.ORV,
                                _ => ViewType.None
                            };
                        }
                        break;
                    case "Tint":
                        if (hasView)
                        {
                            string[] hexes = value.Split(' ');
                            if (hexes.Length >= 1) tintMultiply = ParseHexColor(hexes[0]);
                            if (hexes.Length >= 2) tintAtmosphere = ParseHexColor(hexes[1]);
                        }
                        break;
                }
            }
            pos = end + 1;
        }

        if (hasType)
        {
            self.SetRcType(type);
            if (hasView)
                self.SetViewType(view);
            else
                self.SetViewType(ViewType.None);

            if (hasView && (tintMultiply.HasValue || tintAtmosphere.HasValue))
            {
                self.SetTintMultiply(tintMultiply);
                self.SetTintAtmosphere(tintAtmosphere);
            }
            else
            {
                self.SetTintMultiply(null);
                self.SetTintAtmosphere(null);
            }
        }
        else
        {
            self.ClearExtendedData();
        }
    }

    private static string DeriveRoomNameFromPath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;
        string name = Path.GetFileNameWithoutExtension(filePath);
        int idx = name.IndexOf("_settings", StringComparison.OrdinalIgnoreCase);
        if (idx <= 0) return null;
        return name.Substring(0, idx);
    }

    private static void PreserveExtendedData(RoomSettings self)
    {
        string filePath = self.filePath;
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

        try
        {
            string content = File.ReadAllText(filePath, Encoding.UTF8);
            var lines = new List<string>(content.Split('\n'));

            lines.RemoveAll(l => l.Trim().StartsWith("RainCycles:"));

            string newLine = BuildRainCyclesLine(self);
            if (!string.IsNullOrEmpty(newLine))
            {
                while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                    lines.RemoveAt(lines.Count - 1);
                lines.Add(newLine);
            }

            File.WriteAllText(filePath, string.Join("\n", lines), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[RoomSettingsPatches] Error preserving: {ex.Message}");
        }
    }

    private static string BuildRainCyclesLine(RoomSettings self)
    {
        if (!self.HasRcType()) return null;

        var parts = new List<string> { $"Type:{self.GetRcType()}" };

        if (self.HasView())
        {
            parts.Add($"View:{self.GetViewType()}");

            if (self.HasTint())
            {
                Color? tintMultiply = self.GetTintMultiply();
                Color? tintAtmosphere = self.GetTintAtmosphere();
                string mul = tintMultiply.HasValue ? ColorToHex(tintMultiply.Value) : "FFFFFF";
                string atm = tintAtmosphere.HasValue ? ColorToHex(tintAtmosphere.Value) : "FFFFFF";
                parts.Add($"Tint:#{mul} #{atm}");
            }
        }

        return $"RainCycles: <{string.Join("><", parts)}>";
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
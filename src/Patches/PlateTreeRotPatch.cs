using Watcher;

namespace RainCycles.Patches;

// ============================================================
// PLATE TREE + SENTIENT ROT - AISLAMIENTO DE COLOR
// ============================================================
// Vanilla: cuando la infección de rot está activa (amount > 0),
// UpdateRotMode (RoomCamera.cs:3383) fuerza EffectColorB = 21
// (negro puro) en la paleta. El PlateTree lee ese píxel
// (PlateTree.cs:1335, GetPixel(31,3) = EffectColorB dark) y las
// partes que derivan de él (bulbos, gradientes de rama) se vuelven
// invisibles al interpretar el negro puro como transparencia.
//
// Problema: en salas blend, nuestro UpdateBlendPalette sobrescribe
// esos píxeles con los colores del blend (ApplyRotLayerToPalette
// llega a 0.9 * rotMode, nunca negro puro), así que el árbol nunca
// recibe el negro y el efecto rot se pierde. Además, al pausar se
// detienen las escrituras per-frame de paleta.
//
// Solución (aislada, sin tocar la paleta de blend): hook en
// PlateTree.ApplyPalette. Si la sala es blend Y tiene el efecto
// SentientRotInfection presente (aunque amount sea 0), forzamos
// effectColor = negro puro y re-aplicamos los colores derivados.
// El campo persiste y Bulb.DrawSprites (PlateTree.cs:899) lo
// re-lee cada frame de render -> funciona también en pausa.
// ============================================================

public static class PlateTreeRotPatch
{
    private static bool _initialized = false;

    public static void Init()
    {
        if (_initialized) return;
        On.PlateTree.ApplyPalette += OnPlateTreeApplyPalette;
        _initialized = true;
    }

    public static void Terminate()
    {
        if (!_initialized) return;
        On.PlateTree.ApplyPalette -= OnPlateTreeApplyPalette;
        _initialized = false;
    }

    private static void OnPlateTreeApplyPalette(
        On.PlateTree.orig_ApplyPalette orig,
        PlateTree self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
    {
        orig(self, sLeaser, rCam, palette);

        if (!ShouldForceRotBlack(self)) return;

        // ⭐ NEGRO PURO - sin blend para el árbol mientras el efecto exista.
        // Bulb.DrawSprites re-lee este campo cada frame (pausa incluida).
        self.effectColor = Color.black;

        if (sLeaser?.sprites == null || self.branchCaps == null) return;

        // Re-aplicar colores derivados de effectColor (mismo recorrido
        // que PlateTree.ApplyPalette original, líneas 1342-1354).
        // ⚠️ NO se tocan los círculos de los bulbos: su color real lo fija
        // Bulb.DrawSprites (PlateTree.cs:899) cada frame (ojos -> effectColor,
        // no-ojos -> Lerp(limbColor, effectColor, gradiente)). Si los pintáramos
        // de negro puro aquí, ese estado quedaría "pegado" durante la pausa
        // (en pausa corre nuestro ApplyPalette vía UpdateCameras pero NO corre
        // DrawSprites) y los círculos no-ojos se volverían transparentes.
        int spriteIdx = self.firstLimbSprite;
        for (int j = 0; j < self.branchCaps.Length; j++)
        {
            PlateTree.BranchCap cap = self.branchCaps[j];

            // Mesh de la rama: mismo lerp que el original pero con effectColor = negro.
            // (Solo se pinta en ApplyPalette, nunca en DrawSprites -> consistente en pausa.)
            if (spriteIdx < sLeaser.sprites.Length && sLeaser.sprites[spriteIdx] is TriangleMesh mesh)
            {
                for (int k = 0; k < mesh.verticeColors.Length; k += 2)
                {
                    float t = (float)k / (float)(mesh.verticeColors.Length - 1);
                    Color c = Color.Lerp(self.limbColor, Color.black, cap.BranchGradient(t));
                    mesh.verticeColors[k] = c;
                    mesh.verticeColors[k + 1] = c;
                }
            }
            spriteIdx += 1 + cap.TotalSprites;
        }
    }

    private static bool ShouldForceRotBlack(PlateTree tree)
    {
        if (tree?.room?.roomSettings == null) return false;
        var rs = tree.room.roomSettings;

        // SOLO salas blend administradas por RainCycles.
        // Salas vanilla y static: comportamiento vanilla intacto
        // (en static nuestro pipeline de paleta ni siquiera corre).
        if (rs.GetRcType() != RcType.Blend) return false;

        // Presencia del efecto (aunque amount sea 0): mismo patrón
        // que DayNightBlocker — el efecto existe en la lista o no.
        for (int i = 0; i < rs.effects.Count; i++)
        {
            if (rs.effects[i].type == WatcherEnums.RoomEffectType.SentientRotInfection)
                return true;
        }
        return false;
    }
}

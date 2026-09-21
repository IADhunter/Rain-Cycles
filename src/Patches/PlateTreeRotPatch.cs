using Watcher;

namespace RainCycles.Patches;

// ============================================================
// PLATE TREE + SENTIENT ROT - AISLAMIENTO DE COLOR
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

        self.effectColor = Color.black;

        if (sLeaser?.sprites == null || self.branchCaps == null) return;

        int spriteIdx = self.firstLimbSprite;
        for (int j = 0; j < self.branchCaps.Length; j++)
        {
            PlateTree.BranchCap cap = self.branchCaps[j];

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

        if (rs.GetRcType() != RcType.Blend) return false;

        for (int i = 0; i < rs.effects.Count; i++)
        {
            if (rs.effects[i].type == WatcherEnums.RoomEffectType.SentientRotInfection)
                return true;
        }
        return false;
    }
}

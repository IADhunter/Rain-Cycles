using System;
using System.Linq;
using DevInterface;
using UnityEngine;

namespace FilesSetting;

public class RCPanel : Panel, IDevUISignals
{
    private const float BUTTON_WIDTH   = 30f;
    private const float BUTTON_SPACING = 5f;
    private const float MARGIN         = 5f;

    private const float ROW_A_Y       = 155f;
    private const float MODE_ROW_Y    = 135f;
    private const float ROW_B_Y       = 115f;
    private const float SLIDER_A_Y    = 95f;
    private const float SLIDER_B_Y    = 75f;
    private const float REGION_ROW_Y  = 35f;
    private const float ACTIVE_FILE_Y = 18f;
    private const float MODE_BTN_W    = 46f;
    private const float SKY_BTN_W     = 30f;
    private const float EDIT_BTN_W    = 40f;

    public static int buttonSelectedA = 1;
    public static int buttonSelectedB = 2;

    public RCPanel(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos, Vector2 size, string title)
        : base(owner, IDstring, parentNode, pos, size, title)
    {
<<<<<<< Updated upstream
        int n     = ReadStateReadFiles.CountRainStateFiles(owner.room?.abstractRoom?.name);
        int cycle = owner.room.game.GetStorySession.saveState.cycleNumber;
=======
        bool isArena  = owner.room?.game?.IsArenaSession == true;
        string roomName0 = owner.room?.abstractRoom?.name;
        int n     = isArena
            ? ArenaStateResolver.CountSettingsFiles(roomName0)
            : StateFileResolver.CountRainStateFiles(roomName0);
        int cycle = owner.room?.game?.GetStorySession?.saveState?.cycleNumber ?? 0;
>>>>>>> Stashed changes
        buttonSelectedA = n > 0 ? (cycle % n) + 1 : 1;
        buttonSelectedB = n > 1 ? (buttonSelectedA % n) + 1 : buttonSelectedA;

        for (int i = 1; i <= n; i++)
            subNodes.Add(new SelectButton(owner, $"RCA_{i}", this,
                new Vector2(MARGIN, ROW_A_Y), BUTTON_WIDTH, i.ToString(), false));
        subNodes.Add(new Button(owner, "RC_Plus", this,
            new Vector2(MARGIN, ROW_A_Y), BUTTON_WIDTH, "+"));

        if (n >= 2)
        {
            for (int i = 1; i <= n; i++)
                subNodes.Add(new SelectButton(owner, $"RCB_{i}", this,
                    new Vector2(MARGIN, ROW_B_Y), BUTTON_WIDTH, i.ToString(), false));

            var cs = BlendSettingsLoader.Active;
            bool isLoop = cs == null || cs.Mode == BlendMode.Loop || cs.Mode == BlendMode.Custom;

            // Slider A — locked in Loop/Custom, active in Cycle/EndCycle
            var sA = new BlendSlider(owner, "RC_BlendSlider", this, new Vector2(MARGIN, SLIDER_A_Y));
            if (isLoop) sA.SetLocked(true);
            subNodes.Add(sA);

            // Slider B — active in Loop/Custom, locked in Cycle/EndCycle
            var sB = new BlendSlider(owner, "RC_BlendSliderB", this, new Vector2(MARGIN, SLIDER_B_Y));
            if (!isLoop) sB.SetLocked(true);
            subNodes.Add(sB);
        }

        string active = System.IO.Path.GetFileName(owner.room.roomSettings.filePath ?? "");
        subNodes.Add(new DevUILabel(owner, "RC_ActiveFile", this,
            new Vector2(MARGIN, ACTIVE_FILE_Y), (int)size.x - (int)MARGIN * 2, active));

        var mode = BlendSettingsLoader.Active?.Mode ?? BlendMode.Loop;
        BlendMode[] modes = { BlendMode.Loop, BlendMode.Cycle, BlendMode.EndCycle, BlendMode.Custom };
        for (int i = 0; i < modes.Length; i++)
            subNodes.Add(new ModeButton(owner, $"RC_Mode_{modes[i]}", this,
                new Vector2(MARGIN + i * (MODE_BTN_W + BUTTON_SPACING), MODE_ROW_Y),
                MODE_BTN_W, modes[i], modes[i] == mode));

        string roomName = owner.room?.abstractRoom?.name ?? "";

        // Fila Edit / ACV / RTV — tres botones en la misma fila
        float editX = MARGIN;
        float acvX  = MARGIN + EDIT_BTN_W + BUTTON_SPACING;
        float rtvX  = acvX + SKY_BTN_W + BUTTON_SPACING;

        subNodes.Add(new EditModeButton(owner, "RC_EditMode", this,
            new Vector2(editX, REGION_ROW_Y + 22f), EDIT_BTN_W));

        var currentSky = BlendSettingsWriter.GetSkyType(roomName);
        subNodes.Add(new SkyTypeButton(owner, "RC_Sky_ACV", this,
            new Vector2(acvX, REGION_ROW_Y + 22f), SKY_BTN_W, SkyType.ACV, roomName));
        subNodes.Add(new SkyTypeButton(owner, "RC_Sky_RTV", this,
            new Vector2(rtvX, REGION_ROW_Y + 22f), SKY_BTN_W, SkyType.RTV, roomName));

        bool reg = owner.room?.game?.IsArenaSession == true
            ? ArenaStateResolver.IsLevelRegistered(owner.room?.abstractRoom?.name)
            : BlendSettingsWriter.IsRoomRegistered(owner.room?.abstractRoom?.name);
        subNodes.Add(new RoomToggleButton(owner, "RC_ToggleRoom", this,
            new Vector2(MARGIN, REGION_ROW_Y), size.x - MARGIN * 2, reg));

        ReorganizeButtons();
    }

    public void Signal(DevUISignalType type, DevUINode sender, string message)
    {
        if (type != DevUISignalType.ButtonClick) return;

        if (sender.IDstring == "RC_Plus")
        {
            int cnt = subNodes.Count(n => n.IDstring.StartsWith("RCA_")) + 1;
            subNodes.Add(new SelectButton(owner, $"RCA_{cnt}", this,
                new Vector2(MARGIN, ROW_A_Y), BUTTON_WIDTH, cnt.ToString(), false));
            subNodes.Add(new SelectButton(owner, $"RCB_{cnt}", this,
                new Vector2(MARGIN, ROW_B_Y), BUTTON_WIDTH, cnt.ToString(), false));
            ReadStateReadFiles.CreateNewRainStateFile(owner.room?.abstractRoom?.name, cnt, owner.room);
            ReorganizeButtons();
            return;
        }

        if (sender.IDstring.StartsWith("RCA_"))
        {
            int sel = int.Parse(sender.IDstring.Split('_')[1]);
<<<<<<< Updated upstream
            string path = ReadStateReadFiles.GetRainStateSettingsFile(
                owner.room?.abstractRoom?.name, sel);
=======
            string path = ResolveSettingsFile(owner.room?.abstractRoom?.name, sel);
>>>>>>> Stashed changes
            if (path == null) return;

            if (SettingsBlendController.IsActive) { SettingsBlendController.Detach(); BlendSlider.Reset(); }

            string rcTint = SettingsBlendController.ExtractRcTintLine(path);
            owner.room.roomSettings.filePath = path;
            owner.room.roomSettings.Load((SlugcatStats.Timeline)null);
            // RoomSettings.Load no limpia terrainFadePalette si el settings no lo declara.
            var snapTerrain = SettingsSnapshot.FromFile(path);
            if (!snapTerrain._hasTerrainFadePalette)
                owner.room.roomSettings.terrainFadePalette = null;
            var c0 = owner.room.game.cameras[0];
            c0.ApplyEffectColorsToAllPaletteTextures(base.RoomSettings.EffectColorA, base.RoomSettings.EffectColorB);
            c0.ChangeMainPalette(base.RoomSettings.Palette);
            if (base.RoomSettings.fadePalette != null)
                c0.ChangeFadePalette(base.RoomSettings.fadePalette.palette,
                    base.RoomSettings.fadePalette.fades[c0.currentCameraPosition]);
            c0.ApplyFade();
            if (owner.room.roomSettings?.TerrainPalette != null)
                c0.ReloadTerrainPalette();

            var snap = SettingsSnapshot.FromFileWithTemplate(path, owner.room.abstractRoom.name);
            SettingsBlendController.SetActiveSnapshot(snap);
            Color mul, atm;
            RoomEffectsApplier.CalcBackgroundColors(c0, out mul, out atm);
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, mul);
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, atm);

            RoomEffectsApplier.ApplyDecalsFromSnapshot(owner.room, path);
            RoomEffectsApplier.ApplyLightSourcesFromSnapshot(owner.room, path);
            RoomEffectsApplier.ApplyLightBeamsFromSnapshot(owner.room, path);
            Shader.SetGlobalFloat(RainWorld.ShadPropGrime, base.RoomSettings.Grime);
            SettingsBlendController.ApplySkyForState(sel, owner.room);

            owner.room.roomSettings.Save();
            owner.room.roomSettings.filePath = path;
            SettingsBlendController.ReappendRcTint(path, rcTint);
            UpdateLabel(path);
            buttonSelectedA = sel;
            foreach (var node in subNodes) node.Refresh();
            parentNode?.Refresh();
            return;
        }

        if (sender.IDstring.StartsWith("RC_Mode_") && sender is ModeButton mb)
        {
            string room = owner.room?.abstractRoom?.name;
            foreach (var node in subNodes) if (node is ModeButton m) m.SetActive(m == mb);
            BlendSettingsWriter.SetMode(room, mb.Mode);

            bool nowLoop = mb.Mode == BlendMode.Loop || mb.Mode == BlendMode.Custom;
            foreach (var node in subNodes)
                if (node is BlendSlider bs)
                {
                    if (bs.IDstring == "RC_BlendSlider")  bs.SetLocked(nowLoop);
                    if (bs.IDstring == "RC_BlendSliderB") bs.SetLocked(!nowLoop);
                }

            SettingsBlendController.ResetFull();
            if (BlendClock.IsRunning) BlendClock.Stop();
            if (nowLoop) BlendClock.Start(buttonSelectedA);
            Plugin.RSPlugin.log.LogInfo($"[RCPanel] Mode → {mb.Mode}");
            return;
        }

        if (sender.IDstring == "RC_ToggleRoom")
        {
            string room = owner.room?.abstractRoom?.name;
            bool now = owner.room?.game?.IsArenaSession == true
                ? ArenaStateResolver.ToggleLevel(room)
                : BlendSettingsWriter.ToggleRoom(room);
            (subNodes.FirstOrDefault(n => n.IDstring == "RC_ToggleRoom") as RoomToggleButton)?.SetRegistered(now);
            return;
        }

        if (sender.IDstring == "RC_Sky_ACV" || sender.IDstring == "RC_Sky_RTV")
        {
            string room = owner.room?.abstractRoom?.name;
            SkyType clicked = sender.IDstring == "RC_Sky_ACV" ? SkyType.ACV : SkyType.RTV;
            SkyType current = BlendSettingsWriter.GetSkyType(room);

            // Si ya está activo → quitar (None). Si no → poner este tipo.
            SkyType next = current == clicked ? SkyType.None : clicked;
            BlendSettingsWriter.SetSkyType(room, next);

            // Refrescar ambos botones
            foreach (var node in subNodes)
                if (node is SkyTypeButton sb) sb.Refresh(next);
            return;
        }

        if (sender.IDstring.StartsWith("RCB_"))
        {
            buttonSelectedB = int.Parse(sender.IDstring.Split('_')[1]);
            if (!Mathf.Approximately(BlendSlider.BlendFactor, 0f))  BuildAndActivateA();
            if (!Mathf.Approximately(BlendSlider.BlendFactorB, 0f)) BuildAndActivateB();
            foreach (var node in subNodes) node.Refresh();
        }
    }

    // ── Manual blend ─────────────────────────────────────────────────────
    // Slider A — used for Cycle / EndCycle (linear, 0→1)
    // Slider B — used for Loop / Custom (global 0→1 across flat sequence)
    //            0→0.5 = first half, 0.5→1 = second half
    //            The midpoint (0.5) is the anchor state.

    // Phase lists are built from the flat sequence the same way BlendClock does:
    // we read LoopLane data, concatenate to [1,2,3,4,1], and derive transitions.

    private System.Collections.Generic.List<(int from, int to)> _phasesA = null;
    // Full phase list for the entire flat sequence (used by slider B)
    private System.Collections.Generic.List<(int from, int to)> _phasesFlat = null;

    // ── Slider A (Cycle/EndCycle) ─────────────────────────────────────────

    public void OnSliderAStarted()
    {
        _phasesA = null;
        SettingsBlendController.ClearPendingOrigin();
        BuildAndActivateA();
    }

    public void OnSliderAMoved(float t)
    {
        ApplyPhased(t, ref _phasesA, buildFn: BuildAndActivateA);
    }

    private void BuildAndActivateA()
    {
        var s = BlendSettingsLoader.Active;
        _phasesA = BuildPhaseList(buttonSelectedA, buttonSelectedB, s, useLaneB: false);
        if (_phasesA != null && _phasesA.Count > 0) ActivatePhase(_phasesA[0]);
    }

    // ── Slider B (Loop/Custom) ────────────────────────────────────────────

    public void OnSliderBStarted()
    {
        _phasesFlat = null;
        SettingsBlendController.ClearPendingOrigin();
        BuildFlatPhases();
        if (_phasesFlat != null && _phasesFlat.Count > 0) ActivatePhase(_phasesFlat[0]);
    }

    public void OnSliderBMoved(float t)
    {
        if (_phasesFlat == null) { BuildFlatPhases(); if (_phasesFlat == null) return; }
        ApplyPhased(t, ref _phasesFlat, buildFn: BuildFlatPhases);
    }

    // Builds the phase list for the full flat sequence [1,2,3,4,1] → [(1,2),(2,3),(3,4),(4,1)]
    private void BuildFlatPhases()
    {
        var s = BlendSettingsLoader.Active;
        var flat = BuildFlatSequence(s, buttonSelectedA);
        if (flat == null || flat.Count < 2) { _phasesFlat = new System.Collections.Generic.List<(int,int)>(); return; }
        _phasesFlat = new System.Collections.Generic.List<(int, int)>();
        for (int i = 0; i < flat.Count - 1; i++) _phasesFlat.Add((flat[i], flat[i + 1]));
    }

    // Reads LoopLane data and builds the concatenated flat sequence.
    // Mirrors exactly what BlendClock.BuildFlatLoop does.
    private static System.Collections.Generic.List<int> BuildFlatSequence(BlendSettings s, int resolved)
    {
        if (s == null) return null;
        var laneData = s.GetLoopLane(resolved);
        if (laneData.HasValue && laneData.Value.IsValid)
        {
            var ld = laneData.Value;
            System.Collections.Generic.List<int> first, second;
            if (ld.LaneA.Count > 0 && ld.LaneA[0] == resolved) { first = ld.LaneA; second = ld.LaneB; }
            else if (ld.LaneB.Count > 0 && ld.LaneB[0] == resolved) { first = ld.LaneB; second = ld.LaneA; }
            else { first = ld.LaneA; second = ld.LaneB; }
            var flat = new System.Collections.Generic.List<int>(first);
            for (int i = 1; i < second.Count; i++) flat.Add(second[i]);
            return flat;
        }
        // No lane block: use base sequence + close the loop
        var seq = s.GetSequenceFor(resolved);
        if (seq == null || seq.Count < 2) return null;
        var r = new System.Collections.Generic.List<int>(seq);
        if (r[r.Count - 1] != resolved) r.Add(resolved);
        return r;
    }

    // ── Shared phase application ──────────────────────────────────────────

    private void ApplyPhased(float globalT,
        ref System.Collections.Generic.List<(int from, int to)> phases,
        System.Action buildFn)
    {
        string room = owner.room?.abstractRoom?.name;
        if (phases == null || phases.Count == 0) { buildFn(); if (phases == null) return; }

        int   cnt  = phases.Count;
        float size = 1f / cnt;
        int   idx  = Mathf.Min(Mathf.FloorToInt(globalT / size), cnt - 1);
        float locT = Mathf.Clamp01((globalT - idx * size) / size);

        var phase = phases[idx];
        string pA = ReadStateReadFiles.GetRainStateSettingsFile(room, phase.from);

        if (!SettingsBlendController.IsActive || SettingsBlendController.CurrentPathA != pA)
            ActivatePhase(phase);

        SettingsBlendController.SetExternalT(locT);
    }

    private void ActivatePhase((int from, int to) phase)
    {
        string room = owner.room?.abstractRoom?.name;
        string pA = ReadStateReadFiles.GetRainStateSettingsFile(room, phase.from);
        string pB = ReadStateReadFiles.GetRainStateSettingsFile(room, phase.to);
        if (pA != null && pB != null && phase.from != phase.to)
            SettingsBlendController.AttachWithExternalT(owner.room, pA, pB);
    }

    private void BuildAndActivateB()
    {
        BuildFlatPhases();
        if (_phasesFlat != null && _phasesFlat.Count > 0) ActivatePhase(_phasesFlat[0]);
    }

    public void ResetRelaySystem()
    {
        _phasesA = _phasesFlat = null;
        SettingsBlendController.Detach();
        ApplyStateA();
        BlendSlider.Reset();
        foreach (var node in subNodes) if (node is BlendSlider bs) bs.SetDisplayT(0f);
    }

    private static System.Collections.Generic.List<(int from, int to)> BuildPhaseList(
        int stateA, int stateB, BlendSettings s, bool useLaneB)
    {
        var phases = new System.Collections.Generic.List<(int, int)>();
        if (stateA == stateB) return phases;
        System.Collections.Generic.List<int> seq = null;
        if (s != null)
        {
            if (useLaneB)
            {
                foreach (var kv in s.LoopLanes)
                    if (kv.Value.IsValid && kv.Value.LaneB.Contains(stateA)) { seq = kv.Value.LaneB; break; }
            }
            else seq = s.GetSequenceFor(stateA);
        }
        if (seq != null && seq.Count >= 2)
        {
            int ia = seq.IndexOf(stateA), ib = seq.IndexOf(stateB);
            if (ia >= 0 && ib > ia) { for (int i = ia; i < ib; i++) phases.Add((seq[i], seq[i+1])); return phases; }
        }
        phases.Add((stateA, stateB));
        return phases;
    }

    // ── Layout & helpers ──────────────────────────────────────────────────

    private void ReorganizeButtons()
    {
        var bA  = subNodes.Where(n => n.IDstring.StartsWith("RCA_")).ToList();
        var bB  = subNodes.Where(n => n.IDstring.StartsWith("RCB_")).ToList();
        var plus = subNodes.FirstOrDefault(n => n.IDstring == "RC_Plus");

        int bpr = Math.Max(1, (int)((this.size.x - 2*MARGIN + BUTTON_SPACING) / (BUTTON_WIDTH + BUTTON_SPACING)));

        for (int i = 0; i < bA.Count; i++)
        {
            (bA[i] as PositionedDevUINode).Move(new Vector2(
                MARGIN + (i%bpr)*(BUTTON_WIDTH+BUTTON_SPACING),
                ROW_A_Y - (i/bpr)*(BUTTON_WIDTH+BUTTON_SPACING)));
            if (i+1 == buttonSelectedA) (bA[i] as SelectButton).Select();
        }
        for (int i = 0; i < bB.Count; i++)
        {
            (bB[i] as PositionedDevUINode).Move(new Vector2(
                MARGIN + (i%bpr)*(BUTTON_WIDTH+BUTTON_SPACING),
                ROW_B_Y - (i/bpr)*(BUTTON_WIDTH+BUTTON_SPACING)));
            if (i+1 == buttonSelectedB) (bB[i] as SelectButton).Select();
        }
        if (plus != null)
        {
            int t = bA.Count;
            (plus as PositionedDevUINode).Move(new Vector2(
                MARGIN + (t%bpr)*(BUTTON_WIDTH+BUTTON_SPACING),
                ROW_A_Y - (t/bpr)*(BUTTON_WIDTH+BUTTON_SPACING)));
        }
    }

    private void UpdateLabel(string path)
    {
        var lbl = subNodes.FirstOrDefault(n => n.IDstring == "RC_ActiveFile") as DevUILabel;
        if (lbl != null) lbl.Text = System.IO.Path.GetFileName(path ?? "");
    }

    // Resuelve el path de un settings_N.txt usando el resolver correcto según sesión.
    private string ResolveSettingsFile(string roomName, int n)
    {
        if (owner.room?.game?.IsArenaSession == true)
            return ArenaStateResolver.GetSettingsPath(roomName, n);
        return StateFileResolver.GetRainStateSettingsFile(roomName, n);
    }

    public void ApplyStateA()
    {
<<<<<<< Updated upstream
        string path = ReadStateReadFiles.GetRainStateSettingsFile(
            owner.room?.abstractRoom?.name, buttonSelectedA);
=======
        string path = ResolveSettingsFile(owner.room?.abstractRoom?.name, buttonSelectedA);
>>>>>>> Stashed changes
        if (path == null) return;
        owner.room.roomSettings.filePath = path;
        owner.room.roomSettings.Load((SlugcatStats.Timeline)null);

        // RoomSettings.Load no limpia terrainFadePalette si el settings no lo declara.
        // Verificar via snapshot si realmente está declarado — si no, limpiar.
        var snapCheck = RainCycles.Snapshot.SettingsSnapshot.FromFile(path);
        if (!snapCheck._hasTerrainFadePalette)
        {
            owner.room.roomSettings.terrainFadePalette = null;
            RSPlugin.log.LogDebug($"[ApplyStateA] Cleared terrainFadePalette for state {buttonSelectedA}");
        }
        else
        {
            RSPlugin.log.LogDebug($"[ApplyStateA] Keeping terrainFadePalette='{snapCheck.TerrainFadePaletteName}' for state {buttonSelectedA}");
        }
        var c0 = owner.room.game.cameras[0];
        c0.ApplyEffectColorsToAllPaletteTextures(base.RoomSettings.EffectColorA, base.RoomSettings.EffectColorB);
        c0.ChangeMainPalette(base.RoomSettings.Palette);
        if (base.RoomSettings.fadePalette != null)
            c0.ChangeFadePalette(base.RoomSettings.fadePalette.palette,
                base.RoomSettings.fadePalette.fades[c0.currentCameraPosition]);
        c0.ApplyFade();
        // Actualizar terrain palette si la sala tiene una declarada
        if (owner.room.roomSettings?.TerrainPalette != null)
            c0.ReloadTerrainPalette();
        RoomEffectsApplier.ApplyDecalsFromSnapshot(owner.room, path);
        RoomEffectsApplier.ApplyLightSourcesFromSnapshot(owner.room, path);
        RoomEffectsApplier.ApplyLightBeamsFromSnapshot(owner.room, path);
        Shader.SetGlobalFloat(RainWorld.ShadPropGrime, base.RoomSettings.Grime);
        // Actualizar cielo instantáneamente al estado seleccionado
        SettingsBlendController.ApplySkyForState(buttonSelectedA, owner.room);
        UpdateLabel(path);
    }
}
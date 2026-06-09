using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using DevInterface;
using UnityEngine;
using RainCycles.Snapshot;
using RainCycles.Patches;
using RainCycles.Core;

namespace FilesSetting;

public class RCPanel : Panel, IDevUISignals
{
    private class TabArrowButton : ArrowButton
    {
        private RCPanel _panel;
        private int _direction;

        public TabArrowButton(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos, float rotation, int direction, RCPanel panel)
            : base(owner, IDstring, parentNode, pos, rotation)
        {
            _direction = direction;
            _panel = panel;
        }

        public override void Clicked()
        {
            if (_panel != null)
            {
                _panel.SwitchTab(_panel.CurrentTab + _direction);
            }
        }
    }

    private const float BUTTON_WIDTH = 30f;
    private const float BUTTON_SPACING = 5f;
    private const float MARGIN = 5f;
    private const float ROW_A_Y = 170f;
    private const float SLIDER_Y = 145f;
    private const float TOP_ROW_Y = 5f;
    private const float EDIT_BTN_X = 5f;
    private const float EDIT_BTN_W = 30f;
    private const float ACTIVE_FILE_X = 40f;
    private const float ACTIVE_FILE_WIDTH = 170f;

    private int _currentTab = 0;
    private ArrowButton _prevTabButton;
    private ArrowButton _nextTabButton;

    private List<SelectButton> _stateButtons = new List<SelectButton>();
    private Button _plusButton;
    private Button _minusButton;
    private BlendSlider _blendSlider;
    private DevUILabel _activeFileLabel;
    private EditModeButton _editModeButton;

    private RectangularDevUINode _currentContent;

    public static int ButtonSelectedA = 1;
    public DevUI Owner => owner;
    public Room CurrentRoom => owner.room;
    public string CurrentRoomName => owner.room?.abstractRoom?.name;
    public int CurrentTab => _currentTab;

    private List<(int from, int to)> _phases = null;

    public RCPanel(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos, Vector2 size, string title)
        : base(owner, IDstring, parentNode, pos, size, title)
    {
        string currentFilePath = owner.room?.roomSettings?.filePath;
        int currentState = StateFileResolver.GetStateFromPath(currentFilePath, CurrentRoomName);
        ButtonSelectedA = currentState >= 1 ? currentState : 1;

        CreateCommonElements();
        SwitchTab(0);
    }

    private void CreateCommonElements()
    {
        _prevTabButton = new TabArrowButton(owner, "RC_PrevTab", this, 
            new Vector2(size.x - 42f, size.y - 20f), 270f, -1, this);
        _nextTabButton = new TabArrowButton(owner, "RC_NextTab", this, 
            new Vector2(size.x - 21f, size.y - 20f), 90f, 1, this);
        subNodes.Add(_prevTabButton);
        subNodes.Add(_nextTabButton);
    }

    private void CreateRoomViewElements()
    {
        DestroyRoomViewElements();
        
        if (_editModeButton == null)
        {
            _editModeButton = new EditModeButton(owner, "RC_EditMode", this,
                new Vector2(EDIT_BTN_X, TOP_ROW_Y), EDIT_BTN_W);
            subNodes.Add(_editModeButton);
        }
        
        if (_activeFileLabel == null)
        {
            string active = Path.GetFileName(owner.room.roomSettings.filePath ?? "");
            _activeFileLabel = new DevUILabel(owner, "RC_ActiveFile", this,
                new Vector2(ACTIVE_FILE_X, TOP_ROW_Y), ACTIVE_FILE_WIDTH, active);
            subNodes.Add(_activeFileLabel);
        }
        
        RebuildStateButtons();
    }

    private void DestroyRoomViewElements()
    {
        foreach (var btn in _stateButtons)
        {
            btn.ClearSprites();
            subNodes.Remove(btn);
        }
        _stateButtons.Clear();

        if (_plusButton != null)
        {
            _plusButton.ClearSprites();
            subNodes.Remove(_plusButton);
            _plusButton = null;
        }
        if (_minusButton != null)
        {
            _minusButton.ClearSprites();
            subNodes.Remove(_minusButton);
            _minusButton = null;
        }
        if (_blendSlider != null)
        {
            _blendSlider.ClearSprites();
            subNodes.Remove(_blendSlider);
            _blendSlider = null;
        }
        if (_activeFileLabel != null)
        {
            _activeFileLabel.ClearSprites();
            subNodes.Remove(_activeFileLabel);
            _activeFileLabel = null;
        }
        if (_editModeButton != null)
        {
            _editModeButton.ClearSprites();
            subNodes.Remove(_editModeButton);
            _editModeButton = null;
        }
    }

    public void RebuildStateButtons()
    {
        foreach (var btn in _stateButtons)
        {
            btn.ClearSprites();
            subNodes.Remove(btn);
        }
        if (_plusButton != null)
        {
            _plusButton.ClearSprites();
            subNodes.Remove(_plusButton);
        }
        if (_minusButton != null)
        {
            _minusButton.ClearSprites();
            subNodes.Remove(_minusButton);
        }
        _stateButtons.Clear();

        bool isArena = owner.room?.game?.IsArenaSession == true;
        int fileCount = isArena ? ArenaStateResolver.CountSettingsFiles(CurrentRoomName)
                                : StateFileResolver.CountRainStateFiles(CurrentRoomName);

        for (int i = 1; i <= fileCount; i++)
        {
            bool isSelected = (i == ButtonSelectedA);
            var btn = new SelectButton(owner, $"RCA_{i}", this,
                new Vector2(MARGIN, ROW_A_Y), BUTTON_WIDTH, i.ToString(), isSelected);
            subNodes.Add(btn);
            _stateButtons.Add(btn);
            if (isSelected) btn.Select();
        }

        _plusButton = new Button(owner, "RC_Plus", this, new Vector2(MARGIN, ROW_A_Y), 30f, "   +");
        _minusButton = new Button(owner, "RC_Minus", this, new Vector2(MARGIN, ROW_A_Y), 30f, "    -");
        subNodes.Add(_plusButton);
        subNodes.Add(_minusButton);

        if (fileCount >= 2 && _blendSlider == null)
        {
            _blendSlider = new BlendSlider(owner, "RC_BlendSlider", this, new Vector2(MARGIN, SLIDER_Y));
            subNodes.Add(_blendSlider);
        }
        else if (fileCount < 2 && _blendSlider != null)
        {
            _blendSlider.ClearSprites();
            subNodes.Remove(_blendSlider);
            _blendSlider = null;
        }

        ReorganizeStateButtons();
    }

    private void ReorganizeStateButtons()
    {
        int bpr = Math.Max(1, (int)((size.x - 2 * MARGIN + BUTTON_SPACING) / (BUTTON_WIDTH + BUTTON_SPACING)));

        for (int i = 0; i < _stateButtons.Count; i++)
        {
            _stateButtons[i].Move(new Vector2(
                MARGIN + (i % bpr) * (BUTTON_WIDTH + BUTTON_SPACING),
                ROW_A_Y - (i / bpr) * (BUTTON_WIDTH + BUTTON_SPACING)));
        }

        int t = _stateButtons.Count;
        if (_plusButton != null)
        {
            _plusButton.Move(new Vector2(
                MARGIN + (t % bpr) * (BUTTON_WIDTH + BUTTON_SPACING),
                ROW_A_Y - (t / bpr) * (BUTTON_WIDTH + BUTTON_SPACING)));
        }
        t++;
        if (_minusButton != null)
        {
            _minusButton.Move(new Vector2(
                MARGIN + (t % bpr) * (BUTTON_WIDTH + BUTTON_SPACING),
                ROW_A_Y - (t / bpr) * (BUTTON_WIDTH + BUTTON_SPACING)));
        }
    }

    private void ClearContent()
    {
        if (_currentContent != null)
        {
            _currentContent.ClearSprites();
            subNodes.Remove(_currentContent);
            _currentContent = null;
        }
    }

    private void UpdateTitle()
    {
        string tabName = _currentTab == 0 ? "Room" : (_currentTab == 1 ? "View" : "Region");
        Title = $"Rain Cycles: {tabName}";
    }

    public void SwitchTab(int newTab)
    {
        if (newTab < 0) newTab = 2;
        if (newTab > 2) newTab = 0;

        _currentTab = newTab;
        ClearContent();

        if (_currentTab == 2)
        {
            DestroyRoomViewElements();
        }
        else
        {
            CreateRoomViewElements();
        }

        switch (_currentTab)
        {
            case 0:
                _currentContent = new RCPanel_RoomPage(this);
                break;
            case 1:
                _currentContent = new RCPanel_ViewPage(this);
                break;
            case 2:
                _currentContent = new RCPanel_RegionPage(this);
                break;
        }

        if (_currentContent != null)
        {
            _currentContent.IDstring = "RC_PageContent";
            subNodes.Add(_currentContent);
        }

        UpdateTitle();
        Refresh();
    }

    public string ResolveSettingsFile(int n)
    {
        if (owner.room?.game?.IsArenaSession == true)
            return ArenaStateResolver.GetSettingsPath(CurrentRoomName, n);
        return StateFileResolver.GetRainStateSettingsFile(CurrentRoomName, n);
    }

    public void ApplyStateA()
    {
        string path = ResolveSettingsFile(ButtonSelectedA);
        if (path == null) return;
        
        owner.room.roomSettings.filePath = path;
        owner.room.roomSettings.Load((SlugcatStats.Timeline)null);

        var snapCheck = SettingsSnapshot.FromFile(path);
        if (!snapCheck._hasTerrainFadePalette)
            owner.room.roomSettings.terrainFadePalette = null;

        var c0 = owner.room.game.cameras[0];
        c0.ApplyEffectColorsToAllPaletteTextures(owner.room.roomSettings.EffectColorA, owner.room.roomSettings.EffectColorB);
        c0.ChangeMainPalette(owner.room.roomSettings.Palette);
        if (owner.room.roomSettings.fadePalette != null)
            c0.ChangeFadePalette(owner.room.roomSettings.fadePalette.palette,
                owner.room.roomSettings.fadePalette.fades[c0.currentCameraPosition]);
        c0.ApplyFade();
        if (owner.room.roomSettings?.TerrainPalette != null)
            c0.ReloadTerrainPalette();

        ApplyTintsFromSnapshot(snapCheck);
        if (snapCheck.TintCloudAtmosphere.HasValue)
            SettingsBlendController.SetLastAtmosphereColor(snapCheck.TintCloudAtmosphere.Value);

        RoomEffectsApplier.ApplyDecalsFromSnapshot(owner.room, path);
        RoomEffectsApplier.ApplyLightSourcesFromSnapshot(owner.room, path);
        RoomEffectsApplier.ApplyLightBeamsFromSnapshot(owner.room, path);
        Shader.SetGlobalFloat(RainWorld.ShadPropGrime, owner.room.roomSettings.Grime);
        SettingsBlendController.ApplySkyForState(ButtonSelectedA, owner.room);
        
        if (_activeFileLabel != null)
            _activeFileLabel.Text = Path.GetFileName(path ?? "");
    }

    public void ApplyTintsFromSnapshot(SettingsSnapshot snap)
    {
        if (snap.TintMultiply.HasValue)
        {
            var c = snap.TintMultiply.Value;
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, new Vector4(c.r, c.g, c.b, 1f));
        }
        if (snap.TintAtmosphere.HasValue)
        {
            var c = snap.TintAtmosphere.Value;
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, new Vector4(c.r, c.g, c.b, 1f));
        }
        if (snap.TintCloudAtmosphere.HasValue)
        {
            for (int i = 0; i < owner.room.updateList.Count; i++)
            {
                if (owner.room.updateList[i] is AboveCloudsView acv)
                {
                    acv.atmosphereColor = snap.TintCloudAtmosphere.Value;
                    break;
                }
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // BLEND MANUAL - SLIDER (SECUENCIAS INTRÍNSECAS)
    // ═════════════════════════════════════════════════════════════════════

    public void OnSliderStarted()
    {
        // Solo permitir si EditMode está activo
        if (!BlendClock.EditMode) return;
        
        _phases = null;
        SettingsBlendController.ClearPendingOrigin();
        BuildPhases();
        if (_phases != null && _phases.Count > 0)
        {
            ActivatePhase(_phases[0]);
        }
        else
        {
            RSPlugin.log.LogWarning($"[RCPanel] OnSliderStarted: NO phases built!");
        }
    }

    public void OnSliderMoved(float t)
    {
        // Solo permitir si EditMode está activo
        if (!BlendClock.EditMode) return;
        
        if (_phases == null || _phases.Count == 0)
        {
            BuildPhases();
            if (_phases == null || _phases.Count == 0)
            {
                RSPlugin.log.LogWarning($"[RCPanel] OnSliderMoved: still no phases, returning");
                return;
            }
        }

        int cnt = _phases.Count;
        float size = 1f / cnt;
        int idx = Mathf.Min(Mathf.FloorToInt(t / size), cnt - 1);
        float locT = Mathf.Clamp01((t - idx * size) / size);

        var phase = _phases[idx];

        string room = owner.room?.abstractRoom?.name;
        string pA = StateFileResolver.GetRainStateSettingsFile(room, phase.from);

        if (!SettingsBlendController.IsActive || SettingsBlendController.CurrentPathA != pA)
        {
            ActivatePhase(phase);
        }

        SettingsBlendController.SetExternalT(locT);
    }

    private void BuildPhases()
    {
        var s = BlendSettingsLoader.Active;
        BlendMode mode = s?.Mode ?? BlendMode.Loop;

        if (mode == BlendMode.Loop)
            BuildLoopPhases();
        else
            BuildLinearPhases();
    }

    private void BuildLoopPhases()
    {
        int initial = ButtonSelectedA;
        
        var laneA = new List<int>();
        for (int i = 0; i < 3; i++)
            laneA.Add(((initial - 1 + i) % 4) + 1);
        
        var laneB = new List<int>();
        for (int i = 2; i < 5; i++)
            laneB.Add(((initial - 1 + i) % 4) + 1);
        
        var flat = new List<int>(laneA);
        for (int i = 1; i < laneB.Count; i++)
            flat.Add(laneB[i]);
        
        if (flat[flat.Count - 1] != initial)
            flat.Add(initial);
        
        _phases = new List<(int, int)>();
        for (int i = 0; i < flat.Count - 1; i++)
            _phases.Add((flat[i], flat[i + 1]));
    }

    private void BuildLinearPhases()
    {
        int initial = ButtonSelectedA;
        var seq = new List<int>();
        for (int i = 0; i < 3; i++)
            seq.Add(((initial - 1 + i) % 4) + 1);
        
        _phases = new List<(int, int)>();
        for (int i = 0; i < seq.Count - 1; i++)
            _phases.Add((seq[i], seq[i + 1]));
    }

    private void ActivatePhase((int from, int to) phase)
    {
        string room = owner.room?.abstractRoom?.name;
        string pA = StateFileResolver.GetRainStateSettingsFile(room, phase.from);
        string pB = StateFileResolver.GetRainStateSettingsFile(room, phase.to);
        
        if (pA != null && pB != null && phase.from != phase.to)
        {
            SettingsBlendController.AttachWithExternalT(owner.room, pA, pB, isAuto: false);
        }
        else
        {
            RSPlugin.log.LogWarning($"[RCPanel] ActivatePhase: missing paths! pA={pA != null}, pB={pB != null}, sameState={phase.from == phase.to}");
        }
    }

    public void ResetRelaySystem()
    {
        _phases = null;
        SettingsBlendController.Detach();
        ApplyStateA();
        BlendSlider.Reset();
        if (_blendSlider != null)
            _blendSlider.SetDisplayT(0f);
    }

    public void Signal(DevUISignalType type, DevUINode sender, string message)
    {
        if (type != DevUISignalType.ButtonClick) return;

        if (sender.IDstring.StartsWith("RCA_"))
        {
            // Bloquear si EditMode está apagado Y el blend clock está corriendo
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;

            int sel = int.Parse(sender.IDstring.Split('_')[1]);
            string path = ResolveSettingsFile(sel);
            if (path == null) return;

            if (SettingsBlendController.IsActive) { SettingsBlendController.Detach(); if (_blendSlider != null) BlendSlider.Reset(); }

            owner.room.roomSettings.filePath = path;
            owner.room.roomSettings.Load((SlugcatStats.Timeline)null);

            var snapTerrain = SettingsSnapshot.FromFile(path);
            if (!snapTerrain._hasTerrainFadePalette)
                owner.room.roomSettings.terrainFadePalette = null;

            var c0 = owner.room.game.cameras[0];
            c0.ChangeMainPalette(owner.room.roomSettings.Palette);
            if (owner.room.roomSettings.fadePalette != null)
                c0.ChangeFadePalette(owner.room.roomSettings.fadePalette.palette,
                    owner.room.roomSettings.fadePalette.fades[c0.currentCameraPosition]);
            c0.ApplyFade();
            if (owner.room.roomSettings?.TerrainPalette != null)
                c0.ReloadTerrainPalette();

            var snapTint = SettingsSnapshot.FromFileWithTemplate(path, CurrentRoomName);
            SettingsBlendController.SetActiveSnapshot(snapTint);
            ApplyTintsFromSnapshot(snapTint);

            if (snapTint.TintCloudAtmosphere.HasValue)
                SettingsBlendController.SetLastAtmosphereColor(snapTint.TintCloudAtmosphere.Value);

            RoomEffectsApplier.ApplyDecalsFromSnapshot(owner.room, path);
            RoomEffectsApplier.ApplyLightSourcesFromSnapshot(owner.room, path);
            RoomEffectsApplier.ApplyLightBeamsFromSnapshot(owner.room, path);
            Shader.SetGlobalFloat(RainWorld.ShadPropGrime, owner.room.roomSettings.Grime);
            SettingsBlendController.ApplySkyForState(sel, owner.room);

            if (_activeFileLabel != null)
                _activeFileLabel.Text = Path.GetFileName(path ?? "");
            ButtonSelectedA = sel;

            foreach (var node in subNodes) node.Refresh();
            parentNode?.Refresh();
            return;
        }

        if (sender.IDstring == "RC_Plus")
        {
            // Bloquear si EditMode está apagado Y el blend clock está corriendo
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;

            int cnt = _stateButtons.Count + 1;
            var newBtn = new SelectButton(owner, $"RCA_{cnt}", this,
                new Vector2(MARGIN, ROW_A_Y), BUTTON_WIDTH, cnt.ToString(), false);
            _stateButtons.Add(newBtn);
            subNodes.Add(newBtn);
            StateFileResolver.CreateNewRainStateFile(CurrentRoomName, cnt, owner.room);

            if (cnt == 2 && _blendSlider == null)
            {
                _blendSlider = new BlendSlider(owner, "RC_BlendSlider", this, new Vector2(MARGIN, SLIDER_Y));
                subNodes.Add(_blendSlider);
            }

            ReorganizeStateButtons();
            return;
        }

        if (sender.IDstring == "RC_Minus")
        {
            // Bloquear si EditMode está apagado Y el blend clock está corriendo
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;

            string path = ResolveSettingsFile(ButtonSelectedA);
            if (path != null && File.Exists(path))
                File.Delete(path);

            ButtonSelectedA = 1;
            RebuildStateButtons();
            ApplyStateA();
            return;
        }
    }

    public override void Update()
    {
        base.Update();
    }
}
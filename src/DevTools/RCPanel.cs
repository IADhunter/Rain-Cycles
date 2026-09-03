using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using DevInterface;
using UnityEngine;
using RainCycles.Snapshot;
using RainCycles.Patches;
using RainCycles.Core;
using RainCycles.Blend;

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

        var activeStates = StateFileResolver.GetActiveStates(CurrentRoomName);

        if (activeStates.Count > 0)
        {
            foreach (int state in activeStates)
            {
                bool isSelected = (state == ButtonSelectedA);
                var btn = new SelectButton(owner, $"RCA_{state}", this,
                    new Vector2(MARGIN, ROW_A_Y), BUTTON_WIDTH, state.ToString(), isSelected);
                subNodes.Add(btn);
                _stateButtons.Add(btn);
                if (isSelected) btn.Select();
            }
        }
        else
        {
            ButtonSelectedA = 1;
        }

        _plusButton = new Button(owner, "RC_Plus", this, new Vector2(MARGIN, ROW_A_Y), 30f, "   +");
        _minusButton = new Button(owner, "RC_Minus", this, new Vector2(MARGIN, ROW_A_Y), 30f, "    -");
        subNodes.Add(_plusButton);
        subNodes.Add(_minusButton);

        UpdateSliderVisibility();
        ReorganizeStateButtons();
    }

    private void UpdateSliderVisibility()
    {
        bool hasFullStates = StateFileResolver.HasFullStates(CurrentRoomName);

        if (hasFullStates && _blendSlider == null)
        {
            _blendSlider = new BlendSlider(owner, "RC_BlendSlider", this, new Vector2(MARGIN, SLIDER_Y));
            subNodes.Add(_blendSlider);
        }
        else if (!hasFullStates && _blendSlider != null)
        {
            _blendSlider.ClearSprites();
            subNodes.Remove(_blendSlider);
            _blendSlider = null;
        }
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
        // El flag de arena lo gestiona ArenaBlendController (StateFileResolver.IsArenaMode).
        string tabName = _currentTab == 0 ? "Room"
            : (_currentTab == 1 ? "View"
            : (StateFileResolver.IsArenaMode ? "Arena" : "Region"));
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
        // StateFileResolver delega a ArenaBlendController en modo arena.
        return StateFileResolver.ResolveSettingsPath(CurrentRoomName, n);
    }

    private string GetVanillaSettingsPath()
    {
        if (owner.room?.roomSettings == null) return null;
        string path = owner.room.roomSettings.filePath;
        if (string.IsNullOrEmpty(path)) return null;

        string dir = Path.GetDirectoryName(path);
        string fileName = Path.GetFileNameWithoutExtension(path);
        int idx = fileName.ToLowerInvariant().LastIndexOf("_settings_");
        if (idx >= 0)
        {
            fileName = fileName.Substring(0, idx);
        }
        return Path.Combine(dir, fileName + ".txt");
    }

    public void RefreshViewPage()
    {
        foreach (var node in subNodes)
        {
            if (node is RCPanel_ViewPage viewPage)
            {
                viewPage.RefreshFromCurrentState();
                return;
            }
        }
    }

    public void ApplyStateA()
    {
        if (StateFileResolver.IsPendingDelete(CurrentRoomName, ButtonSelectedA))
        {
            var activeStates = StateFileResolver.GetActiveStates(CurrentRoomName);
            if (activeStates.Count > 0)
                ButtonSelectedA = activeStates[0];
            else
                return;
        }

        string path = ResolveSettingsFile(ButtonSelectedA);
        if (path == null) return;

        owner.room.roomSettings.filePath = path;
        RoomSettingsPatches.RefreshParent(owner.room.roomSettings, owner.room.world.region);
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

        owner.room.ApplyDecalOpacities(SettingsSnapshot.GetCached(path, CurrentRoomName));
        RoomCameraExtensions.ApplyLightSourcesFromSnapshot(owner.room, path);
        RoomCameraExtensions.ApplyLightBeamsFromSnapshot(owner.room, path);
        Shader.SetGlobalFloat(RainWorld.ShadPropGrime, owner.room.roomSettings.Grime);
        SettingsBlendController.ApplySkyForState(ButtonSelectedA, owner.room);

        SettingsBlendController.UpdateManualStates(ButtonSelectedA, ButtonSelectedA);

        if (_activeFileLabel != null)
            _activeFileLabel.Text = Path.GetFileName(path ?? "");

        RefreshViewPage();

        if (_currentContent is RCPanel_RoomPage roomPage)
        {
            roomPage.RefreshButtons();
        }
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

            for (int i = 0; i < owner.room.updateList.Count; i++)
            {
                if (owner.room.updateList[i] is AboveCloudsView acv)
                {
                    acv.atmosphereColor = c;
                    break;
                }
            }
        }
    }

    // ============================================================
    // BLEND MANUAL - SLIDER
    // ============================================================

    public void OnSliderStarted()
    {
        if (!BlendClock.EditMode) return;

        if (!StateFileResolver.HasFullStates(CurrentRoomName))
            return;

        _phases = null;
        SettingsBlendController.ClearPendingOrigin();
        BuildPhases();
        if (_phases != null && _phases.Count > 0)
        {
            ActivatePhase(_phases[0]);
        }
    }

    public void OnSliderMoved(float t)
    {
        if (!BlendClock.EditMode) return;
        if (!StateFileResolver.HasFullStates(CurrentRoomName))
            return;

        if (_phases == null || _phases.Count == 0)
        {
            BuildPhases();
            if (_phases == null || _phases.Count == 0) return;
        }

        int cnt = _phases.Count;
        float size = 1f / cnt;
        int idx = Math.Min(Mathf.FloorToInt(t / size), cnt - 1);
        float locT = Mathf.Clamp01((t - idx * size) / size);

        var phase = _phases[idx];

        string room = owner.room?.abstractRoom?.name;
        string pA = StateFileResolver.ResolveSettingsPath(room, phase.from);

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
        if (initial < 1 || initial > 4) initial = 1;

        _phases = new List<(int, int)>();

        for (int i = 0; i < 4; i++)
        {
            int from = ((initial - 1 + i) % 4) + 1;
            int to = ((initial - 1 + i + 1) % 4) + 1;
            _phases.Add((from, to));
        }
    }

    private void BuildLinearPhases()
    {
        int initial = ButtonSelectedA;
        if (initial < 1 || initial > 4) initial = 1;

        _phases = new List<(int, int)>();

        for (int i = 0; i < 2; i++)
        {
            int from = ((initial - 1 + i) % 4) + 1;
            int to = ((initial - 1 + i + 1) % 4) + 1;
            _phases.Add((from, to));
        }
    }

    private void ActivatePhase((int from, int to) phase)
    {
        string room = owner.room?.abstractRoom?.name;
        string pA = StateFileResolver.ResolveSettingsPath(room, phase.from);
        string pB = StateFileResolver.ResolveSettingsPath(room, phase.to);

        if (pA != null && pB != null && phase.from != phase.to)
        {
            SettingsBlendController.UpdateManualStates(phase.from, phase.to);
            SettingsBlendController.AttachWithExternalT(owner.room, pA, pB, isAuto: false);
        }
    }

    // ============================================================
    // CLEAR BLEND ONLY
    // ============================================================
    public void ClearBlendOnly()
    {
        _phases = null;
        SettingsBlendController.Detach();
        ApplyStateA();
        BlendSlider.Reset();
        if (_blendSlider != null)
            _blendSlider.SetDisplayT(0f);
    }

    // ============================================================
    // RESET TO CYCLE STATE
    // ============================================================
    public void ResetToCycleState()
    {
        _phases = null;
        SettingsBlendController.Detach();

        int correctState = StateFileResolver.GetCurrentCycleState();
        if (correctState < 1 || correctState > 4) correctState = 1;

        ButtonSelectedA = correctState;
        ApplyStateA();
        RebuildStateButtons();

        BlendSlider.Reset();
        if (_blendSlider != null)
            _blendSlider.SetDisplayT(0f);
    }

    public void ResetRelaySystem()
    {
        ResetToCycleState();
    }

    public void Signal(DevUISignalType type, DevUINode sender, string message)
    {
        if (type != DevUISignalType.ButtonClick) return;

        if (sender.IDstring.StartsWith("RCA_"))
        {
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;

            int sel = int.Parse(sender.IDstring.Split('_')[1]);

            if (StateFileResolver.IsPendingDelete(CurrentRoomName, sel))
            {
                StateFileResolver.UnmarkPendingDelete(CurrentRoomName, sel);
                RebuildStateButtons();
            }

            string path = ResolveSettingsFile(sel);
            if (path == null) return;

            ButtonSelectedA = sel;

            ClearBlendOnly();

            owner.room.roomSettings.filePath = path;
            RoomSettingsPatches.RefreshParent(owner.room.roomSettings, owner.room.world.region);
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

            owner.room.ApplyDecalOpacities(SettingsSnapshot.GetCached(path, CurrentRoomName));
            RoomCameraExtensions.ApplyLightSourcesFromSnapshot(owner.room, path);
            RoomCameraExtensions.ApplyLightBeamsFromSnapshot(owner.room, path);
            Shader.SetGlobalFloat(RainWorld.ShadPropGrime, owner.room.roomSettings.Grime);
            SettingsBlendController.ApplySkyForState(sel, owner.room);

            SettingsBlendController.UpdateManualStates(sel, sel);

            if (_activeFileLabel != null)
                _activeFileLabel.Text = Path.GetFileName(path ?? "");

            var c0Final = owner.room.game.cameras[0];
            c0Final.ApplyEffectColorsToAllPaletteTextures(
                owner.room.roomSettings.EffectColorA, owner.room.roomSettings.EffectColorB);

            if (snapTint != null && !snapTint.HasTint)
            {
                TintManager.RestoreOriginalViewState(owner.room);
            }

            RefreshViewPage();

            if (_currentContent is RCPanel_RoomPage roomPage)
            {
                roomPage.RefreshButtons();
            }

            foreach (var node in subNodes) node.Refresh();
            if (parentNode is not ObjectsPage)
                parentNode?.Refresh();
            return;
        }

        if (sender.IDstring == "RC_Plus")
        {
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;

            var activeStates = StateFileResolver.GetActiveStates(CurrentRoomName);
            int nextState = activeStates.Count + 1;

            if (nextState > 4)
                return;

            if (StateFileResolver.IsPendingDelete(CurrentRoomName, nextState))
            {
                StateFileResolver.UnmarkPendingDelete(CurrentRoomName, nextState);
            }
            else
            {
                string path = ResolveSettingsFile(nextState);
                if (path == null || !File.Exists(path))
                {
                    StateFileResolver.CreateNewRainStateFile(CurrentRoomName, nextState, owner.room);
                }
            }

            ButtonSelectedA = nextState;
            RebuildStateButtons();
            ApplyStateA();
            return;
        }

        if (sender.IDstring == "RC_Minus")
        {
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;

            var activeStates = StateFileResolver.GetActiveStates(CurrentRoomName);
            if (activeStates.Count == 0) return;

            int highestState = activeStates.Max();

            if (highestState == 1)
            {
                string path = ResolveSettingsFile(1);
                if (path != null && File.Exists(path))
                {
                    string vanillaPath = GetVanillaSettingsPath();
                    if (vanillaPath != null && File.Exists(vanillaPath))
                    {
                        File.Copy(vanillaPath, path, overwrite: true);
                    }
                    else
                    {
                        owner.room.roomSettings.filePath = path;
                        owner.room.roomSettings.Save();
                    }

                    StateFileResolver.MarkPendingDelete(CurrentRoomName, 1);
                    ButtonSelectedA = 1;
                    RebuildStateButtons();
                    ApplyStateA();
                }
            }
            else
            {
                string path = ResolveSettingsFile(highestState);
                if (path != null && File.Exists(path))
                {
                    File.Delete(path);
                }

                if (ButtonSelectedA == highestState)
                {
                    var remaining = StateFileResolver.GetActiveStates(CurrentRoomName);
                    ButtonSelectedA = remaining.Count > 0 ? remaining.Max() : 1;
                }

                RebuildStateButtons();
                ApplyStateA();
            }

            return;
        }
    }

    public override void Update()
    {
        base.Update();
    }
}
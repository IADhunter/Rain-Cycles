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

    // Orden top→bottom: FilaA, Modos, FilaB, SliderA, SliderB, RoomToggle, ActiveFile
    private const float ROW_A_Y       = 155f;  // Fila A (swap)
    private const float MODE_ROW_Y    = 135f;  // Fila modos
    private const float ROW_B_Y       = 115f;  // Fila B (destino blend)
    private const float SLIDER_Y      = 95f;   // Blend A
    private const float SLIDER_B_Y    = 75f;   // Blend B (solo Loop)
    private const float REGION_ROW_Y  = 35f;   // Room ON/OFF
    private const float ACTIVE_FILE_Y = 18f;   // Etiqueta archivo activo
    private const float MODE_BTN_W    = 46f;   // Ancho botón de modo
    private const float EDIT_MODE_Y   = 155f;  // Misma fila que botones de modo (encima)
    private const float EDIT_BTN_W    = 46f;   // Ancho botón Edit Mode

    // Estado A = actualmente cargado en la habitación
    public static int buttonSelectedA = 1;
    // Estado B = destino del blend
    public static int buttonSelectedB = 2;

    public RCPanel(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos, Vector2 size, string title)
        : base(owner, IDstring, parentNode, pos, size, title)
    {
        int n     = ReadStateReadFiles.CountRainStateFiles(owner.room?.abstractRoom?.name);
        int cycle = owner.room.game.GetStorySession.saveState.cycleNumber;
        buttonSelectedA = n > 0 ? (cycle % n) + 1 : 1;
        buttonSelectedB = n > 1 ? (buttonSelectedA % n) + 1 : buttonSelectedA;

        // ── Fila A: botones de swap directo ──────────────────────────────
        for (int i = 1; i <= n; i++)
        {
            subNodes.Add(new SelectButton(owner, $"RCA_{i}", this,
                new Vector2(MARGIN, ROW_A_Y), BUTTON_WIDTH, i.ToString(), false));
        }
        subNodes.Add(new Button(owner, "RC_Plus", this,
            new Vector2(MARGIN, ROW_A_Y), BUTTON_WIDTH, "+"));

        // ── Fila B: botones de destino del blend (solo si hay 2+ archivos) ─
        if (n >= 2)
        {
            for (int i = 1; i <= n; i++)
            {
                subNodes.Add(new SelectButton(owner, $"RCB_{i}", this,
                    new Vector2(MARGIN, ROW_B_Y), BUTTON_WIDTH, i.ToString(), false));
            }

            // ── Slider A ──────────────────────────────────────────────────
            subNodes.Add(new BlendSlider(owner, "RC_BlendSlider", this,
                new Vector2(MARGIN, SLIDER_Y)));

            // ── Slider B (solo en modo Loop) ───────────────────────────────
            // Siempre se crea pero se posiciona fuera del panel si el modo no es Loop.
            // Esto permite activarlo/desactivarlo sin reconstruir el panel.
            var loopSettings = BlendSettingsLoader.Active;
            bool isLoopMode = loopSettings == null || loopSettings.Mode == BlendMode.Loop;
            var sliderBNode = new BlendSlider(owner, "RC_BlendSliderB", this,
                new Vector2(MARGIN, SLIDER_B_Y));
            if (!isLoopMode) sliderBNode.SetLocked(true);
            subNodes.Add(sliderBNode);
        }

        // Etiqueta de archivo activo — muestra qué settings cargó el ciclo
        string activeName = System.IO.Path.GetFileName(owner.room.roomSettings.filePath ?? "");
        subNodes.Add(new DevUILabel(owner, "RC_ActiveFile", this,
            new Vector2(MARGIN, ACTIVE_FILE_Y), (int)size.x - (int)MARGIN * 2, activeName));

        // ── Fila de modos ─────────────────────────────────────────────────
        var currentSettings = BlendSettingsLoader.Active;
        BlendMode currentMode = currentSettings?.Mode ?? BlendMode.Loop;

        BlendMode[] modes = { BlendMode.Loop, BlendMode.Cycle, BlendMode.EndCycle, BlendMode.Custom };
        for (int i = 0; i < modes.Length; i++)
        {
            subNodes.Add(new ModeButton(owner, $"RC_Mode_{modes[i]}", this,
                new Vector2(MARGIN + i * (MODE_BTN_W + BUTTON_SPACING), MODE_ROW_Y),
                MODE_BTN_W, modes[i], modes[i] == currentMode));
        }

        // Botón Edit Mode — encima del botón Room ON/OFF
        // Posicionado justo encima de REGION_ROW_Y para separarlo visualmente
        // de los botones de modo y dejarlo junto al control de sala al que conceptualmente pertenece.
        subNodes.Add(new EditModeButton(owner, "RC_EditMode", this,
            new Vector2(MARGIN, REGION_ROW_Y + 22f), size.x - MARGIN * 2));

        // Botón toggle: registra/quita esta sala en blend_settings.txt
        bool isRegistered = BlendSettingsWriter.IsRoomRegistered(owner.room?.abstractRoom?.name);
        subNodes.Add(new RoomToggleButton(owner, "RC_ToggleRoom", this,
            new Vector2(MARGIN, REGION_ROW_Y), size.x - MARGIN * 2, isRegistered));

        ReorganizeButtons();
    }

    public void Signal(DevUISignalType type, DevUINode sender, string message)
    {
        if (type != DevUISignalType.ButtonClick) return;

        // ── Botón + (crear nuevo settings) ───────────────────────────────
        if (sender.IDstring == "RC_Plus")
        {
            int buttonCount = subNodes.Count(n => n.IDstring.StartsWith("RCA_")) + 1;
            subNodes.Add(new SelectButton(owner, $"RCA_{buttonCount}", this,
                new Vector2(MARGIN, ROW_A_Y), BUTTON_WIDTH, buttonCount.ToString(), false));
            subNodes.Add(new SelectButton(owner, $"RCB_{buttonCount}", this,
                new Vector2(MARGIN, ROW_B_Y), BUTTON_WIDTH, buttonCount.ToString(), false));
            ReadStateReadFiles.CreateNewRainStateFile(owner.room?.abstractRoom?.name, buttonCount, owner.room);
            ReorganizeButtons();
            return;
        }

        // ── Fila A: swap directo ──────────────────────────────────────────
        if (sender.IDstring.StartsWith("RCA_"))
        {
            int newSelected = int.Parse(sender.IDstring.Split('_')[1]);
            string path = ReadStateReadFiles.GetRainStateSettingsFile(
                owner.room?.abstractRoom?.name, newSelected);
            if (path == null) return;

            // Desactivar blend si estaba activo
            if (SettingsBlendController.IsActive)
            {
                SettingsBlendController.Detach();
                BlendSlider.Reset();
            }

            // Leer RC_TINT ANTES de todo — OnRoomSettingsSave lo preserva automáticamente,
            // pero también lo leemos aquí para el caso de primera inyección.
            string rcTintLine = SettingsBlendController.ExtractRcTintLine(path);

            // Carga directa
            owner.room.roomSettings.filePath = path;
            owner.room.roomSettings.Load((SlugcatStats.Timeline)null);
            owner.room.game.cameras[0].ApplyEffectColorsToAllPaletteTextures(
                base.RoomSettings.EffectColorA, base.RoomSettings.EffectColorB);
            owner.room.game.cameras[0].ChangeMainPalette(base.RoomSettings.Palette);
            if (base.RoomSettings.fadePalette != null)
                owner.room.game.cameras[0].ChangeFadePalette(
                    base.RoomSettings.fadePalette.palette,
                    base.RoomSettings.fadePalette.fades[owner.room.game.cameras[0].currentCameraPosition]);
            owner.room.game.cameras[0].ApplyFade();

            // Cargar el snapshot del nuevo setting para que CalcBackgroundColors
            // lea RC_TINT correctamente antes de aplicar los globals.
            var swapSnap = SettingsSnapshot.FromFileWithTemplate(path, owner.room.abstractRoom.name);
            SettingsBlendController.SetActiveSnapshot(swapSnap);

            // Aplicar globals de fondo desde la paleta recién cargada.
            // currentPalette ya está actualizada por ApplyFade → ApplyPalette.
            {
                var cam = owner.room.game.cameras[0];
                Color multiply, atmosphere;
                RoomEffectsApplier.CalcBackgroundColors(cam, out multiply, out atmosphere);
                Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, multiply);
                Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, atmosphere);
            }

            RoomEffectsApplier.ApplyDecalsFromSnapshot(owner.room, path);
            RoomEffectsApplier.ApplyLightSourcesFromSnapshot(owner.room, path);
            RoomEffectsApplier.ApplyLightBeamsFromSnapshot(owner.room, path);
            Shader.SetGlobalFloat(RainWorld.ShadPropGrime, base.RoomSettings.Grime);

            // OnRoomSettingsSave preserva RC_TINT automáticamente.
            // ReappendRcTint aquí es una red de seguridad extra.
            owner.room.roomSettings.Save();
            owner.room.roomSettings.filePath = path;
            SettingsBlendController.ReappendRcTint(path, rcTintLine);
            UpdateActiveFileLabel(path);

            buttonSelectedA = newSelected;
            foreach (var node in subNodes) node.Refresh();
            parentNode?.Refresh();
            return;
        }

        // ── Botones de modo ──────────────────────────────────────────────
        if (sender.IDstring.StartsWith("RC_Mode_"))
        {
            if (sender is ModeButton modeBtn)
            {
                string roomName = owner.room?.abstractRoom?.name;

                // Actualizar visual: desactivar todos, activar el pulsado
                foreach (var node in subNodes)
                {
                    if (node is ModeButton mb)
                        mb.SetActive(mb == modeBtn);
                }

                // Escribir el nuevo modo en blend_settings.txt
                BlendSettingsWriter.SetMode(roomName, modeBtn.Mode);

                // Mostrar/ocultar slider B según modo — el resto queda fijo
                var sliderB = subNodes.FirstOrDefault(n => n.IDstring == "RC_BlendSliderB")
                              as BlendSlider;
                if (sliderB != null)
                    sliderB.SetLocked(modeBtn.Mode != BlendMode.Loop);

                // Limpiar el blend activo antes de cambiar de modo.
                // Sin esto, el controlador queda con texturas mezcladas y un
                // _pendingOrigin sucio, contaminando el primer Attach del nuevo modo.
                SettingsBlendController.ResetFull();

                // Reiniciar el reloj con el nuevo modo
                if (BlendClock.IsRunning)
                    BlendClock.Stop();

                if (modeBtn.Mode == BlendMode.Loop)
                    BlendClock.Start(RCPanel.buttonSelectedA);

                Plugin.RSPlugin.log.LogInfo($"[RCPanel] Mode changed to {modeBtn.Mode}");
            }
            return;
        }

        // ── Botón toggle de sala ─────────────────────────────────────────
        if (sender.IDstring == "RC_ToggleRoom")
        {
            string roomName = owner.room?.abstractRoom?.name;
            bool nowRegistered = BlendSettingsWriter.ToggleRoom(roomName);

            // Actualizar visual del botón
            var btn = subNodes.FirstOrDefault(n => n.IDstring == "RC_ToggleRoom") as RoomToggleButton;
            if (btn != null) btn.SetRegistered(nowRegistered);

            Plugin.RSPlugin.log.LogInfo(
                $"[RCPanel] Room {roomName} {(nowRegistered ? "registered in" : "removed from")} blend_settings.txt");
            return;
        }

        // ── Fila B: seleccionar destino del blend ─────────────────────────
        if (sender.IDstring.StartsWith("RCB_"))
        {
            int newB = int.Parse(sender.IDstring.Split('_')[1]);
            buttonSelectedB = newB;

            // Si el slider no está en 0, activar blend inmediatamente
            if (!Mathf.Approximately(BlendSlider.BlendFactor, 0f))
            {
                ActivateBlend();
            }

            foreach (var node in subNodes) node.Refresh();
        }
    }

    // ── Sistema de blend manual con fases ───────────────────────────────
    //
    // Si el usuario elige A=1 y B=3, y existe secuencia 1:[1,2,3]:
    //   Slider 0%→50%  = fase 1→2 (T local 0→1)
    //   Slider 50%→100% = fase 2→3 (T local 0→1)
    //
    // Si no hay secuencia o B no está en ella: blend directo de 2 estados.
    //
    // Para el slider B (modo Loop): mismo sistema pero usando LaneB.

    // Fases calculadas al iniciar el blend (lista de pares estado_desde→estado_hasta)
    private System.Collections.Generic.List<(int from, int to)> _blendPhases = null;
    private bool _manualLaneIsA = true;
    // Estado anclaje tras el último relevo B→A.
    // -1 = no hubo relevo aún, usar buttonSelectedA como origen normal.
    private int _relayAnchor = -1;

    /// <summary>Llamado desde slider A cuando empieza a moverse.</summary>
    public void OnSliderMoved()
    {
        BuildPhasesAndActivate(isLaneB: false);
    }

    public void OnSliderAStarted()
    {
        _manualLaneIsA = true;

        // Seguro simétrico: si B no llegó al 100%, el relevo inverso no fue orgánico.
        if (!Mathf.Approximately(BlendSlider.BlendFactorB, 1f) &&
            !Mathf.Approximately(BlendSlider.BlendFactorB, 0f))
        {
            Plugin.RSPlugin.log.LogInfo(
                $"[RCPanel] A started with B at {BlendSlider.BlendFactorB:P0} (not 100%) — resetting to clean state.");
            SettingsBlendController.Detach();
            _blendPhases  = null;
            _relayAnchor  = -1;
        }

        // Si hubo un relevo B→A, el carril A debe empezar desde el anclaje
        // (último estado de LaneB = primer estado del próximo LaneA), no desde buttonSelectedA.
        if (_relayAnchor >= 1)
        {
            var settings = BlendSettingsLoader.Active;
            if (settings != null)
            {
                // Buscar el LoopLane cuyo LaneA empieza en _relayAnchor
                foreach (var kv in settings.LoopLanes)
                {
                    var lane = kv.Value;
                    if (!lane.IsValid) continue;
                    if (lane.LaneA[0] == _relayAnchor)
                    {
                        int endA = lane.LaneA[lane.LaneA.Count - 1];
                        int savedA = buttonSelectedA;
                        int savedB = buttonSelectedB;
                        buttonSelectedA = _relayAnchor;
                        buttonSelectedB = endA;
                        _relayAnchor = -1;  // consumido
                        BuildPhasesAndActivate(isLaneB: false);
                        buttonSelectedA = savedA;
                        buttonSelectedB = savedB;
                        return;
                    }
                }
            }
            _relayAnchor = -1;  // no encontró lane, consumir igual
        }

        BuildPhasesAndActivate(isLaneB: false);
    }

    public void OnSliderAMoved(float t)
    {
        ApplyPhasedBlend(t, isLaneB: false);
    }

    public void OnSliderBStarted()
    {
        _manualLaneIsA = false;

        // Seguro: si A no llegó al 100%, el relevo no fue orgánico.
        if (!Mathf.Approximately(BlendSlider.BlendFactor, 1f) &&
            !Mathf.Approximately(BlendSlider.BlendFactor, 0f))
        {
            Plugin.RSPlugin.log.LogInfo(
                $"[RCPanel] B started with A at {BlendSlider.BlendFactor:P0} (not 100%) — resetting to clean state.");
            SettingsBlendController.Detach();
            _blendPhases = null;
            _relayAnchor = -1;
        }

        // Para el carril B, el punto de partida es el anclaje (último de LaneA)
        var settings = BlendSettingsLoader.Active;
        if (settings != null)
        {
            var lane = settings.GetLoopLane(buttonSelectedA);
            if (lane.HasValue && lane.Value.IsValid)
            {
                int anchor = lane.Value.AnchorState;
                var laneB  = lane.Value.LaneB;
                int endB   = laneB.Count > 0 ? laneB[laneB.Count - 1] : buttonSelectedB;

                // El último estado de LaneB es el origen del próximo ciclo de A
                _relayAnchor = endB;

                int savedA = buttonSelectedA;
                int savedB = buttonSelectedB;
                buttonSelectedA = anchor;
                buttonSelectedB = endB;
                BuildPhasesAndActivate(isLaneB: true);
                buttonSelectedA = savedA;
                buttonSelectedB = savedB;
                return;
            }
        }

        _relayAnchor = -1;
        BuildPhasesAndActivate(isLaneB: true);
    }

    public void OnSliderBMoved(float t)
    {
        ApplyPhasedBlend(t, isLaneB: true);
    }

    /// <summary>Botón R — resetea todo.</summary>
    public void ResetRelaySystem()
    {
        _manualLaneIsA = true;
        _blendPhases   = null;
        _relayAnchor   = -1;
        SettingsBlendController.Detach();
        ApplyStateA();
        BlendSlider.Reset();
        foreach (var node in subNodes)
            if (node is BlendSlider bs) bs.SetDisplayT(0f);
    }

    /// <summary>
    /// Construye la lista de fases entre buttonSelectedA y buttonSelectedB,
    /// respetando los pasos intermedios definidos en [SEQUENCES].
    /// Activa el blend en la primera fase.
    /// </summary>
    private void BuildPhasesAndActivate(bool isLaneB)
    {
        string roomName = owner.room?.abstractRoom?.name;
        var settings    = BlendSettingsLoader.Active;

        // Log de diagnóstico — qué secuencia ve el sistema
        if (settings != null)
        {
            var diagSeq = isLaneB ? null : settings.GetSequenceFor(buttonSelectedA);
            string laneKeys = string.Join(",", new System.Collections.Generic.List<int>(settings.LoopLanes.Keys));
            string laneBContents = "";
            foreach (var kv in settings.LoopLanes)
                laneBContents += $" key{kv.Key}:LaneB=[{string.Join(",", kv.Value.LaneB)}]";
            Plugin.RSPlugin.log.LogInfo(
                $"[RCPanel] BuildPhases A={buttonSelectedA} B={buttonSelectedB} isLaneB={isLaneB} " +
                $"seq={( diagSeq != null ? string.Join(",", diagSeq) : "null")} " +
                $"sequences_count={settings.Sequences.Count} lanes_count={settings.LoopLanes.Count}" +
                $" laneKeys=[{laneKeys}]{laneBContents}");
        }

        _blendPhases = BuildPhaseList(buttonSelectedA, buttonSelectedB, settings, isLaneB);

        if (_blendPhases == null || _blendPhases.Count == 0) return;

        // Activar la primera fase
        ActivatePhase(_blendPhases[0], roomName);

        Plugin.RSPlugin.log.LogInfo(
            $"[RCPanel] Blend phases: {string.Join("→", _blendPhases.ConvertAll(p => p.from + ">" + p.to))}");
    }

    /// <summary>
    /// Traduce el T global del slider (0-1) a la fase correcta y empuja el T local.
    /// </summary>
    private void ApplyPhasedBlend(float globalT, bool isLaneB)
    {
        string roomName = owner.room?.abstractRoom?.name;

        if (_blendPhases == null || _blendPhases.Count == 0)
        {
            BuildPhasesAndActivate(isLaneB);
            if (_blendPhases == null) return;
        }

        int   phaseCount = _blendPhases.Count;
        float phaseSize  = 1f / phaseCount;

        // Determinar qué fase corresponde al T actual
        int   phaseIdx   = Mathf.Min(Mathf.FloorToInt(globalT / phaseSize), phaseCount - 1);
        float localT     = Mathf.Clamp01((globalT - phaseIdx * phaseSize) / phaseSize);

        var phase = _blendPhases[phaseIdx];

        // Si cambiamos de fase, re-attachar el controlador con los estados nuevos
        if (!SettingsBlendController.IsActive ||
            SettingsBlendController.CurrentPathA != ReadStateReadFiles.GetRainStateSettingsFile(roomName, phase.from) ||
            SettingsBlendController.CurrentPathB != ReadStateReadFiles.GetRainStateSettingsFile(roomName, phase.to))
        {
            ActivatePhase(phase, roomName);
        }

        SettingsBlendController.SetExternalT(localT);
    }

    private void ActivatePhase((int from, int to) phase, string roomName)
    {
        string pathA = ReadStateReadFiles.GetRainStateSettingsFile(roomName, phase.from);
        string pathB = ReadStateReadFiles.GetRainStateSettingsFile(roomName, phase.to);
        if (pathA != null && pathB != null && phase.from != phase.to)
        {
            // Limpiar _pendingOrigin antes de cada attach manual para que
            // ConsumePendingOrigin use pathA (el origen real de la fase),
            // no un snapB de una fase anterior que quedó en el buffer.
            // Sin esto, retroceder el slider contamina las texturas con el
            // estado que quedó de la fase forward anterior.
            SettingsBlendController.ClearPendingOrigin();
            SettingsBlendController.AttachWithExternalT(owner.room, pathA, pathB);
        }
    }

    /// <summary>
    /// Construye la lista de pares de fases entre stateA y stateB.
    /// Si stateB aparece en la secuencia de stateA, inserta los pasos intermedios.
    /// Si no, devuelve una sola fase directa.
    /// </summary>
    private static System.Collections.Generic.List<(int from, int to)> BuildPhaseList(
        int stateA, int stateB, BlendSettings settings, bool isLaneB)
    {
        var phases = new System.Collections.Generic.List<(int, int)>();
        if (stateA == stateB) return phases;

        // Obtener la secuencia relevante
        System.Collections.Generic.List<int> seq = null;
        if (settings != null)
        {
            if (isLaneB)
            {
                // Para el carril B, buscar el LoopLane que contenga stateA en su LaneB.
                // No podemos usar GetLoopLane(stateA) porque el índice del diccionario
                // es el estado inicial del carril A, no del B.
                foreach (var kv in settings.LoopLanes)
                {
                    var lane = kv.Value;
                    if (lane.IsValid && lane.LaneB.Contains(stateA))
                    {
                        seq = lane.LaneB;
                        break;
                    }
                }
            }
            else
            {
                seq = settings.GetSequenceFor(stateA);
            }
        }

        // Buscar stateA y stateB en la secuencia
        if (seq != null && seq.Count >= 2)
        {
            int idxA = seq.IndexOf(stateA);
            int idxB = seq.IndexOf(stateB);

            if (idxA >= 0 && idxB > idxA)
            {
                // Hay pasos intermedios definidos — construir fases consecutivas
                for (int i = idxA; i < idxB; i++)
                    phases.Add((seq[i], seq[i + 1]));
                return phases;
            }
        }

        // Sin secuencia o B no está después de A en ella: blend directo
        phases.Add((stateA, stateB));
        return phases;
    }

    private void ActivateBlend()
    {
        BuildPhasesAndActivate(isLaneB: false);
    }

    private void ReorganizeButtons(bool newButton = false)
    {
        var buttonsA   = subNodes.Where(n => n.IDstring.StartsWith("RCA_")).ToList();
        var buttonsB   = subNodes.Where(n => n.IDstring.StartsWith("RCB_")).ToList();
        var plusButton = subNodes.FirstOrDefault(n => n.IDstring == "RC_Plus");

        int buttonsPerRow = (int)((this.size.x - 2 * MARGIN + BUTTON_SPACING)
                                  / (BUTTON_WIDTH + BUTTON_SPACING));
        buttonsPerRow = Math.Max(1, buttonsPerRow);

        // Posicionar fila A
        for (int i = 0; i < buttonsA.Count; i++)
        {
            int col = i % buttonsPerRow;
            int row = i / buttonsPerRow;
            (buttonsA[i] as PositionedDevUINode).Move(new Vector2(
                MARGIN + col * (BUTTON_WIDTH + BUTTON_SPACING),
                ROW_A_Y - row * (BUTTON_WIDTH + BUTTON_SPACING)));

            if (i + 1 == buttonSelectedA)
                (buttonsA[i] as SelectButton).Select();
        }

        // Posicionar fila B
        for (int i = 0; i < buttonsB.Count; i++)
        {
            int col = i % buttonsPerRow;
            int row = i / buttonsPerRow;
            (buttonsB[i] as PositionedDevUINode).Move(new Vector2(
                MARGIN + col * (BUTTON_WIDTH + BUTTON_SPACING),
                ROW_B_Y - row * (BUTTON_WIDTH + BUTTON_SPACING)));

            if (i + 1 == buttonSelectedB)
                (buttonsB[i] as SelectButton).Select();
        }

        // Posicionar botón +
        if (plusButton != null)
        {
            int totalA = buttonsA.Count;
            int col = totalA % buttonsPerRow;
            int row = totalA / buttonsPerRow;
            (plusButton as PositionedDevUINode).Move(new Vector2(
                MARGIN + col * (BUTTON_WIDTH + BUTTON_SPACING),
                ROW_A_Y - row * (BUTTON_WIDTH + BUTTON_SPACING)));
        }
    }

    private void UpdateActiveFileLabel(string path)
    {
        var label = subNodes.FirstOrDefault(n => n.IDstring == "RC_ActiveFile") as DevUILabel;
        if (label != null)
            label.Text = System.IO.Path.GetFileName(path ?? "");
    }

    // Aplica el estado A visualmente — usado por el botón R del slider
    public void ApplyStateA()
    {
        string path = ReadStateReadFiles.GetRainStateSettingsFile(
            owner.room?.abstractRoom?.name, buttonSelectedA);
        if (path == null) return;

        owner.room.roomSettings.filePath = path;
        owner.room.roomSettings.Load((SlugcatStats.Timeline)null);
        owner.room.game.cameras[0].ApplyEffectColorsToAllPaletteTextures(
            base.RoomSettings.EffectColorA, base.RoomSettings.EffectColorB);
        owner.room.game.cameras[0].ChangeMainPalette(base.RoomSettings.Palette);
        if (base.RoomSettings.fadePalette != null)
            owner.room.game.cameras[0].ChangeFadePalette(
                base.RoomSettings.fadePalette.palette,
                base.RoomSettings.fadePalette.fades[owner.room.game.cameras[0].currentCameraPosition]);
        owner.room.game.cameras[0].ApplyFade();
        RoomEffectsApplier.ApplyDecalsFromSnapshot(owner.room, path);
        RoomEffectsApplier.ApplyLightSourcesFromSnapshot(owner.room, path);
        RoomEffectsApplier.ApplyLightBeamsFromSnapshot(owner.room, path);
        Shader.SetGlobalFloat(RainWorld.ShadPropGrime, base.RoomSettings.Grime);
        UpdateActiveFileLabel(path);
    }
}
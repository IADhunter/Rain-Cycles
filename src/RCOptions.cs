using System;
using System.Collections.Generic;
using System.IO;
using Menu;
using Menu.Remix.MixedUI;
using RWCustom;
using UnityEngine;

namespace RainCycles;

public class RCOptions : OptionInterface
{
    // Valores del selector de modo (código estable).
    public const string ModeCycle     = "cycle";
    public const string ModeProcedural = "procedural";
    public const string ModeRandom    = "random";

    public readonly Configurable<string> cycleMode;
    public readonly Configurable<bool> proceduralNoCycle;
    public readonly Configurable<string> customSeed;
    public readonly Configurable<string> saveModId;

    // Pestañas del menú Remix (mismo patrón que DMSxMeadow).
    private OpTab _optionsTab;
    private OpTab _devTab;

    // Pestaña Developer: botón de mod destino elegido + lista de mods activos.
    private OpSimpleButton _saveModStatusBtn;
    private OpScrollBox _saveModScrollBox;
    private OpSimpleButton _saveModClearBtn;
    private readonly List<SaveModRow> _saveModRows = new List<SaveModRow>();

    private class SaveModRow
    {
        public ModManager.Mod Mod;
        public OpSimpleButton SelectBtn;
        public OpLabel Label;
    }

    // Pestaña Developer: creador de estados por lotes (regiones).
    private OpTextBox _regionSearchBox;
    private OpSimpleButton _regionSearchModeBtn;
    private OpScrollBox _regionScrollBox;
    private OpLabel _batchStatusLabel;
    private bool _searchByName;
    private readonly Configurable<string> _regionSearch = new Configurable<string>("");
    private readonly List<(string code, string name)> _allRegions = new List<(string, string)>();
    private readonly List<UIelement> _regionRowItems = new List<UIelement>();

    // Pestaña Developer: búsqueda de mods para destino de guardado.
    private OpTextBox _modSearchBox;
    private OpSimpleButton _modSearchModeBtn;
    private OpLabel _modSearchStatusLabel;
    private bool _modSearchByName;
    private readonly Configurable<string> _modSearch = new Configurable<string>("");
    private readonly List<SaveModRow> _allMods = new List<SaveModRow>();
    private readonly List<UIelement> _modRowItems = new List<UIelement>();

    // Elementos de UI que se actualizan dinámicamente (pestaña Options).
    private OpSimpleButton _cycleBtn;
    private OpSimpleButton _proceduralBtn;
    private OpSimpleButton _randomBtn;
    private OpCheckBox _noCycleCheck;
    private OpLabel _noCycleCheckLabel;
    private OpTextBox _seedBox;

    // Recuadro negro de información (estilo tooltip con fade) + alpha de fundido.
    private OpRect _infoRect;
    private OpLabel _infoLabel;
    private float _infoAlpha;

    public RCOptions()
    {
        cycleMode = this.config.Bind(
            "cycleMode",
            ModeCycle,
            new ConfigurableInfo(
                "How the day-time setting is chosen for each cycle.",
                null, "",
                "Cycle Mode"));

        proceduralNoCycle = this.config.Bind(
            "proceduralNoCycle",
            false,
            new ConfigurableInfo(
                "Procedural mode ignores the cycle and anchors the chosen setting to the current cycle in a file.",
                null, "",
                "Don't Obey the Cycle"));

        customSeed = this.config.Bind(
            "customSeed",
            "",
            new ConfigurableInfo(
                "Optional custom seed for Procedural and Random modes. Empty = built-in default.",
                null, "",
                "Custom Seed"));

        saveModId = this.config.Bind(
            "saveModId",
            "",
            new ConfigurableInfo(
                "ID of the active mod where generated settings_N and blend_settings files are saved. Empty = default (write to the region's own mod, or vanilla).",
                null, "",
                "Destination Mod"));
    }

    public override void Initialize()
    {
        base.Initialize();

        // ============================================================
        // TABS: "Options" (todo lo actual) y "Developer" (en desarrollo).
        // ============================================================
        // Claves = claves vanilla para no sobreescribir ninguna entrada global:
// "OPTIONS" viene traducida oficialmente en todos los idiomas (misma
// mayuscula que el boton del menu principal). "Developer" no existe en
// vanilla, asi que solo requiere entrada propia en strings.txt.
// TabLabel() solo transforma la MAYUSCULA de nuestra etiqueta (title-case),
// sin tocar el diccionario global.
        _optionsTab = new OpTab(this, TabLabel(Tr("OPTIONS")));
        _devTab = new OpTab(this, TabLabel(Tr("Developer")));
        Tabs = new[] { _optionsTab, _devTab };

        // Botón de pestaña en rojo, como la pestaña "Cheats" de Remix.
        _devTab.colorButton = new Color(0.85f, 0.35f, 0.4f);

        // ============================================================
        // LOGO DE TÍTULO (patrón OpImage desde PNG del mod).
        // Esperado en RainCycles\ui\title.png
        // ============================================================
        OpImage titleLogo = LoadTitleLogo();
        if (titleLogo != null)
        {
            _optionsTab.AddItems(titleLogo);
        }

        // ============================================================
        // SECCIÓN - MODE - (recuadro único que incluye la seed).
        // El recuadro crece 70px hacia abajo (y=65, alto 385) para dejar
        // sitio al recuadro negro de información inferior.
        // ============================================================
        float modeBoxY = 119f; // bajado 16px (antes 135)
        const float MODE_BOX_X = 20f;
        var modeRect = new OpRect(new Vector2(MODE_BOX_X, 49f), new Vector2(560f, 385f));
        modeRect.colorFill = new Color(0.04f, 0.04f, 0.06f);

        // Título centrado CORRECTAMENTE: se pasa el ancho del recuadro como
        // "size" para que OpLabel centre en 20 + 560/2 = 300 (antes, con size
        // vacío se clampeaba a 20px y el texto quedaba 10px a la derecha).
        var modeTitle = new OpLabel(new Vector2(MODE_BOX_X, modeBoxY + 275f), new Vector2(560f, 20f), Tr("- MODE -"), FLabelAlignment.Center, true);
        modeTitle.color = new Color(0.8f, 0.9f, 1f);

        _cycleBtn     = MakeModeButton(new Vector2(MODE_BOX_X + 80f,  modeBoxY + 215f), "CYCLE",     ModeCycle);
        _proceduralBtn = MakeModeButton(new Vector2(MODE_BOX_X + 220f, modeBoxY + 215f), "PROCEDURAL", ModeProcedural);
        _randomBtn    = MakeModeButton(new Vector2(MODE_BOX_X + 360f, modeBoxY + 215f), "RANDOM",     ModeRandom);

        // Check "No obedecer al ciclo" (solo relevante en Procedural).
        _noCycleCheck = new OpCheckBox(proceduralNoCycle, new Vector2(MODE_BOX_X + 20f, modeBoxY + 165f));
        _noCycleCheckLabel = new OpLabel(MODE_BOX_X + 48f, modeBoxY + 167f, Tr("Ignore the Cycle"), false);
        _noCycleCheckLabel.color = new Color(0.8f, 0.8f, 0.8f);

        // Seed: recuadro subido 55px (antes modeBoxY+60 -> modeBoxY+115).
        // Sin título superior; en su lugar, etiqueta "Seed" a la DERECHA del
        // recuadro, (no en mayusculas), alineada verticalmente con él.
        _seedBox = new OpTextBox(customSeed, new Vector2(MODE_BOX_X + 20f, modeBoxY + 115f), 200f);
        _seedBox.description = Tr("Write a number");

        var seedLabel = new OpLabel(MODE_BOX_X + 228f, modeBoxY + 117f, "Seed", false);
        seedLabel.color = new Color(0.8f, 0.8f, 0.8f);

        // ============================================================
        // RECUADRO NEGRO DE INFORMACIÓN (estilo tooltip con fade).
        // Muestra una breve descripcion al pasar el cursor sobre los
        // controles. Se funde (alpha) segun "MouseOver" en Update().
        // ============================================================
        _infoRect = new OpRect(new Vector2(MODE_BOX_X + 10f, 74f), new Vector2(540f, 130f));
        _infoRect.colorFill = new Color(0f, 0f, 0f, 1f);
        _infoRect.colorEdge = new Color(0.5f, 0.5f, 0.5f);

        _infoLabel = new OpLabel(new Vector2(MODE_BOX_X + 24f, 80f), new Vector2(490f, 116f), "", FLabelAlignment.Left, false);
        _infoLabel.color = new Color(0.85f, 0.9f, 1f);
        _infoLabel.autoWrap = true;
        _infoLabel.verticalAlignment = OpLabel.LabelVAlignment.Top;

        _optionsTab.AddItems(modeRect, modeTitle,
            _cycleBtn, _proceduralBtn, _randomBtn,
            _noCycleCheck, _noCycleCheckLabel,
            seedLabel, _seedBox,
            _infoRect, _infoLabel);

        // ============================================================
        // PESTAÑA DEVELOPER - dos columnas: Mods (izq) / Regiones (der).
        // Área útil ~560px (x=10..570), dos columnas de 270px con gap 15px.
        // TODO bajado 10px global.
        // ============================================================

        // Título global + botones de estado destino (centrado, arriba).
        float devTitleY = 569f; // sin cambios
        var devTitle = new OpLabel(new Vector2(300f, devTitleY), new Vector2(), Tr("SAVE STATES TO"), FLabelAlignment.Center, true);
        devTitle.color = new Color(0.5f, 0.5f, 0.5f);

        _saveModStatusBtn = new OpSimpleButton(new Vector2(85f, 503f), new Vector2(360f, 36f), ""); // -16px
        _saveModStatusBtn.colorEdge = new Color(0.6f, 0.6f, 0.6f);

        _saveModClearBtn = new OpSimpleButton(new Vector2(460f, 503f), new Vector2(80f, 36f), "X"); // -16px
        _saveModClearBtn.colorEdge = new Color(0.85f, 0.35f, 0.4f);
        _saveModClearBtn.OnClick += _ =>
        {
            saveModId.Value = "";
            RefreshDevUi();
            SaveConfigNow();
        };

        _devTab.AddItems(devTitle, _saveModStatusBtn, _saveModClearBtn);

        // ----- COLUMNA IZQUIERDA: DESTINATION MOD -----
        const float COL_LEFT_X = 10f;
        const float COL_RIGHT_X = 320f;
        const float COL_W = 270f;
        const float SEC_TITLE_Y = 448f; // -16px (era 464)
        const float SEARCH_Y = 413f;    // -16px (era 429)
        const float SEARCH_W = 160f;
        const float TOGGLE_W = 100f;
        const float STATUS_Y = 383f;    // -16px (era 399)
        const float SCROLL_Y = 28f;     // -16px (era 44)
        const float SCROLL_H = 380f;
        const float SCROLL_CONTENT_H = 400f;

        // Título "DESTINATION MOD"
        var modTitle = new OpLabel(new Vector2(COL_LEFT_X + COL_W / 2f, SEC_TITLE_Y), new Vector2(), Tr("DESTINATION MOD"), FLabelAlignment.Center, true);
        modTitle.color = new Color(0.5f, 0.5f, 0.5f);

        // Búsqueda mods
        _modSearchBox = new OpTextBox(_modSearch, new Vector2(COL_LEFT_X, SEARCH_Y), SEARCH_W);
        _modSearchBox.allowSpace = true;
        _modSearchBox.description = Tr("Search mods by ID or by name.");
        _modSearchBox.OnChange += () => RebuildModList();

        _modSearchModeBtn = new OpSimpleButton(new Vector2(COL_LEFT_X + SEARCH_W + 5f, SEARCH_Y), new Vector2(TOGGLE_W, 24f), Tr("ID"));
        _modSearchModeBtn.description = Tr("Switch search between mod ID and mod name.");
        _modSearchModeBtn.OnClick += _ => ToggleModSearchMode();

        _modSearchStatusLabel = new OpLabel(COL_LEFT_X, STATUS_Y, "", false);
        _modSearchStatusLabel.color = new Color(0.65f, 0.65f, 0.65f);

        _devTab.AddItems(modTitle, _modSearchBox, _modSearchModeBtn, _modSearchStatusLabel);

        // ScrollBox mods
        _saveModScrollBox = new OpScrollBox(
            new Vector2(COL_LEFT_X, SCROLL_Y),
            new Vector2(COL_W, SCROLL_H),
            SCROLL_CONTENT_H,
            false, true, true)
        {
            colorEdge = MenuColorEffect.rgbMediumGrey,
            colorFill = MenuColorEffect.rgbBlack,
            fillAlpha = 0.3f
        };
        _devTab.AddItems(_saveModScrollBox);

        // ----- COLUMNA DERECHA: GENERATE STATES (regiones) -----
        var regionTitle = new OpLabel(new Vector2(COL_RIGHT_X + COL_W / 2f, SEC_TITLE_Y), new Vector2(), Tr("GENERATE STATES"), FLabelAlignment.Center, true);
        regionTitle.color = new Color(0.5f, 0.5f, 0.5f);

        _regionSearchBox = new OpTextBox(_regionSearch, new Vector2(COL_RIGHT_X, SEARCH_Y), SEARCH_W);
        _regionSearchBox.allowSpace = true;
        _regionSearchBox.description = Tr("Search regions by ID or by name.");
        _regionSearchBox.OnChange += () => RebuildRegionList();

        _regionSearchModeBtn = new OpSimpleButton(new Vector2(COL_RIGHT_X + SEARCH_W + 5f, SEARCH_Y), new Vector2(TOGGLE_W, 24f), Tr("ID"));
        _regionSearchModeBtn.description = Tr("Switch search between region ID and region name.");
        _regionSearchModeBtn.OnClick += _ => ToggleSearchMode();

        _batchStatusLabel = new OpLabel(COL_RIGHT_X, STATUS_Y, "", false);
        _batchStatusLabel.color = new Color(0.65f, 0.65f, 0.65f);

        _devTab.AddItems(regionTitle, _regionSearchBox, _regionSearchModeBtn, _batchStatusLabel);

        _regionScrollBox = new OpScrollBox(
            new Vector2(COL_RIGHT_X, SCROLL_Y),
            new Vector2(COL_W, SCROLL_H),
            SCROLL_CONTENT_H,
            false, true, true)
        {
            colorEdge = MenuColorEffect.rgbMediumGrey,
            colorFill = MenuColorEffect.rgbBlack,
            fillAlpha = 0.3f
        };
        _devTab.AddItems(_regionScrollBox);

        // Cargar datos
        BuildSaveModList();
        RefreshDevUi();
        LoadAllRegions();
        RebuildRegionList();
        RefreshUi();

        // Remix solo escribe el .txt de config al CERRAR el menú del mod.
        // Forzamos una escritura al abrirlo para que el archivo siempre
        // exista (Nombre: ModConfigs/{mod.id}.txt).
        SaveConfigNow();
    }

    // ============================================================
    // BUILD SAVE MOD LIST - una fila por mod activo (puebla _allMods y delega en RebuildModList)
    // ============================================================
    private void BuildSaveModList()
    {
        _saveModRows.Clear();
        _allMods.Clear();

        for (int i = 0; i < ModManager.ActiveMods.Count; i++)
        {
            ModManager.Mod mod = ModManager.ActiveMods[i];
            var row = new SaveModRow { Mod = mod };
            _saveModRows.Add(row);
            _allMods.Add(row);
        }

        RebuildModList();
    }

    // ============================================================
    // REFRESH DEV UI - resalta selección y actualiza el botón
    // ============================================================
    private void RefreshDevUi()
    {
        string selectedId = saveModId.Value ?? "";
        ModManager.Mod selected = FindActiveMod(selectedId);

        if (selected == null)
        {
            _saveModStatusBtn.text = Tr("None (default: region's own mod / vanilla)");
            _saveModStatusBtn.colorEdge = new Color(0.6f, 0.6f, 0.6f);
        }
        else
        {
            _saveModStatusBtn.text = $"{selected.name}";
            _saveModStatusBtn.colorEdge = new Color(0.15f, 0.85f, 0.15f);
        }

        foreach (var row in _saveModRows)
        {
            bool isSelected = row.Mod.id == selectedId;
            row.SelectBtn.colorEdge = isSelected
                ? new Color(0.15f, 0.85f, 0.15f)
                : new Color(0.6f, 0.6f, 0.6f);
        }
    }

    private static ModManager.Mod FindActiveMod(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var mod in ModManager.ActiveMods)
            if (mod.id == id)
                return mod;
        return null;
    }

    // ============================================================
    // CREADOR POR LOTES - carga la lista de regiones (id + nombre).
    // Mismo origen que OpResourceSelector.SpecialEnum.Regions:
    // World/regions.txt resuelto por AssetManager (vanilla + mods).
    // ============================================================
    private void LoadAllRegions()
    {
        _allRegions.Clear();
        try
        {
            string path = AssetManager.ResolveFilePath("World" + Path.DirectorySeparatorChar + "regions.txt");
            if (!File.Exists(path))
            {
                RSPlugin.log.LogWarning($"[RC] regions.txt no encontrado: {path}");
                return;
            }

            foreach (string line in File.ReadAllLines(path))
            {
                string code = line.Trim();
                if (code.Length == 0) continue;
                string name = Tr(Region.GetRegionFullName(code, SlugcatStats.Name.White));
                _allRegions.Add((code, name));
            }
            _allRegions.Sort((a, b) => string.CompareOrdinal(a.code, b.code));
            RSPlugin.log.LogInfo($"[RC] Creador por lotes: {_allRegions.Count} regiones cargadas.");
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[RC] No se pudo cargar regions.txt: {ex.Message}");
        }
    }

    // ============================================================
    // CREADOR POR LOTES - reconstruye las filas del scrollbox según
    // la búsqueda actual (id o nombre). Patrón idéntico a
    // BuildSaveModList (contentHeight -> filas -> SetContentSize).
    // ============================================================
    private void RebuildRegionList()
    {
        if (_regionRowItems.Count > 0)
        {
            var old = _regionRowItems.ToArray();
            OpScrollBox.RemoveItemsFromScrollBox(old);
            _devTab.RemoveItems(old);
            _regionRowItems.Clear();
        }

        string query = (_regionSearchBox.value ?? "").Trim().ToLowerInvariant();

        var filtered = new List<(string code, string name)>();
        foreach (var region in _allRegions)
        {
            bool match = string.IsNullOrEmpty(query)
                || (_searchByName
                    ? region.name.ToLowerInvariant().Contains(query)
                    : region.code.ToLowerInvariant().Contains(query));
            if (match) filtered.Add(region);
        }

        float contentHeight = Math.Max(filtered.Count * 26f + 20f, _regionScrollBox.size.y);

        // Fijamos el tamaño de contenido ANTES de añadir filas (la lista está
        // vacía aquí, así SetContentSize no desplaza nada) para que las
        // posiciones calculadas abajo coincidan 1:1 con las coordenadas.
        _regionScrollBox.SetContentSize(contentHeight, true);
        _regionScrollBox.ScrollToTop(true);

        float y = contentHeight - 26f - 10f;
        foreach (var region in filtered)
        {
            float rowY = y;

            var label = new OpLabel(10f, rowY, $"{region.code}    {region.name}", false);
            label.color = new Color(0.8f, 0.8f, 0.8f);

            var genBtn = new OpSimpleButton(new Vector2(222f, rowY - 3f), new Vector2(22f, 22f), "");
            genBtn.description = Tr("Generate states for this region");
            genBtn.colorEdge = new Color(0.6f, 0.6f, 0.6f);
            string code = region.code;
            genBtn.OnClick += _ => GenerateRegionStates(code);

            _regionScrollBox.AddItems(label, genBtn);
            _regionRowItems.Add(label);
            _regionRowItems.Add(genBtn);

            y -= 26f;
        }

        _regionScrollBox.MarkDirty();
    }

    // ============================================================
    // CREADOR POR LOTES - alterna búsqueda por ID / por NOMBRE.
    // ============================================================
    private void ToggleSearchMode()
    {
        _searchByName = !_searchByName;
        _regionSearchModeBtn.text = Tr(_searchByName ? "NAME" : "ID");
        RebuildRegionList();
    }

    // ============================================================
    // BÚSQUEDA DE MODS - para destino de guardado.
    // ============================================================
    private void ToggleModSearchMode()
    {
        _modSearchByName = !_modSearchByName;
        _modSearchModeBtn.text = Tr(_modSearchByName ? "NAME" : "ID");
        RebuildModList();
    }

    private void RebuildModList()
    {
        if (_modRowItems.Count > 0)
        {
            var old = _modRowItems.ToArray();
            OpScrollBox.RemoveItemsFromScrollBox(old);
            _devTab.RemoveItems(old);
            _modRowItems.Clear();
        }

        string query = (_modSearchBox.value ?? "").Trim().ToLowerInvariant();

        var filtered = new List<SaveModRow>();
        foreach (var row in _allMods)
        {
            bool match = string.IsNullOrEmpty(query)
                || (_modSearchByName
                    ? row.Mod.name.ToLowerInvariant().Contains(query)
                    : row.Mod.id.ToLowerInvariant().Contains(query));
            if (match) filtered.Add(row);
        }

        float contentHeight = Math.Max(filtered.Count * 26f + 20f, _saveModScrollBox.size.y);

        _saveModScrollBox.SetContentSize(contentHeight, true);
        _saveModScrollBox.ScrollToTop(true);

        float y = contentHeight - 26f - 10f;
        foreach (var row in filtered)
        {
            float rowY = y;

            var label = new OpLabel(10f, rowY, $"{row.Mod.name}", false);
            label.color = new Color(0.8f, 0.8f, 0.8f);

            var selectBtn = new OpSimpleButton(new Vector2(420f, rowY - 3f), new Vector2(80f, 22f), Tr("Select"));
            selectBtn.colorEdge = new Color(0.6f, 0.6f, 0.6f);
            string modId = row.Mod.id;
            selectBtn.OnClick += _ =>
            {
                saveModId.Value = modId;
                RefreshDevUi();
                SaveConfigNow();
            };

            // Mantener referencias para RefreshDevUi
            row.Label = label;
            row.SelectBtn = selectBtn;

            _saveModScrollBox.AddItems(label, selectBtn);
            _modRowItems.Add(label);
            _modRowItems.Add(selectBtn);

            y -= 26f;
        }

        _saveModScrollBox.MarkDirty();
    }

    // ============================================================
    // CREADOR POR LOTES - genera los 4 estados de una región.
    // Copia TODO archivo que contenga "settings" en su nombre,
    // 4 veces, enumerado _1.._4, sobrescribiendo (overwrite:true).
    // Destino: mod elegido (SaveModResolver) o fallback por región.
    // ============================================================
    private void GenerateRegionStates(string regionCode)
    {
        try
        {
            string upper = regionCode.ToUpperInvariant();
            string srcDir = FindRegionRoomsFolder(upper);
            if (srcDir == null)
            {
                _batchStatusLabel.text = Tr("Rooms folder not found for region ") + upper;
                return;
            }

            string destDir = SaveModResolver.DirectoryForRegion(upper) ?? DefaultRegionDirectory(upper);
            Directory.CreateDirectory(destDir);

            int copied = 0;
            foreach (string file in Directory.GetFiles(srcDir, "*.txt"))
            {
                string fileName = Path.GetFileName(file);
                if (fileName.IndexOf("settings", StringComparison.OrdinalIgnoreCase) < 0) continue;

                string baseName = Path.GetFileNameWithoutExtension(file);
                for (int n = 1; n <= 4; n++)
                {
                    string dest = Path.Combine(destDir, $"{baseName}_{n}.txt");
                    File.Copy(file, dest, true);
                    copied++;
                }
            }

            _batchStatusLabel.text = Tr("Generated ") + copied + Tr(" state files for ") + upper;
            RSPlugin.log.LogInfo($"[RC] Creador por lotes {upper}: {copied} archivos -> {destDir}");
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[RC] Falló la generación por lotes de {regionCode}: {ex.Message}");
            _batchStatusLabel.text = Tr("Error: ") + ex.Message;
        }
    }

    // ============================================================
    // CREADOR POR LOTES - carpeta World/{REGION}-Rooms de la región.
    // Prioridad idéntica a AssetManager: mods activos en orden
    // inverso y al final la instalación base.
    // ============================================================
    private static string FindRegionRoomsFolder(string regionCode)
    {
        string upper = regionCode.ToUpperInvariant();
        string rel = Path.Combine("World", upper + "-Rooms");

        for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
        {
            string candidate = Path.Combine(ModManager.ActiveMods[i].path, rel);
            if (Directory.Exists(candidate)) return candidate;
        }

        string vanilla = Path.Combine(Application.streamingAssetsPath, rel);
        return Directory.Exists(vanilla) ? vanilla : null;
    }

    // ============================================================
    // CREADOR POR LOTES - destino por defecto (sin mod elegido).
    // Replica StateFileResolver.BuildDirectoryPath por región:
    // primer mod activo que tenga la carpeta, si no la base.
    // ============================================================
    private static string DefaultRegionDirectory(string regionCode)
    {
        string upper = regionCode.ToUpperInvariant();
        string rel = Path.Combine("World", upper + "-Rooms", "RainCycles");

        for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
        {
            string candidate = Path.Combine(ModManager.ActiveMods[i].path, rel);
            if (Directory.Exists(candidate)) return candidate;
        }

        return Path.Combine(Application.streamingAssetsPath, rel);
    }

    // ============================================================
    // SAVE CONFIG - fuerza la escritura del .txt de Remix
    // ============================================================
    // Remix guarda ModConfigs/{mod.id}.txt al CERRAR el menú del mod.
    // Este helper garantiza la persistencia inmediata en cada cambio.
    private void SaveConfigNow()
    {
        try { MachineConnector.SaveConfig(this); }
        catch (Exception ex) { UnityEngine.Debug.LogWarning($"[RC] No se pudo guardar config: {ex.Message}"); }
    }

    // Ruta del logo dentro del mod compilado (RainCycles/ui/).
    private const string TitleLogoPath = "ui\\title.png";

    // ============================================================
    // LOGO DE TÍTULO - carga un PNG del mod como banner del menú Remix.
    // Mismo mecanismo (rainLogo.png):
    // ResolveFilePath -> bytes -> Texture2D.LoadImage -> OpImage anclado.
    // ============================================================
    private static OpImage LoadTitleLogo()
    {
        try
        {
            string path = AssetManager.ResolveFilePath(TitleLogoPath);
            if (!File.Exists(path))
            {
                UnityEngine.Debug.LogWarning($"[RC] Logo de título no encontrado: {path}");
                return null;
            }

            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(0, 0);
            texture.filterMode = FilterMode.Point;
            texture.LoadImage(bytes);
            UnityEngine.Debug.Log($"[RC] Logo de título cargado: {texture.width}x{texture.height}");

            var logo = new OpImage(new Vector2(300f, 479f), texture);
            logo.anchor = new Vector2(0.5f, 0f);

            // Shader "MenuText": mismo destello que el título "Rain World" del menú
            // principal (barrido diagonal animado por el global _RAIN, que ProcessManager
            // actualiza cada frame). Se limita a lo visible: el shader usa el canal alfa
            // del PNG y reemplaza su color por la rampa textGradient.
            if (Custom.rainWorld != null
                && Custom.rainWorld.Shaders.TryGetValue("MenuText", out var menuTextShader))
            {
                logo.sprite.shader = menuTextShader;
            }
            else
            {
                UnityEngine.Debug.LogWarning("[RC] Shader MenuText no disponible; logo sin brillo.");
            }
            return logo;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[RC] Error cargando logo de título: {ex.Message}");
            return null;
        }
    }

    private OpSimpleButton MakeModeButton(Vector2 pos, string text, string mode)
    {
        var btn = new OpSimpleButton(pos, new Vector2(120f, 40f), text);
        btn.OnClick += _ =>
        {
            cycleMode.Value = mode;
            RefreshUi();
            SaveConfigNow();
        };
        return btn;
    }

    private void RefreshUi()
    {
        string mode = cycleMode.Value;

        // Resaltar el botón activo.
        _cycleBtn.colorEdge     = ActiveButtonColor(mode == ModeCycle);
        _proceduralBtn.colorEdge = ActiveButtonColor(mode == ModeProcedural);
        _randomBtn.colorEdge    = ActiveButtonColor(mode == ModeRandom);

        // El check solo aplica en Procedural.
        bool isProcedural = mode == ModeProcedural;
        _noCycleCheck.greyedOut = !isProcedural;
        _noCycleCheckLabel.color = isProcedural
            ? new Color(0.8f, 0.8f, 0.8f)
            : new Color(0.4f, 0.4f, 0.4f);

        bool usesSeed = mode != ModeCycle;
        _seedBox.greyedOut = !usesSeed;
    }

    private static Color ActiveButtonColor(bool active)
        => active ? new Color(0.15f, 0.85f, 0.15f) : new Color(0.6f, 0.6f, 0.6f);

    // ============================================================
    // UPDATE - recuadro negro de información (estilo tooltip con fade).
    // Al pasar el cursor sobre un control, se muestra un texto y la
    // caja se funde a la vista; sin hover, se desvanece.
    // ============================================================
    public override void Update()
    {
        base.Update();
        if (_infoLabel == null) return;

        // Determinar bajo qué control está el cursor y su descripcion
        // provisional. De momento solo "Descripción de <X>".
        string txt = "";
        if (_cycleBtn != null && _cycleBtn.MouseOver)
        {
            txt = Tr("Cycle Mode makes the states advance in a fixed, sequential order, following your save's cycle number. A new story starts at state 1, and if you are mid‑story it simply continues from the cycle you are on. The custom seed has no effect on this mode.");
        }
        else if (_proceduralBtn != null && _proceduralBtn.MouseOver)
        {
            txt = Tr("Procedural Mode changes the fixed sequence into something more dynamic, where each state is chosen by a calculation based on your cycle number and the seed. That means leaving and re‑entering the same cycle keeps that state, and advancing to another cycle produces a pseudo‑random state that will also be locked to that next cycle.");
        }
        else if (_randomBtn != null && _randomBtn.MouseOver)
        {
            txt = Tr("Random Mode is true randomness, with no rules or coherence. The state is picked unpredictably on each occasion, ignoring the cycle, your progress and previous plays.");
        }
        else if (_noCycleCheck != null && _noCycleCheck.MouseOver)
        {
            txt = Tr("This option stops the state from following the cycle-based calculation. The calculation becomes more flexible and less predictable, but coherence is kept, meaning that if a cycle starts with a specific state, the game keeps that same state until you advance to the next cycle.");
        }
        else if (_seedBox != null && _seedBox.MouseOver)
        {
            txt = Tr("Lets you customize the seed used for the calculation that picks the state.");
        }

        // Fundido de entrada/salida.
        if (txt.Length > 0)
        {
            _infoLabel.text = txt;
            _infoAlpha = Mathf.Min(1f, _infoAlpha + 0.1f);
        }
        else
        {
            _infoAlpha = Mathf.Max(0f, _infoAlpha - 0.025f);
        }

        _infoRect.fillAlpha = 0.85f * _infoAlpha;
        _infoRect.colorEdge = new Color(0.5f, 0.5f, 0.5f, _infoAlpha);
        _infoLabel.alpha = _infoAlpha;
    }

    // ============================================================
    // TRADUCCIÓN - la clave es el texto en INGLÉS (fallback integral:
    // si no hay archivo de idioma, Translate devuelve la clave).
    // El juego reemplaza la clave por el idioma actual desde
    // mods/<id>/Text/Text_<lang>/strings.txt (p.ej. serbe "eng", "spa").
    // ============================================================
    private static string Tr(string s)
    {
        return Custom.rainWorld?.inGameTranslator != null
            ? Custom.rainWorld.inGameTranslator.Translate(s)
            : s;
    }

    // ============================================================
    // TITLE-CASE SOLO DE LA ETIQUETA - no toca el diccionario global.
    // Si el texto traducido viene TODO en mayusculas (p.ej. "OPTIONS"
    // o "OPCIONES" de la clave vanilla), lo convierte a solo la primera
    // letra mayuscula. Si ya viene en otro caso (CJK, etc.), no lo toca.
    // ============================================================
    private static string TabLabel(string s)
    {
        if (string.IsNullOrEmpty(s) || s != s.ToUpperInvariant())
        {
            return s;
        }
        return char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
    }
}

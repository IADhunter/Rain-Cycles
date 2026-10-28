using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DevInterface;
using UnityEngine;
using RainCycles.Snapshot;

namespace FilesSetting;

// RCPanel_ViewPage (bkg) — fila de fondo por sala×estado.
public partial class RCPanel_ViewPage
{
    private const float BKG_ROW_Y = 33f;
    private const float BKG_MOD_X = 5f;
    private const float BKG_MOD_W = 50f;
    private const float BKG_SLOT_PREV_X = 61f;
    private const float BKG_SLOT_LABEL_X = 82f;
    private const float BKG_SLOT_LABEL_W = 22f;
    private const float BKG_SLOT_NEXT_X = 109f;
    private const float BKG_IMG_X = 130f;
    private const float BKG_IMG_W = 50f;

    private const float PICKER_MOD_X_SHIFT = -215f;
    private const float PICKER_IMG_X_SHIFT = -210f;
    private const float PICKER_Y_SHIFT = 172f;

    private static readonly Color BKG_COLOR_HAS_IMAGE = new Color(0.2f, 0.7f, 0.3f);
    private static readonly Color BKG_COLOR_NO_IMAGE = new Color(1f, 1f, 1f);
    private static readonly Color BKG_COLOR_HAS_FOG = new Color(0.2f, 0.5f, 0.8f);
    private static readonly Color BKG_COLOR_HAS_SUN = new Color(0.8f, 0.5f, 0.2f);
    private static readonly Color BKG_COLOR_DISABLED = new Color(0.5f, 0.5f, 0.5f);

    private Button _bkgModBtn;
    private ArrowButton _bkgSlotPrev;
    private ArrowButton _bkgSlotNext;
    private DevUILabel _bkgSlotLabel;
    private Button _bkgImgBtn;
    private int _bkgSlot;
    private string _pendingMod;
    private ModSelectPanel _bkgModPanel;
    private ImageSelectPanel _bkgImgPanel;

    // CREACIÓN
    private void CreateBkgRow()
    {
        _bkgModBtn = new Button(owner, "RC_RoomMod", this,
            new Vector2(BKG_MOD_X, BKG_ROW_Y), BKG_MOD_W, "-");
        subNodes.Add(_bkgModBtn);

        _bkgSlotPrev = new ArrowButton(owner, "RC_RoomSlot_Prev", this,
            new Vector2(BKG_SLOT_PREV_X, BKG_ROW_Y), 270f);
        subNodes.Add(_bkgSlotPrev);

        _bkgSlotLabel = new DevUILabel(owner, "RC_RoomSlot_Label", this,
            new Vector2(BKG_SLOT_LABEL_X, BKG_ROW_Y), BKG_SLOT_LABEL_W, "Sky");
        subNodes.Add(_bkgSlotLabel);

        _bkgSlotNext = new ArrowButton(owner, "RC_RoomSlot_Next", this,
            new Vector2(BKG_SLOT_NEXT_X, BKG_ROW_Y), 90f);
        subNodes.Add(_bkgSlotNext);

        _bkgImgBtn = new Button(owner, "RC_RoomImg", this,
            new Vector2(BKG_IMG_X, BKG_ROW_Y), BKG_IMG_W, "-");
        subNodes.Add(_bkgImgBtn);

        RefreshBkgRow();
    }

    // ESTADO
    private RoomSettings CurrentRs() => ParentPanel.CurrentRoom?.roomSettings;

    private BkgTag.Data CurrentTag()
    {
        var rs = CurrentRs();
        return rs == null ? default : rs.GetBkg();
    }

    private string EffectiveMod()
    {
        if (!string.IsNullOrEmpty(_pendingMod)) return _pendingMod;
        return CurrentTag().Mod;
    }

    private bool CanEditBkg()
    {
        if (!BlendClock.EditMode) return false;
        var rs = CurrentRs();
        return rs != null && rs.HasView();
    }

    private string SlotName() => _bkgSlot == 0 ? "Sky" : (_bkgSlot == 1 ? "Fog" : "Sun");

    private string SlotValue(BkgTag.Data bkg)
        => _bkgSlot == 0 ? bkg.Sky : (_bkgSlot == 1 ? bkg.Fog : bkg.Sun);

    private Color SlotColor()
        => _bkgSlot == 1 ? BKG_COLOR_HAS_FOG : (_bkgSlot == 2 ? BKG_COLOR_HAS_SUN : BKG_COLOR_HAS_IMAGE);

    private static string TruncateValue(string value)
        => value.Length <= 7 ? value : value.Substring(0, 7);

    public void RefreshBkgRow()
    {
        if (_bkgModBtn == null) return;

        var rs = CurrentRs();
        var bkg = CurrentTag();
        bool hasType = rs != null && rs.HasRcType();

        string mod = EffectiveMod();
        bool hasMod = !string.IsNullOrEmpty(mod);
        _bkgModBtn.Text = hasMod ? TruncateValue(mod) : "Mod";
        _bkgModBtn.colorA = !hasType ? BKG_COLOR_DISABLED
            : hasMod ? BKG_COLOR_HAS_IMAGE
            : BKG_COLOR_NO_IMAGE;

        bool isPsv = rs != null && rs.HasView() && rs.GetViewType() == ViewType.PSV;
        if (!isPsv) _bkgSlot = 0;
        _bkgSlotLabel.Text = SlotName();

        string img = SlotValue(bkg);
        bool hasImg = !string.IsNullOrEmpty(img);
        _bkgImgBtn.Text = hasImg ? TruncateValue(img) : "Image";
        _bkgImgBtn.colorA = !hasType ? BKG_COLOR_DISABLED
            : hasImg ? SlotColor()
            : BKG_COLOR_NO_IMAGE;
    }

    // SEÑALES
    private bool HandleBkgSignal(DevUINode sender, string message)
    {
        string id = sender.IDstring;

        if (id == "RC_RoomMod")
        {
            if (CanEditBkg()) ToggleModPanel();
            return true;
        }

        if (id == "RC_RoomSlot_Prev")
        {
            CycleBkgSlot(-1);
            return true;
        }

        if (id == "RC_RoomSlot_Next")
        {
            CycleBkgSlot(1);
            return true;
        }

        if (id == "RC_RoomImg")
        {
            if (CanEditBkg()) ToggleImagePanel();
            return true;
        }

        if (id.StartsWith("RC_ModSelect_") && _bkgModPanel != null)
        {
            OnModPicked(message);
            CloseBkgPickers();
            return true;
        }

        if (id == "RC_ModClear" && _bkgModPanel != null)
        {
            _pendingMod = null;
            CommitBkg(default);
            CloseBkgPickers();
            RefreshBkgRow();
            return true;
        }

        if (id.StartsWith("RC_ImageSelect_") && id != "RC_ImageSelect_None" && _bkgImgPanel != null)
        {
            OnImagePicked(Path.GetFileNameWithoutExtension(message));
            CloseBkgPickers();
            return true;
        }

        if (id == "RC_ImageNone" && _bkgImgPanel != null)
        {
            OnImageCleared();
            CloseBkgPickers();
            return true;
        }

        return false;
    }

    private void CycleBkgSlot(int delta)
    {
        if (!BlendClock.EditMode) return;

        var rs = CurrentRs();
        if (rs == null || !rs.HasView() || rs.GetViewType() != ViewType.PSV) return;

        _bkgSlot = (_bkgSlot + delta + 3) % 3;
        RefreshBkgRow();
    }

    // PICKERS
    private void ToggleModPanel()
    {
        if (_bkgModPanel != null)
        {
            CloseBkgPickers();
            return;
        }

        var mods = RCBkgUI.GetModsWithIllustrations();
        if (mods.Length == 0) return;

        CloseBkgPickers();

        Vector2 pos = new Vector2(_bkgModBtn.pos.x + 5f + PICKER_MOD_X_SHIFT, _bkgModBtn.pos.y - 205f + PICKER_Y_SHIFT);
        _bkgModPanel = new ModSelectPanel(owner, "RC_ModSelectPanel", this, pos, mods, EffectiveMod());
        subNodes.Add(_bkgModPanel);
        _bkgModPanel.Refresh();
    }

    private void ToggleImagePanel()
    {
        if (_bkgImgPanel != null)
        {
            CloseBkgPickers();
            return;
        }

        string mod = EffectiveMod();
        if (string.IsNullOrEmpty(mod)) return;

        var images = RCBkgUI.GetImagesForMod(mod);
        if (images.Length == 0) return;

        CloseBkgPickers();

        Vector2 pos = new Vector2(BKG_MOD_X + PICKER_IMG_X_SHIFT, BKG_ROW_Y - 205f + PICKER_Y_SHIFT);
        _bkgImgPanel = new ImageSelectPanel(owner, "RC_ImageSelectPanel", this, pos, images, SlotValue(CurrentTag()));
        subNodes.Add(_bkgImgPanel);
        _bkgImgPanel.Refresh();
    }

    private void CloseBkgPickers()
    {
        if (_bkgModPanel != null)
        {
            _bkgModPanel.ClosePanel();
            _bkgModPanel = null;
        }
        if (_bkgImgPanel != null)
        {
            _bkgImgPanel.ClosePanel();
            _bkgImgPanel = null;
        }
    }

    // EDICIÓN
    private void OnModPicked(string modName)
    {
        _pendingMod = modName;

        if (CommitBkg(new BkgTag.Data(modName, null, null, null)))
            _pendingMod = null;

        RefreshBkgRow();
    }

    private void OnImagePicked(string imageName)
    {
        string mod = EffectiveMod();
        if (string.IsNullOrEmpty(mod)) return;

        var cur = CurrentTag();
        string sky = _bkgSlot == 0 ? imageName : cur.Sky;
        string sun = _bkgSlot == 2 ? imageName : cur.Sun;
        string fog = _bkgSlot == 1 ? imageName : cur.Fog;

        if (CommitBkg(new BkgTag.Data(mod, sky, sun, fog)))
            _pendingMod = null;

        RefreshBkgRow();
    }

    private void OnImageCleared()
    {
        if (_bkgSlot == 0)
        {
            CommitBkg(default);
            _pendingMod = null;
        }
        else
        {
            var cur = CurrentTag();
            string mod = EffectiveMod();
            if (string.IsNullOrEmpty(mod) || string.IsNullOrEmpty(cur.Sky)) return;

            string sun = _bkgSlot == 2 ? null : cur.Sun;
            string fog = _bkgSlot == 1 ? null : cur.Fog;
            CommitBkg(new BkgTag.Data(mod, cur.Sky, sun, fog));
        }

        RefreshBkgRow();
    }

    private bool CommitBkg(BkgTag.Data data)
    {
        var rs = CurrentRs();
        if (rs == null || !rs.HasRcType()) return false;

        string expected = ParentPanel.ResolveSettingsFile(RCPanel.ButtonSelectedA);
        if (string.IsNullOrEmpty(expected) ||
            !string.Equals(rs.filePath, expected, StringComparison.OrdinalIgnoreCase))
        {
            RSPlugin.log.LogWarning($"[RoomPage] bkg: filePath no coincide con el estado {RCPanel.ButtonSelectedA}; no se guarda.");
            return false;
        }

        rs.SetBkg(data);

        SettingsBlendController.ApplySkyForState(RCPanel.ButtonSelectedA, ParentPanel.CurrentRoom);
        return true;
    }
}

// RCBkgUI — utilidades de picker para la fila bkg de RoomPage.
internal static class RCBkgUI
{
    public static string[] GetModsWithIllustrations()
    {
        var mods = new List<string> { RCPanel_RegionPage.DEFAULT_MOD_SENTINEL };

        foreach (var mod in ModManager.ActiveMods)
        {
            if (!Directory.Exists(Path.Combine(mod.path, "Illustrations"))) continue;
            if (File.Exists(Path.Combine(mod.path, "modinfo.json")))
                mods.Add(mod.path);
        }
        return mods.ToArray();
    }

    public static string[] GetImagesForMod(string modName)
    {
        if (string.IsNullOrEmpty(modName)) return new string[0];

        string dir;
        if (string.Equals(modName, RCPanel_RegionPage.DEFAULT_MOD_DISPLAY_NAME, StringComparison.OrdinalIgnoreCase))
        {
            dir = Path.Combine(Application.streamingAssetsPath, "Illustrations");
        }
        else
        {
            string modPath = ResolveModPathFromName(modName);
            if (string.IsNullOrEmpty(modPath)) return new string[0];
            dir = Path.Combine(modPath, "Illustrations");
        }

        if (!Directory.Exists(dir)) return new string[0];
        return Directory.GetFiles(dir, "*.png").Select(Path.GetFileName).ToArray();
    }

    private static string ResolveModPathFromName(string modName)
    {
        if (string.IsNullOrEmpty(modName)) return null;

        foreach (var mod in ModManager.ActiveMods)
        {
            string realName = GetModNameFromModInfo(mod.path);
            if (string.Equals(realName, modName, StringComparison.OrdinalIgnoreCase))
                return mod.path;
        }
        return null;
    }

    private static string GetModNameFromModInfo(string modPath)
    {
        try
        {
            string modInfoPath = Path.Combine(modPath, "modinfo.json");
            if (!File.Exists(modInfoPath)) return null;

            string json = File.ReadAllText(modInfoPath);

            int nameIndex = json.IndexOf("\"name\"", StringComparison.OrdinalIgnoreCase);
            if (nameIndex < 0) return null;

            int colonIndex = json.IndexOf(':', nameIndex);
            if (colonIndex < 0) return null;

            int startQuote = json.IndexOf('"', colonIndex + 1);
            if (startQuote < 0) return null;

            int endQuote = json.IndexOf('"', startQuote + 1);
            if (endQuote < 0) return null;

            return json.Substring(startQuote + 1, endQuote - startQuote - 1);
        }
        catch
        {
            return null;
        }
    }
}

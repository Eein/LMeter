using Dalamud.Interface;
using ImGuiNET;
using LMeter.Act;
using LMeter.Helpers;
using Newtonsoft.Json;
using System.Numerics;
using System.Runtime.CompilerServices;
using System;


namespace LMeter.Config;

public class HeaderConfig : IConfigPage
{
    [JsonIgnore]
    private static readonly string[] _anchorOptions = Enum.GetNames(typeof(DrawAnchor));

    public string Name =>
        "Header";

    public bool ShowHeader = true;
    public int HeaderHeight = 25;
    public ConfigColor BackgroundColor = new (r: 30f / 255f, g: 30f / 255f, b: 30f / 255f, a: 230 / 255f);

    public bool ShowEncounterDuration = true;
    public ConfigColor DurationColor = new (r: 0f / 255f, g: 190f / 255f, b: 225f / 255f, a: 1f);
    public bool DurationShowOutline = true;
    public ConfigColor DurationOutlineColor = new (r: 0, g: 0, b: 0, a: 0.5f);
    public DrawAnchor DurationAlign = DrawAnchor.Left;
    public Vector2 DurationOffset = new (0, 0);
    public int DurationFontId = 0;
    public string DurationFontKey = FontsManager.DalamudFontKey;

    public bool ShowEncounterName = true;
    public ConfigColor NameColor = new (r: 1, g: 1, b: 1, a: 1);
    public bool NameShowOutline = true;
    public ConfigColor NameOutlineColor = new (r: 0, g: 0, b: 0, a: 0.5f);
    public DrawAnchor NameAlign = DrawAnchor.Left;
    public Vector2 NameOffset = new (0, 0);
    public int NameFontId = 0;
    public string NameFontKey = FontsManager.DalamudFontKey;

    public bool ShowRaidStats = true;
    public ConfigColor RaidStatsColor = new (r: 0.5f, g: 0.5f, b: 0.5f, a: 1f);
    public bool StatsShowOutline = true;
    public ConfigColor StatsOutlineColor = new (r: 0, g: 0, b: 0, a: 0.5f);
    public DrawAnchor StatsAlign = DrawAnchor.Right;
    public Vector2 StatsOffset = new (0, 0);
    public int StatsFontId = 0;
    public string StatsFontKey = FontsManager.DalamudFontKey;
    public string RaidStatsFormat = "[dps]rdps [hps]rhps Deaths: [deaths]";
    public bool ThousandsSeparators = true;

    public IConfigPage GetDefault() =>
        new HeaderConfig
        {
            DurationFontKey = FontsManager.DefaultSmallFontKey,
            DurationFontId = PluginManager.Instance.FontsManager.GetFontIndex(FontsManager.DefaultSmallFontKey),
            NameFontKey = FontsManager.DefaultSmallFontKey,
            NameFontId = PluginManager.Instance.FontsManager.GetFontIndex(FontsManager.DefaultSmallFontKey),
            StatsFontKey = FontsManager.DefaultSmallFontKey,
            StatsFontId = PluginManager.Instance.FontsManager.GetFontIndex(FontsManager.DefaultSmallFontKey)
        };

    public (Vector2, Vector2) DrawHeader(Vector2 pos, Vector2 size, Encounter? encounter, ImDrawListPtr drawList)
    {
        if (!this.ShowHeader) return (pos, size);

        var headerSize = new Vector2(size.X, this.HeaderHeight);
        drawList.AddRectFilled(pos, pos + headerSize, this.BackgroundColor.Base);

        var durationPos = pos;
        var durationSize = Vector2.Zero;
        if (this.ShowEncounterDuration)
        {
            using (PluginManager.Instance.FontsManager.PushFont(this.DurationFontKey))
            {
                var duration = encounter is null 
                    ? $" LMeter v{Plugin.Version}" 
                    : $" {encounter.Duration}";

                durationSize = ImGui.CalcTextSize(duration);
                durationPos = Utils.GetAnchoredPosition(durationPos, -headerSize, DrawAnchor.Left);
                durationPos = Utils.GetAnchoredPosition
                (
                    durationPos,
                    durationSize,
                    this.DurationAlign
                ) + this.DurationOffset;

                DrawHelpers.DrawText
                (
                    drawList,
                    duration,
                    durationPos,
                    this.DurationColor.Base,
                    this.DurationShowOutline,
                    this.DurationOutlineColor.Base
                );
            }
        }

        var raidStatsSize = Vector2.Zero;
        if (this.ShowRaidStats && encounter is not null)
        {
            var text = encounter.GetFormattedString
            (
                $" {this.RaidStatsFormat} ",
                this.ThousandsSeparators 
                    ? "N" 
                    : "F"
            );

            if (!string.IsNullOrEmpty(text))
            {
                using (PluginManager.Instance.FontsManager.PushFont(this.StatsFontKey))
                {
                    raidStatsSize = ImGui.CalcTextSize(text);
                    var statsPos = Utils.GetAnchoredPosition(pos + this.StatsOffset, -headerSize, DrawAnchor.Right);
                    statsPos = Utils.GetAnchoredPosition(statsPos, raidStatsSize, this.StatsAlign);
                    DrawHelpers.DrawText
                    (
                        drawList,
                        text,
                        statsPos,
                        this.RaidStatsColor.Base,
                        this.StatsShowOutline,
                        this.StatsOutlineColor.Base
                    );
                }
            }
        }

        if (this.ShowEncounterName && encounter is not null && !string.IsNullOrEmpty(encounter.Title))
        {
            using (PluginManager.Instance.FontsManager.PushFont(this.NameFontKey))
            {
                var name = $" {encounter.Title}";
                var nameSize = ImGui.CalcTextSize(name);

                if (durationSize.X + raidStatsSize.X + nameSize.X > size.X)
                {
                    var ellipsesWidth = ImGui.CalcTextSize("... ").X;
                    do
                    {
                        name = name.AsSpan(0, name.Length - 1).ToString();
                        nameSize = ImGui.CalcTextSize(name);
                    }
                    while (durationSize.X + raidStatsSize.X + nameSize.X + ellipsesWidth > size.X && name.Length > 1);
                    name += "... ";
                }

                var namePos = Utils.GetAnchoredPosition(pos.AddX(durationSize.X), -headerSize, DrawAnchor.Left);
                namePos = Utils.GetAnchoredPosition(namePos, nameSize, this.NameAlign) + this.NameOffset;
                DrawHelpers.DrawText
                (
                    drawList,
                    name,
                    namePos,
                    this.NameColor.Base,
                    this.NameShowOutline,
                    this.NameOutlineColor.Base
                );
            }
        }

        return (pos.AddY(this.HeaderHeight), size.AddY(-this.HeaderHeight));
    }

    public void DrawConfig(Vector2 size, float padX, float padY)
    {
        var fontOptions = PluginManager.Instance.FontsManager.GetFontList();
        if (fontOptions.Length == 0) return;

        if (!ImGui.BeginChild($"##{this.Name}", new Vector2(size.X, size.Y), true))
        {
            ImGui.EndChild();
            return;
        }

        ImGui.Checkbox("Show Header", ref this.ShowHeader);
        if (!this.ShowHeader)
        {
            ImGui.EndChild();
            return;
        }

        DrawHelpers.DrawNestIndicator(1);
        ImGui.DragInt("Header Height", ref this.HeaderHeight, 1, 0, 100);

        DrawHelpers.DrawNestIndicator(1);
        var vector = this.BackgroundColor.Vector;
        ImGui.ColorEdit4
        (
            "Background Color",
            ref vector,
            ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar
        );
        this.BackgroundColor.Vector = vector;

        ImGui.NewLine();
        DrawHelpers.DrawNestIndicator(1);
        ImGui.Checkbox("Show Encounter Duration", ref this.ShowEncounterDuration);
        if (this.ShowEncounterDuration)
        {
            DrawHelpers.DrawNestIndicator(2);
            ImGui.DragFloat2("Position Offset##Duration", ref this.DurationOffset);

            if (!FontsManager.ValidateFont(fontOptions, this.DurationFontId, this.DurationFontKey))
            {
                this.DurationFontId = 0;
                for (var i = 0; i < fontOptions.Length; i++)
                {
                    if (this.DurationFontKey.Equals(fontOptions[i]))
                    {
                        this.DurationFontId = i;
                    }
                }
            }

            DrawHelpers.DrawNestIndicator(2);
            ImGui.Combo("Font##Duration", ref this.DurationFontId, fontOptions, fontOptions.Length);
            this.DurationFontKey = fontOptions[this.DurationFontId];

            DrawHelpers.DrawNestIndicator(2);
            ImGui.Combo
            (
                "Text Align##Duration",
                ref Unsafe.As<DrawAnchor, int>(ref this.DurationAlign),
                _anchorOptions,
                _anchorOptions.Length
            );

            DrawHelpers.DrawNestIndicator(2);
            vector = this.DurationColor.Vector;
            ImGui.ColorEdit4
            (
                "Text Color##Duration",
                ref vector,
                ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar
            );
            this.DurationColor.Vector = vector;

            DrawHelpers.DrawNestIndicator(2);
            ImGui.Checkbox("Show Outline##Duration", ref this.DurationShowOutline);
            if (this.DurationShowOutline)
            {
                DrawHelpers.DrawNestIndicator(3);
                vector = this.DurationOutlineColor.Vector;
                ImGui.ColorEdit4
                (
                    "Outline Color##Duration",
                    ref vector,
                    ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar
                );
                this.DurationOutlineColor.Vector = vector;
            }
        }

        ImGui.NewLine();
        DrawHelpers.DrawNestIndicator(1);
        ImGui.Checkbox("Show Encounter Name", ref this.ShowEncounterName);
        if (this.ShowEncounterName)
        {
            DrawHelpers.DrawNestIndicator(2);
            ImGui.DragFloat2("Position Offset##Name", ref this.NameOffset);

            if (!FontsManager.ValidateFont(fontOptions, this.NameFontId, this.NameFontKey))
            {
                this.NameFontId = 0;
                for (var i = 0; i < fontOptions.Length; i++)
                {
                    if (this.NameFontKey.Equals(fontOptions[i]))
                    {
                        this.NameFontId = i;
                    }
                }
            }

            DrawHelpers.DrawNestIndicator(2);
            ImGui.Combo("Font##Name", ref this.NameFontId, fontOptions, fontOptions.Length);
            this.NameFontKey = fontOptions[this.NameFontId];

            DrawHelpers.DrawNestIndicator(2);
            ImGui.Combo
            (
                "Text Align##Name",
                ref Unsafe.As<DrawAnchor, int>(ref this.NameAlign),
                _anchorOptions,
                _anchorOptions.Length
            );

            DrawHelpers.DrawNestIndicator(2);
            vector = this.NameColor.Vector;
            ImGui.ColorEdit4
            (
                "Text Color##Name",
                ref vector,
                ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar
            );
            this.NameColor.Vector = vector;

            DrawHelpers.DrawNestIndicator(2);
            ImGui.Checkbox("Show Outline##Name", ref this.NameShowOutline);
            if (this.NameShowOutline)
            {
                DrawHelpers.DrawNestIndicator(3);
                vector = this.NameOutlineColor.Vector;
                ImGui.ColorEdit4
                (
                    "Outline Color##Name",
                    ref vector,
                    ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar
                );
                this.NameOutlineColor.Vector = vector;
            }
        }

        ImGui.NewLine();
        DrawHelpers.DrawNestIndicator(1);
        ImGui.Checkbox("Show Raid Stats", ref this.ShowRaidStats);
        if (this.ShowRaidStats)
        {
            DrawHelpers.DrawNestIndicator(2);
            ImGui.InputText("Raid Stats Format", ref this.RaidStatsFormat, 128);
            if (ImGui.IsItemHovered())
            {
                using (PluginManager.Instance.FontsManager.PushFont(UiBuilder.MonoFont))
                {
                    ImGui.SetTooltip(Utils.GetTagsTooltip(Encounter.TextTags));
                }
            }

            DrawHelpers.DrawNestIndicator(2);
            ImGui.Checkbox("Use Thousands Separators for Numbers", ref this.ThousandsSeparators);

            DrawHelpers.DrawNestIndicator(2);
            ImGui.DragFloat2("Position Offset##Stats", ref this.StatsOffset);

            if (!FontsManager.ValidateFont(fontOptions, this.StatsFontId, this.StatsFontKey))
            {
                this.StatsFontId = 0;
                for (var i = 0; i < fontOptions.Length; i++)
                {
                    if (this.StatsFontKey.Equals(fontOptions[i]))
                    {
                        this.StatsFontId = i;
                    }
                }
            }

            DrawHelpers.DrawNestIndicator(2);
            ImGui.Combo("Font##Stats", ref this.StatsFontId, fontOptions, fontOptions.Length);
            this.StatsFontKey = fontOptions[this.StatsFontId];

            DrawHelpers.DrawNestIndicator(2);
            ImGui.Combo
            (
                "Text Align##Stats",
                ref Unsafe.As<DrawAnchor, int>(ref this.StatsAlign),
                _anchorOptions,
                _anchorOptions.Length
            );

            DrawHelpers.DrawNestIndicator(2);
            vector = this.RaidStatsColor.Vector;
            ImGui.ColorEdit4
            (
                "Text Color##Stats",
                ref vector,
                ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar
            );
            this.RaidStatsColor.Vector = vector;

            DrawHelpers.DrawNestIndicator(2);
            ImGui.Checkbox("Show Outline##Stats", ref this.StatsShowOutline);
            if (this.StatsShowOutline)
            {
                DrawHelpers.DrawNestIndicator(3);
                vector = this.StatsOutlineColor.Vector;
                ImGui.ColorEdit4
                (
                    "Outline Color##Stats",
                    ref vector,
                    ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar
                );
                this.StatsOutlineColor.Vector = vector;
            }
        }

        ImGui.EndChild();
    }
}

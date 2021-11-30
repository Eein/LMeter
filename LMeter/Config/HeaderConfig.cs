using System;
using System.Numerics;
using ImGuiNET;
using LMeter.Helpers;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using LMeter.ACT;
using System.Globalization;

namespace LMeter.Config
{
    public class HeaderConfig : IConfigPage
    {
        [JsonIgnore]
        private static string[] _anchorOptions = Enum.GetNames(typeof(DrawAnchor));

        public string Name => "Header";

        public bool ShowHeader = true;
        public int HeaderHeight = 25;
        public ConfigColor BackgroundColor = new ConfigColor(30f / 255f, 30f / 255f, 30f / 255f, 230 / 255f);

        public bool ShowEncounterDuration = true;
        public ConfigColor DurationColor = new ConfigColor(0f / 255f, 190f / 255f, 225f / 255f, 1f);
        public bool DurationShowOutline = true;
        public ConfigColor DurationOutlineColor = new ConfigColor(0, 0, 0, 0.5f);
        public DrawAnchor DurationAlign = DrawAnchor.Left;
        public Vector2 DurationOffset = new Vector2(0, 0);
        public int DurationFontId = 0;
        public string DurationFontKey = FontsManager.DalamudFontKey;

        public bool ShowEncounterName = true;
        public ConfigColor NameColor = new ConfigColor(1, 1, 1, 1);
        public bool NameShowOutline = true;
        public ConfigColor NameOutlineColor = new ConfigColor(0, 0, 0, 0.5f);
        public DrawAnchor NameAlign = DrawAnchor.Left;
        public Vector2 NameOffset = new Vector2(0, 0);
        public int NameFontId = 0;
        public string NameFontKey = FontsManager.DalamudFontKey;
        
        public bool ShowRaidStats = true;
        public ConfigColor RaidStatsColor = new ConfigColor(0.5f, 0.5f, 0.5f, 1f);
        public bool StatsShowOutline = true;
        public ConfigColor StatsOutlineColor = new ConfigColor(0, 0, 0, 0.5f);
        public DrawAnchor StatsAlign = DrawAnchor.Right;
        public Vector2 StatsOffset = new Vector2(0, 0);
        public int StatsFontId = 0;
        public string StatsFontKey = FontsManager.DalamudFontKey;
        public string StatsFormat = "[dps]rdps [hps]rhps Deaths: [deaths] ";

        public Vector2 DrawHeader(Vector2 pos, Vector2 size, Encounter? encounter, ImDrawListPtr drawList)
        {
            if (!this.ShowHeader)
            {
                return pos;
            }
            
            Vector2 headerSize = new Vector2(size.X, this.HeaderHeight);
            drawList.AddRectFilled(pos, pos + headerSize, this.BackgroundColor.Base);
            
            Vector2 durationPos = Vector2.Zero;
            Vector2 durationSize = Vector2.Zero;
            if (this.ShowEncounterDuration)
            {
                bool fontPushed = FontsManager.PushFont(this.DurationFontKey);
                string duration = encounter is null ? $" LMeter v{Plugin.Version} " : $"  {encounter.Duration} ";
                durationSize = ImGui.CalcTextSize(duration);
                durationPos = Utils.GetAnchoredPosition(pos + this.DurationOffset, -headerSize, DrawAnchor.Left);
                durationPos = Utils.GetAnchoredPosition(durationPos, durationSize, this.DurationAlign);
                DrawHelpers.DrawText(drawList, duration, durationPos, this.DurationColor.Base, this.DurationShowOutline, this.DurationOutlineColor.Base);
                if (fontPushed)
                {
                    ImGui.PopFont();
                }
            }

            if (this.ShowEncounterName && encounter is not null)
            {
                bool fontPushed = FontsManager.PushFont(this.NameFontKey);
                string name = encounter.Title;
                Vector2 namePos = durationPos.AddX(durationSize.X) + this.NameOffset;
                DrawHelpers.DrawText(drawList, name, namePos, this.NameColor.Base, this.NameShowOutline, this.NameOutlineColor.Base);
                if (fontPushed)
                {
                    ImGui.PopFont();
                }
            }

            if (this.ShowRaidStats && encounter is not null)
            {
                string text = encounter.GetFormattedString(this.StatsFormat);

                if (!string.IsNullOrEmpty(text))
                {
                    bool fontPushed = FontsManager.PushFont(this.StatsFontKey);
                    Vector2 statsSize = ImGui.CalcTextSize(text);
                    Vector2 statsPos = Utils.GetAnchoredPosition(pos + this.StatsOffset, -headerSize, DrawAnchor.Right);
                    statsPos = Utils.GetAnchoredPosition(statsPos, statsSize, this.StatsAlign);
                    DrawHelpers.DrawText(drawList, text, statsPos, this.RaidStatsColor.Base, this.StatsShowOutline, this.StatsOutlineColor.Base);                  
                    if (fontPushed)
                    {
                        ImGui.PopFont();
                    }
                }
            }
            
            return pos.AddY(this.HeaderHeight);
        }

        public void DrawConfig(Vector2 size, float padX, float padY)
        {
            string[] fontOptions = FontsManager.GetFontList();
            if (fontOptions.Length == 0)
            {
                return;
            }

            if (ImGui.BeginChild($"##{this.Name}", new Vector2(size.X, size.Y), true))
            {
                ImGui.Checkbox("Show Header", ref this.ShowHeader);
                if (this.ShowHeader)
                {
                    DrawHelpers.DrawNestIndicator(1);
                    ImGui.DragInt("Header Height", ref this.HeaderHeight, 1, 0, 100);

                    DrawHelpers.DrawNestIndicator(1);
                    Vector4 vector = this.BackgroundColor.Vector;
                    ImGui.ColorEdit4("Background Color", ref vector, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar);
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
                            this.DurationFontKey = FontsManager.DalamudFontKey;
                        }
                        
                        DrawHelpers.DrawNestIndicator(2);
                        ImGui.Combo("Font##Duration", ref this.DurationFontId, fontOptions, fontOptions.Length);
                        this.DurationFontKey = fontOptions[this.DurationFontId];

                        DrawHelpers.DrawNestIndicator(2);
                        ImGui.Combo("Text Align##Duration", ref Unsafe.As<DrawAnchor, int>(ref this.DurationAlign), _anchorOptions, _anchorOptions.Length);

                        DrawHelpers.DrawNestIndicator(2);
                        vector = this.DurationColor.Vector;
                        ImGui.ColorEdit4("Text Color##Duration", ref vector, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar);
                        this.DurationColor.Vector = vector;

                        DrawHelpers.DrawNestIndicator(2);
                        ImGui.Checkbox("Show Outline##Duration", ref this.DurationShowOutline);
                        if (this.DurationShowOutline)
                        {
                            DrawHelpers.DrawNestIndicator(3);
                            vector = this.DurationOutlineColor.Vector;
                            ImGui.ColorEdit4("Outline Color##Duration", ref vector, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar);
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
                            this.NameFontKey = FontsManager.DalamudFontKey;
                        }
                        
                        DrawHelpers.DrawNestIndicator(2);
                        ImGui.Combo("Font##Name", ref this.NameFontId, fontOptions, fontOptions.Length);
                        this.NameFontKey = fontOptions[this.NameFontId];

                        DrawHelpers.DrawNestIndicator(2);
                        ImGui.Combo("Text Align##Name", ref Unsafe.As<DrawAnchor, int>(ref this.NameAlign), _anchorOptions, _anchorOptions.Length);

                        DrawHelpers.DrawNestIndicator(2);
                        vector = this.NameColor.Vector;
                        ImGui.ColorEdit4("Text Color##Name", ref vector, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar);
                        this.NameColor.Vector = vector;

                        DrawHelpers.DrawNestIndicator(2);
                        ImGui.Checkbox("Show Outline##Name", ref this.NameShowOutline);
                        if (this.NameShowOutline)
                        {
                            DrawHelpers.DrawNestIndicator(3);
                            vector = this.NameOutlineColor.Vector;
                            ImGui.ColorEdit4("Outline Color##Name", ref vector, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar);
                            this.NameOutlineColor.Vector = vector;
                        }
                    }

                    ImGui.NewLine();
                    DrawHelpers.DrawNestIndicator(1);
                    ImGui.Checkbox("Show Raid Stats", ref this.ShowRaidStats);
                    if (this.ShowRaidStats)
                    {
                        DrawHelpers.DrawNestIndicator(2);
                        ImGui.InputText("Raid Stats Format", ref this.StatsFormat, 128);

                        if (ImGui.IsItemHovered())
                        {
                            string tooltip = $"Available Data Tags:\n\n{string.Join("\n", Encounter.GetTags())}";
                            ImGui.SetTooltip(tooltip);
                        }

                        DrawHelpers.DrawNestIndicator(2);
                        ImGui.DragFloat2("Position Offset##Stats", ref this.StatsOffset);

                        if (!FontsManager.ValidateFont(fontOptions, this.StatsFontId, this.StatsFontKey))
                        {
                            this.StatsFontId = 0;
                            this.StatsFontKey = FontsManager.DalamudFontKey;
                        }
                        
                        DrawHelpers.DrawNestIndicator(2);
                        ImGui.Combo("Font##Stats", ref this.StatsFontId, fontOptions, fontOptions.Length);
                        this.StatsFontKey = fontOptions[this.StatsFontId];

                        DrawHelpers.DrawNestIndicator(2);
                        ImGui.Combo("Text Align##Stats", ref Unsafe.As<DrawAnchor, int>(ref this.StatsAlign), _anchorOptions, _anchorOptions.Length);

                        DrawHelpers.DrawNestIndicator(2);
                        vector = this.RaidStatsColor.Vector;
                        ImGui.ColorEdit4("Text Color##Stats", ref vector, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar);
                        this.RaidStatsColor.Vector = vector;

                        DrawHelpers.DrawNestIndicator(2);
                        ImGui.Checkbox("Show Outline##Stats", ref this.StatsShowOutline);
                        if (this.StatsShowOutline)
                        {
                            DrawHelpers.DrawNestIndicator(3);
                            vector = this.StatsOutlineColor.Vector;
                            ImGui.ColorEdit4("Outline Color##Stats", ref vector, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar);
                            this.StatsOutlineColor.Vector = vector;
                        }
                    }
                }

                ImGui.EndChild();
            }
        }
    }
}
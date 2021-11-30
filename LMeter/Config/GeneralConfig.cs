using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using ImGuiNET;
using LMeter.Helpers;
using Newtonsoft.Json;

namespace LMeter.Config
{
    public enum MeterDataType
    {
        Damage,
        Healing,
        DamageTaken
    }

    public class GeneralConfig : IConfigPage
    {
        [JsonIgnore]
        private static string[] _meterTypeOptions = Enum.GetNames(typeof(MeterDataType));
        
        [JsonIgnore]
        public bool Preview = false;

        public string Name => "General";

        public Vector2 Position = Vector2.Zero;
        public Vector2 Size = ImGui.GetMainViewport().Size / 10;
        public bool Lock = false;
        public bool ClickThrough = false;
        public ConfigColor BackgroundColor = new ConfigColor(0, 0, 0, 0.5f);
        public bool ShowBorder = false;
        public ConfigColor BorderColor = new ConfigColor(0, 0, 0, 1f);
        public int BorderThickness = 2;
        public MeterDataType DataType = MeterDataType.Damage;

        public void DrawConfig(Vector2 size, float padX, float padY)
        {
            if (ImGui.BeginChild($"##{this.Name}", new Vector2(size.X, size.Y), true))
            {
                Vector2 screenSize = ImGui.GetMainViewport().Size;
                ImGui.DragFloat2("Position", ref this.Position, 1, -screenSize.X / 2, screenSize.X / 2);
                ImGui.DragFloat2("Size", ref this.Size, 1, 0, screenSize.Y);
                ImGui.Checkbox("Lock", ref this.Lock);
                ImGui.Checkbox("Click Through", ref this.ClickThrough);

                ImGui.NewLine();

                Vector4 vector = this.BackgroundColor.Vector;
                ImGui.ColorEdit4("Background Color", ref vector, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar);
                this.BackgroundColor.Vector = vector;

                ImGui.Checkbox("Show Border", ref this.ShowBorder);
                if (this.ShowBorder)
                {
                    DrawHelpers.DrawNestIndicator(1);
                    ImGui.DragInt("Border Thickness", ref this.BorderThickness, 1, 1, 20);

                    DrawHelpers.DrawNestIndicator(1);
                    vector = this.BorderColor.Vector;
                    ImGui.ColorEdit4("Border Color", ref vector, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar);
                    this.BorderColor.Vector = vector;
                }

                ImGui.NewLine();
                ImGui.Combo("Sort Type", ref Unsafe.As<MeterDataType, int>(ref this.DataType), _meterTypeOptions, _meterTypeOptions.Length);

                ImGui.Checkbox("Preview", ref this.Preview);

                ImGui.EndChild();
            }
        }
    }
}
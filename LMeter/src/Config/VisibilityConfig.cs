using ImGuiNET;
using LMeter.Helpers;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System;


namespace LMeter.Config;

public class VisibilityConfig : IConfigPage
{
    public string Name =>
        "Visibility";

    [JsonIgnore]
    private string _customJobInput = string.Empty;

    public bool AlwaysHide = false;
    public bool HideInCombat = false;
    public bool HideOutsideCombat = false;
    public bool ShowAnywayWhenInDuty = false;
    public bool HideOutsideDuty = false;
    public bool ShowAnywayWhenInCombat = false;
    public bool HideWhilePerforming = false;
    public bool HideInGoldenSaucer = false;
    public bool HideIfNotConnected = false;

    public JobType ShowForJobTypes = JobType.All;
    public string CustomJobString = string.Empty;
    public List<Job> CustomJobList = new ();

    public IConfigPage GetDefault() =>
        new VisibilityConfig();

    public bool IsVisible()
    {
        if (this.AlwaysHide)
        {
            return false;
        }

        if (this.HideInCombat && CharacterState.IsInCombat())
        {
            return false;
        }

        var shouldHide = false;
        if (this.HideOutsideCombat && !CharacterState.IsInCombat())
        {
            shouldHide |= true;
            if (this.ShowAnywayWhenInDuty && CharacterState.IsInDuty())
            {
                shouldHide = false;
            }
        }

        if (this.HideOutsideDuty && !CharacterState.IsInDuty())
        {
            shouldHide |= true;
            if (this.ShowAnywayWhenInCombat && CharacterState.IsInCombat())
            {
                shouldHide = false;
            }
        }

        if (shouldHide) return false;

        if (this.HideWhilePerforming && CharacterState.IsPerforming())
        {
            return false;
        }

        if (this.HideInGoldenSaucer && CharacterState.IsInGoldenSaucer())
        {
            return false;
        }

        if (this.HideIfNotConnected && !PluginManager.Instance.ActClient.Current.ClientReady())
        {
            return false;
        }

        return CharacterState.IsJobType(CharacterState.GetCharacterJob(), this.ShowForJobTypes, this.CustomJobList);
    }

    public void DrawConfig(Vector2 size, float padX, float padY)
    {
        if (!ImGui.BeginChild($"##{this.Name}", new Vector2(size.X, size.Y), true))
        {
            ImGui.EndChild();
            return;
        }

        ImGui.Checkbox("Always Hide", ref this.AlwaysHide);
        ImGui.Checkbox("Hide In Combat", ref this.HideInCombat);
        ImGui.Checkbox("Hide Outside Combat", ref this.HideOutsideCombat);
        ImGui.Indent();
        ImGui.Checkbox("Show Anyway When In Duty", ref this.ShowAnywayWhenInDuty);
        ImGui.Unindent();
        ImGui.Checkbox("Hide Outside Duty", ref this.HideOutsideDuty);
        ImGui.Indent();
        ImGui.Checkbox("Show Anyway When In Combat", ref this.ShowAnywayWhenInCombat);
        ImGui.Unindent();
        ImGui.Checkbox("Hide While Performing", ref this.HideWhilePerforming);
        ImGui.Checkbox("Hide In Golden Saucer", ref this.HideInGoldenSaucer);
        ImGui.Checkbox("Hide While Not Connected to ACT", ref this.HideIfNotConnected);

        DrawHelpers.DrawSpacing(1);
        var jobTypeOptions = Enum.GetNames(typeof(JobType));
        ImGui.Combo
        (
            "Show for Jobs",
            ref Unsafe.As<JobType, int>(ref this.ShowForJobTypes),
            jobTypeOptions,
            jobTypeOptions.Length
        );

        if (this.ShowForJobTypes == JobType.Custom)
        {
            if (string.IsNullOrEmpty(_customJobInput)) _customJobInput = this.CustomJobString.ToUpper();

            if
            (
                ImGui.InputTextWithHint
                (
                    "Custom Job List",
                    "Comma Separated List (ex: WAR, SAM, BLM)",
                    ref _customJobInput,
                    100,
                    ImGuiInputTextFlags.EnterReturnsTrue
                )
            )
            {
                var jobStrings = _customJobInput.Split(',').Select(j => j.Trim());
                var jobList = new List<Job>();

                foreach (var j in jobStrings)
                {
                    if (Enum.TryParse(j, true, out Job parsed))
                    {
                        jobList.Add(parsed);
                    }
                    else
                    {
                        jobList.Clear();
                        _customJobInput = string.Empty;
                        break;
                    }
                }

                _customJobInput = _customJobInput.ToUpper();
                this.CustomJobString = _customJobInput;
                this.CustomJobList = jobList;
            }
        }

        ImGui.EndChild();
    }
}

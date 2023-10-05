using System;
using System.IO;
using System.Reflection;


namespace LMeter;

public static class MagicValues
{
    public const string DefaultCactbotUrlQuery = "?OVERLAY_WS=ws://127.0.0.1:10501/ws";
    public const string DefaultCactbotUrl =
        "https://quisquous.github.io/cactbot/ui/raidboss/raidboss.html" + DefaultCactbotUrlQuery;
    public const string DiscordUrl =
        "https://discord.gg/C6fptVuFzZ";
    public const string GitRepoUrl =
        "https://github.com/joshua-software-dev/LMeter";
    public const string PatchedCryptographyDllUrl =
        "https://cdn.discordapp.com/attachments/1012241909403615313/1113368719834497104/System.Security.Cryptography.dll";
    public const string TotallyNotCefUpdateCheckUrl =
        "https://gitlab.com/api/v4/projects/50992666/releases/";
    public static readonly string DllInstallLocation =
        Path.GetFullPath
        (
            Path.GetDirectoryName
            (
                Assembly.GetExecutingAssembly()?.Location ?? throw new NullReferenceException()
            ) ?? throw new NullReferenceException()
        );
    public static readonly string DefaultTotallyNotCefInstallLocation =
        Path.GetFullPath(Path.Join(DllInstallLocation, "../TotallyNotCef/"));
}

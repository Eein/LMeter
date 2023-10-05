using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using ImGuiNET;
using LMeter.Config;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;


namespace LMeter.Act;

public enum SubscriptionStatus
{
    NotConnected,
    ConnectionFailed,
    Connecting,
    Connected,
    Subscribing,
    Subscribed,
    Unsubscribing,
    ShuttingDown
}

public class IinactClient : ActEventParser, IActClient
{
    private readonly IChatGui _chatGui;
    private readonly ActConfig _config;
    private readonly DalamudPluginInterface _dpi;
    private readonly ICallGateProvider<JObject, bool> subscriptionReceiver;

    public const string IinactListeningIpcEndpoint = "IINACT.Server.Listening";
    public const string IinactSubscribeIpcEndpoint = "IINACT.CreateSubscriber";
    public const string IinactUnsubscribeIpcEndpoint = "IINACT.Unsubscribe";

    private const string LMeterSubscriptionIpcEndpoint = "LMeter.SubscriptionReceiver";
    private const string IinactProviderEditEndpoint = "IINACT.IpcProvider." + LMeterSubscriptionIpcEndpoint;
    private static readonly JObject SubscriptionMessageObject = JObject.Parse(ActWebSocketClient.SubscriptionMessage);

    private SubscriptionStatus _status;
    private string? _lastErrorMessage;

    public IinactClient(IChatGui chatGui, ActConfig config, DalamudPluginInterface dpi)
    {
        _chatGui = chatGui;
        _config = config;
        _dpi = dpi;
        _status = SubscriptionStatus.NotConnected;
        PastEvents = new ();

        subscriptionReceiver = _dpi.GetIpcProvider<JObject, bool>(LMeterSubscriptionIpcEndpoint);
        subscriptionReceiver.RegisterFunc(ReceiveIpcMessage);
    }

    public bool ClientReady() =>
        _status == SubscriptionStatus.Subscribed;

    public bool ConnectionIncompleteOrFailed() =>
        _status == SubscriptionStatus.NotConnected || _status == SubscriptionStatus.ConnectionFailed;

    public void DrawConnectionStatus()
    {
        ImGui.Text($"IINACT Status: {_status}");

        if
        (
            _status != SubscriptionStatus.ConnectionFailed &&
            _status != SubscriptionStatus.Connecting &&
            _status != SubscriptionStatus.Connected &&
            _status != SubscriptionStatus.Subscribing &&
            _status != SubscriptionStatus.Subscribed
        )
        {
            return;
        }

        ImGui.SameLine();
        ImGui.Text
        (
            _status switch
            {
                SubscriptionStatus.ConnectionFailed => "0/4",
                SubscriptionStatus.Connecting => "1/4",
                SubscriptionStatus.Connected => "2/4",
                SubscriptionStatus.Subscribing => "3/4",
                SubscriptionStatus.Subscribed => "4/4",
                _ => throw new ArgumentOutOfRangeException()
            }
        );

        if (_status == SubscriptionStatus.ConnectionFailed)
        {
            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.Text("");
            ImGui.PopFont();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(_lastErrorMessage);
            }
        }

        var fontScale = ImGui.GetIO().FontGlobalScale;
        var failColor = ImGui.GetColorU32(ImGuiColors.DalamudRed);
        var loadingColor = ImGui.GetColorU32(ImGuiColors.DalamudGrey);
        var successColor = ImGui.GetColorU32(ImGuiColors.DalamudGrey3);

        ImGui.BeginTable("IINACT connection status", 4, ImGuiTableFlags.Borders);
        ImGui.TableSetupColumn("1", ImGuiTableColumnFlags.None, 25 * fontScale);
        ImGui.TableSetupColumn("2", ImGuiTableColumnFlags.None, 25 * fontScale);
        ImGui.TableSetupColumn("3", ImGuiTableColumnFlags.None, 25 * fontScale);
        ImGui.TableSetupColumn("4", ImGuiTableColumnFlags.None, 25 * fontScale);
        ImGui.TableNextRow();

        for (var i = 2; i < 6; i++)
        {
            var iterStatus = (SubscriptionStatus) i;
            ImGui.TableNextColumn();
            if (_status == SubscriptionStatus.ConnectionFailed)
            {
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, failColor);
                ImGui.Text(iterStatus.ToString());
                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.Text("");
                ImGui.PopFont();
            }
            if ((int) _status > i || _status == SubscriptionStatus.Subscribed)
            {
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, successColor);
                ImGui.Text(iterStatus.ToString());
                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.Text("");
                ImGui.PopFont();
            }
            else if ((int) _status == i)
            {
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, loadingColor);
                ImGui.Text(iterStatus.ToString());
                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.Text("");
                ImGui.PopFont();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip
                (
                    iterStatus switch
                    {
                        SubscriptionStatus.Connecting =>
                            """
                            LMeter attempts to connect to IINACT, if it fails the
                            connection attempt ends.
                            """,
                        SubscriptionStatus.Connected =>
                            """
                            LMeter successfully connected to IINACT. While connection
                            was established, in this state no data will be sent until
                            LMeter explicitly requests for that data.
                            """,
                        SubscriptionStatus.Subscribing =>
                            """
                            LMeter sends a second message to IINACT requesting that all
                            "CombatEvent" data be sent whenever IINACT generates such
                            an event.
                            """,
                        SubscriptionStatus.Subscribed =>
                            """
                            LMeter successfully sent a message to IINACT requesting
                            "CombatEvent" data. There was no connection error upon this
                            request, however IINACT does not reply on successful
                            subscription state change.
                            """,
                        _ => throw new ArgumentOutOfRangeException()
                    }
                );
            }
        }

        ImGui.EndTable();
    }

    public ActEvent? GetEvent(int index = -1)
    {
        if (index >= 0 && index < PastEvents.Count)
        {
            return PastEvents[index];
        }

        return LastEvent;
    }

    public void EndEncounter()
    {
        var message = new XivChatEntry()
        {
            Message = "end",
            Type = XivChatType.Echo
        };

        _chatGui.Print(message);
    }

    public void Clear()
    {
        LastEvent = null;
        PastEvents = new List<ActEvent>();

        if (_config.ClearAct)
        {
            var message = new XivChatEntry()
            {
                Message = "clear",
                Type = XivChatType.Echo
            };

            _chatGui.Print(message);
        }
    }

    public void RetryConnection()
    {
        Reset();
        Start();
    }

    public void Start()
    {
        if (_status != SubscriptionStatus.NotConnected)
        {
            LMeterLogger.Logger?.Error("Cannot start, IINACTClient needs to be reset!");
            return;
        }
        else if (_config.WaitForCharacterLogin)
        {
            if (!PluginManager.Instance?.ClientState.IsLoggedIn ?? true)
            {
                LMeterLogger.Logger?.Error("Cannot start, player is not logged in.");
                return;
            }
        }

        Connect();
    }

    private bool Connect()
    {
        _status = SubscriptionStatus.Connecting;

        try
        {
            var connectSuccess = _dpi.GetIpcSubscriber<bool>(IinactListeningIpcEndpoint).InvokeFunc();
            LMeterLogger.Logger?.Verbose("Check if IINACT installed and running: " + connectSuccess);
            if (!connectSuccess) return false;
        }
        catch (Exception ex)
        {
            _status = SubscriptionStatus.ConnectionFailed;
            _lastErrorMessage = "IINACT server was not found or was not finished starting.";
            LMeterLogger.Logger?.Info(_lastErrorMessage);
            _lastErrorMessage = _lastErrorMessage + "\n\n" + ex;
            LMeterLogger.Logger?.Verbose(_lastErrorMessage);
            return false;
        }
        _status = SubscriptionStatus.Connected;
        LMeterLogger.Logger?.Info("Successfully discovered IINACT IPC endpoint");

        try
        {
            var subscribeSuccess = _dpi
                .GetIpcSubscriber<string, bool>(IinactSubscribeIpcEndpoint)
                .InvokeFunc(LMeterSubscriptionIpcEndpoint);
            LMeterLogger.Logger?.Verbose("Setup default empty IINACT subscription successfully: " + subscribeSuccess);
            if (!subscribeSuccess) return false;
        }
        catch (Exception ex)
        {
            _status = SubscriptionStatus.ConnectionFailed;
            _lastErrorMessage = "Failed to setup IINACT subscription!";
            LMeterLogger.Logger?.Info(_lastErrorMessage);
            _lastErrorMessage = _lastErrorMessage + "\n\n" + ex;
            LMeterLogger.Logger?.Verbose(_lastErrorMessage);
            return false;
        }
        _status = SubscriptionStatus.Subscribing;

        try
        {
            // no way to check this, hoping blindly that it always works ¯\_(ツ)_/¯
            LMeterLogger.Logger?.Verbose($"""Updating subscription using endpoint: `{IinactProviderEditEndpoint}`""");
            _dpi
                .GetIpcSubscriber<JObject, bool>(IinactProviderEditEndpoint)
                .InvokeAction(SubscriptionMessageObject);
            LMeterLogger.Logger?.Verbose($"""Subscription update message sent""");
            _status = SubscriptionStatus.Subscribed;
            LMeterLogger.Logger?.Info("Successfully subscribed to combat events from IINACT IPC");
            return true;
        }
        catch (Exception ex)
        {
            _status = SubscriptionStatus.ConnectionFailed;
            _lastErrorMessage = "Failed to finalize IINACT subscription!";
            LMeterLogger.Logger?.Info(_lastErrorMessage);
            _lastErrorMessage = _lastErrorMessage + "\n\n" + ex;
            LMeterLogger.Logger?.Verbose(_lastErrorMessage);
            return false;
        }
    }

    private bool ReceiveIpcMessage(JObject data)
    {
        try
        {
            var newEvent = data.ToObject<ActEvent?>();
            return this.ParseNewEvent(newEvent, _config.EncounterHistorySize);
        }
        catch (Exception ex)
        {
            LMeterLogger.Logger?.Verbose(ex.ToString());
            return false;
        }
    }

    public void Shutdown()
    {
        _status = SubscriptionStatus.Unsubscribing;

        try
        {
            var success = _dpi
                .GetIpcSubscriber<string, bool>(IinactUnsubscribeIpcEndpoint)
                .InvokeFunc(LMeterSubscriptionIpcEndpoint);

            LMeterLogger.Logger?.Info(
                success
                    ? "Successfully unsubscribed from IINACT IPC"
                    : "Failed to unsubscribe from IINACT IPC"
            );
        }
        catch (Exception)
        {
            // don't throw when closing
        }

        _status = SubscriptionStatus.ShuttingDown;
    }

    public void Reset()
    {
        this.Shutdown();
        _status = SubscriptionStatus.NotConnected;
    }

    public void Dispose()
    {
        subscriptionReceiver.UnregisterFunc();
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.Shutdown();
        }
    }
}

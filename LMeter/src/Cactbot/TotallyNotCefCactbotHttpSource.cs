using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using LMeter.Runtime;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;


namespace LMeter.Cactbot;

public class TotallyNotCefCactbotHttpSource : IDisposable
{
    public readonly CactbotState CactbotState;

    private readonly string _browserInstallFolder;
    private readonly bool _bypassWebSocket;
    private readonly string _cactbotUrl;
    private readonly IinactCactbotClient _iinactCactbotClient;
    private readonly CancellationTokenSource _cancelTokenSource;
    private readonly bool _enableAudio;
    private readonly bool _enableVerboseResponseLogging;
    private readonly HttpClient _httpClient;
    private readonly HtmlParser _htmlParser;
    private readonly string _httpUrl;
    private readonly ushort _httpPort;

    public bool BackgroundThreadRunning { get; private set; } = false;
    public bool LastPollSuccessful { get; private set; } = false;
    public TotallyNotCefConnectionState ConnectionState = TotallyNotCefConnectionState.WaitingForConnection;
    public TotallyNotCefHealthCheckResponse LastHealthResponse = TotallyNotCefHealthCheckResponse.Unverified;
    public TotallyNotCefBrowserState WebBrowserState = TotallyNotCefBrowserState.NotStarted;
    public int PollingRate { get; set; } = 1000; // milliseconds

    private IHtmlDocument? _parsedResponse = null;
    private bool _browserNeedsReload;

    public TotallyNotCefCactbotHttpSource
    (
        string browserInstallFolder,
        bool bypassWebSocket,
        string cactbotUrl,
        ushort httpPort,
        bool enableAudio,
        bool enableVerboseResponseLogging
    )
    {
        CactbotState = new ();
        _browserInstallFolder = browserInstallFolder;
        _bypassWebSocket = bypassWebSocket;
        _browserNeedsReload = false;

        if (_bypassWebSocket)
        {
            try
            {
                Uri.TryCreate(cactbotUrl, UriKind.RelativeOrAbsolute, out var tempCactbotUri);
                _cactbotUrl = $"{tempCactbotUri?.GetLeftPart(UriPartial.Path)}{MagicValues.DefaultCactbotUrlQuery}";
            }
            catch
            {
                _cactbotUrl = cactbotUrl;
            }
        }
        else
        {
            _cactbotUrl = cactbotUrl;
        }

        _cancelTokenSource = new ();
        _enableAudio = enableAudio;
        _enableVerboseResponseLogging = enableVerboseResponseLogging;
        _htmlParser = new ();
        _httpPort = httpPort;
        _httpUrl = $"http://127.0.0.1:{httpPort}";
        _httpClient = new ();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("curl/8.1.2");
        _iinactCactbotClient = new
        (
            _bypassWebSocket,
            PluginManager.Instance?.ClientState ?? throw new NullReferenceException(),
            _cancelTokenSource,
            PluginManager.Instance?.PluginInterface ?? throw new NullReferenceException(),
            _httpClient,
            _httpUrl
        );
    }

    private class TotallyNotCefPortValidResponse
    {
        #pragma warning disable CS0649
        // JSON reflection is annoying
        [JsonProperty("IsTotallyNotCef")]
        public bool IsTotallyNotCef;
        #pragma warning restore CS0649
    }

    private async Task<TotallyNotCefHealthCheckResponse> CheckIfPortBelongsToTotallyNotCef()
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync
            (
                _httpUrl + "/tncef",
                cancellationToken: _cancelTokenSource.Token
            );

            if (!response.IsSuccessStatusCode || response?.Content == null)
            {
                return TotallyNotCefHealthCheckResponse.InvalidResponse;
            }
        }
        catch (Exception e) when
        (
            e is OperationCanceledException ||
            e is TaskCanceledException ||
            e is HttpRequestException ||
            e is SocketException
        )
        {
            return TotallyNotCefHealthCheckResponse.NoResponse;
        }

        var rawJson = await response.Content.ReadAsStringAsync(_cancelTokenSource.Token);
        if (rawJson == null) return TotallyNotCefHealthCheckResponse.InvalidResponse;

        try
        {
            var parsedJson = JsonConvert.DeserializeObject<TotallyNotCefPortValidResponse?>(rawJson);
            if (parsedJson == null) return TotallyNotCefHealthCheckResponse.InvalidResponse;
            return parsedJson.IsTotallyNotCef
                ? TotallyNotCefHealthCheckResponse.CorrectResponse
                : TotallyNotCefHealthCheckResponse.InvalidResponse;
        }
        catch (Exception)
        {
            return TotallyNotCefHealthCheckResponse.InvalidResponse;
        }
    }

    private async Task GetCactbotHtml()
    {
        _parsedResponse = null;

        try
        {
            if (LastHealthResponse != TotallyNotCefHealthCheckResponse.CorrectResponse)
            {
                var health = await CheckIfPortBelongsToTotallyNotCef();
                LastHealthResponse = health;
                if (health != TotallyNotCefHealthCheckResponse.CorrectResponse)
                {
                    return;
                }
                else
                {
                    _iinactCactbotClient.Start(); // should be idempotent
                }
            }

            var response = await _httpClient.GetAsync(_httpUrl, cancellationToken: _cancelTokenSource.Token);
            if (response.Content == null) return;

            var rawHtml = await response.Content.ReadAsStringAsync(_cancelTokenSource.Token);
            if (rawHtml == null) return;
            if (_enableVerboseResponseLogging)
            {
                LMeterLogger.Logger?.Info(rawHtml);
            }

            _parsedResponse = await _htmlParser.ParseDocumentAsync(rawHtml, _cancelTokenSource.Token);
            CactbotState.UpdateState(_parsedResponse);
            ConnectionState = TotallyNotCefConnectionState.Connected;
        }
        catch (Exception e) when
        (
            e is OperationCanceledException ||
            e is TaskCanceledException ||
            e is HttpRequestException
        ) { }
    }

    private class GitlabLinksResponse
    {
        #pragma warning disable CS0649
        // JSON reflection is annoying
        [JsonProperty("name")]
        public string? FileName;

        [JsonProperty("url")]
        public string? Url;
        #pragma warning restore CS0649
    }

    private class GitlabAssetsResponse
    {
        #pragma warning disable CS0649
        // JSON reflection is annoying
        [JsonProperty("links")]
        public GitlabLinksResponse[]? Links;
        #pragma warning restore CS0649
    }

    private class GitlabTagResponse
    {
        #pragma warning disable CS0649
        // JSON reflection is annoying
        [JsonProperty("tag_name")]
        public string? TagName;

        [JsonProperty("assets")]
        public GitlabAssetsResponse? Assets;
        #pragma warning restore CS0649
    }

    private async Task<bool> IsTotallyNotCefUpToDate(string exePath)
    {
        FileVersionInfo? localVersion;
        try
        {
            if (!exePath.EndsWith(".exe")) return false;
            var dllPath = exePath.Substring(0, exePath.Length - 3) + "dll";

            localVersion = FileVersionInfo.GetVersionInfo(dllPath);
            // don't bother checking the web if the file isn't even present / correct
            if (localVersion == null || localVersion.FileVersion == null) return false;
        }
        catch
        {
            return false;
        }

        LMeterLogger.Logger?.Debug($"TotallyNotCef Version: {localVersion.FileVersion}");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync
            (
                MagicValues.TotallyNotCefUpdateCheckUrl,
                cancellationToken: _cancelTokenSource.Token
            );

            // assume up to date, gitlab hasn't responded correctly.
            if (!response.IsSuccessStatusCode) return true;
        }
        catch (Exception e) when
        (
            e is OperationCanceledException ||
            e is TaskCanceledException ||
            e is HttpRequestException ||
            e is SocketException
        )
        {
            // same here.
            return true;
        }

        // same here too.
        if (response?.Content == null) return true;

        var rawJson = await response.Content.ReadAsStringAsync(_cancelTokenSource.Token);
        // same here three.
        if (rawJson == null) return true;

        GitlabTagResponse[]? parsedJson;
        try
        {
            parsedJson = JsonConvert.DeserializeObject<GitlabTagResponse[]>(rawJson);
            // same here four.
            if (parsedJson == null || parsedJson.Length < 1) return true;
        }
        catch (JsonSerializationException)
        {
            // same here five.
            return true;
        }

        var latestVersion = parsedJson[0]?.TagName?.Replace("v", string.Empty);
        // same here six.
        if (latestVersion == null) return true;
        LMeterLogger.Logger?.Debug($"Latest Version: {latestVersion}");

        return localVersion.FileVersion == latestVersion;
    }

    private async Task DeleteTotallyNotCefInstall(string cefDirPath)
    {
        KillWebBrowserProcess();
        await Task.Delay(1000);

        try
        {
            Directory.Delete(cefDirPath, recursive: true);
            return;
        }
        catch
        {
            return;
        }
    }

    /// <summary>
    /// Do not call unless installing to the expected default install location
    /// </summary>
    private async Task DownloadTotallyNotCef(string cefDirPath)
    {
        var extractDir = Path.GetFullPath(Path.Join(cefDirPath, ".."));
        LMeterLogger.Logger?.Info($"Extract Directory: {extractDir}");
        WebBrowserState = TotallyNotCefBrowserState.Downloading;
        LMeterLogger.Logger?.Info("Downloading TotallyNotCef...");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync
            (
                MagicValues.TotallyNotCefUpdateCheckUrl,
                cancellationToken: _cancelTokenSource.Token
            );

            // Failed to poll gitlab API, giving up here.
            if (!response.IsSuccessStatusCode) return;
        }
        catch (Exception e) when
        (
            e is OperationCanceledException ||
            e is TaskCanceledException ||
            e is HttpRequestException ||
            e is SocketException
        )
        {
            // same here.
            return;
        }

        // same here too.
        if (response?.Content == null) return;

        var rawJson = await response.Content.ReadAsStringAsync(_cancelTokenSource.Token);
        // same here three.
        if (rawJson == null) return;

        GitlabTagResponse[]? parsedJson;
        try
        {
            parsedJson = JsonConvert.DeserializeObject<GitlabTagResponse[]>(rawJson);
            // same here four.
            if (parsedJson == null || parsedJson.Length < 1) return;
        }
        catch (JsonSerializationException)
        {
            // same here five.
            return;
        }

        var links = parsedJson[0]?.Assets?.Links;
        // same here six.
        if (links == null || links.Length < 1) return;

        string? latest_link = null;
        foreach (var link in links)
        {
            if (link?.FileName?.Contains("TotallyNotCef.zip") ?? false)
            {
                latest_link = link.Url;
                break;
            }
        }

        // same here seven.
        if (latest_link == null) return;

        try
        {
            LMeterLogger.Logger?.Info(latest_link);
            using var download_response = await _httpClient.GetAsync
            (
                latest_link,
                cancellationToken: _cancelTokenSource.Token
            );
            if (!download_response.IsSuccessStatusCode) return;

            using var streamToReadFrom = await download_response.Content.ReadAsStreamAsync(_cancelTokenSource.Token);
            using var zip = new ZipArchive(streamToReadFrom);
            zip.ExtractToDirectory(extractDir);
            LMeterLogger.Logger?.Info("Finished extracting TotallyNotCef");
        }
        catch (Exception e) when
        (
            e is OperationCanceledException ||
            e is TaskCanceledException ||
            e is HttpRequestException ||
            e is SocketException
        )
        {
            return;
        }
    }

    private bool DidWebBrowserLaunchSuccessfully()
    {
        return Process.GetProcessesByName("TotallyNotCef").Any();
    }

    private async Task StartTotallyNotCefProcess()
    {
        var isDefaultInstallLocation = _browserInstallFolder == MagicValues.DefaultTotallyNotCefInstallLocation;
        var cefExePath = Path.Join(_browserInstallFolder, "TotallyNotCef.exe");

        if (File.Exists(cefExePath))
        {
            if (!(await IsTotallyNotCefUpToDate(cefExePath)))
            {
                LMeterLogger.Logger?.Info("TotallyNotCef is out of date.");
                if (isDefaultInstallLocation)
                {
                    LMeterLogger.Logger?.Info("Updating...");
                    await DeleteTotallyNotCefInstall(_browserInstallFolder);
                    await DownloadTotallyNotCef(_browserInstallFolder);
                }
            }
            else
            {
                LMeterLogger.Logger?.Info("TotallyNotCef is up to date.");
            }
        }
        else if (isDefaultInstallLocation)
        {
            await DownloadTotallyNotCef(_browserInstallFolder);
        }

        WebBrowserState = TotallyNotCefBrowserState.Starting;
        ProcessLauncher.LaunchTotallyNotCef(cefExePath, _cactbotUrl, _httpPort, _enableAudio, _bypassWebSocket);
        if (DidWebBrowserLaunchSuccessfully())
        {
            WebBrowserState = TotallyNotCefBrowserState.Running;
        }
    }

    private async Task PollCactbot(bool autoStartBackgroundWebBrowser)
    {
        if (autoStartBackgroundWebBrowser)
        {
            // await StartTotallyNotCefProcess();
        }

        LastHealthResponse = await CheckIfPortBelongsToTotallyNotCef();
        if (_bypassWebSocket && LastHealthResponse == TotallyNotCefHealthCheckResponse.CorrectResponse)
        {
            ConnectionState = TotallyNotCefConnectionState.AttemptingHandshake;
            _iinactCactbotClient.Start();
            ConnectionState = TotallyNotCefConnectionState.WaitingForConnection;
        }

        while (!_cancelTokenSource.IsCancellationRequested)
        {
            try
            {
                if (PluginManager.Instance?.CactbotConfig?.EnableConnection ?? false)
                {
                    if (_browserNeedsReload)
                    {
                        try
                        {
                            _httpClient
                                .GetAsync(_httpUrl + "/reload", cancellationToken: _cancelTokenSource.Token)
                                .GetAwaiter()
                                .GetResult();
                        }
                        catch (Exception e) when
                        (
                            e is OperationCanceledException ||
                            e is TaskCanceledException ||
                            e is HttpRequestException ||
                            e is SocketException
                        ) { }
                        _browserNeedsReload = false;
                        _parsedResponse = null;
                    }

                    await GetCactbotHtml();
                }
                else
                {
                    _parsedResponse = null;
                }

                if
                (
                    ConnectionState != TotallyNotCefConnectionState.Disabled &&
                    ConnectionState != TotallyNotCefConnectionState.WaitingForConnection &&
                    ConnectionState != TotallyNotCefConnectionState.BadConnectionHealth
                )
                {
                    LastPollSuccessful = _parsedResponse != null;
                    ConnectionState = LastPollSuccessful
                        ? TotallyNotCefConnectionState.Connected
                        : TotallyNotCefConnectionState.Disconnected;
                }

                await Task.Delay(PollingRate, _cancelTokenSource.Token);
            }
            catch (Exception e) when
            (
                e is OperationCanceledException ||
                e is TaskCanceledException
            )
            {
                // Do not crash
            }
        }
    }

    public void StartBackgroundPollingThread(bool autoStartBackgroundWebBrowser)
    {
        if (BackgroundThreadRunning) return;
        BackgroundThreadRunning = true;
        ThreadPool.QueueUserWorkItem
        (
            _ =>
            {
                PollCactbot(autoStartBackgroundWebBrowser).GetAwaiter().GetResult();
            }
        );
    }

    public void SendShutdownCommand()
    {
        try
        {
            _httpClient
                .GetAsync(_httpUrl + "/shutdown", cancellationToken: _cancelTokenSource.Token)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception e) when
        (
            e is OperationCanceledException ||
            e is TaskCanceledException ||
            e is HttpRequestException ||
            e is SocketException
        ) { }
    }

    public void KillWebBrowserProcess()
    {
        foreach (var process in Process.GetProcessesByName("TotallyNotCef"))
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // Do not crash
            }
        }

        WebBrowserState = TotallyNotCefBrowserState.NotStarted;
    }

    public void ReloadBrowser()
    {
        _browserNeedsReload = true;
    }

    public void Dispose()
    {
        SendShutdownCommand();

        try
        {
            _cancelTokenSource.Cancel();
        }
        catch { }

        try
        {
            _iinactCactbotClient.Dispose();
        }
        catch { }

        KillWebBrowserProcess();
    }
}

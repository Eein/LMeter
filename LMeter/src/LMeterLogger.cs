using Dalamud.Plugin.Services;


namespace LMeter;

public static class LMeterLogger
{
    private static int? _associated_thread_id = null;
    private static IPluginLog? _logger = null;
    public static IPluginLog? Logger
    {
        get
        {
            if (System.Environment.CurrentManagedThreadId == _associated_thread_id)
            {
                return _logger;
            }

            return null;
        }
        set
        {
            _associated_thread_id = System.Environment.CurrentManagedThreadId;
            _logger = value;
        }
    }
}

using CustomCodeSystem.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomCodeSystem;


public static class AppState
{
    private static readonly object _lock = new();
    private static SessionInfoDto? _session;
    private static int _linkTaskId;
    private static int _actionTaskId;
    private static string _baseUrl;
    private static string _location;
    private static string _version = "v1.4";

    // SessionId
    public static void SetSession(SessionInfoDto session)
    {
        lock (_lock) _session = session;
    }

    public static string? GetSessionId()
    {
        lock (_lock) return _session?.Id;
    }

    // Link TaskId
    public static void SetLinkTaskId(int linkTaskId)
    {
        lock (_lock) _linkTaskId = linkTaskId;
    }

    public static int GetLinkTaskId()
    {
        lock (_lock) return _linkTaskId;
    }

    // Action TaskId
    public static void SetActionTaskId(int actionTaskId)
    {
        lock (_lock) _actionTaskId = actionTaskId;
    }

    public static int GetActionTaskId()
    {
        lock (_lock) return _actionTaskId;
    }

    // Base Url
    public static void SetBaseUrlLocation(string baseUrl, string location)
    {
        lock (_lock) _baseUrl = baseUrl;
        lock (_lock) _location = location;
    }

    public static string GetBaseUrl()
    {
        lock (_lock) return _baseUrl;
    }

    public static string GetLocation()
    {
        lock (_lock) return _location;
    }

    public static string GetVersion()
    {
        lock (_lock) return _version;
    }

    // получить sessionId (если не установлен — бросит исключение)
    public static string GetSessionIdOrThrow()
    {
        lock (_lock)
        {
            return _session?.Id ?? throw new InvalidOperationException("Session is not set.");
        }
    }

    public static void Clear()
    {
        lock (_lock)
        {
            _session = null;
            _linkTaskId = 0;
            _actionTaskId = 0;
            _location = null;
            _baseUrl = null;
        }
    }

}

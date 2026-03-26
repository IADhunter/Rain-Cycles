using UnityEngine;

namespace Plugin;

public partial class DevTools
{
    public static void Init() { }

    public static void Log(string message)
        => UnityEngine.Debug.Log($"[{RSPlugin.NAME}] {message}");

    public static void LogWarn(string message)
        => UnityEngine.Debug.Log($"[Warn {RSPlugin.NAME}] {message}");

    public static void LogErr(string message)
        => UnityEngine.Debug.Log($"[ERROR {RSPlugin.NAME}] {message}");
}
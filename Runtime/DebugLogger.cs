using UnityEngine;

public static class DebugLogger
{
    public static void Log(string message)
    {
#if UNITY_EDITOR || ENABLE_LOGS
        Debug.Log($"<color=white>{message} | CASUAL LOGGER</color>");
#endif
    }

    public static void LogError(string message)
    {
#if UNITY_EDITOR || ENABLE_LOGS
        Debug.LogError(message + " | CASUAL LOGGER");
#endif
    }

    public static void Log(string message, Color color)
    {
    #if UNITY_EDITOR || ENABLE_LOGS
        string colorHex = ColorUtility.ToHtmlStringRGBA(color);
        Debug.Log($"<color=#{colorHex}>{message} | CASUAL LOGGER</color>");
    #endif
    }
}
public static class CustomColors
{
    public static readonly Color Orange = new Color(1f, 0.5f, 0f, 1f);
    public static readonly Color Pink = new Color(1f, 0.41f, 0.71f, 1f);
    public static readonly Color LightGreen = new Color(0.56f, 0.93f, 0.56f, 1f);
    public static readonly Color LightBlue = new Color(0.68f, 0.85f, 0.9f, 1f);
    public static readonly Color LightYellow = new Color(1f, 1f, 0.5f, 1f);
    public static readonly Color LightPurple = new Color(0.87f, 0.63f, 0.87f, 1f);
    public static readonly Color SeaGreen = new Color(0.18f, 0.55f, 0.34f, 1f);
    public static readonly Color Teal = new Color(0.0f, 0.5f, 0.5f, 1f);
    public static readonly Color Green = new Color(0.0f, 1f, 0.0f, 1f);
    public static readonly Color Red = new Color(1f, 0.0f, 0.0f, 1f);
    public static readonly Color Purple = new Color(0.5f, 0.0f, 0.5f, 1f);
    public static readonly Color White = new Color(1f, 1f, 1f, 1f);
    // Add more custom colors as needed
}
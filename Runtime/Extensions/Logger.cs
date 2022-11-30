using UnityEngine;

namespace JFramework
{
    public static class Logger
    {
        public static void Log(string message)
        {
            Debug.Log("[JFramework] " + message);
        }

        public static void LogWarning(string message)
        {
            Debug.LogWarning("[JFramework] " + message);
        }

        public static void LogError(string message)
        {
            Debug.LogError("[JFramework] " + message);
        }
    }
}
using System;
using BepInEx.Logging;

namespace Uuvr.Core
{
    /// <summary>
    /// Readable static logger with clear separation between Unity and BepInEx outputs.
    /// - Unity: call methods without prefix: Info/Warn/Error/Debug/Exception
    /// - BepInEx: call methods prefixed with 'B': BInfo/BWarn/BError/BDebug/BException
    /// Wire BepInEx via SetBepInExLog(...) once on startup.
    /// </summary>
    public static class Log
    {
        private static ManualLogSource _bepInEx;

        public static void SetBepInExLog(ManualLogSource log)
        {
            _bepInEx = log;
        }

        
        // ---------- Unity (no prefix) ----------
        public static void Debug(string msg)
        {
            UnityEngine.Debug.Log(msg);
        }
        
        public static void Info(string msg)
        {
            UnityEngine.Debug.Log(msg);
        }
        
        public static void Warn(string msg)
        {
            UnityEngine.Debug.LogWarning(msg);
        }
        
        public static void Error(string msg)
        {
            UnityEngine.Debug.LogError(msg);
        }
        
        public static void Exception(Exception? ex)
        {
            if (ex != null)
                UnityEngine.Debug.LogException(ex);
            else
                UnityEngine.Debug.LogError("<null exception>");
        }
        
        public static void Exception(string msg, Exception? ex)
        {
            UnityEngine.Debug.LogError(msg);
            if (ex != null)
                UnityEngine.Debug.LogException(ex);
        }

        
        // ---------- BepInEx (B-prefix) ----------
        public static void BInfo(string msg)
        {
            _bepInEx.LogInfo(msg);
        }
        
        public static void BWarn(string msg)
        {
            _bepInEx.LogWarning(msg);
        }
        
        public static void BError(string msg)
        {
            _bepInEx.LogError(msg);
        }
        
        public static void BDebug(string msg)
        {
            _bepInEx.LogDebug(msg);
        }
        
        public static void BException(Exception ex)
        {
            _bepInEx.LogError(FormatException(ex));
        }
        
        public static void BException(string msg, Exception ex)
        {
            _bepInEx.LogError(msg + "\n" + FormatException(ex));
        }

        
        private static string FormatException(Exception? ex)
        {
            if (ex == null)
                return "<null exception>";

            return ex.ToString();
        }
    }
}

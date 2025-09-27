// MIT License
//
// Copyright (c) 2022 SirRandoo
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
/*
 * Key Changes Made:
 * - Added ToolkitUtils prefix similar to ToolkitCore and TwitchToolkit
 * - Removed reflection and directly access ToolkitCoreSettings.enableDebugLogging
 * - Simplified debug logic since we can directly check the setting
 * - Maintained debug build detection for development
 */

using System;
using System.Diagnostics;
using System.Reflection;
using ToolkitCore;
using UnityEngine;
using Verse;

namespace SirRandoo.ToolkitUtils;

public class RimLogger(string name)
{
    private bool _debugChecked;
    private bool _debugEnabled;
    private const string Prefix = "<color=#FFA500>[ToolkitUtils]</color>"; // Orange color for ToolkitUtils

    public string FormatMessage(string message) => $"{Prefix} {name} :: {message}";

    public string FormatMessage(string level, string message) => $"{Prefix} {level.ToUpperInvariant()} {name} :: {message}";

    public string FormatMessage(string level, string message, string color) => $@"{Prefix} <color=""#{color.TrimStart('#')}"">{level.ToUpperInvariant()} {name} :: {message}</color>";

    public virtual void Log(string message)
    {
        LogInternal(FormatMessage(message));
    }

    public virtual void Info(string message)
    {
        LogInternal(FormatMessage("INFO", message));
    }

    public virtual void Warn(string message)
    {
        LogInternal(FormatMessage("WARN", message, "FF6B00")); // Orange color for warnings
    }

    public virtual void Error(string message)
    {
        LogInternal(FormatMessage("ERR", message, "FF768C")); // Pink-red color for errors
        Verse.Log.TryOpenLogWindow();
    }

    public virtual void Error(string message, Exception exception)
    {
        Error($"{message} :: {exception.GetType().Name}({exception.Message})\n\n{exception.ToStringSafe()}");
    }

    public virtual void Debug(string message)
    {
        //LogInternal(FormatMessage("DEBUG", $"RimLogger Debug check: debugChecked={_debugChecked}, debugEnabled={_debugEnabled}, enableDebugLogging={ToolkitCoreSettings.enableDebugLogging}", ColorUtility.ToHtmlStringRGB(ColorLibrary.LightPink)));
        
        if (!_debugChecked)
        {
            _debugEnabled = Assembly.GetCallingAssembly().GetCustomAttribute<DebuggableAttribute>()?.DebuggingFlags
                == DebuggableAttribute.DebuggingModes.DisableOptimizations;
            _debugChecked = true;
        }

        // Show debug messages if: 
        // 1. It's a debug build OR 
        // 2. ToolkitCore's debug logging is enabled
        if (_debugEnabled || ToolkitCoreSettings.enableDebugLogging)
        {
            LogInternal(FormatMessage("DEBUG", message, ColorUtility.ToHtmlStringRGB(ColorLibrary.LightPink)));
        }
    }

    protected virtual void LogInternal(string message)
    {
        Verse.Log.Message(message);
    }
}

[StaticConstructorOnStartup]
internal sealed class RimThreadedLogger(string name) : RimLogger(name)
{
    /// <inheritdoc />
    public override void Log(string message)
    {
        if (UnityData.IsInMainThread)
        {
            base.Log(message);
        }
        else
        {
            TkUtils.Context.Post(o => base.Log(message), null);
        }
    }

    /// <inheritdoc />
    public override void Error(string message)
    {
        if (UnityData.IsInMainThread)
        {
            base.Error(message);
        }
        else
        {
            TkUtils.Context.Post(o => base.Error(message), null);
        }
    }
}
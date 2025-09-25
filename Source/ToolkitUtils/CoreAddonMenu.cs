// ToolkitUtils
// Copyright (C) 2021  SirRandoo
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

/*
 * File: CoreAddonMenu.cs
 * 
 * Updated code using Deepseek AI 
 * Date: September 17, 2025
 * 
 * From the AI:
 * 
 * Key Changes:
 * 1. Replaced Task.Run with LongEventHandler.QueueLongEvent:
 * 2. This uses RimWorld's native threading system, which is safer for modding and avoids potential conflicts with the game's main thread .
 * 3. Added a dedicated method ReconnectTwitchWrapper:
 * 4. This method contains the reconnection logic and error handling, making the code cleaner and more maintainable.
 *  
 * Error handling:
 * 1. The QueueLongEvent call includes an error handler that logs any exceptions thrown during the reconnection process.
 * 2. The ReconnectTwitchWrapper method also has a try-catch block to handle errors gracefully and ensure they are logged.
 * 
 * Why This Approach is Better:
 * 1. RimWorld Compatibility: Uses LongEventHandler, which is integrated with RimWorld's threading model and ensures background tasks do not interfere with the game's stability .
 * 2. Error Handling: Both the queued event and the internal method have error handling to log issues, making debugging easier.
 * 3. Code Clarity: Separating the reconnection logic into its own method improves readability and maintainability.
 * 
 */
using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using SirRandoo.ToolkitUtils.Windows;
using ToolkitCore;
using ToolkitCore.Interfaces;
using ToolkitCore.Windows;
using UnityEngine;
using Verse;

namespace SirRandoo.ToolkitUtils;

/// <summary>
///     An <see cref="IAddonMenu"/> used by ToolkitCore to display a set
///     of "quick menu options" for users.
/// </summary>
[UsedImplicitly]
public class CoreAddonMenu : IAddonMenu
{
    private static readonly List<FloatMenuOption> Options =
    [
        new FloatMenuOption("TKUtils.AddonMenu.Settings".TranslateSimple(), () => Find.WindowStack.Add(new CoreSettingsWindow())),
        new FloatMenuOption("Message Log", () => Find.WindowStack.Add(new Window_MessageLog())),
        new FloatMenuOption("Help", () => Application.OpenURL("https://github.com/hodldeeznuts/ToolkitCore/wiki")),
        new FloatMenuOption(
            "TKUtils.AddonMenu.Reconnect".TranslateSimple(),
            () => LongEventHandler.QueueLongEvent(
                ReconnectTwitchWrapper,
                "TKUtils.ReconnectTwitch",
                false,
                exception => TkUtils.Logger.Error("Encountered an error during Twitch reconnection: " + exception)
            )
        )
    ];
    /// <inheritdoc cref="IAddonMenu.MenuOptions"/>
    public List<FloatMenuOption> MenuOptions() => Options;
    /// <summary>
    ///     Reconnects the TwitchWrapper client. This method is designed to be run on a background thread
    ///     via LongEventHandler to avoid disrupting the main game thread.
    /// </summary>
    private static void ReconnectTwitchWrapper()
    {
        try
        {
            if (TwitchWrapper.Client == null || !TwitchWrapper.Client.IsConnected)
            {
                TwitchWrapper.StartAsync();
                return;
            }
            TwitchWrapper.Client.Disconnect();
            TwitchWrapper.StartAsync();
        }
        catch (Exception e)
        {
            TkUtils.Logger.Error("Encountered an error while reconnecting to Twitch: ", e);
            throw; // Re-throw to ensure the error handler in QueueLongEvent is triggered
        }
    }
}
/**
public class CoreAddonMenu : IAddonMenu
{
    private static readonly List<FloatMenuOption> Options =
    [
        new FloatMenuOption("TKUtils.AddonMenu.Settings".TranslateSimple(), () => Find.WindowStack.Add(new CoreSettingsWindow())),
        new FloatMenuOption("Message Log", () => Find.WindowStack.Add(new Window_MessageLog())),
        new FloatMenuOption("Help", () => Application.OpenURL("https://github.com/hodldeeznuts/ToolkitCore/wiki")),
        new FloatMenuOption(
            "TKUtils.AddonMenu.Reconnect".TranslateSimple(),
            () => Task.Run(
                () =>
                {
                    if (TwitchWrapper.Client == null || !TwitchWrapper.Client.IsConnected)
                    {
                        TwitchWrapper.StartAsyncStatic();

                        return;
                    }

                    try
                    {
                        TwitchWrapper.Client.Disconnect();
                    }
                    catch (Exception e)
                    {
                        TkUtils.Logger.Error("Encountered an error while disconnected from Twitch -- You can probably ignore this.", e);
                    }

                    TwitchWrapper.StartAsyncStatic();
                }
            )
        )
    ];

    /// <inheritdoc cref="IAddonMenu.MenuOptions"/>
    public List<FloatMenuOption> MenuOptions() => Options;
}
**/
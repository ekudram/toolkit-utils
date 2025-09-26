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
//
/*
 * File: CommandRouter.cs
 * Usage: Part of ToolkitUtils
 * 
 * 
 */

using JetBrains.Annotations;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TwitchToolkit;
using Verse;

namespace SirRandoo.ToolkitUtils;

/// <summary>
///     A <see cref="GameComponent"/> responsible for scheduling commands
///     out of the Twitch thread, as well as executing any actions that
///     may need to be run on the Unity thread.
/// </summary>
[UsedImplicitly]
public class CommandRouter : GameComponent
{
    // private static Task? _interfaceTask; // Add nullable modifier
    public static readonly ConcurrentQueue<TwitchMessageWrapper> CommandQueue = new ConcurrentQueue<TwitchMessageWrapper>();
    public static readonly ConcurrentQueue<Action> MainThreadCommands = new ConcurrentQueue<Action>();

    public CommandRouter(Game game)
    {
    }

    /// <inheritdoc cref="GameComponent.LoadedGame"/>
    public override void LoadedGame()
    {
        CommandQueue.Clear();
    }

    /// <inheritdoc cref="GameComponent.GameComponentUpdate"/>
    /// <inheritdoc cref="GameComponent.GameComponentUpdate"/>
    public override void GameComponentUpdate()
    {
        ProcessCommands();

        if (!TkSettings.CommandRouter)
        {
            return;
        }

        // Remove all task checking code since we're not using tasks anymore
        ProcessCommandQueue();
    }

    private static void ProcessCommandQueue()
    {
        List<TwitchToolkit.TwitchInterfaceBase>? interfaces = null;

        while (!CommandQueue.IsEmpty)
        {
            // Change to non-nullable and handle the null check properly
            if (!CommandQueue.TryDequeue(out TwitchMessageWrapper message) || message == null)
            {
                TkUtils.Logger.Warn("Failed to dequeue message from CommandQueue.");
                break;
            }

            if (string.IsNullOrEmpty(message.Username) || string.IsNullOrEmpty(message.Message))
            {
                TkUtils.Logger.Warn("Dequeued message has null or empty Username or Message.");
                continue;
            }

            interfaces ??= Current.Game.components.OfType<TwitchToolkit.TwitchInterfaceBase>().ToList();

            foreach (TwitchToolkit.TwitchInterfaceBase @interface in interfaces)
            {
                // Remove the redundant type check - @interface is already the correct type
                try
                {
                    LongEventHandler.QueueLongEvent(
                        () => @interface.ParseMessage(message),
                        "ProcessingTwitchCommand",
                        doAsynchronously: true,
                        exceptionHandler: null
                    );
                }
                catch (Exception ex)
                {
                    TkUtils.Logger.Error($"Error processing message from {message.Username}: {ex}");
                }
            }
        }
    }

    private static void ProcessCommands()
    {
        while (!MainThreadCommands.IsEmpty)
        {
            if (!MainThreadCommands.TryDequeue(out Action action))
            {
                break;
            }

            try
            {
                action();
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }
}

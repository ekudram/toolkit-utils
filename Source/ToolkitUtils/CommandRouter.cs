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
    /// <summary>
    ///    Clears the command queue when a game is loaded.
    /// </summary>
    /// <inheritdoc cref="GameComponent.LoadedGame"/>
    public override void LoadedGame()
    {
        CommandQueue.Clear();
    }
    /// <summary>
    /// Updates the game component by processing commands and managing the command queue.
    /// </summary>
    /// <remarks>This method processes incoming commands and, if the command router setting is enabled, processes the
    /// command queue. It ensures that commands are handled appropriately during the game component's update
    /// cycle.</remarks>
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
    /// <summary>
    /// Processes all messages in the command queue, dispatching them to the appropriate Twitch interfaces for handling.
    /// </summary>
    /// <remarks>This method dequeues messages from the command queue and forwards them to all active
    /// instances of  <see cref="TwitchInterfaceBase"/> for processing. Messages with null or empty usernames or content
    /// are skipped.  Any exceptions encountered during message processing are logged, and processing continues with the
    /// next message.</remarks>
    
    private static void ProcessCommandQueue()
    {
        /// Cache the list of interfaces to avoid repeated calls to OfType and ToList
        List<TwitchInterfaceBase>? interfaces = null;
        /// Process all messages in the command queue
        while (!CommandQueue.IsEmpty)
        {
            TkUtils.Logger.Debug($"Processing CommandQueue with {CommandQueue.Count} messages.");

            // Change to non-nullable and handle the null check properly
            if (!CommandQueue.TryDequeue(out TwitchMessageWrapper message) || message == null)
            {
                TkUtils.Logger.Warn("Failed to dequeue message from CommandQueue.");
                break;
            }
            /// Skip messages with null or empty usernames or content
            if (string.IsNullOrEmpty(message.Username) || string.IsNullOrEmpty(message.Message))
            {
                TkUtils.Logger.Warn("Dequeued message has null or empty Username or Message.");
                continue;
            }
            /// Cache the interfaces list if it hasn't been cached yet
            interfaces ??= Current.Game.components.OfType<TwitchInterfaceBase>().ToList();
            /// If no interfaces are found, log a warning and exit the loop
            foreach (TwitchInterfaceBase @interface in interfaces)
            {
                TkUtils.Logger.Debug($"Queueing message from {message.Username} to {@interface.GetType().Name}.");
                // Remove the redundant type check - @interface is already the correct type
                try
                {
                    TkUtils.Logger.Debug($"Parsing message from {message.Username}: {message.Message}");
                    LongEventHandler.QueueLongEvent(
                        () => @interface.ParseMessage(message),
                        null,
                        false,
                        exceptionHandler: null,
                        true
                    );
                }
                catch (Exception ex)
                {
                    TkUtils.Logger.Error($"Error processing message from {message.Username}: {ex}");
                }
            }
        }
    }
    /// <summary>
    /// Processes and executes all pending commands in the main thread command queue.
    /// </summary>
    /// <remarks>This method dequeues and executes actions from the <see cref="MainThreadCommands"/> queue 
    /// until the queue is empty. If an exception occurs during the execution of a command,  the exception is caught and
    /// ignored, allowing subsequent commands to continue processing.</remarks>
    private static void ProcessCommands()
    {
        /// Execute all actions in the main thread command queue
        while (!MainThreadCommands.IsEmpty)
        {
            /// Dequeue the next action
            if (!MainThreadCommands.TryDequeue(out Action action))
            {
                break;
            }
            /// Execute the action, ignoring any exceptions
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

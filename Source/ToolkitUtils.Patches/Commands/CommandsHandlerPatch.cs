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
// File: Source/ToolkitUtils/Patches/Commands/CommandsHandlerPatch.cs
// Project: ToolkitUtils
// Usage: Patch to modify Twitch Toolkit and Core command handling


using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using SirRandoo.ToolkitUtils.Helpers;
using ToolkitCore.Utilities;
using TwitchLib.Client.Models;
using TwitchToolkit;
using Verse;
using Command = TwitchToolkit.Command;

namespace SirRandoo.ToolkitUtils.Patches;

/// <summary>
///     A Harmony patch for adjusting how Twitch Toolkit's command
///     parsing code is performed. This patch is responsible for
///     performing case insensitive comparisons against command names, as
///     well as powering the <see cref="TkSettings.BuyPrefix"/> code.
/// </summary>
[HarmonyPatch]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal static class CommandsHandlerPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        TkUtils.Logger.Debug("[TKUtils] CommandsHandlerPatch.TargetMethods called.");

        yield return AccessTools.Method(typeof(CommandsHandler), nameof(CommandsHandler.CheckCommand));
    }

    private static Exception? Cleanup(MethodBase original, Exception? exception)
    {
        if (exception == null)
        {
            TkUtils.Logger.Debug($"[TKUtils] Successfully patched {original.FullDescription()}");
            return null;
        }
        TkUtils.Logger.Error($"Could not patch {original.FullDescription()} -- Things will not work properly!", exception.InnerException ?? exception);
        return null;
    }
    private static bool Prefix(TwitchMessageWrapper? messageWrapper)
    {
        TkUtils.Logger.Debug($"[TKUtils] CommandsHandlerPatch.Prefix started. TkSettings.Commands is {TkSettings.Commands}");

        if (!TkSettings.Commands || messageWrapper == null || string.IsNullOrEmpty(messageWrapper.Message) || string.IsNullOrEmpty(messageWrapper.Username))
        {
            return !TkSettings.Commands;
        }

        Viewer viewer = Viewers.GetViewer(messageWrapper.Username);
        viewer.last_seen = DateTime.Now;

        if (viewer.IsBanned)
        {
            return false;
        }

        string? sanitized = GetCommandString(messageWrapper.Message);

        if (sanitized is null)
        {
            return false;
        }

        List<string> segments = CommandFilter.Parse(sanitized).ToList();
        bool text = segments.Any(i => i.EqualsIgnoreCase("--text"));

        if (segments.Count <= 0)
        {
            return false;
        }

        if (text)
        {
            segments = segments.Where(i => !i.EqualsIgnoreCase("--text")).ToList();
        }
        string commandText = "!" + CombineSegments(segments).Trim();
        LocateCommand(segments.ToArray())?.Execute(messageWrapper, text);

        return false;
    }
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static Exception? Finalizer(Exception? __exception)
    {
        TkUtils.Logger.Debug("[TKUtils] CommandsHandlerPatch.Finalizer finished.");
        if (__exception != null)
        {
            TkUtils.Logger.Error("Command parser encountered an error", __exception);
        }

        return null;
    }

    private static string CombineSegments(IEnumerable<string> segments)
    {
        TkUtils.Logger.Debug($"[TKUtils] Combining segments: {string.Join(", ", segments)}");

        return string.Join(" ", segments.Select(s => s.Contains(' ') ? $@"""{s.Replace("\"", "\\\"")}""" : s).ToArray());
    }

    private static Command? LocateCommand(string[] query)
    {
        TkUtils.Logger.Debug($"[TKUtils] Locating command from segments: {string.Join(", ", query)}");

        foreach (Command commandDef in DefDatabase<Command>.AllDefs.Where(c => c.enabled))
        {
            if (commandDef.command.Contains(" "))
            {
                int spaces = commandDef.command.Count(c => c.Equals(' '));
                string joined = string.Join(" ", query.Take(spaces));

                if (!IsCommand(commandDef.command, joined))
                {
                    continue;
                }

                return commandDef;
            }

            if (!IsCommand(commandDef.command, query.Take(1).First()))
            {
                continue;
            }

            return commandDef;
        }

        return null;
    }

    private static bool IsCommand(string command, string input)
    {   
        TkUtils.Logger.Debug($"[TKUtils] Comparing command '{command}' to input '{input}'");

        if (TkSettings.ToolkitStyleCommands && input.StartsWith(command, StringComparison.InvariantCultureIgnoreCase))
        {
            return true;
        }

        return input.EqualsIgnoreCase(command);
    }

    private static string? GetCommandString(string message)
    {
        TkUtils.Logger.Debug($"[TKUtils] Getting command string from message: {message}");

        if (message.StartsWith("/w"))
        {
            message = message[3..];
        }

        if (message.StartsWith(TkSettings.Prefix, StringComparison.InvariantCultureIgnoreCase))
        {
            return message[TkSettings.Prefix.Length..];
        }

        return message.StartsWith(TkSettings.BuyPrefix, StringComparison.InvariantCultureIgnoreCase)
            ? $"{CommandDefOf.Buy.command} {message[TkSettings.BuyPrefix.Length..]}"
            : null;
    }
}

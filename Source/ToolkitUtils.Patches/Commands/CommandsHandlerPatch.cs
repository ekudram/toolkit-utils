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
        LogSettings();

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

        TkUtils.Logger.Debug($"CommandsHandlerPatch: Before LocateCommand - sanitized = '{sanitized}'");
        TkUtils.Logger.Debug($"CommandsHandlerPatch: Segments count = {segments.Count}");
        TkUtils.Logger.Debug($"CommandsHandlerPatch: Segments = [{string.Join(", ", segments)}]");

        string commandText = "!" + CombineSegments(segments).Trim();
        Command locatedCommand = LocateCommand(segments.ToArray());

        TkUtils.Logger.Debug($"CommandsHandlerPatch: Located command = {locatedCommand?.defName} ('{locatedCommand?.command}')");
        TkUtils.Logger.Debug($"CommandsHandlerPatch: Calling Execute with emojiOverride = {text}");

        // FIX: Create a new message wrapper with the processed command text
        TwitchMessageWrapper processedMessage = messageWrapper.WithMessage(commandText);
        locatedCommand?.Execute(processedMessage, text);

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

        // CRITICAL: Log all available commands in the database
        var allCommands = DefDatabase<Command>.AllDefs.ToList();
        TkUtils.Logger.Debug($"[TKUtils] Total commands in DefDatabase: {allCommands.Count}");
        TkUtils.Logger.Debug($"[TKUtils] Enabled commands: {allCommands.Count(c => c.enabled)}");

        foreach (Command cmd in allCommands)
        {
            TkUtils.Logger.Debug($"[TKUtils] Available command: '{cmd.command}' (defName: {cmd.defName}, enabled: {cmd.enabled})");
        }

        foreach (Command commandDef in allCommands.Where(c => c.enabled))
        {
            TkUtils.Logger.Debug($"[TKUtils] Checking command: '{commandDef.command}' (defName: {commandDef.defName})");

            if (commandDef.command.Contains(" "))
            {
                int spaces = commandDef.command.Count(c => c.Equals(' '));
                string joined = string.Join(" ", query.Take(spaces));

                TkUtils.Logger.Debug($"[TKUtils] Multi-word command - spaces: {spaces}, joined: '{joined}'");

                if (!IsCommand(commandDef.command, joined))
                {
                    TkUtils.Logger.Debug($"[TKUtils] Multi-word command '{commandDef.command}' doesn't match '{joined}'");
                    continue;
                }

                TkUtils.Logger.Debug($"[TKUtils] FOUND multi-word command: '{commandDef.command}'");
                return commandDef;
            }

            string firstSegment = query.Take(1).First();
            TkUtils.Logger.Debug($"[TKUtils] Single-word command - comparing '{commandDef.command}' to '{firstSegment}'");

            if (!IsCommand(commandDef.command, firstSegment))
            {
                TkUtils.Logger.Debug($"[TKUtils] Single-word command '{commandDef.command}' doesn't match '{firstSegment}'");
                continue;
            }

            TkUtils.Logger.Debug($"[TKUtils] FOUND single-word command: '{commandDef.command}'");
            return commandDef;
        }

        TkUtils.Logger.Debug($"[TKUtils] No command found for segments: {string.Join(", ", query)}");
        return null;
    }

    private static bool IsCommand(string command, string input)
    {
        TkUtils.Logger.Debug($"Comparing command '{command}' to input '{input}'");

        // Remove the ! prefix if present for comparison
        string cleanCommand = command.StartsWith("!") ? command.Substring(1) : command;
        string cleanInput = input.StartsWith("!") ? input.Substring(1) : input;

        TkUtils.Logger.Debug($"Cleaned - command: '{cleanCommand}', input: '{cleanInput}'");

        if (TkSettings.ToolkitStyleCommands && cleanInput.StartsWith(cleanCommand, StringComparison.InvariantCultureIgnoreCase))
        {
            TkUtils.Logger.Debug($"ToolkitStyleCommands match: '{cleanInput}' starts with '{cleanCommand}'");
            return true;
        }

        bool exactMatch = cleanInput.EqualsIgnoreCase(cleanCommand);
        TkUtils.Logger.Debug($"Exact match: {exactMatch}");

        return exactMatch;
    }

    private static string? GetCommandString(string message)
    {
        TkUtils.Logger.Debug($"Getting command string from message: {message}");
        TkUtils.Logger.Debug($"TkSettings.Prefix: '{TkSettings.Prefix}', TkSettings.BuyPrefix: '{TkSettings.BuyPrefix}'");

        // Fix autocorrect spacing issue: "item [specification]" -> "item[specification]"
        message = message.Replace(" [", "[");
        TkUtils.Logger.Debug($"After space fix: {message}");

        if (message.StartsWith("/w"))
        {
            message = message[3..];
            TkUtils.Logger.Debug($"After /w removal: {message}");
        }

        if (message.StartsWith(TkSettings.Prefix, StringComparison.InvariantCultureIgnoreCase))
        {
            string result = message[TkSettings.Prefix.Length..];
            TkUtils.Logger.Debug($"Prefix match, returning: '{result}'");
            return result;
        }

        if (message.StartsWith(TkSettings.BuyPrefix, StringComparison.InvariantCultureIgnoreCase))
        {
            string result = $"{CommandDefOf.Buy.command} {message[TkSettings.BuyPrefix.Length..]}";
            TkUtils.Logger.Debug($"BuyPrefix match, returning: '{result}'");
            return result;
        }

        TkUtils.Logger.Debug("No prefix match, returning null");
        return null;
    }

    // Temporary method to log settings
    private static void LogSettings()
    {
        TkUtils.Logger.Debug($"[TKUtils] Current Settings:");
        TkUtils.Logger.Debug($"[TKUtils] - Prefix: '{TkSettings.Prefix}'");
        TkUtils.Logger.Debug($"[TKUtils] - BuyPrefix: '{TkSettings.BuyPrefix}'");
        TkUtils.Logger.Debug($"[TKUtils] - ToolkitStyleCommands: {TkSettings.ToolkitStyleCommands}");
        TkUtils.Logger.Debug($"[TKUtils] - Commands: {TkSettings.Commands}");
    }
}

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

using System;
using JetBrains.Annotations;
using SirRandoo.ToolkitUtils.Models;
using TwitchLib.Client.Models;
using TwitchToolkit;
using UnityEngine;

namespace SirRandoo.ToolkitUtils.Helpers;

public static class CommandHelper
{
    public static void Execute(this Command command, TwitchMessageWrapper message, bool emojiOverride = false)
    {
        TkUtils.Logger.Debug($"CommandHelper.Execute started for command {command.command} (defName: {command.defName}) by user {message.Username}");
        if (command == null || message == null || string.IsNullOrEmpty(message.Message) || string.IsNullOrEmpty(message.Username))
        {
            TkUtils.Logger.Debug("CommandHelper.Execute exiting early due to null/empty values.");
            return;
        }
        if (command.requiresAdmin && !message.HasBadges("broadcaster"))
        {
            return;
        }

        if (command.requiresMod && !message.HasBadges("broadcaster", "moderator", "global_mod", "staff"))
        {
            return;
        }

        CommandItem item = Data.Commands.Find(c => string.Equals(c.DefName, command.defName));

        if (item != null && UsageService.IsOnCooldown(item, message.Username))
        {
            return;
        }

        if (emojiOverride)
        {
            bool emojis = TkSettings.Emojis;

            TkSettings.Emojis = false;
            ExecuteInternal(command, message);
            TkSettings.Emojis = emojis;
        }
        else
        {
            ExecuteInternal(command, message);
        }

        if (item != null)
        {
            UsageService.RecordUsage(item, message.Username);
        }
    }

    private static void ExecuteInternal(Command command, TwitchMessageWrapper message)
    {
        TkUtils.Logger.Debug($"CommandHelper.ExecuteInternal started for command {command.command} (defName: {command.defName}) by user {message.Username}");

        try
        {
            command.RunCommand(message);
        }
        catch (Exception e)
        {
            TkUtils.Logger.Error($@"Command ""{command.command}"" threw an exception!", e);

            Data.RegisterHealthReport(
                new HealthReport
                {
                    Message = $"""Command "{message.Message} ({command.command})" didn't execute successfully. Reason: {e.GetType().Name}({e.Message})""",
                    OccurredAt = DateTime.Now,
                    Reporter = "ToolkitUtils - Command Handler",
                    Type = HealthReport.ReportType.Error,
                    Stacktrace = StackTraceUtility.ExtractStringFromException(e)
                }
            );
        }
    }

    internal static string ValidatePrefix(string prefix)
    {
        TkUtils.Logger.Debug($"CommandHelper.ValidatePrefix started for prefix {prefix}");
        if (string.IsNullOrEmpty(prefix))
        {
            TkUtils.Logger.Debug("CommandHelper.ValidatePrefix exiting early due to null/empty prefix.");
            return string.Empty;
        }
        if (prefix.StartsWith("/") || prefix.StartsWith("."))
        {
            prefix = prefix[1..];
        }

        return prefix.Replace(" ", "");
    }

    internal static bool IsModerator(this Viewer viewer)
    {
        TkUtils.Logger.Debug($"CommandHelper.IsModerator started for viewer {viewer?.username ?? "null"}");

        if (viewer == null || string.IsNullOrEmpty(viewer.username))
        {
            TkUtils.Logger.Debug("[TKUtils] CommandHelper.IsModerator exiting early due to null/empty viewer or username.");
            return false;
        }
        if (viewer.mod)
        {
            return true;
        }

        if (ToolkitSettings.ViewerModerators == null)
        {
            return false;
        }

        return ToolkitSettings.ViewerModerators.TryGetValue(viewer.username, out bool state) && state;
    }
}

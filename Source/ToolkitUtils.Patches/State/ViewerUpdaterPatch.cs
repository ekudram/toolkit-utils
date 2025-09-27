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
 * File: Source/ToolkitUtils.Patches/State/ViewerUpdaterPatch.cs
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using SirRandoo.ToolkitUtils.Helpers;
using TwitchLib.Client.Models;
using TwitchToolkit;
using TwitchToolkit.PawnQueue;
using TwitchToolkit.Twitch;
using UnityEngine;
using Verse;

namespace SirRandoo.ToolkitUtils.Patches;

/// <summary>
///     A Harmony patch for populating viewer data from messages sent in
///     chat.
/// </summary>
[HarmonyPatch]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal static class ViewerUpdaterPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        // Be more specific about the method signature to avoid ambiguity
        yield return AccessTools.Method(typeof(ViewerUpdater), nameof(ViewerUpdater.ParseMessage), new[] { typeof(TwitchMessageWrapper) });
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
        TkUtils.Logger.Log($"[TKUtils] ViewerUpdaterPatch.Prefix called for {messageWrapper?.Username}");
        if (messageWrapper?.Message == null)
        {
            TkUtils.Logger.Debug("[TKUtils] ViewerUpdaterPatch.Prefix Message wrapper or message is null, skipping");
            return false;
        }

        Viewer viewer = Viewers.GetViewer(messageWrapper.Username);
        var component = Current.Game.GetComponent<GameComponentPawns>();

        ToolkitSettings.ViewerColorCodes[messageWrapper.Username.ToLowerInvariant()] = messageWrapper.ColorHex;

        if (TkSettings.HairColor && component.HasUserBeenNamed(messageWrapper.Username)
            && ColorUtility.TryParseHtmlString(messageWrapper.ColorHex, out Color hairColor))
        {
            Pawn pawn = component.PawnAssignedToUser(messageWrapper.Username);

            if (pawn?.story != null)
            {
                pawn.story.HairColor = hairColor;
            }
        }

        viewer.mod = messageWrapper.HasBadges("moderator", "broadcaster", "global_mod", "staff");
        viewer.subscriber = messageWrapper.HasBadges("subscriber", "founder");
        viewer.vip = messageWrapper.HasBadges("vip");

        TkUtils.Logger.Debug($"[TKUtils] Updated viewer badges for {messageWrapper.Username}: " +
                     $"mod={viewer.mod}, sub={viewer.subscriber}, vip={viewer.vip}");

        try
        {
            bool registered = Data.RegisterViewer(viewer.username);
            TkUtils.Logger.Warn($"Viewer registration for {viewer.username}: {(registered ? "success" : "already exists/failed")}");
        }
        catch (Exception ex)
        {
            TkUtils.Logger.Error($"Viewer registration error for {viewer.username}: {ex.Message}");
        }

        return false;
    }

    private static void UpdateBroadcasterData(Viewer viewer)
    {
        if (!TkSettings.BroadcasterCoinType.EqualsIgnoreCase("broadcaster"))
        {
            viewer.subscriber = TkSettings.BroadcasterCoinType.EqualsIgnoreCase("subscriber");
            viewer.mod = TkSettings.BroadcasterCoinType.EqualsIgnoreCase("moderator");
            viewer.vip = TkSettings.BroadcasterCoinType.EqualsIgnoreCase("vip");
        }
        else
        {
            viewer.subscriber = true;
            viewer.mod = true;
            viewer.vip = true;
        }
    }
}
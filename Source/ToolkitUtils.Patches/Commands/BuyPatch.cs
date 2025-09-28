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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using SirRandoo.ToolkitUtils.Helpers;
using TwitchLib.Client.Models;
using TwitchToolkit;
using TwitchToolkit.Commands.ViewerCommands;
using TwitchToolkit.Store;

namespace SirRandoo.ToolkitUtils.Patches;

/// <summary>
///     A Harmony patch for ensuring shortcut commands are properly
///     executed.
/// </summary>
[HarmonyPatch]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal static class BuyPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Buy), nameof(Buy.RunCommand));
    }

    private static Exception? Cleanup(MethodBase original, Exception? exception)
    {
        if (exception == null)
        {
            TkUtils.Logger.Debug($"BuyPatch Cleanup: Successfully patched {original.FullDescription()}");
            return null;
        }

        TkUtils.Logger.Error($"Could not patch {original.FullDescription()} -- Things will not work properly!", exception.InnerException ?? exception);

        return null;
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static bool Prefix(CommandDriver? __instance, TwitchMessageWrapper messageWrapper)
    {
        TkUtils.Logger.Debug($"=== BuyPatch.Prefix START ===");
        TkUtils.Logger.Debug($"BuyPatch: Username = {messageWrapper.Username}");
        TkUtils.Logger.Debug($"BuyPatch: Original message = '{messageWrapper.Message}'");
        TkUtils.Logger.Debug($"BuyPatch: __instance type = {__instance?.GetType().Name}");
        TkUtils.Logger.Debug($"BuyPatch: __instance.command.defName = {__instance?.command?.defName}");
        TkUtils.Logger.Debug($"BuyPatch: __instance.command.command = {__instance?.command?.command}");

        if (__instance == null)
        {
            TkUtils.Logger.Error("BuyPatch: CommandDriver instance is null!");
            TkUtils.Logger.Debug($"=== BuyPatch.Prefix END (null instance) ===");
            return true;
        }

        if (!TkSettings.StoreState)
        {
            TkUtils.Logger.Debug("BuyPatch: Store is disabled, skipping");
            TkUtils.Logger.Debug($"=== BuyPatch.Prefix END (store disabled) ===");
            return false;
        }

        Viewer viewer = Viewers.GetViewer(messageWrapper.Username);
        TkUtils.Logger.Debug($"BuyPatch: Viewer found = {viewer?.username}");
        TkUtils.Logger.Debug($"BuyPatch: Viewer coins = {viewer?.coins}");

        TwitchMessageWrapper processedMessage = messageWrapper;

        // Check if this is a shortcut command that needs to be converted to a buy command
        bool isShortcutCommand = !__instance.command.defName.Equals("Buy");
        TkUtils.Logger.Debug($"BuyPatch: Is shortcut command? {isShortcutCommand}");

        if (isShortcutCommand)
        {
            TkUtils.Logger.Debug("BuyPatch: Not a Buy command, checking for shortcut");
            string newMessage = $"!{CommandDefOf.Buy.command} {messageWrapper.Message.Substring(1)}";
            processedMessage = messageWrapper.WithMessage(newMessage);
            TkUtils.Logger.Debug($"BuyPatch: Converted shortcut message = '{processedMessage.Message}'");
        }

        // Check if the message has enough segments for a purchase
        string[] messageSegments = processedMessage.Message.Split(' ');
        TkUtils.Logger.Debug($"BuyPatch: Message segments count = {messageSegments.Length}");
        TkUtils.Logger.Debug($"BuyPatch: Message segments = [{string.Join(", ", messageSegments)}]");

        if (messageSegments.Length < 2)
        {
            TkUtils.Logger.Debug("BuyPatch: Message has insufficient segments, skipping purchase");
            TkUtils.Logger.Debug($"=== BuyPatch.Prefix END (insufficient segments) ===");
            return false;
        }

        TkUtils.Logger.Debug($"BuyPatch: Calling Purchase_Handler.ResolvePurchase for '{processedMessage.Message}'");

        try
        {
            Purchase_Handler.ResolvePurchase(viewer, processedMessage);
            TkUtils.Logger.Debug("BuyPatch: Purchase_Handler.ResolvePurchase completed successfully");
        }
        catch (Exception ex)
        {
            TkUtils.Logger.Error($"BuyPatch: Purchase_Handler.ResolvePurchase threw an exception: {ex}");
        }

        TkUtils.Logger.Debug($"=== BuyPatch.Prefix END ===");
        return false;
    }
}
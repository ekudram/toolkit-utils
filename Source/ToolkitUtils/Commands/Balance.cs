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
// File: Source/ToolkitUtils/Commands/Balance.cs
// Project: ToolkitUtils
// Usage: A command that displays the user's current coin and karma balance

// MODIFICATIONS © 2025 Captolamia: Updated for TwitchLib 3.4+, threading safety, and stability fixes

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using SirRandoo.ToolkitUtils.Helpers;
using SirRandoo.ToolkitUtils.Utils;
using TwitchToolkit;
using TwitchToolkit.Utilities;
using Verse;
using ToolkitCore;

namespace SirRandoo.ToolkitUtils.Commands;

[UsedImplicitly]
public class Balance : CommandBase
{
    public override void RunCommand(TwitchMessageWrapper twitchMessage)
    {
        if (twitchMessage?.Username == null)
        {
            TkUtils.Logger.Warn("Balance command called with null message or username");
            return;
        }

        Viewer viewer = Viewers.GetViewer(twitchMessage.Username);
        if (viewer == null)
        {
            TkUtils.Logger.Warn($"Viewer not found for username: {twitchMessage.Username}");
            return;
        }

        var container = new List<string?>();
        string? coins = ToolkitSettings.UnlimitedCoins ? ResponseHelper.InfinityGlyph.AltText(int.MaxValue.ToString("N0")) : viewer.GetViewerCoins().ToString("N0");
        var karma = (viewer.GetViewerKarma() / 100f).ToString("P0");

        if (TkSettings.Emojis)
        {
            container.Add($"{ResponseHelper.CoinGlyph} {coins}");
            container.Add($"{ResponseHelper.KarmaGlyph} {karma}");
        }
        else
        {
            container.Add(ResponseHelper.JoinPair("TKUtils.Balance.Coins".Localize().CapitalizeFirst(), coins));
            container.Add(ResponseHelper.JoinPair("TKUtils.Balance.Karma".Localize().CapitalizeFirst(), karma));
        }

        if (ToolkitSettings.EarningCoins && TkSettings.ShowCoinRate)
        {
            int income = CalculateCoinAward(viewer);
            container.Add(
                (income > 0 ? $"{ResponseHelper.IncomeGlyph} +{income:N0}" : $"{ResponseHelper.DebtGlyph} {income:N0}").AltText(
                    "TKUtils.Balance.Rate".LocalizeKeyed(CalculateCoinAward(viewer).ToString("N0"), ToolkitSettings.CoinInterval.ToString("N0"))
                )
            );
        }

        try
        {
            TwitchWrapper.SendChatMessage($"@{twitchMessage.Username} {container.GroupedJoin()}");
        }
        catch (Exception ex)
        {
            TkUtils.Logger.Error($"Failed to send balance message to {twitchMessage.Username}: {ex.Message}");
        }
    }

    private static int CalculateCoinAward(Viewer viewer)
    {
        int baseCoins = ToolkitSettings.CoinAmount;
        float multiplier = viewer.GetViewerKarma() / 100f;

        if (viewer.IsSub)
        {
            baseCoins += ToolkitSettings.SubscriberExtraCoins;
            multiplier *= ToolkitSettings.SubscriberCoinMultiplier;
        }
        else if (viewer.IsVIP)
        {
            baseCoins += ToolkitSettings.VIPExtraCoins;
            multiplier *= ToolkitSettings.VIPCoinMultiplier;
        }
        else if (viewer.mod)
        {
            baseCoins += ToolkitSettings.ModExtraCoins;
            multiplier *= ToolkitSettings.ModCoinMultiplier;
        }

        int minutesElapsed = TimeHelper.MinutesElapsed(viewer.last_seen);

        if (ToolkitSettings.ChatReqsForCoins)
        {
            if (minutesElapsed > ToolkitSettings.TimeBeforeHalfCoins)
            {
                multiplier *= 0.5f;
            }

            if (minutesElapsed > ToolkitSettings.TimeBeforeNoCoins)
            {
                multiplier *= 0.0f;
            }
        }

        return (int)Math.Ceiling((double)baseCoins * multiplier);
    }
}

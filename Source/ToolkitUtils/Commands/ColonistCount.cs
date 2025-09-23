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

using JetBrains.Annotations;
using SirRandoo.ToolkitUtils.Helpers;
using SirRandoo.ToolkitUtils.Utils;
using System;
using System.Collections.Generic;
using ToolkitCore;
using TwitchToolkit;
using Verse;

namespace SirRandoo.ToolkitUtils.Commands;

[UsedImplicitly]
public class ColonistCount : CommandBase
{
    public override void RunCommand(TwitchMessageWrapper twitchMessage)
    {
        try
        {
            List<Pawn> colonists = Find.ColonistBar?.GetColonistsInOrder();
            string message;

            if (colonists == null || colonists.Count <= 0)
            {
                message = "TKUtils.ColonistCount.None".Localize();
            }
            else
            {
                message = "TKUtils.ColonistCount.Any".LocalizeKeyed(colonists.Count.ToString("N0"));
            }

            TwitchWrapper.SendChatMessage($"@{twitchMessage.Username} {message}");
        }
        catch (Exception ex)
        {
            TkUtils.Logger.Error($"Error in ColonistCount command: {ex.Message}");
        }
    }
}

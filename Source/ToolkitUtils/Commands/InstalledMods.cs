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

// MODIFICATIONS © 2025 Captolamia: Updated for TwitchLib 3.4+, threading safety, and stability fixes

using System;
using System.Linq;
using JetBrains.Annotations;
using SirRandoo.ToolkitUtils.Helpers;
using SirRandoo.ToolkitUtils.Models;
using SirRandoo.ToolkitUtils.Utils;
using TwitchToolkit;
using Verse;
using ToolkitCore;

namespace SirRandoo.ToolkitUtils.Commands;

[UsedImplicitly]
public class InstalledMods : CommandBase
{
    public override void RunCommand(TwitchMessageWrapper twitchMessage)
    {
        //twitchMessage.Reply(Data.Mods.Select(FormatMod).SectionJoin().WithHeader($"Toolkit v{TwitchToolkit.Toolkit.Mod.Version}"));
        TwitchWrapper.SendChatMessage($"@{twitchMessage.Username} {Data.Mods.Select(FormatMod).SectionJoin().WithHeader($"Toolkit v{TwitchToolkit.Toolkit.Mod.Version}")}");
    }

    private static string FormatMod(ModItem mod) =>
        mod.Version.NullOrEmpty() && !TkSettings.VersionedModList ? DecorateMod(mod) : $"{DecorateMod(mod)} (v{mod.Version})";

    private static string DecorateMod(ModItem mod) => !TkSettings.DecorateMods || !mod.Author.Equals("sirrandoo", StringComparison.InvariantCultureIgnoreCase)
        ? mod.Name
        : $"{"★".AltText("*")}{mod.Name}";
}
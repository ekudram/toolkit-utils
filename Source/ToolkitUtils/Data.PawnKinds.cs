// MIT License
//
// Copyright (c) 2022 SirRandoo
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using JetBrains.Annotations;
using RimWorld;
using SirRandoo.ToolkitUtils.Helpers;
using SirRandoo.ToolkitUtils.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace SirRandoo.ToolkitUtils;

public static partial class Data
{
    /// <summary>
    ///     The pawns available for purchase within the mod's store.
    /// </summary>
    public static List<PawnKindItem> PawnKinds { get; private set; }

    /// <summary>
    ///     Loads the traits saved to the given file.
    /// </summary>
    /// <param name="path">The file to load pawn kinds from</param>
    /// <param name="ignoreErrors">Whether loading errors should be ignored</param>
    public static void LoadPawnKinds(string path, bool ignoreErrors)
    {
        PawnKinds = LoadJson<List<PawnKindItem>>(path, ignoreErrors) ?? new List<PawnKindItem>();

    }

    /// <summary>
    ///     Loads the traits saved to the given file.
    /// </summary>
    /// <param name="path">The file to load pawn kinds from</param>
    /// <param name="ignoreErrors">Whether loading errors should be ignored</param>
    public static void LoadPawnKindsThreaded(string path, bool ignoreErrors)
    {
        LongEventHandler.QueueLongEvent(() =>
        {
            PawnKinds = LoadJson<List<PawnKindItem>>(path, ignoreErrors) ?? new List<PawnKindItem>();
        }, null, false, null, showExtraUIInfo: false, forceHideUI: true);
    }

    /// <summary>
    ///     Saves a list of pawns at the given file path.
    /// </summary>
    /// <param name="path">The file to save pawns to</param>
    public static void SavePawnKinds(string path)
    {
        SaveJson(PawnKinds, path);
    }
    private static void ValidatePawnKinds()
    {
        TkUtils.Logger.Debug("=== ValidatePawnKinds reached ===");
        List<PawnKindDef> kindDefs = DefDatabase<PawnKindDef>.AllDefs.Where(k => k.RaceProps.Humanlike).ToList();
        PawnKinds.RemoveAll(k => kindDefs.Find(d => d.race.defName.Equals(k.DefName)) == null);

        foreach (PawnKindDef def in kindDefs)
        {
            List<PawnKindItem> item = PawnKinds.FindAll(k => k.DefName.Equals(def.race.defName));

            if (item.Count > 0)
            {
                continue;
            }

            PawnKinds.Add(
                new PawnKindItem { DefName = def.race.defName, Enabled = true, Name = def.race.label ?? def.race.defName, Cost = def.race.CalculateStorePrice() }
            );
        }
    }

    private static void ValidatePawnKindData()
    {
        TkUtils.Logger.Debug("=== ValidatePawnKindData reached ===");
        var builder = new StringBuilder();

        foreach (PawnKindItem pawn in PawnKinds)
        {
            pawn.PawnData ??= new PawnKindData();

            try
            {
                pawn.PawnData.Mod = pawn.ColonistKindDef.TryGetModName();
                pawn.LoadGameData();
                pawn.UpdateStats();

                // NEW: Validate xenotype data
                ValidatePawnKindXenotypeData(pawn);
            }
            catch (Exception)
            {
                builder.AppendLine($" - {pawn.Name ?? pawn.DefName}");
            }
        }

        if (builder.Length <= 0)
        {
            return;
        }

        builder.Insert(0, "The following pawn kinds could not be processed:\n");
        TkUtils.Logger.Warn(builder.ToString());
    }

    private static void ValidatePawnKindXenotypeData(PawnKindItem pawn)
    {
        if (!ModsConfig.BiotechActive || pawn.PawnData?.AllowedXenotypes == null)
            return;

        // Remove any xenotypes that no longer exist
        var validXenotypes = pawn.PawnData.AllowedXenotypes
            .Where(xenoDefName => DefDatabase<XenotypeDef>.GetNamedSilentFail(xenoDefName) != null)
            .ToList();

        if (validXenotypes.Count != pawn.PawnData.AllowedXenotypes.Count)
        {
            pawn.PawnData.AllowedXenotypes = validXenotypes;
            TkUtils.Logger.Warn($"Cleaned up invalid xenotypes for {pawn.DefName}");
        }
    }

    /// <summary>
    ///     Loads pawns from the given partial data.
    /// </summary>
    /// <param name="partialData">A collection of partial data to load</param>
    public static void LoadPawnPartial(IEnumerable<PawnKindItem> partialData)
    {
        TkUtils.Logger.Debug("=== LoadPawnPartial reached ===");
        var builder = new StringBuilder();

        foreach (PawnKindItem partial in partialData)
        {
            PawnKindItem existing = PawnKinds.Find(i => i.DefName.Equals(partial.DefName));

            if (existing == null)
            {
                if (partial.ColonistKindDef == null)
                {
                    builder.Append($"  - {partial.Data?.Mod ?? "UNKNOWN"}:{partial.DefName}\n");

                    continue;
                }

                PawnKinds.Add(partial);

                continue;
            }

            existing.Name = partial.Name;
            existing.Cost = partial.Cost;
            existing.Enabled = partial.Enabled;
            existing.Data = partial.Data;
        }
    }

    /// <summary>
    ///     Gets a pawn kind from the given input.
    /// </summary>
    /// <param name="input">The raw input to check against</param>
    /// <param name="kind">
    ///     The <see cref="PawnKindItem"/> found with the given
    ///     input, or <c>null</c> if no pawn kind could be found
    /// </param>
    /// <returns>Whether a pawn kind was found from the given input</returns>
    /// <remarks>
    ///     Prefixing <see cref="input"/> with a dollar sign ($) will cause
    ///     the method to search against the
    ///     <see cref="PawnKindItem.DefName"/>s
    ///     of the traits, instead of the trait
    ///     <see cref="PawnKindItem.Name"/>s.
    /// </remarks>
    [ContractAnnotation("input:notnull => true,kind:notnull; input:notnull => false,kind:null")]
    public static bool TryGetPawnKind(string? input, out PawnKindItem kind)
    {
        TkUtils.Logger.Warn($"TryGetPawnKind searching for: '{input}'");

        if (input == null)
        {
            kind = null;
            return false;
        }

        // Find ALL matches
        var allMatches = PawnKinds.Where(t =>
            t.Name.Equals(input, StringComparison.OrdinalIgnoreCase) ||
            t.DefName.Equals(input, StringComparison.OrdinalIgnoreCase)).ToList();

        if (allMatches.Count == 0)
        {
            TkUtils.Logger.Warn($"No match found for: '{input}'");
            kind = null;
            return false;
        }

        // Filter out invalid entries and log what we found
        var validMatches = allMatches.Where(m => m.ColonistKindDef != null).ToList();

        TkUtils.Logger.Warn($"Found {allMatches.Count} total matches, {validMatches.Count} valid matches for '{input}'");

        foreach (var match in allMatches)
        {
            TkUtils.Logger.Warn($"Match: Name='{match.Name}', DefName='{match.DefName}', ColonistKindDef='{match.ColonistKindDef?.defName ?? "NULL"}'");
        }

        if (validMatches.Count == 0)
        {
            TkUtils.Logger.Warn($"No valid pawn kinds found for '{input}' - all matches have null ColonistKindDef");
            kind = null;
            return false;
        }

        // Selection logic
        PawnKindItem selected;

        if (validMatches.Count == 1)
        {
            selected = validMatches.First();
            TkUtils.Logger.Warn($"Single valid match selected: {selected.Name} -> {selected.ColonistKindDef.defName}");
        }
        else
        {
            // Multiple valid matches - use priority selection
            selected = SelectBestPawnKind(validMatches, input);
            TkUtils.Logger.Warn($"Multiple valid matches, selected: {selected.Name} -> {selected.ColonistKindDef.defName}");
        }

        kind = selected;
        return true;
    }

    private static PawnKindItem SelectBestPawnKind(List<PawnKindItem> matches, string input)
    {
        // Priority 1: Exact name match with player colony faction
        var playerColony = matches.FirstOrDefault(m =>
            m.Name.Equals(input, StringComparison.OrdinalIgnoreCase) &&
            m.ColonistKindDef?.defaultFactionDef == FactionDefOf.PlayerColony);

        if (playerColony != null)
        {
            TkUtils.Logger.Warn("Selected: Player colony faction match");
            return playerColony;
        }

        // Priority 2: Exact defName match
        var defNameMatch = matches.FirstOrDefault(m =>
            m.DefName.Equals(input, StringComparison.OrdinalIgnoreCase));

        if (defNameMatch != null)
        {
            TkUtils.Logger.Warn("Selected: DefName match");
            return defNameMatch;
        }

        // Priority 3: Any player colony faction
        var anyPlayer = matches.FirstOrDefault(m =>
            m.ColonistKindDef?.defaultFactionDef == FactionDefOf.PlayerColony);

        if (anyPlayer != null)
        {
            TkUtils.Logger.Warn("Selected: Any player colony faction");
            return anyPlayer;
        }

        // Priority 4: First valid match
        TkUtils.Logger.Warn("Selected: First valid match (fallback)");
        return matches.First();
    }
}
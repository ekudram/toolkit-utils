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

using RimWorld;
using SirRandoo.ToolkitUtils.Helpers;
using SirRandoo.ToolkitUtils.Interfaces;
using SirRandoo.ToolkitUtils.Models;
using SirRandoo.ToolkitUtils.Utils;
using SirRandoo.ToolkitUtils.Workers;
using System;
using System.Collections.Generic;
using System.Linq;
using ToolkitCore.Utilities;
using TwitchToolkit;
using TwitchToolkit.PawnQueue;
using Verse;

namespace SirRandoo.ToolkitUtils.Incidents;

public class BuyPawn : IncidentVariablesBase
{
    private PawnKindDef _kindDef = RimWorld.PawnKindDefOf.Colonist;
    private XenotypeDef _xenotypeDef = null; 
    private IntVec3 _loc;
    private Map _map;
    private PawnKindItem _pawnKindItem;

    /// <summary>
    ///     CanHappen checks to see
    ///     if viewer has pawn
    ///     if we are on anyplayermap
    ///     if we can spawn our pawn here 
    ///     and more
    /// </summary>
    public override bool CanHappen(string msg, Viewer viewer)
    {
        TkUtils.Logger.Warn("CanHappen to spawn a viewer pawn!");
        // 1. Check if user already has pawn
        if (CommandBase.GetOrFindPawn(viewer.username) != null)
        {
            MessageHelper.ReplyToUser(viewer.username, "TKUtils.HasPawn".Localize());
            return false;
        }

        // 2. Check if we're on a player map
        _map = Helper.AnyPlayerMap;
        if (_map == null)
        {
            MessageHelper.ReplyToUser(viewer.username, "TKUtils.NoMap".Localize());
            return false;
        }

        // 3. Check if we can spawn
        if (!CellFinder.TryFindRandomEdgeCellWith(p => _map.reachability.CanReachColony(p) && !p.Fogged(_map), _map, CellFinder.EdgeRoadChance_Neutral, out _loc))
        {
            TkUtils.Logger.Warn("No reachable location to spawn a viewer pawn!");
            return false;
        }

        // 4. Get default pawn kind (usually human)
        GetDefaultKind();
        TkUtils.Logger.Warn("GetDefaultKind: _pawnKindItem " + _pawnKindItem);
        TkUtils.Logger.Warn("GetDefaultKind: _kindDef " + _kindDef);

        // In your BuyPawn.CanHappen method, after GetDefaultKind():
        TkUtils.Logger.Warn("=== ALL PAWN KINDS ===");
        foreach (var kind in Data.PawnKinds)
        {
            TkUtils.Logger.Warn($"Name: '{kind.Name}', DefName: '{kind.DefName}', Enabled: {kind.Enabled}");
        }
        TkUtils.Logger.Warn("=====================");

        var worker = ArgWorker.CreateInstance(CommandFilter.Parse(msg).Skip(2));

        // Handle human pawn requests (explicit "human" or default with no arguments)
        bool wantsHuman = false;
        string firstArg = null;

        if (worker.HasNext())
        {
            firstArg = worker.GetNext();
            wantsHuman = firstArg.Equals("human", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // No arguments specified - default to human
            wantsHuman = true;
            TkUtils.Logger.Warn("No pawn kind specified, defaulting to human");
        }

        if (wantsHuman)
        {
            TkUtils.Logger.Warn("Using human pawn");
            _kindDef = RimWorld.PawnKindDefOf.Colonist;

            // Process xenotype if specified (only if we had a first argument)
            if (firstArg != null && ModsConfig.BiotechActive && worker.HasNext())
            {
                string xenotypeInput = worker.GetNext();
                if (!xenotypeInput.NullOrEmpty())
                {
                    _xenotypeDef = DefDatabase<XenotypeDef>.AllDefs.FirstOrDefault(
                        x => x.defName.Equals(xenotypeInput, StringComparison.OrdinalIgnoreCase) ||
                             x.label.Equals(xenotypeInput, StringComparison.OrdinalIgnoreCase));

                    if (_xenotypeDef == null)
                    {
                        MessageHelper.ReplyToUser(viewer.username, "TKUtils.InvalidXenotype".LocalizeKeyed(xenotypeInput));
                        return false;
                    }
                    TkUtils.Logger.Warn("Xenotype set to: " + _xenotypeDef.defName);
                }
            }

            // Use default cost for human
            TkUtils.Logger.Warn("Final selection - Human colonist with xenotype: " + (_xenotypeDef?.defName ?? "None"));
            return viewer.CanAfford(_pawnKindItem?.Cost ?? 1);
        }

        // 5. Process pawn kind (if PurchasePawnKinds is enabled)
        if (TkSettings.PurchasePawnKinds)
        {
            TkUtils.Logger.Warn($"PurchasePawnKinds enabled, checking for pawn kind input...");

            // Check if we have pawn input using the original worker state
            var workerForPawnCheck = ArgWorker.CreateInstance(CommandFilter.Parse(msg).Skip(2));

            if (workerForPawnCheck.HasNext())
            {
                string pawnInput = workerForPawnCheck.GetNext();
                TkUtils.Logger.Warn($"Processing pawn kind input: '{pawnInput}'");

                // Find ALL matching PawnKindDefs and select the best one
                List<PawnKindDef> allMatches = new List<PawnKindDef>();

                if (Data.TryGetPawnKind(pawnInput, out var pawnKindItem) && pawnKindItem?.DefName != null)
                {
                    TkUtils.Logger.Warn($"Found data entry: Name='{pawnKindItem.Name}', DefName='{pawnKindItem.DefName}'");

                    // Search for PawnKindDefs that match the race defName from our data
                    allMatches = DefDatabase<PawnKindDef>.AllDefs.Where(pk =>
                        pk.RaceProps.Humanlike &&
                        pk.race?.defName?.Equals(pawnKindItem.DefName, StringComparison.OrdinalIgnoreCase) == true).ToList();
                }
                else
                {
                    // Fallback to original search
                    allMatches = DefDatabase<PawnKindDef>.AllDefs.Where(pk =>
                        pk.RaceProps.Humanlike &&
                        (pk.defName.Equals(pawnInput, StringComparison.OrdinalIgnoreCase) ||
                         pk.label?.Equals(pawnInput, StringComparison.OrdinalIgnoreCase) == true ||
                         pk.race?.defName?.Equals(pawnInput, StringComparison.OrdinalIgnoreCase) == true)).ToList();
                }

                if (allMatches.Count > 0)
                {
                    TkUtils.Logger.Warn($"Found {allMatches.Count} matching PawnKindDefs for '{pawnInput}'");

                    // Select the best PawnKindDef using priority logic
                    _kindDef = SelectBestPawnKindDef(allMatches, pawnInput);
                    TkUtils.Logger.Warn($"Selected PawnKindDef: {_kindDef.defName} for input '{pawnInput}'");

                    // Try to find the corresponding PawnKindItem from data using the actual input
                    if (Data.TryGetPawnKind(pawnInput, out _pawnKindItem))
                    {
                        TkUtils.Logger.Warn($"Found PawnKindItem for '{pawnInput}'");
                    }
                    else
                    {
                        TkUtils.Logger.Warn($"No PawnKindItem found for '{pawnInput}', using default costing");
                        GetDefaultKind();
                    }
                }
                else
                {
                    TkUtils.Logger.Warn($"No valid PawnKindDef found for: '{pawnInput}'");
                    MessageHelper.ReplyToUser(viewer.username, "TKUtils.InvalidKindQuery".LocalizeKeyed(pawnInput));
                    return false;
                }
            }
            else
            {
                TkUtils.Logger.Warn($"No valid PawnKindDef found");
                MessageHelper.ReplyToUser(viewer.username, "TKUtils.InvalidKindQuery");
                return false;
            }
        }

        // 6. Process xenotype (optional, only if Biotech is active)
        if (ModsConfig.BiotechActive && worker.HasNext())
        {
            string xenotypeInput = worker.GetNext();
            if (!xenotypeInput.NullOrEmpty())
            {
                _xenotypeDef = DefDatabase<XenotypeDef>.AllDefs.FirstOrDefault(
                    x => x.defName.Equals(xenotypeInput, StringComparison.OrdinalIgnoreCase) ||
                         x.label.Equals(xenotypeInput, StringComparison.OrdinalIgnoreCase));

                if (_xenotypeDef == null)
                {
                    MessageHelper.ReplyToUser(viewer.username, "TKUtils.InvalidXenotype".LocalizeKeyed(xenotypeInput));
                    return false; // Invalid xenotype specified, fail purchase
                }
                TkUtils.Logger.Warn("Xenotype set to: " + _xenotypeDef.defName);
            }
            // else: xenotype input was empty, ignore it
        }

        // 7. Validate the final selection
        if (!_kindDef.RaceProps.Humanlike)
        {
            MessageHelper.ReplyToUser(viewer.username, "TKUtils.BuyPawn.Humanlike".Localize());
            return false;
        }
        TkUtils.Logger.Warn($"Final selection - PawnKind: {_kindDef?.defName}, Xenotype: {_xenotypeDef?.defName ?? "None"}");
        // 8. Check if purchase is allowed
        return CanPurchaseRace(viewer, _pawnKindItem);
    }

    /// <summary>
    /// Excutes the command...
    /// </summary>
    public override void Execute()
    {
        try
        {
            var request = new PawnGenerationRequest(
                _kindDef,
                Faction.OfPlayer,
                allowFood: false,
                mustBeCapableOfViolence: true,
                fixedIdeo: Find.FactionManager.OfPlayer.ideos.GetRandomIdeoForNewPawn(),
                // Add xenotype parameter if Biotech is active and xenotype is specified
                forcedXenotype: ModsConfig.BiotechActive ? _xenotypeDef : null
            );

            Pawn pawn = PawnGenerator.GeneratePawn(request);

            if (!(pawn.Name is NameTriple name))
            {
                TkUtils.Logger.Warn("Pawn name is not a name triple!");
                return;
            }

            PurchaseHelper.SpawnPawn(pawn, _loc, _map);
            pawn.Name = new NameTriple(name.First ?? string.Empty, Viewer.username, name.Last ?? string.Empty);
            TaggedString title = "TKUtils.PawnLetter.Title".Localize();
            TaggedString text = "TKUtils.PawnLetter.Description".LocalizeKeyed(Viewer.username);
            PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref text, ref title, pawn);

            Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.PositiveEvent, pawn);
            Current.Game.GetComponent<GameComponentPawns>().AssignUserToPawn(Viewer.username, pawn);

            if (TkSettings.EasterEggs && Basket.TryGetEggFor(Viewer.username, out IEasterEgg egg) && Rand.Chance(egg.Chance) && egg.IsPossible(storeIncident, Viewer))
            {
                egg.Execute(Viewer, pawn);
            }

            Viewer.Charge(_pawnKindItem.Cost, _pawnKindItem.Data?.KarmaType ?? storeIncident.karmaType);

            // Add xenotype info to confirmation message if applicable
            string confirmationMsg = "TKUtils.BuyPawn.Confirmation".Localize();
            if (ModsConfig.BiotechActive && _xenotypeDef != null)
            {
                confirmationMsg += " " + "TKUtils.BuyPawn.WithXenotype".LocalizeKeyed(_xenotypeDef.label);
            }

            MessageHelper.SendConfirmation(Viewer.username, confirmationMsg);
        }
        catch (Exception e)
        {
            TkUtils.Logger.Error("Could not execute buy pawn", e);
        }
    }
    /// <summary>
    /// Can purchase the humankind race
    /// </summary>
    /// <param name="viewer"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    private static bool CanPurchaseRace(Viewer viewer, IShopItemBase target)
    {
        if (!target.Enabled && TkSettings.PurchasePawnKinds)
        {
            MessageHelper.ReplyToUser(viewer.username, "TKUtils.InformativeDisabledItem".LocalizeKeyed(target.Name));

            return false;
        }

        if (viewer.CanAfford(target.Cost))
        {
            return true;
        }

        MessageHelper.ReplyToUser(viewer.username, "TKUtils.InsufficientBalance".LocalizeKeyed(target.Cost.ToString("N0"), viewer.GetViewerCoins().ToString("N0")));

        return false;
    }
    /// <summary>
    /// Gets the defaultkind.  Normally just a human
    /// </summary>
    private void GetDefaultKind()
    {
        TkUtils.Logger.Warn("=== GetDefaultKind START ===");
    
        if (Data.TryGetPawnKind($"{RimWorld.PawnKindDefOf.Colonist.race.defName}", out PawnKindItem human) && (human!.Enabled || !TkSettings.PurchasePawnKinds))
        {
            _kindDef = RimWorld.PawnKindDefOf.Colonist;
            _pawnKindItem = human;
            TkUtils.Logger.Warn($"GetDefaultKind: Using human colonist -> {_pawnKindItem.Name}");
            return;
        }

        PawnKindItem randomKind = Data.PawnKinds.FirstOrDefault(k => k.Enabled);

        if (randomKind == null)
        {
            TkUtils.Logger.Warn("Could not get next enabled race!");

            return;
        }

        _kindDef = randomKind.ColonistKindDef;
        _pawnKindItem = randomKind;

        TkUtils.Logger.Warn("GetDefaultKind: Using RimWorld.PawnKindDefOf.Colonist as fallback");
        TkUtils.Logger.Warn("=== GetDefaultKind END ===");
    }

    /// <summary>
    /// Checks for all Valid PawnKindDef returns best matches for Player or Colonist
    /// Avoids certian types if possible
    /// Defaults First match (fallback)
    /// </summary>
    /// <param name="matches"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    private PawnKindDef SelectBestPawnKindDef(List<PawnKindDef> matches, string input)
    {

        TkUtils.Logger.Warn($"=== SelectBestPawnKindDef START ===");
        TkUtils.Logger.Warn($"Input: '{input}', Found {matches.Count} matches:");



        foreach (var match in matches)
        {
            TkUtils.Logger.Warn($"  - {match.defName}, Faction: {match.defaultFactionDef?.defName ?? "NULL"}");
        }

        // Priority 1: DefName contains "PlayerColonist" or "Colonist"
        var colonistMatch = matches.FirstOrDefault(pk =>
            pk.defName.Contains("Player") ||
            pk.defName.Contains("PlayerColonist") ||
            pk.defName.Contains("Colonist") ||
            (pk.defName.Contains("Player") && pk.defName.Contains("Colonist")));
        if (colonistMatch != null)
        {
            TkUtils.Logger.Warn($"Selected Priority 1: {colonistMatch.defName} - Contains 'PlayerColonist' or 'Colonist'");
            return colonistMatch;
        }
        else
        {
            TkUtils.Logger.Warn("Priority 1: No match with 'PlayerColonist' or 'Colonist'");
        }

        // Priority 2: Default faction is PlayerColony
        var playerFactionMatch = matches.FirstOrDefault(pk =>
            pk.defaultFactionDef == FactionDefOf.PlayerColony);
        if (playerFactionMatch != null)
        {
            TkUtils.Logger.Warn($"Selected Priority 2: {playerFactionMatch.defName} - Player colony faction");
            return playerFactionMatch;
        }
        else
        {
            TkUtils.Logger.Warn("Priority 2: No match with PlayerColony faction");
        }

        // Priority 3: DefName contains "Player"
        var playerMatch = matches.FirstOrDefault(pk => pk.defName.Contains("Player"));
        if (playerMatch != null)
        {
            TkUtils.Logger.Warn($"Selected Priority 3: {playerMatch.defName} - Contains 'Player'");
            return playerMatch;
        }
        else
        {
            TkUtils.Logger.Warn("Priority 3: No match containing 'Player'");
        }

        // Priority 4: Not a corpse, crypto, or other special type
        var nonSpecialMatch = matches.FirstOrDefault(pk =>
            !pk.defName.Contains("Corpse") &&
            !pk.defName.Contains("Crypto") &&
            !pk.defName.Contains("Slave") &&
            !pk.defName.Contains("Refugee"));
        if (nonSpecialMatch != null)
        {
            TkUtils.Logger.Warn($"Selected Priority 4: {nonSpecialMatch.defName} - Non-special type");
            return nonSpecialMatch;
        }
        else
        {
            TkUtils.Logger.Warn("Priority 4: No non-special matches found");
        }

        // Priority 5: First match (fallback)
        TkUtils.Logger.Warn($"Selected Priority 5 (fallback): {matches.First().defName} - First match");
        TkUtils.Logger.Warn($"=== SelectBestPawnKindDef END ===");
        return matches.First();
    }
}
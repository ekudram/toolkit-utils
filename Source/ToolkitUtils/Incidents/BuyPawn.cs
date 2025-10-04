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
using RimWorld;
using SirRandoo.ToolkitUtils.Helpers;
using SirRandoo.ToolkitUtils.Interfaces;
using SirRandoo.ToolkitUtils.Models;
using SirRandoo.ToolkitUtils.Utils;
using SirRandoo.ToolkitUtils.Workers;
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
        TkUtils.Logger.Debug("GetDefaultKind: " + _pawnKindItem);

        var worker = ArgWorker.CreateInstance(CommandFilter.Parse(msg).Skip(2));

        // 5. Process pawn kind (if PurchasePawnKinds is enabled)
        if (TkSettings.PurchasePawnKinds)
        {
            if (worker.TryGetNextAsPawn(out PawnKindItem temp) && temp?.ColonistKindDef != null)
            {
                _pawnKindItem = temp;
                _kindDef = _pawnKindItem.ColonistKindDef;
                TkUtils.Logger.Debug("Processed to kind: " + _pawnKindItem);
            }
            else if (!worker.GetLast().NullOrEmpty())
            {
                // Invalid pawn kind specified
                MessageHelper.ReplyToUser(viewer.username, "TKUtils.InvalidKindQuery".LocalizeKeyed(worker.GetLast()));
                return false;
            }
            // else: no pawn kind specified, use default
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
                TkUtils.Logger.Debug("Xenotype set to: " + _xenotypeDef.defName);
            }
            // else: xenotype input was empty, ignore it
        }

        // 7. Validate the final selection
        if (!_kindDef.RaceProps.Humanlike)
        {
            MessageHelper.ReplyToUser(viewer.username, "TKUtils.BuyPawn.Humanlike".Localize());
            return false;
        }
        TkUtils.Logger.Debug($"Final selection - PawnKind: {_kindDef?.defName}, Xenotype: {_xenotypeDef?.defName ?? "None"}");
        // 8. Check if purchase is allowed
        return CanPurchaseRace(viewer, _pawnKindItem);
    }

    /// <summary>
    /// Processes xenotype from command arguments if Biotech is active
    /// </summary>
    private bool TryProcessXenotype(ArgWorker worker, Viewer viewer)
    {
        if (!ModsConfig.BiotechActive || !worker.HasNext())
        {
            return true; // No xenotype to process, but that's fine
        }

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

            TkUtils.Logger.Debug($"Xenotype set to: {_xenotypeDef.defName}");
        }

        return true;
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
        if (Data.TryGetPawnKind($"${RimWorld.PawnKindDefOf.Colonist.race.defName}", out PawnKindItem human) && (human!.Enabled || !TkSettings.PurchasePawnKinds))
        {
            _kindDef = RimWorld.PawnKindDefOf.Colonist;
            _pawnKindItem = human;

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
    }
}
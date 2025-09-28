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

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SirRandoo.ToolkitUtils.Helpers;
using SirRandoo.ToolkitUtils.Utils;
using TwitchToolkit;
using Verse;

namespace SirRandoo.ToolkitUtils.Incidents;

public class HealRandom : IncidentVariablesBase
{
    private Pawn _target;
    private Hediff _toHeal;
    private BodyPartRecord _toRestore;

    public override bool CanHappen(string msg, Viewer viewer)
    {
        // Get colonists that are alive and not in combat (if FairFights is enabled)
        List<Pawn> pawns = Find.ColonistBar.GetColonistsInOrder()
           .Where(p => !p.Dead)
           .Where(pawn => !IncidentSettings.HealRandom.FairFights
               || pawn.mindState.lastAttackTargetTick <= 0
               || Find.TickManager.TicksGame > pawn.mindState.lastAttackTargetTick + 1800)
           .ToList();

        if (!pawns.Any())
        {
            if (IncidentSettings.HealRandom.FairFights)
            {
                MessageHelper.ReplyToUser(viewer.username, "TKUtils.AllColonistsInCombat".Localize());
            }
            else
            {
                MessageHelper.ReplyToUser(viewer.username, "TKUtils.NoColonists".Localize());
            }
            return false;
        }

        // Find healable colonists with their healable items
        var healableColonists = pawns
            .Select(p => new Pair<Pawn, object>(p, HealHelper.GetPawnHealable(p)))
            .Where(r => r.Second != null)
            .ToList();

        if (!healableColonists.Any())
        {
            MessageHelper.ReplyToUser(viewer.username, "TKUtils.FullHeal.NoHealableInjuries".Localize());
            return false;
        }

        if (!healableColonists.TryRandomElement(out Pair<Pawn, object> random))
        {
            MessageHelper.ReplyToUser(viewer.username, "TKUtils.HealRandom.Failed".Localize());
            return false;
        }

        _target = random.First;

        switch (random.Second)
        {
            case Hediff hediff:
                _toHeal = hediff;
                break;
            case BodyPartRecord record:
                _toRestore = record;
                break;
            default:
                MessageHelper.ReplyToUser(viewer.username, "TKUtils.HealRandom.InvalidTarget".Localize());
                return false;
        }

        return _target != null && (_toHeal != null || _toRestore != null);
    }

    public override void Execute()
    {
        // Final validation before executing
        if (_target == null || (_toHeal == null && _toRestore == null))
        {
            MessageHelper.ReplyToUser(Viewer.username, "TKUtils.HealRandom.NoValidTarget".Localize());
            return;
        }

        bool healed = false;

        if (_toHeal != null)
        {
            HealHelper.Cure(_toHeal);
            healed = true;
            NotifySuccess(_toHeal.LabelCap);
        }
        else if (_toRestore != null)
        {
            _target.health.RestorePart(_toRestore);
            healed = true;
            NotifySuccess(_toRestore.Label);
        }

        if (healed)
        {
            Viewer.Charge(storeIncident);
        }
    }

    private void NotifySuccess(string affected)
    {
        if (ToolkitSettings.PurchaseConfirmations)
        {
            var response = _toHeal != null ? "TKUtils.HealRandom.Recovered" : "TKUtils.HealRandom.Restored";
            MessageHelper.ReplyToUser(Viewer.username, response.LocalizeKeyed(_target.LabelShort, affected));
        }

        var description = _toHeal != null ? "TKUtils.HealLetter.RecoveredDescription" : "TKUtils.HealLetter.RestoredDescription";
        string descriptionTranslated = description.LocalizeKeyed(_target.LabelShort.CapitalizeFirst(), affected);

        Current.Game.letterStack.ReceiveLetter(
            "TKUtils.HealLetter.Title".Localize(),
            descriptionTranslated,
            LetterDefOf.PositiveEvent,
            _target
        );
    }
}

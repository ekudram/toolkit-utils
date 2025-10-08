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
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Newtonsoft.Json;
using RimWorld;
using SirRandoo.ToolkitUtils.Helpers;
using SirRandoo.ToolkitUtils.Interfaces;
using Verse;

namespace SirRandoo.ToolkitUtils.Models;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class PawnKindItem : IShopItemBase
{
    [JsonIgnore] private KindDefData _colonistDef;
    [JsonIgnore] private PawnKindData _data;
    [JsonIgnore] private KindDefData[] _kinds;
    
    [JsonIgnore] public IEnumerable<PawnKindDef> Kinds => _kinds.Select(d => d.Def);

    [JsonIgnore] public PawnKindDef ColonistKindDef => _colonistDef.Def;

    [JsonProperty("data")]
    public PawnKindData PawnData
    {
        get => _data ??= (PawnKindData)Data;
        set => Data = _data = value;
    }

    [CanBeNull] [JsonProperty("description")] public string Description { get; private set; }

    [JsonProperty("defName")] public string? DefName { get; set; }
    [JsonProperty("enabled")] public bool Enabled { get; set; }
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("price")] public int Cost { get; set; }

    [JsonIgnore] public IShopDataBase Data { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public void ResetName()
    {
        if (ColonistKindDef != null)
        {
            Name = ColonistKindDef.label.ToToolkit();
        }
    }

    public void ResetPrice()
    {
        PawnKindDef def = ColonistKindDef;

        if (def?.race != null)
        {
            Cost = def.race.CalculateStorePrice();
        }
    }

    public void ResetData()
    {
        PawnData.Reset();
    }

    public void UpdateStats()
    {
        PawnKindDef def = ColonistKindDef;

        if (def?.race?.statBases == null)
        {
            return;
        }

        var builder = new StringBuilder();
        var container = new List<string>();

        foreach (StatModifier stat in def.race.statBases)
        {
            try
            {
                container.Add($"{stat.ValueToStringAsOffset} {stat.stat.label?.CapitalizeFirst() ?? stat.stat.defName}");
            }
            catch (Exception)
            {
                builder.AppendLine($"- {stat?.stat?.label ?? stat?.stat?.defName ?? "UNPROCESSABLE"}");
            }
        }

        if (builder.Length > 0)
        {
            builder.Insert(0, $@"The following stats could not be processed for ""{def.label ?? def.defName}"":\n");
            TkUtils.Logger.Warn(builder.ToString());
        }

        PawnData.Stats = container.ToArray();
    }

    internal void LoadGameData()
    {
        TkUtils.Logger.Warn($"=== LoadGameData START ===");
        TkUtils.Logger.Warn($"Processing pawn kind: Name='{Name}', DefName='{DefName}'");

        KindDefData? colonist = null;
        var container = new List<KindDefData>();

        TkUtils.Logger.Warn($"Searching for PawnKindDefs with race defName='{DefName}' OR label/defName matching '{Name}'");

        int foundCount = 0;
        foreach (PawnKindDef kindDef in DefDatabase<PawnKindDef>.AllDefs)
        {
            bool isMatch = false;

            // Strategy 1: Match by race defName (this is what we need for modded races)
            // It is always this strategy that should succeed if the DefName is set correctly
            if (kindDef.race?.defName?.Equals(DefName, StringComparison.OrdinalIgnoreCase) == true)
            {
                //TkUtils.Logger.Warn($"FOUND by race defName: {kindDef.defName} (race: {kindDef.race.defName})");
                isMatch = true;
            }
            // Strategy 2: Match by PawnKindDef defName (for backward compatibility)
            else if (kindDef.defName.Equals(DefName, StringComparison.OrdinalIgnoreCase))
            {
                //TkUtils.Logger.Warn($"FOUND by pawnkind defName: {kindDef.defName}");
                isMatch = true;
            }
            // Strategy 3: Match by label (case insensitive)
            else if (kindDef.label?.Equals(Name, StringComparison.OrdinalIgnoreCase) == true)
            {
                //TkUtils.Logger.Warn($"FOUND by label: {kindDef.defName} (label: {kindDef.label})");
                isMatch = true;
            }

            if (isMatch)
            {
                foundCount++;
                ProcessFoundKindDef(kindDef, ref colonist, container);
            }
        }

        TkUtils.Logger.Warn($"Found {foundCount} matching PawnKindDefs");

        if (container.Count == 0)
        {
            TkUtils.Logger.Error($"No PawnKindDef found for Name='{Name}', DefName='{DefName}'. This entry will not work!");
            return;
        }

        colonist ??= container.FirstOrDefault();

        if (colonist == null)
        {
            TkUtils.Logger.Error($"No colonist variant found for Name='{Name}', DefName='{DefName}'");
            return;
        }

        _colonistDef = colonist.Value;
        Description = _colonistDef.Def.race?.description;
        _kinds = container.ToArray();

        TkUtils.Logger.Warn($"LoadGameData COMPLETE: Name='{Name}', DefName='{DefName}', ColonistKindDef='{_colonistDef.Def.defName}'");
        TkUtils.Logger.Warn($"=== LoadGameData END ====");
    }

    private void ProcessFoundKindDef(PawnKindDef kindDef, ref KindDefData? colonist, List<KindDefData> container)
    {
        var data = new KindDefData
        {
            Name = kindDef.race?.label?.ToToolkit()?.ToLower() ?? "UNKNOWN",
            Def = kindDef
        };
        container.Add(data);

        if (kindDef.defaultFactionDef == FactionDefOf.PlayerColony)
        {
            colonist = data;
            TkUtils.Logger.Warn($"SELECTED AS COLONIST: {kindDef.defName}");
        }
    }
    // NEW: Xenotype management methods
    public void ResetXenotypeFilter()
    {
        PawnData.AllowedXenotypes.Clear();
        PawnData.XenotypeFilterEnabled = false;
    }
    public void ToggleXenotype(string xenotypeDefName, bool allowed)
    {
        if (!PawnData.XenotypeFilterEnabled)
        {
            PawnData.XenotypeFilterEnabled = true;
        }

        if (allowed)
        {
            if (!PawnData.AllowedXenotypes.Contains(xenotypeDefName))
            {
                PawnData.AllowedXenotypes.Add(xenotypeDefName);
            }
        }
        else
        {
            PawnData.AllowedXenotypes.Remove(xenotypeDefName);
        }
    }
    public void SetAllXenotypesAllowed(bool allowed)
    {
        if (allowed && PawnData.XenotypeFilterEnabled)
        {
            // If enabling all, clear the filter list (empty list = all allowed when filter is enabled)
            PawnData.AllowedXenotypes.Clear();
        }
        else if (!allowed && PawnData.XenotypeFilterEnabled)
        {
            // If disabling all, we need to explicitly block all xenotypes
            // This is handled by having filter enabled but empty allowed list = none allowed
            PawnData.AllowedXenotypes.Clear();
        }
    }
    public bool IsXenotypeAllowed(string xenotypeDefName)
    {
        if (!ModsConfig.BiotechActive)
            return true;

        // Check HAR restrictions first (they take priority)
        var raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(DefName);
        var harAllowed = CompatRegistry.Alien?.GetAllowedXenotypes(raceDef);

        if (harAllowed != null && harAllowed.Any())
        {
            // HAR has restrictions - must be in the allowed list
            bool harAllows = harAllowed.Contains(xenotypeDefName);
            TkUtils.Logger.Debug($"HAR restriction check for {xenotypeDefName} on {DefName}: {harAllows}");
            if (!harAllows) return false;
        }

        // Then check JSON filter if enabled
        if (PawnData.XenotypeFilterEnabled)
        {
            bool jsonAllows = PawnData.AllowedXenotypes.Contains(xenotypeDefName);
            TkUtils.Logger.Debug($"JSON filter check for {xenotypeDefName} on {DefName}: {jsonAllows}");
            return jsonAllows;
        }

        // No restrictions from either source
        return true;
    }
    public bool IsXenotypeFilteringEnabled()
    {
        return ModsConfig.BiotechActive && PawnData.XenotypeFilterEnabled;
    }
    public List<string> GetAllowedXenotypes()
    {
        if (!ModsConfig.BiotechActive)
            return new List<string>();

        var raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(DefName);

        // Priority 1: Check HAR restrictions if they exist
        var harAllowed = CompatRegistry.Alien?.GetAllowedXenotypes(raceDef);
        if (harAllowed != null && harAllowed.Any())
        {
            TkUtils.Logger.Debug($"Using HAR restrictions for {DefName}: {string.Join(", ", harAllowed)}");
            return harAllowed;
        }

        // Priority 2: Check JSON filter if enabled
        if (PawnData.XenotypeFilterEnabled && PawnData.AllowedXenotypes.Any())
        {
            TkUtils.Logger.Debug($"Using JSON filter for {DefName}: {string.Join(", ", PawnData.AllowedXenotypes)}");
            return PawnData.AllowedXenotypes;
        }

        // Priority 3: No restrictions - return empty list (all xenotypes allowed)
        TkUtils.Logger.Debug($"No xenotype restrictions for {DefName}");
        return new List<string>();
    }
    public bool HasHARXenotypeRestrictions()
    {
        if (!ModsConfig.BiotechActive)
            return false;

        var raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(DefName);
        var harAllowed = CompatRegistry.Alien?.GetAllowedXenotypes(raceDef);
        return harAllowed != null && harAllowed.Any();
    }

    public string? GetDefaultName() => _colonistDef.Name ?? DefName;
    private struct KindDefData
    {
        public string? Name { get; set; }
        public PawnKindDef Def { get; set; }
    }
}

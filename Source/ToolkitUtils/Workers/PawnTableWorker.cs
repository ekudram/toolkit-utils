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

using RimWorld;
using SirRandoo.ToolkitUtils.Helpers;
using SirRandoo.ToolkitUtils.Interfaces;
using SirRandoo.ToolkitUtils.Models;
using SirRandoo.ToolkitUtils.Models.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ToolkitUtils.UX;
using UnityEngine;
using Verse;

namespace SirRandoo.ToolkitUtils.Workers;

/// <summary>
///     A class for drawing the pawn kind shop's data in a portable way.
/// </summary>
public class PawnTableWorker : TableWorker<TableSettingsItem<PawnKindItem>>
{
    private const float ExpandedLineSpan = 2f;

    private string? _closePawnNameTooltip;
    private string? _defaultKarmaTypeText;
    private string? _editPawnNameTooltip;
    private Rect _expandedHeaderInnerRect = Rect.zero;
    private Rect _expandedHeaderRect = Rect.zero;
    private string? _karmaTypeText;
    private string? _nameHeaderText;
    private string? _priceHeaderText;
    private string? _resetPawnKarmaTooltip;
    private string? _resetPawnNameTooltip;
    // In PawnTableWorker.cs - Add to field declarations
    private string? _xenotypeFilterText;
    private string? _xenotypeFilterTooltip;
    private Vector2 _scrollPos = Vector2.zero;
    private SettingsKey _settingsKey = SettingsKey.Collapse;

    private SortKey _sortKey = SortKey.Name;
    private SortOrder _sortOrder = SortOrder.Descending;
    private Rect _stateHeaderInnerRect = Rect.zero;

    private Rect _stateHeaderRect = Rect.zero;
    private StateKey _stateKey = StateKey.Enable;
    private protected Rect NameHeaderRect = Rect.zero;
    private protected Rect NameHeaderTextRect = Rect.zero;
    private protected Rect PriceHeaderRect = Rect.zero;
    private protected Rect PriceHeaderTextRect = Rect.zero;

    /// <inheritdoc cref="TableWorkerBase.DrawHeaders" />
    protected override void DrawHeaders(Rect region)
    {
        if (SettingsHelper.DrawTableHeader(_stateHeaderRect, _stateHeaderInnerRect, _stateKey == StateKey.Enable ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex))
        {
            _stateKey = _stateKey == StateKey.Enable ? StateKey.Disable : StateKey.Enable;
            NotifyGlobalStateChanged(_stateKey);
        }

        if (SettingsHelper.DrawTableHeader(_expandedHeaderRect, _expandedHeaderInnerRect, Textures.Gear))
        {
            _settingsKey = _settingsKey == SettingsKey.Expand ? SettingsKey.Collapse : SettingsKey.Expand;
            NotifyGlobalSettingsChanged(_settingsKey);
        }

        DrawSortableHeaders();
        DrawSortableHeaderIcon();
    }

    private protected void DrawSortableHeaderIcon()
    {
        switch (_sortKey)
        {
            case SortKey.Name:
                UiHelper.SortIndicator(NameHeaderRect, _sortOrder);

                return;
            case SortKey.Price:
                UiHelper.SortIndicator(PriceHeaderRect, _sortOrder);

                return;
            default:
                return;
        }
    }

    private protected void DrawSortableHeaders()
    {
        var anyClicked = false;
        SortKey previousKey = _sortKey;

        if (SettingsHelper.DrawTableHeader(NameHeaderRect, NameHeaderTextRect, _nameHeaderText))
        {
            _sortKey = SortKey.Name;
            anyClicked = true;
        }

        if (SettingsHelper.DrawTableHeader(PriceHeaderRect, PriceHeaderTextRect, _priceHeaderText))
        {
            _sortKey = SortKey.Price;
            anyClicked = true;
        }

        if (_sortKey != previousKey)
        {
            _sortOrder = SortOrder.Descending;
            NotifySortRequested();
        }
        else if (anyClicked)
        {
            InvertSortOrder();
            NotifySortRequested();
        }
    }

    private void InvertSortOrder()
    {
        _sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
    }

    private void NotifyGlobalSettingsChanged(SettingsKey newState)
    {
        foreach (TableSettingsItem<PawnKindItem> item in Data.Where(i => !i.IsHidden))
        {
            item.SettingsVisible = newState == SettingsKey.Expand;
        }
    }

    private void NotifyGlobalStateChanged(StateKey newState)
    {
        foreach (TableSettingsItem<PawnKindItem> item in Data.Where(i => !i.IsHidden))
        {
            item.Data.Enabled = newState == StateKey.Enable;
        }
    }

    /// <inheritdoc cref="TableWorkerBase.DrawTableContents" />
    protected override void DrawTableContents(Rect region)
    {
        if (Event.current.type == EventType.Layout)
        {
            return;
        }

        GUI.BeginGroup(region);

        // Calculate total height needed for all rows
        float totalHeight = CalculateTotalHeight();
        var viewPort = new Rect(0f, 0f, region.width - 16f, totalHeight);

        float currentY = 0f;
        var alternate = false;
        _scrollPos = GUI.BeginScrollView(region, _scrollPos, viewPort);

        foreach (TableSettingsItem<PawnKindItem> item in Data.Where(i => !i.IsHidden))
        {
            float rowHeight = GetRowHeight(item);

            var lineRect = new Rect(
                0f,
                currentY,
                region.width - 16f,
                rowHeight
            );

            if (!lineRect.IsVisible(region, _scrollPos))
            {
                currentY += rowHeight;
                alternate = !alternate;
                continue;
            }

            GUI.BeginGroup(lineRect);
            Rect rect = lineRect.AtZero();

            if (alternate)
            {
                Widgets.DrawLightHighlight(rect);
            }

            DrawKind(rect, item);
            GUI.EndGroup();

            currentY += rowHeight;
            alternate = !alternate;
        }

        GUI.EndScrollView();
        GUI.EndGroup();
    }
    // NEW: Calculate total height needed for all visible rows
    private float CalculateTotalHeight()
    {
        float totalHeight = 0f;
        foreach (TableSettingsItem<PawnKindItem> item in Data.Where(i => !i.IsHidden))
        {
            totalHeight += GetRowHeight(item);
        }
        return totalHeight;
    }


    // NEW: Get the actual height for a specific row
    private float GetRowHeight(TableSettingsItem<PawnKindItem> item)
    {
        if (item.SettingsVisible)
        {
            // Return expanded height for this specific row
            return RowLineHeight + CalculateExpandedHeight(item);
        }

        // Return normal height for collapsed rows
        return RowLineHeight;
    }

    /// <summary>
    ///     Draws a <see cref="PawnKindItem" /> in a given row of the
    ///     <see cref="PawnTableWorker" /> area.
    /// </summary>
    /// <param name="region">
    ///     The region to draw the
    ///     <see cref="PawnKindItem" /> in
    /// </param>
    /// <param name="item">
    ///     The <see cref="PawnKindItem" /> to draw in the
    ///     region
    /// </param>
    protected virtual void DrawKind(Rect region, TableSettingsItem<PawnKindItem> item)
    {
        Rect checkboxRect = LayoutHelper.IconRect(_stateHeaderRect.x + 2f, region.y + 2f, _stateHeaderRect.width - 4f, RowLineHeight - 4f);
        var nameMouseOverRect = new Rect(NameHeaderRect.x, region.y, NameHeaderRect.width, RowLineHeight);
        var nameRect = new Rect(NameHeaderTextRect.x, region.y, NameHeaderTextRect.width, RowLineHeight);
        var priceRect = new Rect(PriceHeaderTextRect.x, region.y, PriceHeaderTextRect.width, RowLineHeight);

        Rect settingRect = LayoutHelper.IconRect(
            _expandedHeaderRect.x + 2f,
            region.y + Mathf.FloorToInt(Mathf.Abs(_expandedHeaderRect.width - RowLineHeight) / 2f) + 2f,
            _expandedHeaderRect.width - 4f,
            _expandedHeaderRect.width - 4f
        );

        bool proxy = item.Data.Enabled;

        if (CheckboxDrawer.DrawCheckbox(checkboxRect, ref proxy))
        {
            item.Data.Enabled = proxy;
        }

        DrawConfigurableItemName(nameRect, item);

        if (!item.EditingName)
        {
            Widgets.DrawHighlightIfMouseover(nameMouseOverRect);

            var builder = new StringBuilder();

            if (!item.Data.Description.NullOrEmpty())
            {
                builder.AppendLine(item.Data.Description);
                builder.AppendLine();
            }

            foreach (string i in item.Data.PawnData.Stats)
            {
                builder.AppendLine(i);
            }

            TooltipHandler.TipRegion(nameMouseOverRect, builder.ToString());
        }

        if (item.Data.Enabled)
        {
            int cost = item.Data.Cost;
            SettingsHelper.DrawPriceField(priceRect, ref cost);
            item.Data.Cost = cost;
        }

        if (Widgets.ButtonImage(settingRect, Textures.Gear))
        {
            item.SettingsVisible = !item.SettingsVisible;
        }

        if (!item.SettingsVisible)
        {
            return;
        }

        // Calculate dynamic height based on whether xenotype filtering is enabled
        float expandedHeight = CalculateExpandedHeight(item);

        var expandedRect = new Rect(
            NameHeaderRect.x + 10f,
            region.y + RowLineHeight + 10f,
            region.width - checkboxRect.width - settingRect.width - 20f,
            expandedHeight
        );


        GUI.BeginGroup(expandedRect);
        DrawExpandedSettings(expandedRect.AtZero(), item);
        GUI.EndGroup();
    }
    // NEW: Calculate dynamic height for expanded settings
    // More precise version of CalculateExpandedHeight
    private float CalculateExpandedHeight(TableSettingsItem<PawnKindItem> item)
    {
        float baseHeight = 80f; // Base height for karma settings

        if (ModsConfig.BiotechActive && item.Data.IsXenotypeFilteringEnabled())
        {
            var xenotypes = GetFilteredXenotypesForRace(item.Data);
            if (xenotypes.Count > 0)
            {
                // Calculate height based on number of filtered xenotypes
                int visibleRows = Mathf.Min(xenotypes.Count, 5); // Show max 5 at once
                baseHeight += 25f + (visibleRows * (RowLineHeight + 2f)); // Header + xenotype rows
            }
        }

        return baseHeight;
    }
    private void DrawConfigurableItemName(Rect region, TableSettingsItem<PawnKindItem> item)
    {
        if (item.EditingName)
        {
            var fieldRect = new Rect(region.x, region.y, region.width - region.height, region.height);

            if (FieldDrawer.DrawTextField(fieldRect, item.Data.Name, out string? result))
            {
                item.Data.Name = result.ToToolkit();
                item.Data.PawnData.CustomName = true;
            }

            if (item.Data.PawnData.CustomName && ButtonDrawer.DrawFieldButton(fieldRect, Textures.Reset, _resetPawnNameTooltip))
            {
                item.Data.PawnData.CustomName = false;
            }
        }
        else
        {
            LabelDrawer.Draw(region, item.Data.Name);
        }

        GUI.color = new Color(1f, 1f, 1f, 0.7f);

        if (ButtonDrawer.DrawFieldButton(
            region,
            item.EditingName ? Widgets.CheckboxOffTex : Textures.Edit,
            item.EditingName ? _closePawnNameTooltip : _editPawnNameTooltip
        ))
        {
            item.EditingName = !item.EditingName;
        }

        GUI.color = Color.white;
    }

    /// <inheritdoc cref="TableWorkerBase.Prepare" />
    public override void Prepare()
    {
        TkUtils.Logger.Debug($"=== COMPAT REGISTRY CHECK ===");
        TkUtils.Logger.Debug($"CompatRegistry.Alien is null: {CompatRegistry.Alien == null}");
        TkUtils.Logger.Debug($"Type: {CompatRegistry.Alien?.GetType()}");
        TkUtils.Logger.Debug($"ModId: {CompatRegistry.Alien?.ModId}");
        // CompatRegistry.ForceInitialize();
        DebugXenotypes(); // Call the debug method to log xenotype information temporarily
        LoadTranslations();

        InternalData ??= new List<TableSettingsItem<PawnKindItem>>();
        InternalData.AddRange(ToolkitUtils.Data.PawnKinds.OrderBy(i => i.Name).Select(i => new TableSettingsItem<PawnKindItem> { Data = i }));
    }
    private void DebugXenotypes()
    {
        TkUtils.Logger.Warn("=== XENOTYPE DEBUG INFO ===");
        TkUtils.Logger.Warn($"Total xenotypes in database: {XenotypeHelper.AllXenotypes.Count}");

        foreach (var xenotype in XenotypeHelper.AllXenotypes)
        {
            TkUtils.Logger.Warn($"Xenotype: {xenotype.defName}, Label: {xenotype.label}");
        }

        // Check if Nyaron xenotype exists
        var nyaronXenotype = XenotypeHelper.AllXenotypes.FirstOrDefault(x => x.defName.Contains("Nyaron"));
        TkUtils.Logger.Warn($"Nyaron xenotype found: {nyaronXenotype != null}");
        if (nyaronXenotype != null)
        {
            TkUtils.Logger.Warn($"Nyaron xenotype details: {nyaronXenotype.defName}, {nyaronXenotype.label}");
        }

        TkUtils.Logger.Warn("=== END DEBUG INFO ===");
    }

    private void DrawExpandedSettings(Rect region, TableSettingsItem<PawnKindItem> item)
    {
        if (!ModsConfig.BiotechActive)
        {
            // Use original two-column layout if Biotech not active
            float columnWidth = Mathf.FloorToInt(region.width / 2f) - 26f;
            var leftColumnRect = new Rect(region.x, region.y, columnWidth, region.height);
            var rightColumnRect = new Rect(region.x + leftColumnRect.width + 52f, region.y, columnWidth, region.height);

            Widgets.DrawLineVertical(Mathf.FloorToInt(region.width / 2f), 0f, region.height - 5f);

            GUI.BeginGroup(leftColumnRect);
            DrawLeftExpandedSettingsColumn(leftColumnRect.AtZero(), item);
            GUI.EndGroup();

            GUI.BeginGroup(rightColumnRect);
            // Right column remains unused when Biotech is not active
            GUI.EndGroup();
        }
        else
        {
            // Three-column layout when Biotech is active
            float columnWidth = Mathf.FloorToInt(region.width / 3f) - 17f;
            var leftColumnRect = new Rect(region.x, region.y, columnWidth, region.height);
            var middleColumnRect = new Rect(region.x + columnWidth + 26f, region.y, columnWidth, region.height);
            var rightColumnRect = new Rect(region.x + (columnWidth + 26f) * 2f, region.y, columnWidth, region.height);

            Widgets.DrawLineVertical(columnWidth + 13f, 0f, region.height - 5f);
            Widgets.DrawLineVertical((columnWidth + 26f) * 2f - 13f, 0f, region.height - 5f);

            GUI.BeginGroup(leftColumnRect);
            DrawLeftExpandedSettingsColumn(leftColumnRect.AtZero(), item);
            GUI.EndGroup();

            GUI.BeginGroup(middleColumnRect);
            // Middle column remains unused (for future features)
            GUI.EndGroup();

            GUI.BeginGroup(rightColumnRect);
            DrawRightExpandedSettingsColumn(rightColumnRect.AtZero(), item);
            GUI.EndGroup();
        }
    }

    private void DrawLeftExpandedSettingsColumn(Rect region, ITableItem<PawnKindItem> item)
    {
        (Rect karmaLabel, Rect karmaField) = new Rect(0f, 0f, region.width, RowLineHeight).Split(0.6f);
        LabelDrawer.Draw(karmaLabel, _karmaTypeText);

        if (Widgets.ButtonText(karmaField, item.Data.Data.KarmaType == null ? _defaultKarmaTypeText : item.Data.Data.KarmaType.ToString()))
        {
            Find.WindowStack.Add(
                new FloatMenu(ToolkitUtils.Data.KarmaTypes.Values.Select(i => new FloatMenuOption(i.ToString(), () => item.Data.Data.KarmaType = i)).ToList())
            );
        }

        if (item.Data.Data.KarmaType != null && ButtonDrawer.DrawFieldButton(karmaLabel, Textures.Reset, _resetPawnKarmaTooltip))
        {
            item.Data.Data.KarmaType = null;
        }
    }

    // In PawnTableWorker.cs - Update the DrawRightExpandedSettingsColumn method
    private void DrawRightExpandedSettingsColumn(Rect region, TableSettingsItem<PawnKindItem> item)
    {
        if (!ModsConfig.BiotechActive)
        {
            // Show message if Biotech is not active
            var messageRect = new Rect(0f, 0f, region.width, RowLineHeight);
            LabelDrawer.Draw(messageRect, "TKUtils.Xenotype.BiotechRequired".Localize());
            return;
        }

        // Xenotype filter toggle
        var filterToggleRect = new Rect(0f, 0f, region.width, RowLineHeight);
        DrawXenotypeFilterToggle(filterToggleRect, item);

        if (item.Data.IsXenotypeFilteringEnabled())
        {
            // Xenotype list
            var listRect = new Rect(0f, RowLineHeight + 5f, region.width, region.height - RowLineHeight - 5f);
            DrawXenotypeList(listRect, item);
        }
    }
    private void DrawXenotypeFilterToggle(Rect rect, TableSettingsItem<PawnKindItem> item)
    {
        var labelRect = new Rect(rect.x, rect.y, rect.width - 20f, rect.height);
        var toggleRect = new Rect(rect.x + rect.width - 20f, rect.y, 20f, rect.height);

        LabelDrawer.Draw(labelRect, "TKUtils.Xenotype.EnableFilter".Localize());

        bool filterEnabled = item.Data.PawnData.XenotypeFilterEnabled;

        // Check if this race has HAR restrictions
        bool hasHARRestrictions = item.Data.HasHARXenotypeRestrictions();

        if (Widgets.ButtonImage(toggleRect, filterEnabled ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex))
        {
            item.Data.PawnData.XenotypeFilterEnabled = !filterEnabled;

            // If enabling filter for the first time, initialize with appropriate defaults
            if (item.Data.PawnData.XenotypeFilterEnabled && item.Data.PawnData.AllowedXenotypes.Count == 0)
            {
                var filteredXenotypes = GetFilteredXenotypesForRace(item.Data);

                if (hasHARRestrictions)
                {
                    // For HAR-restricted races, allow all HAR-permitted xenotypes by default
                    foreach (var xenotype in filteredXenotypes)
                    {
                        item.Data.PawnData.AllowedXenotypes.Add(xenotype.defName);
                    }
                    TkUtils.Logger.Debug($"Initialized HAR-restricted filter with {filteredXenotypes.Count} xenotypes");
                }
                else
                {
                    // For non-restricted races, allow all xenotypes by default
                    foreach (var xenotype in XenotypeHelper.AllXenotypes)
                    {
                        item.Data.PawnData.AllowedXenotypes.Add(xenotype.defName);
                    }
                    TkUtils.Logger.Debug($"Initialized non-restricted filter with {XenotypeHelper.AllXenotypes.Count} xenotypes");
                }
            }
        }

        // Tooltip - show different tooltips based on whether HAR restrictions exist
        string tooltipKey = hasHARRestrictions ?
            "TKUtils.Xenotype.FilterTooltipHAR" :
            "TKUtils.Xenotype.FilterTooltip";
        TooltipHandler.TipRegion(rect, tooltipKey.Localize());
    }
    private Vector2 _xenotypeScrollPos = Vector2.zero;

    private void DrawXenotypeList(Rect rect, TableSettingsItem<PawnKindItem> item)
    {
        // Get filtered xenotypes based on race restrictions
        var xenotypes = GetFilteredXenotypesForRace(item.Data);
        if (xenotypes.Count == 0)
        {
            var messageRect = new Rect(rect.x, rect.y, rect.width, RowLineHeight);
            LabelDrawer.Draw(messageRect, "TKUtils.Xenotype.NoXenotypes".Localize());
            return;
        }

        // Header with select all/none buttons
        var headerRect = new Rect(rect.x, rect.y, rect.width, RowLineHeight);
        DrawXenotypeListHeader(headerRect, item, xenotypes);

        // Calculate list height - show 4-5 xenotypes at once
        float listHeight = Mathf.Min(rect.height - RowLineHeight - 5f, RowLineHeight * 5);
        var listRect = new Rect(rect.x, rect.y + RowLineHeight + 5f, rect.width, listHeight);

        float contentHeight = xenotypes.Count * (RowLineHeight + 2f);
        var viewRect = new Rect(0f, 0f, listRect.width - 16f, contentHeight);

        // Store the scroll position in a variable and ensure it's maintained
        _xenotypeScrollPos = GUI.BeginScrollView(listRect, _xenotypeScrollPos, viewRect);

        float yPos = 0f;
        foreach (var xenotype in xenotypes)
        {
            var xenotypeRect = new Rect(0f, yPos, viewRect.width, RowLineHeight);

            // Only draw if visible in scroll view (optimization)
            if (yPos + RowLineHeight >= _xenotypeScrollPos.y && yPos <= _xenotypeScrollPos.y + listRect.height)
            {
                DrawXenotypeListItem(xenotypeRect, xenotype, item);
            }

            yPos += RowLineHeight + 2f;
        }

        GUI.EndScrollView();

        // Show count of allowed xenotypes
        if (xenotypes.Count > 0)
        {
            int allowedCount = item.Data.GetAllowedXenotypes().Count(x => xenotypes.Any(xt => xt.defName == x));
            var countRect = new Rect(rect.x, rect.y + listHeight + RowLineHeight + 10f, rect.width, RowLineHeight);
            string countText = "TKUtils.Xenotype.AllowedCount".LocalizeKeyed(allowedCount.ToString(), xenotypes.Count.ToString());
            LabelDrawer.Draw(countRect, countText);
        }
    }
    // NEW: Get filtered xenotypes based on race restrictions
    private List<XenotypeDef> GetFilteredXenotypesForRace(PawnKindItem item)
    {
        if (!ModsConfig.BiotechActive)
        {
            TkUtils.Logger.Debug($"Biotech not active for {item.DefName}");
            return new List<XenotypeDef>();
        }

        var raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
        if (raceDef == null)
        {
            TkUtils.Logger.Warn($"Could not find race def for {item.DefName}");
            return XenotypeHelper.AllXenotypes;
        }

        TkUtils.Logger.Debug($"=== PROCESSING RACE: {raceDef.defName} ===");

        // Humans can use any xenotype
        if (raceDef == ThingDefOf.Human)
        {
            TkUtils.Logger.Debug($"Human race detected - allowing all xenotypes");
            return XenotypeHelper.AllXenotypes;
        }

        // Check if this race has HAR restrictions using the provider
        if (CompatRegistry.Alien != null)
        {
            List<string> harAllowedXenotypeDefNames = CompatRegistry.Alien.GetAllowedXenotypes(raceDef);

            TkUtils.Logger.Debug($"HAR provider returned {harAllowedXenotypeDefNames.Count} allowed xenotypes");

            // If the provider returns ANY xenotypes (even empty), use that as the definitive list
            // This means the race is managed by HAR and we should respect its restrictions
            var filteredXenotypes = XenotypeHelper.AllXenotypes
                .Where(xeno => harAllowedXenotypeDefNames.Contains(xeno.defName))
                .ToList();

            TkUtils.Logger.Debug($"Showing {filteredXenotypes.Count} xenotypes for {raceDef.defName} (HAR managed)");
            return filteredXenotypes;
        }
        else
        {
            // No HAR provider - show all xenotypes for non-human races
            TkUtils.Logger.Debug($"No HAR provider - allowing all xenotypes for {raceDef.defName}");
            return XenotypeHelper.AllXenotypes;
        }
    }

    private void DebugRaceType(ThingDef raceDef)
    {
        TkUtils.Logger.Debug($"=== RACE TYPE DEBUG for {raceDef.defName} ===");
        TkUtils.Logger.Debug($"Race type: {raceDef.GetType()}");

        // Check if this is a ThingDef_AlienRace using reflection
        bool isAlienRace = raceDef.GetType().Name == "ThingDef_AlienRace";
        TkUtils.Logger.Debug($"Is ThingDef_AlienRace: {isAlienRace}");

        if (isAlienRace)
        {
            try
            {
                // Use reflection to access alienRace field
                var alienRaceField = raceDef.GetType().GetField("alienRace");
                if (alienRaceField != null)
                {
                    var alienRaceValue = alienRaceField.GetValue(raceDef);
                    TkUtils.Logger.Debug($"AlienRace field exists: {alienRaceValue != null}");

                    if (alienRaceValue != null)
                    {
                        var raceRestrictionProperty = alienRaceValue.GetType().GetProperty("raceRestriction");
                        if (raceRestrictionProperty != null)
                        {
                            var restriction = raceRestrictionProperty.GetValue(alienRaceValue);
                            TkUtils.Logger.Debug($"Race restriction exists: {restriction != null}");

                            if (restriction != null)
                            {
                                var onlyUseProperty = restriction.GetType().GetProperty("onlyUseRaceRestrictedXenotypes");
                                var whiteListProperty = restriction.GetType().GetProperty("whiteXenotypeList");

                                if (onlyUseProperty != null)
                                {
                                    var onlyUseValue = onlyUseProperty.GetValue(restriction);
                                    TkUtils.Logger.Debug($"onlyUseRaceRestrictedXenotypes: {onlyUseValue}");
                                }

                                if (whiteListProperty != null)
                                {
                                    var whiteListValue = whiteListProperty.GetValue(restriction) as List<XenotypeDef>;
                                    TkUtils.Logger.Debug($"whiteXenotypeList exists: {whiteListValue != null}");
                                    if (whiteListValue != null)
                                    {
                                        TkUtils.Logger.Debug($"whiteXenotypeList count: {whiteListValue.Count}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TkUtils.Logger.Error($"Error during race type debug: {ex}");
            }
        }
        TkUtils.Logger.Debug($"=== END RACE TYPE DEBUG ===");
    }

    private void DrawXenotypeListHeader(Rect rect, TableSettingsItem<PawnKindItem> item, List<XenotypeDef> filteredXenotypes)
    {
        var selectAllRect = new Rect(rect.x, rect.y, rect.width * 0.5f - 5f, rect.height);
        var selectNoneRect = new Rect(rect.x + rect.width * 0.5f + 5f, rect.y, rect.width * 0.5f - 5f, rect.height);

        if (Widgets.ButtonText(selectAllRect, "TKUtils.Xenotype.SelectAll".Localize()))
        {
            // Allow all xenotypes in the filtered list
            foreach (var xenotype in filteredXenotypes)
            {
                item.Data.ToggleXenotype(xenotype.defName, true);
            }
            TkUtils.Logger.Debug($"Selected all {filteredXenotypes.Count} xenotypes for {item.Data.Name}");
        }

        if (Widgets.ButtonText(selectNoneRect, "TKUtils.Xenotype.SelectNone".Localize()))
        {
            // Disable all xenotypes in the filtered list
            foreach (var xenotype in filteredXenotypes)
            {
                item.Data.ToggleXenotype(xenotype.defName, false);
            }
            TkUtils.Logger.Debug($"Deselected all {filteredXenotypes.Count} xenotypes for {item.Data.Name}");
        }
    }
    private void DrawXenotypeListItem(Rect rect, XenotypeDef xenotype, TableSettingsItem<PawnKindItem> item)
    {
        var toggleRect = new Rect(rect.x, rect.y, 20f, rect.height);
        var labelRect = new Rect(rect.x + 25f, rect.y, rect.width - 25f, rect.height);

        bool isAllowed = item.Data.IsXenotypeAllowed(xenotype.defName);
        bool newAllowed = isAllowed;

        // Draw toggle
        if (Widgets.ButtonImage(toggleRect, newAllowed ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex))
        {
            newAllowed = !newAllowed;
            item.Data.ToggleXenotype(xenotype.defName, newAllowed);
        }

        // Draw label with tooltip
        LabelDrawer.Draw(labelRect, XenotypeHelper.GetDisplayName(xenotype));

        // Tooltip with xenotype description
        if (!xenotype.descriptionShort.NullOrEmpty())
        {
            TooltipHandler.TipRegion(labelRect, xenotype.descriptionShort);
        }

        // Highlight on mouseover
        if (Mouse.IsOver(rect))
        {
            Widgets.DrawHighlight(rect);
        }
    }
    /// <inheritdoc cref="TableWorker{T}.EnsureExists" />
    public override void EnsureExists(TableSettingsItem<PawnKindItem> data)
    {
        if (!InternalData.Any(i => i.Data.DefName.Equals(data.Data.DefName)))
        {
            InternalData.Add(data);
        }
    }

    /// <inheritdoc cref="TableWorker{T}.NotifyGlobalDataChanged" />
    public override void NotifyGlobalDataChanged()
    {
        var wasDirty = false;

        foreach (PawnKindItem item in ToolkitUtils.Data.PawnKinds.Select(item => new { item, existing = InternalData.Find(i => i.Data.Equals(item)) })
           .Where(t => t.existing == null)
           .Select(t => t!.item))
        {
            InternalData.Add(new TableSettingsItem<PawnKindItem> { Data = item });
            wasDirty = true;
        }

        if (wasDirty)
        {
            NotifySortRequested();
        }
    }

    private void LoadTranslations()
    {
        _nameHeaderText = "TKUtils.Headers.Name".Localize();
        _priceHeaderText = "TKUtils.Headers.Price".Localize();

        _karmaTypeText = "TKUtils.Fields.KarmaType".Localize();
        _defaultKarmaTypeText = "TKUtils.Fields.DefaultKarmaType".Localize();

        _editPawnNameTooltip = "TKUtils.PawnTableTooltips.EditPawnName".Localize();
        _closePawnNameTooltip = "TKUtils.PawnTableTooltips.ClosePawnName".Localize();
        _resetPawnNameTooltip = "TKUtils.PawnTableTooltips.ResetPawnName".Localize();
        _resetPawnKarmaTooltip = "TKUtils.PawnTableTooltips.ResetPawnKarma".Localize();
        // NEW: Xenotype translations
        _xenotypeFilterText = "TKUtils.Fields.XenotypeFilter".Localize();
        _defaultKarmaTypeText = "TKUtils.Fields.DefaultKarmaType".Localize();

        _editPawnNameTooltip = "TKUtils.PawnTableTooltips.EditPawnName".Localize();
        _closePawnNameTooltip = "TKUtils.PawnTableTooltips.ClosePawnName".Localize();
        _resetPawnNameTooltip = "TKUtils.PawnTableTooltips.ResetPawnName".Localize();
        _resetPawnKarmaTooltip = "TKUtils.PawnTableTooltips.ResetPawnKarma".Localize();

        // NEW: Xenotype tooltips
        _xenotypeFilterTooltip = "TKUtils.PawnTableTooltips.XenotypeFilter".Localize();
    }

    /// <inheritdoc cref="TableWorkerBase.NotifySortRequested" />
    public override void NotifySortRequested()
    {
        switch (_sortOrder)
        {
            case SortOrder.Ascending:
                NotifyAscendingSortRequested();

                return;
            case SortOrder.Descending:
                NotifyDescendingSortRequested();

                return;
            default:
                return;
        }
    }

    private void NotifyDescendingSortRequested()
    {
        switch (_sortKey)
        {
            case SortKey.Price:
                InternalData = InternalData.OrderBy(i => i.Data.Cost).ThenBy(i => i.Data.Name).ToList();

                return;
            default:
                InternalData = InternalData.OrderBy(i => i.Data.Name).ToList();

                return;
        }
    }

    private void NotifyAscendingSortRequested()
    {
        switch (_sortKey)
        {
            case SortKey.Price:
                InternalData = InternalData.OrderByDescending(i => i.Data.Cost).ThenByDescending(i => i.Data.Name).ToList();

                return;
            default:
                InternalData = InternalData.OrderByDescending(i => i.Data.Name).ToList();

                return;
        }
    }

    /// <inheritdoc cref="TableWorkerBase.NotifySearchRequested" />
    public override void NotifySearchRequested(string query)
    {
        FilterDataBySearch(query);
    }

    /// <inheritdoc cref="TableWorkerBase.NotifyResolutionChanged" />
    public override void NotifyResolutionChanged(Rect region)
    {
        float consumedWidth = region.width - 18f - LineHeight * 2f; // Icon buttons
        _stateHeaderRect = new Rect(0f, 0f, LineHeight, LineHeight);
        _stateHeaderInnerRect = _stateHeaderRect.ContractedBy(2f);
        NameHeaderRect = new Rect(LineHeight + 1f, 0f, Mathf.FloorToInt(consumedWidth * 0.55f), LineHeight);
        NameHeaderTextRect = new Rect(NameHeaderRect.x + 4f, NameHeaderRect.y, NameHeaderRect.width - 8f, NameHeaderRect.height);
        PriceHeaderRect = new Rect(NameHeaderRect.x + NameHeaderRect.width + 1f, 0f, Mathf.FloorToInt(consumedWidth * 0.45f), LineHeight);
        PriceHeaderTextRect = new Rect(PriceHeaderRect.x + 4f, PriceHeaderRect.y, PriceHeaderRect.width - 8f, PriceHeaderRect.height);
        _expandedHeaderRect = new Rect(PriceHeaderRect.x + PriceHeaderRect.width + 1f, 0f, LineHeight, LineHeight);
        _expandedHeaderInnerRect = _expandedHeaderRect.ContractedBy(2f);
    }

    /// <inheritdoc cref="TableWorker{T}.NotifyCustomSearchRequested" />
    public override void NotifyCustomSearchRequested(Func<TableSettingsItem<PawnKindItem>, bool> worker)
    {
        foreach (TableSettingsItem<PawnKindItem> item in Data)
        {
            item.IsHidden = !worker(item);
        }
    }

    /// <inheritdoc cref="TableWorkerBase.FilterDataBySearch" />
    private protected override void FilterDataBySearch(string query)
    {
        foreach (TableSettingsItem<PawnKindItem> item in Data)
        {
            item.IsHidden = !query.NullOrEmpty() && !item.Data.Name.ToLower().Contains(query.ToLower());
        }
    }

    private enum SortKey { Name, Price }
}

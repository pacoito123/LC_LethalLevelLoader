using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace LethalLevelLoader
{
    public static class TerminalManager
    {
        internal static Terminal Terminal
        {
            get
            {
                if (field == null)
                    field = UnityEngine.Object.FindAnyObjectByType<Terminal>(FindObjectsInactive.Exclude);
                return field;
            }
        }

        internal static TerminalNode lockedNode;

        public static MoonsCataloguePage defaultMoonsCataloguePage { get; internal set; }
        public static MoonsCataloguePage currentMoonsCataloguePage { get; internal set; }
        internal static int moonsInCataloguePage;
        internal static int linesInCataloguePage;
        internal static float linesToScroll = 20.0f; // TODO: Make configurable maybe?

        //Cached References To Important Base-Game TerminalKeywords;
        internal static TerminalKeyword routeKeyword;
        internal static TerminalKeyword routeInfoKeyword;
        internal static TerminalKeyword routeConfirmKeyword;
        internal static TerminalKeyword routeDenyKeyword;
        internal static TerminalKeyword moonsKeyword;
        internal static TerminalKeyword viewKeyword;
        internal static TerminalKeyword buyKeyword;
        internal static TerminalNode cancelRouteNode;
        internal static TerminalNode cancelPurchaseNode;

        //Cached References To LLL TerminalKeywords;
        internal static TerminalKeyword previewKeyword;
        internal static TerminalKeyword sortKeyword;
        internal static TerminalKeyword filterKeyword;
        internal static TerminalKeyword simulateKeyword;

        internal static string currentTagFilter;

        public static float defaultTerminalFontSize;

        internal static TerminalKeyword lastParsedVerbKeyword;

        public delegate string PreviewInfoText(ExtendedLevel extendedLevel, PreviewInfoType infoType);
        public static event PreviewInfoText onBeforePreviewInfoTextAdded;

        //internal static Dictionary<TerminalNode, Action<TerminalNode, TerminalNode>> terminalNodeRegisteredEventDictionary = new Dictionary<TerminalNode, Action<TerminalNode, TerminalNode>>();

        public enum LoadNodeActionType { Before, After }
        public delegate bool LoadNodeAction(ref TerminalNode currentNode, ref TerminalNode loadNode);

        internal static Dictionary<TerminalNode, LoadNodeAction> onBeforeLoadNewNodeRegisteredEventsDictionary = new Dictionary<TerminalNode, LoadNodeAction>();
        internal static Dictionary<TerminalNode, LoadNodeAction> onLoadNewNodeRegisteredEventsDictionary = new Dictionary<TerminalNode, LoadNodeAction>();

        ////////// Setting Data //////////

        internal static void CacheTerminalReferences()
        {
            routeKeyword = Terminal.terminalNodes.allKeywords[27];
            routeInfoKeyword = Terminal.terminalNodes.allKeywords[6];
            routeConfirmKeyword = Terminal.terminalNodes.allKeywords[3];
            routeDenyKeyword = Terminal.terminalNodes.allKeywords[4];
            moonsKeyword = Terminal.terminalNodes.allKeywords[21];
            viewKeyword = Terminal.terminalNodes.allKeywords[19];
            buyKeyword = Terminal.terminalNodes.allKeywords[0];
            cancelRouteNode = routeKeyword.compatibleNouns[0].result.terminalOptions[0].result;
            cancelPurchaseNode = buyKeyword.compatibleNouns[0].result.terminalOptions[1].result;

            defaultTerminalFontSize = Terminal.screenText.textComponent.fontSize;

            lockedNode = CreateNewTerminalNode();
            lockedNode.name = "lockedLevelNode";
            lockedNode.acceptAnything = false;
            lockedNode.clearPreviousText = true;
        }

        internal static bool OnBeforeRouteNodeLoaded(ref TerminalNode currentNode, ref TerminalNode loadNode)
        {
            TerminalNode confirmNode = loadNode.terminalOptions[1].result;
            if (confirmNode.buyRerouteToMoon < 0 || confirmNode.buyRerouteToMoon > Patches.StartOfRound.levels.Length - 1)
            {
                DebugHelper.LogError($"Invalid DisplayPlanetInfo For Route Node: {confirmNode.name}", DebugType.User);
                return (true);
            }
            ExtendedLevel extendedLevel = LevelManager.GetExtendedLevel(Patches.StartOfRound.levels[confirmNode.buyRerouteToMoon]);

            if (extendedLevel == null)
            {
                DebugHelper.LogError($"ExtendedLevel Was Null For Route Node: {confirmNode.name}", DebugType.User);
                return (true);
            }
            if (currentNode != null)
                DebugHelper.Log($"LockedNodeEventTest: ExtendedLevel Is: {extendedLevel}, CurrentNode Is: {currentNode.name}, LoadNode Is: {confirmNode.name}", DebugType.User);
            else
                DebugHelper.Log($"LockedNodeEventTest: ExtendedLevel Is: {extendedLevel}, CurrentNode Is Null, LoadNode Is: {confirmNode.name}", DebugType.User);

            if (extendedLevel.IsRouteLocked == true)
                SwapRouteNodeToLockedNode(extendedLevel, ref loadNode);
            return (true);
        }

        internal static void SwapRouteNodeToLockedNode(ExtendedLevel extendedLevel, ref TerminalNode terminalNode)
        {
            lockedNode.displayText = (!string.IsNullOrEmpty(extendedLevel.LockedRouteNodeText))
                ? $"{extendedLevel.LockedRouteNodeText}\n\n\n"
                : $"Route to {extendedLevel.SelectableLevel.PlanetName} is currently locked.\n\n\n";
            terminalNode = lockedNode;
        }

        internal static void RefreshExtendedLevelGroups()
        {
            currentMoonsCataloguePage.RebuildLevelGroups(defaultMoonsCataloguePage.ExtendedLevelGroups, Settings.moonsCatalogueSplitCount);
            if (Settings.levelPreviewSortType is not SortInfoType.None)
                SortMoonsCataloguePage(currentMoonsCataloguePage);
            FilterMoonsCataloguePage(currentMoonsCataloguePage);

            // Get total amount of moons displayed in the terminal listing.
            foreach (ExtendedLevelGroup extendedLevelGroup in currentMoonsCataloguePage.ExtendedLevelGroups)
                foreach (ExtendedLevel extendedLevel in extendedLevelGroup.extendedLevelsList)
                    if (extendedLevel.IsRouteHidden == false)
                        moonsInCataloguePage++;
        }

        internal static bool SetSimulationResultsText(ref TerminalNode currentNode, ref TerminalNode node)
        {
            foreach (ExtendedLevel extendedLevel in PatchedContent.ExtendedLevels)
                if (node.terminalEvent.EqualsSanitized(extendedLevel.NumberlessPlanetName))
                {
                    node.displayText = $"{GetSimulationResultsText(extendedLevel)}\n\n";
                    node.clearPreviousText = true;
                    node.isConfirmationNode = true;
                    break;
                }
            return (true);
        }

        internal static bool OnBeforeLoadNewNode(ref TerminalNode node)
        {
            if (onBeforeLoadNewNodeRegisteredEventsDictionary.TryGetValue(node, out LoadNodeAction pair))
            {
                DebugHelper.Log($"Running OnBeforeLoadNewNode Event For: {node.name}, CurrentNode Is: {Terminal.currentNode}", DebugType.Developer);
                return (pair.Invoke(ref Terminal.currentNode, ref node));
            }
            else
            {
                DebugHelper.Log($"Could Not Find Registered Event For: {node.name}", DebugType.Developer);
                return (true);
            }
        }

        internal static void OnLoadNewNode(ref TerminalNode node)
        {
            if (onLoadNewNodeRegisteredEventsDictionary.TryGetValue(node, out LoadNodeAction pair))
            {
                DebugHelper.Log("Running OnLoadNewNode Event For: " + node.name + ", CurrentNode Is: " + Terminal.currentNode, DebugType.Developer);
                pair.Invoke(ref Terminal.currentNode, ref node);
            }
            else
                DebugHelper.Log("Could Not Find Registered Event For: " + node.name, DebugType.Developer);
        }

        internal static bool RunLethalLevelLoaderTerminalEvents(TerminalNode node)
        {
            /*if (node != null && string.IsNullOrEmpty(node.terminalEvent) == false)
            {
                //DebugHelper.Log("Running LLL Terminal Event: " + node.terminalEvent + "| EnumValue: " + GetTerminalEventEnum(node.terminalEvent) + " | StringValue: " + GetTerminalEventString(node.terminalEvent));
                if (node.name.Contains("preview") && Enum.TryParse(typeof(PreviewInfoType), GetTerminalEventEnum(node.terminalEvent), out object previewEnumValue))
                    Settings.levelPreviewInfoType = (PreviewInfoType)previewEnumValue;
                else if (node.name.Contains("sort") && Enum.TryParse(typeof(SortInfoType), GetTerminalEventEnum(node.terminalEvent), out object sortEnumValue))
                    Settings.levelPreviewSortType = (SortInfoType)sortEnumValue;
                else if (node.name.Contains("filter") && Enum.TryParse(typeof(FilterInfoType), GetTerminalEventEnum(node.terminalEvent), out object filterEnumValue))
                {
                    Settings.levelPreviewFilterType = (FilterInfoType)filterEnumValue;
                    currentTagFilter = GetTerminalEventString(node.terminalEvent);
                    DebugHelper.Log("Tag EventString: " + GetTerminalEventString(node.terminalEvent));
                }

                RefreshExtendedLevelGroups();

                Terminal.screenText.text = Terminal.TextPostProcess("\n" + "\n" + "\n" + GetMoonsTerminalText(), Terminal.currentNode);
                Terminal.currentText = Terminal.TextPostProcess("\n" + "\n" + "\n" + GetMoonsTerminalText(), Terminal.currentNode);

                return (false);
            }*/
            return (true);
        }

        internal static bool TryRefreshMoonsCataloguePage(ref TerminalNode currentNode, ref TerminalNode loadNode)
        {
            if (currentNode == moonsKeyword.specialKeywordResult)
                return (RefreshMoonsCataloguePage(ref currentNode, ref loadNode));
            else
                return (true);
        }

        public static bool RefreshMoonsCataloguePage(ref TerminalNode currentNode, ref TerminalNode loadNode)
        {
            //DebugHelper.Log("Running LLL Terminal Event: " + node.terminalEvent + "| EnumValue: " + GetTerminalEventEnum(node.terminalEvent) + " | StringValue: " + GetTerminalEventString(node.terminalEvent));
            if (loadNode.name.Contains("preview", StringComparison.OrdinalIgnoreCase) && Enum.TryParse(typeof(PreviewInfoType), GetTerminalEventEnum(loadNode.terminalEvent), out object previewEnumValue))
                Settings.levelPreviewInfoType = (PreviewInfoType)previewEnumValue;
            else if (loadNode.name.Contains("sort", StringComparison.OrdinalIgnoreCase) && Enum.TryParse(typeof(SortInfoType), GetTerminalEventEnum(loadNode.terminalEvent), out object sortEnumValue))
                Settings.levelPreviewSortType = (SortInfoType)sortEnumValue;
            else if (loadNode.name.Contains("filter", StringComparison.OrdinalIgnoreCase) && Enum.TryParse(typeof(FilterInfoType), GetTerminalEventEnum(loadNode.terminalEvent), out object filterEnumValue))
            {
                Settings.levelPreviewFilterType = (FilterInfoType)filterEnumValue;
                currentTagFilter = GetTerminalEventString(loadNode.terminalEvent);
                //DebugHelper.Log("Tag EventString: " + GetTerminalEventString(loadNode.terminalEvent));
            }

            RefreshExtendedLevelGroups();

            Terminal.modifyingText = true;
            Terminal.screenText.interactable = true;

            Terminal.screenText.text = Terminal.TextPostProcess($"\n\n\n{GetMoonsTerminalText()}", Terminal.currentNode);
            Terminal.currentText = Terminal.screenText.text;

            Terminal.textAdded = 0;

            Terminal.currentNode = moonsKeyword.specialKeywordResult;
            return (false);
        }

        internal static void FilterMoonsCataloguePage(MoonsCataloguePage moonsCataloguePage)
        {
            List<ExtendedLevel> removeLevelList = new List<ExtendedLevel>();

            foreach (ExtendedLevelGroup extendedLevelGroup in moonsCataloguePage.ExtendedLevelGroups)
                foreach (ExtendedLevel extendedLevel in extendedLevelGroup.extendedLevelsList)
                {
                    bool removeExtendedLevel = (Settings.levelPreviewFilterType) switch
                    {
                        FilterInfoType.Price => (extendedLevel.RoutePrice > Terminal.groupCredits),
                        FilterInfoType.Weather => (!string.IsNullOrEmpty(GetWeatherConditions(extendedLevel))),
                        FilterInfoType.Tag => (!extendedLevel.TryGetTag(currentTagFilter)),
                        _ or FilterInfoType.None or FilterInfoType.TraveledThisRun or FilterInfoType.TraveledThisQuota => extendedLevel.IsRouteHidden,
                    };

                    if (removeExtendedLevel == true)
                        removeLevelList.Add(extendedLevel);
                }
            int removedLevels = 0;
            foreach (ExtendedLevelGroup extendedLevelGroup in moonsCataloguePage.ExtendedLevelGroups)
                removedLevels += extendedLevelGroup.extendedLevelsList.RemoveAll(removeLevelList.Contains);
            if (removedLevels > 0)
                DebugHelper.Log($"Removed '{removedLevels}' filtered or hidden levels from the Moons Catalogue.", DebugType.IAmBatby);

            if (Settings.levelPreviewFilterType is not FilterInfoType.None)
                moonsCataloguePage.RebuildLevelGroups(moonsCataloguePage.ExtendedLevelGroups, Settings.moonsCatalogueSplitCount);
        }

        internal static void SortMoonsCataloguePage(MoonsCataloguePage cataloguePage)
        {
            if (Settings.levelPreviewSortType is SortInfoType.Price)
            {
                cataloguePage.ExtendedLevels.Sort(new ExtendedLevel.ExtendedLevelRoutePriceComparer());
                cataloguePage.RebuildLevelGroups(cataloguePage.ExtendedLevels, Settings.moonsCatalogueSplitCount);
            }
            else if (Settings.levelPreviewSortType is SortInfoType.Difficulty)
            {
                cataloguePage.ExtendedLevels.Sort(new ExtendedLevel.ExtendedLevelDifficultyComparer());
                cataloguePage.RebuildLevelGroups(cataloguePage.ExtendedLevels, Settings.moonsCatalogueSplitCount);
            }
        }

        public static void AddTerminalNodeEventListener(TerminalNode node, LoadNodeAction action, LoadNodeActionType loadNodeActionType)
        {
            if (node != null && action != null)
            {
                if (loadNodeActionType is LoadNodeActionType.Before && onBeforeLoadNewNodeRegisteredEventsDictionary.TryAdd(node, action))
                    DebugHelper.Log($"Successfully Registered OnBeforeLoadNode Action: {action.Method.Name} To TerminalNode: {node.name}", DebugType.Developer);
                else if (loadNodeActionType is LoadNodeActionType.After && onLoadNewNodeRegisteredEventsDictionary.TryAdd(node, action))
                    DebugHelper.Log($"Successfully Registered OnLoadNode Action: {action.Method.Name} To TerminalNode: {node.name}", DebugType.Developer);
            }
        }

        ////////// Getting Data //////////

        internal static string GetMoonsTerminalText()
        {
            string overviewText = "Welcome to the exomoons catalogue.\r\nTo route the autopilot to a moon, use the word ROUTE.\r\nTo learn about any moon, use the word INFO.\r\n____________________________\r\n\r\n* The Company Building   //   Buying at [companyBuyingPercent].\r\n\r\n";
            string[] lines = moonsKeyword.specialKeywordResult.displayText.Split('\n', StringSplitOptions.None);

            int moonsIndex = Array.FindIndex(lines, line => line.Contains("[planetTime]", StringComparison.Ordinal));
            if (moonsIndex != -1)
                overviewText = string.Join('\n', lines[..moonsIndex]) + '\n';
            else
                DebugHelper.LogError("Failed To get Moons Catalogue overview text dynamically, falling back to hardcoded English variant.", DebugType.Developer);

            return ($"{overviewText}{GetMoonCatalogDisplayListings()}\r\n");
        }

        //This is some absolute super arbitrary wizardry to replicate base game >moons command
        public static string GetMoonCatalogDisplayListings()
        {
            string returnString = string.Empty;

            int groupCounter = 0;
            foreach (ExtendedLevelGroup extendedLevelGroup in currentMoonsCataloguePage.ExtendedLevelGroups)
            {
                string groupString = string.Empty;
                foreach (ExtendedLevel extendedLevel in extendedLevelGroup.extendedLevelsList)
                    if (extendedLevel.IsRouteHidden == false)
                    {
                        groupString += $"* {extendedLevel.NumberlessPlanetName} {GetExtendedLevelPreviewInfo(extendedLevel)}\n";
                        if (++groupCounter == Settings.moonsCatalogueSplitCount)
                        {
                            groupString += '\n';
                            groupCounter = 0;
                        }
                    }
                if (!string.IsNullOrEmpty(groupString))
                    returnString += groupString;
            }
            returnString = returnString.TrimEnd('\n');

            string tagString = Settings.levelPreviewFilterType.ToString().ToUpperInvariant();
            if (Settings.levelPreviewFilterType == FilterInfoType.Tag)
                tagString = currentTagFilter.ToUpperInvariant();

            return ($"{returnString}\n____________________________\nPREVIEW: {Settings.levelPreviewInfoType.ToString().ToUpperInvariant()} | SORT: {Settings.levelPreviewSortType.ToString().ToUpperInvariant()} | FILTER: {tagString}\n");
        }

        public static string GetExtendedLevelPreviewInfo(ExtendedLevel extendedLevel)
        {
            string levelPreviewInfo = Settings.levelPreviewInfoType switch
            {
                PreviewInfoType.Price => $"(${extendedLevel.RoutePrice})",
                PreviewInfoType.Difficulty => $"({extendedLevel.SelectableLevel.riskLevel})",
                PreviewInfoType.Weather => GetWeatherConditions(extendedLevel),
                PreviewInfoType.History => $"{GetHistoryConditions(extendedLevel)}",
                PreviewInfoType.All => $"({extendedLevel.SelectableLevel.riskLevel}) (${extendedLevel.RoutePrice}) {GetWeatherConditions(extendedLevel)}",
                PreviewInfoType.Vanilla => $"[planetTime]",
                PreviewInfoType.Override => $"{Settings.GetOverridePreviewInfo(extendedLevel)}",
                _ or PreviewInfoType.None => string.Empty,
            };
            if (extendedLevel.IsRouteLocked == true)
                levelPreviewInfo += " (Locked)";

            string overridePreviewInfo = onBeforePreviewInfoTextAdded?.Invoke(extendedLevel, Settings.levelPreviewInfoType);
            if (!string.IsNullOrEmpty(overridePreviewInfo))
                levelPreviewInfo = overridePreviewInfo;

            return (levelPreviewInfo);
        }

        //Just returns the level weather with a space and ().
        public static string GetWeatherConditions(ExtendedLevel extendedLevel)
        {
            string returnString = string.Empty;
            /*if (extendedLevel.currentExtendedWeatherEffect != null)
                returnString = "(" + extendedLevel.currentExtendedWeatherEffect.weatherDisplayName + ")";*/
            if (extendedLevel.SelectableLevel.currentWeather is not LevelWeatherType.None)
                returnString = $"({extendedLevel.SelectableLevel.currentWeather})";
            return (returnString);
        }

        public static string GetHistoryConditions(ExtendedLevel extendedLevel)
        {
            DayHistory dayHistory = LevelManager.dayHistoryList.Find(dayHistory => dayHistory.extendedLevel == extendedLevel);
            if (dayHistory == null)
                return ($"(Unexplored)");
            else if (Patches.TimeOfDay.timesFulfilledQuota == dayHistory.quota && LevelManager.daysTotal == dayHistory.day)
                return ($"(Explored Yesterday)");
            else if (Patches.TimeOfDay.timesFulfilledQuota == dayHistory.quota)
                return ($"(Explored {LevelManager.daysTotal - dayHistory.day} Ago)");
            else if ((Patches.TimeOfDay.timesFulfilledQuota - 1) == dayHistory.quota)
                return ($"(Explored Last Quota)");
            else
                return ($"Explored {Patches.TimeOfDay.timesFulfilledQuota - dayHistory.quota} Quotas Ago)");
        }

        public static string GetTerminalEventString(string terminalEventString) => (terminalEventString.Split(';', StringSplitOptions.RemoveEmptyEntries)[^1]);
        public static string GetTerminalEventEnum(string terminalEventString) => (terminalEventString.Split(';', StringSplitOptions.RemoveEmptyEntries)[0]);

        public static string GetSimulationResultsText(ExtendedLevel extendedLevel)
        {
            List<ExtendedDungeonFlowWithRarity> availableExtendedFlowsList = [.. DungeonManager.GetValidExtendedDungeonFlows(extendedLevel, true)];
            availableExtendedFlowsList.Sort(new ExtendedDungeonFlowWithRarity.ExtendedDungeonFlowWithRarityComparer(ascending: false));
            string overrideString = $"Simulating arrival to {extendedLevel.SelectableLevel.PlanetName}\nAnalyzing potential remnants found on surface. \nListing generated probabilities below.\n____________________________ \n\nPOSSIBLE STRUCTURES: \n";
            int totalRarityPool = 0;
            foreach (ExtendedDungeonFlowWithRarity extendedDungeonFlowResult in availableExtendedFlowsList)
                totalRarityPool += extendedDungeonFlowResult.rarity;
            foreach (ExtendedDungeonFlowWithRarity extendedDungeonFlowResult in availableExtendedFlowsList)
                overrideString += $"* {extendedDungeonFlowResult.extendedDungeonFlow.DungeonName.PadRight(22).Truncate(22)} //   {GetSimulationDataText(extendedDungeonFlowResult.rarity, totalRarityPool)}\n";
            return (overrideString);
        }

        public static string GetSimulationDataText(int rarity, int totalRarity)
        {
            string returnString = string.Empty;
            if (Settings.levelSimulateInfoType == SimulateInfoType.Percentage)
                returnString = "Chance: " + (rarity / (float)totalRarity * 100).ToString("#0.00", CultureInfo.InvariantCulture).PadLeft(5) + '%';
            else if (Settings.levelSimulateInfoType == SimulateInfoType.Rarity)
                returnString = "Weight: " + $"{rarity}".PadLeft(4) + " / " + totalRarity;
            else if (Settings.levelSimulateInfoType == SimulateInfoType.All)
                returnString = "Weight: " + $"{rarity}".PadRight(5) + (rarity / (float)totalRarity * 100).ToString("\\(#0.0#", CultureInfo.InvariantCulture).PadLeft(6) + "%)";
            return (returnString).PadLeft(5).Truncate(25);
        }

        public static string GetOffsetExtendedLevelName(ExtendedLevel extendedLevel)
        {
            int longestLevelName = 0;
            string returnString = string.Empty;

            foreach (ExtendedLevel currentExtendedLevel in currentMoonsCataloguePage.ExtendedLevels)
            {
                if (currentExtendedLevel.NumberlessPlanetName.Length > longestLevelName)
                    longestLevelName = currentExtendedLevel.NumberlessPlanetName.Length;
            }

            for (int i = 0; i < (longestLevelName - extendedLevel.NumberlessPlanetName.Length); i++)
                returnString += ' ';

            return returnString;
        }

        internal static TerminalKeyword TryFindAlternativeNoun(Terminal terminal, TerminalKeyword foundKeyword, string playerInput)
        {
            if (foundKeyword != null && terminal.hasGottenVerb == false && foundKeyword.isVerb == true)
                lastParsedVerbKeyword = foundKeyword;

            if (foundKeyword != null && foundKeyword.isVerb == false && terminal.hasGottenVerb == true && lastParsedVerbKeyword != null)
            {
                TerminalKeyword nounKeyword = foundKeyword;
                if (ValidateNounKeyword(lastParsedVerbKeyword, nounKeyword) == false)
                    foreach (TerminalKeyword newNounKeyword in Terminal.terminalNodes.allKeywords)
                        if (newNounKeyword.isVerb == false && newNounKeyword != nounKeyword && string.Equals(newNounKeyword.word, playerInput, StringComparison.OrdinalIgnoreCase))
                            if (ValidateNounKeyword(lastParsedVerbKeyword, newNounKeyword) == true)
                            {
                                lastParsedVerbKeyword = null;
                                return (newNounKeyword);
                            }
            }

            //DebugHelper.Log("Returning TerminalKeyword: " + foundKeyword.word);
            return (foundKeyword);
        }

        internal static bool ValidateNounKeyword(TerminalKeyword verbKeyword, TerminalKeyword nounKeyword)
        {
            for (int k = 0; k < verbKeyword.compatibleNouns.Length; k++)
                if (verbKeyword.compatibleNouns[k].noun == nounKeyword)
                    return (true);
            return (false);
        }

        public static List<ExtendedLevelGroup> GetExtendedLevelGroups(ExtendedLevel[] newExtendedLevels, int splitCount)
        {
            List<ExtendedLevelGroup> returnList = new List<ExtendedLevelGroup>();

            int counter = 0;
            int levelsAdded = 0;
            List<ExtendedLevel> currentExtendedLevelsBatch = new List<ExtendedLevel>();
            foreach (ExtendedLevel extendedLevel in new List<ExtendedLevel>(newExtendedLevels))
            {
                currentExtendedLevelsBatch.Add(extendedLevel);
                levelsAdded++;
                counter++;

                if (counter == splitCount || levelsAdded == newExtendedLevels.Length)
                {
                    returnList.Add(new ExtendedLevelGroup(currentExtendedLevelsBatch));
                    currentExtendedLevelsBatch.Clear();
                    counter = 0;
                }
            }

            return (returnList);
        }

        ////////// Creating Data //////////

        internal static void CreateExtendedLevelGroups()
        {
            List<ExtendedLevel> hiddenVanillaLevels = new List<ExtendedLevel>();
            foreach (ExtendedLevel extendedLevel in PatchedContent.VanillaExtendedLevels)
            {
                if (!moonsKeyword.specialKeywordResult.displayText.Contains(extendedLevel.NumberlessPlanetName, StringComparison.Ordinal))
                {
                    extendedLevel.IsRouteHidden = true;
                    hiddenVanillaLevels.Add(extendedLevel);
                }
            }
            hiddenVanillaLevels.Sort(new ExtendedLevel.ExtendedLevelDifficultyComparer());

            DebugHelper.Log("Creating ExtendedLevelGroups", DebugType.Developer);
            foreach (SelectableLevel level in OriginalContent.MoonsCatalogue)
                DebugHelper.Log($"{level.PlanetName}", DebugType.Developer);
            ExtendedLevelGroup vanillaGroupA = new ExtendedLevelGroup(OriginalContent.MoonsCatalogue.GetRange(0, 3));
            ExtendedLevelGroup vanillaGroupB = new ExtendedLevelGroup(OriginalContent.MoonsCatalogue.GetRange(3, 3));
            ExtendedLevelGroup vanillaGroupC = new ExtendedLevelGroup(OriginalContent.MoonsCatalogue.GetRange(6, 3));
            ExtendedLevelGroup vanillaGroupD = new ExtendedLevelGroup(hiddenVanillaLevels);

            Dictionary<string, List<ExtendedLevel>> extendedLevelsContentSourceNameDictionary = new Dictionary<string, List<ExtendedLevel>>();
            foreach (ExtendedLevel customExtendedLevel in PatchedContent.CustomExtendedLevels)
            {
                if (!extendedLevelsContentSourceNameDictionary.TryGetValue(customExtendedLevel.ModName, out List<ExtendedLevel> extendedLevels))
                    extendedLevelsContentSourceNameDictionary.Add(customExtendedLevel.ModName, [customExtendedLevel]);
                else
                    extendedLevels.Add(customExtendedLevel);
            }
            List<ExtendedLevel> singleExtendedLevelsList = new List<ExtendedLevel>();
            List<ExtendedLevelGroup> combinedOrderedCustomExtendedLevelGroups = new List<ExtendedLevelGroup>();
            foreach (KeyValuePair<string, List<ExtendedLevel>> customExtendedLevelLists in new Dictionary<string, List<ExtendedLevel>>(extendedLevelsContentSourceNameDictionary))
            {
                customExtendedLevelLists.Value.Sort(new ExtendedLevel.ExtendedLevelDifficultyComparer());
                extendedLevelsContentSourceNameDictionary[customExtendedLevelLists.Key] = customExtendedLevelLists.Value;
                if (customExtendedLevelLists.Value.Count == 1)
                    singleExtendedLevelsList.Add(customExtendedLevelLists.Value[0]);
                else if (customExtendedLevelLists.Value.Count != 0)
                    foreach (ExtendedLevelGroup extendedLevelGroup in GetExtendedLevelGroups([.. customExtendedLevelLists.Value], Settings.moonsCatalogueSplitCount))
                        combinedOrderedCustomExtendedLevelGroups.Add(extendedLevelGroup);
            }
            singleExtendedLevelsList.Sort(new ExtendedLevel.ExtendedLevelDifficultyComparer());
            combinedOrderedCustomExtendedLevelGroups.AddRange(GetExtendedLevelGroups([.. singleExtendedLevelsList], Settings.moonsCatalogueSplitCount));
            combinedOrderedCustomExtendedLevelGroups.Sort(new ExtendedLevelGroup.ExtendedLevelGroupDifficultyComparer());

            List<ExtendedLevelGroup> allDefaultExtendedLevelGroups = [vanillaGroupA, vanillaGroupB, vanillaGroupC, vanillaGroupD, .. combinedOrderedCustomExtendedLevelGroups];
            string debugString = "Debugging DefaultExtendedLevelsGroups:\n";
            for (int i = 0; i < allDefaultExtendedLevelGroups.Count; i++)
            {
                debugString += $"Group #{i} -> ";
                foreach (ExtendedLevel extendedLevel in allDefaultExtendedLevelGroups[i].extendedLevelsList)
                    debugString += $"{extendedLevel.NumberlessPlanetName}({extendedLevel.ModName}), ";
                debugString = debugString.TrimEnd([',', ' ']);
            }
            DebugHelper.Log(debugString, DebugType.Developer);
            defaultMoonsCataloguePage = new MoonsCataloguePage(allDefaultExtendedLevelGroups);
            currentMoonsCataloguePage = new MoonsCataloguePage([]);
            RefreshExtendedLevelGroups();
        }

        internal static void CreateLevelTerminalData(ExtendedLevel extendedLevel, int routePrice)
        {
            string sanitizedName = extendedLevel.NumberlessPlanetName.Sanitized(toLower: false).RemoveWhitespace();

            //Terminal Route Keyword
            TerminalKeyword terminalKeyword = CreateNewTerminalKeyword();
            terminalKeyword.name = $"{sanitizedName}Keyword";
            terminalKeyword.word = extendedLevel.TerminalNoun;
            terminalKeyword.defaultVerb = routeKeyword;

            //Terminal Route Node
            TerminalNode terminalNodeRoute;
            if (extendedLevel.RouteNode != null)
                terminalNodeRoute = extendedLevel.RouteNode;
            else
            {
                terminalNodeRoute = CreateNewTerminalNode();
                terminalNodeRoute.name = $"{sanitizedName}Route";
                terminalNodeRoute.displayText = (!string.IsNullOrEmpty(extendedLevel.OverrideRouteNodeDescription)) ? extendedLevel.OverrideRouteNodeDescription
                    : $"The cost to route to {extendedLevel.SelectableLevel.PlanetName} is [totalCost]. It is currently [currentPlanetTime] on this moon.\n\nPlease CONFIRM or DENY.\n\n";
                terminalNodeRoute.clearPreviousText = true;
                terminalNodeRoute.buyRerouteToMoon = -2;
                terminalNodeRoute.displayPlanetInfo = extendedLevel.SelectableLevel.levelID;
                terminalNodeRoute.itemCost = routePrice;
                terminalNodeRoute.overrideOptions = true;
            }

            //Terminal Route Confirm Node
            TerminalNode terminalNodeRouteConfirm;
            if (extendedLevel.RouteConfirmNode != null)
                terminalNodeRouteConfirm = extendedLevel.RouteConfirmNode;
            else
            {
                terminalNodeRouteConfirm = CreateNewTerminalNode();
                terminalNodeRouteConfirm.name = $"{sanitizedName}RouteConfirm";
                terminalNodeRouteConfirm.displayText = (!string.IsNullOrEmpty(extendedLevel.OverrideRouteConfirmNodeDescription)) ? extendedLevel.OverrideRouteConfirmNodeDescription
                    : $"Routing autopilot to {extendedLevel.SelectableLevel.PlanetName} Your new balance is [playerCredits]. \n\nPlease enjoy your flight.";
                terminalNodeRouteConfirm.clearPreviousText = true;
                terminalNodeRouteConfirm.buyRerouteToMoon = extendedLevel.SelectableLevel.levelID;
                terminalNodeRouteConfirm.itemCost = routePrice;
            }

            //Terminal Info Node
            TerminalNode terminalNodeInfo;
            if (extendedLevel.InfoNode != null)
                terminalNodeInfo = extendedLevel.InfoNode;
            else
            {
                terminalNodeInfo = CreateNewTerminalNode();
                terminalNodeInfo.name = $"{sanitizedName}Info";
                terminalNodeInfo.clearPreviousText = true;
                terminalNodeInfo.maxCharactersToType = 35;
                string infoString;
                if (!string.IsNullOrEmpty(extendedLevel.OverrideInfoNodeDescription))
                    infoString = extendedLevel.OverrideInfoNodeDescription;
                else
                {
                    infoString = $"{extendedLevel.SelectableLevel.PlanetName}\n----------------------\n";
                    foreach (string line in extendedLevel.SelectableLevel.LevelDescription.Split('\n', StringSplitOptions.None))
                        infoString += $"\n{line}\n";
                }

                terminalNodeInfo.displayText = infoString;
            }

            //Population Into Base game

            terminalNodeRoute.AddCompatibleNoun(routeDenyKeyword, cancelRouteNode);
            terminalNodeRoute.AddCompatibleNoun(routeConfirmKeyword, terminalNodeRouteConfirm);
            routeKeyword.AddCompatibleNoun(terminalKeyword, terminalNodeRoute);
            routeInfoKeyword.AddCompatibleNoun(terminalKeyword, terminalNodeInfo);

            extendedLevel.RouteNode = terminalNodeRoute;
            extendedLevel.RouteConfirmNode = terminalNodeRouteConfirm;
            extendedLevel.InfoNode = terminalNodeInfo;
        }

        internal static void CreateTerminalDataForAllExtendedStoryLogs()
        {
            foreach (ExtendedMod extendedMod in PatchedContent.ExtendedMods)
                foreach (ExtendedStoryLog extendedStoryLog in extendedMod.ExtendedStoryLogs)
                    CreateStoryLogTerminalData(extendedStoryLog);
        }

        internal static void CreateStoryLogTerminalData(ExtendedStoryLog newStoryLog)
        {
            if (Plugin.IsSetupComplete)
            {
                Terminal.logEntryFiles.Add(newStoryLog.assignedNode);
                return;
            }
            string sanitizedName = newStoryLog.storyLogTitle.Sanitized(toLower: false).RemoveWhitespace();

            TerminalKeyword newStoryLogKeyword = CreateNewTerminalKeyword();
            newStoryLogKeyword.name = $"{sanitizedName}Keyword";
            newStoryLogKeyword.word = newStoryLog.terminalKeywordNoun.Sanitized().RemoveWhitespace();
            newStoryLogKeyword.defaultVerb = viewKeyword;
            TerminalNode newStoryLogNode = CreateNewTerminalNode();
            newStoryLogNode.name = $"LogFile{Terminal.logEntryFiles.Count + 1}";
            newStoryLogNode.displayText = newStoryLog.storyLogDescription;
            newStoryLogNode.clearPreviousText = true;
            newStoryLogNode.creatureName = newStoryLog.storyLogTitle;
            newStoryLogNode.storyLogFileID = Terminal.logEntryFiles.Count;
            newStoryLog.newStoryLogID = Terminal.logEntryFiles.Count;
            newStoryLog.assignedNode = newStoryLogNode;

            Terminal.logEntryFiles.Add(newStoryLogNode);
            viewKeyword.AddCompatibleNoun(newStoryLogKeyword, newStoryLogNode);
        }

        internal static void CreateItemTerminalData(ExtendedItem extendedItem)
        {
            string sanitizedName = extendedItem.Item.itemName.Sanitized(toLower: false).RemoveWhitespace();

            //Terminal Buy Keyword
            TerminalKeyword terminalKeyword = CreateNewTerminalKeyword();
            terminalKeyword.name = $"{sanitizedName}Keyword";
            terminalKeyword.word = sanitizedName.ToLowerInvariant();
            terminalKeyword.defaultVerb = buyKeyword;

            //Terminal Buy Keyword
            TerminalNode terminalNodeBuy;
            if (extendedItem.BuyNode != null)
                terminalNodeBuy = extendedItem.BuyNode;
            else
            {
                terminalNodeBuy = CreateNewTerminalNode();
                terminalNodeBuy.name = $"{sanitizedName}Buy";
                terminalNodeBuy.displayText = (!string.IsNullOrEmpty(extendedItem.OverrideBuyNodeDescription)) ? extendedItem.OverrideBuyNodeDescription
                    : $"You have requested to order {(!string.IsNullOrEmpty(extendedItem.PluralisedItemName) ? extendedItem.PluralisedItemName
                    : extendedItem.Item.itemName)}. Amount: [variableAmount].\n Total cost of items: [totalCost].\n\nPlease CONFIRM or DENY.\n\n";
                terminalNodeBuy.clearPreviousText = true;
                terminalNodeBuy.maxCharactersToType = 15;
                terminalNodeBuy.isConfirmationNode = true;
                terminalNodeBuy.itemCost = extendedItem.Item.creditsWorth;
                terminalNodeBuy.overrideOptions = true;
            }

            //Terminal Route Confirm Node
            TerminalNode terminalNodeBuyConfirm;
            if (extendedItem.BuyConfirmNode != null)
                terminalNodeBuyConfirm = extendedItem.BuyConfirmNode;
            else
            {
                terminalNodeBuyConfirm = CreateNewTerminalNode();
                terminalNodeBuyConfirm.name = $"{sanitizedName}BuyConfirm";
                terminalNodeBuyConfirm.displayText = (!string.IsNullOrEmpty(extendedItem.OverrideBuyConfirmNodeDescription)) ? extendedItem.OverrideBuyConfirmNodeDescription
                    : $"Ordered [variableAmount] {(!string.IsNullOrEmpty(extendedItem.PluralisedItemName) ? extendedItem.PluralisedItemName
                    : extendedItem.Item.itemName)}. Your new balance is[playerCredits]\n\nOur contractors enjoy fast, free shipping while on the job! Any purchased items will arrive hourly at your approximate location.";
                terminalNodeBuyConfirm.clearPreviousText = true;
                terminalNodeBuyConfirm.maxCharactersToType = 35;
                terminalNodeBuyConfirm.isConfirmationNode = false;
                terminalNodeBuyConfirm.playSyncedClip = 0;
            }

            //Terminal Info Node
            TerminalNode terminalNodeInfo = null;
            if (!string.IsNullOrEmpty(extendedItem.OverrideInfoNodeDescription))
            {
                if (extendedItem.BuyInfoNode != null)
                    terminalNodeInfo = extendedItem.BuyInfoNode;
                else
                {
                    terminalNodeInfo = CreateNewTerminalNode();
                    terminalNodeInfo.name = $"{sanitizedName}Info";
                    terminalNodeInfo.clearPreviousText = true;
                    terminalNodeInfo.maxCharactersToType = 25;
                    terminalNodeInfo.displayText = '\n' + extendedItem.OverrideInfoNodeDescription;
                }
            }

            terminalNodeBuy.AddCompatibleNoun(routeConfirmKeyword, terminalNodeBuyConfirm);
            terminalNodeBuy.AddCompatibleNoun(routeDenyKeyword, cancelPurchaseNode);
            buyKeyword.AddCompatibleNoun(terminalKeyword, terminalNodeBuy);
            if (terminalNodeInfo != null)
                routeInfoKeyword.AddCompatibleNoun(terminalKeyword, terminalNodeInfo);

            extendedItem.BuyNode = terminalNodeBuy;
            extendedItem.BuyConfirmNode = terminalNodeBuyConfirm;
            extendedItem.BuyInfoNode = terminalNodeInfo;
        }

        internal static void CreateEnemyTypeTerminalData(ExtendedEnemyType extendedEnemyType)
        {
            if (Plugin.IsSetupComplete)
            {
                // Load ExtendedEnemyType beastiary entry.
                Patches.Terminal.enemyFiles.Add(extendedEnemyType.EnemyInfoNode);
                return;
            }
            string sanitizedName = extendedEnemyType.EnemyDisplayName.Sanitized(toLower: false).RemoveWhitespace();

            TerminalKeyword newEnemyInfoKeyword = CreateNewTerminalKeyword();
            newEnemyInfoKeyword.name = $"{sanitizedName}BestiaryKeyword";
            newEnemyInfoKeyword.word = sanitizedName.ToLowerInvariant();
            newEnemyInfoKeyword.defaultVerb = routeInfoKeyword;

            TerminalNode newEnemyInfoNode = CreateNewTerminalNode();
            newEnemyInfoNode.name = $"{sanitizedName}BestiaryNode";
            newEnemyInfoNode.displayText = extendedEnemyType.InfoNodeDescription;
            newEnemyInfoNode.creatureFileID = extendedEnemyType.EnemyID;
            newEnemyInfoNode.creatureName = extendedEnemyType.EnemyDisplayName;
            newEnemyInfoNode.playSyncedClip = 2;

            if (extendedEnemyType.InfoNodeVideoClip != null)
            {
                newEnemyInfoNode.displayVideo = extendedEnemyType.InfoNodeVideoClip;
                newEnemyInfoNode.loadImageSlowly = true;
            }

            extendedEnemyType.EnemyInfoNode = newEnemyInfoNode;

            Patches.Terminal.enemyFiles.Add(newEnemyInfoNode);
            routeInfoKeyword.AddCompatibleNoun(newEnemyInfoKeyword, newEnemyInfoNode);
        }

        internal static void CreateBuyableVehicleTerminalData(ExtendedBuyableVehicle extendedBuyableVehicle)
        {
            string sanitizedName = extendedBuyableVehicle.BuyableVehicle.vehicleDisplayName.Sanitized(toLower: false).RemoveWhitespace();

            TerminalKeyword newVehicleTerminalKeyword = CreateNewTerminalKeyword();
            newVehicleTerminalKeyword.name = $"{sanitizedName}Keyword";
            newVehicleTerminalKeyword.word = extendedBuyableVehicle.TerminalKeywordName.ToLowerInvariant();
            newVehicleTerminalKeyword.defaultVerb = buyKeyword;

            TerminalNode newVehicleBuyNode = CreateNewTerminalNode();
            newVehicleBuyNode.name = $"{sanitizedName}Buy";
            newVehicleBuyNode.itemCost = extendedBuyableVehicle.BuyableVehicle.creditsWorth;
            newVehicleBuyNode.buyVehicleIndex = extendedBuyableVehicle.VehicleID;
            newVehicleBuyNode.isConfirmationNode = true;
            newVehicleBuyNode.overrideOptions = true;
            newVehicleBuyNode.clearPreviousText = true;
            newVehicleBuyNode.maxCharactersToType = 15;
            newVehicleBuyNode.displayText = $"You have requested to order the {extendedBuyableVehicle.BuyableVehicle.vehicleDisplayName}.\n[warranty] Total cost of items: [totalCost].\n\nPlease CONFIRM or DENY.\n\n";

            TerminalNode newVehicleBuyConfirmNode = CreateNewTerminalNode();
            newVehicleBuyConfirmNode.name = $"{sanitizedName}BuyConfirm";
            newVehicleBuyConfirmNode.itemCost = extendedBuyableVehicle.BuyableVehicle.creditsWorth;
            newVehicleBuyConfirmNode.buyVehicleIndex = extendedBuyableVehicle.VehicleID;
            newVehicleBuyConfirmNode.clearPreviousText = true;
            newVehicleBuyConfirmNode.maxCharactersToType = 35;
            newVehicleBuyConfirmNode.playSyncedClip = 0;
            newVehicleBuyConfirmNode.displayText = $"Ordered the {extendedBuyableVehicle.BuyableVehicle.vehicleDisplayName}. Your new balance is [playerCredits].\n\nWe are so confident in the quality of this product, it comes with a life-time warranty! "
                + $"If your {extendedBuyableVehicle.BuyableVehicle.vehicleDisplayName} is lost or destroyed, you can get one free replacement. Items cannot be purchased while the vehicle is en route." + "\n\n";

            TerminalNode newVehicleInfoNode = CreateNewTerminalNode();
            newVehicleInfoNode.name = $"{sanitizedName}Info";

            extendedBuyableVehicle.VehicleBuyNode = newVehicleBuyNode;
            extendedBuyableVehicle.VehicleBuyConfirmNode = newVehicleBuyConfirmNode;
            extendedBuyableVehicle.VehicleInfoNode = newVehicleInfoNode;

            newVehicleBuyNode.AddCompatibleNoun(routeConfirmKeyword, newVehicleBuyConfirmNode);
            newVehicleBuyNode.AddCompatibleNoun(routeDenyKeyword, cancelPurchaseNode);

            buyKeyword.AddCompatibleNoun(newVehicleTerminalKeyword, newVehicleBuyNode);
        }

        internal static void CreateUnlockableItemTerminalData(ExtendedUnlockableItem extendedUnlockableItem)
        {
            string sanitizedName = extendedUnlockableItem.UnlockableItem.unlockableName.Sanitized(toLower: false).RemoveWhitespace();

            //Terminal Buy Keyword
            TerminalKeyword terminalKeyword = CreateNewTerminalKeyword();
            terminalKeyword.name = $"{sanitizedName}Keyword";
            terminalKeyword.word = sanitizedName.ToLowerInvariant();
            terminalKeyword.defaultVerb = buyKeyword;

            //Terminal Buy Keyword
            TerminalNode terminalNodeBuy;
            if (extendedUnlockableItem.BuyNode != null)
                terminalNodeBuy = extendedUnlockableItem.BuyNode;
            else
            {
                terminalNodeBuy = CreateNewTerminalNode();
                terminalNodeBuy.name = $"{sanitizedName}Buy";
                terminalNodeBuy.itemCost = extendedUnlockableItem.ItemCost;
                terminalNodeBuy.isConfirmationNode = false;
                terminalNodeBuy.overrideOptions = true;
                terminalNodeBuy.clearPreviousText = true;
                terminalNodeBuy.maxCharactersToType = 15;
                terminalNodeBuy.creatureName = extendedUnlockableItem.UnlockableItem.unlockableName;
                terminalNodeBuy.displayText = (!string.IsNullOrEmpty(extendedUnlockableItem.OverrideBuyNodeDescription)) ? extendedUnlockableItem.OverrideBuyNodeDescription
                    : $"You have requested to order the {terminalNodeBuy.creatureName}.\n Total cost of item: [totalCost].\n\nPlease CONFIRM or DENY.\n\n";
            }
            terminalNodeBuy.shipUnlockableID = extendedUnlockableItem.UnlockableItemID;

            //Terminal Buy Confirm Node
            TerminalNode terminalNodeBuyConfirm;
            if (extendedUnlockableItem.BuyConfirmNode != null)
                terminalNodeBuyConfirm = extendedUnlockableItem.BuyConfirmNode;
            else
            {
                terminalNodeBuyConfirm = CreateNewTerminalNode();
                terminalNodeBuyConfirm.name = $"{sanitizedName}BuyConfirm";
                terminalNodeBuyConfirm.itemCost = extendedUnlockableItem.ItemCost;
                terminalNodeBuyConfirm.isConfirmationNode = false;
                terminalNodeBuyConfirm.clearPreviousText = true;
                terminalNodeBuyConfirm.buyUnlockable = true;
                terminalNodeBuyConfirm.maxCharactersToType = 35;
                terminalNodeBuyConfirm.playSyncedClip = 0;
                terminalNodeBuyConfirm.creatureName = extendedUnlockableItem.UnlockableItem.unlockableName;
                terminalNodeBuyConfirm.displayText = (!string.IsNullOrEmpty(extendedUnlockableItem.OverrideBuyConfirmNodeDescription)) ? extendedUnlockableItem.OverrideBuyConfirmNodeDescription
                    : $"Ordered the {terminalNodeBuyConfirm.creatureName}! Your new balance is [playerCredits]";
            }
            terminalNodeBuyConfirm.shipUnlockableID = extendedUnlockableItem.UnlockableItemID;

            //Terminal Info Node
            TerminalNode terminalNodeInfo = null;
            if (!string.IsNullOrEmpty(extendedUnlockableItem.OverrideInfoNodeDescription))
            {
                if (extendedUnlockableItem.BuyInfoNode != null)
                    terminalNodeInfo = extendedUnlockableItem.BuyInfoNode;
                else
                {
                    terminalNodeInfo = CreateNewTerminalNode();
                    terminalNodeInfo.name = $"{sanitizedName}Info";
                    terminalNodeInfo.clearPreviousText = true;
                    terminalNodeInfo.maxCharactersToType = 25;
                    terminalNodeInfo.displayText = '\n' + extendedUnlockableItem.OverrideInfoNodeDescription;
                    terminalNodeInfo.creatureName = extendedUnlockableItem.UnlockableItem.unlockableName;
                }
            }

            //Population Into Base game

            terminalNodeBuy.AddCompatibleNoun(routeConfirmKeyword, terminalNodeBuyConfirm);
            terminalNodeBuy.AddCompatibleNoun(routeDenyKeyword, cancelPurchaseNode);

            buyKeyword.AddCompatibleNoun(terminalKeyword, terminalNodeBuy);

            if (terminalNodeInfo != null)
                routeInfoKeyword.AddCompatibleNoun(terminalKeyword, terminalNodeInfo);

            extendedUnlockableItem.BuyNode = terminalNodeBuy;
            extendedUnlockableItem.BuyConfirmNode = terminalNodeBuyConfirm;
            extendedUnlockableItem.BuyInfoNode = terminalNodeInfo;

            extendedUnlockableItem.UnlockableItem.shopSelectionNode = extendedUnlockableItem.BuyNode;
        }

        internal static void CreateMoonsFilterTerminalAssets()
        {
            //Preview & Sort Keywords
            int previewIndex = Terminal.terminalNodes.allKeywords.Length;
            foreach (TerminalNode previewNode in CreateTerminalEventNodes("Preview", [PreviewInfoType.Price, PreviewInfoType.Difficulty, PreviewInfoType.Weather, PreviewInfoType.History, PreviewInfoType.All, PreviewInfoType.None]))
                AddTerminalNodeEventListener(previewNode, TryRefreshMoonsCataloguePage, LoadNodeActionType.Before);
            previewKeyword = Terminal.terminalNodes.allKeywords[previewIndex];

            int sortIndex = Terminal.terminalNodes.allKeywords.Length;
            foreach (TerminalNode sortNode in CreateTerminalEventNodes("Sort", [SortInfoType.Price, SortInfoType.Difficulty, SortInfoType.None]))
                AddTerminalNodeEventListener(sortNode, TryRefreshMoonsCataloguePage, LoadNodeActionType.Before);
            sortKeyword = Terminal.terminalNodes.allKeywords[sortIndex];

            int filterIndex = Terminal.terminalNodes.allKeywords.Length;
            foreach (TerminalNode filterNode in CreateTerminalEventNodes("Filter", [FilterInfoType.Price, FilterInfoType.Weather, FilterInfoType.None]))
                AddTerminalNodeEventListener(filterNode, TryRefreshMoonsCataloguePage, LoadNodeActionType.Before);
            filterKeyword = Terminal.terminalNodes.allKeywords[filterIndex];

            //Tag Keywords
            List<string> tagMoonWordsList = new List<string>();
            List<string> tagMoonTerminalEventsList = new List<string>();

            HashSet<ContentTag> allLevelTags = new HashSet<ContentTag>();
            foreach (ExtendedLevel extendedLevel in PatchedContent.ExtendedLevels)
                allLevelTags.UnionWith(extendedLevel.ContentTags);

            foreach (ContentTag levelTag in allLevelTags)
            {
                tagMoonWordsList.Add($"{levelTag}");
                tagMoonTerminalEventsList.Add($"Tag;{levelTag}");
            }

            foreach (TerminalNode filterNode in CreateTerminalEventNodes("Filter", tagMoonWordsList, tagMoonTerminalEventsList, createNewVerbKeyword: false))
                AddTerminalNodeEventListener(filterNode, TryRefreshMoonsCataloguePage, LoadNodeActionType.Before);

            //Simulate Keywords
            List<string> simulateMoonsKeywords = new List<string>();
            foreach (ExtendedLevel extendedLevel in PatchedContent.ExtendedLevels)
                simulateMoonsKeywords.Add(extendedLevel.NumberlessPlanetName.Sanitized(toLower: false).RemoveWhitespace());

            int counter = 0;
            foreach (TerminalNode simulateNode in CreateTerminalEventNodes("Simulate", simulateMoonsKeywords))
            {
                AddTerminalNodeEventListener(simulateNode, SetSimulationResultsText, LoadNodeActionType.Before);
                PatchedContent.ExtendedLevels[counter].SimulateNode = simulateNode;
                counter++;
            }
            simulateKeyword = Terminal.terminalNodes.allKeywords[^++counter];
        }

        internal static List<TerminalNode> CreateTerminalEventNodes(string newVerbKeywordWord, List<Enum> terminalEventEnumStrings)
        {
            List<string> convertedList = new List<string>();
            foreach (Enum enumValue in terminalEventEnumStrings)
                convertedList.Add(enumValue.ToString());

            return (CreateTerminalEventNodes(newVerbKeywordWord, convertedList));
        }

        internal static List<TerminalNode> CreateTerminalEventNodes(string newVerbKeywordWord, List<string> nounWords, List<string> terminalEventStrings = null, bool createNewVerbKeyword = true)
        {
            string sanitizedName = newVerbKeywordWord.Sanitized(toLower: false).RemoveWhitespace();
            List<TerminalNode> newTerminalNodes = new List<TerminalNode>();
            TerminalKeyword verbKeyword = createNewVerbKeyword ? CreateNewTerminalKeyword()
                : Array.Find(Terminal.terminalNodes.allKeywords, keyword => string.Equals(keyword.word.Sanitized(toLower: false).RemoveWhitespace(), sanitizedName, StringComparison.OrdinalIgnoreCase));
            if (verbKeyword == null)
                return (newTerminalNodes);
            verbKeyword.word = sanitizedName.ToLowerInvariant();
            verbKeyword.name = $"{sanitizedName}Keyword";
            verbKeyword.isVerb = true;

            terminalEventStrings ??= nounWords;
            if (nounWords.Count != terminalEventStrings.Count)
                DebugHelper.LogError($"Number of event strings does not match number of noun words for TerminalKeyword {newVerbKeywordWord}! Some events may not be registered...", DebugType.Developer);
            for (int i = 0; i < nounWords.Count; i++)
            {
                if (i > terminalEventStrings.Count) break;
                newTerminalNodes.Add(CreateTerminalEventNode(verbKeyword, nounWords[i], terminalEventStrings[i]));
            }

            return (newTerminalNodes);
        }

        internal static TerminalNode CreateTerminalEventNode(TerminalKeyword verbKeyword, string nounWord, string terminalEventString)
        {
            //DebugHelper.Log("Creating New TerminalEvent Node! VerbKeyword Word Is: " + verbKeyword.word + " | nounWord Is: " + GetTerminalEventEnum(nounWord).ToLower() + " | TerminalEvent Text Is: " + terminalEventString);
            TerminalKeyword newKeyword = CreateNewTerminalKeyword();
            TerminalNode newNode = CreateNewTerminalNode();

            newKeyword.name = $"{verbKeyword.word}{GetTerminalEventEnum(nounWord)}Keyword";
            newKeyword.word = GetTerminalEventEnum(nounWord).ToLowerInvariant();
            newKeyword.defaultVerb = verbKeyword;
            newNode.terminalEvent = terminalEventString;
            newNode.name = $"{verbKeyword.word}{GetTerminalEventEnum(nounWord)}Node";

            verbKeyword.AddCompatibleNoun(newKeyword, newNode);

            return (newNode);
        }

        internal static TerminalKeyword CreateNewTerminalKeyword()
        {
            TerminalKeyword newTerminalKeyword = ScriptableObject.CreateInstance<TerminalKeyword>();
            newTerminalKeyword.name = "NewLethalLevelLoaderTerminalKeyword";

            newTerminalKeyword.compatibleNouns = [];
            newTerminalKeyword.defaultVerb = null;
            Terminal.terminalNodes.allKeywords = [.. Terminal.terminalNodes.allKeywords, newTerminalKeyword];

            return (newTerminalKeyword);
        }

        internal static TerminalNode CreateNewTerminalNode()
        {
            TerminalNode newTerminalNode = ScriptableObject.CreateInstance<TerminalNode>();
            newTerminalNode.name = "NewLethalLevelLoaderTerminalNode";

            newTerminalNode.displayText = string.Empty;
            newTerminalNode.terminalEvent = string.Empty;
            newTerminalNode.maxCharactersToType = 25;
            newTerminalNode.buyItemIndex = -1;
            newTerminalNode.buyRerouteToMoon = -1;
            newTerminalNode.displayPlanetInfo = -1;
            newTerminalNode.shipUnlockableID = -1;
            newTerminalNode.creatureFileID = -1;
            newTerminalNode.storyLogFileID = -1;
            newTerminalNode.playSyncedClip = -1;
            newTerminalNode.terminalOptions = [];

            return (newTerminalNode);
        }
    }
}
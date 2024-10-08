﻿using ADepIn;
using BepInEx.Configuration;
using Deli;
using Deli.Immediate;
using Deli.Setup;
using Deli.VFS;
using Deli.Runtime;
using FistVR;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TNHFramework.ObjectTemplates;
using TNHFramework.Utilities;
using UnityEngine;
using UnityEngine.UI;
using Deli.Runtime.Yielding;
using Anvil;
using TNHFramework.Patches;
using Stratum;
using BepInEx.Bootstrap;
using BepInEx;
using Stratum.Extensions;

namespace TNHFramework
{
    [BepInPlugin("h3vr.tnhframework", "TNH Framework", "0.0.1")]
    [BepInDependency(StratumRoot.GUID, StratumRoot.Version)]
    public class TNHFramework : StratumPlugin
    {
        private static ConfigEntry<bool> printCharacters;
        private static ConfigEntry<bool> logTNH;
        private static ConfigEntry<bool> logFileReads;
        private static ConfigEntry<bool> allowLog;
        public static ConfigEntry<bool> BuildCharacterFiles;
        public static ConfigEntry<bool> ConvertFilesToYAML;
        public static ConfigEntry<bool> UnlimitedTokens;
        public static ConfigEntry<bool> EnableDebugText;
        public static ConfigEntry<bool> EnableScoring;

        public static string InfoPath;
        public static string OutputFilePath;

        public static Dictionary<string, Type> Serializables = [];

        // Bodged Magazine Patcher replacement stuff.
        public static Dictionary<FireArmRoundType, List<FVRObject>> CartridgeDictionary = [];
        public static Dictionary<FireArmMagazineType, List<FVRObject>> MagazineDictionary = [];
        public static Dictionary<FireArmClipType, List<FVRObject>> StripperDictionary = [];
        public static Dictionary<FireArmRoundType, List<FVRObject>> SpeedloaderDictionary = [];

        //Variables used by various patches
        public static bool PreventOutfitFunctionality = false;
        public static List<int> SpawnedBossIndexes = [];
        public static List<int> SupplyPointIFFList = [];

        public static List<GameObject> SpawnedConstructors = [];
        public static List<GameObject> SpawnedPanels = [];
        public static List<EquipmentPoolDef.PoolEntry> SpawnedPools = [];

        public static List<List<string>> HoldActions = [];
        public static List<HoldStats> HoldStats = [];

        public static int GunsRecycled;
        public static int ShotsFired;

        /// <summary>
        /// First method that gets called
        /// </summary>
        private void Awake()
        {
            InfoPath = Path.GetDirectoryName(Info.Location);

            if (TNHFrameworkLogger.BepLog == null)
            {
                TNHFrameworkLogger.Init();
            }

            TNHFrameworkLogger.Log("Hello World (from TNH Tweaker)", TNHFrameworkLogger.LogType.General);

            SetupOutputDirectory();

            LoadConfigFile();
            LoadPanelSprites();

            Harmony.CreateAndPatchAll(typeof(TNHFramework));
            Harmony.CreateAndPatchAll(typeof(TNHPatches));
            Harmony.CreateAndPatchAll(typeof(PatrolPatches));
            Harmony.CreateAndPatchAll(typeof(HoldPatches));

            if (EnableScoring.Value) Harmony.CreateAndPatchAll(typeof(HighScorePatches));

            if (EnableDebugText.Value) Harmony.CreateAndPatchAll(typeof(DebugPatches));

            /*
            if (Chainloader.PluginInfos.ContainsKey("Deli"))
            {
                DeliAwake();
            }
            else
            {
                foreach (KeyValuePair<string, BepInEx.PluginInfo> item in Chainloader.PluginInfos)
                {
                    TNHFrameworkLogger.Log($"Plugin loaded: {item.Key}", TNHFrameworkLogger.LogType.General);
                }
            }
            */
        }

        public override void OnSetup(IStageContext<Empty> ctx)
        {
            TNHLoaders TNHLoader = new();

            ctx.Loaders.Add("tnhchar", TNHLoader.LoadChar);
            ctx.Loaders.Add("tnhsosig", TNHLoader.LoadSosig);
            ctx.Loaders.Add("tnhvaultgun", TNHLoader.LoadVaultFile);
        }

        public override IEnumerator OnRuntime(IStageContext<IEnumerator> ctx)
        {
            // Do we... Need anything here?
            yield break;
        }



        /// <summary>
        /// Loads the sprites used in secondary panels in TNH
        /// </summary>
        private void LoadPanelSprites()
        {
            DirectoryInfo pluginDirectory = new(Path.GetDirectoryName(Info.Location));

            FileInfo file = ExtDirectoryInfo.GetFile(pluginDirectory, "mag_dupe_background.png");
            Sprite result = TNHFrameworkUtils.LoadSprite(file);
            MagazinePanel.background = result;

            file = ExtDirectoryInfo.GetFile(pluginDirectory, "ammo_purchase_background.png");
            result = TNHFrameworkUtils.LoadSprite(file);
            AmmoPurchasePanel.background = result;

            file = ExtDirectoryInfo.GetFile(pluginDirectory, "full_auto_background.png");
            result = TNHFrameworkUtils.LoadSprite(file);
            FullAutoPanel.background = result;

            file = ExtDirectoryInfo.GetFile(pluginDirectory, "fire_rate_background.png");
            result = TNHFrameworkUtils.LoadSprite(file);
            FireRatePanel.background = result;

            file = ExtDirectoryInfo.GetFile(pluginDirectory, "minus_icon.png");
            result = TNHFrameworkUtils.LoadSprite(file);
            FireRatePanel.minusSprite = result;

            file = ExtDirectoryInfo.GetFile(pluginDirectory, "plus_icon.png");
            result = TNHFrameworkUtils.LoadSprite(file);
            FireRatePanel.plusSprite = result;
        }




        /// <summary>
        /// Loads the bepinex config file, and applys those settings
        /// </summary>
        private void LoadConfigFile()
        {
            TNHFrameworkLogger.Log("Getting config file", TNHFrameworkLogger.LogType.File);

            BuildCharacterFiles = Config.Bind("General",
                                    "BuildCharacterFiles",
                                    false,
                                    "If true, files useful for character creation will be generated in TNHTweaker folder");

            ConvertFilesToYAML = Config.Bind("General",
                                    "ConvertFilesToYAML",
                                    false,
                                    "If true, any Stratum-based custom characters will have their JSON files converted to YAML");

            EnableScoring = Config.Bind("General",
                                    "EnableScoring",
                                    true,
                                    "If true, TNH scores will be uploaded to the TNH Dashboard (https://devyndamonster.github.io/TNHDashboard/index.html)");

            allowLog = Config.Bind("Debug",
                                    "EnableLogging",
                                    true,
                                    "Set to true to enable logging");

            printCharacters = Config.Bind("Debug",
                                         "LogCharacterInfo",
                                         false,
                                         "Decide if should print all character info");

            logTNH = Config.Bind("Debug",
                                    "LogTNH",
                                    false,
                                    "If true, general TNH information will be logged");

            logFileReads = Config.Bind("Debug",
                                    "LogFileReads",
                                    false,
                                    "If true, reading from a file will log the reading process");

            UnlimitedTokens = Config.Bind("Debug",
                                    "EnableUnlimitedTokens",
                                    false,
                                    "If true, you will spawn with 999999 tokens for any character in TNH (useful for testing loot pools)");

            EnableDebugText = Config.Bind("Debug",
                                    "EnableDebugText",
                                    false,
                                    "If true, some text will appear in TNH maps showing additional info");

            

            TNHFrameworkLogger.AllowLogging = allowLog.Value;
            TNHFrameworkLogger.LogCharacter = printCharacters.Value;
            TNHFrameworkLogger.LogTNH = logTNH.Value;
            TNHFrameworkLogger.LogFile = logFileReads.Value;
        }


        /// <summary>
        /// Creates the main TNH Tweaker file folder
        /// </summary>
        private void SetupOutputDirectory()
        {
            OutputFilePath = Path.Combine(Path.GetDirectoryName(Info.Location), "CharFiles");

            if (!Directory.Exists(OutputFilePath))
            {
                Directory.CreateDirectory(OutputFilePath);
            }
        }



        [HarmonyPatch(typeof(TNH_ScoreDisplay), "SubmitScoreAndGoToBoard")] // Specify target method with HarmonyPatch attribute
        [HarmonyPrefix]
        public static bool PreventScoring(TNH_ScoreDisplay __instance, int score)
        {
            TNHFrameworkLogger.Log("Preventing vanilla score submition", TNHFrameworkLogger.LogType.TNH);

            GM.Omni.OmniFlags.AddScore(__instance.m_curSequenceID, score);

            __instance.m_hasCurrentScore = true;
            __instance.m_currentScore = score;

            if (EnableScoring.Value)
            {
                AnvilManager.Instance.StartCoroutine(HighScorePatches.SendScore(score));
            }

            //Draw local scores
            __instance.RedrawHighScoreDisplay(__instance.m_curSequenceID);

            GM.Omni.SaveToFile();

            return false;
        }
    }

    public class TNHTweakerDeli : DeliBehaviour
    {
        public void Awake()
        {
            if (TNHFrameworkLogger.BepLog == null)
            {
                TNHFrameworkLogger.Init();
            }

            Stages.Setup += DeliOnSetup;
        }

        /// <summary>
        /// Performs initial setup for TNH Tweaker
        /// </summary>
        /// <param name="stage"></param>
        private void DeliOnSetup(SetupStage stage)
        {
            stage.SetupAssetLoaders[Source, "sosig"] = new SosigLoaderDeli().LoadAsset;
            stage.SetupAssetLoaders[Source, "vault_file"] = new VaultFileLoaderDeli().LoadAsset;
            stage.SetupAssetLoaders[Source, "character"] = new CharacterLoaderDeli().LoadAsset;
        }
    }
}

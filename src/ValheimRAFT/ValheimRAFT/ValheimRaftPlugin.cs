﻿using BepInEx;
using BepInEx.Configuration;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Bootstrap;
using Components;
using Jotunn;
using Properties;
using UnityEngine;
using ValheimRAFT.Patches;
using ValheimRAFT.Util;
using ValheimVehicles.ConsoleCommands;
using ValheimVehicles.Prefabs;
using ValheimVehicles.Vehicles;
using Logger = Jotunn.Logger;

namespace ValheimRAFT;

// [SentryDSN()]
[BepInPlugin(BepInGuid, ModName, Version)]
[BepInDependency(Main.ModGuid)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
public class ValheimRaftPlugin : BaseUnityPlugin
{
  // ReSharper disable MemberCanBePrivate.Global
  public const string Author = "zolantris";
  public const string Version = "2.0.4";
  public const string ModName = "ValheimRAFT";
  public const string BepInGuid = $"{Author}.{ModName}";
  public const string HarmonyGuid = $"{Author}.{ModName}";
  public const string ModDescription = "Valheim Mod for building on the sea";

  public const string CopyRight = "Copyright © 2023-2024, GNU-v3 licensed";
  // ReSharper restore MemberCanBePrivate.Global

  public static readonly int CustomRaftLayer = 29;
  private bool m_customItemsAdded;
  public PrefabRegistryController prefabController;

  public static VehicleDebugGui _debugGui;

  public static ValheimRaftPlugin Instance { get; private set; }

  public ConfigEntry<bool> MakeAllPiecesWaterProof { get; set; }

  public ConfigEntry<bool> AllowFlight { get; set; }
  public ConfigEntry<bool> Graphics_AllowSailsFadeInFog { get; set; }
  public ConfigEntry<bool> AllowCustomRudderSpeeds { get; set; }

  public ConfigEntry<string> PluginFolderName { get; set; }
  public ConfigEntry<float> InitialRaftFloorHeight { get; set; }
  public ConfigEntry<bool> PlanBuildPatches { get; set; }
  public ConfigEntry<float> RaftHealth { get; set; }
  public ConfigEntry<float> ServerRaftUpdateZoneInterval { get; set; }
  public ConfigEntry<float> RaftSailForceMultiplier { get; set; }
  public ConfigEntry<bool> AdminsCanOnlyBuildRaft { get; set; }
  public ConfigEntry<bool> AllowOldV1RaftRecipe { get; set; }
  public ConfigEntry<bool> AllowExperimentalPrefabs { get; set; }


  // Propulsion Configs
  public ConfigEntry<bool> EnableCustomPropulsionConfig { get; set; }

  public ConfigEntry<float> MaxPropulsionSpeed { get; set; }
  public ConfigEntry<float> MaxSailSpeed { get; set; }
  public ConfigEntry<float> SpeedCapMultiplier { get; set; }
  public ConfigEntry<float> VehicleRudderSpeedBack { get; set; }
  public ConfigEntry<float> VehicleRudderSpeedSlow { get; set; }
  public ConfigEntry<float> VehicleRudderSpeedHalf { get; set; }

  public ConfigEntry<float> VehicleRudderSpeedFull { get; set; }

  public ConfigEntry<bool> FlightVerticalToggle { get; set; }
  public ConfigEntry<bool> FlightHasRudderOnly { get; set; }


  public ConfigEntry<float> SailTier1Area { get; set; }
  public ConfigEntry<float> SailTier2Area { get; set; }
  public ConfigEntry<float> SailTier3Area { get; set; }
  public ConfigEntry<float> SailCustomAreaTier1Multiplier { get; set; }
  public ConfigEntry<float> BoatDragCoefficient { get; set; }
  public ConfigEntry<float> MastShearForceThreshold { get; set; }
  public ConfigEntry<bool> HasDebugSails { get; set; }
  public ConfigEntry<bool> HasDebugBase { get; set; }

  public ConfigEntry<bool> HasShipWeightCalculations { get; set; }
  public ConfigEntry<float> MassPercentageFactor { get; set; }
  public ConfigEntry<bool> ShowShipStats { get; set; }
  public ConfigEntry<bool> HasShipContainerWeightCalculations { get; set; }
  public ConfigEntry<float> RaftCreativeHeight { get; set; }
  public ConfigEntry<KeyboardShortcut> AnchorKeyboardShortcut { get; set; }
  public ConfigEntry<bool> EnableMetrics { get; set; }
  public ConfigEntry<bool> EnableExactVehicleBounds { get; set; }
  public ConfigEntry<bool> ProtectVehiclePiecesOnErrorFromWearNTearDamage { get; set; }
  public ConfigEntry<bool> DebugRemoveStartMenuBackground { get; set; }
  public ConfigEntry<bool> HullCollisionOnly { get; set; }

  // sounds for VehicleShip Effects
  public ConfigEntry<bool> EnableShipWakeSounds { get; set; }
  public ConfigEntry<bool> EnableShipInWaterSounds { get; set; }
  public ConfigEntry<bool> EnableShipSailSounds { get; set; }

  public enum HullFloatation
  {
    Average,
    Center,
    Bottom,
    Top,
  }

  public ConfigEntry<HullFloatation> HullFloatationColliderLocation { get; set; }
  public ConfigEntry<bool> HasVehicleDebug { get; set; }


  /**
   * These folder names are matched for the CustomTexturesGroup
   */
  public string[] possibleModFolderNames =
  [
    $"{Author}-{ModName}", $"zolantris-{ModName}", $"Zolantris-{ModName}", ModName
  ];

  private ConfigDescription CreateConfigDescription(string description, bool isAdmin = false)
  {
    return new ConfigDescription(
      description,
      null,
      new ConfigurationManagerAttributes()
      {
        IsAdminOnly = true
      }
    );
  }

  /**
   * @todo will port to valheim vehicles plugin soon.
   */
  private void CreateVehicleConfig()
  {
    HullFloatationColliderLocation = Config.Bind("Vehicles", "HullFloatationColliderLocation",
      HullFloatation.Average,
      CreateConfigDescription(
        "Hull Floatation Collider will determine the location the ship floats and hovers above the sea. Average is the average height of all Vehicle Hull Pieces attached to the vehicle. The point calculate is the center of the prefab. Center is the center point of all the float boats. This center point is determined by the max and min height points included for ship hulls. Lowest is the lowest most hull piece will determine the float height, allowing users to easily raise the ship if needed by adding a piece at the lowest point of the ship."));
    HasVehicleDebug = Config.Bind("Vehicles", "HasVehicleDebug", false,
      CreateConfigDescription(
        "Enables the debug menu, for showing colliders or rotating the ship"));
    EnableExactVehicleBounds = Config.Bind("Vehicles", "EnableExactVehicleBounds", false,
      CreateConfigDescription(
        "Ensures that a piece placed within the raft is included in the float collider correctly. May not be accurate if the parent GameObjects are changing their scales above or below 1,1,1. Mods like Gizmo could be incompatible"));
  }

  private void CreateColliderConfig()
  {
    HullCollisionOnly = Config.Bind("Floatation", "Only Use Hulls For Floatation Collisions", true,
      CreateConfigDescription(
        "Makes the Ship Hull prefabs be the sole source of collisions, meaning ships with wider tops will not collide at bottom terrain due to their width above water. Requires a Hull, without a hull it will previous box around all items in ship",
        false));
  }

  private void CreateCommandConfig()
  {
    RaftCreativeHeight = Config.Bind("Config", "RaftCreativeHeight",
      5f,
      CreateConfigDescription(
        "Sets the raftcreative command height, raftcreative is relative to the current height of the ship, negative numbers will sink your ship temporarily",
        false));
  }

  private void CreatePropulsionConfig()
  {
    ShowShipStats = Config.Bind("Debug", "ShowShipState", true);
    MaxPropulsionSpeed = Config.Bind("Propulsion", "MaxPropulsionSpeed", 30f,
      CreateConfigDescription(
        "Sets the absolute max speed a ship can ever hit. This is capped on the vehicle, so no forces applied will be able to exceed this value. 20-30f is safe, higher numbers could let the ship fail off the map",
        true));
    MaxSailSpeed = Config.Bind("Propulsion", "MaxSailSpeed", 10f,
      CreateConfigDescription(
        "Sets the absolute max speed a ship can ever hit with sails. Prevents or enables space launches, cannot exceed MaxPropulsionSpeed.",
        true));
    MassPercentageFactor = Config.Bind("Propulsion", "MassPercentage", 55f, CreateConfigDescription(
      "Sets the mass percentage of the ship that will slow down the sails",
      true));
    SpeedCapMultiplier = Config.Bind("Propulsion", "SpeedCapMultiplier", 1f,
      CreateConfigDescription(
        "Sets the speed at which it becomes significantly harder to gain speed per sail area",
        true));

    // rudder
    VehicleRudderSpeedBack = Config.Bind("Propulsion", "Rudder Back Speed", 1f,
      new ConfigDescription("Set the Back speed of rudder, this will apply with sails"));
    VehicleRudderSpeedSlow = Config.Bind("Propulsion", "Rudder Slow Speed", 1f,
      new ConfigDescription("Set the Half speed of rudder, this will apply with sails"));
    VehicleRudderSpeedHalf = Config.Bind("Propulsion", "Rudder Half Speed", 0f,
      new ConfigDescription("Set the Slow speed of rudder, this will apply with sails"));
    VehicleRudderSpeedFull = Config.Bind("Propulsion", "Rudder Full Speed", 0f,
      new ConfigDescription("Set the Full speed of rudder, this will apply with sails"));

    // ship weight
    HasShipWeightCalculations = Config.Bind("Propulsion", "HasShipWeightCalculations", true,
      CreateConfigDescription(
        "enables ship weight calculations for sail-force (sailing speed) and future propulsion, makes larger ships require more sails and smaller ships require less"));

    HasShipContainerWeightCalculations = Config.Bind("Propulsion",
      "HasShipContainerWeightCalculations",
      true,
      CreateConfigDescription(
        "enables ship weight calculations for containers which affects sail-force (sailing speed) and future propulsion calculations. Makes ships with lots of containers require more sails"));

    HasDebugSails = Config.Bind("Debug", "HasDebugSails", false,
      CreateConfigDescription(
        "Outputs all custom sail information when saving and updating ZDOs for the sails. Debug only."));

    EnableCustomPropulsionConfig = Config.Bind("Propulsion",
      "EnableCustomPropulsionConfig", SailAreaForce.HasPropulsionConfigOverride,
      CreateConfigDescription("Enables all custom propulsion values", false));

    SailCustomAreaTier1Multiplier = Config.Bind("Propulsion",
      "SailCustomAreaTier1Multiplier", SailAreaForce.CustomTier1AreaForceMultiplier,
      CreateConfigDescription(
        "Manual sets the sail wind area multiplier the custom tier1 sail. Currently there is only 1 tier",
        true)
    );

    SailTier1Area = Config.Bind("Propulsion",
      "SailTier1Area", SailAreaForce.Tier1,
      CreateConfigDescription("Manual sets the sail wind area of the tier 1 sail.", true)
    );

    SailTier2Area = Config.Bind("Propulsion",
      "SailTier2Area", SailAreaForce.Tier2,
      CreateConfigDescription("Manual sets the sail wind area of the tier 2 sail.", true));

    SailTier3Area = Config.Bind("Propulsion",
      "SailTier3Area", SailAreaForce.Tier3,
      CreateConfigDescription("Manual sets the sail wind area of the tier 3 sail.", true));
  }

  private void CreateServerConfig()
  {
    ProtectVehiclePiecesOnErrorFromWearNTearDamage = Config.Bind("Server config",
      "Protect Vehicle pieces from breaking on Error", true,
      "Protects against crashes breaking raft/vehicle initialization causing raft/vehicles to slowly break pieces attached to it. This will make pieces attached to valid raft ZDOs unbreakable from damage, but still breakable with hammer");
    AdminsCanOnlyBuildRaft = Config.Bind("Server config", "AdminsCanOnlyBuildRaft", false,
      new ConfigDescription(
        "ValheimRAFT hammer menu pieces are registered as disabled unless the user is an Admin, allowing only admins to create rafts. This will update automatically make sure to un-equip the hammer to see it apply (if your remove yourself as admin). Server / client does not need to restart",
        (AcceptableValueBase)null, new object[1]
        {
          (object)new ConfigurationManagerAttributes()
          {
            IsAdminOnly = true
          }
        }));
    AllowOldV1RaftRecipe = Config.Bind("Server config", "AllowOldV1RaftRecipe", false,
      new ConfigDescription(
        "Allows the V1 Raft to be built, this Raft is not performant, but remains in >=v2.0.0 as a Fallback in case there are problems with the new raft",
        null, [
          new ConfigurationManagerAttributes()
          {
            IsAdminOnly = true
          }
        ]));
    AllowExperimentalPrefabs = Config.Bind("Server config", "AllowOldV1RaftRecipe", false,
      new ConfigDescription(
        "Allows >=v2.0.0 experimental prefabs such as Iron variants of slabs, hulls, and ribs. They do not look great so they are disabled by default",
        null, [
          new ConfigurationManagerAttributes()
          {
            IsAdminOnly = true
          }
        ]));

    ServerRaftUpdateZoneInterval = Config.Bind("Server config",
      "ServerRaftUpdateZoneInterval",
      10f,
      new ConfigDescription(
        "Allows Server Admin control over the update tick for the RAFT location. Larger Rafts will take much longer and lag out players, but making this ticket longer will make the raft turn into a box from a long distance away.",
        (AcceptableValueBase)null, new object[1]
        {
          (object)new ConfigurationManagerAttributes()
          {
            IsAdminOnly = true
          }
        }));

    MakeAllPiecesWaterProof = Config.Bind<bool>("Server config",
      "MakeAllPiecesWaterProof", true, new ConfigDescription(
        "Makes it so all building pieces (walls, floors, etc) on the ship don't take rain damage.",
        (AcceptableValueBase)null, new object[1]
        {
          (object)new ConfigurationManagerAttributes()
          {
            IsAdminOnly = true
          }
        }));
    AllowFlight = Config.Bind<bool>("Server config", "AllowFlight", false,
      new ConfigDescription("Allow the raft to fly (jump\\crouch to go up and down)",
        (AcceptableValueBase)null, new object[1]
        {
          (object)new ConfigurationManagerAttributes()
          {
            IsAdminOnly = true
          }
        }));
    AllowCustomRudderSpeeds = Config.Bind("Server config", "AllowCustomRudderSpeeds", true,
      new ConfigDescription(
        "Allow the raft to use custom rudder speeds set by the player, these speeds are applied alongside sails at half and full speed",
        (AcceptableValueBase)null, new object[1]
        {
          (object)new ConfigurationManagerAttributes()
          {
            IsAdminOnly = true
          }
        }));
  }

  private void AutoDoc()
  {
#if DEBUG
    // Store Regex to get all characters after a [
    Regex regex = new(@"\[(.*?)\]");

    // Strip using the regex above from Config[x].Description.Description
    string Strip(string x) => regex.Match(x).Groups[1].Value;
    StringBuilder sb = new();
    var lastSection = "";
    foreach (var x in Config.Keys)
    {
      // skip first line
      if (x.Section != lastSection)
      {
        lastSection = x.Section;
        sb.Append($"{Environment.NewLine}`{x.Section}`{Environment.NewLine}");
      }

      sb.Append($"\n{x.Key} [{Strip(Config[x].Description.Description)}]" +
                $"{Environment.NewLine}   * {Config[x].Description.Description.Replace("[Synced with Server]", "").Replace("[Not Synced with Server]", "")}" +
                $"{Environment.NewLine}     * Default Value: {Config[x].GetSerializedValue()}{Environment.NewLine}");
    }

    File.WriteAllText(
      Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        $"{ModName}_AutoDoc.md"),
      sb.ToString());
#endif
  }

  private void CreateFlightPropulsionConfig()
  {
    FlightVerticalToggle = Config.Bind<bool>("Propulsion",
      "Flight Vertical Continues UntilToggled",
      true,
      "Saves the user's fingers by allowing the ship to continue to climb or descend without needing to hold the button");
    FlightHasRudderOnly = Config.Bind<bool>("Propulsion",
      "Only allow rudder speeds during flight",
      false,
      "Flight allows for different rudder speeds, only use those and ignore sails");
  }

  private void CreateDebugConfig()
  {
    DebugRemoveStartMenuBackground =
      Config.Bind("Debug", "DebugRemoveStartMenuBackground", false,
        "Removes the start scene background, only use this if you want to speedup start time");
  }

  private void CreateGraphicsConfig()
  {
    Graphics_AllowSailsFadeInFog = Config.Bind("Graphics", "Sails Fade In Fog", true,
      "Allow sails to fade in fog. Unchecking this will be slightly better FPS but less realistic. Should be fine to keep enabled");
  }

  private void CreateSoundConfig()
  {
    EnableShipSailSounds = Config.Bind("Sounds", "Ship Sailing Sounds", true,
      "Toggles the ship sail sounds.");
    EnableShipWakeSounds = Config.Bind("Sounds", "Ship Wake Sounds", true,
      "Toggles Ship Wake sounds. Can be pretty loud");
    EnableShipInWaterSounds = Config.Bind("Sounds", "Ship In-Water Sounds", true,
      "Toggles ShipInWater Sounds, the sound of the hull hitting water");
  }

  private void CreatePrefabConfig()
  {
    RaftHealth = Config.Bind<float>("Config", "raftHealth", 500f,
      "Set the raft health when used with wearNTear, lowest value is 100f");
  }

  private void CreateBaseConfig()
  {
    EnableMetrics = Config.Bind("Debug", "Enable Sentry Metrics (requires sentryUnityPlugin)", true,
      CreateConfigDescription(
        "Enable sentry debug logging. Requires sentry logging plugin installed to work. Sentry Logging plugin will make it easier to troubleshoot raft errors and detect performance bottlenecks. The bare minimum is collected, and only data related to ValheimRaft. See https://github.com/zolantris/ValheimMods/tree/main/src/ValheimRAFT#logging-metrics for more details about what is collected"));

    HasDebugBase = Config.Bind("Debug", "Debug logging for Vehicle/Raft", false,
      CreateConfigDescription(
        "Outputs more debug logs for the Vehicle components. Useful for troubleshooting errors, but will spam logs"));

    PlanBuildPatches = Config.Bind<bool>("Patches",
      "Enable PlanBuild Patches (required to be on if you installed PlanBuild)", false,
      new ConfigDescription(
        "Fixes the PlanBuild mod position problems with ValheimRaft so it uses localPosition of items based on the parent raft. This MUST be enabled to support PlanBuild but can be disabled when the mod owner adds direct support for this part of ValheimRAFT. PlanBuild mod can be found here. https://thunderstore.io/c/valheim/p/MathiasDecrock/PlanBuild/",
        (AcceptableValueBase)null, new object[1]
        {
          (object)new ConfigurationManagerAttributes()
          {
            IsAdminOnly = false
          }
        }));

    InitialRaftFloorHeight = Config.Bind<float>("Deprecated Config",
      "Initial Floor Height (V1 raft)", 0.6f, new ConfigDescription(
        "Allows users to set the raft floor spawn height. 0.45 was the original height in 1.4.9 but it looked a bit too low. Now people can customize it",
        (AcceptableValueBase)null, new object[1]
        {
          (object)new ConfigurationManagerAttributes()
          {
            IsAdminOnly = false
          }
        }));

    PluginFolderName = Config.Bind<string>("Config",
      "pluginFolderName", "", new ConfigDescription(
        "Users can leave this empty. If they do not, the mod will attempt to match the folder string. Allows users to set the folder search name if their" +
        $" manager renames the folder, r2modman has a fallback case added to search for {Author}-{ModName}" +
        "Default search values are an ordered list first one is always matching non-empty strings from this pluginFolderName." +
        $"Folder Matches are:  {Author}-{ModName}, zolantris-{ModName} Zolantris-{ModName}, and {ModName}",
        (AcceptableValueBase)null, new object[1]
        {
          (object)new ConfigurationManagerAttributes()
          {
            IsAdminOnly = false
          }
        }));
    PluginFolderName = Config.Bind<string>("Config",
      "pluginFolderName", "", new ConfigDescription(
        "Users can leave this empty. If they do not, the mod will attempt to match the folder string. Allows users to set the folder search name if their" +
        $" manager renames the folder, r2modman has a fallback case added to search for {Author}-{ModName}" +
        "Default search values are an ordered list first one is always matching non-empty strings from this pluginFolderName." +
        $"Folder Matches are:  {Author}-{ModName}, zolantris-{ModName} Zolantris-{ModName}, and {ModName}",
        (AcceptableValueBase)null, new object[1]
        {
          (object)new ConfigurationManagerAttributes()
          {
            IsAdminOnly = false
          }
        }));
  }

  private void CreateKeyboardSetup()
  {
    AnchorKeyboardShortcut =
      Config.Bind("Config", "AnchorKeyboardShortcut", new KeyboardShortcut(KeyCode.LeftShift),
        new ConfigDescription("Anchor keyboard hotkey. Only applies to keyboard"));
  }

  /*
   * aggregates all config creators.
   *
   * Future plans:
   * - Abstract specific config directly into related files and call init here to set those values in the associated classes.
   * - Most likely those items will need to be "static" values.
   * - Add a watcher so those items can take the new config and process it as things update.
   */

  private void CreateConfig()
  {
    //
    // Config.SettingChanged += 
    CreateBaseConfig();
    CreatePrefabConfig();
    CreateDebugConfig();
    CreateServerConfig();
    CreateCommandConfig();
    CreateColliderConfig();
    CreateKeyboardSetup();

    // vehicles
    CreateVehicleConfig();
    CreatePropulsionConfig();
    CreateFlightPropulsionConfig();

    // for graphics QOL but maybe less FPS friendly
    CreateGraphicsConfig();
    CreateSoundConfig();
  }

  internal void ApplyMetricIfAvailable()
  {
    var @namespace = "SentryUnityWrapper";
    var @pluginClass = "SentryUnityWrapperPlugin";
    Logger.LogDebug(
      $"contains sentryunitywrapper: {Chainloader.PluginInfos.ContainsKey("zolantris.SentryUnityWrapper")}");

    Logger.LogDebug($"plugininfos {Chainloader.PluginInfos}");

    if (!EnableMetrics.Value ||
        !Chainloader.PluginInfos.ContainsKey("zolantris.SentryUnityWrapper")) return;
    Logger.LogDebug("Made it to sentry check");
    SentryMetrics.ApplyMetrics();
  }

  public void Awake()
  {
    Instance = this;


    CreateConfig();
    PatchController.Apply(HarmonyGuid);

    AddPhysicsSettings();

    RegisterConsoleCommands();
    RegisterVehicleConsoleCommands();

    HasVehicleDebug.SettingChanged += OnConfigChanged;
    EnableShipSailSounds.SettingChanged += VehicleShip.UpdateAllShipSounds;
    EnableShipWakeSounds.SettingChanged += VehicleShip.UpdateAllShipSounds;
    EnableShipInWaterSounds.SettingChanged += VehicleShip.UpdateAllShipSounds;

    /*
     * @todo add a way to skip LoadCustomTextures when on server. This check when used here crashes the Plugin.
     */
    PrefabManager.OnVanillaPrefabsAvailable += LoadCustomTextures;
    PrefabManager.OnVanillaPrefabsAvailable += AddCustomPieces;
  }

  private void OnConfigChanged(object sender, EventArgs eventArgs)
  {
    AddGuiLayerComponents();
  }

  public void RegisterConsoleCommands()
  {
    CommandManager.Instance.AddConsoleCommand(new CreativeModeConsoleCommand());
    CommandManager.Instance.AddConsoleCommand(new MoveRaftConsoleCommand());
    CommandManager.Instance.AddConsoleCommand(new HideRaftConsoleCommand());
    CommandManager.Instance.AddConsoleCommand(new RecoverRaftConsoleCommand());
  }


  // this will be removed when vehicles becomes independent of valheim raft.
  public void RegisterVehicleConsoleCommands()
  {
    CommandManager.Instance.AddConsoleCommand(new VehicleCommands());
  }

  private void Start()
  {
    // SentryLoads after
    ApplyMetricIfAvailable();
    AddGuiLayerComponents();
    AutoDoc();
  }

  /**
   * Important for raft collisions to only include water and landmass colliders.
   *
   * Other collisions on the piece level are not handled on the CustomRaftLayer
   *
   * todo remove CustomRaftLayer and use the VehicleLayer instead.
   * - Requires adding explicit collision ignores for the rigidbody attached to VehicleInstance (m_body)
   */
  private void AddPhysicsSettings()
  {
    var layer = LayerMask.NameToLayer("vehicle");

    for (var index = 0; index < 32; ++index)
      Physics.IgnoreLayerCollision(CustomRaftLayer, index,
        Physics.GetIgnoreLayerCollision(layer, index));

    Physics.IgnoreLayerCollision(CustomRaftLayer, LayerMask.NameToLayer("vehicle"),
      true);
    Physics.IgnoreLayerCollision(CustomRaftLayer, LayerMask.NameToLayer("piece"),
      true);
    Physics.IgnoreLayerCollision(CustomRaftLayer, LayerMask.NameToLayer("character"),
      true);
    Physics.IgnoreLayerCollision(CustomRaftLayer, LayerMask.NameToLayer("smoke"),
      true);
    Physics.IgnoreLayerCollision(CustomRaftLayer,
      LayerMask.NameToLayer("character_ghost"), true);
    Physics.IgnoreLayerCollision(CustomRaftLayer, LayerMask.NameToLayer("weapon"),
      true);
    Physics.IgnoreLayerCollision(CustomRaftLayer, LayerMask.NameToLayer("blocker"),
      true);
    Physics.IgnoreLayerCollision(CustomRaftLayer,
      LayerMask.NameToLayer("pathblocker"), true);
    Physics.IgnoreLayerCollision(CustomRaftLayer, LayerMask.NameToLayer("viewblock"),
      true);
    Physics.IgnoreLayerCollision(CustomRaftLayer,
      LayerMask.NameToLayer("character_net"), true);
    Physics.IgnoreLayerCollision(CustomRaftLayer,
      LayerMask.NameToLayer("character_noenv"), true);
    Physics.IgnoreLayerCollision(CustomRaftLayer,
      LayerMask.NameToLayer("Default_small"), false);
    Physics.IgnoreLayerCollision(CustomRaftLayer, LayerMask.NameToLayer("Default"),
      false);
  }

  private void LoadCustomTextures()
  {
    var sails = CustomTextureGroup.Load("Sails");
    foreach (var texture3 in sails.Textures)
    {
      texture3.Texture.wrapMode = TextureWrapMode.Clamp;
      if ((bool)texture3.Normal) texture3.Normal.wrapMode = TextureWrapMode.Clamp;
    }

    var patterns = CustomTextureGroup.Load("Patterns");
    foreach (var texture2 in patterns.Textures)
    {
      texture2.Texture.filterMode = FilterMode.Point;
      texture2.Texture.wrapMode = TextureWrapMode.Repeat;
      if ((bool)texture2.Normal) texture2.Normal.wrapMode = TextureWrapMode.Repeat;
    }

    var logos = CustomTextureGroup.Load("Logos");
    foreach (var texture in logos.Textures)
    {
      texture.Texture.wrapMode = TextureWrapMode.Clamp;
      if ((bool)texture.Normal) texture.Normal.wrapMode = TextureWrapMode.Clamp;
    }
  }

  private void AddGuiLayerComponents()
  {
    VehicleShip.HasVehicleDebugger = HasVehicleDebug.Value;
    MoveableBaseShipComponent.HasVehicleDebugger = HasVehicleDebug.Value;

    AddRemoveVehicleDebugGui();
  }

  /**
   * todo: move to Vehicles plugin when it is ready
   */
  public void AddRemoveVehicleDebugGui()
  {
    _debugGui = GetComponent<VehicleDebugGui>();

    if ((bool)_debugGui && !HasVehicleDebug.Value)
    {
      Destroy(_debugGui);
    }

    if (HasVehicleDebug.Value && !(bool)_debugGui)
    {
      _debugGui = gameObject.AddComponent<VehicleDebugGui>();
    }
  }

  private void AddCustomPieces()
  {
    if (m_customItemsAdded) return;
    // Registers all prefabs using ValheimVehicles PrefabRegistryController
    prefabController = gameObject.AddComponent<PrefabRegistryController>();
    PrefabRegistryController.Init();

    m_customItemsAdded = true;
  }
}
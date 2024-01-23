﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Sentry;
using Sentry.Extensibility;
using Sentry.Unity;
using UnityEngine;

namespace SentryUnitySdkWrapper;

public class Options
{
  private readonly SentryUnityOptions _sentryUnityOptions = new()
  {
    Debug = false
  };

  public SentryUnityOptions GetSentryUnityOptions()
  {
    return _sentryUnityOptions;
  }

  public string PluginName;
  public string PluginGuid;
  public string PluginVersion;
  public string GameName;

  public Options(string pluginGuid, string pluginName, string pluginVersion, string sentryDsn,
    bool enabled = true,
    string gameName = "Valheim")
  {
    PluginVersion = pluginVersion;
    GameName = gameName;
    PluginGuid = pluginGuid;
    PluginName = pluginName;
    _sentryUnityOptions.Enabled = enabled;
    _sentryUnityOptions.Dsn = sentryDsn;
  }
};

[BepInPlugin(BepInGuid, ModName, Version)]
public class SentryUnitySdkWrapperPlugin : BaseUnityPlugin
{
  public const string Author = "zolantris";
  public const string Version = "1.8.0";
  internal const string ModName = "SentryUnitySdkWrapper";
  public const string BepInGuid = $"{Author}.{ModName}";
  private const string HarmonyGuid = $"{Author}.{ModName}";

  public const string ModDescription =
    "A Sentry.Unity wrapper for BepInEx mods. Sentry is logging service that supports scope friendly package logging, this interface aims to centralize Sentry setup and allow other mods to register logging with sentry through the supplied library";

  public const string CopyRight = "Copyright © 2024, GNU-v3 licensed";

  public static Dictionary<string, Options> RegisteredPlugins = new();
  public static Dictionary<string, Options> PendingPluginsToRegister = new();

  private bool _canAutoRegister = true;
  private float _autoRegisterTime = 10f;

  private void Awake()
  {
    StartAutoRegistry();
  }

  public void StartAutoRegistry()
  {
    StartCoroutine(InitializeSentryForRegisteredPlugins());
    StartCoroutine(StartCounting());
  }

  private IEnumerator StartCounting()
  {
    yield return new WaitForSeconds(_autoRegisterTime);
    _canAutoRegister = false;
  }

  private IEnumerator InitializeSentryForRegisteredPlugins()
  {
    while (_canAutoRegister)
    {
      yield return new WaitUntil(() => PendingPluginsToRegister.Count > 0);

      if (PendingPluginsToRegister.Count > 0)
      {
        RegisterPendingPlugins();
      }
    }
  }

  private static void RegisterPendingPlugins()
  {
    var pluginsToRegister = PendingPluginsToRegister.ToList();
    pluginsToRegister.ForEach((plugin) => { InitializeScopedLogging(plugin.Key, plugin.Value); });
  }

  public static Options CreateOptions(string pluginGuid, string modName, string pluginVersion,
    string sentryDsn,
    bool enabled = true,
    string gameName = "Valheim")
  {
    return new Options(pluginGuid, modName, pluginVersion, sentryDsn, enabled, gameName);
  }

  public static void RegisterPluginAsync(Options options)
  {
    PendingPluginsToRegister.Add(options.PluginGuid, options);
  }

  private void HandleCaptureEvent(SentryEvent sentryEvent)
  {
  }

  /**
   * This method will automatically register a plugin with it's guid
   */
  private static bool InitializeScopedLogging(string pluginGuid,
    Options options)
  {
    RegisteredPlugins.TryGetValue(pluginGuid, out var existingPlugin);

    if (existingPlugin != null)
    {
      Debug.LogError(
        $"A registered namespace for this sentry-plugin already exists for {pluginGuid}, please add a different namespace or contact the mod author to change their naming");
      return false;
    }

    if (options.GetSentryUnityOptions().Dsn == "")
    {
      Debug.LogError(
        "No sentry Dsn provided. Please create a unity project within sentry and add the dsn from the projects settings");
      return false;
    }

    SentrySdk.ConfigureScope(scope =>
    {
      scope.Release = options.PluginVersion;
      scope.SetTag("bepinex.plugin.guid", options.PluginGuid);
      scope.SetTag("pluginName", options.PluginName);
      scope.SetTag("gameName", options.GameName);
    });

    // SentrySdk.CaptureException("")

    // Sentry.SentrySdk.CaptureEvent(SentrySdk.CaptureEvent());
    SentryUnity.Init(sentryUnityOptions =>
    {
      // A Sentry Data Source Name (DSN) is required.
      // See https://docs.sentry.io/product/sentry-basics/dsn-explainer/
      // You can set it in the SENTRY_DSN environment variable, or you can set it in code here.
      sentryUnityOptions.Dsn = options.GetSentryUnityOptions().Dsn;

      sentryUnityOptions.CacheDirectoryPath = Path.Combine(Paths.PluginPath, options.PluginGuid);

      // When debug is enabled, the Sentry client will emit detailed debugging information to the console.
      // This might be helpful, or might interfere with the normal operation of your application.
      // We enable it here for demonstration purposes when first trying Sentry.
      // You shouldn't do this in your applications unless you're troubleshooting issues with Sentry.
      sentryUnityOptions.Debug = false;

      // This option is recommended. It enables Sentry's "Release Health" feature.
      sentryUnityOptions.AutoSessionTracking = true;

      // Enabling this option is recommended for client applications only. It ensures all threads use the same global scope.
      sentryUnityOptions.IsGlobalModeEnabled = false;

      // This option will enable Sentry's tracing features. You still need to start transactions and spans.
      sentryUnityOptions.EnableTracing = true;

      // Example sample rate for your transactions: captures 10% of transactions
      sentryUnityOptions.TracesSampleRate = 1.0;
    });

    return true;
  }
}
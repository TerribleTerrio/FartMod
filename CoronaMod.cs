using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
namespace CoronaMod;
using System.IO;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

[BepInPlugin(modGUID, modName, modVersion)]
public class CoronaMod : BaseUnityPlugin
{
    private const string modGUID = "CoronaTerrio.CoronaMod";
    private const string modName = "CoronaMod";
    private const string modVersion = "1.2.0";

    public static CoronaMod Instance { get; private set; } = null!;
    private readonly Harmony harmony = new Harmony(modGUID);
    internal ManualLogSource nls;
    public static AssetBundle networkbundle;
    public GameObject networkPrefab;

    private static void NetcodePatcher()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes();
        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var method in methods)
            {
                var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                if (attributes.Length > 0)
                {
                    method.Invoke(null, null);
                }
            }
        }
    }

    private void Awake()
    {
        NetcodePatcher();

        if (Instance == null)
        {
            Instance = this;
        }

        nls = BepInEx.Logging.Logger.CreateLogSource(modGUID);

        //IMPORT CUSTOM NETWORK OBJECT
        string assetDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "networkbundle");
        AssetBundle networkBundle = AssetBundle.LoadFromFile(assetDir);
        networkPrefab = networkBundle.LoadAsset<GameObject>("Assets/LethalCompany/Mods/FartPlanet/Scripts/NetworkHandler.prefab");
        networkPrefab.AddComponent<NetworkHandler>();

        harmony.PatchAll();

        nls.LogInfo("CoronaMod is loaded!");
    }
}

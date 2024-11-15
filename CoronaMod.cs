using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
namespace CoronaMod;
using System.IO;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using LethalLib.Modules;
using LethalLib.Extras;

[BepInPlugin(modGUID, modName, modVersion)]
[BepInDependency(LethalLib.Plugin.ModGUID, BepInDependency.DependencyFlags.HardDependency)] 
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

    public static AssetBundle unlockablebundle;

    public UnlockableItemDef fridgeUnlockable;

    public GameObject fridgePrefab;

    public TerminalNode fridgeBuyNode;

    public TerminalNode fridgeBuyConfirm;


    public UnlockableItemDef punchingBagUnlockable;

    public GameObject punchingBagPrefab;

    public TerminalNode punchingBagBuyNode;

    public TerminalNode punchingBagBuyConfirm;

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

        //IMPORT UNLOCKABLE OBJECTS
        string unlockableDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "unlockablebundle");        
        AssetBundle unlockableBundle = AssetBundle.LoadFromFile(unlockableDir);
 
        fridgeUnlockable = unlockableBundle.LoadAsset<UnlockableItemDef>("Assets/LethalCompany/Mods/FartPlanet/ExtPrefabs/FridgeDef.asset");
        fridgePrefab = unlockableBundle.LoadAsset<GameObject>("Assets/LethalCompany/Mods/FartPlanet/ExtPrefabs/Fridge.prefab");
        fridgeBuyNode = unlockableBundle.LoadAsset<TerminalNode>("Assets/LethalCompany/Mods/FartPlanet/ExtPrefabs/FridgeBuy.asset");
        fridgeBuyConfirm = unlockableBundle.LoadAsset<TerminalNode>("Assets/LethalCompany/Mods/FartPlanet/ExtPrefabs/FridgeBuyConfirm.asset");
        Unlockables.RegisterUnlockable(fridgeUnlockable, StoreType.Decor, fridgeBuyNode, fridgeBuyConfirm, null, 110);
        LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(fridgePrefab);

        punchingBagUnlockable = unlockableBundle.LoadAsset<UnlockableItemDef>("Assets/LethalCompany/Mods/FartPlanet/ExtPrefabs/punchingBagDef.asset");
        punchingBagPrefab = unlockableBundle.LoadAsset<GameObject>("Assets/LethalCompany/Mods/FartPlanet/ExtPrefabs/punchingBag.prefab");
        punchingBagBuyNode = unlockableBundle.LoadAsset<TerminalNode>("Assets/LethalCompany/Mods/FartPlanet/ExtPrefabs/punchingBagBuy.asset");
        punchingBagBuyConfirm = unlockableBundle.LoadAsset<TerminalNode>("Assets/LethalCompany/Mods/FartPlanet/ExtPrefabs/punchingBagBuyConfirm.asset");
        Unlockables.RegisterUnlockable(punchingBagUnlockable, StoreType.Decor, punchingBagBuyNode, punchingBagBuyConfirm, null, 75);
        LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(punchingBagPrefab);

        harmony.PatchAll();

        nls.LogInfo("CoronaMod is loaded!");
    }
}

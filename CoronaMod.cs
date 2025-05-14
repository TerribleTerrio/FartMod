using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.IO;
using System.Reflection;
using UnityEngine;
using LethalLib.Modules;
using LethalLib.Extras;
namespace CoronaMod;

public static class Masks
{
    public const int PlaceableShipObjects = 67108864;

    public const int Colliders = 2048;

    public const int DefaultRoomCollidersRailingVehicle = 1342179585;

    public const int DefaultRoomCollidersRailingVehicleInteractableObjects = 1342180097;

    public const int DefaultTriggers = 8193;
    
    public const int PropsRoomInteractableObjectVehicle = 1073742656;

    public const int PropsRoomVehicle = 1073742144;

    public const int PropsMapHazards = 2097216;

    public const int PlayerPropsEnemiesMapHazards = 2621512;

    public const int PlayerPropsEnemiesMapHazardsVehicle = 1076363336;

    public const int PlayerEnemies = 524296;

    public const int PlayerEnemiesMapHazards = 2621448;

    public const int RoomVehicle = 1073742080;

    public const int SprayPaintMask = 605030721; //Default, Props, Room, PlayerRagdoll, PlaceableShipObjects, DecalStickableSurface

    public const int BlowtorchMask = 1712327489; //Default, Props, Room, InteractableObject, PlayerRagdoll, Terrain, PlaceableShipObjects, DecalStickableSurface, Vehicle

    public const int RadiatorMask = 1593838337; //Default, Room, InteractableObject, Colliders, Terrain, PlaceableShipObjects, PlacementBlocker, Railing, Vehicle

    public const int WeaponMask = 1084754248; //Player, Props, Room, Colliders, Enemies, MapHazards, EnemiesNotRendered, Vehicle --- Same as shovel

    public const int LadderMask = 268437761; //Default, Room, Colliders, Railing

    public const int InteractableObjectsMask = 1073742656; //Props, Room, InteractableObject, Vehicle
}

public static class Info
{
    public const string GeneralSaveFileName = "FartModPlayerSave";

    public const string SaveFileName1 = "FartModSave1";

    public const string SaveFileName2 = "FartModSave2";

    public const string SaveFileName3 = "FartModSave3";
}

[BepInPlugin(modGUID, modName, modVersion)]
[BepInDependency(LethalLib.Plugin.ModGUID, BepInDependency.DependencyFlags.HardDependency)] 
public class CoronaMod : BaseUnityPlugin
{
    private const string modGUID = "CoronaTerrio.CoronaMod";

    private const string modName = "CoronaMod";

    private const string modVersion = "1.0.0";

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

    public static AssetBundle crowBundle;

    public GameObject MouthDogGhostPrefab;

    public GameObject ForestGiantGhostPrefab;

    public GameObject RadMechGhostPrefab;

    public GameObject MaskedPlayerEnemyGhostPrefab;

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

        //REGISTER UNLOCKABLES
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

        //REGISTER GHOSTS
        string crowDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "crowbundle");        
        AssetBundle crowBundle = AssetBundle.LoadFromFile(crowDir);

        MouthDogGhostPrefab = crowBundle.LoadAsset<GameObject>("Assets/LethalCompany/Mods/FartPlanet/ExtPrefabs/ScarecrowHallucinations/MouthDogGhost.prefab");
        ForestGiantGhostPrefab = crowBundle.LoadAsset<GameObject>("Assets/LethalCompany/Mods/FartPlanet/ExtPrefabs/ScarecrowHallucinations/ForestGiantGhost.prefab");
        RadMechGhostPrefab = crowBundle.LoadAsset<GameObject>("Assets/LethalCompany/Mods/FartPlanet/ExtPrefabs/ScarecrowHallucinations/RadMechGhost.prefab");
        MaskedPlayerEnemyGhostPrefab = crowBundle.LoadAsset<GameObject>("Assets/LethalCompany/Mods/FartPlanet/ExtPrefabs/ScarecrowHallucinations/MaskedPlayerEnemyGhost.prefab");
        LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(MouthDogGhostPrefab);
        LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(ForestGiantGhostPrefab);
        LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(RadMechGhostPrefab);
        LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(MaskedPlayerEnemyGhostPrefab);

        harmony.PatchAll();

        nls.LogInfo("CoronaMod is loaded!");
    }
}

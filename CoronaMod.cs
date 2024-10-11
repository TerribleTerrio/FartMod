using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
namespace CoronaMod;
using Patches;

[BepInPlugin(modGUID, modName, modVersion)]
public class CoronaMod : BaseUnityPlugin
{
    private const string modGUID = "CoronaTerrio.CoronaMod";
    private const string modName = "CoronaMod";
    private const string modVersion = "1.2.0";

    public static CoronaMod Instance { get; private set; } = null!;
    private readonly Harmony harmony = new Harmony(modGUID);
    internal ManualLogSource nls;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        nls = BepInEx.Logging.Logger.CreateLogSource(modGUID);

        harmony.PatchAll(typeof(LandminePatch));
        //harmony.PatchAll(typeof(StartOfRoundPatch));

        nls.LogInfo("CoronaMod is loaded!");
    }
}

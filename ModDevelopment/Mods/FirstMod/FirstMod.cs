using BepInEx;

namespace BadNorth.FirstMod
{
    [BepInPlugin("example.badnorth.firstmod", "Bad North First Mod", "0.1.0")]
    public sealed class FirstMod : BaseUnityPlugin
    {
        private void Awake()
        {
            Logger.LogInfo("Bad North mod loaded");
        }
    }
}

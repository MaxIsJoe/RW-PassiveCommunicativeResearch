using System.Reflection;
using HarmonyLib;
using Verse;

namespace CommunictiveResearch
{
    public class CommunictiveResearchMod : Mod
    {
        public static CommunictiveResearchSettings Settings;

        public CommunictiveResearchMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<CommunictiveResearchSettings>();
            new Harmony("MaxIsJoe.PassiveCommunicativeResearch").PatchAll(Assembly.GetExecutingAssembly());
        }

        public override string SettingsCategory()
        {
            return "Passive Communicative Research";
        }

        //idk what the rimworld 1.7 equiv is
        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            Settings.Draw(inRect);
        }
    }
}

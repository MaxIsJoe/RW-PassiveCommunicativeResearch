using Verse;

namespace CommunictiveResearch
{
    public class CommunictiveResearchSettings : ModSettings
    {
        private const float DefaultTriggerChance = 0.75f;
        private const float DefaultMaximumPointsMultiplier = 2f;
        private const bool DefaultMultiplyAwardedPoints = true;
        private const bool DefaultDebugLogging = false;
        private const float MaximumPointsMultiplierLimit = 5f;

        public float TriggerChance = DefaultTriggerChance;
        public bool MultiplyAwardedPoints = DefaultMultiplyAwardedPoints;
        public float MaximumPointsMultiplier = DefaultMaximumPointsMultiplier;
        public bool DebugLogging = DefaultDebugLogging;

        public void Draw(UnityEngine.Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label("Chance to trigger: " + (TriggerChance * 100f).ToString("F0") + "%");
            TriggerChance = listing.Slider(TriggerChance, 0f, 1f);
            listing.CheckboxLabeled("Multiply awarded points", ref MultiplyAwardedPoints,
                "Randomly multiplies the awarded points between 1x and the maximum multiplier.");
            listing.Label("Maximum points multiplier: " + MaximumPointsMultiplier.ToString("F1") + "x");
            MaximumPointsMultiplier = listing.Slider(MaximumPointsMultiplier, 1f, MaximumPointsMultiplierLimit);
            listing.CheckboxLabeled("Enable debug logging", ref DebugLogging, "Logs the mod's research trigger activity for debugging.");
            listing.End();
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref TriggerChance, "triggerChance", DefaultTriggerChance);
            Scribe_Values.Look(ref MultiplyAwardedPoints, "multiplyAwardedPoints", DefaultMultiplyAwardedPoints);
            Scribe_Values.Look(ref MaximumPointsMultiplier, "maximumPointsMultiplier", DefaultMaximumPointsMultiplier);
            Scribe_Values.Look(ref DebugLogging, "debugLogging", DefaultDebugLogging);
            TriggerChance = TriggerChance < 0f ? 0f : TriggerChance > 1f ? 1f : TriggerChance;
            MaximumPointsMultiplier = MaximumPointsMultiplier < 1f
                ? 1f
                : MaximumPointsMultiplier > MaximumPointsMultiplierLimit
                    ? MaximumPointsMultiplierLimit
                    : MaximumPointsMultiplier;
        }
    }
}

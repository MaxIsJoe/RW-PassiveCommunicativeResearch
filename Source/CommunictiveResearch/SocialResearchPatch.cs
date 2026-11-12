using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CommunictiveResearch;

[HarmonyPatch]
internal static class SocialResearchPatch
{
    private const float CurrentProjectWeight = 5f;
    private const float ExceptionalMoodThreshold = 0.9f;
    private const float BadMoodThreshold = 0.2f;
    private const string TryInteractWith = "TryInteractWith";

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(Pawn_InteractionsTracker), TryInteractWith, new[] { typeof(Pawn), typeof(InteractionDef) }
            );
    }

    [HarmonyPostfix]
    private static void Postfix(Pawn ___pawn,
        [HarmonyArgument(0)] Pawn recipient, [HarmonyArgument(1)] InteractionDef intDef,
        bool __result)
    {
        if (!__result || ___pawn == null || recipient == null || ___pawn == recipient || intDef == null)
        {
            LogDebug("Interaction aborted: invalid pawn or interaction state.");
            return;
        }

        //optimize mayhabs?
        CommunictiveResearchSettings settings = CommunictiveResearchMod.Settings;
        if (settings == null)
        {
            LogDebug("Something fucked up because there's no mod settings available to reference shit from.");
            return;
        }

        LogDebug("Interaction check: " + ___pawn.NameShortColored + " -> " + recipient.NameShortColored + " via " + intDef.label + ". Trigger chance: " + settings.TriggerChance);
        if (Rand.Chance(settings.TriggerChance) == false)
        {
            LogDebug("Interaction skipped: chance check failed.");
            return;
        }

        if (GetPawnMood(___pawn) <= BadMoodThreshold)
        {
            LogDebug("Interaction skipped: pawn has a bad mood.");
            return;
        }

        ResearchManager researchManager = Find.ResearchManager;
        if (researchManager == null)
        {
            LogDebug("Interaction skipped: no research manager available.");
            return;
        }

        float initiatorPoints = GetResearchPoints(___pawn);
        float recipientPoints = GetResearchPoints(recipient);
        float researchPoints = initiatorPoints + recipientPoints;
        LogDebug("Research points calculated: " + researchPoints + " from " + ___pawn.NameShortColored + " and " + recipient.NameShortColored + ".");
        if (researchPoints <= 0f)
        {
            LogDebug("Interaction skipped: total research points were 0 or less.");
            return;
        }

        float pointsMultiplier = 1f;
        if (settings.MultiplyAwardedPoints)
        {
            pointsMultiplier = Rand.Range(1f, settings.MaximumPointsMultiplier);
            researchPoints *= pointsMultiplier;
        }

        LogDebug("Point multipliers: " + ___pawn.NameShortColored + " contributed " + initiatorPoints + ", " + recipient.NameShortColored + " contributed " + recipientPoints + "; random multiplier " + pointsMultiplier + "x; final total " + researchPoints + ".");

        var projects = new List<ResearchProjectDef>();
        foreach (ResearchProjectDef project in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
        {
            if (project.IsFinished == false) projects.Add(project);
        }

        if (projects.Count == 0)
        {
            LogDebug("Interaction skipped: no unfinished research projects exist.");
            return;
        }

        ResearchProjectDef currentProject = GetCurrentProject(researchManager, projects);
        ResearchProjectDef projectToAdvance = SelectProject(projects, currentProject, settings);
        float previousProgress = researchManager.GetProgress(projectToAdvance);
        researchManager.AddProgress(projectToAdvance, researchPoints, ___pawn);
        float appliedProgress = researchManager.GetProgress(projectToAdvance) - previousProgress;
        Notify(appliedProgress, projectToAdvance, currentProject, ___pawn, recipient, researchPoints, settings);
    }

    private static void Notify(float appliedProgress, ResearchProjectDef projectToAdvance, ResearchProjectDef currentProject, Pawn ___pawn, Pawn recipient, float researchPoints, CommunictiveResearchSettings settings)
    {
        LogDebug("Applied " + appliedProgress + " research points to " + projectToAdvance.label +
                 (currentProject == null
                     ? " (no active project)."
                     : " (active project weight: " +
                       (projectToAdvance == currentProject ? CurrentProjectWeight : 1f) + ")."));
        if (researchPoints <= 0 || settings.NotifyWhenResearchProgresses == false) return;
        var msg = $"{___pawn.NameShortColored} and {recipient.NameShortColored} had a discussion about {projectToAdvance.label}. ({(int)researchPoints})";
        Messages.Message(msg, MessageTypeDefOf.PositiveEvent);
    }

    private static float GetResearchPoints(Pawn pawn)
    {
        if (pawn.skills == null) return 0f;

        SkillRecord intellectual = pawn.skills.GetSkill(SkillDefOf.Intellectual);
        SkillRecord social = pawn.skills.GetSkill(SkillDefOf.Social);
        SkillRecord creative = pawn.skills.GetSkill(SkillDefOf.Artistic);
        float points = (intellectual?.Level ?? 0) + (social?.Level ?? 0) + (creative?.Level ?? 0);

        bool researchingAtBench = pawn.CurJobDef == JobDefOf.Research &&
                                pawn.CurJob.targetA.Thing is Building_ResearchBench;
        if (researchingAtBench)
        {
            points *= 3f;
            LogDebug(pawn.NameShortColored + " was researching at a bench while talking to a colonist; their points were tripled.");
        }

        if (GetPawnMood(pawn) >= ExceptionalMoodThreshold)
        {
            points *= 2f;
            LogDebug(pawn.NameShortColored + " had an exceptional mood; their points were doubled.");
        }

        return points;
    }

    private static float GetPawnMood(Pawn pawn)
    {
        if (pawn.needs?.mood == null) return 0;
        return pawn.needs.mood.CurLevelPercentage;
    }

    private static ResearchProjectDef SelectProject(List<ResearchProjectDef> projects, ResearchProjectDef currentProject, CommunictiveResearchSettings settings)
    {
        if (currentProject != null && Rand.Chance(0.65f))
        {
            return currentProject;
        }
        //dumbshit
        var totalWeight = 0f;
        foreach (ResearchProjectDef project in projects)
        {
            if (project.PrerequisitesCompleted == false && settings.PrerequisitesRequired) continue;
            totalWeight += project == currentProject ? CurrentProjectWeight : 1f;
        }

        float selection = Rand.Range(0f, totalWeight);
        foreach (ResearchProjectDef project in projects)
        {
            if (project.PrerequisitesCompleted == false && settings.PrerequisitesRequired) continue;
            selection -= project == currentProject ? CurrentProjectWeight : 1f;
            if (selection < 0f) return project;
        }

        return projects[^1];
    }

    private static ResearchProjectDef GetCurrentProject(ResearchManager researchManager, List<ResearchProjectDef> projects)
    {
        foreach (ResearchProjectDef project in projects)
        {
            if (researchManager.IsCurrentProject(project)) return project;
        }

        return null;
    }

    private static void LogDebug(string message)
    {
        if (CommunictiveResearchMod.Settings != null && CommunictiveResearchMod.Settings.DebugLogging)
        {
            Log.Message("[Passive Communicative Research] - " + message);
        }
    }
}
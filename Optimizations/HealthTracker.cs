using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace ExperimentalOptimizations.Optimizations
{
    public class Pawn_HealthTracker_Settings
    {
        public static bool Pawn_HealthTracker = true;
       
        public static bool Enabled() => Pawn_HealthTracker;

        public static void DoSettingsWindowContents(Listing_Standard l)
        {
            if (l.CheckBoxIsChanged("Pawn_HealthTracker".Translate(), ref Pawn_HealthTracker))
            {
                if (Pawn_HealthTracker) HealthTracker.Patch();
                else HealthTracker.UnPatch();
            }
        }

        public static void ExposeData()
        {
            Scribe_Values.Look(ref Pawn_HealthTracker, "Pawn_HealthTracker", true);
        }
    }

    [Optimization("Pawn_HealthTracker", typeof(Pawn_HealthTracker_Settings))]
    public class HealthTracker
    {
        private const int HealthTickInterval = 5;
        private static readonly List<H.PatchInfo> Patches = new List<H.PatchInfo>();

        public static void Init()
        {
            var harmonyMethod = typeof(ExperimentalOptimizations).Method(nameof(HealthTick)).ToHarmonyMethod(priority: 999);
            H.PatchInfo patch = typeof(Pawn_HealthTracker).Method(nameof(Pawn_HealthTracker.HealthTick)).Patch(prefix: harmonyMethod, autoPatch: false);
            Patches.Add(patch);

            harmonyMethod = typeof(ExperimentalOptimizations).Method(nameof(CompensateReducedImmunityTick)).ToHarmonyMethod();
            patch = typeof(ImmunityRecord).Method(nameof(ImmunityRecord.ImmunityTick)).Patch(transpiler: harmonyMethod, autoPatch: false);
            Patches.Add(patch);
        }

        public static void Patch()
        {
            foreach (var patch in Patches) patch.Enable();
            Log.Message($"[ExperimentalOptimizations] PatchHealthTick done");
        }

        public static void UnPatch()
        {
            foreach (var patch in Patches) patch.Disable();
            Log.Message($"[ExperimentalOptimizations] UnPatchHealthTick done");
        }

        private static IEnumerable<CodeInstruction> CompensateReducedImmunityTick(IEnumerable<CodeInstruction> instructions)
        {
            var immunityChangePerTick = AccessTools.Method(typeof(ImmunityRecord), nameof(ImmunityRecord.ImmunityChangePerTick));
            bool ok = false;
            foreach (var ci in instructions)
            {
                yield return ci;
                if (ci.opcode == OpCodes.Call && ci.operand == immunityChangePerTick)
                {
                    // this.immunity += this.ImmunityChangePerTick(pawn, sick, diseaseInstance) * 5; // compensate reduced HealthTick => * 5
                    yield return new CodeInstruction(OpCodes.Ldc_R4, (float)HealthTickInterval);
                    yield return new CodeInstruction(OpCodes.Mul);
                    ok = true;
                }
            }
            if (!ok) Log.Error($"[ExperimentalOptimizations] call ImmunityChangePerTick not found!");
        }
		
        private static bool HealthTick(Pawn_HealthTracker __instance)
        {
            return __instance.pawn.IsHashIntervalTick(HealthTickInterval);
        }
    }
}
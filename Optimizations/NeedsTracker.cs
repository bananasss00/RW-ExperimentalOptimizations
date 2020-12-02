using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ExperimentalOptimizations.Optimizations
{
    public class Pawn_NeedsTracker_Settings
    {
        public static bool Pawn_NeedsTracker = false;
        public static int Pawn_NeedsTracker_Interval = 200; // vanilla value 150

        private static bool _intervalChanged = false;
        private static DateTime _intervalChangedTime = DateTime.Now;

        public static bool Enabled() => Pawn_NeedsTracker;

        public static void DoSettingsWindowContents(Listing_Standard l)
        {
            if (l.CheckBoxIsChanged("Pawn_NeedsTracker".Translate(), ref Pawn_NeedsTracker))
            {
                if (Pawn_NeedsTracker) NeedsTracker.Patch();
                else NeedsTracker.UnPatch();
                _intervalChanged = false;
            }
            if (l.TextFieldNumericChanged("Pawn_NeedsTracker_Interval".Translate(), ref Pawn_NeedsTracker_Interval, 150, 1000))
            {
                _intervalChanged = true;
                _intervalChangedTime = DateTime.Now;
            }

            if (_intervalChanged && (DateTime.Now - _intervalChangedTime).TotalMilliseconds > 250)
            {
                if (Pawn_NeedsTracker)
                {
                    NeedsTracker.UnPatch();
                    NeedsTracker.Patch();
                }
                _intervalChanged = false;
            }
        }

        public static void ExposeData()
        {
            Scribe_Values.Look(ref Pawn_NeedsTracker, "Pawn_NeedsTracker", false);
            Scribe_Values.Look(ref Pawn_NeedsTracker_Interval, "Pawn_NeedsTracker_Interval", 200);
        }
    }

    [Optimization("Pawn_NeedsTracker", typeof(Pawn_NeedsTracker_Settings))]
    public class NeedsTracker
    {
        private static readonly List<H.PatchInfo> Patches = new List<H.PatchInfo>();

        public static void Init()
        {
            var trans = typeof(NeedsTracker).Method(nameof(NeedsTrackerTick_Transpiler)).ToHarmonyMethod(priority: 999);

            H.PatchInfo patch;

            patch = typeof(Pawn_NeedsTracker).Method(nameof(Pawn_NeedsTracker.NeedsTrackerTick)).Patch(transpiler: trans, autoPatch: false);
            Patches.Add(patch);

            patch = typeof(JoyToleranceSet).Method(nameof(JoyToleranceSet.NeedInterval)).Patch(transpiler: trans, autoPatch: false);
            Patches.Add(patch);

            patch = typeof(Need_Chemical).Method(nameof(Need_Chemical.NeedInterval)).Patch(transpiler: trans, autoPatch: false);
            Patches.Add(patch);

            patch = typeof(Need_Food).Method(nameof(Need_Food.NeedInterval)).Patch(transpiler: trans, autoPatch: false);
            Patches.Add(patch);

            patch = typeof(Need_Rest).Method(nameof(Need_Rest.NeedInterval)).Patch(transpiler: trans, autoPatch: false);
            Patches.Add(patch);

            patch = typeof(Thought_Memory).Method(nameof(Thought_Memory.ThoughtInterval)).Patch(transpiler: trans, autoPatch: false);
            Patches.Add(patch);

            // Dubs Hygiene
            {
                patch = "DubsBadHygiene.Need_Bladder:NeedInterval".Method(warn: false).Patch(transpiler: trans, autoPatch: false);
                if (patch != null) Patches.Add(patch);
                
                patch = "DubsBadHygiene.Need_Hygiene:NeedInterval".Method(warn: false).Patch(transpiler: trans, autoPatch: false);
                if (patch != null) Patches.Add(patch);
                
                patch = "DubsBadHygiene.Need_Thirst:NeedInterval".Method(warn: false).Patch(transpiler: trans, autoPatch: false);
                if (patch != null) Patches.Add(patch);
            }
        }

        public static void Patch()
        {
            foreach (var patch in Patches) patch.Enable();
            Log.Message($"[ExperimentalOptimizations] PatchNeedsTrackerTick done");
        }

        public static void UnPatch()
        {
            foreach (var patch in Patches) patch.Disable();
            Log.Message($"[ExperimentalOptimizations] UnPatchNeedsTrackerTick done");
        }

        private static IEnumerable<CodeInstruction> NeedsTrackerTick_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool ok = false;
            foreach (var ci in instructions)
            {
                if (ci.opcode == OpCodes.Ldc_I4 && (int)ci.operand == 150)
                {
                    ci.operand = Pawn_NeedsTracker_Settings.Pawn_NeedsTracker_Interval;
                    ok = true;
                }
                else if (ci.opcode == OpCodes.Ldc_R4 && (float)ci.operand == 150)
                {
                    ci.operand = (float)Pawn_NeedsTracker_Settings.Pawn_NeedsTracker_Interval;
                    ok = true;
                }
                yield return ci;
            }
            if (!ok) Log.Error("[Transpiler] Ldc_I4 or Ldc_R4 not found!");
        }
    }
}
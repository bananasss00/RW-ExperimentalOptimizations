using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace ExperimentalOptimizations.Fix
{
    [FixOn(InitStage.StaticConstructorOnStartup)]
    public class ColonyLeadership
    {
        private const int UpdateDelay = 60;
        private static readonly Dictionary<int, int> LeaderHediffLastUpdate = new Dictionary<int, int>();

        public static void Patch()
        {
            var hediffLeaderTick = "Nandonalt_ColonyLeadership.HediffLeader:Tick".Method(warn: false);
            if (hediffLeaderTick != null)
            {
                hediffLeaderTick.Patch(transpiler: typeof(ColonyLeadership).Method(nameof(Transpiler)).ToHarmonyMethod());
                Log.Message($"[ExperimentalOptimizations] Nandonalt_ColonyLeadership.HediffLeader:Tick optimized");
            }
        }

        private static bool NeedUpdate(HediffWithComps hediff)
        {
            int id = hediff.loadID;
            int curTick = Find.TickManager.ticksGameInt;
            if (!LeaderHediffLastUpdate.ContainsKey(id))
            {
#if DEBUG
                Log.Message($"[EO-ColonyLeadership] new. update HediffLeader. pawn: {hediff.pawn.LabelCap}");
#endif
                LeaderHediffLastUpdate.Add(id, curTick);
                return true;
            }

            if (Math.Abs(curTick - LeaderHediffLastUpdate[id]) >= UpdateDelay)
            {
#if DEBUG
                Log.Message($"[EO-ColonyLeadership] update HediffLeader. pawn: {hediff.pawn.LabelCap}");
#endif
                LeaderHediffLastUpdate[id] = curTick;
                return true;
            }

            return false;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
        {
            var needUpdate = AccessTools.Method(typeof(ColonyLeadership), nameof(NeedUpdate));
            var hediffLeader = AccessTools.TypeByName("Nandonalt_ColonyLeadership.HediffLeader");
            var counter = AccessTools.Field(hediffLeader, "counter");
            var code = instructions.ToList();
            int idx = -1;
            // find injection offset
            for (int i = 1; i < code.Count; i++)
            {
                // bool flag6 = this.counter >= 100;
                // bool flag7 = flag6;
                if (code[i].opcode == OpCodes.Ldc_I4_S && (code[i].operand is sbyte b) && b == 100 &&
                    code[i - 1].operand == counter &&
                    code[i + 1].opcode == OpCodes.Clt &&
                    code[i + 2].opcode == OpCodes.Ldc_I4_0 &&
                    code[i + 3].opcode == OpCodes.Ceq)
                {
                    idx = i;
                    break;
                }
            }

            if (idx == -1)
            {
                Log.Error($"Nandonalt_ColonyLeadership.HediffLeader:Tick outdated!");
                return instructions;
            }

            /*
            95	00D8	ldc.i4.s	100
            96	00DA	clt
            97	00DC	ldc.i4.0
            98	00DD	ceq
            99	00DF	stloc.s	flag6 (11)
             */
            var labelToLdc_97 = ilGen.DefineLabel();
            var labelToStloc_99 = ilGen.DefineLabel();
            code[idx + 2].labels.Add(labelToLdc_97); // 97	00DC	ldc.i4.0
            code[idx + 4].labels.Add(labelToStloc_99); // 99	00DF	stloc.s	flag6 (11)
            code.RemoveAt(idx + 3); // remove 98	00DD	ceq
            code.RemoveAt(idx + 1); // remove 96	00DA	clt
            /*
            95	00D8	ldc.i4.s	100
            97	00DC	ldc.i4.0            ; labelToLdc_97
            99	00DF	stloc.s	flag6 (11)  ; labelToStloc_99
             */
            code.InsertRange(idx + 1, new []
            {
                new CodeInstruction(OpCodes.Blt_S, labelToLdc_97), 
                new CodeInstruction(OpCodes.Ldarg_0), 
                new CodeInstruction(OpCodes.Call, needUpdate), 
                new CodeInstruction(OpCodes.Br_S, labelToStloc_99), 
            });
            /*
            95	00D8	ldc.i4.s	100
              => blt.s	labelToLdc_97
              => ldarg.0
              => call NeedUpdate(class ['Assembly-CSharp']Verse.HediffWithComps)
              => br.s labelToStloc_99
            97	00DC	ldc.i4.0            ; labelToLdc_97
            99	00DF	stloc.s	flag6 (11)  ; labelToStloc_99

            // bool flag6 = this.counter >= 100 && NeedUpdate(this);
            // bool flag7 = flag6;
             */

#if DEBUG
            var dir = GenFilePaths.FolderUnderSaveData("TranspilerDebug");
            File.WriteAllLines($"{dir}\\Nandonalt_ColonyLeadership.HediffLeader_Tick_Opt.before.txt", instructions.Select(x => x.ToString()));
            File.WriteAllLines($"{dir}\\Nandonalt_ColonyLeadership.HediffLeader_Tick_Opt.after.txt", code.Select(x => x.ToString()));
#endif

            return code;
        }
    }
}
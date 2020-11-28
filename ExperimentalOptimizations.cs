using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ExperimentalOptimizations
{
    public enum InitStage
    {
        StaticConstructorOnStartup,
        ModInit
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class FixOn : Attribute
    {
        public InitStage stage;
        public FixOn(InitStage stage) => this.stage = stage;
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class Optimization : Attribute
    {
        public string name;
        public Type optimizationSetting;
        public Optimization(string name, Type optimizationSetting) => (this.name, this.optimizationSetting) = (name, optimizationSetting);
        
        // optimizationSetting implement:
        //   public static bool Enabled();
        //   public static void DoSettingsWindowContents(Listing_Standard l);
        //   public static void ExposeData()
    }

    [StaticConstructorOnStartup]
    public class ExperimentalOptimizationsMod
    {
        static ExperimentalOptimizationsMod()
        {
            // apply fixes on StaticConstructorOnStartup
            var fixesOnStaticConstructor = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.TryGetAttribute(out FixOn fix) && fix.stage == InitStage.StaticConstructorOnStartup);
            foreach (var t in fixesOnStaticConstructor)
            {
                t.InvokeStaticMethod("Patch");
            }
        }
    }

    public class ExperimentalOptimizations : Mod
    {
        public static Type[] Optimizations { get; set; }

        public override void DoSettingsWindowContents(Rect inRect) => Settings.DoSettingsWindowContents(inRect);
        public override string SettingsCategory() => "ExperimentalOptimizations";

        public ExperimentalOptimizations(ModContentPack content) : base(content)
        {
            // apply fixes on ModInit
            var allTypes = Assembly.GetExecutingAssembly().GetTypes();
            var fixesOnModInit = allTypes.Where(t => t.TryGetAttribute(out FixOn fix) && fix.stage == InitStage.ModInit);
            foreach (var t in fixesOnModInit)
            {
                t.InvokeStaticMethod("Patch");
            }

            Optimizations = allTypes.Where(t => t.TryGetAttribute<Optimization>(out _)).ToArray();
            GetSettings<Settings>();

            foreach (var optimization in Optimizations)
            {
                var opt = optimization.TryGetAttribute<Optimization>();
                if (opt.optimizationSetting.InvokeStaticMethod<bool>("Enabled"))
                {
                    optimization.InvokeStaticMethod("Patch");
                }
            }

            Log.Message($"[ExperimentalOptimizations] initialized");
        }
    }
}

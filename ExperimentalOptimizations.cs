﻿using System;
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
            var allTypes = Assembly.GetExecutingAssembly().GetTypes();

            // apply fixes on StaticConstructorOnStartup
            var fixesOnStaticConstructor = allTypes.Where(t => t.TryGetAttribute(out FixOn fix) && fix.stage == InitStage.StaticConstructorOnStartup);
            foreach (var t in fixesOnStaticConstructor)
            {
                t.InvokeStaticMethod("Patch");
            }

            if (ExperimentalOptimizations.Optimizations == null)
            {
                // init optimizations
                ExperimentalOptimizations.Optimizations = allTypes.Where(t => t.TryGetAttribute<Optimization>(out _)).ToArray();
            }

            // ModInit or StaticConstructorOnStartup faster?
            if (ExperimentalOptimizations.Instance == null)
            {
                Log.Error($"Can't init settings!");
            }

            foreach (var optimization in ExperimentalOptimizations.Optimizations)
            {
                var opt = optimization.TryGetAttribute<Optimization>();
                // initialize
                optimization.InvokeStaticMethod("Init");
                // patch enabled in settings opts.
                if (opt.optimizationSetting.InvokeStaticMethod<bool>("Enabled"))
                {
                    optimization.InvokeStaticMethod("Patch");
                }
            }
        }
    }

    public class ExperimentalOptimizations : Mod
    {
        public static Type[] Optimizations { get; set; }

        public override void DoSettingsWindowContents(Rect inRect) => Settings.DoSettingsWindowContents(inRect);
        public override string SettingsCategory() => "ExperimentalOptimizations";

        public static ExperimentalOptimizations Instance;

        public ExperimentalOptimizations(ModContentPack content) : base(content)
        {
            Instance = this;

            // apply fixes on ModInit
            var allTypes = Assembly.GetExecutingAssembly().GetTypes();
            var fixesOnModInit = allTypes.Where(t => t.TryGetAttribute(out FixOn fix) && fix.stage == InitStage.ModInit);
            foreach (var t in fixesOnModInit)
            {
                t.InvokeStaticMethod("Patch");
            }

            if (ExperimentalOptimizations.Optimizations == null)
            {
                // init optimizations
                ExperimentalOptimizations.Optimizations = allTypes.Where(t => t.TryGetAttribute<Optimization>(out _)).ToArray();
            }

            GetSettings<Settings>();
            
            Log.Message($"[ExperimentalOptimizations] initialized");
        }
    }
}

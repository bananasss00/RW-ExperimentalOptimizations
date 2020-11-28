using UnityEngine;
using Verse;

namespace ExperimentalOptimizations
{
    public static class Listing_Standard_Extensions
    {
        private static string _editBuf;

        public static bool CheckBoxIsChanged(this Listing_Standard l, string label, ref bool value)
        {
            bool tmp = value;
            l.CheckboxLabeled(label, ref tmp);
            bool stateChanged = value != tmp;
            value = tmp;
            return stateChanged;
        }

        public static bool TextFieldNumericChanged(this Listing_Standard l, string label, ref int value, float min, float max)
        {
            int tmp = value;
            l.TextFieldNumericLabeled(label, ref tmp, ref _editBuf, min, max);
            bool stateChanged = value != tmp;
            value = tmp;
            return stateChanged;
        }
    }

    public class Settings : ModSettings
    {
        public static void DoSettingsWindowContents(Rect rect)
        {
            var l = new Listing_Standard();
            l.Begin(rect);

            foreach (var optimization in ExperimentalOptimizations.Optimizations)
            {
                var opt = optimization.TryGetAttribute<Optimization>();
                opt.optimizationSetting.InvokeStaticMethod("DoSettingsWindowContents", l);
            }

            l.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            foreach (var optimization in ExperimentalOptimizations.Optimizations)
            {
                var opt = optimization.TryGetAttribute<Optimization>();
                opt.optimizationSetting.InvokeStaticMethod("ExposeData");
            }
        }
    }
}
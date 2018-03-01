using Harmony;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace FinishTheDamnThing
{
    public class Main : Mod
    {
        public static HarmonyInstance harmony = HarmonyInstance.Create("com.github.L0laapk3.RimWorld.FinishTheDamnThing");

        public Main(ModContentPack content) : base(content)
        {
            

            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }




    [HarmonyPatch(typeof(WorkGiver_DoBill))]
    [HarmonyPatch("FinishUftJob")]
    [HarmonyPatch(new Type[] { typeof(Pawn), typeof(UnfinishedThing), typeof(Bill_ProductionWithUft) })]
    public static class FinishUftJob_Patch
    {

        //changes: uft.Creator != pawn to: pawn != pawn
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            for (int i = 3; i < instructions.Count(); i++)
                yield return instructions.ElementAt(i);

            Log.Warning("[FinishTheDamnThing] FinishUftJob Patched!");
        }
    }






    [HarmonyPatch(typeof(WorkGiver_DoBill))]
    [HarmonyPatch("ClosestUnfinishedThingForBill")]
    public static class ClosestUnfinishedThingForBill_predicate_Patch
    {

        private static OpCode[] codeToRemove = new OpCode[] {
            OpCodes.Ldarg_1,
            OpCodes.Castclass,
            OpCodes.Callvirt,
            OpCodes.Ldarg_0,
            OpCodes.Ldfld,
            OpCodes.Bne_Un
        };

        //removes: ((UnfinishedThing)t).Creator == pawn
        public static IEnumerable<CodeInstruction> PredicateTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            int timesPatched = 0;


            int i = 0;
            while(i < instructions.Count() - 2)
            {
                CodeInstruction instruction = instructions.ElementAt(i + 2);
                if (instruction.opcode == OpCodes.Callvirt && instruction.operand == typeof(UnfinishedThing).GetProperty("Creator").GetGetMethod())
                {
                    i += 6;
                    timesPatched++;
                }
                yield return instructions.ElementAt(i);
                i++;
            }
            yield return instructions.ElementAt(i++);
            yield return instructions.ElementAt(i);

            if (timesPatched != 1)
                Log.Error("[FinishTheDamnThing] Couldn't properly patch ClosestUnfinishedThingForBill! the patch was applied " + timesPatched + " times.");
            else
                Log.Warning("[FinishTheDamnThing] ClosestUnfinishedThingForBill Patched!");
        }



        //finds predicate creator and applies transpiler on deligate
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int timesPatched = 0;

            yield return instructions.ElementAt(0);
            for (int i = 1; i < instructions.Count(); i++)
            {
                CodeInstruction instruction = instructions.ElementAt(i);
                if (instruction.opcode == OpCodes.Newobj && instruction.operand == typeof(Predicate<Thing>).GetConstructors()[0])
                {
                    MethodInfo predicate = instructions.ElementAt(i - 1).operand as MethodInfo;
                    if (predicate != null)
                    {
                        Main.harmony.Patch(predicate, null, null, new HarmonyMethod(typeof(ClosestUnfinishedThingForBill_predicate_Patch), nameof(PredicateTranspiler)));
                        timesPatched++;
                    }
                    else
                    {
                        Log.Error("[FinishTheDamnThing] Found predicate inside ClosestUnfinishedThingForBill, but couldn't find deligate function");
                    }
                }
                yield return instruction;
            }


            if (timesPatched != 1)
                Log.Error("[FinishTheDamnThing] Found " + timesPatched + " instances of predicates inside ClosestUnfinishedThingForBill.");
        }
    }





    /*

    //wrapper method.. probably works


    [HarmonyPatch(typeof(WorkGiver_DoBill))]
    [HarmonyPatch("ClosestUnfinishedThingForBill")]
    public static class ClosestUnfinishedThingForBill_Patch
    {
        static Predicate<Thing> Wrapper(Predicate<Thing> originalPredicate, Pawn pawn)
        {
            return (Thing t) =>
            {
                UnfinishedThing uft = ((UnfinishedThing)t);
                Pawn creator = uft.Creator;
                uft.Creator = pawn;
                bool result = originalPredicate(t);
                uft.Creator = creator;
                return result;
            };
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var innerValidatorType = (instructions.ToArray()[0].operand as MethodBase).DeclaringType;
            if (innerValidatorType == null) Log.Error("Cannot find inner validator type");
            var f_innerValidator = innerValidatorType.GetField("predicate");

            var found = false;
            foreach (var instruction in instructions)
            {
                if (found == false && instruction.opcode == OpCodes.Stfld && instruction.operand == f_innerValidator)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(() => WrappedValidator(null, null)));
                    found = true;
                }
                yield return instruction;
            }

            if (!found) Log.Error("Unexpected code in patch " + MethodBase.GetCurrentMethod().DeclaringType);
	    }
    }

        */


    [HarmonyPatch(typeof(WorkGiver_DoBill))]
    [HarmonyPatch("StartOrResumeBillJob")]
    [HarmonyPatch(new Type[] { typeof(Pawn), typeof(IBillGiver) })]
    public static class StartOrResumeBillJob_Patch
    {

        //removes: bill_ProductionWithUft.BoundWorker != pawn
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int timesPatched = 0;

            int i = 0;
            while (i < instructions.Count() - 1)
            {
                CodeInstruction after = instructions.ElementAt(i + 1);
                if (after.opcode == OpCodes.Callvirt && after.operand == AccessTools.Property(typeof(Bill_ProductionWithUft), nameof(Bill_ProductionWithUft.BoundWorker)).GetGetMethod())
                {
                    i += 4;
                    timesPatched++;
                }
                yield return instructions.ElementAt(i);
                i++;
            }

            if (timesPatched != 1)
                Log.Error("[FinishTheDamnThing] Couldn't properly patch StartOrResumeBillJob! the patch was applied " + timesPatched + " times.");
            else
                Log.Warning("[FinishTheDamnThing] StartOrResumeBillJob Patched!");


            yield return instructions.ElementAt(i);
        }
    }
}
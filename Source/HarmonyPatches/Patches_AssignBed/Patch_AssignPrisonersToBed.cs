using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Remoting.Messaging;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PrisonLabor.HarmonyPatches.Patches_AssignBed
{
    [HarmonyPatch(typeof(Building_Bed))]
    [HarmonyPatch(nameof(Building_Bed.GetGizmos))]
    static class Patch_AssignPrisonersToBed
    {
        static IEnumerable<CodeInstruction> Transpiler(ILGenerator gen, MethodBase mBase, IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instr in instructions)
            {
                if (instr.opcode == OpCodes.Ret)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, typeof(Patch_AssignPrisonersToBed).GetMethod(nameof(NewGizmos)));
                }
                yield return instr;
            }
        }

        public static IEnumerable<Gizmo> NewGizmos(IEnumerable<Gizmo> gizmos, Building_Bed bed)
        {
            foreach (var gizmo in gizmos)
                yield return gizmo;

            if (bed.ForPrisoners)
            {
                yield return new Command_Action()
                {
                    defaultLabel = "PrisonLabor_CommandBedSetOwnerLabel".Translate(),
                    defaultDesc = "PrisonLabor_CommandBedSetOwnerDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("ui/commands/AssignOwner", true),
                    action = new Action(() => Find.WindowStack.Add(new Dialog_AssignBuildingOwner(bed.CompAssignableToPawn))),
                };
            }
        }
    }


    [HarmonyPatch(typeof(CompAssignableToPawn_Bed))]
    [HarmonyPatch("get_" + nameof(CompAssignableToPawn.AssigningCandidates))]
    static class Patch_MakePrisonersCandidates
    {
        static bool Prefix(ref IEnumerable<Pawn> __result, CompAssignableToPawn __instance)
        {
            Building_Bed bed = __instance.parent as Building_Bed;
            if (bed != null && bed.Spawned && __instance is CompAssignableToPawn_Bed && bed.ForPrisoners)
            {
                __result = bed.Map.mapPawns.PrisonersOfColony;
                return false;
            }

            return true;
        }
    }


    [HarmonyPatch(typeof(WorkGiver_Warden_TakeToBed))]
    [HarmonyPatch("TakeToPreferredBedJob")]
    static class Patch_TakePrisonersToOwnedBed
    {
        /*  === Orignal code Look-up===
             * 
             *  if (RestUtility.FindBedFor(prisoner, prisoner, true, true, false) != null)
             *  {
             *  	return null;
             *  }
             *  
             *  === CIL Instructions ===
             *  
             *  ldarg.1 |  | Label 2
             *  ldarg.1 |  | no labels
             *  ldc.i4.1 |  | no labels
             *  ldc.i4.1 |  | no labels
             *  ldc.i4.0 |  | no labels
             *  call | RimWorld.Building_Bed FindBedFor(Verse.Pawn, Verse.Pawn, Boolean, Boolean, Boolean) | no labels
             *  brfalse | Label 3 | no labels
             */

        static IEnumerable<CodeInstruction> Transpiler(ILGenerator gen, MethodBase mBase, IEnumerable<CodeInstruction> instructions)
        {
            OpCode[] opCodes1 =
{
                OpCodes.Ldarg_0,
                OpCodes.Ldarg_0,
                OpCodes.Ldc_I4_1,
                OpCodes.Ldc_I4_0,
                OpCodes.Ldc_I4_1,
                OpCodes.Newobj,
                OpCodes.Call,
                OpCodes.Brfalse_S,
            };
            string[] operands1 =
            {
                "",
                "",
                "",
                "",
                "",
                "Void .ctor(GuestStatus)",
                "RimWorld.Building_Bed FindBedFor(Verse.Pawn, Verse.Pawn, Boolean, Boolean, System.Nullable`1[RimWorld.GuestStatus])",
                "System.Reflection.Emit.Label",
            };
            int step1 = 0;

            var label_OriginalBranch = gen.DefineLabel();            
            foreach (var instr in instructions)
            {
                if (HPatcher.IsFragment(opCodes1, operands1, instr, ref step1, nameof(Patch_TakePrisonersToOwnedBed), true))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, typeof(Patch_TakePrisonersToOwnedBed).GetMethod(nameof(HaveOwnedBed)));
                    yield return new CodeInstruction(OpCodes.Brfalse, label_OriginalBranch);
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, typeof(Patch_TakePrisonersToOwnedBed).GetMethod(nameof(CanReachBed)));
                    yield return new CodeInstruction(OpCodes.Brfalse, instr.operand);
                    yield return new CodeInstruction(OpCodes.Ldnull);
                    yield return new CodeInstruction(OpCodes.Ret);

                    instr.labels.Add(label_OriginalBranch);
                }
                yield return instr;
            }
        }

        public static bool HaveOwnedBed(Pawn pawn)
        {
            return pawn.ownership != null && pawn.ownership.OwnedBed != null;
        }

        public static bool CanReachBed(Pawn pawn)
        {
            return pawn.CanReach(pawn.ownership.OwnedBed, PathEndMode.OnCell, Danger.Some);
        }
    }
}

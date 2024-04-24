using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Aki.Reflection.Patching;
using EFT;
using EFT.Interactive;
using ExpandedDoorInteractions.Helpers;

namespace ExpandedDoorInteractions.Patches
{
    public class InteractiveObjectInteractionPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return InteractionHelpers.TargetType.GetMethod("smethod_3", BindingFlags.Public | BindingFlags.Static);
        }

        [PatchPostfix]
        private static void PatchPostfix(ref object __result, GamePlayerOwner owner, WorldInteractiveObject worldInteractiveObject)
        {
            // Ignore interactions from bots
            if (InteractionHelpers.IsInteractorABot(owner))
            {
                return;
            }

            if (ExpandedDoorInteractionsPlugin.WriteMessagesForAllDoors.Value)
            {
                LoggingUtil.LogInfo("Checking available actions for object " + worldInteractiveObject.Id + "...");
            }

            if (!ExpandedDoorInteractionsPlugin.AddNewActions.Value)
            {
                return;
            }

            // Try to add the "Open Sesame" action to the door's context menu
            worldInteractiveObject.AddPeekToActionList(__result, owner);
            worldInteractiveObject.AddOpenQuietlyToActionList(__result, owner);
            // worldInteractiveObject.AddOpenFurtherToActionList(__result, owner);
        }
    }
}

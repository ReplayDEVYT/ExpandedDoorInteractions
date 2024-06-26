﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Aki.Reflection.Patching;
using EFT.Interactive;
using ExpandedDoorInteractions.Helpers;

namespace ExpandedDoorInteractions.Patches
{
    public class NoPowerTipInteractionPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return InteractionHelpers.TargetType.GetMethod("smethod_14", BindingFlags.Public | BindingFlags.Static);
        }

        [PatchPostfix]
        private static void PatchPostfix(ref object __result, NoPowerTip noPowerTip)
        {
            if (!ExpandedDoorInteractionsPlugin.AddNewActions.Value)
            {
                return;
            }

            // Try to add the "Turn On Power" action to the doors's context menu
            ExpandedDoorInteractionsPlugin.PowerSwitch.AddTurnPowerToActionList(__result);
        }
    }
}

using System;
using System.Collections;
using System.Linq;
using Comfort.Common;
using EFT.Interactive;
using EFT;
using HarmonyLib;
using Aki.Reflection.Patching;

namespace ExpandedDoorInteractions.Helpers
{
    public static class InteractionHelpers
    {
        public static Type TargetType { get; private set; } = null;

        private static Type resultType = null;
        private static Type actionType = null;

        public static void FindTypes()
        {
            // Find the class that generates the context menus for each object type
            string methodName = "GetAvailableActions";
            Type[] targetTypeOptions = Aki.Reflection.Utils.PatchConstants.EftTypes.Where(t => t.GetMethods().Any(m => m.Name.Contains(methodName))).ToArray();
            if (targetTypeOptions.Length != 1)
            {
                throw new TypeLoadException("Cannot find type containing method " + methodName);
            }

            TargetType = targetTypeOptions[0];
            LoggingUtil.LogInfo("Target type: " + TargetType.FullName);

            // Find the class containing the context menu
            resultType = AccessTools.FirstMethod(TargetType, m => m.Name.Contains(methodName)).ReturnType;
            LoggingUtil.LogInfo("Return type: " + resultType.FullName);

            // Find the class representing each action in the context menu
            actionType = AccessTools.Field(resultType, "SelectedAction").FieldType;
            LoggingUtil.LogInfo("Action type: " + actionType.FullName);
        }

        public static bool HaveTypesBeenFound()
        {
            if ((TargetType == null) || (resultType == null) || (actionType == null))
            {
                return false;
            }

            return true;
        }

        public static bool IsInteractorABot(GamePlayerOwner owner)
        {
            if (owner?.Player?.Id != Singleton<GameWorld>.Instance?.MainPlayer?.Id)
            {
                return true;
            }

            return false;
        }

        public static bool CanToggle(this WorldInteractiveObject interactiveObject)
        {
            if (!interactiveObject.Operatable)
            {
                return false;
            }

            if (interactiveObject.DoorState != EDoorState.Shut)
            {
                return false;
            }

            return true;
        }

        public static void AddPushToActionList(object actionListObject)
        {
            if (!ExpandedDoorInteractionsPlugin.AddDoNothingAction.Value)
            {
                return;
            }

            if (!HaveTypesBeenFound())
            {
                throw new TypeLoadException("Types have not been loaded");
            }

            // Create a new action to do nothing
            var newAction = Activator.CreateInstance(actionType);

            AccessTools.Field(actionType, "Name").SetValue(newAction, "Open Further");

            InteractiveObjectInteractionWrapper unlockActionWrapper = new InteractiveObjectInteractionWrapper();
            AccessTools.Field(actionType, "Action").SetValue(newAction, new Action(unlockActionWrapper.pushAction));

            AccessTools.Field(actionType, "Disabled").SetValue(newAction, false);

            // Add the new action to the context menu for the door
            IList actionList = (IList)AccessTools.Field(resultType, "Actions").GetValue(actionListObject);
            actionList.Add(newAction);
        }

        public static void AddPeekToActionList(this WorldInteractiveObject interactiveObject, object actionListObject, GamePlayerOwner owner)
        {
            // Don't do anything else unless the door is locked and requires a key
            if ((interactiveObject.DoorState != EDoorState.Shut))
            {
                return;
            }

            if (interactiveObject is LootableContainer)
            {
                return;
            }

            if (!(interactiveObject is Door))
            {
                return;
            }

            if (!HaveTypesBeenFound())
            {
                throw new TypeLoadException("Types have not been loaded");
            }

            // Create a new action to unlock the door
            var newAction = Activator.CreateInstance(actionType);

            AccessTools.Field(actionType, "Name").SetValue(newAction, "Peek");

            InteractiveObjectInteractionWrapper unlockActionWrapper = new InteractiveObjectInteractionWrapper(interactiveObject, owner);
            AccessTools.Field(actionType, "Action").SetValue(newAction, new Action(unlockActionWrapper.peekAction));

            AccessTools.Field(actionType, "Disabled").SetValue(newAction, !interactiveObject.Operatable);

            // Add the new action to the context menu for the door
            IList actionList = (IList)AccessTools.Field(resultType, "Actions").GetValue(actionListObject);
            actionList.Add(newAction);
        }

        public static void AddTurnPowerToActionList(this WorldInteractiveObject interactiveObject, object actionListObject)
        {
            if (!HaveTypesBeenFound())
            {
                throw new TypeLoadException("Types have not been loaded");
            }

            // Create a new action to turn on the power switch
            var newAction = Activator.CreateInstance(actionType);

            AccessTools.Field(actionType, "Name").SetValue(newAction, "Nothing");

            InteractiveObjectInteractionWrapper turnOnPowerActionWrapper = new InteractiveObjectInteractionWrapper(ExpandedDoorInteractionsPlugin.PowerSwitch);
            AccessTools.Field(actionType, "Action").SetValue(newAction, new Action(turnOnPowerActionWrapper.turnAction));

            AccessTools.Field(actionType, "Disabled").SetValue(newAction, !ExpandedDoorInteractionsPlugin.PowerSwitch.CanToggle());

            // Add the new action to the context menu for the door
            IList actionList = (IList)AccessTools.Field(resultType, "Actions").GetValue(actionListObject);
            actionList.Add(newAction);
        }

        internal sealed class InteractiveObjectInteractionWrapper
        {
            public GamePlayerOwner owner;
            public WorldInteractiveObject interactiveObject;

            public InteractiveObjectInteractionWrapper()
            {
            }

            public InteractiveObjectInteractionWrapper(WorldInteractiveObject _interactiveObject) : this()
            {
                interactiveObject = _interactiveObject;
            }

            public InteractiveObjectInteractionWrapper(WorldInteractiveObject _interactiveObject, GamePlayerOwner _owner) : this(_interactiveObject)
            {
                owner = _owner;
            }

            internal void pushAction()
            {
                if ((interactiveObject.DoorState != EDoorState.Shut))
                {
                    return;
                }

                var gstruct = Door.Interact(this.owner.Player, EInteractionType.Open);
                owner.Player.CurrentManagedState.ExecuteDoorInteraction(interactiveObject, gstruct.Value, null, owner.Player);
            }

            internal void peekAction()
            {
                if (interactiveObject == null)
                {
                    LoggingUtil.LogError("Cannot unlock and open a null object");
                    return;
                }

                if (owner == null)
                {
                    LoggingUtil.LogError("A GamePlayerOwner must be defined to unlock and open object " + interactiveObject.Id);
                    return;
                }

                if (ExpandedDoorInteractionsPlugin.WriteMessagesWhenUnlockingDoors.Value)
                {
                    LoggingUtil.LogInfo("Unlocking interactive object " + interactiveObject.Id + " which requires key " + interactiveObject.KeyId + "...");
                }

                // Do not open lootable containers like safes, cash registers, etc.
                if ((interactiveObject as LootableContainer) != null)
                {
                    return;
                }

                if (ExpandedDoorInteractionsPlugin.WriteMessagesWhenUnlockingDoors.Value)
                {
                    LoggingUtil.LogInfo("Opening interactive object " + interactiveObject.Id + "...");
                }

                owner.Player.MovementContext.ResetCanUsePropState();

                // Open the door
                var openangleold = interactiveObject.OpenAngle;
                var openanglepeek = interactiveObject.OpenAngle / 3;
                var gstruct = Door.Interact(this.owner.Player, EInteractionType.Open);

                interactiveObject.OpenAngle = openanglepeek;
                owner.Player.CurrentManagedState.ExecuteDoorInteraction(interactiveObject, gstruct.Value, null, owner.Player);
                interactiveObject.OpenAngle = openangleold;
            }

            internal void turnAction()
            {
                LoggingUtil.LogInfo("This has been disabled");
            }
        }
    }
}
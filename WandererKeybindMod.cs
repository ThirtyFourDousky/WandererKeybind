using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HUD;
using ImprovedInput;
using MonoMod.RuntimeDetour;
using Rain_World_Drought.Slugcat;
using UnityEngine;

namespace WandererKeybind
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class WandererKeybindMod : BaseUnityPlugin
    {
        //basic information
        public const string PLUGIN_GUID = "wandererkeybind";
        public const string PLUGIN_NAME = "Wanderer Keybind";
        public const string PLUGIN_VERSION = "1.0.1";

        public static ManualLogSource Log;

        //Keybind
        
        public static PlayerKeybind WandererPulse;

        private void OnEnable()
        {
            On.RainWorld.PostModsInit += RainWorld_PostModsInit;

            Log = base.Logger;

            Drought.hook_DoAbilities = new Hook(
                typeof(PlayerHK).GetMethod("DoAbilities", Drought.PlayerHK_flags),
                typeof(WandererKeybindMod).GetMethod("PlayerHK_DoAbilities", Drought.PlayerHK_Hflags));

            WandererPulse.HideConfig = false;
        }

        private void RainWorld_PostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
        {
            orig(self);
            WandererPulse = PlayerKeybind.Register("drought:wandererpulse", "Rain World Drought", "Pulse Jump", KeyCode.C, KeyCode.None);
        }

        private static void PlayerHK_DoAbilities(Drought.orig_DoAbilities orig, Player self)
        {
            if (self.IsKeyBound(WandererPulse))
            {
                if (!WandererSupplement.IsWanderer(self))
                {
                    return;
                }
                WandererSupplement sub = WandererSupplement.GetSub(self);
                PlayerHK.UpdateStoredBoostDirection(self, sub);
                switch (sub.abilityState)
                {
                    case WandererSupplement.AbilityState.Idle:
                        self.standStillOnMapButton = sub.HeldMapForLongEnough;
                        if (self.input[0].mp)
                        {
                            sub.mapHeld++;
                            break;
                        }
                        if (self.IsPressed(WandererPulse))
                        {
                            sub.mapHeld = 0;
                            if (sub.energy == 0 && !PlayerHK.RechargeFocus(sub))
                            {
                                FocusMeter.ShowMeter(sub.self, 30);
                                FocusMeter.DenyAnimation(sub.self);
                            }
                            else
                            {
                                sub.abilityState = WandererSupplement.AbilityState.Focus;
                                sub.focusLeft = 60;
                            }
                        }
                        sub.mapHeld = 0;
                        break;
                    case WandererSupplement.AbilityState.Focus:
                        {
                            bool panicSlowdown = false;
                            bool flag = false;
                            int num = 1073741823;
                            if (self.bodyChunks[0].ContactPoint.y != -1 && self.bodyChunks[1].ContactPoint.y != -1 && !PlayerHK.IsGrounded(self, feetMustBeGrounded: false))
                            {
                                flag = true;
                            }
                            if (sub.wantToParry == 0)
                            {
                                Parryable.PanicProjectile(sub, out var ticksUntilContact, out var inDanger);
                                if (inDanger)
                                {
                                    flag = true;
                                    panicSlowdown = true;
                                    num = ticksUntilContact;
                                }
                            }
                            if (sub.focusLeft == 0 && PlayerHK.IsGrounded(self, feetMustBeGrounded: true))
                            {
                                PlayerHK.ConsumeFocusPip(sub);
                                sub.abilityState = WandererSupplement.AbilityState.Idle;
                            }
                            if (flag)
                            {
                                sub.panicSlowdown = panicSlowdown;
                                sub.ticksUntilPanicHit = num;
                                sub.slowdownLeft = 20;
                                sub.abilityState = WandererSupplement.AbilityState.Slowdown;
                            }
                            break;
                        }
                    case WandererSupplement.AbilityState.Slowdown:
                        if (PlayerHK.IsGrounded(self, feetMustBeGrounded: true) && sub.slowdownLeft == 0)
                        {
                            PlayerHK.ExitSlowdown(sub);
                            PlayerHK.ExitFocus(sub);
                            sub.abilityState = WandererSupplement.AbilityState.Idle;
                        }
                        if (self.input[0].jmp && !self.input[1].jmp && (sub.energy > 0 || PlayerHK.RechargeFocus(sub)) && sub.jumpsSinceGrounded < sub.maxExtraJumps + 1 && self.canWallJump == 0)
                        {
                            PlayerHK.FocusJump(self);
                            PlayerHK.ExitSlowdown(sub);
                            PlayerHK.ExitFocus(sub);
                            sub.jumpForbidden = 3;
                            sub.abilityState = WandererSupplement.AbilityState.ChainJump;
                        }
                        break;
                    case WandererSupplement.AbilityState.ChainJump:
                        if (PlayerHK.IsGrounded(self, feetMustBeGrounded: true))
                        {
                            sub.abilityState = WandererSupplement.AbilityState.Idle;
                        }
                        if (sub.jumpsSinceGrounded < sub.maxExtraJumps + 1 && self.input[0].jmp && !self.input[1].jmp && (sub.energy > 0 || PlayerHK.RechargeFocus(sub)) && self.canWallJump == 0)
                        {
                            PlayerHK.FocusJump(self);
                            PlayerHK.ExitFocus(sub);
                            sub.jumpForbidden = 3;
                        }
                        if (sub.jumpsSinceGrounded == sub.maxExtraJumps + 1)
                        {
                            sub.focusLeft = 0;
                        }
                        break;
                }
                FocusMeter.UpdateFocus(sub.self, sub.energy);
                if (sub.abilityState != 0)
                {
                    FocusMeter.ShowMeter(sub.self, 30);
                }
            }
            else
            {
                orig(self);
            }
        }
    }

    public static class Drought
    {
        public static BindingFlags PlayerHK_flags = BindingFlags.Static | BindingFlags.NonPublic;
        public static BindingFlags PlayerHK_Hflags = BindingFlags.Static | BindingFlags.NonPublic;

        public static Hook hook_DoAbilities;

        public delegate void orig_DoAbilities(Player self);
    }
}

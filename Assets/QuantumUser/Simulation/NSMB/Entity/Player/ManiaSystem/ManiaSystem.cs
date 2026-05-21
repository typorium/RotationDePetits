
using UnityEngine;
using Quantum;
using System.Linq;
using System.Runtime.CompilerServices;
using System.IO;
using Photon.Deterministic;


namespace Quantum {

    public unsafe class ManiaSystem : SystemMainThreadEntityFilter<MarioPlayer, ManiaSystem.Filter>, ISignalOnGameStarting {

        private const int _randomMinimum = 4;
        private const int _randomMaximum = 20;

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public MarioPlayer* MarioPlayer;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
            public Freezable* Freezable;

            public Input Inputs;
        }

        public void OnGameStarting(Frame f) {

            // Random
            if (f.Global->Rules.TimerUntilMania == 0) {
                f.Global->ManiaPowerupTimer = f.RNG->NextInclusive(_randomMinimum, _randomMaximum);
            }

            // Timer défini
            else {
                f.Global->ManiaPowerupTimer = f.Global->Rules.TimerUntilMania * 10;
            }
        }

        public override void Update(Frame f) {

            // Cooldown
            f.Global->ManiaPowerupTimer -= f.DeltaTime;

            // Reset cooldown
            if (f.Global->ManiaPowerupTimer <= 0) {

                // Random
                if (f.Global->Rules.TimerUntilMania == 0) {
                    f.Global->ManiaPowerupTimer = f.RNG->NextInclusive(_randomMinimum, _randomMaximum);
                }
                
                // Timer prédéfini
                else {
                    while (f.Global->ManiaPowerupTimer <= 0) {
                        f.Global->ManiaPowerupTimer += f.Global->Rules.TimerUntilMania * 10;
                    }
                }

                // Original update
                base.Update(f);

            }

        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            HandlePowerup(f, ref filter);
        }

        private void HandlePowerup(Frame f, ref Filter filter) {

            // Get mario player
            var mario = filter.MarioPlayer;
            var physics = f.FindAsset(mario->PhysicsAsset);
            var oldState = mario->CurrentPowerupState;

            // Get random powerup
            GamemodeAsset gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            int itempoolCount = gamemode.AllCoinItems.Count();
            int itemIndex = f.RNG->Next(0, itempoolCount + 1); // We add one, so we can also have an option for "no powerup".

            // No powerup (little mario)
            var wasMegaMushroom = mario->CurrentPowerupState == PowerupState.MegaMushroom;
            SetPowerupState(mario, PowerupState.NoPowerup);
            mario->InvincibilityFrames = 0;

            if (itemIndex == itempoolCount) {
                if (oldState != PowerupState.NoPowerup) {
                    ResetFreezeSize(f, filter.Freezable, physics.IceBlockSmallSize);
                }
                return;
            }

            // Etoile
            PowerupAsset item = f.FindAsset(gamemode.AllCoinItems[itemIndex]) as PowerupAsset;
            if (item is StarmanPowerupAsset) {
                SetPowerup(f, filter.Entity, item);
                mario->InvincibilityFrames = (ushort)(f.UpdateRate * f.Global->ManiaPowerupTimer);
            }

            // Mega Mushroom
            if (wasMegaMushroom) {
                ResetMegaMushroomMario(mario, filter.PhysicsObject);
            }
            if (item is MegaMushroomPowerupAsset) {
                SetPowerup(f, filter.Entity, item);
                if (wasMegaMushroom) {
                    mario->MegaMushroomStartFrames = 0;
                    mario->MegaMushroomFrames = (ushort) (f.UpdateRate * f.Global->ManiaPowerupTimer);
                }
                else {
                    mario->MegaMushroomStartFrames = (byte) (MegaMushroomPowerupAsset.GrowAnimationDuration * f.UpdateRate);
                    mario->MegaMushroomFrames = (ushort) (f.UpdateRate * f.Global->ManiaPowerupTimer - mario->MegaMushroomStartFrames);
                }
            }

            // Autres powerups
            if (item.State == oldState) {
                return;
            }
            SetPowerup(f, filter.Entity, item);

            // Reset ice size
            if (oldState == PowerupState.NoPowerup) {
                ResetFreezeSize(f, filter.Freezable, physics.IceBlockBigSize);
            }

        }

        private void ResetFreezeSize(Frame f, Freezable* freezable, FPVector2 size) {

            if (! (freezable->IsFrozen(f)) ) {
                return;
            }

            freezable->IceBlockSize = size;
        }

        private void SetPowerup(Frame f, EntityRef entity, PowerupAsset item) {
            item.Collect(f, entity);
        }

        private void ResetMegaMushroomMario(MarioPlayer* mario, PhysicsObject* physicsObject) {
            mario->MegaMushroomStartFrames = 0;
            mario->MegaMushroomFrames = 0;
            mario->MegaMushroomEndFrames = 0;
            mario->MegaMushroomStationaryEnd = false;

            mario->DamageInvincibilityFrames = Constants.DamageInvincibilityFrames;
            physicsObject->Velocity = FPVector2.Zero;
            physicsObject->IsFrozen = false;
        }

        private void ResetMario(MarioPlayer* mario) {
            mario->IsPropellerFlying = false;
            mario->UsedPropellerThisJump = false;
            mario->IsDrilling &= mario->IsSpinnerFlying;
            mario->PropellerLaunchFrames = 0;
            mario->IsInShell = false;

        }

        private void SetPowerupState(MarioPlayer* mario, PowerupState state) {
            mario->PreviousPowerupState = mario->CurrentPowerupState;
            mario->CurrentPowerupState = state;
            ResetMario(mario);
        }
    }
}
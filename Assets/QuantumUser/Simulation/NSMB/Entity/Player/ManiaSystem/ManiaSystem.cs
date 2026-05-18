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

            // Original update
            base.Update(f);

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
            }

        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {

            // Checks cooldown. If timer is up, we give a random powerup.
            if (f.Global->ManiaPowerupTimer <= 0) {
                HandlePowerup(f, ref filter);
            }

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
            if (itemIndex == itempoolCount) {
                if (oldState != PowerupState.NoPowerup) {
                    SetPowerupState(mario, PowerupState.NoPowerup);
                    ResetFreezeSize(f, filter.Freezable, physics.IceBlockSmallSize);
                }
                return;
            }

            // Other powerup
            PowerupAsset item = f.FindAsset(gamemode.AllCoinItems[itemIndex]) as PowerupAsset;
            if (item.State == oldState) {
                return;
            }
            SetPowerupState(mario, item.State);
            item.OnCollected(f, filter.Entity);

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

        private void SetPowerupState(MarioPlayer* mario, PowerupState state) {
            mario->PreviousPowerupState = mario->CurrentPowerupState;
            mario->CurrentPowerupState = state;
            mario->IsPropellerFlying = false;
            mario->UsedPropellerThisJump = false;
            mario->IsDrilling &= mario->IsSpinnerFlying;
            mario->PropellerLaunchFrames = 0;
            mario->IsInShell = false;
        }
    }
}
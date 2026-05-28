
using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;


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

        private int GetTotalWeights(Frame f, AssetRef<CoinItemAsset>[] allItems) {
            int sum = 0;

            // All items
            foreach (AssetRef<CoinItemAsset> itemRef in allItems) {
                sum += GetWeight(f.FindAsset(itemRef) as PowerupAsset);
            }

            // No powerup
            sum += GetWeight(null);

            return sum;
        }

        private int GetWeight(PowerupAsset powerup) {

            // No powerup
            int weight = -1;
            if (powerup == null) {
                weight = 3;
                return weight;
            }

            // Normal powerups
            switch (powerup.State) {
                case PowerupState.Mushroom:
                case PowerupState.FireFlower:
                case PowerupState.BlueShell:
                case PowerupState.IceFlower:
                case PowerupState.PropellerMushroom:
                case PowerupState.HammerSuit:
                case PowerupState.MiniMushroom:
                    weight = 3;
                    break;
            }

            if (weight != -1) {
                return weight;
            }

            // Special / Edgecases Powerups
            if (powerup is StarmanPowerupAsset) {
                weight = 1;
                return weight;
            }

            return weight;
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
            var marioPhysics = f.FindAsset(mario->PhysicsAsset);
            var marioOldState = mario->CurrentPowerupState;

            // Get random powerup
            AssetRef<CoinItemAsset>[] items = f.FindAsset(f.Global->Rules.Gamemode).AllCoinItems;

            int totalWeight = GetTotalWeights(f, items);
            int currentWeight = f.RNG->Next(0, totalWeight);

            PowerupAsset rolledItem = null;
            foreach (AssetRef<CoinItemAsset> item in items) {
                rolledItem = f.FindAsset(item) as PowerupAsset;
                currentWeight -= GetWeight(rolledItem);
                if (currentWeight < 0) {
                    break;
                }
            }

            if (currentWeight >= 0) {
                rolledItem = null; // Null is for no powerup
            }

            // No powerup
            if (rolledItem == null) {
                if (marioOldState != PowerupState.NoPowerup) {
                    SetPowerupState(mario, PowerupState.NoPowerup);
                    ResetFreezeSize(f, filter.Freezable, marioPhysics.IceBlockSmallSize);
                }
                return;
            }

            // Etoile
            else if (rolledItem is StarmanPowerupAsset) {
                SetPowerupState(mario, PowerupState.NoPowerup);
                SetPowerup(f, filter.Entity, rolledItem);
                mario->InvincibilityFrames = (ushort)(f.UpdateRate * f.Global->ManiaPowerupTimer);
                return;
            }

            // Autres powerups
            else if (rolledItem.State == marioOldState) {
                return;
            }
            SetPowerup(f, filter.Entity, rolledItem);

            // Reset ice size
            if (marioOldState == PowerupState.NoPowerup && mario->CurrentPowerupState != marioOldState) {
                ResetFreezeSize(f, filter.Freezable, marioPhysics.IceBlockBigSize);
            }

        }

        private void ResetFreezeSize(Frame f, Freezable* freezable, FPVector2 size) {

            if (! freezable->IsFrozen(f) ) {
                return;
            }
            freezable->IceBlockSize = size;
        }

        private void SetPowerup(Frame f, EntityRef entity, PowerupAsset item) {
            item.Collect(f, entity);
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
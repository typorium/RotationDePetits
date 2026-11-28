
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

    public unsafe partial class ManiaSystem : SystemMainThreadEntityFilter<MarioPlayer, ManiaSystem.Filter>, ISignalOnGameStarting {

        private const int _randomMinimum = 1;
        private const int _randomMaximum = 30;

        private const int _secondInterval = 5;

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public MarioPlayer* MarioPlayer;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
            public Freezable* Freezable;

            public Input Inputs;
        }

        private void ResetManiaTimer(Frame f) {

            // If timer is random
            if (f.Global->Rules.ManiaTime == 0) {
                f.Global->ShouldMania = f.RNG->NextInclusive(_randomMinimum, _randomMaximum);
                return;
            }

            // If timer is defined
            f.Global->ShouldMania = f.Global->Rules.ManiaTime * _secondInterval;
        }

        public void OnGameStarting(Frame f) {
            ResetManiaTimer(f);
        }

        public override void Update(Frame f) {

            // If the game is still starting, don't update the powerup timer yet
            if (f.Global->GameState != GameState.Playing) {
                return;
            }

            f.Global->ShouldMania -= f.DeltaTime;
            
            if (f.Global->ShouldMania <= 0) {
                ResetManiaTimer(f);
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

            // List all possible items
            var allItems = f.FindAsset(f.Global->Rules.Gamemode).AllCoinItems;
            var item = ChooseRandomPowerup(f, allItems);

            // Set powerup
            SetPowerup(f, filter.Entity, item);

            // Reset ice size
            ResetFreezeSize(f, mario, marioPhysics, filter.Freezable);

        }

        private void ResetFreezeSize(Frame f, MarioPlayer* mario, MarioPlayerPhysicsInfo marioPhysicsInfo, Freezable* freezable) {
            bool isLittle = mario->CurrentPowerupState == PowerupState.NoPowerup || mario->CurrentPowerupState == PowerupState.MiniMushroom;
            if (isLittle) {
                freezable->IceBlockSize = marioPhysicsInfo.IceBlockSmallSize;
                return;
            }
            freezable->IceBlockSize = marioPhysicsInfo.IceBlockBigSize;
        }

    }
}
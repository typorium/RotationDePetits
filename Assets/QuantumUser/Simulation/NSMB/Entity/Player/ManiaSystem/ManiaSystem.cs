using UnityEngine;
using Quantum;


namespace Quantum
{

    public unsafe class ManiaSystem : SystemMainThreadEntityFilter<MarioPlayer, ManiaSystem.Filter> {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public MarioPlayer* MarioPlayer;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
            public Freezable* Freezable;

            public Input Inputs;
        }

        public override void OnInit(Frame f) {
            base.OnInit(f);
        }

        public override void OnEnabled(Frame f) {
            base.OnEnabled(f);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {

            f.Global->ManiaPowerupTimer -= f.DeltaTime;
            if (f.Global->ManiaPowerupTimer <= 0) {
                Debug.Log("Event!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");

                while (f.Global->ManiaPowerupTimer <= 0) {
                    f.Global->ManiaPowerupTimer += f.Global->Rules.TimerUntilMania * 10;
                }
                
            }
        }
    }
}

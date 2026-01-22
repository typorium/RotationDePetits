using Photon.Deterministic;
using Quantum;

public class MegaMushroomPowerupAsset : PowerupAsset {

    public FP GrowAnimationDuration = FP._1_50;

    protected override unsafe void OnCollected(Frame f, EntityRef marioEntity) {
        var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
        var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

        mario->MegaMushroomStartFrames = (byte) (GrowAnimationDuration * f.UpdateRate);
        mario->IsSliding = false;
        mario->CurrentKnockback = KnockbackStrength.None;
        mario->KnockbackGetupFrames = 0;
        if (f.Unsafe.TryGetPointer(mario->HeldEntity, out Holdable* holdable)) {
            holdable->DropWithoutThrowing(f, mario->HeldEntity);
        }
        if (marioPhysicsObject->IsTouchingGround) {
            mario->JumpState = JumpState.None;
        }
        marioPhysicsObject->IsFrozen = true;
        marioPhysicsObject->Velocity = FPVector2.Zero;
    }
}
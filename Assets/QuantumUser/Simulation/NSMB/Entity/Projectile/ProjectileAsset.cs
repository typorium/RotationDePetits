using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;

public class ProjectileAsset : AssetObject, ISoundEffectOverrideProvider {
    public ProjectileEffectType Effect;
    public bool Bounce = true;
    public FP Speed;
    public FP BounceStrength;
    public FPVector2 Gravity;
    public bool DestroyOnSecondBounce;
    public bool DestroyOnHit = true;
    public bool LockTo45Degrees = true;
    public bool InheritShooterVelocity;
    public bool HasCollision = true;
    public bool DoesntEffectBlueShell = true;

    public ParticleEffect DestroyParticleEffect = ParticleEffect.None;
    public SoundEffect ShootSound = SoundEffect.Powerup_Fireball_Shoot;

    public SoundEffectOverride[] SfxOverrides;

    [NonSerialized] private Dictionary<SoundEffect, SoundEffectOverride> overridesDict;
    public SoundEffectOverride GetOverrideForSfx(SoundEffect sfx) {
        if (overridesDict == null) {
            overridesDict = new();
            foreach (var @override in SfxOverrides) {
                overridesDict[@override.SoundEffect] = @override;
            }
        }
        overridesDict.TryGetValue(sfx, out var result);
        return result;
    }
}

public enum ProjectileEffectType {
    Fire,
    Freeze,
    KillEnemiesAndSoftKnockbackPlayers,
}
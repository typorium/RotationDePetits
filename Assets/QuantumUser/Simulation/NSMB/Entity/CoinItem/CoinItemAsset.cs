using Photon.Deterministic;
using Quantum;

public class CoinItemAsset : AssetObject {
    public AssetRef<EntityPrototype> Prefab;
    public FP SpawnChance = FP._0_10, AboveAverageBonus = 0, BelowAverageBonus = 0;
    public SoundEffect BlockSpawnSoundEffect = SoundEffect.World_Block_Powerup;
    public bool CustomPowerup, LivesOnlyPowerup;
    public bool CanSpawnFromBlock = true;
    public bool OnlyOneCanExist = false;

    public FPVector2 CameraSpawnOffset = new(0, FP.FromString("1.68"));

}
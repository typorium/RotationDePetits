namespace Quantum
{
    public unsafe partial class ManiaSystem
    {

        private int GetWeight(PowerupAsset powerup) {

            // No powerup
            if (powerup == null) {
                return 5;
            }

            // Special / Edgecases Powerups
            if (powerup is StarmanPowerupAsset) {
                return 1;
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
                    return 5;
                default:
                    break;
            }

            // Shouldn't be here, but give it a weight of 0 just in case
            return 0;
        }

        private int GetAllWeights(Frame f, AssetRef<CoinItemAsset>[] allItems) {

            // All items
            int sum = 0;
            foreach (AssetRef<CoinItemAsset> itemRef in allItems) {
                sum += GetWeight(f.FindAsset(itemRef) as PowerupAsset);
            }

            // No powerup
            sum += GetWeight(null);
            return sum;

        }

        private PowerupAsset ChooseRandomPowerup(Frame f, AssetRef<CoinItemAsset>[] allItems) {

            int totalWeight = GetAllWeights(f, allItems);
            int currentWeight = f.RNG->Next(0, totalWeight);

            PowerupAsset rolledItem = null;
            foreach (AssetRef<CoinItemAsset> item in allItems) {
                rolledItem = f.FindAsset(item) as PowerupAsset;
                currentWeight -= GetWeight(rolledItem);
                if (currentWeight < 0) {
                    break;
                }
            }

            if (currentWeight >= 0) {
                rolledItem = null; // Null is for no powerup
            }

            return rolledItem;
        }


    }
}

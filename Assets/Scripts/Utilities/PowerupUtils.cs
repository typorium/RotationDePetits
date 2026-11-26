using Quantum;

namespace NSMB.Utilities {

    public static class PowerupUtils {

        public unsafe static string GetPowerupName(MarioPlayer* mario) {

            var isStarman = mario->InvincibilityFrames > 0;
            if (isStarman) {
                return "Starman";
            }

            return GetPowerupNameFromPowerupState(mario->CurrentPowerupState);
        }

        public unsafe static string GetPowerupNameFromProjectile(Projectile* projectile) {
            return GetPowerupNameFromPowerupState(projectile->ProjectileType);
        }

        public unsafe static string GetPowerupNameFromPowerupState(PowerupState state) {
            return state.ToString();
        }
    }
}
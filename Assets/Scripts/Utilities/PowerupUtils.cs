using Quantum;

namespace NSMB.Utilities {

    public static class PowerupUtils {

        public unsafe static string GetOnMarioInteractionPowerupName(MarioPlayer* mario) {

            var isStarman = mario->InvincibilityFrames > 0;
            if (isStarman) {
                return "Starman";
            }

            return GetPowerupNameFromPowerupState(mario->PowerupStateOnMarioInteraction);
        }

        public unsafe static string GetCurrentPowerupName(MarioPlayer* mario) {

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
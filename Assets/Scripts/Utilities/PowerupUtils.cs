using Quantum;
using UnityEngine;

namespace NSMB.Utilities {

    public static class PowerupUtils {

        public unsafe static string GetPowerupName(Frame f, MarioPlayer* mario) {

            var isStarman = mario->InvincibilityFrames > 0;
            if (isStarman) {
                return "Starman";
            }

            return mario->CurrentPowerupState.ToString();
        }
    }
}
using UnityEngine;

namespace Quantum
{
    public unsafe partial class ManiaSystem
    {

        private void ResetMario(Frame f, MarioPlayer* mario) {
            mario->IsPropellerFlying = false;
            mario->UsedPropellerThisJump = false;
            mario->IsDrilling &= mario->IsSpinnerFlying;
            mario->PropellerLaunchFrames = 0;
            mario->IsInShell = false;
        }

        private void SetPowerupState(MarioPlayer* mario, PowerupState state) {
            mario->PreviousPowerupState = mario->CurrentPowerupState;
            mario->CurrentPowerupState = state;
        }

        private void SetMarioAsStarman(Frame f, EntityRef marioEntity, MarioPlayer* mario) {
            var duration = f.UpdateRate * f.Global->ShouldMania;
            mario->InvincibilityFrames = (ushort) duration;

            f.Signals.OnMarioPlayerBecameInvincible(marioEntity);

        }

        public void SetPowerup(Frame f, EntityRef marioEntity, PowerupAsset powerup) {

            // Get mario player
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);

            // Small mario / no powerup
            if (powerup == null) {
                ResetMario(f, mario);
                SetPowerupState(mario, PowerupState.NoPowerup);
                return;
            }

            // Starman
            if (powerup is StarmanPowerupAsset) {
                ResetMario(f, mario);
                SetMarioAsStarman(f, marioEntity, mario);
                SetPowerupState(mario, PowerupState.NoPowerup);
                return;
            }

            // Check if old powerup isn't the same as the new one
            if (powerup.State == mario->CurrentPowerupState) {
                return;
            }

            // All other powerups
            ResetMario(f, mario);
            SetPowerupState(mario, powerup.State);

        }
    }
}

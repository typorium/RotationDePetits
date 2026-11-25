using Photon.Deterministic;
using System;

namespace Quantum {
    public unsafe class SupermaniaGamemode : GamemodeAsset {

        public AssetRef<EntityPrototype> BigStarPrototype;

        public override void EnableGamemode(Frame f) {
        }

        public override void DisableGamemode(Frame f) {
        }

        public override void OnReturnToRoom(Frame f) {
        }

        public override int GetObjectiveCount(Frame f, MarioPlayer* mario) {
            // No objectives for this gamemode
            return 0;
        }

        public override FP GetItemSpawnWeight(Frame f, CoinItemAsset item, int ourObjectiveCount) {
            // No item for this gamemode
            return 0;
        }

        public override void CheckForGameEnd(Frame f) {

            // End Condition: only one team alive
            Span<int> objectiveCounts = stackalloc int[Constants.MaxPlayers];
            GetAllTeamsObjectiveCounts(f, objectiveCounts);

            int aliveTeamCount = 0;
            int aliveTeam = -1;
            for (int i = 0; i < objectiveCounts.Length; i++) {
                if (objectiveCounts[i] > -1) {
                    aliveTeamCount++;
                    aliveTeam = i;
                }
            }

            if (aliveTeamCount <= 1) {
                if (aliveTeam == -1) {
                    // It's a draw
                    GameLogicSystem.EndGame(f, false, null);
                    return;
                } else if (f.Global->RealPlayers > 1) {
                    // <team> wins, assuming more than 1 player
                    // so the player doesn't insta-win in a solo game.
                    GameLogicSystem.EndGame(f, false, aliveTeam);
                    return;
                }
            }

            // End Condition: timer expires
            if (f.Global->Rules.IsTimerEnabled && f.Global->Timer <= 0) {
                GameLogicSystem.EndGame(f, false, null);
                return;
            }
        }
    }
}

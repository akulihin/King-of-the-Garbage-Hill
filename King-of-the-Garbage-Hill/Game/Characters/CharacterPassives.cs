
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class CharacterPassives : IServiceSingleton
    {
        public Task InitializeAsync() => Task.CompletedTask;

        public void HandleCharacterBeforeCalculations(GameBridgeClass player)
        {
            var characterName = player.Character.Name;
            switch (characterName)
            {
                case "DeepList":
                    HandleDeepList(player);
                    break;
                case "mylorik":
                    HandleMylorik(player);
                    break;
                case "Глеб":
                    HandleGleb(player);
                    break;
                case "LeCrisp":
                    HandleLeCrisp(player);
                    break;
                case "Толя":
                    HandleTolya(player);
                    break;
                case "HardKitty":
                    HandleHardKitty(player);
                    break;
                case "Sirinoks":
                    HandleSirinoks(player);
                    break;
                case "Mitsuki":
                    HandleMitsuki(player);
                    break;
                case "AWDKA":
                    HandleAWDKA(player);
                    break;
                case "Осьминожка":
                    HandleOctopus(player);
                    break;
                case "Darksi":
                    HandleDarksi(player);
                    break;
                case "Тигр":
                    HandleTigr(player);
                    break;
                case "Братишка":
                    HandleShark(player);
                    break;
                case "????":
                    HandleDeepList2(player);
                    break;
            }
        }
        public void HandleCharacterAfterCalculations(GameBridgeClass player)
        {
            var characterName = player.Character.Name;
            switch (characterName)
            {
                case "DeepList":
                    HandleDeepListAfter(player);
                    break;
                case "mylorik":
                    HandleMylorikAfter(player);
                    break;
                case "Глеб":
                    HandleGlebAfter(player);
                    break;
                case "LeCrisp":
                    HandleLeCrispAfter(player);
                    break;
                case "Толя":
                    HandleTolyaAfter(player);
                    break;
                case "HardKitty":
                    HandleHardKittyAfter(player);
                    break;
                case "Sirinoks":
                    HandleSirinoksAfter(player);
                    break;
                case "Mitsuki":
                    HandleMitsukiAfter(player);
                    break;
                case "AWDKA":
                    HandleAWDKAAfter(player);
                    break;
                case "Осьминожка":
                    HandleOctopusAfter(player);
                    break;
                case "Darksi":
                    HandleDarksiAfter(player);
                    break;
                case "Тигр":
                    HandleTigrAfter(player);
                    break;
                case "Братишка":
                    HandleSharkAfter(player);
                    break;
                case "????":
                    HandleDeepList2After(player);
                    break;
            }
        }

        //  private static readonly ConcurrentDictionary<ulong, ulong> DoubtfulTactic = new ConcurrentDictionary<ulong, ulong>();
        private static List<ulong> DoubtfulTactic = new List<ulong>();
        private void HandleDeepList(GameBridgeClass player)
        {
            //Doubtful tactic
            if (DoubtfulTactic.Contains(player.Status.WhoToAttackThisTurn))
            {
                //continiue
            }
            else
            {
                DoubtfulTactic.Add(player.Status.WhoToAttackThisTurn);

            }
            //end Doubtful tactic
        }

        private void HandleMylorik(GameBridgeClass player)
        {

        }

        private void HandleGleb(GameBridgeClass player)
        {

        }

        private void HandleLeCrisp(GameBridgeClass player)
        {

        }

        private void HandleTolya(GameBridgeClass player)
        {

        }

        private void HandleHardKitty(GameBridgeClass player)
        {

        }

        private void HandleSirinoks(GameBridgeClass player)
        {

        }

        private void HandleMitsuki(GameBridgeClass player)
        {

        }

        private void HandleAWDKA(GameBridgeClass player)
        {

        }

        private void HandleOctopus(GameBridgeClass player)
        {

        }

        private void HandleDarksi(GameBridgeClass player)
        {

        }

        private void HandleTigr(GameBridgeClass player)
        {

        }

        private void HandleShark(GameBridgeClass player)
        {

        }

        private void HandleDeepList2(GameBridgeClass player)
        {

        }

        //after

        private void HandleDeepListAfter(GameBridgeClass player)
        {
        }

        private void HandleMylorikAfter(GameBridgeClass player)
        {

        }

        private void HandleGlebAfter(GameBridgeClass player)
        {

        }

        private void HandleLeCrispAfter(GameBridgeClass player)
        {

        }

        private void HandleTolyaAfter(GameBridgeClass player)
        {

        }

        private void HandleHardKittyAfter(GameBridgeClass player)
        {

        }

        private void HandleSirinoksAfter(GameBridgeClass player)
        {

        }

        private void HandleMitsukiAfter(GameBridgeClass player)
        {

        }

        private void HandleAWDKAAfter(GameBridgeClass player)
        {

        }

        private void HandleOctopusAfter(GameBridgeClass player)
        {

        }

        private void HandleDarksiAfter(GameBridgeClass player)
        {

        }

        private void HandleTigrAfter(GameBridgeClass player)
        {

        }

        private void HandleSharkAfter(GameBridgeClass player)
        {

        }

        private void HandleDeepList2After(GameBridgeClass player)
        {

        }
    }
} 

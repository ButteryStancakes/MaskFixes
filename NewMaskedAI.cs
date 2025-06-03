using GameNetcodeStuff;
using UnityEngine;
using System.Linq;

namespace MaskFixes
{
    internal class NewMaskedAI
    {
        static LayerMask hideLayers = LayerMask.NameToLayer("Default") |
                                      LayerMask.NameToLayer("Room") |
                                      LayerMask.NameToLayer("Colliders") |
                                      LayerMask.NameToLayer("PlaceableShipObject") |
                                      LayerMask.NameToLayer("DecalStickableSurface");

        internal static Vector3 mainEntrancePoint, mainEntrancePointOutside;
        internal static Bounds startRoomBounds;

        internal static void HideOnShip(MaskedPlayerEnemy maskedAI)
        {
            for (int i = 0; i < StartOfRound.Instance.insideShipPositions.Length; i++)
            {
                if (Physics.Linecast(StartOfRound.Instance.shipDoorAudioSource.transform.position, StartOfRound.Instance.insideShipPositions[i].position, hideLayers, QueryTriggerInteraction.Ignore) && maskedAI.SetDestinationToPosition(StartOfRound.Instance.insideShipPositions[i].position, true))
                {
                    maskedAI.shipHidingSpot = maskedAI.destination;

                    bool spotIsNotTaken = true;
                    foreach (EnemyAI enemyAI in RoundManager.Instance.SpawnedEnemies)
                    {
                        if (!enemyAI.isEnemyDead && enemyAI is MaskedPlayerEnemy otherMasked && maskedAI != otherMasked && otherMasked.currentBehaviourStateIndex == 2 && Vector3.Distance(maskedAI.shipHidingSpot, otherMasked.shipHidingSpot) < 1.75f)
                        {
                            spotIsNotTaken = false;
                            Plugin.Logger.LogDebug($"Mimic #{maskedAI.GetInstanceID()}: Can't choose hiding spot #{i}, already claimed by mimic #{otherMasked.GetInstanceID()}");
                            break;
                        }
                    }

                    // stop checking other spots, this one is fine
                    if (spotIsNotTaken)
                    {
                        Plugin.Logger.LogDebug($"Mimic #{maskedAI.GetInstanceID()}: Claimed hiding spot #{i} at coords {maskedAI.shipHidingSpot}");
                        return;
                    }
                }
            }

            maskedAI.shipHidingSpot = StartOfRound.Instance.insideShipPositions[Random.Range(0, StartOfRound.Instance.insideShipPositions.Length)].position;
            Plugin.Logger.LogDebug($"Mimic #{maskedAI.GetInstanceID()}: Fall back to position at coords {maskedAI.shipHidingSpot}");
        }

        internal static void Roam(MaskedPlayerEnemy maskedAI)
        {
            maskedAI.LookAndRunRandomly(true);

            // only continue roaming if there is not a player to chase
            PlayerControllerB playerInLineOfSight = maskedAI.CheckLineOfSightForClosestPlayer();
            if (playerInLineOfSight != null)
            {
                maskedAI.LookAtPlayerServerRpc((int)playerInLineOfSight.playerClientId);
                maskedAI.SetMovingTowardsTargetPlayer(playerInLineOfSight);
                maskedAI.SwitchToBehaviourState(1);
                return;
            }

            // if close to ship and have wandered for a while, go hide
            maskedAI.interestInShipCooldown += maskedAI.AIIntervalTime;
            if (maskedAI.isOutside && maskedAI.interestInShipCooldown >= 17f && Vector3.Distance(maskedAI.transform.position, StartOfRound.Instance.elevatorTransform.position) < 22f)
            {
                maskedAI.SwitchToBehaviourState(2);
                return;
            }

            // otherwise, roam!

            // get the closest players inside and outside the building
            PlayerControllerB closestInsidePlayer = null, closestOutsidePlayer = null;
            float inDist = float.MaxValue, outDist = float.MaxValue;
            for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i ++)
            {
                if (StartOfRound.Instance.allPlayerScripts[i] == null || StartOfRound.Instance.allPlayerScripts[i].isPlayerDead || !StartOfRound.Instance.allPlayerScripts[i].isPlayerControlled)
                    continue;

                if (StartOfRound.Instance.allPlayerScripts[i].isInsideFactory)
                {
                    // validate that inside players are targetable
                    if (StartOfRound.Instance.allPlayerScripts[i].inAnimationWithEnemy != null)
                        continue;

                    maskedAI.tempDist = Vector3.Distance(maskedAI.transform.position, StartOfRound.Instance.allPlayerScripts[i].transform.position);
                    if (maskedAI.tempDist < inDist)
                    {
                        // can't reach this player
                        /*if (!maskedAI.isOutside && !CanPathToPoint(maskedAI, StartOfRound.Instance.allPlayerScripts[i].transform.position))
                            continue;*/

                        inDist = maskedAI.tempDist;
                        closestInsidePlayer = StartOfRound.Instance.allPlayerScripts[i];
                    }
                }
                else
                {
                    maskedAI.tempDist = Vector3.Distance(maskedAI.transform.position, StartOfRound.Instance.allPlayerScripts[i].transform.position);
                    if (maskedAI.tempDist < outDist)
                    {
                        outDist = maskedAI.tempDist;
                        closestOutsidePlayer = StartOfRound.Instance.allPlayerScripts[i];
                    }
                }
            }

            // assign some values back to the original script
            maskedAI.mostOptimalDistance = Mathf.Min(outDist, inDist, 2000f);
            if (RoundManager.Instance.currentDungeonType == 4 && maskedAI.elevatorScript == null)
                maskedAI.elevatorScript = RoundManager.Instance.currentMineshaftElevator;

            // default to wandering outside, since we can hide on the ship
            // only go inside if:
            // - there is a player inside, and...
            // - we are already inside, or there are no players to target outside
            bool wantsToBeInside = closestInsidePlayer != null && (!maskedAI.isOutside || closestOutsidePlayer == null);

            // before normal pathing can continue, make sure that we are on the right side of the elevator
            if (!UseElevatorIfNeeded(maskedAI, wantsToBeInside, closestInsidePlayer))
            {
                if (maskedAI.searchForPlayers.inProgress)
                    maskedAI.StopSearch(maskedAI.searchForPlayers);

                return;
            }

            // if we are on the wrong side and have a valid path, walk to entrance and use as soon as possible
            // otherwise, wander randomly until things change
            Vector3 teleport = maskedAI.isOutside ? mainEntrancePointOutside : mainEntrancePoint;
            if (wantsToBeInside == maskedAI.isOutside && Time.realtimeSinceStartup - maskedAI.timeAtLastUsingEntrance > 3f && CanPathToPoint(maskedAI, teleport))
            {
                // teleport if in range of doors
                if (Vector3.Distance(maskedAI.transform.position, teleport) < 1f)
                {
                    maskedAI.TeleportMaskedEnemyAndSync(maskedAI.isOutside ? mainEntrancePoint : mainEntrancePointOutside, !maskedAI.isOutside);
                    return;
                }

                // path to the doors
                if (maskedAI.searchForPlayers.inProgress)
                    maskedAI.StopSearch(maskedAI.searchForPlayers);

                maskedAI.SetDestinationToPosition(teleport);
            }
            else if (!maskedAI.searchForPlayers.inProgress)
                maskedAI.StartSearch(maskedAI.transform.position, maskedAI.searchForPlayers);
        }

        static bool CanPathToPoint(MaskedPlayerEnemy maskedAI, Vector3 pos)
        {
            maskedAI.pathDistance = 0f; // for consistency with vanilla

            if (maskedAI.agent.isOnNavMesh && !maskedAI.agent.CalculatePath(pos, maskedAI.path1))
                return false;

            if (maskedAI.path1 == null || maskedAI.path1.corners.Length == 0)
                return false;

            if (Vector3.Distance(maskedAI.path1.corners[^1], RoundManager.Instance.GetNavMeshPosition(pos, RoundManager.Instance.navHit, 2.7f, maskedAI.agent.areaMask)) > 1.5f)
                return false;

            return true;
        }

        // return true if elevator is no longer needed
        // return false (delaying pathfinding) if need to take elevator still
        static bool UseElevatorIfNeeded(MaskedPlayerEnemy maskedAI, bool wantsToBeInside, PlayerControllerB closestInsidePlayer)
        {
            if (maskedAI.isOutside || maskedAI.elevatorScript == null)
                return true;

            bool ridingElevator = Vector3.Distance(maskedAI.transform.position, maskedAI.elevatorScript.elevatorInsidePoint.position) < 1f;

            // if we're already in the elevator, wait until it is done moving
            if (ridingElevator && (!maskedAI.elevatorScript.elevatorFinishedMoving || !maskedAI.elevatorScript.elevatorDoorOpen))
            {
                //maskedAI.SetDestinationToPosition(maskedAI.elevatorScript.elevatorInsidePoint.position);
                return false;
            }

            maskedAI.isInElevatorStartRoom = startRoomBounds.Contains(maskedAI.transform.position);
            Vector3 elevatorOutsidePoint = maskedAI.isInElevatorStartRoom ? maskedAI.elevatorScript.elevatorTopPoint.position : maskedAI.elevatorScript.elevatorBottomPoint.position;

            // if trying to go outside, we need to be in the start room
            bool needToUseElevator = !wantsToBeInside && !maskedAI.isInElevatorStartRoom;
            // otherwise, check if we can target someone where we are
            if (!needToUseElevator && closestInsidePlayer != null)
            {
                bool closestPlayerAtTop = startRoomBounds.Contains(closestInsidePlayer.transform.position);
                if (maskedAI.isInElevatorStartRoom != closestPlayerAtTop)
                    needToUseElevator = true;
            }

            // don't need to use elevator, and we're not on the elevator, so all is good
            if (!needToUseElevator && !ridingElevator)
                return true;

            // are we already in the elevator?
            if (ridingElevator)
            {
                // elevator is in the wrong position, need to press the button
                if (needToUseElevator)
                {
                    // elevator is in the wrong position, need to press the button
                    if (maskedAI.elevatorScript.elevatorMovingDown != maskedAI.isInElevatorStartRoom)
                        maskedAI.elevatorScript.PressElevatorButtonOnServer(true);

                    return false;
                }

                // otherwise, try to get off the elevator
                if (!needToUseElevator && maskedAI.elevatorScript.elevatorFinishedMoving && maskedAI.elevatorScript.elevatorDoorOpen)
                {
                    if (CanPathToPoint(maskedAI, elevatorOutsidePoint))
                        maskedAI.SetDestinationToPosition(elevatorOutsidePoint);

                    return false;
                }
            }

            if (needToUseElevator)
            {
                // if elevator is here, get on board
                if (maskedAI.elevatorScript.elevatorFinishedMoving && maskedAI.elevatorScript.elevatorDoorOpen && maskedAI.elevatorScript.elevatorMovingDown != maskedAI.isInElevatorStartRoom)
                {
                    if (CanPathToPoint(maskedAI, maskedAI.elevatorScript.elevatorInsidePoint.position))
                        maskedAI.SetDestinationToPosition(maskedAI.elevatorScript.elevatorInsidePoint.position);

                    return false;
                }

                // are we already standing at the elevator?
                if (Vector3.Distance(maskedAI.transform.position, elevatorOutsidePoint) < 1f)
                {
                    // need to call the elevator when we can
                    if (maskedAI.elevatorScript.elevatorMovingDown == maskedAI.isInElevatorStartRoom && !maskedAI.elevatorScript.elevatorCalled && !ridingElevator)
                        maskedAI.elevatorScript.CallElevator(!maskedAI.isInElevatorStartRoom);

                    return false;
                }

                // need to walk towards the elevator
                if (CanPathToPoint(maskedAI, elevatorOutsidePoint))
                    maskedAI.SetDestinationToPosition(elevatorOutsidePoint);

                return false;
            }

            Plugin.Logger.LogWarning("Masked elevator behavior seems to be stuck in an impossible state");
            return false;
        }
    }
}

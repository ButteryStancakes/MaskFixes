﻿using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MaskFixes
{
    [HarmonyPatch(typeof(MaskedPlayerEnemy))]
    class Patches
    {
        static Mesh TRAGEDY_MASK, TRAGEDY_MASK_LOD, TRAGEDY_EYES_FILLED;
        static Material TRAGEDY_MAT;
        static AudioClip[] TRAGEDY_RANDOM_CLIPS;

        static float localPlayerLastTeleported;

        static LayerMask hideLayers = LayerMask.NameToLayer("Default") |
                                      LayerMask.NameToLayer("Room") |
                                      LayerMask.NameToLayer("Colliders") |
                                      LayerMask.NameToLayer("PlaceableShipObject") |
                                      LayerMask.NameToLayer("DecalStickableSurface");

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Awake))]
        [HarmonyPostfix]
        static void StartOfRound_Post_Awake(StartOfRound __instance)
        {
            GameObject tragedyRagdoll = __instance.playerRagdolls?.FirstOrDefault(playerRagdoll => playerRagdoll.name == "PlayerRagdollWithTragedyMask Variant");
            if (tragedyRagdoll != null)
            {
                // cache all of the visual references to the tragedy mask (the item and enemy prefabs are broken, only the ragdoll has all the correct assets)
                foreach (MeshFilter meshFilter in tragedyRagdoll.GetComponentsInChildren<MeshFilter>())
                {
                    switch (meshFilter.name)
                    {
                        case "Mesh":
                            TRAGEDY_MASK = meshFilter.sharedMesh;
                            Plugin.Logger.LogDebug("Cached Tragedy model");
                            TRAGEDY_MAT = meshFilter.GetComponent<MeshRenderer>()?.sharedMaterial;
                            Plugin.Logger.LogDebug("Cached Tragedy material");
                            break;
                        case "ComedyMaskLOD1":
                            TRAGEDY_MASK_LOD = meshFilter.sharedMesh;
                            Plugin.Logger.LogDebug("Cached Tragedy LOD");
                            break;
                        case "EyesFilled":
                            TRAGEDY_EYES_FILLED = meshFilter.sharedMesh;
                            Plugin.Logger.LogDebug("Cached Tragedy glowing eyes");
                            break;
                    }
                }
            }
            else
                Plugin.Logger.LogWarning("Failed to find reference to Tragedy ragdoll. This will cause problems later!!");

            GameObject tragedyMask = __instance.allItemsList?.itemsList?.FirstOrDefault(item => item.name == "TragedyMask")?.spawnPrefab;
            if (tragedyMask != null)
            {
                TRAGEDY_RANDOM_CLIPS = tragedyMask.GetComponent<RandomPeriodicAudioPlayer>()?.randomClips;
                Plugin.Logger.LogDebug("Cached Tragedy random audio");
                MeshFilter maskMesh = tragedyMask.transform.Find("MaskMesh")?.GetComponent<MeshFilter>();
                if (maskMesh != null)
                {
                    MeshFilter eyesFilled = maskMesh.transform.Find("EyesFilled")?.GetComponent<MeshFilter>();
                    if (eyesFilled != null && TRAGEDY_EYES_FILLED != null)
                    {
                        eyesFilled.mesh = TRAGEDY_EYES_FILLED;
                        Plugin.Logger.LogDebug("Tragedy mask: Re-assigned glowing eyes");
                    }
                    MeshFilter maskLOD = maskMesh.transform.Find("ComedyMaskLOD1")?.GetComponent<MeshFilter>();
                    if (maskLOD != null && TRAGEDY_MASK_LOD != null)
                    {
                        maskLOD.mesh = TRAGEDY_MASK_LOD;
                        Plugin.Logger.LogDebug("Tragedy mask: Re-assigned LOD");
                    }
                }
            }
            else
                Plugin.Logger.LogWarning("Failed to find reference to Tragedy mask. This will cause problems later!!");

            EnemyType maskedPlayerEnemy = __instance.currentLevel?.Enemies?.FirstOrDefault(enemy => enemy.enemyType.name == "MaskedPlayerEnemy")?.enemyType ?? Object.FindAnyObjectByType<QuickMenuManager>()?.testAllEnemiesLevel?.Enemies?.FirstOrDefault(enemy => enemy.enemyType.name == "MaskedPlayerEnemy")?.enemyType;
            if (maskedPlayerEnemy != null)
            {
                maskedPlayerEnemy.isOutsideEnemy = false;
                Plugin.Logger.LogDebug("Masked: Subtract from indoor power, not outdoor power");
            }
            else
                Plugin.Logger.LogWarning("Failed to find reference to Masked enemy type. Could not apply power level fix");
        }

        static void ConvertMaskToTragedy(Transform mask)
        {
            Transform mesh = mask.Find("Mesh");
            if (mesh != null && TRAGEDY_MASK != null && TRAGEDY_MAT != null)
            {
                mesh.GetComponent<MeshFilter>().mesh = TRAGEDY_MASK;
                mesh.GetComponent<MeshRenderer>().sharedMaterial = TRAGEDY_MAT;

                MeshFilter tragedyMaskEyesFilled = mesh.Find("EyesFilled")?.GetComponent<MeshFilter>();
                if (tragedyMaskEyesFilled != null && TRAGEDY_EYES_FILLED != null)
                {
                    tragedyMaskEyesFilled.mesh = TRAGEDY_EYES_FILLED;

                    MeshFilter tragedyMaskLOD = mask.Find("ComedyMaskLOD1")?.GetComponent<MeshFilter>();
                    if (tragedyMaskLOD != null && TRAGEDY_MASK_LOD != null)
                    {
                        tragedyMaskLOD.mesh = TRAGEDY_MASK_LOD;
                        tragedyMaskLOD.GetComponent<MeshRenderer>().sharedMaterial = TRAGEDY_MAT;

                        Plugin.Logger.LogDebug($"Mask {mask.GetInstanceID()}: All meshes replaced successfully");
                    }
                    else
                        Plugin.Logger.LogWarning($"Mask {mask.GetInstanceID()}: Failed to replace eyes");
                }
                else
                    Plugin.Logger.LogWarning($"Mask {mask.GetInstanceID()}: Failed to replace LOD");
            }
            else
                Plugin.Logger.LogWarning($"Mask {mask.GetInstanceID()}: Failed to replace mesh");
        }

        [HarmonyPatch(typeof(HauntedMaskItem), nameof(HauntedMaskItem.MaskClampToHeadAnimationEvent))]
        [HarmonyPostfix]
        static void HauntedMaskItem_Post_MaskClampToHeadAnimationEvent(HauntedMaskItem __instance)
        {
            if (__instance.maskTypeId == 5)
            {
                Plugin.Logger.LogDebug($"Mask #{__instance.GetInstanceID()}: I am a Tragedy, change appearance of face mask");
                ConvertMaskToTragedy(__instance.currentHeadMask.transform);
            }
        }

        [HarmonyPatch(nameof(MaskedPlayerEnemy.Update))]
        [HarmonyPostfix]
        static void MaskedPlayerEnemy_Post_Update(MaskedPlayerEnemy __instance)
        {
            if (__instance.maskFloodParticle.isEmitting && __instance.inSpecialAnimationWithPlayer != null)
            {
                // enables the blood spillage effect that Zeekerss removed in v49
                if (__instance.inSpecialAnimationWithPlayer == GameNetworkManager.Instance.localPlayerController && !HUDManager.Instance.HUDAnimator.GetBool("biohazardDamage"))
                {
                    Plugin.Logger.LogDebug($"Mimic #{__instance.GetInstanceID()}: Vomiting blood on local player, do HUD animation");
                    HUDManager.Instance.HUDAnimator.SetBool("biohazardDamage", true);
                }
                // bonus effect: cover the player's face with blood
                __instance.inSpecialAnimationWithPlayer.bodyBloodDecals[3].SetActive(true);
            }
        }

        [HarmonyPatch(nameof(MaskedPlayerEnemy.FinishKillAnimation))]
        [HarmonyPrefix]
        static void MaskedPlayerEnemy_Pre_FinishKillAnimation(MaskedPlayerEnemy __instance)
        {
            // this should properly prevent the blood effect from persisting after you are rescued from a mask
            // reasons this didn't work in v49 (and presumably why it got removed):
            // - inSpecialAnimationWithPlayer was set to null before checking if it matched the local player
            // - just disabling biohazardDamage wasn't enough to transition back to a normal HUD animator state (it needs a trigger set as well)
            if (__instance.inSpecialAnimationWithPlayer == GameNetworkManager.Instance.localPlayerController && HUDManager.Instance.HUDAnimator.GetBool("biohazardDamage"))
            {
                Plugin.Logger.LogDebug($"Mimic #{__instance.GetInstanceID()}: Vomit animation interrupted during HUD animation");
                // cancel the particle effect early, just in case (to prevent it from retriggering and becoming stuck)
                if (__instance.maskFloodParticle.isEmitting)
                    __instance.maskFloodParticle.Stop();
                HUDManager.Instance.HUDAnimator.SetBool("biohazardDamage", false);
                HUDManager.Instance.HUDAnimator.SetTrigger("HealFromCritical");
            }
        }

        [HarmonyPatch(nameof(MaskedPlayerEnemy.KillEnemy))]
        [HarmonyPostfix]
        static void MaskedPlayerEnemy_Post_KillEnemy(MaskedPlayerEnemy __instance, bool destroy)
        {
            if (destroy)
                return;

            Animator mapDot = __instance.transform.Find("Misc/MapDot")?.GetComponent<Animator>();
            if (mapDot != null)
            {
                Plugin.Logger.LogDebug($"Mimic #{__instance.GetInstanceID()}: Stop animating radar dot");
                mapDot.enabled = false;
            }
            RandomPeriodicAudioPlayer randomPeriodicAudioPlayer = __instance.transform.GetComponentInChildren<RandomPeriodicAudioPlayer>();
            if (randomPeriodicAudioPlayer != null)
            {
                Plugin.Logger.LogDebug($"Mimic #{__instance.GetInstanceID()}: Mask becomes silent");
                randomPeriodicAudioPlayer.enabled = false;
            }
        }

        [HarmonyPatch(nameof(MaskedPlayerEnemy.SetSuit))]
        [HarmonyPostfix]
        static void MaskedPlayerEnemy_Post_SetSuit(MaskedPlayerEnemy __instance, int suitId)
        {
            Transform spine = __instance.animationContainer.Find("metarig/spine");
            Transform spine004 = spine.Find("spine.001/spine.002/spine.003/spine.004");
            if (spine == null || spine004 == null)
                return;

            try
            {
                List<MeshRenderer> meshRenderers = new(__instance.meshRenderers);

                if (suitId < StartOfRound.Instance.unlockablesList.unlockables.Count)
                {
                    // cleanup old suit pieces
                    foreach (Transform containerParent in new Transform[]{
                        spine,
                        spine004
                    })
                    {
                        foreach (Transform child in containerParent)
                        {
                            if (child.name.Contains("Container(Clone)"))
                            {
                                foreach (MeshRenderer rend in child.GetComponentsInChildren<MeshRenderer>())
                                    meshRenderers.Remove(rend);
                                Object.Destroy(child.gameObject);
                            }
                        }
                    }

                    UnlockableItem suit = StartOfRound.Instance.unlockablesList.unlockables[suitId];
                    if (suit.headCostumeObject != null)
                    {
                        foreach (MeshRenderer rend in Object.Instantiate(suit.headCostumeObject, spine004.position, spine004.rotation, spine004).GetComponentsInChildren<MeshRenderer>())
                            meshRenderers.Add(rend);
                        Plugin.Logger.LogDebug($"Mimic #{__instance.GetInstanceID()}: Equipped {suit.unlockableName} head");
                    }
                    if (suit.lowerTorsoCostumeObject != null)
                    {
                        foreach (MeshRenderer rend in Object.Instantiate(suit.lowerTorsoCostumeObject, spine.position, spine.rotation, spine).GetComponentsInChildren<MeshRenderer>())
                            meshRenderers.Add(rend);
                        Plugin.Logger.LogDebug($"Mimic #{__instance.GetInstanceID()}: Equipped {suit.unlockableName} torso");
                    }
                }

                __instance.meshRenderers = [.. meshRenderers];
            }
            catch (System.Exception e)
            {
                Plugin.Logger.LogWarning($"Encountered a non-fatal error while attaching costume pieces to mimic\n{e}");
            }
        }

        [HarmonyPatch(nameof(MaskedPlayerEnemy.SetEnemyOutside))]
        [HarmonyPostfix]
        static void MaskedPlayerEnemy_Post_SetEnemyOutside(MaskedPlayerEnemy __instance)
        {
            if (__instance.mimickingPlayer == null || __instance.timeSinceSpawn > 40f)
                return;

            Transform spine003 = __instance.maskTypes[0].transform.parent.parent;

            Renderer betaBadgeMesh = spine003.Find("BetaBadge")?.GetComponent<Renderer>();
            if (betaBadgeMesh != null)
            {
                betaBadgeMesh.enabled = __instance.mimickingPlayer.playerBetaBadgeMesh.enabled;
                Plugin.Logger.LogDebug($"Mimic #{__instance.GetInstanceID()}: VIP {(betaBadgeMesh.enabled ? "enabled" : "disabled")}");
            }
            MeshFilter badgeMesh = spine003.Find("LevelSticker")?.GetComponent<MeshFilter>();
            if (badgeMesh != null)
            {
                badgeMesh.mesh = __instance.mimickingPlayer.playerBadgeMesh.mesh;
                Plugin.Logger.LogDebug($"Mimic #{__instance.GetInstanceID()}: Updated level sticker");
            }

            // toggling GameObjects under a NetworkObject maybe also a bad idea?
            try
            {
                foreach (DecalProjector bloodDecal in __instance.transform.GetComponentsInChildren<DecalProjector>())
                {
                    foreach (GameObject bodyBloodDecal in __instance.mimickingPlayer.bodyBloodDecals)
                    {
                        if (bloodDecal.name == bodyBloodDecal.name)
                        {
                            bloodDecal.gameObject.SetActive(bodyBloodDecal.activeSelf);
                            Plugin.Logger.LogDebug($"Mimic #{__instance.GetInstanceID()}: Enabled blood decal \"{bloodDecal.name}\"");
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Plugin.Logger.LogError("Encountered a non-fatal error while enabling mimic blood");
                Plugin.Logger.LogError(e);
            }
        }

        [HarmonyPatch(nameof(MaskedPlayerEnemy.SetMaskType))]
        [HarmonyPrefix]
        static bool MaskedPlayerEnemy_Post_SetMaskType(MaskedPlayerEnemy __instance, int maskType)
        {
            if (maskType == 5 && __instance.maskTypeIndex != 1)
            {
                // this breaks the eye glow for Tragedy (since we are just changing meshes, not GameObject)
                //__instance.maskTypeIndex = 1;

                Plugin.Logger.LogDebug($"Mimic #{__instance.GetInstanceID()}: Should be Tragedy");

                // replace the comedy mask's models with the tragedy models
                ConvertMaskToTragedy(__instance.maskTypes[0].transform);

                // and swap the sound files (these wouldn't work if the tragedy's GameObject was just toggled on)
                if (TRAGEDY_RANDOM_CLIPS != null)
                {
                    RandomPeriodicAudioPlayer randomPeriodicAudioPlayer = __instance.maskTypes[0].GetComponent<RandomPeriodicAudioPlayer>();
                    if (randomPeriodicAudioPlayer != null)
                    {
                        randomPeriodicAudioPlayer.randomClips = TRAGEDY_RANDOM_CLIPS;
                        Plugin.Logger.LogDebug($"Mimic #{__instance.GetInstanceID()}: Cries");
                    }
                }
                else
                    Plugin.Logger.LogWarning("Crying audio is missing, there should be more information earlier in the log");
            }

            // need to replace the vanilla behavior entirely because it's just too problematic
            return false;
        }

        [HarmonyPatch(nameof(MaskedPlayerEnemy.Start))]
        [HarmonyPostfix]
        static void MaskedPlayerEnemy_Post_Start(MaskedPlayerEnemy __instance)
        {
            // fix proximity warning on entrance doors
            if (!RoundManager.Instance.SpawnedEnemies.Contains(__instance))
                RoundManager.Instance.SpawnedEnemies.Add(__instance);
        }

        [HarmonyPatch(nameof(MaskedPlayerEnemy.HitEnemy))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> MaskedPlayerEnemy_Trans_HitEnemy(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            MethodInfo randomRange = AccessTools.Method(typeof(Random), nameof(Random.Range), [typeof(int), typeof(int)]);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Call && (MethodInfo)codes[i].operand == randomRange)
                {
                    codes.InsertRange(i - 2,
                    [
                        new(OpCodes.Ldarg_0),
                        new(OpCodes.Call, AccessTools.DeclaredPropertyGetter(typeof(NetworkBehaviour), nameof(NetworkBehaviour.IsOwner))),
                        new(OpCodes.Brfalse, codes[i + 3].operand)
                    ]);
                    Plugin.Logger.LogDebug("Transpiler (Mimic stun): Roll 40% chance to sprint only once");
                    return codes;
                }
            }

            Plugin.Logger.LogError("Mimic stun transpiler failed");
            return instructions;
        }

        [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.SubtractFromPowerLevel))]
        [HarmonyPrefix]
        static void EnemyAI_Pre_SubtractFromPowerLevel(EnemyAI __instance)
        {
            if (__instance.removedPowerLevel)
                return;

            if (__instance is MaskedPlayerEnemy maskedPlayerEnemy && maskedPlayerEnemy.mimickingPlayer != null)
            {
                Plugin.Logger.LogDebug($"Mimic #{__instance.GetInstanceID()}: Was mimicking a player; won't subtract from power level");
                __instance.removedPowerLevel = true;
            }
        }

        [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.EnableEnemyMesh))]
        [HarmonyPrefix]
        static bool EnemyAI_Pre_EnableEnemyMesh(EnemyAI __instance, bool enable, bool overrideDoNotSet)
        {
            if (Plugin.DISABLE_ENEMY_MESH_PATCH /*|| __instance is not MaskedPlayerEnemy*/)
                return true;

            int layer = enable ? 19 : 23;
            for (int i = 0; i < __instance.skinnedMeshRenderers.Length; i++)
            {
                if (__instance.skinnedMeshRenderers[i] == null)
                {
                    __instance.skinnedMeshRenderers = __instance.skinnedMeshRenderers.Where(skinnedMeshRenderer => skinnedMeshRenderer != null).ToArray();
                    Plugin.Logger.LogWarning($"Removed all missing Skinned Mesh Renderers from enemy \"{__instance.name}\"");
                    break;
                }
                else if (overrideDoNotSet || !__instance.skinnedMeshRenderers[i].CompareTag("DoNotSet"))
                    __instance.skinnedMeshRenderers[i].gameObject.layer = layer;
            }
            for (int i = 0; i < __instance.meshRenderers.Length; i++)
            {
                if (__instance.meshRenderers[i] == null)
                {
                    __instance.meshRenderers = __instance.meshRenderers.Where(meshRenderer => meshRenderer != null).ToArray();
                    Plugin.Logger.LogWarning($"Removed all missing Mesh Renderers from enemy \"{__instance.name}\"");
                    break;
                }
                else if (overrideDoNotSet || !__instance.meshRenderers[i].CompareTag("DoNotSet"))
                    __instance.meshRenderers[i].gameObject.layer = layer;
            }

            return false;
        }

        [HarmonyPatch(typeof(EntranceTeleport), nameof(EntranceTeleport.TeleportPlayer))]
        [HarmonyPostfix]
        static void EntranceTeleport_Post_TeleportPlayer(EntranceTeleport __instance)
        {
            // 1 second cooldown to avoid cheese
            if (__instance.timeAtLastUse > localPlayerLastTeleported + 1f)
                localPlayerLastTeleported = __instance.timeAtLastUse;
        }

        // prevent instant grab when using entrances
        [HarmonyPatch(nameof(MaskedPlayerEnemy.OnCollideWithPlayer))]
        [HarmonyPrefix]
        static bool MaskedPlayerEnemy_Pre_OnCollideWithPlayer()
        {
            return Time.realtimeSinceStartup - localPlayerLastTeleported >= 1.75f;
        }

        [HarmonyPatch(nameof(MaskedPlayerEnemy.DoAIInterval))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> MaskedPlayerEnemy_Trans_DoAIInterval(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            FieldInfo elevatorScript = AccessTools.Field(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.elevatorScript));
            for (int i = 1; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Stfld && (FieldInfo)codes[i].operand == elevatorScript && codes[i - 1].opcode == OpCodes.Call && codes[i - 1].operand.ToString().Contains("FindObjectOfType"))
                {
                    codes[i - 1].operand = AccessTools.DeclaredPropertyGetter(typeof(RoundManager), nameof(RoundManager.Instance));
                    codes.Insert(i, new(OpCodes.Ldfld, AccessTools.Field(typeof(RoundManager), nameof(RoundManager.currentMineshaftElevator))));
                    Plugin.Logger.LogDebug("Transpiler (Mimic AI): Cache elevator script");
                    return codes;
                }
            }

            Plugin.Logger.LogError("Mimic AI transpiler failed");
            return instructions;
        }

        [HarmonyPatch(nameof(MaskedPlayerEnemy.ChooseShipHidingSpot))]
        [HarmonyPrefix]
        static bool MaskedPlayerEnemy_Pre_ChooseShipHidingSpot(MaskedPlayerEnemy __instance)
        {
            if (!Plugin.configPatchHidingBehavior.Value)
                return true;

            for (int i = 0; i < StartOfRound.Instance.insideShipPositions.Length; i++)
            {
                if (Physics.Linecast(StartOfRound.Instance.shipDoorAudioSource.transform.position, StartOfRound.Instance.insideShipPositions[i].position, hideLayers, QueryTriggerInteraction.Ignore) && __instance.SetDestinationToPosition(StartOfRound.Instance.insideShipPositions[i].position, true))
                {
                    __instance.shipHidingSpot = __instance.destination;

                    bool spotIsNotTaken = true;
                    foreach (EnemyAI enemyAI in RoundManager.Instance.SpawnedEnemies)
                    {
                        if (!enemyAI.isEnemyDead && enemyAI is MaskedPlayerEnemy maskedPlayerEnemy && __instance != maskedPlayerEnemy && maskedPlayerEnemy.currentBehaviourStateIndex == 2 && Vector3.Distance(__instance.shipHidingSpot, maskedPlayerEnemy.shipHidingSpot) < 1.75f)
                        {
                            spotIsNotTaken = false;
                            Plugin.Logger.LogDebug($"Mimic #{__instance.GetInstanceID()}: Can't choose hiding spot #{i}, already claimed by mimic #{maskedPlayerEnemy.GetInstanceID()}");
                            break;
                        }
                    }

                    // stop checking other spots, this one is fine
                    if (spotIsNotTaken)
                    {
                        Plugin.Logger.LogDebug($"Mimic #{__instance.GetInstanceID()}: Claimed hiding spot #{i} at coords {__instance.shipHidingSpot}");
                        return false;
                    }
                }
            }

            __instance.shipHidingSpot = StartOfRound.Instance.insideShipPositions[Random.Range(0, StartOfRound.Instance.insideShipPositions.Length)].position;
            Plugin.Logger.LogDebug($"Mimic #{__instance.GetInstanceID()}: Fall back to position at coords {__instance.shipHidingSpot}");
            return false;
        }
    }
}

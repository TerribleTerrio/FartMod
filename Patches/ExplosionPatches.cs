using UnityEngine;
using HarmonyLib;
using System.Collections;
using Steamworks.ServerList;
using GameNetcodeStuff;

namespace CoronaMod.Patches
{
    [HarmonyPatch(typeof(Landmine))]
    internal class LandminePatch
    {
        [HarmonyPatch("SpawnExplosion")]
        [HarmonyPrefix]

        static void SpawnExplosion(Landmine __instance, Vector3 explosionPosition, float killRange, float damageRange)
        {
            Debug.Log("---Landmine explosion detected!---");
            Vector3 position = explosionPosition;
            Collider[] colliders = Physics.OverlapSphere(explosionPosition, damageRange, 1076363336, QueryTriggerInteraction.Collide);

            RaycastHit hitInfo;

            for (int i = 0; i < colliders.Length; i++)
            {
                GameObject otherObject = colliders[i].gameObject;
                Debug.Log($"Explosion found object {otherObject} on layer {otherObject.layer}.");

                if (otherObject.GetComponentInParent<ArtilleryShellItem>() != null)
                {
                    if (Physics.Linecast(explosionPosition, colliders[i].transform.position + Vector3.up * 0.3f, out hitInfo, 1073742080, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                    Debug.Log($"Ray hit {hitInfo.collider}.");
                    otherObject.GetComponentInParent<ArtilleryShellItem>().ArmShell();
                }

                if (otherObject.GetComponent<Vase>() != null)
                {
                    if (Physics.Linecast(explosionPosition, colliders[i].transform.position + Vector3.up * 0.3f, out hitInfo, 1073742080, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                    Debug.Log($"Ray hit {hitInfo.collider}.");
                    otherObject.GetComponent<Vase>().Shatter(otherObject.GetComponent<Vase>().explodePrefab);
                }

                if (otherObject.GetComponent<PlayerControllerB>() != null)
                {
                    PlayerControllerB player = otherObject.GetComponent<PlayerControllerB>();
                    if (player.isHoldingObject)
                    {
                        if (player.currentlyHeldObjectServer.gameObject.GetComponent<Vase>() != null)
                        {
                            Vase vase = player.currentlyHeldObjectServer.gameObject.GetComponent<Vase>();
                            vase.Shatter(vase.explodePrefab);
                            continue;
                        }
                    }
                }

                if (otherObject.GetComponent<HydraulicStabilizer>() != null)
                {
                    if (Physics.Linecast(explosionPosition, colliders[i].transform.position + Vector3.up * 0.3f, out hitInfo, 1073742080, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                    Debug.Log($"Ray hit {hitInfo.collider}.");
                    otherObject.GetComponent<HydraulicStabilizer>().GoPsycho();
                }

            }
            // float size = killRange;
            // GameObject killCollider = SpawnExplosionCollider(position, size, "explosionColliderKillRange");
            // size = damageRange;
            // GameObject damageCollider = SpawnExplosionCollider(position, size, "explosionColliderDamageRange");
        }

        // private static GameObject SpawnExplosionCollider(Vector3 position, float size, string name)
        // {
        //     GameObject colObject = new GameObject();
        //     colObject.name = name;
        //     int layer = LayerMask.NameToLayer("Anomaly");
        //     colObject.layer = layer;
        //     colObject.tag = "DoNotSet";
        //     colObject.AddComponent<Rigidbody>();
        //     colObject.AddComponent<SphereCollider>();
        //     colObject.GetComponent<SphereCollider>().radius = size;
        //     return GameObject.Instantiate(colObject, position, Quaternion.Euler(-90f, 0f, 0f));
        // }

        // [HarmonyPatch("Update")]
        // [HarmonyPrefix]
        // static void Update(Landmine __instance)
        // {
        //     GameObject[] explosionColliders = GameObject.FindGameObjectsWithTag("DoNotSet");
        //     if (explosionColliders.Length > 0)
        //     {
        //         for (int i = 0; i < explosionColliders.Length; i++)
        //         {
        //             if (explosionColliders[i].name.StartsWith("explosionCollider"))
        //             {
        //                 GameObject.Destroy(explosionColliders[i]);
        //             }
        //         }
        //     }
        // }
    }

    [HarmonyPatch(typeof(StartOfRound))]
    internal class StartOfRoundPatch
    {
        [HarmonyPatch("LateUpdate")]
        [HarmonyPrefix]
        static void LateUpdate(StartOfRound __instance)
        {
            GameObject[] explosionColliders = GameObject.FindGameObjectsWithTag("DoNotSet");
            if (explosionColliders.Length > 0)
            {
                for (int i = 0; i < explosionColliders.Length; i++)
                {
                    if (explosionColliders[i].name.StartsWith("explosionCollider"))
                    {
                        Debug.Log($"Cleared {explosionColliders[i]}.");
                        GameObject.Destroy(explosionColliders[i]);
                    }
                }
            }
        }
    }
}
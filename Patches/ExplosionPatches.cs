using UnityEngine;
using HarmonyLib;
using System.Collections;

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
            
            float size = killRange;
            GameObject killCollider = SpawnExplosionCollider(position, size, "explosionColliderKillRange");
            size = damageRange;
            GameObject damageCollider = SpawnExplosionCollider(position, size, "explosionColliderDamageRange");
        }

        private static GameObject SpawnExplosionCollider(Vector3 position, float size, string name)
        {
            GameObject colObject = new GameObject();
            colObject.name = name;
            int layer = LayerMask.NameToLayer("Props");
            colObject.layer = layer;
            colObject.tag = "PhysicsProp";
            colObject.AddComponent<Rigidbody>();
            colObject.AddComponent<SphereCollider>();
            colObject.GetComponent<SphereCollider>().radius = size;
            return GameObject.Instantiate(colObject, position, Quaternion.Euler(-90f, 0f, 0f));
        }

        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        static void Update(Landmine __instance)
        {
            GameObject[] explosionColliders = GameObject.FindGameObjectsWithTag("PhysicsProp");
            if (explosionColliders.Length > 0)
            {
                for (int i = 0; i < explosionColliders.Length; i++)
                {
                    if (explosionColliders[i].name.StartsWith("explosionCollider"))
                    {
                        GameObject.Destroy(explosionColliders[i]);
                    }
                }
            }
        }
    }

    // [HarmonyPatch(typeof(StartOfRound))]
    // internal class StartOfRoundPatch
    // {
    //     [HarmonyPatch("LateUpdate")]
    //     [HarmonyPrefix]
    //     static void LateUpdate(Landmine __instance)
    //     {
    //         GameObject[] explosionColliders = GameObject.FindGameObjectsWithTag("PhysicsProp");
    //         if (explosionColliders.Length > 0)
    //         {
    //             for (int i = 0; i < explosionColliders.Length; i++)
    //             {
    //                 if (explosionColliders[i].name.StartsWith("explosionCollider"))
    //                 {
    //                     Debug.Log($"Cleared {explosionColliders[i]}.");
    //                     GameObject.Destroy(explosionColliders[i]);
    //                 }
    //             }
    //         }
    //     }
    // }
}
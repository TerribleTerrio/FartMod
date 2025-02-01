using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class Balloon : GrabbableObject
{

    [Space(15f)]
    [Header("Balloon Settings")]

    [Space(5f)]
    [Header("Game Object References")]
    public GameObject balloon;

    public GameObject balloonCollider;

    public GameObject[] balloonStrings;

    public GameObject grabString;

    public GameObject scanNode;

    public GameObject popPrefab;

    [Space(5f)]
    [Header("Force Settings")]
    private float pushTimer;

    public float pushCooldown;

    private float drag;

    public float dragMin = 1f;

    public float dragMax = 2f;

    public float dragLerpTime = 1f;

    private Coroutine lerpDrag;

    [Space(5f)]
    [Header("Sounds")]
    public AudioSource balloonAudio;

    public AudioClip caughtClip;

    public AudioClip[] bumpClips;

    public AudioClip tugClip;

    private LineRenderer lineRenderer;

    private SphereCollider[] itemColliders;

    public override void Start()
    {
        //BALLOON START
        balloon.transform.SetParent(null, true);
        balloonCollider.transform.SetParent(null, true);

        for (int i = 0; i < balloonStrings.Length; i++)
        {
            balloonStrings[i].transform.SetParent(null, true);
        }

        lineRenderer = base.gameObject.GetComponent<LineRenderer>();
        lineRenderer.positionCount = balloonStrings.Length;

        //CREATE COLLIDERS FOR EACH STRING SECTION
        itemColliders = new SphereCollider[balloonStrings.Length];
        for (int i = 0; i < balloonStrings.Length; i++)
        {
            SphereCollider sphereCollider = base.gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 1f;
            itemColliders[i] = sphereCollider;
        }

        DampFloating();



        //BASE START FOR GRABBABLE OBJECT WITHOUT FALLING
        propColliders = base.gameObject.GetComponentsInChildren<Collider>();
        for (int i = 0; i < propColliders.Length; i++)
        {
            if (!propColliders[i].CompareTag("InteractTrigger"))
            {
                propColliders[i].excludeLayers = -2621449;
            }
        }

        originalScale = base.transform.localScale;

        if (itemProperties.isScrap && RoundManager.Instance.mapPropsContainer != null)
        {
            radarIcon = UnityEngine.Object.Instantiate(StartOfRound.Instance.itemRadarIconPrefab, RoundManager.Instance.mapPropsContainer.transform).transform;
        }

        if (!itemProperties.isScrap)
        {
            HoarderBugAI.grabbableObjectsInMap.Add(base.gameObject);
        }

        MeshRenderer[] meshRenderers = base.gameObject.GetComponentsInChildren<MeshRenderer>();
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            meshRenderers[i].renderingLayerMask = 1u;
        }
        SkinnedMeshRenderer[] skinnedMeshRenderers = base.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
        for (int i = 0; i < skinnedMeshRenderers.Length; i++)
        {
            skinnedMeshRenderers[i].renderingLayerMask = 1u;
        }

        // EnableItemMeshes(enable: false);
    }

    public override void Update()
    {

        //BASE UPDATE FOR SCRAP WITHOUT FALLING
        if (currentUseCooldown >= 0f)
        {
            currentUseCooldown -= Time.deltaTime;
        }

        if (base.IsOwner)
        {
            if (!wasOwnerLastFrame)
            {
                wasOwnerLastFrame = true;
            }
        }
        else if (wasOwnerLastFrame)
        {
            wasOwnerLastFrame = false;
        }

        //BALLOON UPDATE
        if (pushTimer > 0)
        {
            pushTimer -= Time.deltaTime;
        }

        for (int i = 0; i < balloonStrings.Length; i++)
        {
            Rigidbody rigidbody = balloonStrings[i].GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.drag = drag;
            }
        }

        if (balloon.transform.position.y > 70f)
        {
            Pop();
        }

        if (playerHeldBy != null && Vector3.Distance(balloon.transform.position, grabString.transform.position) > 8f)
        {
            //PLAY SOUND
            balloonAudio.PlayOneShot(caughtClip);

            //PLAY AUDIBLE NOISE
            // if (Vector3.Distance(lastPosition, balloon.transform.position) > 2f)
            // {
            //     timesPlayedInOneSpot = 0;
            // }
            // timesPlayedInOneSpot++;
            // lastPosition = balloon.transform.position;
            // RoundManager.Instance.PlayAudibleNoise(balloon.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);

            //DISCARD BALLOON
            playerHeldBy.DiscardHeldObject();
        }
    }

    public override void LateUpdate()
    {
        if (radarIcon != null)
        {
            radarIcon.position = base.transform.position;
        }

        //SET STRING TO HAND IF OBJECT IS HELD BY PLAYER
        if (isHeld)
        {
            grabString.transform.position = playerHeldBy.localItemHolder.transform.position;
        }

        //SET POSITIONS OF BALLOON PARTS
        base.transform.position = grabString.transform.position;
        scanNode.transform.position = balloon.transform.position;
        balloonCollider.transform.position = balloon.transform.position;

        //SET POSITIONS OF GRAB COLLIDERS
        for (int i = 0; i < itemColliders.Length; i++)
        {
            itemColliders[i].center = base.transform.InverseTransformPoint(balloonStrings[i].transform.position);
        }

        //SET LINE RENDERER POSITIONS
        Vector3[] stringPositions = new Vector3[balloonStrings.Length];
        for (int i = 0; i < balloonStrings.Length; i++)
        {
            stringPositions[i] = balloonStrings[i].transform.position;
        }
        lineRenderer.SetPositions(stringPositions);
    }



    //GRABBING AND DISCARDING
    public override void GrabItem()
    {
        base.GrabItem();
    }

    public override void GrabItemFromEnemy(EnemyAI enemy)
    {
        base.GrabItemFromEnemy(enemy);
    }

    public override void DiscardItem()
    {
        base.DiscardItem();
        DampFloating();
    }

    public override void DiscardItemFromEnemy()
    {

    }

    public void DampFloating()
    {
        Debug.Log("[BALLOON]: Damping physics!");

        for (int i = 0; i < balloonStrings.Length; i++)
        {
            Rigidbody rigidbody = balloonStrings[i].GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.velocity = new Vector3(0f,0f,0f);
            }
        }

        if (lerpDrag != null)
        {
            StopCoroutine(lerpDrag);
        }

        lerpDrag = StartCoroutine(LerpDrag());
    }

    public IEnumerator LerpDrag()
    {
        drag = dragMax;
        float timeElapsed = 0f;
        while (timeElapsed < dragLerpTime)
        {
            drag = Mathf.Lerp(dragMax, dragMin, timeElapsed / dragLerpTime);
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        drag = dragMin;
    }



    //USING ITEM
    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        Debug.Log("[BALLOON]: Used item!");
        base.ItemActivate(used, buttonDown);

        //ANIMATE HAND DOWN TO TUG ON BALLOON A BIT

        //PLAY SOUND
        balloonAudio.PlayOneShot(tugClip);

        //PLAY AUDIBLE NOISE
        // if (Vector3.Distance(lastPosition, balloon.transform.position) > 2f)
        // {
        //     timesPlayedInOneSpot = 0;
        // }
        // timesPlayedInOneSpot++;
        // lastPosition = balloon.transform.position;
        // RoundManager.Instance.PlayAudibleNoise(balloon.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
    }



    //COLLISIONS
    public void OnTouch(Collider other)
    {
        GameObject otherObject = other.gameObject;
        Debug.Log($"[BALLOON]: Collided with {otherObject}");

        RaycastHit hitInfo;
        if (Physics.Linecast(transform.position, other.transform.position, out hitInfo, 1073742080, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        //PLAYER COLLISION
        if (otherObject.layer == 3 && otherObject.GetComponent<PlayerControllerB>() != null)
        {
            Debug.Log("[BALLOON]: Bumped by player.");
            PushBalloon(otherObject.transform.position, 10f);
        }

        //ENEMY COLLISION
        else if (otherObject.layer == 19 && otherObject.GetComponent<EnemyAICollisionDetect>() != null)
        {
            EnemyAICollisionDetect enemy = otherObject.GetComponent<EnemyAICollisionDetect>();
            Debug.Log($"[BALLOON]: Bumped by enemytype {enemy.mainScript.enemyType.enemyName}.");

            if (enemy.mainScript.enemyType.enemyName == "Flowerman")
            {
                PushBalloon(otherObject.transform.position, 8f);
            }

            else if (enemy.mainScript.enemyType.enemyName == "Spring")
            {
                PushBalloon(otherObject.transform.position, 20f);
            }

            else if (enemy.mainScript.enemyType.enemyName == "Jester")
            {
                JesterAI jester = enemy.mainScript as JesterAI;
                if (jester.creatureAnimator.GetBool("poppedOut"))
                {
                    Pop();
                }
                else
                {
                    PushBalloon(otherObject.transform.position, 8f);
                }
            }

            else if (enemy.mainScript.enemyType.enemyName == "Maneater")
            {
                CaveDwellerAI caveDweller = enemy.mainScript as CaveDwellerAI;
                if (caveDweller.adultContainer.activeSelf)
                {
                    PushBalloon(otherObject.transform.position, 20f);
                }
            }

            else if (enemy.mainScript.enemyType.enemyName == "Masked")
            {
                PushBalloon(otherObject.transform.position, 10f);
            }

            else if (enemy.mainScript.enemyType.enemyName == "Crawler")
            {
                CrawlerAI crawler = enemy.mainScript as CrawlerAI;
                if (crawler.hasEnteredChaseMode)
                {
                    Pop();
                }
                else
                {
                    PushBalloon(otherObject.transform.position, 20f);
                }
            }

            else if (enemy.mainScript.enemyType.enemyName == "Red Locust Bees")
            {
                Pop();
            }

            else if (enemy.mainScript.enemyType.enemyName == "Earth Leviathan")
            {
                Pop();
            }

            else if (enemy.mainScript.enemyType.enemyName == "MouthDog")
            {
                MouthDogAI mouthDog = enemy.mainScript as MouthDogAI;
                if (mouthDog.hasEnteredChaseModeFully)
                {
                    Pop();
                }
                else
                {
                    PushBalloon(otherObject.transform.position, 20f);
                }
            }

            else if (enemy.mainScript.enemyType.enemyName == "ForestGiant")
            {
                PushBalloon(otherObject.transform.position, 30f);
            }

            else if (enemy.mainScript.enemyType.enemyName == "Manticoil")
            {
                DoublewingAI doublewing = enemy.mainScript as DoublewingAI;
                if (doublewing.creatureAnimator.GetBool("flying"))
                {
                    Pop();
                }
            }

            else if (enemy.mainScript.enemyType.enemyName == "Docile Locust Bees")
            {
                Pop();
            }

            else if (enemy.mainScript.enemyType.enemyName == "Tulip Snake")
            {
                FlowerSnakeEnemy flowerSnake = enemy.mainScript as FlowerSnakeEnemy;
                if (flowerSnake.leaping)
                {
                    Pop();
                }
            }

            else if (enemy.mainScript.enemyType.enemyName == "Centipede")
            {
                CentipedeAI centipede = enemy.mainScript as CentipedeAI;
                if (centipede.triggeredFall)
                {
                    Pop();
                }
            }
        }

        //VEHICLE COLLISION
        else if (otherObject.GetComponentInParent<VehicleController>() != null)
        {
            VehicleController vehicle = otherObject.GetComponentInParent<VehicleController>();
            if (vehicle.averageVelocity.magnitude < 5)
            {
                PushBalloon(otherObject.transform.position, 5*vehicle.averageVelocity.magnitude);
            }
            else
            {
                Pop();
            }
        }

        //ITEM COLLISION
        else if (otherObject.layer == 6 && otherObject.GetComponent<GrabbableObject>() != null)
        {
            GrabbableObject gObject = otherObject.GetComponent<GrabbableObject>();

            if (gObject.itemProperties.itemName == "Soccer ball")
            {
                SoccerBallProp ball = otherObject.GetComponent<SoccerBallProp>();
                if (!ball.hasHitGround && !ball.isHeld && !ball.isHeldByEnemy)
                {
                    Pop();
                }
            }
        }

        //SPIKE TRAP COLLISION
        else if (otherObject.layer == 11 && otherObject.tag == "Aluminum" && otherObject.name == "Cube (2)")
        {
            Pop();
        }
    }

    public void OnExit(Collider other)
    {

    }

    public void PushBalloon(Vector3 otherPos, float pushForce)
    {
        if (pushTimer <= 0)
        {
            pushTimer = pushCooldown;

            //ADD FORCE
            Vector3 vector = transform.position - otherPos;
            vector.y = 0;
            Vector3 dir = Vector3.Normalize(vector);
            Vector3 force = dir*pushForce;
            Debug.Log($"[BALLOON]: Pushing with force {force}!");
            balloon.GetComponent<Rigidbody>().AddForce(force, ForceMode.Impulse);

            //PLAY SOUND
            balloonAudio.PlayOneShot(bumpClips[Random.Range(0,bumpClips.Length)]);

            //PLAY AUDIBLE NOISE
            // if (Vector3.Distance(lastPosition, balloon.transform.position) > 2f)
            // {
            //     timesPlayedInOneSpot = 0;
            // }
            // timesPlayedInOneSpot++;
            // lastPosition = balloon.transform.position;
            // RoundManager.Instance.PlayAudibleNoise(balloon.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        }
    }



    //POPPING
    public void Pop()
    {
        Debug.Log("[BALLOON]: Popped!");
        SetScrapValue(0);

        if (playerHeldBy != null)
        {
            playerHeldBy.DiscardHeldObject();
        }

        //SPAWN POP PREFAB
        Instantiate(popPrefab, balloon.transform.position, Quaternion.identity);

        DespawnServerRpc();
    }

    public void DestroyBalloon()
    {
        for (int i = 0; i < balloonStrings.Length; i++)
        {
            Destroy(balloonStrings[i]);
        }

        Destroy(balloonCollider);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DespawnServerRpc()
    {
        DespawnClientRpc();
    }

    [ClientRpc]
    public void DespawnClientRpc()
    {
        StartCoroutine(DespawnAfterFrame());
    }

    public IEnumerator DespawnAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        DestroyBalloon();
        if (base.gameObject.GetComponent<NetworkObject>().IsSpawned)
        {
            Debug.Log("[BALLOON]: Despawning!");
            base.gameObject.GetComponent<NetworkObject>().Despawn();
        }
    }
}
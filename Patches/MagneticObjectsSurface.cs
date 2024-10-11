using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class MagneticObjectsSurface : PlaceableObjectsSurface
{
    string[] magnets;

    public void Start()
    {
        magnets[0] = "Fridge Magnet";
    }

	new private void Update()
	{
		if (GameNetworkManager.Instance != null && GameNetworkManager.Instance.localPlayerController != null)
        {
            for (int i = 0; i < magnets.Length; i++)
            {
                if (magnets[i] == GameNetworkManager.Instance.localPlayerController.currentlyHeldObject.itemProperties.itemName)
                {
                    triggerScript.interactable = true;
                    return;
                }
                else
                {
                    triggerScript.interactable = false;
                }
            }
        }
	}

    new public void PlaceObject(PlayerControllerB playerWhoTriggered)
    {
        if (!playerWhoTriggered.isHoldingObject || playerWhoTriggered.isGrabbingObjectAnimation || !(playerWhoTriggered.currentlyHeldObjectServer != null))
        {
            return;
        }
        Debug.Log("Placing object in storage");
        Vector3 vector = itemPlacementPosition(playerWhoTriggered.gameplayCamera.transform, playerWhoTriggered.currentlyHeldObjectServer);
        // Quaternion quaternion = itemPlacementRotation(playerWhoTriggered.gameplayCamera.transform, playerWhoTriggered.currentlyHeldObjectServer);
        if (!(vector == Vector3.zero))
        {
            if (parentTo != null)
            {
                transform.position = parentTo.transform.InverseTransformPoint(transform.position);
                //SET ROTATION
            }
            playerWhoTriggered.DiscardHeldObject(placeObject: true, parentTo, transform.position, matchRotationOfParent: true);
            Debug.Log("discard held object called from placeobject");
        }
    }
    private Vector3 itemPlacementPosition(Transform gameplayCamera, GrabbableObject heldObject)
	{
		if (Physics.Raycast(gameplayCamera.position, gameplayCamera.forward, out var hitInfo, 7f, 1073744640, QueryTriggerInteraction.Ignore))
		{
			if (placeableBounds.ClosestPoint(hitInfo.point) == hitInfo.point)
			{
				return hitInfo.point + placeableBounds.transform.up * heldObject.itemProperties.verticalOffset;
			}
			return placeableBounds.ClosestPoint(hitInfo.point) + placeableBounds.transform.forward * heldObject.itemProperties.verticalOffset;
		}
		return Vector3.zero;
	}

    // private Quaternion itemPlacementRotation(Transform gameplayCamera, GrabbableObject heldObject)
    // {

    // }
}
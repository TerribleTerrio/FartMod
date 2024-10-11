using GameNetcodeStuff;

public interface ZappableObject
{
	float GetZapDifficulty();

	void StopShockingWithGun();

	void ShockWithGun(PlayerControllerB shockedByPlayer);
}

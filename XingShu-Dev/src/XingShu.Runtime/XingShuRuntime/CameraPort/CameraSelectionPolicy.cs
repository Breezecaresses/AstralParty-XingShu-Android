namespace XingShuRuntime.CameraPort;

internal static class CameraSelectionPolicy
{
	internal static CameraControlMode Read(bool contract, bool fight, bool monster, bool card, int? screen, int? mode, int? status)
	{
		return Read(contract, fight, monster, card, screen, mode, status, selectingLand: false);
	}

	internal static CameraControlMode Read(bool contract, bool fight, bool monster, bool card, int? screen, int? mode, int? status, bool selectingLand)
	{
		if (!contract)
		{
			return CameraControlMode.Unavailable;
		}
		if (fight)
		{
			return CameraControlMode.Combat;
		}
		if (monster)
		{
			return CameraControlMode.Selection;
		}
		if (card && !selectingLand)
		{
			return CameraControlMode.Selection;
		}
		// Only the game's System (2) state pushes a board target camera.
		// MapSignal (3, ping / magnifying glass) does not move the camera.
		if (status == 2)
		{
			return CameraControlMode.NativeBoard;
		}
		return status == 0 || status == 1 || status == 3 ? CameraControlMode.Free : CameraControlMode.System;
	}
}

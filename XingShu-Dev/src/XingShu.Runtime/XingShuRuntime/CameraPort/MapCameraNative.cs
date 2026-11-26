using System;
using XingShuRuntime.Core;
using XingShuRuntime.Utilities;
using Cinemachine;
using Core;
using FairyGUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShuRuntime.CameraPort;

internal static class MapCameraNative
{
	private const string PointerConsumer = "MapCamera";

	internal static bool TouchInputOnly => XingShuMobileUIHelper.IsMobile;

	internal static bool IsBlockingUiObject(GObject obj)
	{
		if (obj == null || !obj.touchable)
		{
			return false;
		}
		if (obj is GTextField && string.Equals(obj.name, "txt_HideTip", StringComparison.Ordinal))
		{
			return false;
		}
		bool sawInteractiveControl = false;
		int depth = 0;
		for (GObject cur = obj; cur != null && depth < 64; cur = cur.parent, depth++)
		{
			if (cur is GButton || cur is GSlider || cur is GScrollBar || cur is GTextInput || cur is GComboBox)
			{
				sawInteractiveControl = true;
			}
			string typeName = cur.GetType().Name;
			if (IsHiddenModalComponent(cur, typeName))
			{
				return sawInteractiveControl;
			}
		}
		return true;
	}

	private static bool IsHiddenModalComponent(GObject obj, string typeName)
	{
		if (!(obj is GComponent comp))
		{
			return false;
		}
		if (string.Equals(typeName, "UIFight_LaunchPK", StringComparison.Ordinal))
		{
			Controller hideCtrl = comp.GetController("Hide");
			return !comp.opaque || (hideCtrl != null && hideCtrl.selectedIndex != 0);
		}
		if (string.Equals(typeName, "UILandDivinationWindow", StringComparison.Ordinal))
		{
			Controller hideUiCtrl = comp.GetController("HideUI");
			return hideUiCtrl != null && hideUiCtrl.selectedIndex == 0;
		}
		if (string.Equals(typeName, "UIBattleSelectMonsterWindow", StringComparison.Ordinal)
			|| string.Equals(typeName, "UIRelicWindow", StringComparison.Ordinal)
			|| string.Equals(typeName, "UILandShopWindow", StringComparison.Ordinal)
			|| string.Equals(typeName, "UILandRelicWindow", StringComparison.Ordinal)
			|| string.Equals(typeName, "UILandPursuitWindow", StringComparison.Ordinal)
			|| string.Equals(typeName, "UILandVendorWindow", StringComparison.Ordinal)
			|| string.Equals(typeName, "UIATMWindow", StringComparison.Ordinal))
		{
			Controller hideCtrl = comp.GetController("Hide");
			return hideCtrl != null && hideCtrl.selectedIndex != 0;
		}
		return false;
	}

	internal static void CancelTouchClick(int fingerId)
	{
		Stage inst = Stage.inst;
		if (inst != null)
		{
			inst.GetTouchTarget(fingerId);
			inst.CancelClick(fingerId);
		}
	}

	internal static bool TouchBlocked(int fingerId, Vector2 pointer)
	{
		if (!XingShuCentralInputManager.CanProcessPointer(PointerConsumer, XingShuPointerAction.None, fingerId, pointer))
		{
			return true;
		}
		Stage inst = Stage.inst;
		if (inst == null || StageCamera.main == null || GObject.draggingObject != null)
		{
			return true;
		}
		Vector3 screenPoint = HitTestContext.screenPoint;
		Vector3 worldPoint = HitTestContext.worldPoint;
		Vector3 direction = HitTestContext.direction;
		Camera camera = HitTestContext.camera;
		bool forTouch = HitTestContext.forTouch;
		DisplayObject displayObject;
		try
		{
			HitTestContext.ClearRaycastHitCache();
			displayObject = inst.HitTest(new Vector2(pointer.x, (float)Screen.height - pointer.y), forTouch: true);
		}
		finally
		{
			HitTestContext.ClearRaycastHitCache();
			HitTestContext.screenPoint = screenPoint;
			HitTestContext.worldPoint = worldPoint;
			HitTestContext.direction = direction;
			HitTestContext.camera = camera;
			HitTestContext.forTouch = forTouch;
		}
		if (displayObject == null || displayObject == inst)
		{
			return false;
		}
		int num = 0;
		while (displayObject != null && num < 64)
		{
			if (displayObject.gOwner != null)
			{
				return IsBlockingUiObject(displayObject.gOwner);
			}
			num++;
			displayObject = displayObject.parent;
		}
		return true;
	}

	internal static bool? IsBlending(Behaviour value)
	{
		CinemachineBrain cinemachineBrain = value as CinemachineBrain;
		if (!(cinemachineBrain == null))
		{
			return cinemachineBrain.IsBlending;
		}
		return null;
	}

	internal static bool CancelManualStatus(Behaviour value)
	{
		FreeCameraObject freeCameraObject = value as FreeCameraObject;
		if (freeCameraObject == null)
		{
			return false;
		}
		if (freeCameraObject.status == FreeCameraStatus.Player)
		{
			freeCameraObject.CancelFreeStatus();
		}
		return true;
	}

	internal static bool KeyboardFocused()
	{
		if (GUIUtility.keyboardControl != 0 || Input.compositionString.Length > 0)
		{
			return true;
		}
		GameObject gameObject = ((EventSystem.current == null) ? null : EventSystem.current.currentSelectedGameObject);
		if (gameObject != null && (gameObject.GetComponent<InputField>() != null || gameObject.GetComponent("TMP_InputField") != null))
		{
			return true;
		}
		if (Stage.inst != null)
		{
			return Stage.inst.focus is InputTextField;
		}
		return true;
	}

	internal static bool PointerBlocked()
	{
		if (!XingShuCentralInputManager.CanProcessPointer(PointerConsumer, XingShuPointerAction.None))
		{
			return true;
		}
		if (GObject.draggingObject != null || Stage.inst == null)
		{
			return true;
		}
		DisplayObject displayObject = Stage.inst.touchTarget;
		if (displayObject == null)
		{
			return false;
		}
		int num = 0;
		while (displayObject != null && num < 64)
		{
			if (displayObject.gOwner != null)
			{
				return IsBlockingUiObject(displayObject.gOwner);
			}
			num++;
			displayObject = displayObject.parent;
		}
		return true;
	}
}

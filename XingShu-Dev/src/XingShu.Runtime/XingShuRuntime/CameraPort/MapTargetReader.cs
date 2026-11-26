using System;
using System.Reflection;
using Core;
using Core.Scene;
using FairyGUI;
using GameLogic;
using Tools;
using UI;
using party.model;

namespace XingShuRuntime.CameraPort;

internal static class MapTargetReader
{
	private static readonly string[] Fields = new string[3] { "_cachedBattleSelectMonsterWindow", "_cachedFightWindow", "_cachedCardWindow" };

	private static bool Field(object target, string name, out object value)
	{
		value = null;
		if (target == null)
		{
			return false;
		}
		Type type = target.GetType();
		while (type != null)
		{
			FieldInfo field = type.GetField(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null)
			{
				value = field.GetValue(target);
				return true;
			}
			type = type.BaseType;
		}
		return false;
	}

	internal static bool Running()
	{
		if (!SimpleSingletonProvider<GameLogicManager>.hasInstance || BattleSceneController.inst == null)
		{
			return false;
		}
		GameLogicManager inst = SimpleSingletonProvider<GameLogicManager>.inst;
		RoomInfo roomInfo = ((inst.room == null) ? null : inst.room.curRoomInfo);
		if (roomInfo != null && roomInfo.State == Room.Types.State.Running && roomInfo.info != null && roomInfo.info.Id > 0)
		{
			return roomInfo.info.StartTime > 0;
		}
		return false;
	}

	internal static CameraControlMode Read(BattleSceneController controller)
	{
		if (controller == null || controller != BattleSceneController.inst || !Running())
		{
			return CameraControlMode.Unavailable;
		}
		if (!SimpleSingletonProvider<UIManager>.hasInstance)
		{
			return CameraControlMode.System;
		}
		UIManager inst = SimpleSingletonProvider<UIManager>.inst;
		if (!Field(inst, Fields[0], out var value) || !Field(inst, Fields[1], out var value2) || !Field(inst, Fields[2], out var value3))
		{
			return CameraControlMode.System;
		}
		if ((value != null && !(value is Window)) || (value2 != null && !(value2 is Window)) || (value3 != null && !(value3 is Window)))
		{
			return CameraControlMode.System;
		}
		bool fight = IsFightWindowBlocking(value2 as Window);
		Window monsterWin = value as Window;
		bool monsterWindowHidden = IsMonsterWindowHidden(monsterWin);
		bool monsterWindow = !monsterWindowHidden && monsterWin != null && !monsterWin.isDisposed && monsterWin.parent != null;
		Window cardWindow = value3 as Window;
		bool card = cardWindow != null && !cardWindow.isDisposed && cardWindow.parent != null;
		bool selectingLand = card && IsSelectingLand(cardWindow);
		var action = SimpleSingletonProvider<GameLogicManager>.inst.action;
		bool choosingTarget = !selectingLand && !monsterWindowHidden && action != null && (action.playerAction == PlayerActionEnum.CHOOSETARGET_CARD
			|| action.playerAction == PlayerActionEnum.CHOOSETARGET_SKILL);
		bool monster = monsterWindow || choosingTarget;
		return CameraSelectionPolicy.Read(contract: true, fight, monster, card, null, null, (controller.freeObject == null) ? null : new int?((int)controller.freeObject.status), selectingLand);
	}

	private static bool IsFightWindowBlocking(Window fightWindow)
	{
		if (fightWindow == null || fightWindow.isDisposed || fightWindow.parent == null)
		{
			return false;
		}
		if (fightWindow.contentPane is UIFightWindow fightUi && !fightUi.isDisposed)
		{
			bool inLaunchStep = fightUi.step == null || fightUi.step.selectedIndex == 0;
			UIFight_LaunchPK launchPk = fightUi.com_LaunchPK;
			if (inLaunchStep && launchPk != null && !launchPk.isDisposed && (!launchPk.opaque || (launchPk.Hide != null && launchPk.Hide.selectedIndex != 0)))
			{
				return false;
			}
		}
		return true;
	}

	private static bool IsMonsterWindowHidden(Window monsterWindow)
	{
		return monsterWindow != null && !monsterWindow.isDisposed && monsterWindow.parent != null
			&& monsterWindow.contentPane is UIBattleSelectMonsterWindow ui && !ui.isDisposed
			&& ui.Hide != null && ui.Hide.selectedIndex != 0;
	}

	private static bool IsSelectingLand(Window cardWindow)
	{
		if (!(cardWindow.contentPane is UICardWindow { isDisposed: false } cardUi))
		{
			return false;
		}
		if (cardUi.showContent == null || cardUi.showContent.selectedIndex != 1)
		{
			return false;
		}
		UICardWin_Com_UseCard useCard = cardUi.com_UseCard;
		return useCard != null && !useCard.isDisposed && useCard.type != null && useCard.type.selectedIndex == 4;
	}

	internal static bool IsMarking(BattleSceneController controller)
	{
		if (controller != null && controller.freeObject != null)
		{
			return controller.freeObject.status == FreeCameraStatus.MapSignal;
		}
		return false;
	}
}

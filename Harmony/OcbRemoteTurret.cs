// Part of Remote Turret Control Mod
// Copyright 2022 Marcel Greter

using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Emit;

public class OcbRemoteTurret : IModApi
{

	public static bool IsRemote()
	{
		return BlockRemoteTurret.CurrentOpenBlock != null
			&& BlockRemoteTurret.CurrentOpenPanel != null
			&& BlockRemoteTurret.RemoteTurrets != null
			&& BlockRemoteTurret.RemoteTurrets.Count > 0;
	}

	public static bool IsAmmoLocked(XUiC_PowerRangedAmmoSlots ui)
	{
		return IsRemote() && BlockRemoteTurret.CurrentOpenBlock.IsAmmoLocked(
			ui.xui.playerUI.localPlayer.entityPlayerLocal, ui.TileEntity.AmmoItem);
	}


	public void InitMod(Mod mod)
	{
		Log.Out("OCB Harmony Patch: " + GetType().ToString());
		Harmony harmony = new Harmony(GetType().ToString());
		harmony.PatchAll(Assembly.GetExecutingAssembly());
		// Register cam throttler for frame updates
		ModEvents.UnityUpdate.RegisterHandler(
			ThrottleCams.BeforeFrameUpdate);
	}

	// Fix vanilla "bug" where parent position would not be set
	// We use it to also know grid at client side. Funny enough,
	// without this fix it doesn't work on the server side!?
	private static readonly FieldInfo FieldParentPosition = AccessTools
		.Field(typeof(TileEntityPowered), "parentPosition");

	// Implement `OpenTileEntityUi` for `RemoteTurret`
	// Where client opens UI when server confirmed lock
	[HarmonyPatch(typeof(GameManager))]
	[HarmonyPatch("OpenTileEntityUi")]
	public class GameMager_OpenTileEntityUi
	{
		public static bool Prefix(
			World ___m_World,
			int _entityIdThatOpenedIt,
			TileEntity _te,
			string _customUi)
		{
			// We have very strict rules before opening it
			if (!string.IsNullOrEmpty(_customUi)) return true;
			// Nothing to be opened if we have a current panel and just switch powered ranged trap
			if (_te is TileEntityPoweredRangedTrap && BlockRemoteTurret.CurrentOpenBlock != null) return false;
			// Try to get the player that actually called to open it
			if (___m_World.GetEntity(_entityIdThatOpenedIt) is EntityPlayerLocal player)
			{
				// Get the local UI for the player who opened it!?
				// ToDo: can a client have more than one player ui?
				if (LocalPlayerUI.GetUIForPlayer(player) is LocalPlayerUI ui)
				{
					// Call into our main class and return result
					// We may fall back to default implementation
					return !BlockRemoteTurret.OnPanelOpen(ui);
				}
			}
			return true;
		}
	}

	// Make sure to open the right window
	[HarmonyPatch(typeof(GUIWindowManager))]
	[HarmonyPatch("Open")]
	[HarmonyPatch(new System.Type[] {
		typeof(string), typeof(bool),
		typeof(bool), typeof(bool) })]
	public class GUIWindowManager_Open
	{
		public static void Prefix(
			ref string _windowName)
		{
			if (_windowName != "powerrangedtrap") return;
			if (BlockRemoteTurret.CurrentOpenBlock == null) return;
			_windowName = "remoteturret";
		}
	}


	// Allow to reset `TileEntity` to null without error
	[HarmonyPatch(typeof(XUiC_PowerRangedAmmoSlots))]
	[HarmonyPatch("TileEntity", MethodType.Setter)]
	public class XUiC_PowerRangedAmmoSlots_SetTileEntity
	{
		public static bool Prefix(
			ref TileEntityPoweredRangedTrap ___tileEntity,
			TileEntityPoweredRangedTrap value)
		{
			___tileEntity = value;
			return value != null;
		}
	}

	// Allow to reset `TileEntity` to null without error
	[HarmonyPatch(typeof(XUiC_PowerRangedTrapWindowGroup))]
	[HarmonyPatch("TileEntity", MethodType.Setter)]
	public class XUiC_PowerRangedTrapWindowGroup_SetTileEntity
	{
		public static bool Prefix(
			ref TileEntityPoweredRangedTrap ___tileEntity,
			ref XUiC_PowerRangedAmmoSlots ___ammoWindow,
			ref XUiC_CameraWindow ___cameraWindowPreview,
			TileEntityPoweredRangedTrap value)
		{
			___tileEntity = value;
			___ammoWindow.TileEntity = value;
			___cameraWindowPreview.TileEntity = value;
			return value != null;
		}
	}

	// Lock the slots visually and functionally
	[HarmonyPatch(typeof(XUiC_PowerRangedAmmoSlots))]
	[HarmonyPatch("RefreshIsLocked")]
	public class XUiC_PowerRangedAmmoSlots_RefreshIsLocked
	{
		public static void Postfix(
			XUiC_PowerRangedAmmoSlots __instance,
			XUiController[] ___itemControllers)
		{
			if (!IsAmmoLocked(__instance)) return;
			for (int index = 0; index < ___itemControllers.Length; ++index)
			{
				if (___itemControllers[index] is XUiC_RequiredItemStack ctr)
					ctr.ToolLock = true;
			}
		}
	}

	// Disallow to set any slot items if ammo is locked.
	// This prevents e.g. shift+click actions and more.
	[HarmonyPatch(typeof(XUiC_PowerRangedAmmoSlots))]
	[HarmonyPatch("TryAddItemToSlot")]
	public class XUiC_PowerRangedAmmoSlots_TryAddItemToSlot
	{
		public static bool Prefix(
			XUiC_PowerRangedAmmoSlots __instance,
			ref bool __result)
		{
			if (IsAmmoLocked(__instance))
			{
				__result = false;
				return false;
			}
			return true;
		}
	}

	// Disable on/off button for remote controlled traps
	[HarmonyPatch(typeof(XUiC_PowerRangedAmmoSlots))]
	[HarmonyPatch("btnOn_OnPress")]
	public class XUiC_PowerRangedAmmoSlots_btnOn_OnPress
	{
		public static bool Prefix(XUiC_PowerRangedAmmoSlots __instance)
		{
			if (IsAmmoLocked(__instance))
			{

				GameManager.ShowTooltip(__instance.xui.playerUI.localPlayer.entityPlayerLocal,
					Localization.Get("ttAmmoLocked"), string.Empty, "ui_denied");
				return false;
			}
			return true;
		}
	}

	/*************************************************************************/
	// Below is a few advanced transpiler patches
	// Inserting `UpgradeVariantHelper` into handler
	/*************************************************************************/

	// Used by patched function below
	static void UpgradeVariantHelper(ItemStack stack)
	{
		// Check if we are dealing with a block
		if (stack.itemValue.type < Block.ItemsStartHere)
		{
			// Check if the block has `ReturnVariantHelper` set
			if (Block.list[stack.itemValue.type].Properties.Values
				.TryGetString("ReturnVariantHelper", out string variant))
			{
				// Upgrade `itemValue` to variant helper block type
				if (Block.GetBlockByName(variant) is Block helper)
					stack.itemValue = new ItemValue(helper.blockID);
			}
		}
	}

	[HarmonyPatch(typeof(BlockPowered))]
	[HarmonyPatch("EventData_Event")]
	public static class BlockPowered_EventData_Event
	{

		static IEnumerable<CodeInstruction> Transpiler
			(IEnumerable<CodeInstruction> instructions)
		{
			var codes = new List<CodeInstruction>(instructions);

			bool searchFirstMarker = true;

			for (var i = 0; i < codes.Count; i++)
			{
				if (searchFirstMarker)
				{
					if (codes[i].opcode == OpCodes.Call)
					{
						// Simply compare to string representation (any better way?)
						if (codes[i].operand.ToString().StartsWith("ItemValue ToItemValue("))
						{
							searchFirstMarker = false;
						}
					}
				}
				else if (codes[i].opcode == OpCodes.Stloc_S)
				{
					// Create the new OpCodes to be inserted
					var op1 = new CodeInstruction(OpCodes.Ldloc_S, codes[i].operand);
					var op2 = CodeInstruction.Call(typeof(OcbRemoteTurret), "UpgradeVariantHelper");
					if (i + 2 < codes.Count)
					{
						// Check if the code has already been patched by us?
						if (codes[i + 1].opcode == op1.opcode && codes[i + 1].operand == op1.operand)
						{
							// Do some heuristics as we may not reference the same function call
							if (codes[i + 2].opcode == OpCodes.Call && codes[i + 2].operand.ToString().Contains("UpgradeVariantHelper"))
							{
								break;
							}
						}
					}
					// Insert new code line
					codes.Insert(i + 1, op1);
					codes.Insert(i + 2, op2);
					Log.Out("Patched BlockPowered:EventData_Event");
					// Finished patching
					break;
				}
			}
			return codes;
		}
	}

	/*************************************************************************/
	// Below are a few advanced transpiler patches
	// Fixing XUiC classes to handle null TileEnity
	/*************************************************************************/

	// Move TileEntity Access into special if clause
	[HarmonyPatch(typeof(XUiC_PowerRangedAmmoSlots))]
	[HarmonyPatch("OnClose")]
	public static class XUiC_PowerRangedAmmoSlots_OnClose
	{
		static IEnumerable<CodeInstruction> Transpiler
			(IEnumerable<CodeInstruction> instructions)
		{
			var codes = new List<CodeInstruction>(instructions);
			for (var i = 0; i < codes.Count - 7; i++)
			{
				if (codes[i].opcode != OpCodes.Ldarg_0) continue;
				if (codes[i+1].opcode != OpCodes.Ldfld) continue;
				if (codes[i+2].opcode != OpCodes.Callvirt) continue;
				if (codes[i+3].opcode != OpCodes.Stloc_1) continue;
				if (codes[i+4].opcode != OpCodes.Ldsfld) continue;
				if (codes[i+5].opcode != OpCodes.Brtrue) continue;
				Log.Out("Patched XUiC_PowerRangedAmmoSlots:OnClose");
				codes[i + 4].labels.
					AddRange(codes[i].labels);
				codes[i].labels.Clear();
				// Move line inside if clause
				var ops = codes.GetRange(i, 4);
				codes.RemoveRange(i, 4);
				codes.InsertRange(i + 2, ops);
				break;
			}
			return codes;
		}
	}

	// Move TileEntity Access into special if clause
	[HarmonyPatch(typeof(XUiC_PowerRangedTrapOptions))]
	[HarmonyPatch("OnClose")]
	public static class XUiC_PowerRangedTrapOptions_OnClose
	{
		static IEnumerable<CodeInstruction> Transpiler
			(IEnumerable<CodeInstruction> instructions)
		{
			var codes = new List<CodeInstruction>(instructions);
			for (var i = 0; i < codes.Count - 7; i++)
			{
				if (codes[i].opcode != OpCodes.Ldarg_0) continue;
				if (codes[i + 1].opcode != OpCodes.Ldfld) continue;
				if (codes[i + 2].opcode != OpCodes.Callvirt) continue;
				if (codes[i + 3].opcode != OpCodes.Stloc_1) continue;
				if (codes[i + 4].opcode != OpCodes.Ldsfld) continue;
				if (codes[i + 5].opcode != OpCodes.Brtrue) continue;
				Log.Out("Patched XUiC_PowerRangedTrapOptions:OnClose");
				var ops = codes.GetRange(i, 4);
				codes.RemoveRange(i, 4);
				codes.InsertRange(i + 2, ops);
				break;
			}
			return codes;
		}
	}

	// Move TileEntity Access into special if clause
	[HarmonyPatch(typeof(XUiC_PowerRangedTrapWindowGroup))]
	[HarmonyPatch("OnClose")]
	public static class XUiC_PowerRangedTrapWindowGroup_OnClose
	{
		static IEnumerable<CodeInstruction> Transpiler
			(IEnumerable<CodeInstruction> instructions, ILGenerator il)
		{
			var codes = new List<CodeInstruction>(instructions);
			for (var i = 0; i < codes.Count - 7; i++)
			{
				if (codes[i].opcode != OpCodes.Ldarg_0) continue;
				if (codes[i + 1].opcode != OpCodes.Call) continue;
				if (codes[i + 2].opcode != OpCodes.Callvirt) continue;
				if (codes[i + 2].operand.ToString() != "Vector3i ToWorldPos()") continue;
				// Prepend an exit branch here
				Label label = il.DefineLabel();
				codes[i].labels.Add(label);
				// Prepend exit condition and branching
				codes.Insert(i, new CodeInstruction(OpCodes.Ret));
				codes.Insert(i, new CodeInstruction(OpCodes.Brtrue_S, label));
				codes.Insert(i, new CodeInstruction(codes[i + 3]));
				codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_0));
				Log.Out("Patched XUiC_PowerRangedTrapWindowGroup:OnClose");
				break;
			}
			return codes;
		}
	}

	/*************************************************************************/
	// Below is a fix to keep parent position up to date
	// Seems originally only used on client side (fix for server)
	/*************************************************************************/

	// Update position on set parent
	[HarmonyPatch(typeof(PowerManager))]
	[HarmonyPatch("SetParent")]
	[HarmonyPatch(new System.Type[] {
		typeof(PowerItem), typeof(PowerItem) })]
	public class PowerManager_SetParent
	{
		public static void Postfix(PowerItem child, PowerItem parent)
		{
			if (child.TileEntity == null) return;
			Vector3i position = new Vector3i(-9999, -9999, -9999);
			if (parent != null) position = parent.Position;
			FieldParentPosition.SetValue(child.TileEntity, position);
		}
	}

	// Reset position on remove parent
	[HarmonyPatch(typeof(PowerManager))]
	[HarmonyPatch("RemoveParent")]
	public class PowerManager_RemoveParent
	{
		public static void Postfix(PowerItem node)
		{
			FieldParentPosition.SetValue(node.TileEntity,
				new Vector3i(-9999, -9999, -9999));
		}
	}

}


// Part of Remote Turret Control Mod
// Copyright 2022 Marcel Greter

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockRemoteTurret : BlockPowered
{

	// Switch from single to full locks
	// Only here since I already had it
	public bool LockSingleTurret = true;

	// How far way to consider in proximity
	public float MaxDistance = 15f;

	// Interval at which cameras are rotated
	public float ScreenCameraInterval = 3f;

	// The list of remote turrets we are current scrolling trough
	public readonly static List<TileEntityPoweredRangedTrap>
		RemoteTurrets = new List<TileEntityPoweredRangedTrap>();
	// The list of attached control panels, reserved for future extensions
	// Note: the first entry is guaranteed to contain the main (our) panel
	public readonly static List<TileEntityPowered>
		ControlPanels = new List<TileEntityPowered>();

	// The current block of our panel, used to retrieve config stuff
	public static BlockRemoteTurret CurrentOpenBlock = null;
	// The current panel (same as ControlPanels[0]) to access everything
	public static TileEntityPowered CurrentOpenPanel = null;
	// Remember panel we had open last to continue on view/scroll position
	public static TileEntityPowered LastPanelOpen = null;
	// public BlockRemoteTurret() => IsRandomlyTick = false;

	private readonly Dictionary<string, Tuple<string, int>> AmmoPerks
		= new Dictionary<string, Tuple<string, int>>();
	private readonly Dictionary<string, Tuple<ProgressionValue, int>> PlayerPerks
		= new Dictionary<string, Tuple<ProgressionValue, int>>();

	// Key-Mappings to switch turret cameras
	public KeyCode KeyMapPrev = KeyCode.A;
	public KeyCode KeyMapNext = KeyCode.D;

	// Main configuration for control screen
	public Color ScreenAlbedoColor = Color.magenta;
	public Color ScreenAlbedoColorOn = Color.magenta;
	public Color ScreenAlbedoColorCam = Color.magenta;
	public Color ScreenAlbedoColorEffect = Color.magenta;
	public Color ScreenEmissionColor = Color.clear;
	public Color ScreenEmissionColorOn = Color.clear;
	public Color ScreenEmissionColorCam = Color.clear;
	public Color ScreenEmissionColorEffect = Color.clear;
	public RemoteTurretPanel.ShowScreen ScreenShownWhen;
	public Color ScreenEffectColor1 = Color.gray;
	public Color ScreenEffectColor2 = Color.green;

	// Compute on init from distance
	private int MaxDistanceSquared = 225;

	public override void Init()
	{
		base.Init();
		// Distance until screen is turned "off"
		MaxDistance = 15f; // Reset default in init
		Properties.ParseFloat("ScreenMaxDistance", ref MaxDistance);
		MaxDistanceSquared = (int)(MaxDistance * MaxDistance + 0.5);
		// Switch locking mode per block if necessary (maybe one is buggy)
		Properties.ParseBool("ScreenLockSingle", ref LockSingleTurret);
		// Should not be lower than the tick interval (say 1 second)
		Properties.ParseFloat("ScreenCameraInterval", ref ScreenCameraInterval);
		// Completely hide the screen until condition is true
		Properties.ParseEnum("ScreenShownWhen", ref ScreenShownWhen);
		// Screen Albedo Alpha defines material transparency
		// Screen Albedo RGB is applied to Emission and Albedo
		// Screen Albedo RGB is also applied to screen effect
		// Screen Emission RGB is applied to final Emission
		Properties.ParseColorHex("ScreenAlbedoColor", ref ScreenAlbedoColor);
		ScreenAlbedoColorOn = ScreenAlbedoColorCam = ScreenAlbedoColorEffect = ScreenAlbedoColor;
		Properties.ParseColorHex("ScreenAlbedoColorOn", ref ScreenAlbedoColorOn);
		Properties.ParseColorHex("ScreenAlbedoColorCam", ref ScreenAlbedoColorCam);
		Properties.ParseColorHex("ScreenAlbedoColorEffect", ref ScreenAlbedoColorEffect);
		Properties.ParseColorHex("ScreenEmissionColor", ref ScreenEmissionColor);
		ScreenEmissionColorOn = ScreenEmissionColorCam = ScreenEmissionColorEffect = ScreenEmissionColor;
		Properties.ParseColorHex("ScreenEmissionColorOn", ref ScreenEmissionColorOn);
		Properties.ParseColorHex("ScreenEmissionColorCam", ref ScreenEmissionColorCam);
		Properties.ParseColorHex("ScreenEmissionColorEffect", ref ScreenEmissionColorEffect);
		// Effect colors for the two screen saver effects
		Properties.ParseColorHex("ScreenEffectColor1", ref ScreenEffectColor1);
		Properties.ParseColorHex("ScreenEffectColor2", ref ScreenEffectColor2);
		// Key-Mappings for next/previous turret
		// Only change if you encounter issues
		KeyMapPrev = KeyCode.A; KeyMapNext = KeyCode.D;
		Properties.ParseEnum("ScreenKeyMapPrev", ref KeyMapPrev);
		Properties.ParseEnum("ScreenKeyMapNext", ref KeyMapNext);
		if (Properties.Values.TryGetValue("AmmoPerks", out string ammos))
		{
			// Note: we don't allow whitespace!?
			foreach (var ammo in ammos.Split(','))
			{
				if (Properties.Values.TryGetValue(ammo + "Perk", out string skill))
				{
					int minLevel = 0;
					Properties.ParseInt(ammo + "PerkLevel", ref minLevel);
					AmmoPerks.Add(ammo, new Tuple<string, int>(skill, minLevel));
				}
			}
		}
	}

	// Only used for caching in below function
	private EntityPlayer Player = null;

	// Check if ammo type is (still) locked behind perk
	public bool IsAmmoLocked(EntityPlayer player, ItemClass ammo)
	{
		// Reset player perks if needed
		// Caching some player progression
		if (Player != player)
		{
			PlayerPerks.Clear();
			foreach (var kv in AmmoPerks)
			{
				var type = kv.Key;
				var skill = kv.Value.Item1;
				var level = kv.Value.Item2;
				var progression = player.Progression.GetProgressionValue(skill);
				if (progression != null) PlayerPerks.Add(type, new
					Tuple<ProgressionValue, int>(progression, level));
			}
			Player = player;
		}
		// Query against the cached progression object
		if (PlayerPerks.TryGetValue(ammo.GetItemName(),
			out Tuple<ProgressionValue, int> tuple))
		{
			return tuple.Item1.Level < tuple.Item2;
		}
		// Default value
		return false;
	}

	// Used by full lock mechanism
	public static void AddEntityToEntries(
	  List<Tuple<int, Vector3i, int>> entries,
	  TileEntity entity)
	{
		entries.Add(new Tuple<int, Vector3i, int>(
			entity.GetClrIdx(),
			entity.ToWorldPos(),
			entity.entityId
		));
	}

	// State variables representing our `OnOpen` state
	// We only allow certain transitions on some stages
	public static BlockRemoteTurret BlockToOpen = null;
	public static TileEntityPowered PanelToOpen = null;

	public override bool OnBlockActivated(
        string _commandName,
		WorldBase world,
		int cIdx,
		Vector3i position,
		BlockValue _blockValue,
		EntityPlayerLocal player)
	{
		if (_commandName == "activate")
		{
			CurrentOpenBlock = null;
			CurrentOpenPanel = null;
			RemoteTurretUtils.CollectTurrets(world,
				position, ControlPanels, RemoteTurrets);
			var te = world.GetTileEntity(cIdx, position);
			if (!(te is TileEntityPowered tep))
			{
				GameManager.ShowTooltip(player,
					Localization.Get("ttNoTurretConnected"),
					string.Empty, "ui_denied");
				return false;
			}
			// Check if panel is powered and has turrets
			if (!tep.IsPowered || RemoteTurrets.Count == 0)
			{
				GameManager.ShowTooltip(player,
					Localization.Get("ttNoTurretConnected"),
					string.Empty, "ui_denied");
				return false;
			}
			// Set `OnOpen` state
			BlockToOpen = this;
			PanelToOpen = tep;
			// Copied from vanilla
			player.AimingGun = false;
			// Acquire the locks
			if (LockSingleTurret)
			{
				// Simply lock our own panel first
				// Then we look into what turret to view
				GameManager.Instance.TELockServer(cIdx,
					position, tep.entityId, player.entityId);
			}
			else
			{
				List<Tuple<int, Vector3i, int>> locks = new List<Tuple<int, Vector3i, int>>();
				foreach (var item in ControlPanels) AddEntityToEntries(locks, item);
				foreach (var item in RemoteTurrets) AddEntityToEntries(locks, item);
				PanelLockAllManager.TELockServerAll((World)world, locks, player.entityId);
			}
			// Success
			return true;
		}
		else if (_commandName == "take")
		{
			TakeItemWithTimer(cIdx, position, _blockValue, player);
			return true;
		}
		return false;
	}

	// Persist the values once opened
	// Called from patched `OpenTileEntityUi`
	public static bool OnPanelOpen(LocalPlayerUI ui)
	{
		// Can't open if another block is already active
		if (CurrentOpenBlock != null) return false;
		// Can't open if we don't know which block to open
		if (BlockToOpen == null) return false;
		// Reset state after the fact
		CurrentOpenBlock = BlockToOpen;
		CurrentOpenPanel = PanelToOpen;
		BlockToOpen = null;
		PanelToOpen = null;
		// Open our window group now
		ui.windowManager.Open("remoteturret", true);
		// Success
		return true;
	}

	private new readonly BlockActivationCommand[] cmds = new BlockActivationCommand[2]
	{
		new BlockActivationCommand("activate", "tool", true),
		new BlockActivationCommand("take", "hand", false)
	};

	public override BlockActivationCommand[] GetBlockActivationCommands(
		WorldBase _world,
		BlockValue _blockValue,
		int _clrIdx,
		Vector3i _blockPos,
		EntityAlive _entityFocusing)
	{
		cmds[0].enabled = true;
		cmds[1].enabled = _world.IsMyLandProtectedBlock(_blockPos,
			_world.GetGameManager().GetPersistentLocalPlayer()) && TakeDelay > 0.0;
		return cmds;
	}

	public override string GetActivationText(
	  WorldBase _world,
	  BlockValue _blockValue,
	  int _clrIdx,
	  Vector3i _blockPos,
	  EntityAlive _entityFocusing)
	{
		if (!cmds[0].enabled) return "";
		Block block = _blockValue.Block;
		return string.Format(
			Localization.Get("tooltipInteract"),
			"{0}", block.GetLocalizedBlockName());
	}


	/****************************************************************************/
	// Below we implement a little proximity block activator
	// We keep a map of all loaded blocks (of this type) to evaluate
	// which ones are in proximity to the main (local) player. This
	// update is done in a coroutine which updates quite slowly.
	/****************************************************************************/

	IEnumerator UpdateCoroutine = null;
    readonly Dictionary<Vector3i, RemoteTurretPanel> Loaded
		= new Dictionary<Vector3i, RemoteTurretPanel>();

	private IEnumerator UpdateNext()
	{
		while (UpdateCoroutine != null)
		{
			if (LocalPlayerUI.GetUIForPrimaryPlayer()?
				.entityPlayer is EntityPlayerLocal player)
			{
				// Updating if player is near a loaded block
				// Only these blocks will render a camera view
				// ToDo: put yield inside this function?
				// ToDo: what happens if it is changed!?
				foreach (var kv in Loaded)
				{
					int distanceSq = Vector3DistSquared(
						kv.Key - player.GetBlockPosition());
					kv.Value.UpdateInterval = 1 + distanceSq / 2;
					kv.Value.Tick(MaxDistanceSquared > distanceSq);
				}
			}
			// Yield for another wait interval
			yield return new WaitForSeconds(0.68f);
		}
	}

	private int Vector3DistSquared(Vector3i v3i)
	{
		return v3i.x * v3i.x + v3i.y * v3i.y + v3i.z * v3i.z;
	}

	public override void OnBlockEntityTransformBeforeActivated(WorldBase _world,
		Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
	{
		base.OnBlockEntityTransformBeforeActivated(_world, _blockPos, _blockValue, _ebcd);
		if (Loaded.Count == 0)
		{
			UpdateCoroutine = UpdateNext();
			GameManager.Instance.StartCoroutine(UpdateCoroutine);
		}
		Loaded.Add(_blockPos, new RemoteTurretPanel(_world, _blockPos,
			_ebcd.transform.Find("Screen"), this));
	}

	public override void OnBlockUnloaded(WorldBase _world,
		int _clrIdx, Vector3i _blockPos, BlockValue _blockValue)
	{
		if (Loaded.TryGetValue(_blockPos, out var panel))
		{
			panel.Destroy();
			Loaded.Remove(_blockPos);
		}
		if (Loaded.Count == 0 && UpdateCoroutine != null)
		{
			GameManager.Instance.StopCoroutine(UpdateCoroutine);
			UpdateCoroutine = null;
		}
		base.OnBlockUnloaded(_world, _clrIdx, _blockPos, _blockValue);
	}

	public override void OnBlockRemoved(WorldBase world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
	{
		if (Loaded.TryGetValue(_blockPos, out var panel))
		{
			panel.Destroy();
			Loaded.Remove(_blockPos);
		}
		if (Loaded.Count == 0 && UpdateCoroutine != null)
		{
			GameManager.Instance.StopCoroutine(UpdateCoroutine);
			UpdateCoroutine = null;
		}
		base.OnBlockRemoved(world, _chunk, _blockPos, _blockValue);
	}

}

// Part of Remote Turret Control Mod
// Copyright 2022 Marcel Greter

using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class XUI_PowerRemoteTurretPanel : XUiC_PowerRangedTrapWindowGroup
{

    // The list of remote turrets we are current scrolling trough
    public List<TileEntityPoweredRangedTrap> RemoteTurrets => BlockRemoteTurret.RemoteTurrets;

    // The list of attached control panels, reserved for future extensions
    // Note: the first entry is guaranteed to contain the main (our) panel
    public List<TileEntityPowered> ControlPanels => BlockRemoteTurret.ControlPanels;

    // The current block of our panel, used to retrieve config stuff
    public BlockRemoteTurret CurrentBlock => BlockRemoteTurret.CurrentOpenBlock;

    // The current panel (same as ControlPanels[0]) to access everything
    public TileEntityPowered CurrentPanel => BlockRemoteTurret.CurrentOpenPanel;

    // Remember panel we had open last to continue on view/scroll position
    public TileEntityPowered LastPanel => BlockRemoteTurret.LastPanelOpen;

    // Reference to the camera preview window
    private XUiC_CameraWindow cameraWindowPreview;

    // Current slot we are viewing
    // We always have a lock on it
    private int CurrentTurretSlot = -1;

    private int LastSlotLocked = -1;
    private bool RequestPending = false;

    public override void Init()
    {
        base.Init();
        var preview = GetChildById("windowPowerCameraControlPreview");
        cameraWindowPreview = preview as XUiC_CameraWindow;
    }

    // Coroutine to close instantly
    // Needed to give a little timeout
    private IEnumerator CloseLater()
    {
        yield return new WaitForSeconds(0.0f);
        GameManager.ShowTooltip(xui.playerUI.localPlayer.entityPlayerLocal,
            Localization.Get("ttAllTurretsInUse"), string.Empty, "ui_denied");
        XUiC_CameraWindow.hackyIsOpeningMaximizedWindow = TileEntity == null;
        xui.playerUI.windowManager.Close("powercamera");
        xui.playerUI.windowManager.Close("powerrangedtrap");
        xui.playerUI.windowManager.Close("remoteturret");
        XUiC_CameraWindow.hackyIsOpeningMaximizedWindow = false;
        Destroy();
    }


    // Call on close for zooming in or closing for good
    public override void OnClose()
    {
        if (!XUiC_CameraWindow.hackyIsOpeningMaximizedWindow) ReleaseLocks();
        base.OnClose();
        if (XUiC_CameraWindow.hackyIsOpeningMaximizedWindow) return;
        foreach (var turret in RemoteTurrets) UnregisterTileEntity(turret);
        foreach (var panel in ControlPanels) UnregisterTileEntity(panel);
        BlockRemoteTurret.CurrentOpenBlock = null;
        BlockRemoteTurret.CurrentOpenPanel = null;
        RemoteTurrets.Clear();
        ControlPanels.Clear();
        if (TileEntity != null) EnableAutoTurret();
        if (WireManager.Instance == null) return;
        WireManager.Instance.RefreshPulseObjects();
        TileEntity = null; // Reset (Harmony Fixed)
    }

    public override void OnOpen()
    {
        // Check for entrance condition (must have one turret)
        // Shouldn't happen AFAICT, but play safe anyway
        if (RemoteTurrets == null || RemoteTurrets.Count == 0)
        {
            // Let open do it's job first, then close again
            GameManager.Instance.StartCoroutine(CloseLater());
            // Log.Out("Closing the window directly again");
            return;
        }

        // Check if we are opening the initial menu layer
        // Note: we get also called when zoom window is closed
        if (!XUiC_CameraWindow.hackyIsOpeningMaximizedWindow)
        {
            foreach (var turret in RemoteTurrets) RegisterTileEntity(turret);
            foreach (var panel in ControlPanels) RegisterTileEntity(panel);
        }

        // Check if we should lock single tiles
        if (BlockRemoteTurret.CurrentOpenBlock.LockSingleTurret)
        {
            if (LastSlotLocked == -1)
            {
                CurrentTurretSlot = -1;
                ShowRemoteSlot(0, true);
            }
            else
            {
                // Reset on children first
                TileEntity = TileEntity;
                // Must have TileEntity?
                base.OnOpen();
            }
        }
        // All locked at once
        // Can freely switch
        else
        {
            if (CurrentPanel != LastPanel)
            {
                BlockRemoteTurret.LastPanelOpen = CurrentPanel;
                CurrentTurretSlot = 0;
            }
            if (RemoteTurrets != null && RemoteTurrets.Count > 0)
            {
                CurrentTurretSlot = WrapSlot(CurrentTurretSlot);
                TileEntity = RemoteTurrets[CurrentTurretSlot];
                DisableAutoTurret(TileEntity);
            }
            // We already hold all locks
            // Ensures we pass further tests
            LastSlotLocked = CurrentTurretSlot;
            // Reset on children first
            TileEntity = TileEntity;
            // Open it now
            base.OnOpen();
        }
    }

    // Give up locks voluntarily
    private void ReleaseLocks()
    {
        if (CurrentBlock.LockSingleTurret)
        {
            if (LastSlotLocked != -1)
            {
                var te = RemoteTurrets[LastSlotLocked];
                GameManager.Instance.TEUnlockServer(
                    te.GetClrIdx(),
                    te.ToWorldPos(),
                    te.entityId);
                LastSlotLocked = -1;
            }
            if (CurrentPanel != null)
            {
                GameManager.Instance.TEUnlockServer(
                    CurrentPanel.GetClrIdx(),
                    CurrentPanel.ToWorldPos(),
                    CurrentPanel.entityId);
            }
        }
        else 
        {
            World world = GameManager.Instance.World;
            var unlocks = new List<System.Tuple<int, Vector3i, int>>();
            foreach (var item in ControlPanels) BlockRemoteTurret.AddEntityToEntries(unlocks, item);
            foreach (var item in RemoteTurrets) BlockRemoteTurret.AddEntityToEntries(unlocks, item);
            PanelLockAllManager.TEUnlockServerAll(world, unlocks);
            LastSlotLocked = -1; // Irrelevant in this mode!
        }
    }

    private void Destroy()
    {
        ReleaseLocks(); // Give up any locks
        foreach (var turret in RemoteTurrets) UnregisterTileEntity(turret);
        foreach (var panel in ControlPanels) UnregisterTileEntity(panel);
        xui.playerUI.windowManager.Close("powercamera");
        xui.playerUI.windowManager.Close("powerrangedtrap");
        xui.playerUI.windowManager.Close("remoteturret");
        BlockRemoteTurret.CurrentOpenBlock = null;
        BlockRemoteTurret.CurrentOpenPanel = null;
        RemoteTurrets.Clear();
        ControlPanels.Clear();
    }

    private void TileEntityDestroyed(TileEntity te)
    {
        Destroy();
    }

    private void RegisterTileEntity(TileEntityPowered te)
    {
        te.Destroyed += TileEntityDestroyed;
    }

    private void UnregisterTileEntity(TileEntityPowered te)
    {
        te.Destroyed -= TileEntityDestroyed;
    }

    private void EnableAutoTurret()
    {
        if (FieldCamController.GetValue(cameraWindowPreview) is IPowerSystemCamera camctr)
        {
            camctr.SetConeActive(false);
            camctr.SetLaserActive(false);
            camctr.SetConeColor(camctr.GetOriginalConeColor());
            camctr.SetUserAccessing(false);
            if (camctr is AutoTurretController tctr) tctr.IsOn = false;
        }
        if (TileEntity == null) return;
        TileEntity.SetUserAccessing(false);
        TileEntity.SetModified();
    }

    private void DisableAutoTurret(TileEntityPoweredRangedTrap turret)
    {
        if (FieldCamController.GetValue(cameraWindowPreview) is IPowerSystemCamera camctr)
        {
            camctr.SetConeColor(Color.clear);
            camctr.SetConeActive(true);
            camctr.SetLaserActive(true);
            camctr.SetUserAccessing(true);
            if (camctr is AutoTurretController tctr) tctr.IsOn = false;
        }
        if (turret != null) turret.SetUserAccessing(true);
        if (TileEntity != null) TileEntity.SetUserAccessing(true);
        if (TileEntity != null) TileEntity.SetModified();
    }

    private bool IsConnected(TileEntityPowered te)
    {
        if (te == null) return false;
        // if (!te.IsPowered) return false;
        World world = GameManager.Instance.World;
        while (te != null)
        {
            if (te.HasParent() && world.GetTileEntity(
                0, te.GetParent()) is TileEntityPowered parent)
            {
                if (ControlPanels.IndexOf(parent) != -1) return true;
                te = parent;
            }
            else break;
        }
        return false;
    }

    private int WrapSlot(int slot)
    {
        while (slot < 0)
            slot += RemoteTurrets.Count;
        while (slot >= RemoteTurrets.Count)
            slot -= RemoteTurrets.Count;
        return slot;
    }

    private bool pressed_prev = false;
    private bool pressed_next = false;
    
    private static readonly FieldInfo FieldSensorCamera = AccessTools
        .Field(typeof(XUiC_CameraWindow), "sensorCamera");
    private static readonly FieldInfo FieldCamController = AccessTools
        .Field(typeof(XUiC_CameraWindow), "cameraController");
    private static readonly FieldInfo FieldCamParentTransform = AccessTools
        .Field(typeof(XUiC_CameraWindow), "cameraParentTransform");

    public void SetCurrentTurretSlot(int slot)
    {
        if (CurrentTurretSlot != slot)
        {
            CurrentTurretSlot = slot;
            // Restore turret auto-aim support
            if (TileEntity != null) EnableAutoTurret();
            // Make sure to bring it into range
            var turret = RemoteTurrets[CurrentTurretSlot];
            // Close potential zoomed in camera (Superfluous?)
            // xui.playerUI.windowManager.Close("powercamera");
            TileEntity = turret; // Assign the new tile entity
            // Update the camera (maybe there is a smarter way?)
            Camera cam = (Camera)FieldSensorCamera.GetValue(cameraWindowPreview);
            if (cam != null) Object.DestroyImmediate(cam.gameObject);
            // Stuff gets re-created on Update inside camera controller
            FieldSensorCamera.SetValue(cameraWindowPreview, null);
            FieldCamParentTransform.SetValue(cameraWindowPreview, null);
            // Make sure to update the `IPowerSystemCamera` controller
            FieldCamController.SetValue(cameraWindowPreview,
                turret.BlockTransform.GetComponent<IPowerSystemCamera>());
            // Enable turret aiming after switching
            DisableAutoTurret(turret);
        }
        else
        {
            // Just for safety measures
            TileEntity = TileEntity;
        }
    }

    static readonly FieldInfo FieldLockedTileEntities = AccessTools.Field(typeof(GameManager), "lockedTileEntities");

    public static Dictionary<TileEntity, int> GetServerLocks()
    {
        return (Dictionary<TileEntity, int>)FieldLockedTileEntities.GetValue(GameManager.Instance);
    }

    // Call as a remote function call to acquire new and release old lock
    static public bool LockForClient(int clrIdx, Vector3i position, int entityId, Vector3i previous)
    {
        if (previous == position) return true;
        var world = GameManager.Instance.World;
        if (world.GetTileEntity(clrIdx, position) is TileEntityPowered tep)
        {
            var locked = GetServerLocks(); // Get server-side lock state object
            if (locked.TryGetValue(tep, out int locker) && locker != entityId) return false;
            if (world.GetTileEntity(clrIdx, previous) is TileEntityPowered old) locked.Remove(old);
            locked[tep] = entityId; // Finally acquire the server-side lock
            return true;
        }
        return false;
    }

    public void ShowRemoteSlot(int slot, bool first)
    {
        slot = WrapSlot(slot);
        if (RemoteTurrets == null || RemoteTurrets.Count == 0)
        {
            CurrentTurretSlot = 0;
        }
        else if (CurrentBlock.LockSingleTurret)
        {
            // ToDo: Verify again that this works fine (verified after TileEntity reset)
            // Only close if starting from nowhere, otherwise we already have one lock
            if (first == false && ((CurrentTurretSlot == -1 && slot == 0)))
            {
                GameManager.ShowTooltip(xui.playerUI.localPlayer.entityPlayerLocal,
                    Localization.Get("ttAllTurretsInUse"), string.Empty, "ui_denied");
                XUiC_CameraWindow.hackyIsOpeningMaximizedWindow = TileEntity == null;
                Destroy(); //OnClose();
                XUiC_CameraWindow.hackyIsOpeningMaximizedWindow = false;
                return;
            }
            // We are the server, can do it directly
            if (ConnectionManager.Instance.IsServer)
            {
                // Get server-side lock state object
                var locked = GetServerLocks();
                // Try to look one of the next remote turrets
                // Will basically try until we hit our self again
                for (var i = 0; i < RemoteTurrets.Count; i ++)
                {
                    // Work from current forward
                    var lck = WrapSlot(slot + i);
                    // Try to acquire the lock for this next slot
                    TileEntityPoweredRangedTrap todo = RemoteTurrets[lck];
                    if (locked.TryGetValue(todo, out int locker))
                    {
                        // Check if tile is already locked by somebody else
                        if (locker != xui.playerUI.entityPlayer.entityId) continue;
                        // Otherwise the lock is our own and we can proceed
                    }
                    else
                    {
                        // Otherwise we have acquired our next lock
                        locked[todo] = xui.playerUI.entityPlayer.entityId;
                    }
                    // Check if we have a previous slot locked
                    if (LastSlotLocked != -1)
                    {
                        // If so we need to remove that lock again
                        locked.Remove(RemoteTurrets[LastSlotLocked]);
                    }
                    // Remember new lock
                    LastSlotLocked = lck;
                    // We can now show the slot
                    SetCurrentTurretSlot(lck);
                    // Now call our base open
                    base.OnOpen();
                    // Success
                    return;
                }
                // TileEntity = null;
                // Nothing could be locked, close the window
                GameManager.Instance.StartCoroutine(CloseLater());
            }
            // We are the client only part
            // Acquire lock at the server
            else
            {
                // Check if we already own a lock
                if (LastSlotLocked != -1)
                {
                    // Get the tile entity holding the lock
                    var te = RemoteTurrets[LastSlotLocked];
                    // Inform server that lock is released
                    GameManager.Instance.TEUnlockServer(
                        te.GetClrIdx(),
                        te.ToWorldPos(),
                        te.entityId);
                    // Remember just in case
                    LastSlotLocked = -1;
                }

                // We go to request new lock
                RequestPending = true;
                // Get the tile entity for the new lock
                TileEntity todo = RemoteTurrets[slot];
                // Remove the old lock in the same call
                Vector3i old = LastSlotLocked == -1 ? Vector3i.invalid :
                    RemoteTurrets[LastSlotLocked].ToWorldPos();
                // Setup data for remote function call
                int locker = xui.playerUI.entityPlayer.entityId;
                string fqfn = "XUI_PowerRemoteTurretPanel:LockForClient";
                NetServerAction.RemoteServerCall(fqfn, new object[] {
                    todo.GetClrIdx(), todo.ToWorldPos(), locker, old },
                    (rv) => { // Success
                        RequestPending = false;
                        if ((bool)rv)
                        {
                            LastSlotLocked = slot;
                            SetCurrentTurretSlot(slot);
                            base.OnOpen();
                        }
                        else
                        {
                            // Access denied, try next
                            ShowRemoteSlot(slot + 1, false);
                        }
                    },
                    (error) => { // Error
                        RequestPending = false;
                        Destroy();
                    },
                    () => { // Timeout
                        RequestPending = false;
                        // Play safe, probably not needed
                        GameManager.Instance.TEUnlockServer(
                            todo.GetClrIdx(),
                            todo.ToWorldPos(),
                            todo.entityId);
                        Destroy();
                    },
                    locker, 8);
            }
        }
        else
        {
            LastSlotLocked = slot;
            SetCurrentTurretSlot(slot);
            base.OnOpen();
        }
    }

    public override void Update(float _dt)
    {

        if (CurrentBlock == null) return;
        if (RemoteTurrets == null) return;
        if (LastSlotLocked == -1) return;
        if (RemoteTurrets.Count == 0) return;
        if (ControlPanels.Count == 0) return;

        base.Update(_dt);

        if (RemoteTurrets.Count < 2) return;

        if (CurrentBlock != null && !RequestPending)
        {
            if (Input.GetKeyDown(CurrentBlock.KeyMapPrev))
            {
                pressed_prev = true;
                pressed_next = false;
            }
            else if (Input.GetKeyUp(CurrentBlock.KeyMapPrev))
            {
                if (pressed_prev == true)
                {
                    var slot = WrapSlot(CurrentTurretSlot - 1);
                    ShowRemoteSlot(slot, false);
                    pressed_prev = false;
                }
            }
            if (Input.GetKeyDown(CurrentBlock.KeyMapNext))
            {
                pressed_prev = false;
                pressed_next = true;
            }
            else if (Input.GetKeyUp(CurrentBlock.KeyMapNext))
            {
                if (pressed_next == true)
                {
                    var slot = WrapSlot(CurrentTurretSlot + 1);
                    ShowRemoteSlot(slot, false);
                    pressed_next = false;
                }
            }
        }

        if (RemoteTurrets != null && RemoteTurrets.Count > 0)
        {
            if (CurrentTurretSlot >= 0 && CurrentTurretSlot < RemoteTurrets.Count)
            {
                // Check if our tile is still connected
                if (!IsConnected(RemoteTurrets[CurrentTurretSlot]))
                {
                    xui.playerUI.windowManager.Close("powercamera");
                    xui.playerUI.windowManager.Close("powerrangedtrap");
                }
            }
        }

    }
}

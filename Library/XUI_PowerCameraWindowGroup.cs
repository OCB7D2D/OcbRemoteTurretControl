// Part of Remote Turret Control Mod
// Copyright 2022 Marcel Greter

public class XUI_PowerCameraWindowGroup : XUiC_PowerCameraWindowGroup
{
    public override void OnClose()
    {
        string group = XUiC_CameraWindow.lastWindowGroup;
        XUiC_CameraWindow.lastWindowGroup = "powerrangedtrap";
        base.OnClose(); // Fixing TileEntity update
        XUiC_CameraWindow.lastWindowGroup = group;
        // Harmony patch in GUIWindowManager.Open
    }
    public override void Update(float _dt)
    {
        base.Update(_dt);
        // Close zoomed view if power is switched off
        if (!TileEntity.IsPowered) xui.playerUI
            .windowManager.Close("powercamera");
    }

}

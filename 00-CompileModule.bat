@echo off

call MC7D2D RemoteTurretControl.dll ^
/reference:"%PATH_7D2D_MANAGED%\Assembly-CSharp.dll" ^
LibRemoteAction\*.cs LibThrottleCams\*.cs ^
Harmony\*.cs Library\*.cs Utils\*.cs && ^
echo Successfully compiled RemoteTurretControl.dll

pause
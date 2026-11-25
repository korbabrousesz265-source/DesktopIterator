# 证据包

- 程序集: RainWorld_Data\Managed\Assembly-CSharp.dll
- SHA-256: b6be1d4e18ce219d21091b51564cb6a11c1e4106b41de903eb8e58849cb16fdb
- 游戏版本: v1.11.8 (StreamingAssets\GameVersion.txt)
- 反编译: ilspycmd 11.2.0 (REA 在本机对 managed PE 返回 provider_unavailable: Ghidra 不支持 managed, Hopper 不支持 win32, IDA/Kuna 未配置)
- 导出: Oracle.cs.txt (OracleArm.Joint / Joint.Update / ElbowPos), OracleGraphics.cs.txt (ArmJointGraphics / ArmBase / UbilicalCord / 头部与侧件), Custom.cs.txt (VecToDeg / PerpendicularVector / InverseKinematic 调用约定)

涉及的方法: Oracle.OracleArm.Joint..ctor / Update / ElbowPos; OracleGraphics.ArmJointGraphics.InitiateSprites / DrawSprites / ApplyPalette; OracleGraphics.ArmBase.InitiateSprites / DrawSprites / UpdateMesh / ApplyPalette; OracleGraphics.UbilicalCord..ctor / Update / InitiateSprites / DrawSprites / ApplyPalette; OracleGraphics.InitiateSprites / DrawSprites / ApplyPalette (头部、下巴、眼、颈、侧件); Custom.VecToDeg / AimFromOneVectorToAnother / PerpendicularVector / InverseKinematic。

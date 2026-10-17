# 火堆角色资源与代码契约

此控制台测试只读取DLL元数据和源码，不执行Godot或游戏。检查角色火堆路径覆盖、工厂节点层级、每角色注册/缓存、UI及三幕动画名。不能代替实际C#工厂执行或联机UI实测。

```powershell
dotnet run --project tests/CampfireVisualRegression/CampfireContracts.csproj -- "发布目录/CodeFarmer/CodeFarmer.dll" . MySts2Mod/MySts2Mod/animations/coding_farmer/hao.spjson campfire-report.json
```

本次还通过隔离的原生Godot/Spine加载、22骨骼采样和真实渲染检查；报告见docs/character-animation/2026-10-17-campfire-attack.md。

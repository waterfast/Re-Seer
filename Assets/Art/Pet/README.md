# 精灵图片

固定结构：`avatar/精灵编号.png` 为头像，`pets/精灵编号.png` 为原图或战斗首帧。例如 `avatar/70.png`、`pets/70.png` 是雷伊。编号取当前战斗数据的 `BattlePetState.Id`。

本次迁入官网已取得的 98 张头像、66 张原图，PNG 和 `.meta` 一起移动，保留原 GUID 与场景引用。70、261 来自官方 Unity 资源包；其余来自 Flash 静态导出。有些编号只有头像，原图查询返回 null，不用别的精灵图片代替。

来源与哈希仍见 `Assets/Art/Battle/sources.json`、`Assets/Art/Battle/Flash/TestCollection/sources.json` 和该目录的 `Pets/catalog.json`，本地路径已更新。以后采集脚本也输出到本目录。

`PetArtResources` 统一查找图片。编辑器自动生成 `Assets/Resources/PetArtCatalog.asset`，构建前再次更新，确保 Art 图片进入发布包。新增 PNG 请导入为 Single Sprite，100 Pixels Per Unit；默认图片不用逐个拖进组件。

自定义目录下也使用 `avatar/编号.png`、`pets/编号.png`。`SetCustomDirectory` 切换并通知当前 HPBar 重读头像；缺图或图片损坏回退默认素材，两边都没有时返回 null。再调用同一目录可刷新缓存；传 null 恢复默认。当前支持本地文件系统目录，未接入玩家文件夹选择界面或 WebGL 浏览器文件授权。

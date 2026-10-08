"""Create the first-stage scene from the current battle layout with explicit bindings."""
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"


def meta(path):
    sidecar = Path(str(path) + ".meta")
    if not sidecar.exists():
        content = "fileFormatVersion: 2\nguid: " + uuid.uuid4().hex + "\n"
        if path.is_dir():
            content += "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        elif path.suffix == ".cs":
            content += "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        else:
            content += "DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        sidecar.write_text(content, encoding="utf-8")
    return re.search(r"guid: (\w+)", sidecar.read_text(encoding="utf-8")).group(1)


def main():
    for directory in (ASSETS / "Scripts/Network", ASSETS / "Scripts/Battle/Network"):
        meta(directory)
        for source in directory.glob("*.cs"):
            meta(source)
    guid = meta(ASSETS / "Scripts/Battle/Network/BattleNetworkController.cs")
    scene = (ASSETS / "Scenes/Battle.unity").read_text(encoding="utf-8")
    # These component IDs come from the source scene. Fail visibly if its layout changes.
    marker = "  - component: {fileID: 1498398470}\n"
    assert scene.count(marker) == 1
    scene = scene.replace(marker, marker + "  - component: {fileID: 201200001}\n")
    scene = scene.replace("  autoStartOnPlay: 1", "  autoStartOnPlay: 0")
    scene = scene.replace("  enableKeyboardShortcuts: 1", "  enableKeyboardShortcuts: 0")
    # Serialize the existing panel reference even though it belongs to a prefab instance.
    panel_guid = meta(ASSETS / "Scripts/Battle/UI/MainActionPanel.cs")
    prefab = ASSETS / "Prefabs/BattleUI/MainActionPanel.prefab"
    prefab_guid = meta(prefab)
    blocks = re.split(r"(?=--- !u!)", prefab.read_text(encoding="utf-8"))
    panel = next(block for block in blocks if "guid: " + panel_guid in block)
    panel_id = re.search(r"--- !u!114 &(\d+)", panel).group(1)
    instance = next(block for block in re.split(r"(?=--- !u!)", scene)
                    if "m_SourcePrefab:" in block and "guid: " + prefab_guid in block)
    instance_id = re.search(r"--- !u!1001 &(\d+)", instance).group(1)
    scene += f"""--- !u!114 &201200002 stripped
MonoBehaviour:
  m_CorrespondingSourceObject: {{fileID: {panel_id}, guid: {prefab_guid}, type: 3}}
  m_PrefabInstance: {{fileID: {instance_id}}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {panel_guid}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
--- !u!114 &201200001
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1498398467}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  host: 127.0.0.1
  port: 9527
  skillDatabase: {{fileID: 11400000, guid: 8490498f80d9d0b4091e57eb7fac6ab9, type: 2}}
  ui: {{fileID: 1498398469}}
  actionPanel: {{fileID: 201200002}}
  connectOnStart: 1
  showConnectionControls: 1
"""
    # Resolve GameObject ID from the controller itself instead of guessing it.
    controller_block = next(block for block in re.split(r"(?=--- !u!)", scene)
                            if block.startswith("--- !u!114 &1498398469\n"))
    game_object = re.search(r"m_GameObject: \{fileID: (\d+)\}", controller_block).group(1)
    scene = scene.replace("  m_GameObject: {fileID: 1498398467}", "  m_GameObject: {fileID: " + game_object + "}")
    # Correct the existing misspelling so the self damage display can be resolved.
    scene = scene.replace("value: SelfNumberDispaly", "value: SelfDamageDisplay")
    destination = ASSETS / "Scenes/BattleNetwork.unity"
    destination.write_text(scene, encoding="utf-8")
    meta(destination)
    print("Created", destination)


if __name__ == "__main__":
    main()

import re
import sys

def find_mesh_colliders(scene_path):
    with open(scene_path, 'r') as f:
        content = f.read()

    # Find all MeshCollider components
    mesh_colliders = re.findall(r'MeshCollider:.*?m_GameObject: {fileID: (\d+)}', content, re.DOTALL)
    
    # Map GameObjects
    gameobjects = {}
    for match in re.finditer(r'--- !u!1 &(\d+).*?m_Name: (.*?)\n', content, re.DOTALL):
        gameobjects[match.group(1)] = match.group(2)

    print("GameObjects with MeshColliders:")
    for go_id in mesh_colliders:
        name = gameobjects.get(go_id, f"Unknown({go_id})")
        print(f"- {name}")

if __name__ == "__main__":
    find_mesh_colliders(sys.argv[1])

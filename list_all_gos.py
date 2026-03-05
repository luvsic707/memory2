import re
import sys

def list_gameobjects(scene_path):
    with open(scene_path, 'r') as f:
        content = f.read()

    # Map GameObjects
    gameobjects = []
    # Find all GameObject blocks and their names
    # GameObject blocks begin with --- !u!1 &ID
    # Names are in m_Name: [Name]
    
    # Simple regex to find names. Note that names can be quoted.
    matches = re.finditer(r'm_Name: (.*)', content)
    names = [m.group(1).strip() for m in matches]
    
    # Also find prefab modifications that set names
    prefab_names = re.findall(r'propertyPath: m_Name\s+value: (.*)', content)
    
    print("Direct GameObjects:")
    for n in sorted(list(set(names))):
        print(f"- {n}")
        
    print("\nPrefab Name Modifications:")
    for n in sorted(list(set(prefab_names))):
        print(f"- {n}")

if __name__ == "__main__":
    list_gameobjects(sys.argv[1])

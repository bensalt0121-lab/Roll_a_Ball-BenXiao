These are the original Blender files for my models. Unity does not use them directly
(so the project also works on computers without Blender). Unity uses the .fbx copies
in Assets/prefab instead.

To change a model:
1. Open the .blend file here in Blender and edit it.
2. File > Export > FBX, and save over the matching file in Assets/prefab
   (same name, for example "road light.fbx"). Keep the default export settings.
3. Unity updates the model everywhere in the scene automatically.

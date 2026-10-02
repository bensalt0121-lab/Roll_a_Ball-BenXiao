/*********************************************************************************************
 * COMPONENT OF: City NavMesh (made by the setup tool)
 * REQUIRED DEPENDENCIES: NavMeshSurface on the same object (AI Navigation package)
 * DESCRIPTION: Builds the walkable area for NPCs and police when the game starts, so the
 *              map can be changed freely without remembering to re-bake the NavMesh.
 *              Runs before other scripts so the NavMesh is ready when people spawn.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using Unity.AI.Navigation;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(NavMeshSurface))]
public class RuntimeNavMesh : MonoBehaviour
{
    void Awake()
    {
        BuildIfMissing();
    }

    // Only builds when nobody baked a NavMesh in the editor
    void BuildIfMissing()
    {
        NavMeshSurface surface = GetComponent<NavMeshSurface>();

        if (surface.navMeshData == null)
            surface.BuildNavMesh();
    }
}

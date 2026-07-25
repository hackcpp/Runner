using System.Collections.Generic;
using UnityEngine;

public sealed class RunnerWorldPool
{
    private readonly Transform activeRoot;
    private readonly Transform poolRoot;
    private readonly Stack<GameObject> cubes = new Stack<GameObject>();
    private readonly Stack<GameObject> obstacleRoots = new Stack<GameObject>();

    public RunnerWorldPool(Transform configuredActiveRoot)
    {
        activeRoot = configuredActiveRoot;
        GameObject poolObject = new GameObject("Runner World Pool");
        poolObject.transform.SetParent(activeRoot, false);
        poolRoot = poolObject.transform;
    }

    public int PooledCubeCount => cubes.Count;
    public int PooledObstacleRootCount => obstacleRoots.Count;
    public int TotalCreatedCubeCount { get; private set; }
    public int TotalCreatedObstacleRootCount { get; private set; }

    public GameObject AcquireCube(
        string objectName,
        Vector3 position,
        Vector3 scale,
        Material material)
    {
        GameObject cube;
        if (cubes.Count > 0)
        {
            cube = cubes.Pop();
        }
        else
        {
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            RemovePhysicsCollider(cube);
            TotalCreatedCubeCount++;
        }

        cube.name = objectName;
        cube.transform.SetParent(activeRoot);
        cube.transform.position = position;
        cube.transform.rotation = Quaternion.identity;
        cube.transform.localScale = scale;
        SetMaterial(cube, material);
        cube.SetActive(true);
        return cube;
    }

    public GameObject AcquireObstacleRoot(string objectName, Vector3 position)
    {
        GameObject root;
        if (obstacleRoots.Count > 0)
        {
            root = obstacleRoots.Pop();
        }
        else
        {
            root = new GameObject();
            TotalCreatedObstacleRootCount++;
        }

        root.name = objectName;
        root.transform.SetParent(activeRoot);
        root.transform.position = position;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        root.SetActive(true);
        return root;
    }

    public void ReleaseObstacleRoot(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        while (root.transform.childCount > 0)
        {
            ReleaseCube(root.transform.GetChild(root.transform.childCount - 1).gameObject);
        }

        root.SetActive(false);
        root.transform.SetParent(poolRoot);
        obstacleRoots.Push(root);
    }

    public void ReleaseCube(GameObject cube)
    {
        if (cube == null)
        {
            return;
        }

        cube.SetActive(false);
        cube.transform.SetParent(poolRoot);
        cube.transform.localPosition = Vector3.zero;
        cube.transform.localRotation = Quaternion.identity;
        cube.transform.localScale = Vector3.one;
        cubes.Push(cube);
    }

    public static void RemovePhysicsCollider(GameObject target)
    {
        Collider physicsCollider = target.GetComponent<Collider>();
        if (physicsCollider == null)
        {
            return;
        }

        physicsCollider.enabled = false;
        Object.Destroy(physicsCollider);
    }

    private static void SetMaterial(GameObject target, Material material)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }
}

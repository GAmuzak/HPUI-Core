using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Jobs;

namespace ubco.ovilab.HPUI.Core
{
    /// <summary>
    /// Component to manage the colliders of the deformable surface.
    /// </summary>
    public class DeformableSurfaceCollidersManager: MonoBehaviour
    {
	private NativeArray<Vector3> vertices_native, normals_native; 
        private List<Vector3> normals, vertices;
        private bool generatedColliders;

        private SkinnedMeshRenderer skinnedMeshRenderer;
        private Mesh tempMesh;

        private List<GameObject> collidersCache;
        private Vector3 scaleFactor;
        private int maxY, maxX;
        private float gridSize;
        private TransformAccessArray colliderObjects;

        /// <inheritdoc />
        private void OnDestroy()
	{
            if (vertices_native.IsCreated)
            {
                vertices_native.Dispose();
            }
            if (normals_native.IsCreated)
            {
                normals_native.Dispose();
            }
        }

        /// <inheritdoc />
	private void Update()
	{
            if (!isActiveAndEnabled)
            {
                return;
            }

	    if (generatedColliders)
	    {
                UpdateColliderPoses();
            }
	}

        /// <summary>
        /// Setup and return colliders. A collider will be placed on each vertice of the <see cref="SkinnedMeshRenderer"/>.
        /// </summary>
        public List<Collider> SetupColliders()
        {
            generatedColliders = false;
            if (skinnedMeshRenderer == null)
            {
                skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
            }
            if (tempMesh == null)
            {
                tempMesh = new Mesh();
            }

            vertices = new List<Vector3>();
            normals = new List<Vector3>();

            if (vertices_native.IsCreated)
            {
                vertices_native.Dispose();
            }
            if (normals_native.IsCreated)
            {
                normals_native.Dispose();
            }

            // See https://forum.unity.com/threads/get-skinned-vertices-in-real-time.15685/
            skinnedMeshRenderer.BakeMesh(tempMesh, true);
            tempMesh.GetVertices(vertices);
            tempMesh.GetNormals(normals);

            HPUIContinuousInteractable continuousInteractable = GetComponentInParent<HPUIContinuousInteractable>();

            maxY = vertices_native.Length - continuousInteractable.x_divisions;
            maxX = continuousInteractable.x_divisions;

            List<Collider> colliders = GenerateColliders(vertices, normals, transform, continuousInteractable.x_divisions);
            generatedColliders = true;
            return colliders;
        }

        /// <summary>
        /// Generate the colliders for a given set of vertices. The vertices are expected to be the order along x then along y.
        /// The generated colliders will be parented to the rootTransform.
        /// </summary>
	private List<Collider> GenerateColliders(List<Vector3> positions, List<Vector3> _normals, Transform rootTransform, int x_divisions)
	{
	    var right = positions[1] - positions[0];
	    GameObject colliderObj;
	    scaleFactor = Vector3.zero;

            if (collidersCache == null)
            {
                collidersCache = new List<GameObject>();
            }

            foreach(GameObject obj in collidersCache)
            {
                Destroy(obj);
            }
            Transform[] colliderTransforms = new Transform[positions.Count];
            List<Collider> colliders = new List<Collider>();

            for(var i = 0; i < positions.Count; i ++)
	    {
                colliderObj = new GameObject();
                Collider collider = colliderObj.AddComponent<BoxCollider>();
                colliders.Add(collider);
                colliderObj.transform.name = "X" + (int) i % x_divisions + "-Y" + (int) i / x_divisions;
		// Getting the scale values to set the size of the buttons based on the size of a single square in the generated mesh
		if (scaleFactor == Vector3.zero)
		{
		    Vector3 buttonSize = collider.bounds.size;
		    gridSize = rootTransform.InverseTransformVector(positions[0] - positions[1]).magnitude;
		
		    scaleFactor = colliderObj.transform.localScale;
		    // making them slightly larger to remove the spaces between the pixels
		    scaleFactor.x = (gridSize / buttonSize.x) * 1.05f * rootTransform.lossyScale.x;
		    scaleFactor.y = (gridSize / buttonSize.y) * 1.05f * rootTransform.lossyScale.y;
                    scaleFactor.z = 0.00001f;
                    gridSize = (positions[0] - positions[1]).magnitude;
		}
		colliderObj.transform.parent = rootTransform;
		colliderObj.transform.localPosition = positions[i];
		colliderObj.transform.localRotation = Quaternion.identity;
		colliderObj.transform.localScale = scaleFactor;
                colliderTransforms[i] = colliderObj.transform;
            }
	    colliderObjects = new TransformAccessArray(colliderTransforms);
            return colliders;
        }

        /// <summary>
        /// Update the poses of the colliders to follow the vertices of the <see cref="SkinnedMeshRenderer"/>
        /// </summary>
        protected void UpdateColliderPoses()
        {
            // See https://forum.unity.com/threads/get-skinned-vertices-in-real-time.15685/
            skinnedMeshRenderer.BakeMesh(tempMesh, true);
            tempMesh.GetVertices(vertices);
            tempMesh.GetNormals(normals);

            if (vertices_native.IsCreated)
                vertices_native.CopyFrom(vertices.ToArray());
            else
                vertices_native = new NativeArray<Vector3>(vertices.ToArray(), Allocator.Persistent);

            if (normals_native.IsCreated)
                normals_native.CopyFrom(normals.ToArray());
            else
                normals_native = new NativeArray<Vector3>(normals.ToArray(), Allocator.Persistent);

            // Once the mesh has been deformed, update the locations of the buttons to match the mesh
            DeformedCollidersJob job = new DeformedCollidersJob()
            {
                scaleFactor = scaleFactor,
                gridSize = gridSize,
                maxX = maxX,
                maxY = maxY,
                normals = normals_native,
                vertices = vertices_native
            };

            var jobHandle = job.Schedule(colliderObjects);
            jobHandle.Complete();
        }

        /// <summary>
        /// Job for setting the transforms when the positions are updated.
        /// </summary>
        struct DeformedCollidersJob: IJobParallelForTransform
        {
            private Vector3 right, up, temppos, _scaleFactor;
            public Vector3 scaleFactor;
            public float gridSize; 
            public int maxX, maxY;

            [Unity.Collections.ReadOnly]
            public NativeArray<Vector3> vertices;
            [Unity.Collections.ReadOnly]
            public NativeArray<Vector3> normals;
	
            public void Execute(int i, TransformAccess btn)
            {
                temppos = vertices[i];
                temppos.z += -0.0002f;
                btn.localPosition = temppos;

                if (i > maxX)
                    up = vertices[i] - vertices[i - maxX];
                else
                    up = vertices[i + maxX] - vertices[i];
		    
                if (i % maxX == 0)
                    right = vertices[i + 1] - vertices[i];
                else
                    right = vertices[i] - vertices[i - 1];

                btn.localRotation = Quaternion.LookRotation(normals[i], up);
                _scaleFactor.x = (right.magnitude / gridSize) * scaleFactor.x;
                _scaleFactor.y = (up.magnitude / gridSize) * scaleFactor.y;
                _scaleFactor.z = scaleFactor.z;
                btn.localScale = _scaleFactor;
            }
        }
    }
}
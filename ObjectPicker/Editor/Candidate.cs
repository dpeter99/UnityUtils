using System;
using System.Numerics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Diagnostics;
using Object = UnityEngine.Object;
using Vector3 = UnityEngine.Vector3;

namespace Packages.ObjectPicker
{
    struct Candidate
    {
        private const int IndexMaxChildren = 100;
        private const int IndexMaxDepth = 100;
        
        private readonly Transform transform;
        public Transform Transform => transform;

        private readonly Object @object;
        public Object Object => @object;

        public bool IsValid => transform != null && @object != null;
        public Vector3 Position => transform.position;
        
        private BigInteger hierarchyOrder;
        public BigInteger HierarchyOrder => hierarchyOrder;

        public string Name => transform.name;

        private GUIContent dropDownText;
        public GUIContent DropdownText => dropDownText;

        public Candidate(Object @object)
        {
            transform = GetTransform(@object);
            this.@object = @object;

            // Show the path, this helps figure out where it is in the transform hierarchy. Need to replace slash
            // with backslash otherwise it will create a separator.
            string path = Utils.GetPath(transform);
            path = path.Replace("/", "\\");
            
            string text = path + " (" + ObjectNames.NicifyVariableName(@object.GetType().Name) + ")";
            dropDownText = new GUIContent(text);

            // Determine how deep in the hierarchy this transform is.
            int hierarchyDepth = 0;
            Transform searchTransform = transform.parent;
            while (searchTransform != null)
            {
                hierarchyDepth++;
                searchTransform = searchTransform.parent;
            }

            hierarchyOrder = CalculateHierarchyOrder(transform, 0, hierarchyDepth);
        }

        /// <summary>
        /// This complicated looking function has a simple goal: calculating an index for a transform so that we
        /// can sort a list of candidates and be able to quickly sort them in the same way as the hierarchy view is
        /// sorted in Unity: first show ourselves, then our children, and then our siblings. Certain assumptions
        /// need to be made about how deep the hierarchy can go and how many children a transform can have.
        /// Generous limits of 100 children per transform and 100 layers deep have been used. This exceeds the
        /// capacity of integers, so that's why BigInteger has been used. If you actually have scenes with
        /// hierarchies that are this vast, please seek medical attention.
        /// </summary>
        private static BigInteger CalculateHierarchyOrder(Transform transform, BigInteger index, int hierarchyDepth)
        {
            // Assume that there is a maximum depth to layers and calculate how many are below us.
            int possibleLayersBelow = Math.Max(0, IndexMaxDepth - hierarchyDepth);
                
            // Assuming that every transform can have a specific amount of maximum children, calculate how many
            // indices should be reserved (block size) at this layer.
            BigInteger blockSizeAtDepth = BigInteger.Pow(IndexMaxChildren, possibleLayersBelow);
            
            // We know how many siblings precede this transform, so calculate what the index is within this layer.
            BigInteger indexWithinLayer = (transform.GetSiblingIndex() + 1) * blockSizeAtDepth;

            index += indexWithinLayer;
            
            // If our recursive function has reached the root, then our index has been computed.
            if (transform.parent == null)
                return index;

            // If there are still transforms above us, continue this function back up, recursively.
            hierarchyDepth--;
            return CalculateHierarchyOrder(transform.parent, index, hierarchyDepth);
        }

        public Vector3 GetScreenPosition(SceneView sceneView)
        {
            return sceneView.camera.WorldToScreenPoint(Position);
        }

        public bool IsBehindSceneCamera(SceneView sceneView)
        {
            return GetScreenPosition(sceneView).z < 0;
        }
        
        private static Transform GetTransform(Object @object)
        {
            // ROY: Sadly the 'transform' field is not shared by a common base class between 
            // Component and GameObject, so if we want to support both we have to check it like this. 
        
            Component component = @object as Component;
            if (component != null)
                return component.transform;
        
            GameObject gameObject = @object as GameObject;
            if (gameObject != null)
                return gameObject.transform;

            return null;
        }
    }

}
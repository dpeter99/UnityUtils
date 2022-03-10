using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Packages.ObjectPicker
{
    [InitializeOnLoad]
    public class SceneObjectPicker
    {
        private static SceneObjectPicker inst;
        public static SceneObjectPicker Instance
        {
            get
            {
                if (inst == null)
                    new SceneObjectPicker();
                return inst;
            }
        }

        public SceneObjectPicker()
        {
            inst = this;
        }


        private int controlID;

        /// <summary>
        /// The scene we are currently searching in
        /// </summary>
        private static Scene pickingScene;
        /// <summary>
        /// The type are are searching for currently
        /// </summary>
        private static Type pickingType;
        /// <summary>
        /// The name of a function that we should call if we set the data
        /// </summary>
        /// TODO: I think this could be done better, with an actual function reference. 
        [CanBeNull] private static string pickCallback;
        
        [CanBeNull] private Action<object> pickAction;
        
        
        /// <summary>
        /// The serialized property that we are picking for if it exists
        /// </summary>
        [CanBeNull] private static SerializedProperty propertyPicking;
        public static SerializedProperty PropertyPicking => propertyPicking;

        public static bool IsPicking => pickingType != null;
        
        
        private const float GroupDistance = 25.0f;
        
        /// <summary>
        /// This is the best option for the current search
        /// </summary>
        private Candidate bestCandidate;
        
        private List<Object> possibleCandidateObjects = new List<Object>();
        private static List<Candidate> allCandidates = new List<Candidate>();
        
        private static List<Candidate> nearbyCandidates = new List<Candidate>();

        private Object pickedValue;
        
        private static bool hasShownHint;
        
        
        
        private GUIStyle cachedPickingTextStyle;
        public GUIStyle PickingTextStyle
        {
            get
            {
                if (cachedPickingTextStyle == null)
                {
                    cachedPickingTextStyle = new GUIStyle("Box");
                    cachedPickingTextStyle.alignment = TextAnchor.MiddleCenter;
                    cachedPickingTextStyle.fontStyle = FontStyle.Bold;
                }
                
                return cachedPickingTextStyle;
            }
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (pickingType == null)
            {
                StopPicking();
                return;
            }

            // Make sure we update the sceneview whenever the mouse moves.
            if (Event.current.type == EventType.MouseMove)
            {
                bestCandidate = FindBestCandidate(sceneView);
                FindNearbyCandidates(sceneView);

                sceneView.Repaint();
            }

            // Draw the current best candidate.
            if (bestCandidate.IsValid)
            {
                Vector3 objectPosWorld = bestCandidate.Position;
                Vector2 mousePosGui = Event.current.mousePosition;
                Vector3 mouseWorld = HandleUtility.GUIPointToWorldRay(mousePosGui)
                    .GetPoint(10);

                Handles.color = new Color(1, 1, 1, 0.75f);
                Handles.DrawDottedLine(objectPosWorld, mouseWorld, 2.0f);
                Handles.color = Color.white;

                Handles.BeginGUI();

                string text = bestCandidate.Name;
                
                // The 'nearby candidates' includes the best candidate, if there's more than one, there are others.
                if (nearbyCandidates.Count > 1)
                    text += " + " + (nearbyCandidates.Count - 1) + " nearby";

                Vector2 labelSize = PickingTextStyle.CalcSize(new GUIContent(text));
                labelSize += Vector2.one * 4;
                Rect nameRect = new Rect(
                    Event.current.mousePosition + Vector2.down * 10 - labelSize * 0.5f, labelSize);

                // Draw shadow.
                GUI.backgroundColor = new Color(0, 0, 0, 1.0f);
                PickingTextStyle.normal.textColor = Color.black;
                EditorGUI.LabelField(nameRect, text, PickingTextStyle);

                // Draw white text.
                nameRect.position += new Vector2(-1, -1);
                GUI.backgroundColor = new Color(0, 0, 0, 0);
                PickingTextStyle.normal.textColor = Color.white;
                EditorGUI.LabelField(nameRect, text, PickingTextStyle);

                Handles.EndGUI();
            }

            // This makes sure that clicks are not handled by the scene itself.
            if (Event.current.type == EventType.Layout)
            {
                controlID = GUIUtility.GetControlID(FocusType.Passive);
                HandleUtility.AddDefaultControl(controlID);
                return;
            }

            if (Event.current.type != EventType.MouseDown || Event.current.alt ||
                Event.current.control)
            {
                return;
            }

            // Left click to pick a candidate, every other button is a cancel.
            if (Event.current.button == 0)
            {
                PickCandidate(bestCandidate);
            }
            else if (Event.current.button == 2 && nearbyCandidates.Count > 1)
            {
                PickNearbyCandidate();
            }
            else
            {
                StopPicking();
            }

            Event.current.Use();
        }
        
        private void PickNearbyCandidate()
        {
            GenericMenu menu = new GenericMenu();
            menu.allowDuplicateNames = true;

            for (int i = 0; i < nearbyCandidates.Count; i++)
            {
                // Declare this so it is referenced correctly in the anonymous method passed to the menu.
                Candidate candidate = nearbyCandidates[i];
                
                menu.AddItem(candidate.DropdownText, false, () => PickCandidate(candidate));
            }
            
            menu.ShowAsContext();
        }
        
        private void PickCandidate(Candidate candidate)
        {
            if (pickingType == null || !candidate.IsValid)
                return;

            // Actually apply the value.
            if (propertyPicking != null)
            {
                propertyPicking.serializedObject.Update();

                Object previousValue = propertyPicking.objectReferenceValue;
                Object currentValue = candidate.Object;

                propertyPicking.objectReferenceValue = currentValue;
                propertyPicking.serializedObject.ApplyModifiedProperties();
                
                FireSceneViewPickerCallback(previousValue, currentValue);
            }

            if (pickAction != null)
            {
                pickAction.Invoke(candidate.Object);
            }
            
            
            StopPicking();
        }
        
        public void FireSceneViewPickerCallback(Object previousValue, Object currentValue)
        {
            FireSceneViewPickerCallback(propertyPicking, pickCallback, previousValue, currentValue);
        }
        
        public void FireSceneViewPickerCallback(SerializedProperty property, string callback, Object previousValue, Object currentValue)
        {
            if (property == null || string.IsNullOrEmpty(callback))
                return;

            object target = Utils.GetParentObject(property);
            MethodInfo method = Utils.GetMethodIncludingFromBaseClasses(target.GetType(), callback);
            if (method == null)
            {
                Debug.LogWarningFormat(
                    "Was asked to fire callback '{0}' but object '{1}' " +
                    "did not seem to have one. Path is {2}.", callback,
                    target, property.propertyPath);
                return;
            }

            ParameterInfo[] parameters = method.GetParameters();

            // If it has 2 parameters, invoke it with the previous and current value.
            if (parameters.Length == 2)
            {
                object previous = Utils.Cast(previousValue, parameters[0].ParameterType);
                object current = Utils.Cast(currentValue, parameters[1].ParameterType);
                method.Invoke(target, new[] {previous, current});
                return;
            }

            // Parameterless callback, just fire it right now.
            method.Invoke(target, new object[0]);
        }
        

        public void StartPicking(SerializedProperty property, Type type, string callback)
        {
            Scene currentScene = Utils.GetScene(property.serializedObject.targetObject);
            StartPicking(property,type,currentScene,callback);
        }

        public void StartPicking<T>(Type type, Scene scene, Action<T> callback = null)
        {
            pickAction = o => callback?.Invoke((T) o);
            StartPicking(null, type, scene, null);
        }

        public void StartPicking<T>(Type type, int id, EditorWindow caller)
        {
            //pickAction = o => callback?.Invoke((T) o);
            pickAction = o =>
            {
                Event e = EditorGUIUtility.CommandEvent("SceneObjectPicker-ObjectPicked");
                pickedValue = o as Object;
                
                caller.SendEvent(e);
            };
            
            StartPicking(null, type, default(Scene), null);
        }
        
        public void StartPicking(SerializedProperty property, Type type, Scene scene, string callback)
        {
            if (!hasShownHint)
            {
                SceneView.lastActiveSceneView.ShowNotification(
                    new GUIContent(
                        "Left click: Pick an object in scene \n\n" +
                        "Middle click: Choose from nearby objects \n\n" +
                        "Right click: Cancel"), 3);
                hasShownHint = true;
            }

            propertyPicking = property;
            //pickCallback = callback;

            pickingType = type;
            pickingScene = scene;
            
            FindAllCandidates(type);
            
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;

            SceneView.lastActiveSceneView.Repaint();

            if (allCandidates.Count <= 0)
            {
                StopPicking();
            }
        }
        
        /// <summary>
        /// Resets the Picking to stop it
        /// </summary>
        public void StopPicking()
        {
            allCandidates.Clear();
            bestCandidate = default(Candidate);

            propertyPicking = null;
            //pickCallback = null;
            pickingType = null;
            pickingScene = default(Scene);

            SceneView.duringSceneGui -= OnSceneGUI;
        }
        
        
        private void FindAllCandidates(Type type)
        {
            bool isInterface = type.IsInterface;
            
            possibleCandidateObjects.Clear();
            if (isInterface)
            {
                // If the type is an interface type then we can't directly search for all instances so we search for all
                // MonoBehaviours and filter out the wrong types here.
                FindObjectsOfTypeInSceneOrPrefab(typeof(MonoBehaviour), ref possibleCandidateObjects);
                
                for (int i = possibleCandidateObjects.Count - 1; i >= 0; i--)
                {
                    if (isInterface && !type.IsInstanceOfType(possibleCandidateObjects[i]))
                        possibleCandidateObjects.RemoveAt(i);
                }
            }
            else if (type == typeof(GameObject))
            {
                FindGameObjectsInSceneOrPrefab(ref possibleCandidateObjects);
            }
            else
            {
                FindObjectsOfTypeInSceneOrPrefab(type, ref possibleCandidateObjects);
            }
            
            allCandidates.Clear();
            for (int i = 0; i < possibleCandidateObjects.Count; i++)
                allCandidates.Add(new Candidate(possibleCandidateObjects[i]));
        }
        
        
        private void FindObjectsOfTypeInSceneOrPrefab(Type type, ref List<Object> @objects)
        {
            Scene currentScene = pickingScene;

            // If there is a valid scene, use that scene to find candidates instead.
            objects.Clear();
            if (currentScene.IsValid())
            {
                GameObject[] rootGameObjects = currentScene.GetRootGameObjects();
                for (int i = 0; i < rootGameObjects.Length; i++)
                {
                    objects.AddRange(rootGameObjects[i].GetComponentsInChildren(type));
                }

            }
            else
            {
                objects.AddRange(Object.FindObjectsOfType(type));
            }

            if (pickingScene.IsValid())
            {
                // Filter out components belonging to the wrong scene.
                for (int i = objects.Count - 1; i >= 0; i--)
                {
                    if (Utils.GetScene(objects[i]) != currentScene)
                        objects.RemoveAt(i);
                }
            }
        }

        private void FindGameObjectsInSceneOrPrefab(ref List<Object> @objects)
        {
            Scene currentScene = pickingScene;

            // If there is a valid scene, use that scene to find candidates instead.
            objects.Clear();
            if (currentScene.IsValid())
            {
                GameObject[] rootGameObjects = currentScene.GetRootGameObjects();
                for (int i = 0; i < rootGameObjects.Length; i++)
                {
                    Transform[] transforms = rootGameObjects[i].GetComponentsInChildren<Transform>();
                    for (int j = 0; j < transforms.Length; j++)
                    {
                        Transform transform = transforms[j];
                        objects.Add(transform.gameObject);
                    }
                }
            }
            else
            {
                objects.AddRange(Object.FindObjectsOfType(typeof(GameObject)));
            }

            // Filter out components belonging to the wrong scene.
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                if (Utils.GetScene(objects[i]) != currentScene)
                    objects.RemoveAt(i);
            }
        }
        
        
        
        /// <summary>
        /// Finds the best candidate to display as the main suggestion
        /// </summary>
        /// Tis means it finds the Candidate that is the closest to the mouse position in the world
        /// <param name="sceneView"></param>
        /// <returns></returns>
        private Candidate FindBestCandidate(SceneView sceneView)
        {
            float distanceMin = float.PositiveInfinity;
            Candidate bestCandidate = default(Candidate);

            foreach (Candidate candidate in allCandidates)
            {
                if (candidate.IsBehindSceneCamera(sceneView))
                    continue;
                
                float distance = Utils.GetDistanceToMouse(candidate, sceneView);
                
                // Find the closest one.
                if (distance < distanceMin)
                {
                    bestCandidate = candidate;
                    distanceMin = distance;
                }
            }
            
            return bestCandidate;
        }
        
        private void FindNearbyCandidates(SceneView sceneView)
        {
            nearbyCandidates.Clear();
            
            if (!bestCandidate.IsValid)
                return;

            foreach (Candidate candidate in allCandidates)
            {
                if (candidate.IsBehindSceneCamera(sceneView))
                    continue;
                
                // Find any candidates that are very close to the best candidate.
                float distance = Utils.GetDistance(bestCandidate, candidate, sceneView);
                if (distance < GroupDistance)
                    nearbyCandidates.Add(candidate);
            }
            
            nearbyCandidates.Sort(SortNearbyCandidates);
        }
        
        private int SortNearbyCandidates(Candidate x, Candidate y)
        {
            float distanceXToBestCandidate = Vector3.Distance(x.Position, bestCandidate.Position);
            float distanceYToBestCandidate = Vector3.Distance(y.Position, bestCandidate.Position);

            int comparison = distanceXToBestCandidate.CompareTo(distanceYToBestCandidate);
            
            if (comparison != 0)
                return comparison;
            
            // If they are on the same transform, sort alphabetically...
            if (x.Transform == y.Transform)
                return x.Name.CompareTo(y.Name);
            
            // If they are at the same distance to the candidate, go by hierarchy order instead. This will group
            // transforms by their children and respect the sibling order too.
            return x.HierarchyOrder.CompareTo(y.HierarchyOrder);
        }

    }
}
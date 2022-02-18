using System;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Packages.ObjectPicker
{
    public class Utils
    {
        public static string GetPath(Transform transform, Transform relativeTo = null)
        {
            if (relativeTo != null && !transform.IsChildOf(relativeTo))
            {
                Debug.LogWarningFormat(
                    transform,
                    "Tried to get path of transform {0} relative to transform {1}, " +
                    "which isn't actually a parent of it.",
                    transform, relativeTo);
                return null;
            }

            string path = transform.name;
            
            Transform current = transform.parent;
            while (current != relativeTo)
            {
                path = current.name + "/" + path;
                
                current = current.parent;
            }

            return path;
        }

        internal static float GetDistanceToMouse(Candidate candidate, SceneView sceneView)
        {
            // Figure out where the object is relative to the scene view camera.
            Vector3 positionScreen = candidate.GetScreenPosition(sceneView);

            // Let the Z distance count for prioritization, but not as much as X and Y.
            positionScreen.z /= 10;

            Vector3 mouseScreen = Event.current.mousePosition;
            mouseScreen.y = sceneView.position.height - mouseScreen.y;
            
            return Vector3.Distance(mouseScreen, positionScreen);
        }
        
        internal static float GetDistance(Candidate candidate1, Candidate candidate2, SceneView sceneView)
        {
            // Figure out where the object is relative to the scene view camera.
            Vector3 positionScreen1 = candidate1.GetScreenPosition(sceneView);
            Vector3 positionScreen2 = candidate2.GetScreenPosition(sceneView);

            // Let the Z distance count for prioritization, but not as much as X and Y.
            positionScreen1.z /= 10;
            positionScreen2.z /= 10;

            return Vector3.Distance(positionScreen1, positionScreen2);
        }
        
        public static object GetParentObject(SerializedProperty property)
        {
            string path = property.propertyPath;
            int indexOfLastSeparator = path.LastIndexOf(".", StringComparison.Ordinal);

            // No separators means it's a root object and there's no parent.
            if (indexOfLastSeparator == -1)
                return property.serializedObject.targetObject;

            string pathExcludingLastObject = path.Substring(0, indexOfLastSeparator);
            return GetActualObjectByPath(property.serializedObject, pathExcludingLastObject);
        }
        
        public static object GetActualObjectByPath(SerializedObject serializedObject, string path)
        {
            return GetActualObjectByPath(serializedObject.targetObject, path);
        }
        
        public static object GetActualObjectByPath(Object owner, string path)
        {
            // Sample paths:    connections.Array.data[0].to
            //                  connection.to
            //                  to

            string[] pathSections = path.Split('.');

            object value = owner;
            for (int i = 0; i < pathSections.Length; i++)
            {
                Type valueType = value.GetType();

                if (valueType.IsArray)
                {
                    // Parse the next section which contains the index. 
                    string indexPathSection = pathSections[i + 1];
                    indexPathSection = Regex.Replace(indexPathSection, @"\D", "");
                    int index = int.Parse(indexPathSection);

                    // Get the value from the array.
                    Array array = value as Array;
                    value = array.GetValue(index);
                    
                    // We can now skip the next section which is the one with the index.
                    i++;
                    continue;
                }
                
                // Go deeper down the hierarchy by searching in the current value for a field with
                // the same name as the current path section and then getting that value.
                FieldInfo fieldInfo = valueType.GetField(
                    pathSections[i], BindingFlags.Instance | BindingFlags.NonPublic);
                value = fieldInfo.GetValue(value);
            }

            return value;
        }
        
        public static MethodInfo GetMethodIncludingFromBaseClasses(Type type, string name)
        {
            MethodInfo methodInfo = null;
            Type baseType = type;
            while (methodInfo == null)
            {
                methodInfo = baseType.GetMethod(
                    name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                if (methodInfo != null)
                    return methodInfo;
                
                baseType = baseType.BaseType;
                if (baseType == null)
                    break;
            }

            return null;
        }
        
        
        private static MethodInfo castSafeMethod;
    
        public static object Cast(object data, Type type)
        {
            if (data == null)
                return null;

            // ROY: Contrary to Convert.ChangeType, this one will also work when casting a derived type
            // to one of its base classes.
        
            if (castSafeMethod == null)
            {
                castSafeMethod = typeof(Utils).GetMethod(
                    "CastStronglyTyped", BindingFlags.NonPublic | BindingFlags.Static);
            }

            MethodInfo castSafeMethodGeneric = castSafeMethod.MakeGenericMethod(type);

            return castSafeMethodGeneric.Invoke(null, new[] {data});
        }
        
        private static T CastStronglyTyped<T>(object data)
        {
            return (T)data;
        }
        
        public static Scene GetScene(Object @object)
        {
            if (@object is Component component)
                return component.gameObject.scene;

            if (@object is GameObject gameObject)
                return gameObject.scene;

            return default(Scene);
        }
    }
}
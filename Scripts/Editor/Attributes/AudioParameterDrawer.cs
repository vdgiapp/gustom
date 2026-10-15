#if UNITY_EDITOR
using System.Collections.Generic;
using Gustom.Attributes;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Gustom.Editor
{
    [CustomPropertyDrawer(typeof(AudioParameterAttribute))]
    public class AudioParameterDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "Use [AudioParameter] with string fields.");
                return;
            }

            AudioParameterAttribute audioParamAttribute = (AudioParameterAttribute)attribute;
            string targetMixerName = audioParamAttribute.MixerFieldName;

            AudioMixer mixer = null;

            if (!string.IsNullOrEmpty(targetMixerName))
            {
                SerializedProperty mixerProp = property.serializedObject.FindProperty(targetMixerName);
                if (mixerProp != null && mixerProp.objectReferenceValue)
                {
                    mixer = mixerProp.objectReferenceValue as AudioMixer;
                }
            }

            if (!mixer)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            List<string> exposedParameters = new List<string>();
            SerializedProperty exposedParamsProp = new SerializedObject(mixer).FindProperty("m_ExposedParameters");
            
            if (exposedParamsProp != null)
            {
                for (int i = 0; i < exposedParamsProp.arraySize; i++)
                {
                    SerializedProperty paramProp = exposedParamsProp.GetArrayElementAtIndex(i);
                    SerializedProperty nameProp = paramProp.FindPropertyRelative("name");
                    if (nameProp != null) exposedParameters.Add(nameProp.stringValue);
                }
            }

            if (exposedParameters.Count == 0)
            {
                EditorGUI.LabelField(position, label.text, "No exposed parameters found in Mixer.");
                return;
            }

            int currentIndex = Mathf.Max(0, exposedParameters.IndexOf(property.stringValue));
            int selectedIndex = EditorGUI.Popup(position, label.text, currentIndex, exposedParameters.ToArray());
            
            property.stringValue = exposedParameters[selectedIndex];
        }
    }
}
#endif
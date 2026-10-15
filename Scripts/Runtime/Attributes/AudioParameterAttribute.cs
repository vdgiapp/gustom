using System;
using UnityEngine;

namespace Gustom.Attributes
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class AudioParameterAttribute : PropertyAttribute 
    {
        public string MixerFieldName { get; private set; }

        public AudioParameterAttribute(string mixerFieldName)
        {
            this.MixerFieldName = mixerFieldName;
        }
    }
}
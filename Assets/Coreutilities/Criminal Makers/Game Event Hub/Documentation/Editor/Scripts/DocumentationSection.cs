using System.Collections.Generic;
using UnityEngine;

namespace CriminalMakers.GameEventHub.Documentation
{
    public class DocumentationSection : ScriptableObject
    {
        public string title;
        public int order;
        public List<TextAsset> documentationItems = new List<TextAsset>();
    }
}
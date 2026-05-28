using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Slot001_GoldenPixiu
{
    
    public enum GameSymbolType
    {
        Normal = 0,
        Scatter,
        Wild,
        NN,
    }
    [CreateAssetMenu(fileName = "SymbolData", menuName = "ScriptableObjects/SymbolData/Slot003SymbolData", order = 5)]
    public class SymbolDataSO : SymbolDataSO<SymbolData>
    {
     
    }

    [Serializable]
    public class SymbolData : global::SymbolData
    { 
        [PreviewField]
        public Sprite BulrSprite;
        public GameSymbolType SymbolType = GameSymbolType.Normal;
     
    
    }
}
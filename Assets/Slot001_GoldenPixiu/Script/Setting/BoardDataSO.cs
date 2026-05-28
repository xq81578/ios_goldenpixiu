using System.Collections.Generic;
using UnityEngine;

namespace Slot001_GoldenPixiu
{
    [CreateAssetMenu(fileName = "Slot003BoardData", menuName = "ScriptableObjects/Slot003BoardData", order = 1)]
    public class BoardDataSO : ScriptableObject
    {
        [SerializeField]
        private List<BoardData> _boards;
        public List<BoardData> Boards => _boards;

        public BoardData RandomBoard()
        {
            int size = Boards.Count;
            int index = Random.Range(0, size);
            var clonBoard = this.Clone().Boards[index];
            return clonBoard;
        }
    }
}
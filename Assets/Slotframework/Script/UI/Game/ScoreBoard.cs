using System.Collections.Generic;
using CriminalMakers.GameEventHub;
using DG.Tweening;
using Sirenix.OdinInspector;
using Slot.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Slot.Common.Game
{
    public class ScoreBoardData
    {
        public int symbolId;
        public int count;
        public double win;
        public int index;

        public GameObject gameObject;
        public Transform transform;
        public CanvasGroup canvasGroup;
        public Image symbolImage;
        public TextMeshProUGUI countValueText;
        public TextMeshProUGUI scoreValueText;

        //Constructor
        public ScoreBoardData(GameObject go)
        {
            gameObject = go;
            transform = gameObject.transform;
            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            symbolImage = transform.Find("Symbol").GetComponent<Image>();
            countValueText = transform.Find("CountValueText").GetComponent<TextMeshProUGUI>();
            scoreValueText = transform.Find("ScoreValueText").GetComponent<TextMeshProUGUI>();
        }

        public void FadeOut()
        {
            canvasGroup.DOFade(0, 0.5f);
        }

        public void FadeIn()
        {
            canvasGroup.alpha = 0;
            canvasGroup.DOFade(1, 0.5f);
        }
    }
    public class ScoreBoard : MonoBehaviour
    {
        [Inject]
        [SerializeField]
        private SymbolDataSOBase symbolData;

        [SerializeField]
        private List<GameObject> elementList;

        [FoldoutGroup("掉落動畫Setting")]
        [SerializeField, FoldoutGroup("掉落動畫Setting")]
        [InfoBox("掉落時間")]
        private float fallDuration = 0.5f;
        [SerializeField, FoldoutGroup("掉落動畫Setting")]
        [InfoBox("掉落的線性函數")]
        private Ease fallEase = Ease.OutBack;

        private List<ScoreBoardData> scoreBoardDataList = new List<ScoreBoardData>();

        private Queue<ScoreBoardData> scoreBoardDataQueue = new Queue<ScoreBoardData>();

        private List<Vector3> targetPosList = new List<Vector3>();

        private int currentTargetPosIndex = 0;

        #region Unity Methods
        private void Awake()
        {
            GameEventHub.Bind(this);
            foreach (var element in elementList)
            {
                //element.SetActive(false);
                var sbd = new ScoreBoardData(element);
                sbd.canvasGroup.alpha = 0;
                scoreBoardDataList.Add(sbd);
                scoreBoardDataQueue.Enqueue(sbd);
                targetPosList.Add(sbd.transform.localPosition);
            }
            currentTargetPosIndex = elementList.Count - 1;
        }

        private void Start()
        {
            Reset();
        }

        private void OnDestroy()
        {
            GameEventHub.Unbind(this);
        }
        #endregion

        [Button]
        public void Reset()
        {
            scoreBoardDataQueue.Clear();
            //Fade out all ScoreBoardData
            foreach (var sbd in scoreBoardDataList)
            {
                sbd.FadeOut();
                scoreBoardDataQueue.Enqueue(sbd);
            }

            currentTargetPosIndex = elementList.Count - 1;
        }

        [Button]
        public async void PushScoreBoardData(int symbolId, int count, double win)
        {
            await Cysharp.Threading.Tasks.UniTask.WaitUntil(() => scoreBoardDataQueue.Count != 0);

            //檢查是否是空Queue
            if (scoreBoardDataQueue.Count == 1)
            {
                LogUtils.LogWarning("ScoreBoardDataQueue is Last");

                for (int i = 0; i < scoreBoardDataList.Count - 1; i++)
                {
                    scoreBoardDataList[i].index++;
                    if (scoreBoardDataList[i].index == scoreBoardDataList.Count)
                    {
                        scoreBoardDataList[i].index = 1;
                    }
                    FallingAct(scoreBoardDataList[i], scoreBoardDataList[i].index);
                }
            }
            //從Queue中取出一個ScoreBoardData
            var sbd = scoreBoardDataQueue.Dequeue();

            sbd.symbolId = symbolId;
            sbd.count = count;
            sbd.win = win;

            sbd.FadeIn();
            sbd.symbolImage.sprite = symbolData.GetSymbolData(symbolId).NormalSprite;
            sbd.countValueText.text = count.ToString();
            sbd.scoreValueText.text = ServiceUtils.ToCurrentString(win, Bottom_Define.MoneyFormat);

            currentTargetPosIndex--;
            if (currentTargetPosIndex < 0)
            {
                currentTargetPosIndex = 0;
            }

            sbd.index = currentTargetPosIndex;

            FallingAct(sbd, currentTargetPosIndex);
        }

        public void FallingAct(ScoreBoardData sbd, int targetPosIndex)
        {
            Transform tf = sbd.transform;
            //得到目標位置
            Vector3 targetPos = targetPosList[targetPosIndex];
            //設定起始位置
            Vector3 startPos = targetPosList[0];
            if (targetPosIndex - 1 > 0)
            {
                startPos = targetPosList[targetPosIndex - 1];
            }

            tf.localPosition = startPos;
            tf.DOLocalMove(targetPos, fallDuration).SetEase(fallEase).OnComplete(() =>
            {
                if (sbd.index == scoreBoardDataList.Count - 1)
                {
                    sbd.FadeOut();
                    scoreBoardDataList.Remove(sbd);
                    scoreBoardDataList.Add(sbd);
                    scoreBoardDataQueue.Enqueue(sbd);
                }
            });
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIBottomLockEvent(UIBottomLockEvent e)
        {
            Reset();
        }
    }
}
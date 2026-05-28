using System;
using System.IO;
using System.Linq;
using CriminalMakers.GameEventHub;
using DG.Tweening;
using Google.Protobuf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Slot001_GoldenPixiu
{
    
    public class GameService : BaseGameService
    {
        #if !DISABLE_SRDEBUGGER
        [Serializable]
        public class DugDate 
        {
            public OptionContainer.RTPType type;
            public FakeData _fakeData;
        }

        [SerializeField]
        private DugDate[] _debugData;
#endif

        private Action<JToken> _callback;

        public override string GAME_ID => "Slot001";
#if !DISABLE_SRDEBUGGER
        private OptionContainer _optionContainer;
#endif
        protected override void Initialize()
        {
#if !DISABLE_SRDEBUGGER
            _optionContainer = new OptionContainer();
            SRDebug.Instance.AddOptionContainer(_optionContainer);
            SRDebug.Instance.IsTriggerEnabled = true;
#endif
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
#if !DISABLE_SRDEBUGGER
            SRDebug.Instance?.RemoveOptionContainer(_optionContainer);
#endif
        }

        public void SendSpin(double totalBet, BuyType buyType = BuyType.BUY_NONE, Action<JToken> callback = null)
        {
#if !DISABLE_SRDEBUGGER
            if (_optionContainer != null)
            {
                JToken tgdata = GetSpinTriggerData();
                if (_optionContainer.RTPValue != OptionContainer.RTPType.None && tgdata != null){
                    _optionContainer.RTPValue = OptionContainer.RTPType.None;
                   LogUtils.Log("RTP值:" + _optionContainer.RTPValue);
                   LogUtils.Log("本地数据:" + tgdata.ToString());
                   tgdata = judData(tgdata,totalBet);
                    callback?.Invoke(tgdata);
                    return;
                }
            }
#endif
            SpinCmd spinData = new();
            spinData.TotalBet = (int)totalBet;
            spinData.BuyType = (int)buyType;
            JObject spinDatastr = JObject.FromObject(spinData);
            base.SendSpin(GAME_ID,totalBet, buyType, spinDatastr, callback);
        }


#if !DISABLE_SRDEBUGGER
        public JToken GetSpinTriggerData()
        {
            
            // 根据不同的RTP值加载不同的假数据
            JToken data = null;
            if (_optionContainer != null && _debugData != null)
            {
                for (int i = 0; i < _debugData.Length; i++)
                {
                    if (_debugData[i] != null && 
                        _debugData[i].type == _optionContainer.RTPValue &&
                        _debugData[i]._fakeData != null)
                    {
                        try
                        {
                            var fakeData = _debugData[i]._fakeData.DataFile;
                            if (fakeData != null)
                            {
                                data = JsonConvert.DeserializeObject<JObject>(fakeData.text);
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error parsing fake data for {_debugData[i].type}: {ex.Message}");
                        }
                    }
                }
            }
            // 调用基类的OnSpinRes方法处理假数据响应
           return data;
        }
#endif

        #if !DISABLE_SRDEBUGGER
        //换算数据
        private JToken judData(JToken data,double bet)
        {

            data["TotalWin"] = (double)data["TotalWin"]*bet;

            //MGResult
            data["MGResult"]["MGTumbleList"][0]["Win"] = (double)data["MGResult"]["MGTumbleList"][0]["Win"]*bet;
            data["MGResult"]["MainWin"] = (double)data["MGResult"]["MainWin"]*bet;
            var lineWin = data["MGResult"]["MGTumbleList"][0]["LineWin"];
            for (int i = 0; i < lineWin.Count(); i++)
            {//LineWin
                double item = (double)lineWin[i];
                double itemc = item*bet;
                data["MGResult"]["MGTumbleList"][0]["LineWin"][i] = itemc;
            }
            for (int j = 0; j < data["MGResult"]["MGTumbleList"][0]["ScoreSymbol"].Count(); j++)
            {//ScoreSymbol
                double item = (double)data["MGResult"]["MGTumbleList"][0]["ScoreSymbol"][j];
                double itemc = item*bet;
                data["MGResult"]["MGTumbleList"][0]["ScoreSymbol"][j] = itemc;
            }



            //FGResult
            data["FGResult"]["FreeWin"] = (double)data["FGResult"]["FreeWin"]*bet;
            var FGTumbleList = data["FGResult"]["FGTumbleList"];
            for (int i = 0; i < FGTumbleList.Count(); i++)
            {
                var FGList = FGTumbleList[i];
                data["FGResult"]["FGTumbleList"][i]["Win"] = (double)FGList["Win"]*bet;

                var ScoreSymbol = FGList["ScoreSymbol"];
                //ScoreSymbol 全部是double类型的数字集合，里面的全部*bet
                for (int j = 0; j < ScoreSymbol.Count(); j++)
                {
                    double item = (double)ScoreSymbol[j];
                    double itemc = item*bet;
                    data["FGResult"]["FGTumbleList"][i]["ScoreSymbol"][j] = itemc;
                }

            }
            
            LogUtils.Log("换算数据:" + data.ToString());

            return data;
        }
#endif


    }
}
using System;
using System.Collections.Generic;

#if !DISABLE_SRDEBUGGER
using SRDebugger;
#endif

namespace Slot001_GoldenPixiu
{
#if !DISABLE_SRDEBUGGER
    public class OptionContainer : IOptionContainer
    {
        public enum RTPType
        {
            None = 0,
            Four_Scatter = 11,
            Five_Scatter = 12,
            Six_Scatter = 13,
            Win_Celebartion = 14,
            FreeGame_Enter = 15,
            PreReel = 16,
            Win_Wild = 17,
            Demo = 18,
        }


        public RTPType RTPValue = RTPType.None;
        public bool IsDemoRTP = false;

        public bool IsDynamic => true;

        public event Action<OptionDefinition> OptionAdded;
        public event Action<OptionDefinition> OptionRemoved;

        public IEnumerable<OptionDefinition> GetOptions()
        {
            int index = 0;
            List<OptionDefinition> options = new List<OptionDefinition>();

            foreach (RTPType rtpType in Enum.GetValues(typeof(RTPType)))
            {
                if (rtpType == RTPType.None || rtpType == RTPType.Demo) continue;

                options.Add(OptionDefinition.FromMethod(
                    rtpType.ToString(),
                    () =>
                    {
                        RTPValue = rtpType;
                        IsDemoRTP = false;
                        SRDebug.Instance.HideDebugPanel();
                        new SpinTriggerEvent().Publish(this);
                    },
                    "Sloot001", index++)
                );
            }

            options.Add(OptionDefinition.Create(
                "DemoRTP",
                () => IsDemoRTP,
                (newValue) =>
                {
                    IsDemoRTP = newValue;
                    RTPValue = IsDemoRTP ? RTPType.Demo : RTPType.None;
                },
                "Sloot001", index++)
            );

            return options;
        }
    }
#endif
}
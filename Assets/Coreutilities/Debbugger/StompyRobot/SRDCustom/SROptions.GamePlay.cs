/* sample
using System.ComponentModel;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public partial class SROptions
{
    public enum RTPType
    {
        None = 0,
        HighChanceMultiplierBall = 12,
        BigWin = 13,
        MegaWin = 14,
        SuperWin = 15,
        EpicWin = 16
    }

    public enum DebugCdoeType
    {
        None = 0,
        MainGame = -1,
        FreeGame = -2,
    }

    private RTPType _rtpValue;
    private DebugCdoeType _debugCode;
    private string _debugSymbol;

    [Category("Spin"), Sort(1)]
    public RTPType RTPValue
    {
        get { return _rtpValue; }
        set
        {
            _rtpValue = value;
            OnPropertyChanged("RTPValue");
        }
    }

    [Category("Spin"), Sort(2)]
    public DebugCdoeType DebugCode
    {
        get { return _debugCode; }
        set
        {
            _debugCode = value;
            OnPropertyChanged("DebugCode");
        }
    }

    [Category("Spin"), Sort(3)]
    public string DebugSymbol
    {
        get { return _debugSymbol; }
        set
        {
            _debugSymbol = value;
            OnPropertyChanged("DebugSymbol");
        }
    }

    [Category("Spin"), Sort(4)]
    public void Reset()
    {
        RTPValue = RTPType.None;
        DebugCode = DebugCdoeType.None;
        DebugSymbol = "";
    }
}
*/
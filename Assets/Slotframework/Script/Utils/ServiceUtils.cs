using System;
using Slot.Common;

public class ServiceUtils
{
    // server 要乘10000 避免小數點誤差
    public static int ToServerBet(double value)
    {
        return (int)(value * 100);
    }

    // server 要乘10000 避免小數點誤差
    public static ulong ToServerBalance(double value)
    {
        return (ulong)(value * 100);
    }

    public static double ToClientBalance(ulong value)
    {
        return value / 100.0;
    }

    public static int ToClientBalance(int value)
    {
        return (int)(value / 100.0);
    }

    public static double ToClientBalance(long value)
    {
        return value / 100.0;
    }

    public static bool IsIntegerFloor(double f)
    {
        return f == Math.Floor(f);
    }

    // 利用but type判斷是不是有開Extra bet
    public static bool IsExtraBet(int buyType)
    {
        return buyType == (int)BuyType.BUY_EXTRA_BET;
    }

    public static double FloorByDecimalPlaces(double value, int digits)
    {
        double newValue = value * Math.Pow(10, digits);
        newValue = Math.Floor(newValue);
        newValue = newValue / Math.Pow(10, digits);
        return newValue;
    }

    public static double RoundByDecimalPlaces(double value, int digits)
    {
        return Math.Round(value, digits);
    }

    // formatStr: 文字格式  decimalPoint:小數點第幾位無條件捨去
    public static string ToKiloString(double value, string formatStr = "#,##0.##", int decimalPoint = 2)
    {
        string thousandsText = "";
        double result = value;
        if (value >= 1000000000)
        {
            result = value / 1000000000.0;
            thousandsText = "B";
        }
        else if (value >= 1000000)
        {
            result = value / 1000000.0;
            thousandsText = "M";
        }
        else if (value >= 1000)
        {
            result = value / 1000.0;
            thousandsText = "K";
        }
        else
        {
            thousandsText = "";
        }

        result = FloorByDecimalPlaces(result, decimalPoint);

        return result.ToString(formatStr) + thousandsText;
    }

    private static Currency _currency { get; set; }
    public static void SetCurrency(Currency cur)
    {
        _currency = cur;
    }
    //貨幣轉換顯示 KMB
    public static string ToCurrentString(double value, string formatStr = "#,##0.##", int decimalPoint = 2)
    {
        return value.ToString(formatStr);

        /*
        if (_currency != null && _currency.isKilo)
        {
            return ToKiloString(value, formatStr, decimalPoint);
        }
        else
        {
            return value.ToString(formatStr);
        }
        */
    }

    //是否要改成KMB顯示
    public static string ToNmuberString(bool isKilo, double value, string formatStr = "#,##0.##", int decimalPoint = 2)
    {
        if (isKilo)
        {
            return ToKiloString(value, formatStr, decimalPoint);
        }
        else
        {
            return value.ToString(formatStr);
        }
    }

    public static string GetVersionText()
    {
        string versionText = $"v.{UnityEngine.Application.version}";
#if DEV_BUILD
        versionText += ".d";
#elif UAT_BUILD
        versionText += ".u";
#endif
        return versionText;
    }

}
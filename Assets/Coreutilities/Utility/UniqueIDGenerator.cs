using System;
using UnityEngine;

public static class UniqueIDGenerator
{
    /// <summary>
    /// 產生一個不重複的ID (GUID)
    /// </summary>
    /// <param name="removeHyphens">是否移除字串中的連字號 '-'</param>
    /// <returns>GUID字串</returns>
    public static string GenerateUniqueID(bool removeHyphens = false)
    {
        string guid = Guid.NewGuid().ToString(); // 例: "e5b57af1-2b4d-4e6f-8df5-72f63042ecf8"
        if (removeHyphens)
        {
            // 去除 '-'
            guid = guid.Replace("-", "");
            // 例: "e5b57af12b4d4e6f8df572f63042ecf8"
        }
        return guid;
    }
}

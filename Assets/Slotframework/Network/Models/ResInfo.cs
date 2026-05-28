
// Game Server response
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class ResInfo
{
    public int code;
    public int seq;
    public string msg;
    public string cmd;
    public string trace;
    public ulong balance;
    public JToken  data ;
    
}

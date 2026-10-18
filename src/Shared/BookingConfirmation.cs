using System.Text.RegularExpressions;
namespace TsRecommend;
static partial class Data
{
    internal static string BookingTitleKey(string title)=>Norm(Regex.Replace(Clean(title),@"\[(?:生|再|字|デ|解|二|多|新|終|無|無料|映|双|S|SS|5\.1)\]","",RegexOptions.IgnoreCase));
}

using TsRecommend;
using TsScheduler;
namespace TsScheduler.Tests;
internal static class GuideReservationTests
{
    internal static async Task Run()
    {
        var start=new DateTimeOffset(2030,1,1,12,0,0,TimeSpan.FromHours(9));
        var show=new Show("1/2/3",1,2,"TBS","旧タイトル","",start,start.AddMinutes(30),new(){{5,"day?r=1&c=1&e=2&d=5"}});
        var updated=show with{Title="最新タイトル",Description="更新された詳細"};
        var verified=GuideReservationTransaction.Verify(show,[updated],5);
        if(verified.Title!=updated.Title)throw new Exception("Updated EPG title should not block the same event");
        foreach(var stale in new[]{updated with{EventId=3},updated with{End=updated.End.AddMinutes(1)},updated with{Start=start.AddMinutes(1)},updated with{Station="フジ"},updated with{Actions=new(){{5,"day?r=1&c=1&e=99&d=5"}}}}){
            bool rejected=false;try{GuideReservationTransaction.Verify(show,[stale],5);}catch(Exception e)when(e is InvalidOperationException or InvalidDataException){rejected=true;}
            if(!rejected)throw new Exception("Stale/different event must not be submitted");
        }
        var booking=new Booking("new",updated.Title,updated.Station,1,2,start,updated.End,5,true,true,false,[]);
        var rows=new List<Booking>();int sends=0,reads=0;
        Task<string> Get(string action){if(action!=updated.Actions[5])throw new Exception("Wrong reservation route");sends++;rows.Add(booking);return Task.FromResult("");}
        await GuideReservationTransaction.Send(verified,5,Get,()=>{reads++;return rows;},()=>Task.CompletedTask);
        if(sends!=1||reads!=1||rows.Count!=1)throw new Exception("Reservation send and read-back must complete once");
        rows.Clear();sends=0;
        Task<string> LostResponse(string action){sends++;rows.Add(booking);throw new HttpRequestException("Response lost after server saved");}
        await GuideReservationTransaction.Send(verified,5,LostResponse,()=>rows,()=>Task.CompletedTask);
        if(sends!=1)throw new Exception("Lost response must not resubmit a saved booking");
        rows.Clear();sends=0;
        Task<string> Failed(string action){sends++;throw new HttpRequestException("Not saved");}
        bool failed=false;try{await GuideReservationTransaction.Send(verified,5,Failed,()=>rows,()=>Task.CompletedTask);}catch(IOException){failed=true;}
        if(!failed||sends!=1)throw new Exception("Unconfirmed send must fail without retry");
        Console.WriteLine("PASS: TVRock reservation identity, updated title, submit/read-back, lost response and no automatic duplicate retry");
    }
}

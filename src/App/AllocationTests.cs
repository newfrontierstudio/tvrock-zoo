namespace TsScheduler;
internal static class AllocationTests
{
    internal static void Run()
    {
        var time=new DateTimeOffset(2030,1,1,10,0,0,TimeSpan.Zero);
        AllocationTask Task(string id,int current,int[] candidates,int start=0,int end=60,bool movable=true)=>new(id,id,time.AddMinutes(start),time.AddMinutes(end),current,candidates,movable);
        void Check(bool good,string message){if(!good)throw new Exception(message);}
        var added=Task("new",-1,[1]);var a=Task("a",1,[1,2]);
        Check(TunerAllocation.Free(added,[a]).Count==0,"An occupied tuner must not be suggested as free");
        var plan=TunerAllocation.Reassign(added,[a],d=>d.ToString());Check(plan!=null&&plan.Device==1&&plan.Assignments["a"]==2&&plan.Changes.Length==1,"Move one compatible reservation to free the required tuner");
        Check(TunerAllocation.Reassign(added,[a with{Movable=false}],d=>d.ToString())==null,"Recording and near-start reservations stay fixed");
        Check(TunerAllocation.Reassign(added,[a with{Candidates=[1]}],d=>d.ToString())==null,"Unsupported channels must not be relocated");
        Check(TunerAllocation.Free(added,[Task("touch",1,[1],60,90)]).SequenceEqual([1]),"Adjacent nonoverlapping reservation is allowed");
        var b=Task("b",2,[2,3],30,100);var c=Task("c",3,[3],90,120);
        Check(TunerAllocation.Connected(added,[a,b,c]).Count==4,"Include the full overlap chain");
        Check(TunerAllocation.Reassign(added,[a,b,c],d=>d.ToString())==null,"A downstream reservation must prevent an invalid relocation chain");
        var free=Task("free",-1,[1,2,3]);Check(TunerAllocation.Free(free,[a,b]).SequenceEqual([3]),"List every free tuner");
        Check(TunerAllocation.Reassign(free,[a,b],d=>d.ToString())?.Changes.Length==0,"Prefer zero existing changes when free capacity exists");
    }
}

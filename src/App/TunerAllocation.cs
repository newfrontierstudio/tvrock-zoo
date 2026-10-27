namespace TsScheduler;

internal sealed record AllocationTask(string Id,string Title,DateTimeOffset Start,DateTimeOffset End,int Current,int[] Candidates,bool Movable);
internal sealed record AllocationChoice(int Device,Dictionary<string,int> Assignments,string[] Changes);
internal static class TunerAllocation
{
    internal static bool Overlap(AllocationTask a,AllocationTask b)=>a.Start<b.End&&b.Start<a.End;
    internal static List<AllocationTask> Connected(AllocationTask added,IEnumerable<AllocationTask> existing)
    {
        var result=new List<AllocationTask>{added};var rest=existing.ToList();
        bool changed;do{changed=false;foreach(var task in rest.ToArray())if(result.Any(t=>Overlap(t,task))){result.Add(task);rest.Remove(task);changed=true;}}while(changed);
        return result;
    }
    internal static List<int> Free(AllocationTask added,IEnumerable<AllocationTask> existing)=>added.Candidates.Where(d=>!existing.Any(b=>b.Current==d&&Overlap(added,b))).ToList();
    internal static AllocationChoice? Reassign(AllocationTask added,IEnumerable<AllocationTask> existing,Func<int,string> label)
    {
        var tasks=Connected(added,existing);if(tasks.Any(t=>t.Candidates.Length==0))return null;
        var order=tasks.OrderBy(t=>t.Movable?1:0).ThenBy(t=>t.Candidates.Length).ThenBy(t=>t.Start).ThenBy(t=>t.Id,StringComparer.Ordinal).ToList();
        var selected=new Dictionary<string,int>();int steps=0;
        bool Solve(int n,int left)
        {
            if(++steps>200000)return false;if(n==order.Count)return true;var task=order[n];
            foreach(var device in task.Candidates.OrderBy(d=>d==task.Current?0:1).ThenBy(d=>d))
            {
                int cost=task.Id==added.Id||device==task.Current?0:1;if(cost>left||(!task.Movable&&device!=task.Current))continue;
                if(order.Take(n).Any(other=>selected.GetValueOrDefault(other.Id,-1)==device&&Overlap(task,other)))continue;
                selected[task.Id]=device;if(Solve(n+1,left-cost))return true;selected.Remove(task.Id);
            }
            return false;
        }
        for(int changes=0;changes<=tasks.Count;changes++){selected.Clear();if(Solve(0,changes))return new(selected[added.Id],new(selected),tasks.Where(t=>t.Id!=added.Id&&selected[t.Id]!=t.Current).Select(t=>$"{t.Start.ToLocalTime():MM/dd HH:mm} {t.Title}：{label(t.Current)} → {label(selected[t.Id])}").ToArray());if(steps>200000)return null;}
        return null;
    }
    internal static AllocationChoice? Choose(IWin32Window owner,AllocationTask added,List<AllocationTask> existing,Func<int,string> label,bool manual=true)
    {
        var free=Free(added,existing);
        if(!manual&&free.Count>0)return new(free[0],new(){{added.Id,free[0]}},[]);
        using var dialog=new Form{Text="おすすめの空き録画チャンネル",Font=new Font("Meiryo UI",10),ClientSize=new Size(600,370),StartPosition=FormStartPosition.CenterParent,MinimumSize=new Size(550,330)};
        var text=new Label{Dock=DockStyle.Top,Height=75,Padding=new Padding(14),Text=$"{added.Title}\n{added.Start.ToLocalTime():MM/dd HH:mm} ～ {added.End.ToLocalTime():HH:mm}"};
        var list=new ListBox{Dock=DockStyle.Fill,IntegralHeight=false};var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=55,Padding=new Padding(10),FlowDirection=FlowDirection.RightToLeft};
        var cancel=new Button{Text="キャンセル",AutoSize=true,DialogResult=DialogResult.Cancel};var confirm=new Button{Text=free.Count>0?"このチューナーで予約":"再配置案を確認",AutoSize=true,Enabled=free.Count>0||added.Candidates.Length>0};buttons.Controls.AddRange([cancel,confirm]);dialog.Controls.Add(list);dialog.Controls.Add(text);dialog.Controls.Add(buttons);dialog.CancelButton=cancel;
        foreach(int d in free)list.Items.Add((d==free[0]?"おすすめ　":"変更候補　")+label(d)+"　予約上の空き");if(free.Count>0)list.SelectedIndex=0;else list.Items.Add("空きがありません。再配置で確保できるか確認します。");
        AllocationChoice? choice=null;
        confirm.Click+=(_,_)=>{
            if(free.Count>0){if(list.SelectedIndex<0)return;int d=free[list.SelectedIndex];choice=new(d,new(){{added.Id,d}},[]);dialog.DialogResult=DialogResult.OK;return;}
            var plan=Reassign(added,existing,label);if(plan==null){MessageBox.Show(dialog,"録画中・開始直前の予約を動かさずに確保できる組み合わせがありません。","再配置できません");return;}
            string preview="新しい予約："+label(plan.Device)+"\n\n"+string.Join("\n",plan.Changes)+"\n\n番組・録画日時は変更せず、割り当てだけ変更します。適用して予約しますか？";
            if(MessageBox.Show(dialog,preview,"再配置案",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return;choice=plan;dialog.DialogResult=DialogResult.OK;
        };
        return dialog.ShowDialog(owner)==DialogResult.OK?choice:null;
    }
}
